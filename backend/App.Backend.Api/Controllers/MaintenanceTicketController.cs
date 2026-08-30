using App.Backend.Api.Hubs;
using App.Backend.Api.Services;
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
    private readonly ICacheService _cache;

    public MaintenanceTicketController(
        IMaintenanceTicketRepository repository,
        IHubContext<MaintenanceHub, IMaintenanceClient> hubContext,
        ICacheService cache)
    {
        _repository = repository;
        _hubContext = hubContext;
        _cache = cache;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<MaintenanceTicket>>> GetTickets([FromQuery] string? status)
    {
        var cacheKey = $"tickets:all:{status ?? "all"}";
        var result = await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            if (!string.IsNullOrEmpty(status))
            {
                return await _repository.GetByStatusAsync(status);
            }
            return await _repository.GetAllAsync();
        }, TimeSpan.FromMinutes(10));

        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<MaintenanceTicket>> GetTicket(Guid id)
    {
        var cacheKey = $"tickets:item:{id}";
        var ticket = await _cache.GetOrSetAsync(cacheKey, () => _repository.GetByIdAsync(id), TimeSpan.FromMinutes(15));
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<MaintenanceTicket>> CreateTicket([FromBody] MaintenanceTicket ticket)
    {
        var created = await _repository.CreateAsync(ticket);

        // On-demand write-through and invalidation
        await _cache.SetAsync($"tickets:item:{created.Id}", created, TimeSpan.FromMinutes(15));
        await _cache.RemoveByPatternAsync("tickets:all*");
        await _cache.RemoveAsync("dashboard:metrics");

        // Broadcast real-time event to all connected clients
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

        // On-demand write-through and invalidation
        await _cache.SetAsync($"tickets:item:{id}", updated, TimeSpan.FromMinutes(15));
        await _cache.RemoveByPatternAsync("tickets:all*");
        await _cache.RemoveAsync("dashboard:metrics");

        // Broadcast real-time update
        await _hubContext.Clients.All.TicketUpdated(updated);

        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] string status)
    {
        var updated = await _repository.UpdateStatusAsync(id, status);
        if (updated == null) return NotFound();

        // On-demand write-through and invalidation
        await _cache.SetAsync($"tickets:item:{id}", updated, TimeSpan.FromMinutes(15));
        await _cache.RemoveByPatternAsync("tickets:all*");
        await _cache.RemoveAsync("dashboard:metrics");

        // Broadcast real-time status change and full updated ticket
        await _hubContext.Clients.All.StatusChanged(id, status);
        await _hubContext.Clients.All.TicketUpdated(updated);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        var deleted = await _repository.DeleteAsync(id);
        if (!deleted) return NotFound();

        // On-demand cache purge
        await _cache.RemoveAsync($"tickets:item:{id}");
        await _cache.RemoveByPatternAsync("tickets:all*");
        await _cache.RemoveAsync("dashboard:metrics");

        // Broadcast real-time deletion
        await _hubContext.Clients.All.TicketDeleted(id);

        return NoContent();
    }
}
