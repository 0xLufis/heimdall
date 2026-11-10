using Microsoft.AspNetCore.SignalR;

namespace App.Backend.Api.Hubs;

/// <summary>
/// Tenant-aware SignalR addressing for <see cref="MaintenanceHub"/>.
///
/// Every connection joins exactly the groups its tenant scope allows (see
/// <see cref="MaintenanceHub.OnConnectedAsync"/>): a member of organization X joins
/// <c>org:X</c>; platform administrators join <see cref="PlatformAdminGroup"/>. Server-side
/// publishers must address events through <see cref="ForOrganization"/> instead of
/// <c>Clients.All</c>, so one tenant's tickets and telemetry never reach another tenant.
/// </summary>
public static class MaintenanceHubGroups
{
    /// <summary>Group of platform administrators (unscoped; receive every tenant's events).</summary>
    public const string PlatformAdminGroup = "platform-admins";

    public static string Organization(string organizationId) => $"org:{organizationId}";

    /// <summary>
    /// Clients allowed to see an entity owned by <paramref name="organizationId"/>: that
    /// organization's group plus platform administrators. Entities without an organization are
    /// sent to administrators only (fail closed); tenant users still see such global rows on
    /// their next REST fetch, where the EF tenant filter applies.
    /// </summary>
    public static IMaintenanceClient ForOrganization(IHubClients<IMaintenanceClient> clients, string? organizationId)
    {
        return string.IsNullOrWhiteSpace(organizationId)
            ? clients.Group(PlatformAdminGroup)
            : clients.Groups(new[] { Organization(organizationId), PlatformAdminGroup });
    }
}
