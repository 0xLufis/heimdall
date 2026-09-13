using App.Backend.Api.Services;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for managing Client PCs / Industrial Controllers.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ClientPcController : ControllerBase
{
    private readonly IControllerRepository _repository;
    private readonly ILogger<ClientPcController> _logger;
    private readonly ICacheService? _cache;

    public ClientPcController(
        IControllerRepository repository, 
        ILogger<ClientPcController> logger,
        ICacheService? cache = null)
    {
        _repository = repository;
        _logger = logger;
        _cache = cache;
    }

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

    [HttpPost]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<ClientPc>> CreateClientPc(ClientPc pc)
    {
        var createdPc = await _repository.CreateAsync(pc);
        return CreatedAtAction(nameof(GetClientPc), new { id = createdPc.Id }, createdPc);
    }

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

    [HttpDelete("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> DeleteClientPc(Guid id)
    {
        var deleted = await _repository.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

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

    [HttpGet("{id}/snapshots/{snapshotId}/download")]
    public async Task<IActionResult> DownloadDiagnosticSnapshot(Guid id, Guid snapshotId)
    {
        var snapshot = await _repository.GetSnapshotByIdAsync(snapshotId);
        if (snapshot == null || snapshot.ClientPcId != id)
        {
            return NotFound(new { message = $"Snapshot '{snapshotId}' was not found for this node." });
        }

        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(snapshot.SnapshotPayloadJson);
        string filename = $"diagnostic_snapshot_{snapshot.Hostname}_{snapshot.CapturedAtUtc:yyyyMMdd_HHmmss}.json";

        return File(payloadBytes, "application/json", filename);
    }
}
