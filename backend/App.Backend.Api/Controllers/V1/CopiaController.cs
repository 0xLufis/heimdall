using System.Security.Claims;
using App.Backend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class CopiaController : ControllerBase
{
    private readonly ICopiaIntegrationService _copiaService;

    public CopiaController(ICopiaIntegrationService copiaService)
    {
        _copiaService = copiaService;
    }

    /// <summary>
    /// Lists all pending cloud sync items waiting for end-user personal API key authorization.
    /// </summary>
    [HttpGet("pending-syncs")]
    [Authorize]
    public ActionResult<IEnumerable<PendingCloudSyncRecord>> GetPendingSyncs()
    {
        return Ok(_copiaService.GetPendingCloudSyncs());
    }

    /// <summary>
    /// Stage 1: Records an ADS change in the local Git repository using the 'heimdall-probe' service identity.
    /// Does not call Copia Cloud; queues a pending sync notification for an authorized engineer.
    /// </summary>
    [HttpPost("track-local")]
    [Authorize]
    public async Task<ActionResult<LocalPlcTrackingResult>> TrackLocalPlcChange(
        [FromBody] TrackLocalPlcRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _copiaService.TrackLocalPlcChangeAsync(
            request.DeviceId,
            request.RepositoryPath,
            request.Branch ?? "main",
            request.ModifiedFiles ?? Array.Empty<string>(),
            request.CommitMessage,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Stage 2: Pulls/pushes the local Git repository into Copia Cloud using the user's personal API key.
    /// Enforces strict Copia EULA compliance by prohibiting user pooling or service accounts.
    /// </summary>
    [HttpPost("sync-cloud")]
    [Authorize]
    public async Task<ActionResult<CopiaCloudSyncResult>> SyncToCopiaCloud(
        [FromBody] SyncCloudRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserApiKey))
        {
            return BadRequest(new
            {
                error = "EULA Compliance Violation: A user-specific Copia API key must be provided. Automated service accounts or user pooling are prohibited by Copia licensing terms."
            });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? request.UserEmail;
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return BadRequest(new { error = "A valid user email associated with a licensed Copia seat is required." });
        }

        var userName = User.Identity?.Name ?? request.UserDisplayName;

        try
        {
            var result = await _copiaService.SyncLocalRepoToCopiaCloudAsync(
                request.SyncId,
                request.UserApiKey,
                userEmail,
                userName,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Webhook receiver for Copia Cloud automated events (push, commit, PR).
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook(
        [FromHeader(Name = "X-Copia-Event")] string? eventType,
        [FromHeader(Name = "X-Copia-Signature-256")] string? signature,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var jsonPayload = await reader.ReadToEndAsync(cancellationToken);
        var secret = Environment.GetEnvironmentVariable("COPIA_WEBHOOK_SECRET");

        var success = await _copiaService.ProcessWebhookAsync(
            eventType ?? "push",
            jsonPayload,
            signature,
            secret,
            cancellationToken);

        return success ? Ok(new { success = true }) : BadRequest(new { success = false });
    }
}

public record TrackLocalPlcRequest(
    string DeviceId,
    string RepositoryPath,
    string? Branch,
    IReadOnlyList<string>? ModifiedFiles,
    string? CommitMessage
);

public record SyncCloudRequest(
    string SyncId,
    string UserApiKey,
    string? UserEmail,
    string? UserDisplayName
);
