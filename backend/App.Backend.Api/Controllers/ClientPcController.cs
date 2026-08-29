using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing Client PCs using repository interfaces.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientPcController : ControllerBase
{
    private readonly IControllerRepository _repository;
    private readonly ILogger<ClientPcController> _logger;

    public ClientPcController(IControllerRepository repository, ILogger<ClientPcController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.ClientPcDto>>> GetClientPcs()
    {
        var pcs = await _repository.GetAllAsync();
        var dtos = pcs.Select(c => new App.Backend.Api.Dtos.ClientPcDto
        {
            Id = c.Id,
            Name = c.Hostname ?? c.Name,
            DisplayName = c.Hostname ?? c.Name,
            OrganizationId = "Heimdall Root",
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
            InventoryItems = c.InventoryItems.Select(MapToInventoryItemDto).ToList()
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
    public async Task<ActionResult<ClientPc>> CreateClientPc(ClientPc pc)
    {
        var createdPc = await _repository.CreateAsync(pc);
        return CreatedAtAction(nameof(GetClientPc), new { id = createdPc.Id }, createdPc);
    }

    [HttpPut("{id}")]
    [AllowAnonymous]
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
}
