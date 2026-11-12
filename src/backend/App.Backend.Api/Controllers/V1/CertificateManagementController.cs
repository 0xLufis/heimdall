using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using App.Backend.Api.Services.Pki;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for managing enterprise and industrial X.509 PKI certificates: the trust store
/// (Heimdall root / intermediate, imported AD / enterprise CA chains), Heimdall's CA (self-signed or
/// subordinate), issuance of user and computer certificates, validation and identity mapping, and
/// Active Directory Organizational Unit (OU) certificate auto-enrollment rules.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ItAdministration")]
public class CertificateManagementController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<CertificateManagementController> _logger;
    private readonly PkiService _pki;

    /// <summary>
    /// Initializes a new instance of <see cref="CertificateManagementController"/>.
    /// </summary>
    /// <param name="dbContextFactory">Factory for creating database context instances.</param>
    /// <param name="logger">Structured logger instance.</param>
    /// <param name="pki">PKI service (trust store, issuance, validation); built from the factory when not injected.</param>
    public CertificateManagementController(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<CertificateManagementController> logger, PkiService? pki = null)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _pki = pki ?? new PkiService(dbContextFactory, NullLogger<PkiService>.Instance);
    }

    private string CurrentUser => User?.FindFirstValue(ClaimTypes.Email) ?? User?.Identity?.Name ?? "system";

    /// <summary>The Heimdall CA in the legacy root-CA response shape (the admin UI reads it).</summary>
    private static ClientCertificateRecord AsRootRecord(TrustedCaCertificate ca) => new()
    {
        Id = ca.Id,
        CommonName = ca.Subject,
        Issuer = ca.Issuer,
        Thumbprint = ca.Thumbprint,
        ValidFrom = ca.ValidFrom ?? ca.CreatedAt,
        ValidTo = ca.ValidTo ?? ca.CreatedAt,
        Status = ca.Status,
        IsRootCa = true,
        ProfileName = ca.Source,
        KeyAlgorithm = "RSA",
        SerialNumber = ca.SerialNumber,
        RawPem = ca.RawPem,
        CreatedAt = ca.CreatedAt
    };

    /// <summary>
    /// Retrieves all client and machine certificates ordered by creation date.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var certs = await db.ClientCertificates
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return Ok(certs);
    }

    /// <summary>
    /// The active Heimdall issuing CA (self-signed root or subordinate CA). A self-signed root is
    /// created on first use. Kept in the trust store (<c>trusted_ca_certificates</c>) with its key.
    /// </summary>
    [HttpGet("root-ca")]
    public async Task<IActionResult> GetRootCa()
    {
        var ca = await _pki.EnsureHeimdallCaAsync(CurrentUser);
        return Ok(AsRootRecord(ca));
    }

    /// <summary>
    /// Imports enterprise CA certificate(s) (PEM, one or more blocks) into the trust store.
    /// <c>Source</c> is "ad" (AD CS chain) or "imported" (default). Heimdall's own issuing CA is not
    /// changed; use root-ca/subordinate/* to make Heimdall's CA subordinate to an enterprise CA.
    /// </summary>
    /// <param name="request">Request containing raw PEM (or Base64 DER) certificate text and profile metadata.</param>
    [HttpPost("root-ca/import")]
    public async Task<IActionResult> ImportRootCertificate([FromBody] ImportRootCertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawPem))
        {
            return BadRequest(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.MissingRequiredField, "Raw certificate PEM or CRT text is required."));
        }

        var pem = request.RawPem.Contains("-----BEGIN", StringComparison.Ordinal)
            ? request.RawPem
            : "-----BEGIN CERTIFICATE-----\n" + request.RawPem.Trim() + "\n-----END CERTIFICATE-----";
        try
        {
            var imported = await _pki.ImportTrustedCasAsync(pem, request.Source ?? TrustSources.Imported, request.Description ?? request.ProfileName, CurrentUser);
            var first = AsRootRecord(imported[0]);
            first.ProfileName = request.ProfileName ?? imported[0].Source;
            return Ok(first);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Rejected CA import: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Downloads the active Heimdall CA certificate (with its chain for a subordinate CA) as PEM.</summary>
    [HttpGet("root-ca/download")]
    public async Task<IActionResult> DownloadRootCertificate()
    {
        var ca = await _pki.EnsureHeimdallCaAsync(CurrentUser);
        var pem = await _pki.BuildChainPemAsync(X509Certificate2.CreateFromPem(ca.RawPem!));
        return File(Encoding.UTF8.GetBytes(pem), "application/x-x509-ca-cert", "heimdall-project-root-ca.crt");
    }

    /// <summary>Creates a new self-signed Heimdall root CA; the previous Heimdall CA stays trusted (Superseded).</summary>
    [HttpPost("root-ca/self-signed")]
    public async Task<IActionResult> CreateSelfSignedRoot([FromBody] CreateCaRequest? request)
    {
        var ca = await _pki.CreateSelfSignedRootAsync(CurrentUser, string.IsNullOrWhiteSpace(request?.Subject) ? null : request!.Subject);
        return Ok(AsRootRecord(ca));
    }

    /// <summary>
    /// Subordinate root, step 1: generates the Heimdall CA key and returns a PKCS#10 CSR to be signed by
    /// an AD / enterprise CA (SubCA template).
    /// </summary>
    [HttpPost("root-ca/subordinate/csr")]
    public async Task<IActionResult> CreateSubordinateCsr([FromBody] CreateCaRequest? request)
    {
        var pending = await _pki.CreateSubordinateRequestAsync(request?.Subject, CurrentUser);
        return Ok(new { pending.Id, pending.Subject, pending.Status, pending.CsrPem });
    }

    /// <summary>
    /// Subordinate root, step 2: imports the CA-signed certificate and its chain (PEM, up to the root).
    /// Validated on import (key match, CA flag, chain); the chain is added to the trust store as "ad".
    /// </summary>
    [HttpPost("root-ca/subordinate/complete")]
    public async Task<IActionResult> CompleteSubordinate([FromBody] CompleteSubordinateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.CertificatePem))
            return BadRequest(new { Message = "CertificatePem is required." });
        try
        {
            var ca = await _pki.CompleteSubordinateAsync(request.CertificatePem, request.ChainPem, CurrentUser);
            return Ok(AsRootRecord(ca));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or CryptographicException)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>The trust store: trusted CA certificates with source and status (keys are never returned).</summary>
    [HttpGet("trust-store")]
    public async Task<IActionResult> GetTrustStore() => Ok(await _pki.ListTrustStoreAsync());

    /// <summary>Imports CA certificate(s) into the trust store (Source "ad" or "imported").</summary>
    [HttpPost("trust-store/import")]
    public async Task<IActionResult> ImportTrustedCa([FromBody] ImportTrustedCaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Pem)) return BadRequest(new { Message = "Pem is required." });
        try
        {
            return Ok(await _pki.ImportTrustedCasAsync(request.Pem, request.Source ?? TrustSources.Imported, request.Notes, CurrentUser));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Stops trusting a CA certificate (certificates chaining only to it no longer validate).</summary>
    [HttpPost("trust-store/{id:guid}/disable")]
    public async Task<IActionResult> DisableTrustedCa(Guid id) =>
        await _pki.SetTrustStatusAsync(id, enabled: false) is { } row ? Ok(row) : NotFound();

    /// <summary>Trusts a previously disabled CA certificate again.</summary>
    [HttpPost("trust-store/{id:guid}/enable")]
    public async Task<IActionResult> EnableTrustedCa(Guid id) =>
        await _pki.SetTrustStatusAsync(id, enabled: true) is { } row ? Ok(row) : NotFound();

    /// <summary>
    /// PEM bundle of the trusted CA certificates. Public data (CA certificates); agents use it as
    /// <c>TrustedCaCertificatesPem</c> to verify plugin signatures.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("trust-bundle")]
    public async Task<IActionResult> GetTrustBundle() =>
        Content(await _pki.GetTrustBundlePemAsync(), "application/x-pem-file");

    /// <summary>
    /// Issues a user or computer certificate from the Heimdall CA (SAN conventions: users UPN / e-mail,
    /// computers DNS hostname + urn:heimdall:machine:&lt;id&gt;). Without a CSR the generated key is
    /// returned once as PKCS#12 with a random password.
    /// </summary>
    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssueCertificateRequest request)
    {
        if (request == null) return BadRequest(new { Message = "Request body is required." });
        if (string.Equals(request.Kind, "server", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Message = "The server certificate is created by Heimdall itself." });
        try
        {
            return Ok(await _pki.IssueAsync(request, CurrentUser));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Validates a certificate (chain, validity, revocation) and maps it to a ClientPc / user.</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateCertificateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.CertificatePem)) return BadRequest(new { Message = "CertificatePem is required." });
        return Ok(await _pki.ValidateAsync(request.CertificatePem, request.ChainPem));
    }

    /// <summary>
    /// Retrieves all configured Active Directory OU certificate assignment and auto-enrollment rules.
    /// </summary>
    [HttpGet("ou-rules")]
    public async Task<IActionResult> GetOuRules()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var rules = await db.OuCertificateRules
            .OrderBy(r => r.OuPath)
            .ToListAsync();

        if (rules.Count == 0)
        {
            // Seed initial industrial OU rules if none exist
            var defaultRules = new List<OuCertificateRule>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    OuPath = "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp",
                    ProfileName = "High-Assurance-Robotics-mTLS",
                    ValidityYears = 2,
                    AutoEnroll = true,
                    KeyAlgorithm = "RSA-2048",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    OuPath = "OU=Fastening,OU=VLAN50-Joining,DC=factory,DC=corp",
                    ProfileName = "Line-Gateway-Joining-mTLS",
                    ValidityYears = 2,
                    AutoEnroll = true,
                    KeyAlgorithm = "RSA-2048",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    OuPath = "OU=AOI-Vision,OU=VLAN20-Inspection,DC=factory,DC=corp",
                    ProfileName = "Vision-Edge-Telemetry-Profile",
                    ValidityYears = 3,
                    AutoEnroll = true,
                    KeyAlgorithm = "ECDSA-P256",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            db.OuCertificateRules.AddRange(defaultRules);
            await db.SaveChangesAsync();
            rules = defaultRules;
        }

        return Ok(rules);
    }

    /// <summary>
    /// Creates or updates an Active Directory OU certificate assignment rule.
    /// </summary>
    /// <param name="dto">Rule specification containing OU path, profile name, and validity parameters.</param>
    [HttpPost("ou-rules")]
    public async Task<IActionResult> SaveOuRule([FromBody] SaveOuRuleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OuPath) || string.IsNullOrWhiteSpace(dto.ProfileName))
        {
            return BadRequest(new { Message = "OuPath and ProfileName are required." });
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        OuCertificateRule? rule = null;
        if (dto.Id.HasValue && dto.Id.Value != Guid.Empty)
        {
            rule = await db.OuCertificateRules.FindAsync(dto.Id.Value);
        }

        if (rule == null)
        {
            rule = new OuCertificateRule
            {
                Id = Guid.NewGuid(),
                OuPath = dto.OuPath.Trim(),
                ProfileName = dto.ProfileName.Trim(),
                ValidityYears = dto.ValidityYears > 0 ? dto.ValidityYears : 2,
                AutoEnroll = dto.AutoEnroll,
                KeyAlgorithm = dto.KeyAlgorithm ?? "RSA-2048",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.OuCertificateRules.Add(rule);
        }
        else
        {
            rule.OuPath = dto.OuPath.Trim();
            rule.ProfileName = dto.ProfileName.Trim();
            rule.ValidityYears = dto.ValidityYears > 0 ? dto.ValidityYears : 2;
            rule.AutoEnroll = dto.AutoEnroll;
            rule.KeyAlgorithm = dto.KeyAlgorithm ?? rule.KeyAlgorithm;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Saved OU certificate assignment rule: {OuPath} -> {Profile}", rule.OuPath, rule.ProfileName);
        return Ok(rule);
    }

    /// <summary>
    /// Deletes an OU certificate assignment rule by its identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the rule to delete.</param>
    [HttpDelete("ou-rules/{id}")]
    public async Task<IActionResult> DeleteOuRule(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var rule = await db.OuCertificateRules.FindAsync(id);
        if (rule == null) return NotFound();

        db.OuCertificateRules.Remove(rule);
        await db.SaveChangesAsync();
        _logger.LogInformation("Deleted OU certificate rule {Id} ({OuPath})", id, rule.OuPath);
        return Ok(new { Message = "Rule deleted successfully" });
    }

    /// <summary>
    /// Reconciles client PCs with active auto-enrollment OU certificate rules, issuing certificates for uncertified nodes.
    /// </summary>
    [HttpPost("sync-ou-certificates")]
    public async Task<IActionResult> SyncOuCertificates()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var rules = await db.OuCertificateRules.Where(r => r.AutoEnroll).ToListAsync();
        var pcs = await db.ClientPcs.Where(p => !string.IsNullOrEmpty(p.AdOuPath)).ToListAsync();

        int syncedCount = 0;

        foreach (var pc in pcs)
        {
            // Find rule matching OU path
            var matchingRule = rules.FirstOrDefault(r =>
                pc.AdOuPath!.Equals(r.OuPath, StringComparison.OrdinalIgnoreCase) ||
                pc.AdOuPath!.Contains(r.OuPath, StringComparison.OrdinalIgnoreCase));

            if (matchingRule == null) continue;

            // Check if active cert with this profile already exists
            var existingCert = await db.ClientCertificates
                .FirstOrDefaultAsync(c => c.ClientPcId == pc.Id && c.Status == "Active" && c.ProfileName == matchingRule.ProfileName);

            if (existingCert == null)
            {
                var issued = await _pki.IssueAsync(new IssueCertificateRequest
                {
                    Kind = "computer",
                    CommonName = pc.Hostname ?? pc.Name,
                    Hostname = pc.Hostname ?? pc.Name,
                    MachineIdentifier = pc.MachineIdentifier,
                    ClientPcId = pc.Id,
                    ProfileName = matchingRule.ProfileName,
                    ValidityDays = matchingRule.ValidityYears * 365
                }, CurrentUser);

                pc.CertificateThumbprint = issued.Record.Thumbprint;
                pc.CertificateProfileName = matchingRule.ProfileName;
                syncedCount++;
            }
            else
            {
                pc.CertificateThumbprint = existingCert.Thumbprint;
                pc.CertificateProfileName = matchingRule.ProfileName;
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Synced OU certificates: {Count} devices auto-enrolled across {Rules} active rules", syncedCount, rules.Count);
        return Ok(new { Message = $"OU Certificate Synchronization complete. {syncedCount} hosts enrolled.", SyncedCount = syncedCount, ActiveRulesCount = rules.Count });
    }

    /// <summary>
    /// Generates and assigns an X.509 client certificate for a specified common name or industrial controller.
    /// </summary>
    /// <param name="request">Certificate generation request parameters.</param>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateCertificate([FromBody] GenerateCertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CommonName))
        {
            return BadRequest(new { Message = "CommonName is required." });
        }

        string? hostname = request.CommonName;
        string? machineIdentifier = null;
        if (request.ClientPcId is { } pcId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var pc = await db.ClientPcs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pcId);
            hostname = pc?.Hostname ?? hostname;
            machineIdentifier = pc?.MachineIdentifier;
        }

        var issued = await _pki.IssueAsync(new IssueCertificateRequest
        {
            Kind = "computer",
            CommonName = request.CommonName,
            Hostname = hostname,
            MachineIdentifier = machineIdentifier,
            ClientPcId = request.ClientPcId,
            ValidityDays = (request.ValidityYears > 0 ? request.ValidityYears : 1) * 365
        }, CurrentUser);

        _logger.LogInformation("Generated client certificate for {CommonName} (Thumbprint: {Thumbprint})", issued.Record.CommonName, issued.Record.Thumbprint);
        return Ok(issued.Record);
    }

    /// <summary>
    /// Revokes an issued client certificate by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the certificate record to revoke.</param>
    [HttpPost("{id}/revoke")]
    public async Task<IActionResult> RevokeCertificate(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var cert = await db.ClientCertificates.FindAsync(id);
        if (cert == null) return NotFound();

        cert.Status = "Revoked";
        await db.SaveChangesAsync();

        _logger.LogInformation("Revoked client certificate {Id} (Thumbprint: {Thumbprint})", id, cert.Thumbprint);
        return Ok(new { Message = "Certificate revoked successfully", Certificate = cert });
    }
}

