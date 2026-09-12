using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for managing Machine (Station) entities using repository interfaces.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MachineController : ControllerBase
{
    private readonly IStationRepository _stationRepository;

    public MachineController(IStationRepository stationRepository)
    {
        _stationRepository = stationRepository;
    }

    [HttpGet]
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
            MachineType = m.MachineType,
            GroupId = m.GroupId,
            PreferredTechnicianId = m.PreferredTechnicianId,
            PreferredTechnicianName = m.PreferredTechnicianName,
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
            }).ToList()
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("by-technology")]
    public async Task<IActionResult> GetMachinesByTechnology()
    {
        var machines = await _stationRepository.GetAllAsync();
        var grouped = machines
            .GroupBy(m => string.IsNullOrWhiteSpace(m.MachineType) ? "General Assembly" : m.MachineType)
            .Select(g => new
            {
                Technology = g.Key,
                MachineCount = g.Count(),
                Machines = g.Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.DisplayName,
                    m.CustomIdentifier,
                    m.MachineType,
                    m.GroupId,
                    ControllersCount = m.Controllers.Count,
                    Controllers = m.Controllers.Select(c => new { c.Id, c.Hostname, c.IpAddress, c.LastOnline })
                }).ToList()
            })
            .OrderByDescending(g => g.MachineCount)
            .ToList();

        return Ok(grouped);
    }

    [HttpGet("by-line")]
    public async Task<IActionResult> GetMachinesByLine()
    {
        var machines = await _stationRepository.GetAllAsync();
        var grouped = machines
            .GroupBy(m => string.IsNullOrWhiteSpace(m.GroupId) ? "Unassigned Line" : m.GroupId)
            .Select(g => new
            {
                LineId = g.Key,
                LineName = g.Key,
                MachineCount = g.Count(),
                ControllersCount = g.Sum(m => m.Controllers.Count),
                Machines = g.Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.DisplayName,
                    m.CustomIdentifier,
                    m.MachineType,
                    m.GroupId,
                    Controllers = m.Controllers.Select(c => new { c.Id, c.Hostname, c.IpAddress, c.LastOnline })
                }).ToList()
            })
            .OrderBy(g => g.LineName)
            .ToList();

        return Ok(grouped);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Machine>> GetMachine(Guid id)
    {
        var machine = await _stationRepository.GetByIdAsync(id);
        if (machine == null) return NotFound();
        return Ok(machine);
    }

    [HttpPost]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<Machine>> CreateMachine(Machine machine)
    {
        var created = await _stationRepository.CreateAsync(machine);
        return CreatedAtAction(nameof(GetMachine), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> UpdateMachine(Guid id, [FromBody] App.Backend.Api.Dtos.MachineUpdateDto update)
    {
        var updated = await _stationRepository.UpdateAsync(
            id,
            update.Name,
            update.CustomIdentifier,
            update.PinnedObjectHandle,
            null,
            update.ControllerIds
        );

        if (updated == null) return NotFound();
        return NoContent();
    }
}
