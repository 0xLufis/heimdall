namespace App.Backend.Api.Services.Pki;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Heimdall's PKI: the trust store (<c>trusted_ca_certificates</c>), Heimdall's own CA (self-signed
/// or subordinate to an AD / enterprise CA), issuance of user and computer certificates, chain
/// validation with revocation of Heimdall-issued certificates, and identity mapping
/// (user certificate → Better-Auth user, computer certificate → ClientPc).
/// BCL only: X509Certificate2, X509Chain, CertificateRequest, RSA.
/// </summary>
public class PkiService
{
    public const string HeimdallRootSubject = "CN=Heimdall Project Industrial Root CA, O=Enterprise Factory Automation, C=US";
    public const string ServerSigningProfile = "Heimdall-Server-Signing";
    public static readonly string[] TrustedStatuses = { "Active", "Superseded" };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<PkiService> _logger;
    private readonly X509RevocationMode _externalRevocationMode;

    public PkiService(IDbContextFactory<AppDbContext> dbFactory, ILogger<PkiService> logger, IConfiguration? configuration = null)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _externalRevocationMode = Enum.TryParse<X509RevocationMode>(configuration?["Heimdall:Pki:ExternalRevocationMode"], true, out var mode)
            ? mode
            : X509RevocationMode.NoCheck;
    }

    // ------------------------------------------------------------------ trust store

    public async Task<List<TrustedCaCertificate>> ListTrustStoreAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TrustedCaCertificates.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync();
    }

    /// <summary>Trusted CA certificates (Active or Superseded) with their sources.</summary>
    public async Task<List<TrustedCa>> LoadTrustedCasAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.TrustedCaCertificates.AsNoTracking()
            .Where(c => TrustedStatuses.Contains(c.Status) && c.RawPem != null)
            .ToListAsync();
        var result = new List<TrustedCa>();
        foreach (var row in rows)
        {
            try { result.Add(new TrustedCa(X509Certificate2.CreateFromPem(row.RawPem!), row.Source)); }
            catch (CryptographicException ex) { _logger.LogWarning(ex, "Skipping unreadable trusted CA {Id}", row.Id); }
        }
        return result;
    }

    /// <summary>PEM bundle of all trusted CA certificates (public data; agents use it to verify plugin signatures).</summary>
    public async Task<string> GetTrustBundlePemAsync()
    {
        var cas = await LoadTrustedCasAsync();
        return string.Join("\n", cas.Select(c => c.Certificate.ExportCertificatePem()));
    }

    /// <summary>
    /// Imports CA certificates (one or more PEM blocks, e.g. an AD CS root + issuing CA) into the trust
    /// store. Every certificate must be a CA (basicConstraints cA=TRUE) and currently valid.
    /// </summary>
    public async Task<List<TrustedCaCertificate>> ImportTrustedCasAsync(string pem, string source, string? notes, string? createdBy)
    {
        if (source != TrustSources.ActiveDirectory && source != TrustSources.Imported)
            throw new ArgumentException($"Source must be '{TrustSources.ActiveDirectory}' or '{TrustSources.Imported}'.");
        List<X509Certificate2> certs;
        try { certs = CertificateChainValidator.ParsePemBundle(pem); }
        catch (CryptographicException ex) { throw new ArgumentException($"Not a PEM certificate bundle: {ex.Message}"); }
        if (certs.Count == 0) throw new ArgumentException("No certificate found in the PEM text.");

        var now = DateTimeOffset.UtcNow;
        foreach (var cert in certs)
        {
            if (!CertificateChainValidator.IsCertificateAuthority(cert))
                throw new ArgumentException($"'{cert.Subject}' is not a CA certificate (basicConstraints cA=TRUE required).");
            if (cert.NotAfter.ToUniversalTime() < now.UtcDateTime || cert.NotBefore.ToUniversalTime() > now.UtcDateTime)
                throw new ArgumentException($"'{cert.Subject}' is outside its validity period.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var imported = new List<TrustedCaCertificate>();
        foreach (var cert in certs)
        {
            var existing = await db.TrustedCaCertificates.FirstOrDefaultAsync(c => c.Thumbprint == cert.Thumbprint);
            if (existing != null)
            {
                if (existing.Status == "Disabled") { existing.Status = "Active"; existing.UpdatedAt = now; }
                imported.Add(existing);
                continue;
            }
            var row = ToTrustRow(cert, source, "Active", createdBy);
            row.Notes = notes;
            db.TrustedCaCertificates.Add(row);
            imported.Add(row);
        }
        await db.SaveChangesAsync();
        _logger.LogInformation("Imported {Count} CA certificate(s) into the trust store (source {Source}) by {User}", imported.Count, source, createdBy);
        return imported;
    }

    public async Task<TrustedCaCertificate?> SetTrustStatusAsync(Guid id, bool enabled)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.TrustedCaCertificates.FindAsync(id);
        if (row == null || row.Status == "PendingSignature") return row;
        if (!enabled) row.Status = "Disabled";
        else if (TrustSources.IsHeimdall(row.Source) && await HasOtherActiveHeimdallCa(db, row.Id)) row.Status = "Superseded";
        else row.Status = "Active";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return row;
    }

    private static Task<bool> HasOtherActiveHeimdallCa(AppDbContext db, Guid id) =>
        db.TrustedCaCertificates.AnyAsync(c => c.Id != id && c.Status == "Active" &&
            (c.Source == TrustSources.HeimdallSelfSigned || c.Source == TrustSources.HeimdallSubordinate));

    // ------------------------------------------------------------------ Heimdall CA

    /// <summary>The active Heimdall issuing CA (with private key); creates a self-signed root on first use.</summary>
    public async Task<TrustedCaCertificate> EnsureHeimdallCaAsync(string? createdBy = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var active = await ActiveHeimdallCaRow(db);
        if (active != null) return active;
        return await CreateSelfSignedRootInternalAsync(db, createdBy ?? "system");
    }

    /// <summary>Creates a new self-signed Heimdall root; the previous Heimdall CA stays trusted as Superseded.</summary>
    public async Task<TrustedCaCertificate> CreateSelfSignedRootAsync(string? createdBy, string? subject = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await CreateSelfSignedRootInternalAsync(db, createdBy ?? "system", subject);
    }

    private async Task<TrustedCaCertificate> CreateSelfSignedRootInternalAsync(AppDbContext db, string createdBy, string? subject = null)
    {
        using var key = RSA.Create(4096);
        var request = new CertificateRequest(subject ?? HeimdallRootSubject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        AddCaExtensions(request, pathLength: 1);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(10));

        await SupersedeActiveHeimdallCas(db);
        var row = ToTrustRow(cert, TrustSources.HeimdallSelfSigned, "Active", createdBy);
        row.PrivateKeyPem = key.ExportPkcs8PrivateKeyPem();
        db.TrustedCaCertificates.Add(row);
        await db.SaveChangesAsync();
        _logger.LogInformation("Created self-signed Heimdall root CA {Subject} ({Thumbprint})", row.Subject, row.Thumbprint);
        return row;
    }

    /// <summary>
    /// Starts a subordinate Heimdall CA: generates a key and returns a PKCS#10 CSR for an AD /
    /// enterprise CA to sign (e.g. <c>certreq -submit -attrib "CertificateTemplate:SubCA"</c>).
    /// A previous pending request is replaced.
    /// </summary>
    public async Task<TrustedCaCertificate> CreateSubordinateRequestAsync(string? subject, string? createdBy)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var stale = await db.TrustedCaCertificates.Where(c => c.Status == "PendingSignature").ToListAsync();
        db.TrustedCaCertificates.RemoveRange(stale);

        using var key = RSA.Create(4096);
        var request = new CertificateRequest(string.IsNullOrWhiteSpace(subject) ? "CN=Heimdall Issuing CA, O=Enterprise Factory Automation" : subject,
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        AddCaExtensions(request, pathLength: 0);

        var row = new TrustedCaCertificate
        {
            Subject = request.SubjectName.Name,
            Source = TrustSources.HeimdallSubordinate,
            Status = "PendingSignature",
            CsrPem = request.CreateSigningRequestPem(),
            PrivateKeyPem = key.ExportPkcs8PrivateKeyPem(),
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.TrustedCaCertificates.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    /// <summary>
    /// Completes the subordinate CA with the CA-signed certificate and its chain (PEM, up to the root).
    /// Validated on import: the certificate must carry the pending key, be a CA and chain to a root
    /// contained in <paramref name="chainPem"/> or already trusted. Chain certificates are added to the
    /// trust store (source "ad"); the new CA becomes the active issuer.
    /// </summary>
    public async Task<TrustedCaCertificate> CompleteSubordinateAsync(string certificatePem, string? chainPem, string? createdBy)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pending = await db.TrustedCaCertificates.FirstOrDefaultAsync(c => c.Status == "PendingSignature")
            ?? throw new InvalidOperationException("No subordinate CA request is pending. Create a CSR first.");

        List<X509Certificate2> certs;
        try { certs = CertificateChainValidator.ParsePemBundle(certificatePem + "\n" + (chainPem ?? string.Empty)); }
        catch (CryptographicException ex) { throw new ArgumentException($"Not a PEM certificate bundle: {ex.Message}"); }

        using var key = RSA.Create();
        key.ImportFromPem(pending.PrivateKeyPem);
        var spki = key.ExportSubjectPublicKeyInfo();
        var signed = certs.FirstOrDefault(c => c.PublicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(spki))
            ?? throw new ArgumentException("None of the supplied certificates carries the key of the pending request.");
        if (!CertificateChainValidator.IsCertificateAuthority(signed))
            throw new ArgumentException("The signed certificate is not a CA certificate (basicConstraints cA=TRUE required).");

        var chainCerts = certs.Where(c => c.Thumbprint != signed.Thumbprint).ToList();
        var anchors = (await LoadTrustedCasAsync())
            .Concat(chainCerts.Where(CertificateChainValidator.IsSelfIssued).Select(c => new TrustedCa(c, TrustSources.ActiveDirectory)))
            .ToList();
        var validation = CertificateChainValidator.Validate(signed, anchors, chainCerts, requireEndEntity: false);
        if (!validation.IsValid)
            throw new ArgumentException($"The signed certificate does not validate: {validation.Status} - {validation.Reason}");

        foreach (var ca in chainCerts.Where(CertificateChainValidator.IsCertificateAuthority))
        {
            if (!await db.TrustedCaCertificates.AnyAsync(c => c.Thumbprint == ca.Thumbprint))
                db.TrustedCaCertificates.Add(ToTrustRow(ca, TrustSources.ActiveDirectory, "Active", createdBy));
        }

        await SupersedeActiveHeimdallCas(db);
        pending.Subject = signed.Subject;
        pending.Issuer = signed.Issuer;
        pending.Thumbprint = signed.Thumbprint;
        pending.SerialNumber = signed.SerialNumber;
        pending.IsSelfSigned = false;
        pending.ValidFrom = signed.NotBefore.ToUniversalTime();
        pending.ValidTo = signed.NotAfter.ToUniversalTime();
        pending.RawPem = signed.ExportCertificatePem();
        pending.Status = "Active";
        pending.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        _logger.LogInformation("Heimdall subordinate CA {Subject} activated, issued by {Issuer}", pending.Subject, pending.Issuer);
        return pending;
    }

    private static async Task<TrustedCaCertificate?> ActiveHeimdallCaRow(AppDbContext db) =>
        await db.TrustedCaCertificates
            .Where(c => c.Status == "Active" && c.PrivateKeyPem != null && c.RawPem != null &&
                        (c.Source == TrustSources.HeimdallSelfSigned || c.Source == TrustSources.HeimdallSubordinate))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

    private static async Task SupersedeActiveHeimdallCas(AppDbContext db)
    {
        var actives = await db.TrustedCaCertificates
            .Where(c => c.Status == "Active" && (c.Source == TrustSources.HeimdallSelfSigned || c.Source == TrustSources.HeimdallSubordinate))
            .ToListAsync();
        foreach (var ca in actives) { ca.Status = "Superseded"; ca.UpdatedAt = DateTimeOffset.UtcNow; }
    }

    private static X509Certificate2 LoadWithKey(TrustedCaCertificate row) =>
        X509Certificate2.CreateFromPem(row.RawPem!, row.PrivateKeyPem!);

    private static void AddCaExtensions(CertificateRequest request, int pathLength)
    {
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, pathLength, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
    }

    private static TrustedCaCertificate ToTrustRow(X509Certificate2 cert, string source, string status, string? createdBy) => new()
    {
        Subject = cert.Subject,
        Issuer = cert.Issuer,
        Thumbprint = cert.Thumbprint,
        SerialNumber = cert.SerialNumber,
        Source = source,
        Status = status,
        IsSelfSigned = CertificateChainValidator.IsSelfIssued(cert),
        ValidFrom = cert.NotBefore.ToUniversalTime(),
        ValidTo = cert.NotAfter.ToUniversalTime(),
        RawPem = cert.ExportCertificatePem(),
        CreatedBy = createdBy,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    // ------------------------------------------------------------------ issuance

    /// <summary>
    /// Issues a user, computer or server certificate from the active Heimdall CA.
    /// SAN conventions: user = UPN otherName and/or rfc822Name e-mail; computer / server = dNSName
    /// (hostname or FQDN) plus URI <c>urn:heimdall:machine:&lt;machine identifier&gt;</c>.
    /// With <see cref="IssueCertificateRequest.CsrPem"/> the requester keeps its key; otherwise a key is
    /// generated and returned once as PKCS#12 (never stored, except for the server's own certificate).
    /// </summary>
    public async Task<IssuedCertificate> IssueAsync(IssueCertificateRequest request, string? issuedBy)
    {
        var kind = (request.Kind ?? "computer").Trim().ToLowerInvariant();
        if (kind is not ("user" or "computer" or "server")) throw new ArgumentException("Kind must be user, computer or server.");

        var san = new SubjectAlternativeNameBuilder();
        string commonName;
        if (kind == "user")
        {
            if (string.IsNullOrWhiteSpace(request.UserPrincipalName) && string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("A user certificate needs a UserPrincipalName or an Email.");
            if (!string.IsNullOrWhiteSpace(request.UserPrincipalName)) san.AddUserPrincipalName(request.UserPrincipalName.Trim());
            if (!string.IsNullOrWhiteSpace(request.Email)) san.AddEmailAddress(request.Email.Trim());
            commonName = request.CommonName ?? request.UserPrincipalName ?? request.Email!;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Hostname) && string.IsNullOrWhiteSpace(request.MachineIdentifier))
                throw new ArgumentException("A computer certificate needs a Hostname and/or a MachineIdentifier.");
            if (kind != "server" && string.Equals(request.MachineIdentifier?.Trim(), App.Shared.Plugins.PluginSignatures.ServerMachineIdentifier, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The machine identifier is reserved for the Heimdall server certificate.");
            if (!string.IsNullOrWhiteSpace(request.Hostname)) san.AddDnsName(request.Hostname.Trim());
            if (!string.IsNullOrWhiteSpace(request.MachineIdentifier)) san.AddUri(new Uri(CertificateIdentity.MachineUri(request.MachineIdentifier)));
            commonName = request.CommonName ?? request.Hostname ?? request.MachineIdentifier!;
        }

        var caRow = await EnsureHeimdallCaAsync(issuedBy);
        using var issuer = LoadWithKey(caRow);

        RSA? generatedKey = null;
        CertificateRequest certRequest;
        var subject = new X500DistinguishedName("CN=" + commonName.Replace(",", "\\,").Replace("+", "\\+").Replace("=", "\\="));
        if (!string.IsNullOrWhiteSpace(request.CsrPem))
        {
            CertificateRequest csr;
            try { csr = CertificateRequest.LoadSigningRequestPem(request.CsrPem, HashAlgorithmName.SHA256, CertificateRequestLoadOptions.Default, RSASignaturePadding.Pkcs1); }
            catch (CryptographicException ex) { throw new ArgumentException($"Invalid CSR: {ex.Message}"); }
            certRequest = new CertificateRequest(subject, csr.PublicKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        else
        {
            generatedKey = RSA.Create(2048);
            certRequest = new CertificateRequest(subject, generatedKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        try
        {
            certRequest.CertificateExtensions.Add(san.Build());
            certRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }; // clientAuth
            if (kind == "user") eku.Add(new Oid("1.3.6.1.5.5.7.3.3"));          // codeSigning (plugin signatures)
            if (kind == "computer") eku.Add(new Oid("1.3.6.1.5.5.7.3.1"));      // serverAuth
            if (kind == "server") { eku.Add(new Oid("1.3.6.1.5.5.7.3.1")); eku.Add(new Oid("1.3.6.1.5.5.7.3.3")); }
            certRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));
            certRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certRequest.PublicKey, false));
            certRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));

            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            var notAfter = DateTimeOffset.UtcNow.AddDays(request.ValidityDays is > 0 and <= 3650 ? request.ValidityDays.Value : 365);
            var issuerNotAfter = new DateTimeOffset(issuer.NotAfter.ToUniversalTime());
            if (notAfter > issuerNotAfter) notAfter = issuerNotAfter;

            var serial = RandomNumberGenerator.GetBytes(16);
            serial[0] &= 0x7F;
            using var cert = certRequest.Create(issuer, notBefore, notAfter, serial);

            var record = new ClientCertificateRecord
            {
                Id = Guid.NewGuid(),
                ClientPcId = request.ClientPcId,
                CommonName = commonName,
                Thumbprint = cert.Thumbprint,
                ValidFrom = cert.NotBefore.ToUniversalTime(),
                ValidTo = cert.NotAfter.ToUniversalTime(),
                Status = "Active",
                Issuer = cert.Issuer,
                SerialNumber = cert.SerialNumber,
                KeyAlgorithm = "RSA-2048",
                ProfileName = request.ProfileName ?? kind switch { "user" => "Heimdall-User", "server" => ServerSigningProfile, _ => "Heimdall-Computer" },
                RawPem = cert.ExportCertificatePem(),
                PrivateKeyPem = kind == "server" ? generatedKey?.ExportPkcs8PrivateKeyPem() : null,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await using var db = await _dbFactory.CreateDbContextAsync();
            db.ClientCertificates.Add(record);
            if (request.ClientPcId is { } pcId && await db.ClientPcs.FindAsync(pcId) is { } pc)
            {
                pc.CertificateThumbprint = cert.Thumbprint;
                pc.CertificateProfileName = record.ProfileName;
            }
            await db.SaveChangesAsync();

            string? pfx = null, pfxPassword = null;
            if (generatedKey != null && kind != "server")
            {
                pfxPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
                using var withKey = cert.CopyWithPrivateKey(generatedKey);
                pfx = Convert.ToBase64String(withKey.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, pfxPassword));
            }

            _logger.LogInformation("Issued {Kind} certificate {Subject} ({Thumbprint}) from {Issuer} for {User}", kind, cert.Subject, cert.Thumbprint, issuer.Subject, issuedBy);
            return new IssuedCertificate(record, cert.ExportCertificatePem(), issuer.ExportCertificatePem(), pfx, pfxPassword);
        }
        finally
        {
            generatedKey?.Dispose();
        }
    }

    /// <summary>
    /// The Heimdall server's own computer certificate (with key) used to countersign plugins.
    /// Issued from the Heimdall CA on first use and kept in <c>client_certificates</c>, key encrypted at rest.
    /// </summary>
    public async Task<X509Certificate2> GetOrCreateServerSigningCertificateAsync()
    {
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var now = DateTimeOffset.UtcNow.AddDays(1);
            var existing = await db.ClientCertificates.AsNoTracking()
                .Where(c => c.ProfileName == ServerSigningProfile && c.Status == "Active" && c.PrivateKeyPem != null && c.RawPem != null && c.ValidTo > now)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
            if (existing != null) return X509Certificate2.CreateFromPem(existing.RawPem!, existing.PrivateKeyPem!);
        }

        var host = Environment.MachineName;
        var issued = await IssueAsync(new IssueCertificateRequest
        {
            Kind = "server",
            CommonName = $"Heimdall Server ({host})",
            Hostname = host,
            MachineIdentifier = App.Shared.Plugins.PluginSignatures.ServerMachineIdentifier,
            ValidityDays = 730
        }, "system");
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var row = await db2.ClientCertificates.AsNoTracking().FirstAsync(c => c.Id == issued.Record.Id);
        return X509Certificate2.CreateFromPem(row.RawPem!, row.PrivateKeyPem!);
    }

    /// <summary>Certificates of the chain from <paramref name="leaf"/> up to its trust anchor, as PEM (leaf first).</summary>
    public async Task<string> BuildChainPemAsync(X509Certificate2 leaf)
    {
        var result = CertificateChainValidator.Validate(leaf, await LoadTrustedCasAsync(), requireEndEntity: false);
        var chain = result.Chain.Count > 0 ? result.Chain : new[] { leaf };
        return string.Join("\n", chain.Select(c => c.ExportCertificatePem()));
    }

    // ------------------------------------------------------------------ validation and mapping

    /// <summary>
    /// Validates a certificate (PEM, optionally followed by its intermediates) against the trust store,
    /// checks revocation of Heimdall-issued certificates in the registry, and maps the identity.
    /// </summary>
    public async Task<PkiValidationResult> ValidateAsync(string certificatePem, string? chainPem = null, DateTimeOffset? at = null)
    {
        List<X509Certificate2> certs;
        try { certs = CertificateChainValidator.ParsePemBundle(certificatePem + "\n" + (chainPem ?? string.Empty)); }
        catch (CryptographicException ex) { return PkiValidationResult.Invalid("Malformed", $"Not a PEM certificate: {ex.Message}"); }
        if (certs.Count == 0) return PkiValidationResult.Invalid("Malformed", "No certificate found.");

        var leaf = certs[0];
        var trusted = await LoadTrustedCasAsync();
        var chain = CertificateChainValidator.Validate(leaf, trusted, certs.Skip(1), at, externalRevocationMode: _externalRevocationMode);
        var identity = CertificateIdentity.FromCertificate(leaf);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var status = chain.Status;
        var reason = chain.Reason;
        bool heimdallIssued = TrustSources.IsHeimdall(chain.IssuerSource);
        if (chain.IsValid && heimdallIssued)
        {
            var record = await db.ClientCertificates.AsNoTracking().FirstOrDefaultAsync(c => c.Thumbprint == leaf.Thumbprint);
            if (record == null) { status = "Unregistered"; reason = "Issued by a Heimdall CA but unknown to the certificate registry."; }
            else if (record.Status == "Revoked") { status = "Revoked"; reason = "The certificate has been revoked in Heimdall."; }
        }
        bool valid = status == "Valid";

        var computer = valid ? await MapComputerAsync(db, leaf, identity) : null;
        var user = valid ? await MapUserAsync(db, identity) : null;

        return new PkiValidationResult
        {
            Valid = valid,
            Status = status,
            Reason = reason,
            Subject = leaf.Subject,
            Issuer = leaf.Issuer,
            Thumbprint = leaf.Thumbprint,
            SerialNumber = leaf.SerialNumber,
            NotBefore = leaf.NotBefore.ToUniversalTime(),
            NotAfter = leaf.NotAfter.ToUniversalTime(),
            IssuerSource = chain.IssuerSource,
            AnchorSource = chain.AnchorSource,
            HeimdallIssued = heimdallIssued,
            Identity = identity,
            Computer = computer,
            User = user,
            Warnings = chain.Warnings
        };
    }

    /// <summary>Computer certificate → ClientPc: machine URI, else DNS name = hostname, else registered thumbprint.</summary>
    public static async Task<ComputerMapping?> MapComputerAsync(AppDbContext db, X509Certificate2 cert, CertificateIdentity identity)
    {
        var machineId = identity.MachineIdentifier;
        if (!string.IsNullOrWhiteSpace(machineId))
        {
            var lowered = machineId.ToLowerInvariant();
            var byUri = await db.ClientPcs.AsNoTracking().FirstOrDefaultAsync(p => p.MachineIdentifier != null && p.MachineIdentifier.ToLower() == lowered);
            if (byUri != null) return new ComputerMapping(byUri.Id, byUri.Name, byUri.Hostname, byUri.MachineIdentifier, "machine-uri");
        }

        var hostCandidates = identity.DnsNames
            .SelectMany(d => new[] { d, d.Split('.')[0] })
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => d.Length > 0)
            .Distinct()
            .ToList();
        if (hostCandidates.Count > 0)
        {
            var byHost = await db.ClientPcs.AsNoTracking().FirstOrDefaultAsync(p => p.Hostname != null && hostCandidates.Contains(p.Hostname.ToLower()));
            if (byHost != null) return new ComputerMapping(byHost.Id, byHost.Name, byHost.Hostname, byHost.MachineIdentifier, "dns-hostname");
        }

        var thumbprint = cert.Thumbprint.ToUpperInvariant();
        var byThumb = await db.ClientPcs.AsNoTracking().FirstOrDefaultAsync(p => p.CertificateThumbprint != null && p.CertificateThumbprint.ToUpper() == thumbprint);
        return byThumb == null ? null : new ComputerMapping(byThumb.Id, byThumb.Name, byThumb.Hostname, byThumb.MachineIdentifier, "thumbprint");
    }

    /// <summary>User certificate → Better-Auth user: UPN or rfc822Name e-mail equals the user's e-mail (case-insensitive).</summary>
    public static async Task<UserMapping?> MapUserAsync(AppDbContext db, CertificateIdentity identity)
    {
        var candidates = identity.UserCandidates;
        if (candidates.Count == 0) return null;
        var user = await db.AuthUsers.AsNoTracking().FirstOrDefaultAsync(u => candidates.Contains(u.Email.ToLower()));
        return user == null ? null : new UserMapping(user.Id, user.Email, user.Name, user.Role);
    }

    /// <summary>
    /// Kiosk workstation identity: a valid computer certificate mapped to a ClientPc, plus the stations
    /// bound to that controller (StationControllers).
    /// </summary>
    public async Task<WorkstationIdentity> ResolveWorkstationAsync(string certificatePem)
    {
        var validation = await ValidateAsync(certificatePem);
        if (!validation.Valid)
            return new WorkstationIdentity(false, validation.Status, validation.Reason, null, Array.Empty<string>());
        if (validation.Computer == null)
            return new WorkstationIdentity(false, "Unmapped", "The certificate does not map to a registered controller (machine URI, DNS hostname or thumbprint).", null, Array.Empty<string>());

        await using var db = await _dbFactory.CreateDbContextAsync();
        var stations = await db.StationControllers.AsNoTracking()
            .Where(sc => sc.ClientPcId == validation.Computer.ClientPcId)
            .Select(sc => new { sc.MachineId, sc.Machine!.CustomIdentifier })
            .ToListAsync();
        var keys = stations.SelectMany(s => new[] { s.MachineId.ToString(), s.CustomIdentifier })
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new WorkstationIdentity(true, "Valid", null, validation.Computer, keys);
    }
}