/// <summary>
/// Request payload for generating an X.509 client certificate.
/// </summary>
public class GenerateCertRequest
{
    /// <summary>Certificate Subject Common Name (CN).</summary>
    public string CommonName { get; set; } = string.Empty;

    /// <summary>Optional identifier of the client PC associated with this certificate.</summary>
    public Guid? ClientPcId { get; set; }

    /// <summary>Certificate validity lifetime in years.</summary>
    public int ValidityYears { get; set; } = 1;
}

/// <summary>
/// Request payload for importing an enterprise X.509 Root CA certificate.
/// </summary>
public class ImportRootCertRequest
{
    /// <summary>Raw PEM or Base64 encoded CRT text of the certificate.</summary>
    public string RawPem { get; set; } = string.Empty;

    /// <summary>Profile name for certificate tracking.</summary>
    public string? ProfileName { get; set; }

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Trust store source: "ad" or "imported" (default).</summary>
    public string? Source { get; set; }
}

/// <summary>Request to create a Heimdall CA (self-signed root or subordinate CSR).</summary>
public class CreateCaRequest
{
    /// <summary>Optional X.500 subject, e.g. "CN=Heimdall Issuing CA, O=Factory".</summary>
    public string? Subject { get; set; }
}

/// <summary>CA-signed certificate for the pending subordinate Heimdall CA, plus its chain.</summary>
public class CompleteSubordinateRequest
{
    public string CertificatePem { get; set; } = string.Empty;
    /// <summary>Issuing CA chain up to (and including) the root, PEM.</summary>
    public string? ChainPem { get; set; }
}

