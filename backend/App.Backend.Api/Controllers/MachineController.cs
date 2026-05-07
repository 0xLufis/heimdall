using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing Machine (Station) entities using the refactored model.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MachineController : ControllerBase
{
    private readonly AppDbContext _context;

    public MachineController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.MachineDto>>> GetMachines()
    {
        return await _context.Machines
            .Select(m => new App.Backend.Api.Dtos.MachineDto
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
                Children = m.Children.Select(c => new App.Backend.Api.Dtos.InventoryItemDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    DisplayName = c.DisplayName,
                    ItemType = c.GetType().Name,
                    Metadata = c.Metadata,
                    Children = c.Children.Select(ci => new App.Backend.Api.Dtos.InventoryItemDto
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
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<Machine>> CreateMachine(Machine machine)
    {
        _context.Machines.Add(machine);
        await _context.SaveChangesAsync();
        return Ok(machine);
    }

    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateMachine(Guid id, [FromBody] App.Backend.Api.Dtos.MachineUpdateDto machineUpdate)
    {
        var machine = await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.ResponsibleTeams)
            .FirstOrDefaultAsync(m => m.Id == id);
            
        if (machine == null) return NotFound();

        if (!string.IsNullOrEmpty(machineUpdate.Name)) machine.Name = machineUpdate.Name;
        if (!string.IsNullOrEmpty(machineUpdate.CustomIdentifier)) machine.CustomIdentifier = machineUpdate.CustomIdentifier;
        if (!string.IsNullOrEmpty(machineUpdate.PinnedObjectHandle)) machine.PinnedObjectHandle = machineUpdate.PinnedObjectHandle;
        if (!string.IsNullOrEmpty(machineUpdate.OrganizationId)) machine.OrganizationId = machineUpdate.OrganizationId;
        
        // Update controllers (ClientPcs)
        if (machineUpdate.ControllerIds != null)
        {
            machine.Controllers.Clear();
            foreach (var pcId in machineUpdate.ControllerIds)
            {
                var existingPc = await _context.ClientPcs.FindAsync(pcId);
                if (existingPc != null)
                {
                    machine.Controllers.Add(existingPc);
                }
            }
        }
        
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
