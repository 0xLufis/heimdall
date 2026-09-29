using App.Shared.Entities;

namespace App.Infrastructure.Repositories;

public interface IMaintenanceTicketRepository
{
    Task<List<MaintenanceTicket>> GetAllAsync();
    Task<MaintenanceTicket?> GetByIdAsync(Guid id);
    Task<List<MaintenanceTicket>> GetByStatusAsync(string status);
    Task<List<MaintenanceTicket>> GetByDepartmentAsync(string department);
    Task<MaintenanceTicket> CreateAsync(MaintenanceTicket ticket);
    Task<MaintenanceTicket?> UpdateAsync(MaintenanceTicket ticket);
    Task<MaintenanceTicket?> UpdateStatusAsync(Guid id, string status);
    Task<MaintenanceTicket?> EscalateAsync(Guid id, string reason, string escalatedBy, App.Contracts.Enums.EscalationHandoverState handoverState = App.Contracts.Enums.EscalationHandoverState.Notification, string? target = null);
    Task<MaintenanceTicket?> DeescalateAsync(Guid id, string resolvedBy, string? resolutionNotes = null);
    Task<MaintenanceTicket?> SetPendingAsync(Guid id, App.Contracts.Enums.PendingReason reason, string? details);
    Task<MaintenanceTicket?> ReserveAsync(Guid id, string technician);
    Task<MaintenanceTicket?> QrPickupAsync(Guid id, string technician);
    Task<bool> DeleteAsync(Guid id);
    Task<int> GetPendingAlertsCountAsync(TimeSpan timeSpan);
    Task<List<AgentEvent>> GetRecentAgentEventsAsync(int count);
}
