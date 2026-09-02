using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "SystemAdministration")]
public class CertificateManagementController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<CertificateManagementController> _logger;

    public CertificateManagementController(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<CertificateManagementController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var certs = await db.ClientCertificates
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return Ok(certs);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateCertificate([FromBody] GenerateCertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CommonName))
        {
            return BadRequest(new { Message = "CommonName is required." });
        }

        // Generate synthetic X.509 thumbprint / simulated issuance
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

public class GenerateCertRequest
{
    public string CommonName { get; set; } = string.Empty;
    public Guid? ClientPcId { get; set; }
    public int ValidityYears { get; set; } = 1;
}
