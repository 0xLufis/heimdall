using App.Backend.Api.Hubs;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace App.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaintenanceTicketController : ControllerBase
{
    private readonly IMaintenanceTicketRepository _repository;
    private readonly IHubContext<MaintenanceHub, IMaintenanceClient> _hubContext;

    public MaintenanceTicketController(
        IMaintenanceTicketRepository repository,
        IHubContext<MaintenanceHub, IMaintenanceClient> hubContext)
    {
        _repository = repository;
        _hubContext = hubContext;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<MaintenanceTicket>>> GetTickets([FromQuery] string? status)
    {
        if (!string.IsNullOrEmpty(status))
        {
            return Ok(await _repository.GetByStatusAsync(status));
        }
        return Ok(await _repository.GetAllAsync());
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<MaintenanceTicket>> GetTicket(Guid id)
    {
        var ticket = await _repository.GetByIdAsync(id);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<MaintenanceTicket>> CreateTicket([FromBody] MaintenanceTicket ticket)
    {
        var created = await _repository.CreateAsync(ticket);
        await _hubContext.Clients.All.TicketCreated(created);
        return CreatedAtAction(nameof(GetTicket), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] MaintenanceTicket ticket)
    {
        ticket.Id = id;
        var updated = await _repository.UpdateAsync(ticket);
        if (updated == null) return NotFound();
        await _hubContext.Clients.All.TicketUpdated(updated);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] string status)
    {
        var updated = await _repository.UpdateStatusAsync(id, status);
        if (updated == null) return NotFound();
        await _hubContext.Clients.All.StatusChanged(id, status);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        var deleted = await _repository.DeleteAsync(id);
        if (!deleted) return NotFound();
        await _hubContext.Clients.All.TicketDeleted(id);
        return NoContent();
    }
}
