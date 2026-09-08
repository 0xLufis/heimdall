using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using App.Shared.Entities;

namespace App.Backend.Api.Hubs;

public interface IMaintenanceClient
{
    Task TicketCreated(MaintenanceTicket ticket);
    Task TicketUpdated(MaintenanceTicket ticket);
    Task TicketDeleted(Guid ticketId);
    Task StatusChanged(Guid ticketId, string newStatus);
    Task ReceiveNotification(string message);
    Task InventoryUpdated(string source, string hostname, string macAddress);
    Task TelemetryReceived(string hostname, string macAddress, object telemetrySummary);
}

[Authorize(Policy = "MaintenanceOperations")]
public class MaintenanceHub : Hub<IMaintenanceClient>
{
    public async Task JoinTicketGroup(string ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
    }

    public async Task LeaveTicketGroup(string ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
    }

    [Authorize(Policy = "MaintenanceOperations")]
    public async Task SendTicketUpdate(MaintenanceTicket ticket)
    {
        await Clients.All.TicketUpdated(ticket);
    }
}
