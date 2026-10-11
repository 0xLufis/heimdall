using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for managing enterprise and industrial X.509 PKI certificates,
/// root CAs, and Active Directory Organizational Unit (OU) certificate auto-enrollment rules.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ItAdministration")]
public class CertificateManagementController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<CertificateManagementController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CertificateManagementController"/>.
    /// </summary>
    /// <param name="dbContextFactory">Factory for creating database context instances.</param>
    /// <param name="logger">Structured logger instance.</param>
    public CertificateManagementController(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<CertificateManagementController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

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
    /// Retrieves the current active industrial Root CA certificate record, seeding a default CA if none exists.
    /// </summary>
    [HttpGet("root-ca")]
    public async Task<IActionResult> GetRootCa()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var rootCert = await db.ClientCertificates
            .FirstOrDefaultAsync(c => c.IsRootCa && c.Status == "Active");

        if (rootCert == null)
        {
            // Seed a default project root CA if none exists yet
            using var rsa = RSA.Create(4096);
            var req = new CertificateRequest("CN=Heimdall Project Industrial Root CA, O=Enterprise Factory Automation, C=US", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddYears(10));

            var pem = cert.ExportCertificatePem();

            rootCert = new ClientCertificateRecord
            {
                Id = Guid.NewGuid(),
                CommonName = "CN=Heimdall Project Industrial Root CA",
                Issuer = "CN=Heimdall Project Industrial Root CA",
                Thumbprint = cert.Thumbprint,
                ValidFrom = cert.NotBefore.ToUniversalTime(),
                ValidTo = cert.NotAfter.ToUniversalTime(),
                Status = "Active",
                IsRootCa = true,
                ProfileName = "Root-CA-Profile",
                KeyAlgorithm = "RSA-4096",
                SerialNumber = cert.SerialNumber,
                RawPem = pem,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.ClientCertificates.Add(rootCert);
            await db.SaveChangesAsync();
        }

        return Ok(rootCert);
    }

    /// <summary>
    /// Imports or replaces an enterprise X.509 Root CA certificate.
    /// Demotes any previously active Root CA certificates to Superseded and records the CA metadata.
    /// </summary>
    /// <param name="request">Request containing raw PEM or Base64 certificate text and profile metadata.</param>
    [HttpPost("root-ca/import")]
    public async Task<IActionResult> ImportRootCertificate([FromBody] ImportRootCertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawPem))
        {
            return BadRequest(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.MissingRequiredField, "Raw certificate PEM or CRT text is required."));
        }

        try
        {
            X509Certificate2 cert;
            try
            {
                cert = X509Certificate2.CreateFromPem(request.RawPem);
            }
            catch
            {
                var cleaned = request.RawPem
                    .Replace("-----BEGIN CERTIFICATE-----", "")
                    .Replace("-----END CERTIFICATE-----", "")
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Trim();
                var bytes = Convert.FromBase64String(cleaned);
                cert = X509CertificateLoader.LoadCertificate(bytes);
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            // Demote any existing active Root CA
            var existingRoots = await db.ClientCertificates
                .Where(c => c.IsRootCa && c.Status == "Active")
                .ToListAsync();

            foreach (var r in existingRoots)
            {
                r.Status = "Superseded";
            }

            var record = new ClientCertificateRecord
            {
                Id = Guid.NewGuid(),
                CommonName = cert.Subject,
                Issuer = cert.Issuer,
                Thumbprint = cert.Thumbprint,
                ValidFrom = cert.NotBefore.ToUniversalTime(),
                ValidTo = cert.NotAfter.ToUniversalTime(),
                Status = "Active",
                IsRootCa = true,
                ProfileName = request.ProfileName ?? "Corporate-Project-Root-CA",
                KeyAlgorithm = cert.SignatureAlgorithm?.FriendlyName ?? "RSA-X509",
                SerialNumber = cert.SerialNumber,
                RawPem = request.RawPem.Trim(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.ClientCertificates.Add(record);

            // Also persist in SystemSettings
            var rootSetting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "Project.RootCA");
            var summary = JsonSerializer.Serialize(new
            {
                record.Id,
                record.CommonName,
                record.Issuer,
                record.Thumbprint,
                record.ValidFrom,
                record.ValidTo,
                record.KeyAlgorithm,
                record.SerialNumber,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            if (rootSetting == null)
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = "Project.RootCA",
                    Category = "Certificates",
                    ValueJson = summary,
                    UpdatedBy = User?.Identity?.Name ?? "system",
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                rootSetting.ValueJson = summary;
                rootSetting.UpdatedBy = User?.Identity?.Name ?? "system";
                rootSetting.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("Successfully imported new Project Root Certificate: {Subject} (Thumbprint: {Thumbprint})", record.CommonName, record.Thumbprint);

            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse imported X.509 Root Certificate");
            return BadRequest(new { Message = $"Failed to parse X.509 certificate: {ex.Message}" });
        }
    }

    /// <summary>
    /// Downloads the active Project Root Certificate as an X.509 CRT file.
    /// </summary>
    [HttpGet("root-ca/download")]
    public async Task<IActionResult> DownloadRootCertificate()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var rootCert = await db.ClientCertificates
            .FirstOrDefaultAsync(c => c.IsRootCa && c.Status == "Active");

        if (rootCert == null || string.IsNullOrWhiteSpace(rootCert.RawPem))
        {
            return NotFound(new { Message = "No active Project Root Certificate found." });
        }

        var bytes = Encoding.UTF8.GetBytes(rootCert.RawPem);
        return File(bytes, "application/x-x509-ca-cert", "heimdall-project-root-ca.crt");
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
                using var rsa = RSA.Create(2048);
                var cn = pc.Hostname ?? pc.Name;
                var certReq = new CertificateRequest($"CN={cn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(matchingRule.ValidityYears));

                var newCert = new ClientCertificateRecord
                {
                    Id = Guid.NewGuid(),
                    ClientPcId = pc.Id,
                    CommonName = cn,
                    Thumbprint = cert.Thumbprint,
                    ValidFrom = cert.NotBefore.ToUniversalTime(),
                    ValidTo = cert.NotAfter.ToUniversalTime(),
                    Status = "Active",
                    AdOuPath = pc.AdOuPath,
                    ProfileName = matchingRule.ProfileName,
                    KeyAlgorithm = matchingRule.KeyAlgorithm,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                db.ClientCertificates.Add(newCert);

                pc.CertificateThumbprint = newCert.Thumbprint;
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

        using var rsa = RSA.Create(2048);
        var certReq = new CertificateRequest($"CN={request.CommonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(request.ValidityYears > 0 ? request.ValidityYears : 1));

        var record = new ClientCertificateRecord
        {
            Id = Guid.NewGuid(),
            ClientPcId = request.ClientPcId,
            CommonName = request.CommonName,
            Thumbprint = cert.Thumbprint,
            ValidFrom = cert.NotBefore.ToUniversalTime(),
            ValidTo = cert.NotAfter.ToUniversalTime(),
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.ClientCertificates.Add(record);
        await db.SaveChangesAsync();

        _logger.LogInformation("Generated client certificate for {CommonName} (Thumbprint: {Thumbprint})", record.CommonName, record.Thumbprint);
        return Ok(record);
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
