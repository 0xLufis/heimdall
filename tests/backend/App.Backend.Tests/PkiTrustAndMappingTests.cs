using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using App.Backend.Api.Services.Pki;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// Trust store, chain validation (self-signed root, subordinate root, AD-like external CA, expired,
/// wrong CA, revoked) and identity mapping of user and computer certificates.
/// </summary>
public class PkiTrustAndMappingTests
{
    internal sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory() => _options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"pki-{Guid.NewGuid()}").Options;
        public AppDbContext CreateDbContext() => new(_options);
    }

    internal static (PkiService Pki, Factory Db) NewPki()
    {
        var db = new Factory();
        return (new PkiService(db, NullLogger<PkiService>.Instance), db);
    }

    internal static X509Certificate2 MakeCa(string subject, X509Certificate2? parent = null, int days = 3650)
    {
        using var key = RSA.Create(2048);
        var req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
        if (parent == null) return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-90), DateTimeOffset.UtcNow.AddDays(days));
        using var cert = req.Create(parent, parent.NotBefore, DateTimeOffset.UtcNow.AddDays(Math.Min(days, 3000)), RandomNumberGenerator.GetBytes(8));
        return cert.CopyWithPrivateKey(key);
    }

    internal static X509Certificate2 MakeLeaf(X509Certificate2 issuer, string subject, Action<SubjectAlternativeNameBuilder>? san = null,
        DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var key = RSA.Create(2048);
        var req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        if (san != null)
        {
            var builder = new SubjectAlternativeNameBuilder();
            san(builder);
            req.CertificateExtensions.Add(builder.Build());
        }
        var issuerNotBefore = new DateTimeOffset(issuer.NotBefore.ToUniversalTime());
        var from = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);
        if (from < issuerNotBefore) from = issuerNotBefore;
        using var cert = req.Create(issuer, from, notAfter ?? DateTimeOffset.UtcNow.AddDays(365), RandomNumberGenerator.GetBytes(8));
        return cert.CopyWithPrivateKey(key);
    }

    private static async Task<ClientPc> AddPc(Factory factory, string hostname, string? machineIdentifier = null, string? thumbprint = null)
    {
        await using var db = factory.CreateDbContext();
        var pc = new ClientPc { Id = Guid.NewGuid(), Name = hostname, Hostname = hostname, MachineIdentifier = machineIdentifier, CertificateThumbprint = thumbprint, MacAddress = "00:00:00:00:00:01" };
        db.ClientPcs.Add(pc);
        await db.SaveChangesAsync();
        return pc;
    }

    [Fact]
    public async Task SelfSignedHeimdallRoot_IssuesComputerCertificate_ThatValidatesAndMapsByMachineUri()
    {
        var (pki, factory) = NewPki();
        var pc = await AddPc(factory, "IPC-L03-CONVEYOR-MAIN", "HW-IPC-L03-CONVEYOR-MAIN");

        var issued = await pki.IssueAsync(new IssueCertificateRequest { Kind = "computer", Hostname = "IPC-L03-CONVEYOR-MAIN", MachineIdentifier = "HW-IPC-L03-CONVEYOR-MAIN", ClientPcId = pc.Id }, "admin@test");
        Assert.NotNull(issued.Pkcs12Base64);

        var ca = (await pki.ListTrustStoreAsync()).Single();
        Assert.Equal(TrustSources.HeimdallSelfSigned, ca.Source);
        Assert.Equal("Active", ca.Status);
        Assert.NotNull(ca.PrivateKeyPem);

        var result = await pki.ValidateAsync(issued.CertificatePem);
        Assert.True(result.Valid, result.Reason);
        Assert.True(result.HeimdallIssued);
        Assert.Equal(TrustSources.HeimdallSelfSigned, result.IssuerSource);
        Assert.Equal("urn:heimdall:machine:HW-IPC-L03-CONVEYOR-MAIN", result.Identity!.Uris.Single());
        Assert.Equal("IPC-L03-CONVEYOR-MAIN", result.Identity.DnsNames.Single());
        Assert.Equal(pc.Id, result.Computer!.ClientPcId);
        Assert.Equal("machine-uri", result.Computer.MappedBy);
    }

    [Fact]
    public async Task SubordinateHeimdallRoot_CsrSignedByAdCa_IsValidatedOnImport_AndIssuesUserCertificates()
    {
        var (pki, factory) = NewPki();
        using var adRoot = MakeCa("CN=Factory AD Root CA, DC=factory, DC=corp");

        var pending = await pki.CreateSubordinateRequestAsync("CN=Heimdall Issuing CA, O=Factory", "admin@test");
        Assert.Equal("PendingSignature", pending.Status);
        Assert.Contains("BEGIN CERTIFICATE REQUEST", pending.CsrPem);

        // A wrong certificate (other key) is rejected.
        using var stranger = MakeCa("CN=Stranger CA", adRoot);
        await Assert.ThrowsAsync<ArgumentException>(() => pki.CompleteSubordinateAsync(stranger.ExportCertificatePem(), adRoot.ExportCertificatePem(), "admin@test"));

        // The AD CA signs the CSR (SubCA template).
        var csr = CertificateRequest.LoadSigningRequestPem(pending.CsrPem!, HashAlgorithmName.SHA256, CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions, RSASignaturePadding.Pkcs1);
        using var subCa = csr.Create(adRoot, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5), RandomNumberGenerator.GetBytes(8));
        var active = await pki.CompleteSubordinateAsync(subCa.ExportCertificatePem(), adRoot.ExportCertificatePem(), "admin@test");
        Assert.Equal("Active", active.Status);
        Assert.Equal(TrustSources.HeimdallSubordinate, active.Source);

        var store = await pki.ListTrustStoreAsync();
        Assert.Contains(store, c => c.Thumbprint == adRoot.Thumbprint && c.Source == TrustSources.ActiveDirectory);

        await using (var db = factory.CreateDbContext())
        {
            db.AuthUsers.Add(new AuthUser { Id = "usr-eng-1", Email = "Jane.Doe@factory.corp", Name = "Jane Doe", Role = "controls_engineer" });
            await db.SaveChangesAsync();
        }

        var issued = await pki.IssueAsync(new IssueCertificateRequest { Kind = "user", UserPrincipalName = "jane.doe@factory.corp", Email = "jane.doe@factory.corp" }, "admin@test");
        var result = await pki.ValidateAsync(issued.CertificatePem);
        Assert.True(result.Valid, result.Reason);
        Assert.Equal(TrustSources.HeimdallSubordinate, result.IssuerSource);
        Assert.Equal(TrustSources.ActiveDirectory, result.AnchorSource);
        Assert.Equal("jane.doe@factory.corp", result.Identity!.UserPrincipalNames.Single());
        Assert.Equal("usr-eng-1", result.User!.UserId);
    }

    [Fact]
    public async Task AdLikeExternalCa_ComputerCertificate_MapsByDnsHostname_WithRevocationGapWarning()
    {
        var (pki, factory) = NewPki();
        using var adRoot = MakeCa("CN=Factory AD Root CA");
        using var adIssuing = MakeCa("CN=Factory AD Issuing CA", adRoot);
        await pki.ImportTrustedCasAsync(adRoot.ExportCertificatePem() + "\n" + adIssuing.ExportCertificatePem(), TrustSources.ActiveDirectory, "AD CS", "admin@test");
        var pc = await AddPc(factory, "IPC-L06-ROB-01");

        using var leaf = MakeLeaf(adIssuing, "CN=IPC-L06-ROB-01.factory.corp", s => s.AddDnsName("IPC-L06-ROB-01.factory.corp"));
        var result = await pki.ValidateAsync(leaf.ExportCertificatePem());

        Assert.True(result.Valid, result.Reason);
        Assert.False(result.HeimdallIssued);
        Assert.Equal(TrustSources.ActiveDirectory, result.IssuerSource);
        Assert.Contains(result.Warnings, w => w.Contains("not checked"));
        Assert.Equal(pc.Id, result.Computer!.ClientPcId);
        Assert.Equal("dns-hostname", result.Computer.MappedBy);
    }

    [Fact]
    public async Task ComputerCertificate_WithoutMatchingSan_MapsByRegisteredThumbprint()
    {
        var (pki, factory) = NewPki();
        using var adRoot = MakeCa("CN=Factory AD Root CA");
        await pki.ImportTrustedCasAsync(adRoot.ExportCertificatePem(), TrustSources.ActiveDirectory, null, "admin@test");
        using var leaf = MakeLeaf(adRoot, "CN=kiosk-7", s => s.AddDnsName("kiosk-7.other.domain"));
        var pc = await AddPc(factory, "KIOSK-OP10", thumbprint: leaf.Thumbprint);

        var result = await pki.ValidateAsync(leaf.ExportCertificatePem());
        Assert.True(result.Valid, result.Reason);
        Assert.Equal(pc.Id, result.Computer!.ClientPcId);
        Assert.Equal("thumbprint", result.Computer.MappedBy);
    }

    [Fact]
    public async Task ExpiredCertificate_IsRejected()
    {
        var (pki, _) = NewPki();
        using var adRoot = MakeCa("CN=Factory AD Root CA");
        await pki.ImportTrustedCasAsync(adRoot.ExportCertificatePem(), TrustSources.ActiveDirectory, null, "admin@test");
        using var leaf = MakeLeaf(adRoot, "CN=old-kiosk", s => s.AddDnsName("old-kiosk"),
            DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));

        var result = await pki.ValidateAsync(leaf.ExportCertificatePem());
        Assert.False(result.Valid);
        Assert.Equal("Expired", result.Status);
    }

    [Fact]
    public async Task CertificateFromAnUntrustedCa_IsRejected()
    {
        var (pki, _) = NewPki();
        using var trusted = MakeCa("CN=Trusted Root");
        using var rogue = MakeCa("CN=Rogue Root");
        await pki.ImportTrustedCasAsync(trusted.ExportCertificatePem(), TrustSources.Imported, null, "admin@test");
        using var leaf = MakeLeaf(rogue, "CN=evil", s => s.AddDnsName("IPC-L03-CONVEYOR-MAIN"));

        var result = await pki.ValidateAsync(leaf.ExportCertificatePem());
        Assert.False(result.Valid);
        Assert.Equal("UntrustedIssuer", result.Status);
        Assert.Null(result.Computer);

        // Presenting the rogue root as "chain" does not make it trusted.
        var withChain = await pki.ValidateAsync(leaf.ExportCertificatePem(), rogue.ExportCertificatePem());
        Assert.Equal("UntrustedIssuer", withChain.Status);
    }

    [Fact]
    public async Task RevokedHeimdallCertificate_IsRejected_AndUnregisteredOnesToo()
    {
        var (pki, factory) = NewPki();
        var issued = await pki.IssueAsync(new IssueCertificateRequest { Kind = "computer", Hostname = "IPC-1" }, "admin@test");
        Assert.True((await pki.ValidateAsync(issued.CertificatePem)).Valid);

        await using (var db = factory.CreateDbContext())
        {
            var record = await db.ClientCertificates.SingleAsync(c => c.Id == issued.Record.Id);
            record.Status = "Revoked";
            await db.SaveChangesAsync();
        }
        var revoked = await pki.ValidateAsync(issued.CertificatePem);
        Assert.False(revoked.Valid);
        Assert.Equal("Revoked", revoked.Status);

        // Signed with the Heimdall CA key but never recorded (e.g. key misuse): fail closed.
        var caRow = (await pki.ListTrustStoreAsync()).Single();
        using var ca = X509Certificate2.CreateFromPem(caRow.RawPem!, caRow.PrivateKeyPem!);
        using var rogueLeaf = MakeLeaf(ca, "CN=unrecorded", s => s.AddDnsName("IPC-1"));
        Assert.Equal("Unregistered", (await pki.ValidateAsync(rogueLeaf.ExportCertificatePem())).Status);
    }

    [Fact]
    public async Task TrustStore_RejectsNonCaImports_AndDisabledCasStopValidating()
    {
        var (pki, _) = NewPki();
        using var root = MakeCa("CN=Root");
        using var leaf = MakeLeaf(root, "CN=leaf", s => s.AddDnsName("leaf"));
        await Assert.ThrowsAsync<ArgumentException>(() => pki.ImportTrustedCasAsync(leaf.ExportCertificatePem(), TrustSources.Imported, null, "admin@test"));

        var row = (await pki.ImportTrustedCasAsync(root.ExportCertificatePem(), TrustSources.Imported, null, "admin@test")).Single();
        Assert.True((await pki.ValidateAsync(leaf.ExportCertificatePem())).Valid);

        await pki.SetTrustStatusAsync(row.Id, enabled: false);
        Assert.Equal("UntrustedIssuer", (await pki.ValidateAsync(leaf.ExportCertificatePem())).Status);

        // A CA certificate is not accepted as an end-entity identity.
        await pki.SetTrustStatusAsync(row.Id, enabled: true);
        using var sub = MakeCa("CN=Sub", root);
        Assert.Equal("NotAnEndEntity", (await pki.ValidateAsync(sub.ExportCertificatePem())).Status);
    }

    [Fact]
    public async Task WorkstationIdentity_ReturnsTheStationsBoundToTheController()
    {
        var (pki, factory) = NewPki();
        var pc = await AddPc(factory, "IPC-L03-CONVEYOR-MAIN", "HW-IPC-L03-CONVEYOR-MAIN");
        var other = await AddPc(factory, "IPC-OTHER");
        var stationId = Guid.NewGuid();
        await using (var db = factory.CreateDbContext())
        {
            db.Machines.Add(new Machine { Id = stationId, Name = "OP010", CustomIdentifier = "LINE-03-OP010" });
            db.Machines.Add(new Machine { Id = Guid.NewGuid(), Name = "OP900", CustomIdentifier = "LINE-09-OP900" });
            db.StationControllers.Add(new StationController { Id = Guid.NewGuid(), MachineId = stationId, ClientPcId = pc.Id });
            await db.SaveChangesAsync();
        }

        var issued = await pki.IssueAsync(new IssueCertificateRequest { Kind = "computer", Hostname = "IPC-L03-CONVEYOR-MAIN", MachineIdentifier = "HW-IPC-L03-CONVEYOR-MAIN" }, "admin@test");
        var identity = await pki.ResolveWorkstationAsync(issued.CertificatePem);
        Assert.True(identity.Valid, identity.Reason);
        Assert.Equal(pc.Id, identity.Computer!.ClientPcId);
        Assert.Contains("LINE-03-OP010", identity.StationKeys);
        Assert.Contains(stationId.ToString(), identity.StationKeys);
        Assert.DoesNotContain("LINE-09-OP900", identity.StationKeys);

        var unmapped = await pki.IssueAsync(new IssueCertificateRequest { Kind = "computer", Hostname = "NOT-REGISTERED" }, "admin@test");
        var none = await pki.ResolveWorkstationAsync(unmapped.CertificatePem);
        Assert.False(none.Valid);
        Assert.Equal("Unmapped", none.Status);
        Assert.NotEqual(other.Id, none.Computer?.ClientPcId);
    }
}
