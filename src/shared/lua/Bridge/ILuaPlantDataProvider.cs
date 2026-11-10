namespace App.Shared.Lua.Bridge;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Entities;

/// <summary>
/// Data bridge providing safe, decoupled access to plant entities and operations
/// for the sandboxed Lua runtime.
/// </summary>
public interface ILuaPlantDataProvider
{
    // --- MACHINES ---
    Task<Machine?> GetMachineAsync(string machineId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Machine>> ListMachinesAsync(string? status = null, string? machineType = null, string? lineGroupId = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateMachineStatusAsync(string machineId, string status, string? reason = null, CancellationToken cancellationToken = default);
    Task<bool> SetMachineCustomFieldAsync(string machineId, string key, object? value, CancellationToken cancellationToken = default);
    Task<object?> GetMachineCustomFieldAsync(string machineId, string key, CancellationToken cancellationToken = default);

    // --- GROUPINGS (Machines, Users, Telemetry, and Generic) ---
    Task<object?> GetGroupAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<object>> ListGroupsAsync(string groupType = "machine", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default);
    Task<bool> AddGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default);
    Task<bool> RemoveGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default);
    Task<bool> IsGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default);

    // --- TELEMETRY ---
    Task<double?> GetLatestTelemetryValueAsync(string machineId, string metric, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(double Value, DateTimeOffset Timestamp)>> GetTelemetryHistoryAsync(string machineId, string metric, int count = 20, CancellationToken cancellationToken = default);
    Task EmitTelemetryAsync(string machineId, string metric, double value, Dictionary<string, object?>? metadata = null, CancellationToken cancellationToken = default);

    // --- TICKETS (Automatic tickets, Analytics, SLA preventive maintenance) ---
    Task<MaintenanceTicket?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceTicket>> ListTicketsAsync(string? status = null, string? machineId = null, string? priority = null, bool? isEscalated = null, CancellationToken cancellationToken = default);
    Task<MaintenanceTicket> CreateTicketAsync(MaintenanceTicket ticket, CancellationToken cancellationToken = default);
    Task<bool> UpdateTicketStatusAsync(Guid ticketId, string status, string? notes = null, CancellationToken cancellationToken = default);
    Task<bool> EscalateTicketAsync(Guid ticketId, string reason, string escalatedBy, string? target = null, CancellationToken cancellationToken = default);
}
