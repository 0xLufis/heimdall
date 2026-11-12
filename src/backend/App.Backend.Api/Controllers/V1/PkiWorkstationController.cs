using App.Backend.Api.Services.Pki;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Kiosk terminal workstation identity for the BFF (production path): the TLS-terminating reverse
/// proxy verifies the workstation's client certificate, the BFF forwards the PEM here and allows the
/// terminal only for the stations returned. Anonymous because the kiosk has no user session: the
/// caller must present a certificate that validates against Heimdall's trust store, and the answer
/// only names the controller it maps to and that controller's stations.
/// </summary>
[ApiController]
[Route("api/v1/pki")]
[AllowAnonymous]
public class PkiWorkstationController : ControllerBase
{
    private readonly PkiService _pki;

    public PkiWorkstationController(PkiService pki) => _pki = pki;

    /// <summary>Validates a computer certificate and returns the mapped ClientPc and its bound stations.</summary>
    [HttpPost("workstation-identity")]
    public async Task<IActionResult> ResolveWorkstation([FromBody] WorkstationIdentityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.CertificatePem) || request.CertificatePem.Length > 20_000)
            return BadRequest(new { Message = "CertificatePem is required (max 20 kB)." });
        var identity = await _pki.ResolveWorkstationAsync(request.CertificatePem);
        return Ok(new
        {
            identity.Valid,
            identity.Status,
            identity.Reason,
            ClientPcId = identity.Computer?.ClientPcId,
            Hostname = identity.Computer?.Hostname,
            MappedBy = identity.Computer?.MappedBy,
            identity.StationKeys
        });
    }
}

/// <summary>Certificate forwarded by the BFF.</summary>
public class WorkstationIdentityRequest
{
    public string CertificatePem { get; set; } = string.Empty;
}