/// <summary>CA certificate(s) to trust.</summary>
public class ImportTrustedCaRequest
{
    public string Pem { get; set; } = string.Empty;
    /// <summary>"ad" or "imported".</summary>
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Certificate to validate (leaf first; optional intermediates).</summary>
public class ValidateCertificateRequest
{
    public string CertificatePem { get; set; } = string.Empty;
    public string? ChainPem { get; set; }
}

/// <summary>
/// Data transfer object for saving or updating an Active Directory OU certificate auto-enrollment rule.
/// </summary>
public class SaveOuRuleDto
{
    /// <summary>Optional rule identifier for updates; null for creation.</summary>
    public Guid? Id { get; set; }

    /// <summary>Distinguished name or pattern of the Active Directory Organizational Unit.</summary>
    public string OuPath { get; set; } = string.Empty;

    /// <summary>Certificate profile name applied to nodes in this OU.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Certificate validity lifetime in years.</summary>
    public int ValidityYears { get; set; } = 2;

    /// <summary>Indicates whether certificates should be automatically issued during OU synchronization.</summary>
    public bool AutoEnroll { get; set; } = true;

    /// <summary>Cryptographic key algorithm to use (e.g., RSA-2048, ECDSA-P256).</summary>
    public string? KeyAlgorithm { get; set; } = "RSA-2048";
}
