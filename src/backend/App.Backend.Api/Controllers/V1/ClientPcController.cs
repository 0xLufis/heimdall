using App.Backend.Api.Services;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for managing Client PCs / Industrial Controllers, hardware metadata,
/// diagnostics, and point-in-time system snapshots.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ClientPcController : ControllerBase
{
    private readonly IControllerRepository _repository;
    private readonly ILogger<ClientPcController> _logger;
    private readonly ICacheService? _cache;
    private readonly App.Contracts.Configuration.BackendFeatureFlags _featureFlags;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientPcController"/>.
    /// </summary>
    /// <param name="repository">Controller and asset repository.</param>
    /// <param name="logger">Structured logger instance.</param>
    /// <param name="cache">Optional distributed or in-memory cache service.</param>
    /// <param name="featureFlags">Backend feature flags governing debug exports.</param>
    public ClientPcController(
        IControllerRepository repository,
        ILogger<ClientPcController> logger,
        ICacheService? cache = null,
        App.Contracts.Configuration.BackendFeatureFlags? featureFlags = null)
    {
        _repository = repository;
        _logger = logger;
        _cache = cache;
        _featureFlags = featureFlags ?? new App.Contracts.Configuration.BackendFeatureFlags { EnableDevFeatures = true, EnableDebugFeatures = true };
    }

    /// <summary>
    /// Retrieves all registered client PCs / industrial controllers with cached summary models.
    /// </summary>
    /// <returns>Collection of client PC summary DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.ClientPcDto>>> GetClientPcs()
    {
        if (_cache != null)
        {
            var cached = await _cache.GetAsync<List<App.Backend.Api.Dtos.ClientPcDto>>("inventory:client_pcs");
            if (cached != null)
            {
                return Ok(cached);
            }
        }

        var pcs = await _repository.GetAllAsync();
        var dtos = pcs.Select(c => new App.Backend.Api.Dtos.ClientPcDto
        {
            Id = c.Id,
            Name = c.Hostname ?? c.Name,
            DisplayName = c.Hostname ?? c.Name,
            OrganizationId = c.OrganizationId ?? "Heimdall Root",
            Hostname = c.Hostname,
            MacAddress = c.MacAddress,
            MachineIdentifier = c.MachineIdentifier,
            PinnedObjectHandle = c.PinnedObjectHandle,
            LastSeen = c.LastOnline,
            Machines = c.ControlledMachines.Select(m => new App.Backend.Api.Dtos.MachineSummaryDto
            {
                Id = m.Id,
                CustomIdentifier = m.CustomIdentifier,
                PinnedObjectHandle = m.PinnedObjectHandle,
                Name = m.Name
            }).ToList(),
            ResponsibleTeams = c.ResponsibleTeams.Select(t => new App.Backend.Api.Dtos.TeamSummaryDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList(),
            InventoryItems = c.InventoryItems.Select(MapToInventoryItemDto).ToList(),
            FreeDiskSpace = c.FreeDiskSpace,
            SystemMetadata = c.SystemMetadata,
            ResourceAverages = c.ResourceAverages
        }).ToList();

        if (_cache != null)
        {
            await _cache.SetAsync("inventory:client_pcs", dtos, TimeSpan.FromMinutes(2));
        }

        return Ok(dtos);
    }

    private static App.Backend.Api.Dtos.InventoryItemDto MapToInventoryItemDto(BaseInventoryItem item)
    {
        return new App.Backend.Api.Dtos.InventoryItemDto
        {
            Id = item.Id,
            Name = item.Name,
            DisplayName = item.DisplayName,
            ItemType = item.GetType().Name,
            Metadata = item.Metadata,
            Children = item.Children.Select(MapToInventoryItemDto).ToList()
        };
    }

    /// <summary>
    /// Retrieves a specific client PC entity by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the client PC.</param>
    [HttpGet("{id}")]
    public async Task<ActionResult<ClientPc>> GetClientPc(Guid id)
    {
        var pc = await _repository.GetByIdAsync(id);
        if (pc == null)
        {
            return NotFound();
        }
        return Ok(pc);
    }

    /// <summary>
    /// Registers a new client PC in the system.
    /// </summary>
    /// <param name="pc">Client PC registration entity.</param>
    [HttpPost]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<ClientPc>> CreateClientPc(ClientPc pc)
    {
        var createdPc = await _repository.CreateAsync(pc);
        return CreatedAtAction(nameof(GetClientPc), new { id = createdPc.Id }, createdPc);
    }

    /// <summary>
    /// Updates configuration, network hostname, and machine assignments for a client PC.
    /// </summary>
    /// <param name="id">Unique identifier of the client PC to update.</param>
    /// <param name="update">Updated controller attributes.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> UpdateClientPc(Guid id, [FromBody] App.Backend.Api.Dtos.ClientPcUpdateDto update)
    {
        var pc = await _repository.UpdateAsync(
            id,
            update.Name,
            update.Hostname,
            update.MacAddress,
            update.PinnedObjectHandle,
            update.ControlledMachineIds
        );

        if (pc == null) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Deletes a client PC record and its associations.
    /// </summary>
    /// <param name="id">Unique identifier of the client PC to delete.</param>
    [HttpDelete("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> DeleteClientPc(Guid id)
    {
        var deleted = await _repository.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Captures a point-in-time diagnostic snapshot of an industrial controller's state.
    /// </summary>
    /// <param name="id">Unique identifier of the target client PC.</param>
    [HttpPost("{id}/snapshot")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<App.Backend.Api.Dtos.DiagnosticSnapshotDetailDto>> CaptureDiagnosticSnapshot(Guid id)
    {
        try
        {
            string userId = User.Identity?.Name ?? "system";
            string? userName = User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "System Engineer";
            string? orgId = Request.Headers["X-Organization-Id"].FirstOrDefault();

            var snapshot = await _repository.CreateDiagnosticSnapshotAsync(id, userId, userName, orgId);

            _logger.LogInformation("Captured diagnostic snapshot '{SnapshotId}' for ClientPc '{ClientPcId}' ({Hostname})",
                snapshot.Id, id, snapshot.Hostname);

            return Ok(new App.Backend.Api.Dtos.DiagnosticSnapshotDetailDto
            {
                Id = snapshot.Id,
                ClientPcId = snapshot.ClientPcId,
                Hostname = snapshot.Hostname,
                MachineIdentifier = snapshot.MachineIdentifier,
                CapturedByUserId = snapshot.CapturedByUserId,
                CapturedByUserName = snapshot.CapturedByUserName,
                CapturedAtUtc = snapshot.CapturedAtUtc,
                PayloadHashSha256 = snapshot.PayloadHashSha256,
                PayloadSizeBytes = System.Text.Encoding.UTF8.GetByteCount(snapshot.SnapshotPayloadJson),
                SnapshotPayloadJson = snapshot.SnapshotPayloadJson
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Client PC '{id}' was not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture diagnostic snapshot for ClientPc '{ClientPcId}'", id);
            return StatusCode(500, new { message = "Failed to capture diagnostic snapshot.", error = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves all historical diagnostic snapshot summaries captured for a client PC.
    /// </summary>
    /// <param name="id">Unique identifier of the target client PC.</param>
    [HttpGet("{id}/snapshots")]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.DiagnosticSnapshotSummaryDto>>> GetDiagnosticSnapshots(Guid id)
    {
        var snapshots = await _repository.GetSnapshotsByClientPcIdAsync(id);
        var dtos = snapshots.Select(s => new App.Backend.Api.Dtos.DiagnosticSnapshotSummaryDto
        {
            Id = s.Id,
            ClientPcId = s.ClientPcId,
            Hostname = s.Hostname,
            MachineIdentifier = s.MachineIdentifier,
            CapturedByUserId = s.CapturedByUserId,
            CapturedByUserName = s.CapturedByUserName,
            CapturedAtUtc = s.CapturedAtUtc,
            PayloadHashSha256 = s.PayloadHashSha256,
            PayloadSizeBytes = System.Text.Encoding.UTF8.GetByteCount(s.SnapshotPayloadJson)
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Retrieves a detailed diagnostic snapshot by snapshot ID for a client PC.
    /// </summary>
    /// <param name="id">Client PC identifier.</param>
    /// <param name="snapshotId">Diagnostic snapshot identifier.</param>
    [HttpGet("{id}/snapshots/{snapshotId}")]
    public async Task<ActionResult<App.Backend.Api.Dtos.DiagnosticSnapshotDetailDto>> GetDiagnosticSnapshot(Guid id, Guid snapshotId)
    {
        var snapshot = await _repository.GetSnapshotByIdAsync(snapshotId);
        if (snapshot == null || snapshot.ClientPcId != id)
        {
            return NotFound(new { message = $"Snapshot '{snapshotId}' was not found for this node." });
        }

        return Ok(new App.Backend.Api.Dtos.DiagnosticSnapshotDetailDto
        {
            Id = snapshot.Id,
            ClientPcId = snapshot.ClientPcId,
            Hostname = snapshot.Hostname,
            MachineIdentifier = snapshot.MachineIdentifier,
            CapturedByUserId = snapshot.CapturedByUserId,
            CapturedByUserName = snapshot.CapturedByUserName,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            PayloadHashSha256 = snapshot.PayloadHashSha256,
            PayloadSizeBytes = System.Text.Encoding.UTF8.GetByteCount(snapshot.SnapshotPayloadJson),
            SnapshotPayloadJson = snapshot.SnapshotPayloadJson
        });
    }

    /// <summary>
    /// Exports and downloads a full JSON diagnostic snapshot payload for offline analysis.
    /// Guarded behind EnableDebugFeatures.
    /// </summary>
    /// <param name="id">Client PC identifier.</param>
    /// <param name="snapshotId">Diagnostic snapshot identifier.</param>
    [HttpGet("{id}/snapshots/{snapshotId}/download")]
    public async Task<IActionResult> DownloadDiagnosticSnapshot(Guid id, Guid snapshotId)
    {
        if (!_featureFlags.EnableDebugFeatures)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new App.Shared.Errors.ApiError(
                App.Shared.Errors.ErrorCode.AccessDenied,
                "Verbose diagnostic snapshot exports and dumps are disabled in production. Set HEIMDALL_ENABLE_DEBUG=true to enable."));
        }

        var snapshot = await _repository.GetSnapshotByIdAsync(snapshotId);
        if (snapshot == null || snapshot.ClientPcId != id)
        {
            return NotFound(new { message = $"Snapshot '{snapshotId}' was not found for this node." });
        }

        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(snapshot.SnapshotPayloadJson);
        string filename = $"diagnostic_snapshot_{snapshot.Hostname}_{snapshot.CapturedAtUtc:yyyyMMdd_HHmmss}.json";

        return File(payloadBytes, "application/json", filename);
    }

    /// <summary>
    /// Verbose raw telemetry and diagnostic dump export for industrial controllers.
    /// Strictly guarded behind EnableDebugFeatures.
    /// </summary>
    [HttpGet("{id}/telemetry/raw-dump")]
    public async Task<IActionResult> GetRawTelemetryDump(Guid id)
    {
        if (!_featureFlags.EnableDebugFeatures)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new App.Shared.Errors.ApiError(
                App.Shared.Errors.ErrorCode.AccessDenied,
                "Raw telemetry debug dumps are disabled in production. Set HEIMDALL_ENABLE_DEBUG=true to enable."));
        }

        var pc = await _repository.GetByIdAsync(id);
        if (pc == null) return NotFound();

        return Ok(new
        {
            pc.Id,
            pc.Hostname,
            pc.MacAddress,
            pc.LastOnline,
            ResourceAverages = pc.ResourceAverages,
            FreeDiskSpace = pc.FreeDiskSpace,
            SystemMetadata = pc.SystemMetadata,
            InventoryCount = pc.InventoryItems.Count,
            ControlledMachinesCount = pc.ControlledMachines.Count
        });
    }
}
