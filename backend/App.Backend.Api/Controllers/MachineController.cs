using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing Machine (Station) entities using repository interfaces.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MachineController : ControllerBase
{
    private readonly IStationRepository _stationRepository;

    public MachineController(IStationRepository stationRepository)
    {
        _stationRepository = stationRepository;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.MachineDto>>> GetMachines()
    {
        var machines = await _stationRepository.GetAllAsync();
        var dtos = machines.Select(m => new App.Backend.Api.Dtos.MachineDto
        {
            Id = m.Id,
            Name = m.Name,
            DisplayName = m.DisplayName,
            OrganizationId = m.OrganizationId ?? "Heimdall Root",
            CustomIdentifier = m.CustomIdentifier,
            PinnedObjectHandle = m.PinnedObjectHandle,
            Controllers = m.Controllers.Select(c => new App.Backend.Api.Dtos.ClientPcSummaryDto
            {
                Id = c.Id,
                Hostname = c.Hostname,
                PinnedObjectHandle = c.PinnedObjectHandle,
                Name = c.Name
            }).ToList(),
            ResponsibleTeams = m.ResponsibleTeams.Select(t => new App.Backend.Api.Dtos.TeamSummaryDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList(),
            Children = m.Children.Select(c => MapToInventoryItemDto(c)).ToList()
        }).ToList();

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

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<Machine>> CreateMachine(Machine machine)
    {
        var created = await _stationRepository.CreateAsync(machine);
        return Ok(created);
    }

    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateMachine(Guid id, [FromBody] App.Backend.Api.Dtos.MachineUpdateDto machineUpdate)
    {
        var updated = await _stationRepository.UpdateAsync(
            id,
            machineUpdate.Name,
            machineUpdate.CustomIdentifier,
            machineUpdate.PinnedObjectHandle,
            machineUpdate.OrganizationId,
            machineUpdate.ControllerIds
        );

        if (updated == null) return NotFound();
        return NoContent();
    }
}
