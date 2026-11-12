namespace App.Infrastructure.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Database-backed implementation of ILuaPlantDataProvider for the Heimdall production backend.
/// Machines, groups (machine + personnel), tickets and machine custom fields are read from and
/// written to PostgreSQL. Telemetry is served from a bounded process-local ring buffer fed by
/// the Lua telemetry evaluation pipeline (there is no historical telemetry store yet).
/// </summary>
public class DbPlantDataProvider : ILuaPlantDataProvider
{
    /// <summary>Key inside <see cref="BaseInventoryItem.Metadata"/> holding Lua-managed custom fields.</summary>
    public const string CustomFieldsMetadataKey = "luaCustomFields";

    private const int MaxCustomFieldsPerMachine = 100;
    private const int MaxCustomFieldKeyLength = 64;
    private const int MaxCustomFieldValueBytes = 8 * 1024;
    private const int MaxListResults = 1000;
    private const int MaxTelemetryMachines = 10_000;
    private const int MaxTelemetryMetricsPerMachine = 200;
    private const int MaxTelemetryPointsPerSeries = 200;

    private readonly AppDbContext _context;
    private readonly IMachineGroupRepository _machineGroupRepository;
    private readonly IMaintenanceTicketRepository _ticketRepository;
    private readonly ILogger<DbPlantDataProvider> _logger;

    // Recent telemetry points per machine/metric. Process-local and bounded in every dimension.
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<(double Value, DateTimeOffset Timestamp)>>> _telemetryBuffer
        = new(StringComparer.OrdinalIgnoreCase);

    public DbPlantDataProvider(
        AppDbContext context,
        IMachineGroupRepository machineGroupRepository,
        IMaintenanceTicketRepository ticketRepository,
        ILogger<DbPlantDataProvider> logger)
    {
        _context = context;
        _machineGroupRepository = machineGroupRepository;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    // --- MACHINES ---
    public async Task<Machine?> GetMachineAsync(string machineId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(machineId)) return null;

        if (Guid.TryParse(machineId, out var guid))
        {
            var byGuid = await _context.Machines.FirstOrDefaultAsync(m => m.Id == guid, cancellationToken);
            if (byGuid != null) return byGuid;
        }

        return await _context.Machines
            .FirstOrDefaultAsync(m => m.CustomIdentifier == machineId || m.Name == machineId, cancellationToken);
    }

    public async Task<IReadOnlyList<Machine>> ListMachinesAsync(string? status = null, string? machineType = null, string? lineGroupId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Machines.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(m => m.Status == status);
        }

        if (!string.IsNullOrEmpty(machineType))
        {
            query = query.Where(m => m.MachineType == machineType);
        }

        if (!string.IsNullOrEmpty(lineGroupId))
        {
            if (Guid.TryParse(lineGroupId, out var lGuid))
            {
                query = query.Where(m => m.LineGroupId == lGuid || m.SubLineGroupId == lGuid);
            }
            else
            {
                query = query.Where(m => m.GroupId == lineGroupId);
            }
        }

