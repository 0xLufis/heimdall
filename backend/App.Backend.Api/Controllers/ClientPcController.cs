using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing Client PCs using the refactored model.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientPcController : ControllerBase
{
    private readonly ClientPcRepository _repository;
    private readonly ILogger<ClientPcController> _logger;

    public ClientPcController(ClientPcRepository repository, ILogger<ClientPcController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.ClientPcDto>>> GetClientPcs(
        [FromServices] AppDbContext dbContext)
    {
        var pcs = await dbContext.ClientPcs
            .Select(c => new App.Backend.Api.Dtos.ClientPcDto
            {
                Id = c.Id,
                Name = c.Hostname ?? c.Name,
                DisplayName = c.Hostname ?? c.Name, // Default to hostname
                OrganizationId = "Heimdall Root", // Default for now
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
                InventoryItems = c.InventoryItems.Select(i => new App.Backend.Api.Dtos.InventoryItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    DisplayName = i.DisplayName,
                    ItemType = i.GetType().Name,
                    Metadata = i.Metadata,
                    Children = i.Children.Select(ci => new App.Backend.Api.Dtos.InventoryItemDto
                    {
                        Id = ci.Id,
                        Name = ci.Name,
                        DisplayName = ci.DisplayName,
                        ItemType = ci.GetType().Name,
                        Metadata = ci.Metadata,
                        Children = ci.Children.Select(gci => new App.Backend.Api.Dtos.InventoryItemDto
                        {
                            Id = gci.Id,
                            Name = gci.Name,
                            DisplayName = gci.DisplayName,
                            ItemType = gci.GetType().Name,
                            Metadata = gci.Metadata,
                            Children = gci.Children.Select(ggci => new App.Backend.Api.Dtos.InventoryItemDto
                            {
                                Id = ggci.Id,
                                Name = ggci.Name,
                                DisplayName = ggci.DisplayName,
                                ItemType = ggci.GetType().Name,
                                Metadata = ggci.Metadata,
                                Children = ggci.Children.Select(gggci => new App.Backend.Api.Dtos.InventoryItemDto
                                {
                                    Id = gggci.Id,
                                    Name = gggci.Name,
                                    DisplayName = gggci.DisplayName,
                                    ItemType = gggci.GetType().Name,
                                    Metadata = gggci.Metadata
                                }).ToList()
                            }).ToList()
                        }).ToList()
                    }).ToList()
                }).ToList()
            })
            .ToListAsync();
        return Ok(pcs);
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
    public async Task<IActionResult> UpdateClientPc(Guid id, [FromBody] App.Backend.Api.Dtos.ClientPcUpdateDto update, [FromServices] AppDbContext dbContext)
    {
        var pc = await dbContext.ClientPcs
            .Include(c => c.ControlledMachines)
            .Include(c => c.ResponsibleTeams)
            .FirstOrDefaultAsync(c => c.Id == id);
            
        if (pc == null) return NotFound();

        if (!string.IsNullOrEmpty(update.PinnedObjectHandle)) pc.PinnedObjectHandle = update.PinnedObjectHandle;
        if (!string.IsNullOrEmpty(update.Name)) pc.Name = update.Name;
        if (!string.IsNullOrEmpty(update.Hostname)) pc.Hostname = update.Hostname;
        if (!string.IsNullOrEmpty(update.MacAddress)) pc.MacAddress = update.MacAddress;
        
        // Update Controlled Machines
        if (update.ControlledMachineIds != null)
        {
            pc.ControlledMachines.Clear();
            foreach (var machineId in update.ControlledMachineIds)
            {
                var existingMachine = await dbContext.Machines.FindAsync(machineId);
                if (existingMachine != null)
                {
                    pc.ControlledMachines.Add(existingMachine);
                }
            }
        }
        
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
