using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using App.Backend.Api.Security;
using App.Contracts.Telemetry;
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
    Task PlcMemoryReceived(string hostname, string machineIdentifier, string amsNetId, string symbolName, object memoryData);
    /// <summary>Station production KPIs derived from ingested PLC telemetry (sent to the station's org group).</summary>
    Task StationTelemetryReceived(string stationId, StationTelemetryBroadcastDto stationKpi);
    /// <summary>Station machine dynamics derived from ingested PLC telemetry (sent to the station's org group).</summary>
    Task StationOscillationReceived(string stationId, StationOscillationBroadcastDto oscillationData);
}

[Authorize(Policy = "MaintenanceOperations")]
public class MaintenanceHub : Hub<IMaintenanceClient>
{
    private readonly IAuthorizationService _authorization;

    public MaintenanceHub(IAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    /// <summary>
    /// Places the connection into the groups its tenant scope allows, using the same rules as
    /// <see cref="TenantScopeMiddleware"/>: platform administrators join the admin group,
    /// organization members join <c>org:{OrgId}</c>, and callers without an organization join
    /// nothing (they receive no tenant events).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        foreach (var group in await ResolveGroupsAsync(Context.User, _authorization))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }
        await base.OnConnectedAsync();
    }

    public static async Task<IReadOnlyList<string>> ResolveGroupsAsync(
        System.Security.Claims.ClaimsPrincipal? user, IAuthorizationService authorization)
    {
        if (user?.Identity?.IsAuthenticated != true) return Array.Empty<string>();

        var scope = await TenantScopeMiddleware.ResolveScopeAsync(user, authorization);
        if (scope == null) return new[] { MaintenanceHubGroups.PlatformAdminGroup };
        if (scope == TenantScopeMiddleware.NoOrganizationSentinel) return Array.Empty<string>();
        return new[] { MaintenanceHubGroups.Organization(scope) };
    }

    // Note: there are intentionally no client-invokable methods. Ticket and telemetry events are
    // published server-side only (controllers / TelemetryIngestionService via IHubContext), and
    // group membership is decided by the server from the caller's claims. The former
    // JoinTicketGroup/LeaveTicketGroup let any caller join arbitrary groups and were unused.
}
