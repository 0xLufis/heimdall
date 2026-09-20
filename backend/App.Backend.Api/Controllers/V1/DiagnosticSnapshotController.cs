namespace App.Backend.Api.Controllers.V1;

using System;
using System.Threading.Tasks;
using App.Contracts.Configuration;
using App.Infrastructure.Repositories;
using App.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Controller for inspecting diagnostic snapshots, verbose raw telemetry exports,
/// and development snapshot generation.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DiagnosticSnapshotController : ControllerBase
{
    private readonly IControllerRepository _repository;
    private readonly ILogger<DiagnosticSnapshotController> _logger;
    private readonly BackendFeatureFlags _featureFlags;

    public DiagnosticSnapshotController(
        IControllerRepository repository,
        ILogger<DiagnosticSnapshotController> logger,
        BackendFeatureFlags? featureFlags = null)
    {
        _repository = repository;
        _logger = logger;
        _featureFlags = featureFlags ?? new BackendFeatureFlags { EnableDevFeatures = true, EnableDebugFeatures = true };
    }

    /// <summary>
    /// Gets raw verbose diagnostic dump of a snapshot.
    /// Strictly guarded behind EnableDebugFeatures.
    /// </summary>
    [HttpGet("{id}/raw-dump")]
    public async Task<IActionResult> GetRawDump(Guid id)
    {
        if (!_featureFlags.EnableDebugFeatures)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError(
                ErrorCode.AccessDenied,
                "Diagnostic raw dumps are disabled in production mode. Set HEIMDALL_ENABLE_DEBUG=true to enable."));
        }

        var snapshot = await _repository.GetSnapshotByIdAsync(id);
        if (snapshot == null)
        {
            return NotFound(new { message = $"Snapshot '{id}' not found." });
        }

        return Ok(new
        {
            snapshot.Id,
            snapshot.ClientPcId,
            snapshot.Hostname,
            snapshot.CapturedAtUtc,
            snapshot.PayloadHashSha256,
            RawPayload = snapshot.SnapshotPayloadJson
        });
    }

    /// <summary>
    /// Dev-only snapshot seed endpoint for automated testing.
    /// Strictly guarded behind EnableDevFeatures.
    /// </summary>
    [HttpPost("dev-seed")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> DevSeedSnapshot([FromQuery] Guid clientPcId)
    {
        if (!_featureFlags.EnableDevFeatures)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError(
                ErrorCode.AccessDenied,
                "Dev snapshot generation is disabled in production mode. Set HEIMDALL_ENABLE_DEV=true to enable."));
        }

        var snapshot = await _repository.CreateDiagnosticSnapshotAsync(
            clientPcId,
            userId: User?.Identity?.Name ?? "dev-user",
            userName: "Dev Seeder",
            orgId: "dev-org");

        return Ok(snapshot);
    }
}
