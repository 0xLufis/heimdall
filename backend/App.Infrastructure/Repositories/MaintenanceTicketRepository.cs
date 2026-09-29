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

    public async Task<List<MaintenanceTicket>> GetByDepartmentAsync(string department)
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Machine)
            .Include(t => t.ClientPc)
            .Include(t => t.Equipment)
            .Where(t => t.ResponsibleDepartment != null && t.ResponsibleDepartment.ToLower() == department.ToLower())
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<MaintenanceTicket> CreateAsync(MaintenanceTicket ticket)
    {
        if (ticket.Id == Guid.Empty)
        {
            ticket.Id = Guid.NewGuid();
        }
        if (ticket.CreatedAt == default)
        {
            ticket.CreatedAt = DateTimeOffset.UtcNow;
        }
        if (ticket.Status.Equals("InProgress", StringComparison.OrdinalIgnoreCase))
        {
            ticket.StartedAt ??= ticket.CreatedAt;
        }
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
        existing.PendingReason = ticket.PendingReason;
        existing.PendingDetails = ticket.PendingDetails;
        existing.IsEscalated = ticket.IsEscalated;
        existing.EscalationReason = ticket.EscalationReason;
        existing.EscalatedAt = ticket.EscalatedAt;
        existing.EscalatedBy = ticket.EscalatedBy;
        existing.EscalationTarget = ticket.EscalationTarget;
        existing.EscalationHandoverState = ticket.EscalationHandoverState;
        existing.EscalationClosedAt = ticket.EscalationClosedAt;
        existing.EscalationClosedBy = ticket.EscalationClosedBy;
        existing.Priority = ticket.Priority;
        existing.IsLineStop = ticket.IsLineStop;
        existing.LineStopDurationMinutes = ticket.LineStopDurationMinutes;
        existing.ResponsibleDepartment = ticket.ResponsibleDepartment;
        existing.OriginatorType = ticket.OriginatorType;
        existing.IssueType = ticket.IssueType;
        existing.ExternalOperatorId = ticket.ExternalOperatorId;
        existing.ExternalOperatorName = ticket.ExternalOperatorName;
        existing.StartedAt = ticket.StartedAt;
        existing.QrScannedAt = ticket.QrScannedAt;
        existing.ReactionTimeMinutes = ticket.ReactionTimeMinutes;
        existing.ReservedBy = ticket.ReservedBy;
        existing.TelemetrySnapshot = ticket.TelemetrySnapshot;
        existing.Tags = ticket.Tags;
        existing.ChangeHistory = ticket.ChangeHistory;
        existing.MachineId = ticket.MachineId;
        existing.ClientPcId = ticket.ClientPcId;
        existing.AssetId = ticket.AssetId;
        existing.AssignedTo = ticket.AssignedTo;

        if (ticket.Status.Equals("InProgress", StringComparison.OrdinalIgnoreCase))
        {
            existing.StartedAt ??= DateTimeOffset.UtcNow;
            if (existing.ReactionTimeMinutes == null)
            {
                existing.ReactionTimeMinutes = (existing.StartedAt.Value - existing.CreatedAt).TotalMinutes;
            }
        }
        else if (ticket.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || 
                 ticket.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
        {
            existing.ResolvedAt ??= DateTimeOffset.UtcNow;
            if (existing.IsLineStop && existing.LineStopDurationMinutes == null)
            {
                existing.LineStopDurationMinutes = (DateTimeOffset.UtcNow - existing.CreatedAt).TotalMinutes;
            }
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<MaintenanceTicket?> UpdateStatusAsync(Guid id, string status)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(id);
        if (existing == null) return null;

        existing.Status = status;
        if (status.Equals("InProgress", StringComparison.OrdinalIgnoreCase))
        {
            existing.StartedAt ??= DateTimeOffset.UtcNow;
            if (existing.ReactionTimeMinutes == null)
            {
                existing.ReactionTimeMinutes = (existing.StartedAt.Value - existing.CreatedAt).TotalMinutes;
            }
        }
        else if (status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || 
                 status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
        {
            existing.ResolvedAt = DateTimeOffset.UtcNow;
            if (existing.IsLineStop && existing.LineStopDurationMinutes == null)
            {
                existing.LineStopDurationMinutes = (DateTimeOffset.UtcNow - existing.CreatedAt).TotalMinutes;
            }
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<MaintenanceTicket?> EscalateAsync(
        Guid id,
        string reason,
        string escalatedBy,
        App.Contracts.Enums.EscalationHandoverState handoverState = App.Contracts.Enums.EscalationHandoverState.Notification,
        string? target = null)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(id);
        if (existing == null) return null;

        existing.IsEscalated = true;
        existing.EscalationReason = reason;
        existing.EscalatedAt = DateTimeOffset.UtcNow;
        existing.EscalatedBy = escalatedBy;
        existing.EscalationHandoverState = handoverState;
        existing.EscalationTarget = target ?? "DedicatedEngineer";
        existing.EscalationClosedAt = null;
        existing.EscalationClosedBy = null;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<MaintenanceTicket?> DeescalateAsync(Guid id, string resolvedBy, string? resolutionNotes = null)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(id);
        if (existing == null) return null;

        existing.IsEscalated = false;
        existing.EscalationClosedAt = DateTimeOffset.UtcNow;
        existing.EscalationClosedBy = resolvedBy;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<MaintenanceTicket?> SetPendingAsync(Guid id, App.Contracts.Enums.PendingReason reason, string? details)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(id);
        if (existing == null) return null;

        existing.Status = "Pending";
        existing.PendingReason = reason;
        existing.PendingDetails = details;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<MaintenanceTicket?> ReserveAsync(Guid id, string technician)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(id);
        if (existing == null) return null;

        existing.ReservedBy = technician;
        existing.AssignedTo = technician;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<MaintenanceTicket?> QrPickupAsync(Guid id, string technician)
    {
        var existing = await _context.MaintenanceTickets.FindAsync(id);
        if (existing == null) return null;

        var now = DateTimeOffset.UtcNow;
        existing.QrScannedAt = now;
        existing.StartedAt = now;
        existing.Status = "InProgress";
        if (!string.IsNullOrWhiteSpace(technician))
        {
            existing.AssignedTo = technician;
            existing.ReservedBy = technician;
        }
        existing.ReactionTimeMinutes = (now - existing.CreatedAt).TotalMinutes;

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
