using App.Shared.Entities;

namespace App.Infrastructure.Repositories;

public interface IMaintenanceTicketRepository
{
    Task<List<MaintenanceTicket>> GetAllAsync();
    Task<MaintenanceTicket?> GetByIdAsync(Guid id);
    Task<List<MaintenanceTicket>> GetByStatusAsync(string status);
    Task<MaintenanceTicket> CreateAsync(MaintenanceTicket ticket);
    Task<MaintenanceTicket?> UpdateAsync(MaintenanceTicket ticket);
    Task<MaintenanceTicket?> UpdateStatusAsync(Guid id, string status);
    Task<bool> DeleteAsync(Guid id);
    Task<int> GetPendingAlertsCountAsync(TimeSpan timeSpan);
    Task<List<AgentEvent>> GetRecentAgentEventsAsync(int count);
}