/// <summary>Issuance request (POST /api/v1/certificatemanagement/issue).</summary>
public class IssueCertificateRequest
{
    /// <summary>user, computer or server.</summary>
    public string? Kind { get; set; } = "computer";
    public string? CommonName { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? Email { get; set; }
    /// <summary>Computer DNS name (hostname or FQDN) → dNSName SAN.</summary>
    public string? Hostname { get; set; }
    /// <summary>Machine identifier → URI SAN urn:heimdall:machine:&lt;id&gt;.</summary>
    public string? MachineIdentifier { get; set; }
    public Guid? ClientPcId { get; set; }
    public int? ValidityDays { get; set; }
    public string? ProfileName { get; set; }
    /// <summary>Optional PKCS#10 request; when given, the key stays with the requester.</summary>
    public string? CsrPem { get; set; }
}

public sealed record IssuedCertificate(ClientCertificateRecord Record, string CertificatePem, string IssuerPem, string? Pkcs12Base64, string? Pkcs12Password);

public sealed record ComputerMapping(Guid ClientPcId, string Name, string? Hostname, string? MachineIdentifier, string MappedBy);

public sealed record UserMapping(string UserId, string Email, string Name, string? Role);

public sealed record WorkstationIdentity(bool Valid, string Status, string? Reason, ComputerMapping? Computer, IReadOnlyList<string> StationKeys);

public sealed record PkiValidationResult
{
    public bool Valid { get; init; }
    public string Status { get; init; } = "Invalid";
    public string? Reason { get; init; }
    public string? Subject { get; init; }
    public string? Issuer { get; init; }
    public string? Thumbprint { get; init; }
    public string? SerialNumber { get; init; }
    public DateTimeOffset? NotBefore { get; init; }
    public DateTimeOffset? NotAfter { get; init; }
    public string? IssuerSource { get; init; }
    public string? AnchorSource { get; init; }
    public bool HeimdallIssued { get; init; }
    public CertificateIdentity? Identity { get; init; }
    public ComputerMapping? Computer { get; init; }
    public UserMapping? User { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static PkiValidationResult Invalid(string status, string reason) => new() { Valid = false, Status = status, Reason = reason };
}
