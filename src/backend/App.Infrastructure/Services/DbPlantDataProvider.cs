namespace App.Infrastructure.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
/// Connects the sandboxed Lua runtime directly to PostgreSQL entities and in-memory telemetry buffers.
/// </summary>
public class DbPlantDataProvider : ILuaPlantDataProvider
{
    private readonly AppDbContext _context;
    private readonly IStationRepository _stationRepository;
    private readonly IMachineGroupRepository _machineGroupRepository;
    private readonly IMaintenanceTicketRepository _ticketRepository;
    private readonly ILogger<DbPlantDataProvider> _logger;

    // Buffer for telemetry metrics streamed from PLC agents
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<(double Value, DateTimeOffset Timestamp)>>> _telemetryBuffer
        = new(StringComparer.OrdinalIgnoreCase);

    // Custom fields storage for runtime dynamic machine attributes
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object?>> _customFields
        = new(StringComparer.OrdinalIgnoreCase);

    // Generic dynamic group storage
    private static readonly ConcurrentDictionary<string, HashSet<string>> _genericGroups
        = new(StringComparer.OrdinalIgnoreCase);

    public DbPlantDataProvider(
        AppDbContext context,
        IStationRepository stationRepository,
        IMachineGroupRepository machineGroupRepository,
        IMaintenanceTicketRepository ticketRepository,
        ILogger<DbPlantDataProvider> logger)
    {
        _context = context;
        _stationRepository = stationRepository;
        _machineGroupRepository = machineGroupRepository;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    // --- MACHINES ---
    public async Task<Machine?> GetMachineAsync(string machineId, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(machineId, out var guid))
        {
            var byGuid = await _stationRepository.GetByIdAsync(guid);
            if (byGuid != null) return byGuid;
        }

        // Search by CustomIdentifier or Name
        return await _context.Machines
            .Include(m => m.Components)
            .FirstOrDefaultAsync(m => m.CustomIdentifier == machineId || m.Name == machineId, cancellationToken);
    }

    public async Task<IReadOnlyList<Machine>> ListMachinesAsync(string? status = null, string? machineType = null, string? lineGroupId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Machines.AsQueryable();

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

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateMachineStatusAsync(string machineId, string status, string? reason = null, CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineAsync(machineId, cancellationToken);
        if (machine == null) return false;

        machine.Status = status;
        machine.OperationalState = status;
        if (!string.IsNullOrEmpty(reason))
        {
            machine.DecommissionReason = reason;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> SetMachineCustomFieldAsync(string machineId, string key, object? value, CancellationToken cancellationToken = default)
    {
        var map = _customFields.GetOrAdd(machineId, _ => new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
        map[key] = value;
        return Task.FromResult(true);
    }

    public Task<object?> GetMachineCustomFieldAsync(string machineId, string key, CancellationToken cancellationToken = default)
    {
        if (_customFields.TryGetValue(machineId, out var map) && map.TryGetValue(key, out var val))
        {
            return Task.FromResult<object?>(val);
        }
        return Task.FromResult<object?>(null);
    }

    // --- GROUPINGS ---
    public async Task<object?> GetGroupAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (string.Equals(groupType, "machine", StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(groupId, out var gGuid))
            {
                return await _machineGroupRepository.GetByIdAsync(gGuid, cancellationToken);
            }
            return await _context.MachineGroups.FirstOrDefaultAsync(g => g.Name == groupId, cancellationToken);
        }

        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        if (_genericGroups.ContainsKey(key))
        {
            return new { Id = groupId, GroupType = groupType };
        }

        return null;
    }

    public async Task<IReadOnlyList<object>> ListGroupsAsync(string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (string.Equals(groupType, "machine", StringComparison.OrdinalIgnoreCase))
        {
            var groups = await _machineGroupRepository.GetAllAsync(cancellationToken);
            return groups.Cast<object>().ToList();
        }

        string prefix = $"{groupType.ToLowerInvariant()}:";
        return _genericGroups.Keys
            .Where(k => k.StartsWith(prefix))
            .Select(k => (object)new { Id = k[prefix.Length..], GroupType = groupType })
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (string.Equals(groupType, "machine", StringComparison.OrdinalIgnoreCase))
        {
            Guid? gGuid = Guid.TryParse(groupId, out var g) ? g : null;
            if (gGuid == null)
            {
                var grp = await _context.MachineGroups.FirstOrDefaultAsync(x => x.Name == groupId, cancellationToken);
                gGuid = grp?.Id;
            }

            if (gGuid.HasValue)
            {
                var machineIds = await _context.Machines
                    .Where(m => m.LineGroupId == gGuid || m.SubLineGroupId == gGuid || m.ProjectGroupId == gGuid || m.BusinessUnitGroupId == gGuid)
                    .Select(m => m.Id.ToString())
                    .ToListAsync(cancellationToken);
                return machineIds;
            }
        }

        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        if (_genericGroups.TryGetValue(key, out var members))
        {
            lock (members)
            {
                return members.ToList();
            }
        }

        return Array.Empty<string>();
    }

    public Task<bool> AddGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        var set = _genericGroups.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (set)
        {
            set.Add(memberId);
        }
        return Task.FromResult(true);
    }

    public Task<bool> RemoveGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        if (_genericGroups.TryGetValue(key, out var set))
        {
            lock (set)
            {
                return Task.FromResult(set.Remove(memberId));
            }
        }
        return Task.FromResult(false);
    }

    public async Task<bool> IsGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        var members = await GetGroupMembersAsync(groupId, groupType, cancellationToken);
        return members.Contains(memberId, StringComparer.OrdinalIgnoreCase);
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
                int take = Math.Min(series.Count, count);
                var slice = series.Skip(series.Count - take).ToList();
                return Task.FromResult<IReadOnlyList<(double, DateTimeOffset)>>(slice);
            }
        }
        return Task.FromResult<IReadOnlyList<(double, DateTimeOffset)>>(Array.Empty<(double, DateTimeOffset)>());
    }

    public Task EmitTelemetryAsync(string machineId, string metric, double value, Dictionary<string, object?>? metadata = null, CancellationToken cancellationToken = default)
    {
        var metrics = _telemetryBuffer.GetOrAdd(machineId, _ => new ConcurrentDictionary<string, List<(double, DateTimeOffset)>>(StringComparer.OrdinalIgnoreCase));
        var series = metrics.GetOrAdd(metric, _ => new List<(double, DateTimeOffset)>());
        lock (series)
        {
            series.Add((value, DateTimeOffset.UtcNow));
            if (series.Count > 200)
            {
                series.RemoveAt(0);
            }
        }
        return Task.CompletedTask;
    }

    // --- TICKETS ---
    public async Task<MaintenanceTicket?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await _ticketRepository.GetByIdAsync(ticketId);
    }

    public async Task<IReadOnlyList<MaintenanceTicket>> ListTicketsAsync(string? status = null, string? machineId = null, string? priority = null, bool? isEscalated = null, CancellationToken cancellationToken = default)
    {
        var query = _context.MaintenanceTickets.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrEmpty(machineId) && Guid.TryParse(machineId, out var mGuid))
            query = query.Where(t => t.MachineId == mGuid);

        if (!string.IsNullOrEmpty(priority))
            query = query.Where(t => t.Priority == priority);

        if (isEscalated.HasValue)
            query = query.Where(t => t.IsEscalated == isEscalated.Value);

        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
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
