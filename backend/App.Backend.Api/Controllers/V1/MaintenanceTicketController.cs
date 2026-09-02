using App.Backend.Api.Hubs;
using App.Backend.Api.Services;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class MaintenanceTicketController : ControllerBase
{
    private readonly IMaintenanceTicketRepository _repository;
    private readonly IHubContext<MaintenanceHub, IMaintenanceClient> _hubContext;
    private readonly ICacheService _cache;
    private readonly IDbContextFactory<AppDbContext>? _dbContextFactory;

    public MaintenanceTicketController(
        IMaintenanceTicketRepository repository,
        IHubContext<MaintenanceHub, IMaintenanceClient> hubContext,
        ICacheService cache,
        IDbContextFactory<AppDbContext>? dbContextFactory = null)
    {
        _repository = repository;
        _hubContext = hubContext;
        _cache = cache;
        _dbContextFactory = dbContextFactory;
    }

    [HttpGet]
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
        }, TimeSpan.FromMinutes(2));

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MaintenanceTicket>> GetTicket(Guid id)
    {
        var ticket = await _repository.GetByIdAsync(id);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<ActionResult<MaintenanceTicket>> CreateTicket(MaintenanceTicket ticket)
    {
        var created = await _repository.CreateAsync(ticket);
        await _cache.RemoveAsync("tickets:all:all");
        await _cache.RemoveAsync("dashboard:metrics");
        await _cache.SetAsync($"tickets:item:{created.Id}", created, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.TicketCreated(created);
        return CreatedAtAction(nameof(GetTicket), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<IActionResult> UpdateTicket(Guid id, MaintenanceTicket ticket)
    {
        if (id != ticket.Id) return BadRequest();
        var updated = await _repository.UpdateAsync(ticket);
        if (updated == null) return NotFound();

        await _cache.RemoveAsync("tickets:all:all");
        await _cache.SetAsync($"tickets:item:{id}", updated, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.TicketUpdated(updated);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] string status)
    {
        var updated = await _repository.UpdateStatusAsync(id, status);
        if (updated == null) return NotFound();

        await _cache.RemoveAsync("tickets:all:all");
        await _cache.SetAsync($"tickets:item:{id}", updated, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.StatusChanged(id, status);
        await _hubContext.Clients.All.TicketUpdated(updated);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        var success = await _repository.DeleteAsync(id);
        if (!success) return NotFound();

        await _cache.RemoveAsync("tickets:all:all");
        await _cache.RemoveAsync($"tickets:item:{id}");
        await _hubContext.Clients.All.TicketDeleted(id);
        return NoContent();
    }
}