        return await query.OrderBy(m => m.Name).Take(MaxListResults).ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateMachineStatusAsync(string machineId, string status, string? reason = null, CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineAsync(machineId, cancellationToken);
        if (machine == null) return false;

        machine.Status = status;
        machine.OperationalState = status;
        if (!string.IsNullOrEmpty(reason) && string.Equals(status, "decommissioned", StringComparison.OrdinalIgnoreCase))
        {
            machine.DecommissionReason = reason;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetMachineCustomFieldAsync(string machineId, string key, object? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxCustomFieldKeyLength) return false;

        var machine = await GetMachineAsync(machineId, cancellationToken);
        if (machine == null) return false;

        var valueNode = JsonSerializer.SerializeToNode(value);
        if (valueNode != null && valueNode.ToJsonString().Length > MaxCustomFieldValueBytes) return false;

        var root = machine.Metadata != null
            ? JsonNode.Parse(machine.Metadata.RootElement.GetRawText()) as JsonObject ?? new JsonObject()
            : new JsonObject();
        var fields = root[CustomFieldsMetadataKey] as JsonObject ?? new JsonObject();
        if (!fields.ContainsKey(key) && fields.Count >= MaxCustomFieldsPerMachine) return false;

        fields[key] = valueNode;
        root[CustomFieldsMetadataKey] = fields;
        machine.Metadata = JsonDocument.Parse(root.ToJsonString());

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object?> GetMachineCustomFieldAsync(string machineId, string key, CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineAsync(machineId, cancellationToken);
        if (machine?.Metadata == null) return null;

        if (machine.Metadata.RootElement.ValueKind == JsonValueKind.Object &&
            machine.Metadata.RootElement.TryGetProperty(CustomFieldsMetadataKey, out var fields) &&
            fields.ValueKind == JsonValueKind.Object &&
            fields.TryGetProperty(key, out var value))
        {
            return value.Clone();
        }
        return null;
    }

    // --- GROUPINGS ---
    // Supported group types: "machine" (MachineGroups + memberships) and "user" (PersonnelGroups).
    // Other types have no backing store and are reported as unsupported rather than faked.

    public async Task<object?> GetGroupAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (IsMachineGroup(groupType))
        {
            return await ResolveMachineGroupAsync(groupId, cancellationToken);
        }
        if (IsUserGroup(groupType))
        {
            var group = await ResolvePersonnelGroupAsync(groupId, cancellationToken);
            return group == null ? null : new { Id = group.Id.ToString(), group.Name, group.Description, GroupType = "user" };
        }
        return null;
    }

    public async Task<IReadOnlyList<object>> ListGroupsAsync(string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (IsMachineGroup(groupType))
        {
            var groups = await _machineGroupRepository.GetAllAsync(cancellationToken);
            return groups.Cast<object>().ToList();
        }
        if (IsUserGroup(groupType))
        {
            var groups = await _context.PersonnelGroups.AsNoTracking()
                .OrderBy(g => g.Name)
                .Take(MaxListResults)
                .Select(g => new { Id = g.Id.ToString(), g.Name, g.Description, GroupType = "user" })
                .ToListAsync(cancellationToken);
            return groups.Cast<object>().ToList();
        }
        return Array.Empty<object>();
    }

    public async Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (IsMachineGroup(groupType))
        {
            var group = await ResolveMachineGroupAsync(groupId, cancellationToken);
            if (group == null) return Array.Empty<string>();
            var gid = group.Id;

            var hierarchyMembers = await _context.Machines.AsNoTracking()
                .Where(m => m.LineGroupId == gid || m.SubLineGroupId == gid || m.ProjectGroupId == gid || m.BusinessUnitGroupId == gid)
                .Select(m => m.Id)
                .Take(MaxListResults)
                .ToListAsync(cancellationToken);
            var explicitMembers = await _context.MachineGroupMemberships.AsNoTracking()
                .Where(x => x.GroupId == gid)
                .Select(x => x.MachineId)
                .Take(MaxListResults)
                .ToListAsync(cancellationToken);

            return hierarchyMembers.Concat(explicitMembers).Distinct().Select(id => id.ToString()).ToList();
        }

        if (IsUserGroup(groupType))
        {
            var group = await ResolvePersonnelGroupAsync(groupId, cancellationToken);
            if (group == null) return Array.Empty<string>();
            return await _context.PersonnelGroupMembers.AsNoTracking()
                .Where(m => m.PersonnelGroupId == group.Id)
                .Select(m => m.UserId)
                .Take(MaxListResults)
                .ToListAsync(cancellationToken);
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Organisation write scope of machine-group memberships changed by Lua, the same rule as
    /// <c>MachineGroupController</c> (platform administrators: any group; an organisation's groups:
    /// members of that organisation; groups without organisation: platform administrators and
    /// EngineeringAdministration). Scripts run without a user or organisation: the automation host
    /// executes them in its own scope, triggered by telemetry, schedules, SLA checks or UI actions, and
    /// only EngineeringAdministration may author them. So a script may change the platform-managed
    /// groups (no organisation), which is exactly what its authors may change through the API, and is
    /// refused for organisation-owned groups: there is no organisation membership to check, and
    /// allowing it would let any script author, or a user who merely triggers a script, rewrite
    /// another organisation's machine groups.
    /// </summary>
    public static bool ScriptMayChangeMachineGroupMembers(MachineGroup group) => string.IsNullOrWhiteSpace(group.OrganizationId);

    private bool RefuseOrganizationGroupWrite(MachineGroup group, string action)
    {
        if (ScriptMayChangeMachineGroupMembers(group)) return false;
        _logger.LogWarning("Lua {Action} on machine group {Group} refused: the group belongs to organization {Organization} and scripts run without an organization",
            action, group.Id, group.OrganizationId);
        return true;
    }

    public async Task<bool> AddGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (IsMachineGroup(groupType))
        {
            var group = await ResolveMachineGroupAsync(groupId, cancellationToken);
            var machine = await GetMachineAsync(memberId, cancellationToken);
            if (group == null || machine == null) return false;
            if (RefuseOrganizationGroupWrite(group, "add_member")) return false;

            bool exists = await _context.MachineGroupMemberships
                .AnyAsync(x => x.GroupId == group.Id && x.MachineId == machine.Id, cancellationToken);
            if (!exists)
            {
                _context.MachineGroupMemberships.Add(new MachineGroupMembership
                {
                    GroupId = group.Id,
                    MachineId = machine.Id,
                    AssignedBy = "LuaAutomation"
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        if (IsUserGroup(groupType))
        {
            if (memberId.Length > 128) return false;
            var group = await ResolvePersonnelGroupAsync(groupId, cancellationToken);
            if (group == null) return false;

            bool exists = await _context.PersonnelGroupMembers
                .AnyAsync(m => m.PersonnelGroupId == group.Id && m.UserId == memberId, cancellationToken);
            if (!exists)
            {
                _context.PersonnelGroupMembers.Add(new PersonnelGroupMember { PersonnelGroupId = group.Id, UserId = memberId });
                await _context.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        return false;
    }

    public async Task<bool> RemoveGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (IsMachineGroup(groupType))
        {
            var group = await ResolveMachineGroupAsync(groupId, cancellationToken);
            var machine = await GetMachineAsync(memberId, cancellationToken);
            if (group == null || machine == null) return false;
            if (RefuseOrganizationGroupWrite(group, "remove_member")) return false;

            var rows = await _context.MachineGroupMemberships
                .Where(x => x.GroupId == group.Id && x.MachineId == machine.Id)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0) return false;
            _context.MachineGroupMemberships.RemoveRange(rows);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (IsUserGroup(groupType))
        {
            var group = await ResolvePersonnelGroupAsync(groupId, cancellationToken);
            if (group == null) return false;
            var rows = await _context.PersonnelGroupMembers
                .Where(m => m.PersonnelGroupId == group.Id && m.UserId == memberId)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0) return false;
            _context.PersonnelGroupMembers.RemoveRange(rows);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> IsGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        var members = await GetGroupMembersAsync(groupId, groupType, cancellationToken);
        if (members.Contains(memberId, StringComparer.OrdinalIgnoreCase)) return true;

        if (IsMachineGroup(groupType) && !Guid.TryParse(memberId, out _))
        {
            var machine = await GetMachineAsync(memberId, cancellationToken);
            return machine != null && members.Contains(machine.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool IsMachineGroup(string? groupType) =>
        string.IsNullOrEmpty(groupType) || string.Equals(groupType, "machine", StringComparison.OrdinalIgnoreCase);

    private static bool IsUserGroup(string? groupType) =>
        string.Equals(groupType, "user", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(groupType, "personnel", StringComparison.OrdinalIgnoreCase);

    private async Task<MachineGroup?> ResolveMachineGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(groupId)) return null;
        if (Guid.TryParse(groupId, out var gGuid))
        {
            return await _machineGroupRepository.GetByIdAsync(gGuid, cancellationToken);
        }
        return await _context.MachineGroups.FirstOrDefaultAsync(g => g.Name == groupId, cancellationToken);
    }

    private async Task<PersonnelGroup?> ResolvePersonnelGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(groupId)) return null;
        if (Guid.TryParse(groupId, out var gGuid))
        {
            return await _context.PersonnelGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gGuid, cancellationToken);
        }
        return await _context.PersonnelGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Name == groupId, cancellationToken);
    }

    // --- TELEMETRY ---
    public Task<double?> GetLatestTelemetryValueAsync(string machineId, string metric, CancellationToken cancellationToken = default)
    {
        if (_telemetryBuffer.TryGetValue(machineId, out var metrics) &&
            metrics.TryGetValue(metric, out var series))
        {
            lock (series)
            {
                if (series.Count > 0)
                {
                    return Task.FromResult<double?>(series[^1].Value);
                }
            }
        }
        return Task.FromResult<double?>(null);
    }

    public Task<IReadOnlyList<(double Value, DateTimeOffset Timestamp)>> GetTelemetryHistoryAsync(string machineId, string metric, int count = 20, CancellationToken cancellationToken = default)
    {
        if (_telemetryBuffer.TryGetValue(machineId, out var metrics) &&
            metrics.TryGetValue(metric, out var series))
        {
            lock (series)
            {
                int take = Math.Clamp(count, 0, series.Count);
                var slice = series.Skip(series.Count - take).ToList();
                return Task.FromResult<IReadOnlyList<(double, DateTimeOffset)>>(slice);
            }
        }
        return Task.FromResult<IReadOnlyList<(double, DateTimeOffset)>>(Array.Empty<(double, DateTimeOffset)>());
    }

    public Task EmitTelemetryAsync(string machineId, string metric, double value, Dictionary<string, object?>? metadata = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(metric) ||
            machineId.Length > 128 || metric.Length > 128)
        {
            return Task.CompletedTask;
        }

        if (!_telemetryBuffer.TryGetValue(machineId, out var metrics))
        {
            if (_telemetryBuffer.Count >= MaxTelemetryMachines)
            {
                _logger.LogWarning("Lua telemetry buffer machine limit reached; dropping point for {MachineId}/{Metric}", machineId, metric);
                return Task.CompletedTask;
            }
            metrics = _telemetryBuffer.GetOrAdd(machineId, _ => new ConcurrentDictionary<string, List<(double, DateTimeOffset)>>(StringComparer.OrdinalIgnoreCase));
        }

        if (!metrics.TryGetValue(metric, out var series))
        {
            if (metrics.Count >= MaxTelemetryMetricsPerMachine) return Task.CompletedTask;
            series = metrics.GetOrAdd(metric, _ => new List<(double, DateTimeOffset)>());
        }

        lock (series)
        {
            series.Add((value, DateTimeOffset.UtcNow));
            if (series.Count > MaxTelemetryPointsPerSeries)
            {
                series.RemoveAt(0);
            }
        }
        return Task.CompletedTask;
    }

    // --- TICKETS ---
    public async Task<MaintenanceTicket?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await _context.MaintenanceTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceTicket>> ListTicketsAsync(string? status = null, string? machineId = null, string? priority = null, bool? isEscalated = null, CancellationToken cancellationToken = default)
    {
        var query = _context.MaintenanceTickets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrEmpty(machineId))
        {
            Guid? mGuid = Guid.TryParse(machineId, out var parsed) ? parsed : (await GetMachineAsync(machineId, cancellationToken))?.Id;
            if (mGuid == null) return Array.Empty<MaintenanceTicket>();
            query = query.Where(t => t.MachineId == mGuid);
        }

        if (!string.IsNullOrEmpty(priority))
            query = query.Where(t => t.Priority == priority);

        if (isEscalated.HasValue)
            query = query.Where(t => t.IsEscalated == isEscalated.Value);

        return await query.OrderByDescending(t => t.CreatedAt).Take(MaxListResults).ToListAsync(cancellationToken);
    }

    public async Task<MaintenanceTicket> CreateTicketAsync(MaintenanceTicket ticket, CancellationToken cancellationToken = default)
    {
        return await _ticketRepository.CreateAsync(ticket);
    }

    public async Task<bool> UpdateTicketStatusAsync(Guid ticketId, string status, string? notes = null, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null) return false;

        ticket.Status = status;
        if (!string.IsNullOrEmpty(notes))
        {
            ticket.Description = $"{ticket.Description}\n[Status Updated to {status}]: {notes}";
        }
        if (status == "Closed" || status == "Resolved")
        {
            ticket.ResolvedAt = DateTimeOffset.UtcNow;
        }
        else if (status == "InProgress" && !ticket.StartedAt.HasValue)
        {
            ticket.StartedAt = DateTimeOffset.UtcNow;
        }

        await _ticketRepository.UpdateAsync(ticket);
        return true;
    }

    public async Task<bool> EscalateTicketAsync(Guid ticketId, string reason, string escalatedBy, string? target = null, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null) return false;

        ticket.IsEscalated = true;
        ticket.EscalationReason = reason;
        ticket.EscalatedBy = escalatedBy;
        ticket.EscalatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(target))
        {
            ticket.AssignedTo = target;
        }

        await _ticketRepository.UpdateAsync(ticket);
        return true;
    }
}
