using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class MaintenanceTicketRepository : IMaintenanceTicketRepository
{
    private readonly AppDbContext _context;

    public MaintenanceTicketRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<MaintenanceTicket>> GetAllAsync()
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Machine)
            .Include(t => t.ClientPc)
            .Include(t => t.Equipment)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<MaintenanceTicket?> GetByIdAsync(Guid id)
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Machine)
            .Include(t => t.ClientPc)
            .Include(t => t.Equipment)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<MaintenanceTicket>> GetByStatusAsync(string status)
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Machine)
            .Include(t => t.ClientPc)
            .Include(t => t.Equipment)
            .Where(t => t.Status.ToLower() == status.ToLower())
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<MaintenanceTicket> CreateAsync(MaintenanceTicket ticket)
    {
        if (ticket.Id == Guid.Empty)
        {
            ticket.Id = Guid.NewGuid();
        }
        ticket.CreatedAt = DateTimeOffset.UtcNow;
        _context.MaintenanceTickets.Add(ticket);
        await _context.SaveChangesAsync();
        return ticket;
    }

    public async Task<MaintenanceTicket?> UpdateAsync(MaintenanceTicket ticket)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(ticket.Id);
        if (existing == null) return null;

        existing.Title = ticket.Title;
        existing.Description = ticket.Description;
        existing.Status = ticket.Status;
        existing.Priority = ticket.Priority;
        existing.MachineId = ticket.MachineId;
        existing.ClientPcId = ticket.ClientPcId;
        existing.AssetId = ticket.AssetId;
        existing.AssignedTo = ticket.AssignedTo;
        if (ticket.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || 
            ticket.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
        {
            existing.ResolvedAt ??= DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<MaintenanceTicket?> UpdateStatusAsync(Guid id, string status)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(id);
        if (existing == null) return null;

        existing.Status = status;
        if (status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || 
            status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
        {
            existing.ResolvedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var ticket = await _context.MaintenanceTickets.FindAsync(id);
        if (ticket == null) return false;
        _context.MaintenanceTickets.Remove(ticket);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetPendingAlertsCountAsync(TimeSpan timeSpan)
    {
        var threshold = DateTime.UtcNow.Subtract(timeSpan);
        try
        {
            return await _context.AgentEvents.CountAsync(e => 
                (e.Level == "Warning" || e.Level == "Error" || e.Level == "Critical") && 
                e.Timestamp >= threshold);
        }
        catch
        {
            return 0;
        }
    }

    public async Task<List<AgentEvent>> GetRecentAgentEventsAsync(int count)
    {
        try
        {
            return await _context.AgentEvents
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch
        {
            return new List<AgentEvent>();
        }
    }
}
