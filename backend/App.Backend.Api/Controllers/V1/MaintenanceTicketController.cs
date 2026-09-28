using App.Backend.Api.Hubs;
using App.Backend.Api.Services;
using App.Contracts.Caching;
using App.Contracts.Enums;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers.V1;

public record EscalateTicketRequest(string Reason, string EscalatedBy);
public record DeescalateTicketRequest(string ResolvedBy, string? ResolutionNotes = null);
public record SetPendingRequest(PendingReason PendingReason, string? PendingDetails);

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
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
        var cacheKey = CacheKeyFactory.Tickets.All(status);
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
        if (ticket == null) return NotFound(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.TicketNotFound));
        return Ok(ticket);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<ActionResult<MaintenanceTicket>> CreateTicket(MaintenanceTicket ticket)
    {
        if (_dbContextFactory != null)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            if (!string.IsNullOrWhiteSpace(ticket.CreatedBy))
            {
                var userExists = await db.AuthUsers.AnyAsync(u => u.Id == ticket.CreatedBy || u.Name == ticket.CreatedBy || u.Email == ticket.CreatedBy);
                if (!userExists)
                {
                    db.AuthUsers.Add(new AuthUser
                    {
                        Id = ticket.CreatedBy.StartsWith("usr-") ? ticket.CreatedBy : $"usr-{Guid.NewGuid():N}"[..12],
                        Name = ticket.CreatedBy,
                        Email = ticket.CreatedBy.Contains('@') ? ticket.CreatedBy : $"{ticket.CreatedBy.ToLowerInvariant().Replace(" ", ".")}@factory.corp",
                        Role = HeimdallRoles.Operator
                    });
                    await db.SaveChangesAsync();
                }
            }
            if (!string.IsNullOrWhiteSpace(ticket.AssignedTo))
            {
                var techExists = await db.AuthUsers.AnyAsync(u => u.Id == ticket.AssignedTo || u.Name == ticket.AssignedTo || u.Email == ticket.AssignedTo);
                if (!techExists)
                {
                    db.AuthUsers.Add(new AuthUser
                    {
                        Id = ticket.AssignedTo.StartsWith("usr-") ? ticket.AssignedTo : $"usr-{Guid.NewGuid():N}"[..12],
                        Name = ticket.AssignedTo,
                        Email = ticket.AssignedTo.Contains('@') ? ticket.AssignedTo : $"{ticket.AssignedTo.ToLowerInvariant().Replace(" ", ".")}@factory.corp",
                        Role = HeimdallRoles.Technician
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        var created = await _repository.CreateAsync(ticket);
        await InvalidateTicketCachesAsync(created.Id);
        await _cache.SetAsync(CacheKeyFactory.Tickets.Item(created.Id), created, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.TicketCreated(created);
        return CreatedAtAction(nameof(GetTicket), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> UpdateTicket(Guid id, MaintenanceTicket ticket)
    {
        if (id != ticket.Id) return BadRequest(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.InvalidInput, "Route ID does not match ticket ID."));
        var updated = await _repository.UpdateAsync(ticket);
        if (updated == null) return NotFound(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.TicketNotFound));

        await InvalidateTicketCachesAsync(id);
        await _cache.SetAsync(CacheKeyFactory.Tickets.Item(id), updated, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.TicketUpdated(updated);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] string status)
    {
        var updated = await _repository.UpdateStatusAsync(id, status);
        if (updated == null) return NotFound(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.TicketNotFound));

        await InvalidateTicketCachesAsync(id);
        await _cache.SetAsync(CacheKeyFactory.Tickets.Item(id), updated, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.StatusChanged(id, status);
        await _hubContext.Clients.All.TicketUpdated(updated);
        return NoContent();
    }

    /// <summary>
    /// Escalates a maintenance ticket orthogonally without altering its status.
    /// A ticket can be escalated in any status.
    /// </summary>
    [HttpPost("{id}/escalate")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> EscalateTicket(Guid id, [FromBody] EscalateTicketRequest request)
    {
        var updated = await _repository.EscalateAsync(id, request.Reason, request.EscalatedBy);
        if (updated == null) return NotFound(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.TicketNotFound));

        await InvalidateTicketCachesAsync(id);
        await _cache.SetAsync(CacheKeyFactory.Tickets.Item(id), updated, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.TicketUpdated(updated);
        return Ok(updated);
    }

    /// <summary>
    /// Closes an escalation independently of the ticket lifecycle.
    /// </summary>
    [HttpPost("{id}/de-escalate")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> DeescalateTicket(Guid id, [FromBody] DeescalateTicketRequest request)
    {
        var updated = await _repository.DeescalateAsync(id, request.ResolvedBy, request.ResolutionNotes);
        if (updated == null) return NotFound(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.TicketNotFound));

        await InvalidateTicketCachesAsync(id);
        await _cache.SetAsync(CacheKeyFactory.Tickets.Item(id), updated, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.TicketUpdated(updated);
        return Ok(updated);
    }

    /// <summary>
    /// Transitions ticket into Pending status with a specific sub-reason (Parts, ExternalOk, Action).
    /// </summary>
    [HttpPatch("{id}/pending")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> SetPending(Guid id, [FromBody] SetPendingRequest request)
    {
        var updated = await _repository.SetPendingAsync(id, request.PendingReason, request.PendingDetails);
        if (updated == null) return NotFound(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.TicketNotFound));

        await InvalidateTicketCachesAsync(id);
        await _cache.SetAsync(CacheKeyFactory.Tickets.Item(id), updated, TimeSpan.FromMinutes(5));
        await _hubContext.Clients.All.StatusChanged(id, "Pending");
        await _hubContext.Clients.All.TicketUpdated(updated);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        var success = await _repository.DeleteAsync(id);
        if (!success) return NotFound(new App.Shared.Errors.ApiError(App.Shared.Errors.ErrorCode.TicketNotFound));

        await InvalidateTicketCachesAsync(id);
        await _hubContext.Clients.All.TicketDeleted(id);
        return NoContent();
    }

    private async Task InvalidateTicketCachesAsync(Guid id)
    {
        await _cache.RemoveAsync(CacheKeyFactory.Tickets.All(null));
        await _cache.RemoveAsync(CacheKeyFactory.Tickets.All("open"));
        await _cache.RemoveAsync(CacheKeyFactory.Tickets.All("inprogress"));
        await _cache.RemoveAsync(CacheKeyFactory.Tickets.All("pending"));
        await _cache.RemoveAsync(CacheKeyFactory.Tickets.All("resolved"));
        await _cache.RemoveAsync(CacheKeyFactory.Tickets.All("closed"));
        await _cache.RemoveAsync(CacheKeyFactory.Tickets.Item(id));
        await _cache.RemoveAsync(CacheKeyFactory.Dashboard.Metrics);
    }
}
