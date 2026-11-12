using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.Extensions;
using App.Agent.Daemon.Infrastructure.Plugins;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Services.Pki;
using App.Backend.Api.Services.Plugins;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Errors;
using App.Shared.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// Plugins: the installing user signs the package with their user certificate (Heimdall- or
/// AD-issued), the backend verifies and countersigns with the server certificate, the agent verifies
/// both chains against its trusted CAs before installing.
/// </summary>
public class PluginDoubleSigningTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "heimdall_plugin_dsig_" + Guid.NewGuid().ToString("N"));
    private readonly AppDbContext _pluginDb = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"plugins-{Guid.NewGuid()}").Options);

    public void Dispose()
    {
        _pluginDb.Dispose();
        try { Directory.Delete(_sandbox, true); } catch { }
    }

    private static byte[] Package(string pluginId = "spindle-monitor", string code = "print('ok')") =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            manifest = new { pluginId, name = "Spindle Monitor", version = "1.0.0", entrypoint = "main.py", runtimeType = "Python" },
            files = new Dictionary<string, string> { ["main.py"] = code }
        });

    private static PluginSignatureEnvelope UserSign(X509Certificate2 userCertWithKey, byte[] package, string? chainPem = null) =>
        PluginSignatures.Sign(userCertWithKey, chainPem ?? userCertWithKey.ExportCertificatePem(),
            SHA256.HashData(package), PluginSignatures.Sha256Hex(package), PluginSignatures.UserFormat);

    private static async Task<(PkiService Pki, PkiTrustAndMappingTests.Factory Db, X509Certificate2 UserCert)> HeimdallUser(string userId = "usr-eng-1")
    {
        var (pki, db) = PkiTrustAndMappingTests.NewPki();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.AuthUsers.Add(new AuthUser { Id = userId, Email = "jane.doe@factory.corp", Name = "Jane Doe", Role = "controls_engineer" });
            await ctx.SaveChangesAsync();
        }
        var issued = await pki.IssueAsync(new IssueCertificateRequest { Kind = "user", UserPrincipalName = "jane.doe@factory.corp" }, "admin@test");
        var userCert = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(issued.Pkcs12Base64!), issued.Pkcs12Password);
        return (pki, db, userCert);
    }

    private PluginManager Agent(string? trustedPem, string environment = "Production")
    {
        var config = new AgentConfig { Environment = environment, AllowUnsignedPlugins = false, TrustedCaCertificatesPem = trustedPem, SandboxesDirectory = _sandbox };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        return new PluginManager(configService, new PluginSandboxService(configService, NullLogger<PluginSandboxService>.Instance),
            new ExtensionRegistry(configService, NullLogger<ExtensionRegistry>.Instance), NullLogger<PluginManager>.Instance);
    }

    [Fact]
    public async Task HeimdallIssuedUserSignature_IsCountersigned_AndInstalledByTheAgent()
    {
        var (pki, _, userCert) = await HeimdallUser();
        var service = new PluginService(_pluginDb, NullLogger<PluginService>.Instance, pki);
        var package = Package();

        var bundle = await service.RegisterSignedPluginAsync(package, UserSign(userCert, package), "usr-eng-1");

        Assert.NotNull(bundle.ServerSignature);
        Assert.Equal(PluginSignatures.ServerFormat, bundle.ServerSignature!.Format);
        Assert.Equal(PluginSignatures.Sha256Hex(package), bundle.ServerSignature.PackageSha256);
        Assert.Equal("spindle-monitor", bundle.Manifest.PluginId);

        // The server certificate was created on first use and is kept with its (encrypted) key.
        var check = PluginSignatures.VerifyDoubleSigned(package, bundle.UserSignature, bundle.ServerSignature, await pki.LoadTrustedCasAsync());
        Assert.True(check.IsValid, check.Reason);
        Assert.Equal("urn:heimdall:machine:heimdall-server", CertificateIdentity.FromCertificate(check.ServerCertificate!).Uris.Single());

        var agent = Agent(await pki.GetTrustBundlePemAsync());
        var result = await agent.InstallPluginAsync(bundle);
        Assert.True(result.Success, result.Message);
        Assert.False(result.IsSandboxed);
        Assert.True(agent.GetInstalledPlugins().Single().IsSigned);
    }

    [Fact]
    public async Task AdIssuedUserSignature_IsAccepted_WhenTheAdChainIsTrusted()
    {
        var (pki, db) = PkiTrustAndMappingTests.NewPki();
        using var adRoot = PkiTrustAndMappingTests.MakeCa("CN=Factory AD Root CA");
        using var adIssuing = PkiTrustAndMappingTests.MakeCa("CN=Factory AD Issuing CA", adRoot);
        await pki.ImportTrustedCasAsync(adRoot.ExportCertificatePem(), TrustSources.ActiveDirectory, null, "admin@test");
        await using (var ctx = db.CreateDbContext())
        {
            ctx.AuthUsers.Add(new AuthUser { Id = "usr-ad-7", Email = "max.muster@factory.corp", Name = "Max Muster" });
            await ctx.SaveChangesAsync();
        }
        using var userCert = PkiTrustAndMappingTests.MakeLeaf(adIssuing, "CN=Max Muster", s => { s.AddUserPrincipalName("max.muster@factory.corp"); s.AddEmailAddress("max.muster@factory.corp"); });
        var service = new PluginService(_pluginDb, NullLogger<PluginService>.Instance, pki);
        var package = Package();

        // The AD issuing CA travels in the envelope's chain; only the AD root is in the trust store.
        var envelope = UserSign(userCert, package, userCert.ExportCertificatePem() + "\n" + adIssuing.ExportCertificatePem());
        var bundle = await service.RegisterSignedPluginAsync(package, envelope, "usr-ad-7");

        var result = await Agent(await pki.GetTrustBundlePemAsync()).InstallPluginAsync(bundle);
        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task Backend_RejectsForeignTamperedAndUntrustedUserSignatures()
    {
        var (pki, _, userCert) = await HeimdallUser();
        var service = new PluginService(_pluginDb, NullLogger<PluginService>.Instance, pki);
        var package = Package();
        var envelope = UserSign(userCert, package);

        // Signed by Jane, uploaded by someone else.
        var foreign = await Assert.ThrowsAsync<PluginSignatureException>(() => service.RegisterSignedPluginAsync(package, envelope, "usr-other"));
        Assert.Contains("own certificate", foreign.Message);

        // Package changed after signing.
        await Assert.ThrowsAsync<PluginSignatureException>(() => service.RegisterSignedPluginAsync(Package(code: "import os; os.system('rm -rf /')"), envelope, "usr-eng-1"));

        // Signature by a certificate from a CA that is not trusted.
        using var rogueCa = PkiTrustAndMappingTests.MakeCa("CN=Rogue CA");
        using var rogueUser = PkiTrustAndMappingTests.MakeLeaf(rogueCa, "CN=Jane", s => s.AddUserPrincipalName("jane.doe@factory.corp"));
        var untrusted = await Assert.ThrowsAsync<PluginSignatureException>(() => service.RegisterSignedPluginAsync(package, UserSign(rogueUser, package), "usr-eng-1"));
        Assert.Contains("not trusted", untrusted.Message);
    }

    [Fact]
    public async Task Agent_RejectsSingleSignedTamperedAndUntrustedBundles_OutsideDevMode()
    {
        var (pki, _, userCert) = await HeimdallUser();
        var service = new PluginService(_pluginDb, NullLogger<PluginService>.Instance, pki);
        var package = Package();
        var bundle = await service.RegisterSignedPluginAsync(package, UserSign(userCert, package), "usr-eng-1");
        var trust = await pki.GetTrustBundlePemAsync();

        // Only the user signature.
        var single = await Agent(trust).InstallPluginAsync(bundle with { ServerSignature = null });
        Assert.False(single.Success);
        Assert.Equal(ErrorCode.PluginSignatureInvalid, single.ErrorCode);

        // Files swapped after signing (the signed package bytes are unchanged).
        var tampered = await Agent(trust).InstallPluginAsync(bundle with { Files = new Dictionary<string, string> { ["main.py"] = "print('evil')" } });
        Assert.False(tampered.Success);

        // Countersignature made with a user certificate instead of the server certificate.
        var forgedServer = PluginSignatures.Sign(userCert, userCert.ExportCertificatePem(),
            PluginSignatures.CountersignedHash(bundle.UserSignature!.PackageSha256, bundle.UserSignature.Signature),
            bundle.UserSignature.PackageSha256, PluginSignatures.ServerFormat);
        Assert.False((await Agent(trust).InstallPluginAsync(bundle with { ServerSignature = forgedServer })).Success);

        // Agent without the CA in its trust material.
        using var otherRoot = PkiTrustAndMappingTests.MakeCa("CN=Some Other Root");
        Assert.False((await Agent(otherRoot.ExportCertificatePem()).InstallPluginAsync(bundle)).Success);

        // Dev mode still installs an unsigned plugin into the sandbox.
        var dev = await Agent(null, environment: "Development").InstallPluginAsync(bundle with { UserSignature = null, ServerSignature = null });
        Assert.True(dev.Success);
    }

    [Fact]
    public void PluginController_RequiresThePolicyOnEveryEndpoint()
    {
        var attribute = typeof(PluginController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal(AuthorizationPolicies.EndpointConfigManagement, attribute.Policy);
        Assert.DoesNotContain(typeof(PluginController).GetMethods(), m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any());
    }
}
