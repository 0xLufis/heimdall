namespace App.Shared.Lua.Bridge;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Entities;

/// <summary>
/// Thread-safe in-memory implementation of ILuaPlantDataProvider with seeded sample data.
/// Test/dev double only: production DI registers DbPlantDataProvider and nothing falls back to this type.
/// </summary>
public class InMemoryPlantDataProvider : ILuaPlantDataProvider
{
    private readonly ConcurrentDictionary<string, Machine> _machines = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object?>> _machineCustomFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MachineGroup> _machineGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _groupMemberships = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<(double Value, DateTimeOffset Timestamp)>>> _telemetryStore = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, MaintenanceTicket> _tickets = new();

    public InMemoryPlantDataProvider()
    {
        SeedSampleData();
    }

    private void SeedSampleData()
    {
        // Seed Machines
        var m1 = new Machine
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "CNC Milling Station 01",
            CustomIdentifier = "L01-CNC-01",
            MachineType = "Milling",
            Status = "active",
            OperationalState = "Running",
            Criticality = "Tier1_Critical",
            Technologies = new List<string> { "Machining", "CNC", "Cooling" },
            Spoc = "Sarah Miller",
            SpocUserId = "usr-tech-01",
            Owner = "Line 01 Production",
            GroupId = "grp-line-01"
        };

        var m2 = new Machine
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Robotic Welder Cell 02",
            CustomIdentifier = "L01-WLD-02",
            MachineType = "Welding",
            Status = "active",
            OperationalState = "Running",
            Criticality = "Tier1_Critical",
            Technologies = new List<string> { "Robotics", "Welding" },
            Spoc = "Alex Kovacs",
            SpocUserId = "usr-tech-02",
            Owner = "Line 01 Production",
            GroupId = "grp-line-01"
        };

        var m3 = new Machine
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Optical Inspection SMT 03",
            CustomIdentifier = "L02-AOI-03",
            MachineType = "Automatic Optical Inspection",
            Status = "maintenance",
            OperationalState = "Maintenance",
            Criticality = "Tier2_Major",
            Technologies = new List<string> { "Vision", "SMT" },
            Spoc = "Elena Rossi",
            SpocUserId = "usr-tech-03",
            Owner = "Line 02 SMT",
            GroupId = "grp-line-02"
        };

        _machines[m1.Id.ToString()] = m1;
        _machines[m1.CustomIdentifier] = m1;
        _machines[m2.Id.ToString()] = m2;
        _machines[m2.CustomIdentifier] = m2;
        _machines[m3.Id.ToString()] = m3;
        _machines[m3.CustomIdentifier] = m3;

        // Seed Machine Groups
        var mgLine1 = new MachineGroup
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = "Production Line 01",
            Description = "Main powertrain machining and welding line",
            Color = "#2563eb"
        };
        _machineGroups[mgLine1.Id.ToString()] = mgLine1;
        _machineGroups["grp-line-01"] = mgLine1;
        _machineGroups["Line 01"] = mgLine1;

        // Group Memberships
        SetGroupMembers("machine", mgLine1.Id.ToString(), new[] { m1.Id.ToString(), m2.Id.ToString() });
        SetGroupMembers("machine", "grp-line-01", new[] { m1.Id.ToString(), m2.Id.ToString() });

        // User / Technician Group Memberships
        SetGroupMembers("user", "tier1-responders", new[] { "usr-tech-01", "usr-tech-02" });
        SetGroupMembers("user", "electrical-specialists", new[] { "usr-tech-02", "usr-tech-03" });

        // Seed Telemetry
        RecordTelemetry(m1.Id.ToString(), "spindle_vibration", 0.045);
        RecordTelemetry(m1.Id.ToString(), "spindle_vibration", 0.048);
        RecordTelemetry(m1.Id.ToString(), "spindle_vibration", 0.052);
        RecordTelemetry(m1.Id.ToString(), "temperature", 68.5);
        RecordTelemetry(m1.Id.ToString(), "runtime_hours", 1420.0);
        RecordTelemetry(m1.Id.ToString(), "cycle_time", 42.1);

        RecordTelemetry(m2.Id.ToString(), "weld_current", 125.0);
        RecordTelemetry(m2.Id.ToString(), "temperature", 74.0);
        RecordTelemetry(m2.Id.ToString(), "runtime_hours", 2150.0);

        // Seed Tickets
        var t1 = new MaintenanceTicket
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999991"),
            Title = "Spindle Calibration Due",
            Description = "Preventive maintenance: 1000h spindle calibration inspection.",
            Status = "Open",
            Priority = "Medium",
            MachineId = m1.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-10),
            AssignedTo = "Sarah Miller"
        };
        _tickets[t1.Id] = t1;
    }

    private void SetGroupMembers(string groupType, string groupId, IEnumerable<string> members)
    {
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        _groupMemberships[key] = new HashSet<string>(members, StringComparer.OrdinalIgnoreCase);
    }

    private void RecordTelemetry(string machineId, string metric, double value)
    {
        var machineMetrics = _telemetryStore.GetOrAdd(machineId, _ => new ConcurrentDictionary<string, List<(double, DateTimeOffset)>>(StringComparer.OrdinalIgnoreCase));
        var series = machineMetrics.GetOrAdd(metric, _ => new List<(double, DateTimeOffset)>());
        lock (series)
        {
            series.Add((value, DateTimeOffset.UtcNow));
        }
    }

    // --- MACHINES ---
    public Task<Machine?> GetMachineAsync(string machineId, CancellationToken cancellationToken = default)
    {
        _machines.TryGetValue(machineId, out var machine);
        return Task.FromResult(machine);
    }

    public Task<IReadOnlyList<Machine>> ListMachinesAsync(string? status = null, string? machineType = null, string? lineGroupId = null, CancellationToken cancellationToken = default)
    {
        // Distinct by Id since machines are indexed by both Guid and CustomIdentifier
        var query = _machines.Values.GroupBy(m => m.Id).Select(g => g.First()).AsEnumerable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(m => string.Equals(m.Status, status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(machineType))
            query = query.Where(m => string.Equals(m.MachineType, machineType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(lineGroupId))
            query = query.Where(m => string.Equals(m.GroupId, lineGroupId, StringComparison.OrdinalIgnoreCase) ||
                                     m.LineGroupId?.ToString() == lineGroupId);

        return Task.FromResult<IReadOnlyList<Machine>>(query.ToList());
    }

    public Task<bool> UpdateMachineStatusAsync(string machineId, string status, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (_machines.TryGetValue(machineId, out var machine))
        {
            machine.Status = status;
            machine.OperationalState = status;
            if (!string.IsNullOrEmpty(reason))
            {
                machine.DecommissionReason = reason;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> SetMachineCustomFieldAsync(string machineId, string key, object? value, CancellationToken cancellationToken = default)
    {
        var fields = _machineCustomFields.GetOrAdd(machineId, _ => new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
        fields[key] = value;
        return Task.FromResult(true);
    }

    public Task<object?> GetMachineCustomFieldAsync(string machineId, string key, CancellationToken cancellationToken = default)
    {
        if (_machineCustomFields.TryGetValue(machineId, out var fields) && fields.TryGetValue(key, out var val))
        {
            return Task.FromResult<object?>(val);
        }
        return Task.FromResult<object?>(null);
    }

    // --- GROUPINGS ---
    public Task<object?> GetGroupAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (string.Equals(groupType, "machine", StringComparison.OrdinalIgnoreCase))
        {
            _machineGroups.TryGetValue(groupId, out var grp);
            return Task.FromResult<object?>(grp);
        }
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        if (_groupMemberships.ContainsKey(key))
        {
            return Task.FromResult<object?>(new { Id = groupId, GroupType = groupType });
        }
        return Task.FromResult<object?>(null);
    }

    public Task<IReadOnlyList<object>> ListGroupsAsync(string groupType = "machine", CancellationToken cancellationToken = default)
    {
        if (string.Equals(groupType, "machine", StringComparison.OrdinalIgnoreCase))
        {
            var groups = _machineGroups.Values.GroupBy(g => g.Id).Select(g => g.First()).Cast<object>().ToList();
            return Task.FromResult<IReadOnlyList<object>>(groups);
        }

        string prefix = $"{groupType.ToLowerInvariant()}:";
        var groupKeys = _groupMemberships.Keys
            .Where(k => k.StartsWith(prefix))
            .Select(k => (object)new { Id = k[prefix.Length..], GroupType = groupType })
            .ToList();

        return Task.FromResult<IReadOnlyList<object>>(groupKeys);
    }

    public Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        if (_groupMemberships.TryGetValue(key, out var members))
        {
            return Task.FromResult<IReadOnlyList<string>>(members.ToList());
        }
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<bool> AddGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        var set = _groupMemberships.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (set)
        {
            set.Add(memberId);
        }
        return Task.FromResult(true);
    }

    public Task<bool> RemoveGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        if (_groupMemberships.TryGetValue(key, out var set))
        {
            lock (set)
            {
                return Task.FromResult(set.Remove(memberId));
            }
        }
        return Task.FromResult(false);
    }

    public Task<bool> IsGroupMemberAsync(string groupId, string memberId, string groupType = "machine", CancellationToken cancellationToken = default)
    {
        string key = $"{groupType}:{groupId}".ToLowerInvariant();
        if (_groupMemberships.TryGetValue(key, out var set))
        {
            lock (set)
            {
                return Task.FromResult(set.Contains(memberId));
            }
        }
        return Task.FromResult(false);
    }

    // --- TELEMETRY ---
    public Task<double?> GetLatestTelemetryValueAsync(string machineId, string metric, CancellationToken cancellationToken = default)
    {
        if (_telemetryStore.TryGetValue(machineId, out var metrics) &&
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
        if (_telemetryStore.TryGetValue(machineId, out var metrics) &&
            metrics.TryGetValue(metric, out var series))
        {
            lock (series)
            {
                var takeCount = Math.Min(series.Count, count);
                var slice = series.Skip(series.Count - takeCount).ToList();
                return Task.FromResult<IReadOnlyList<(double, DateTimeOffset)>>(slice);
            }
        }
        return Task.FromResult<IReadOnlyList<(double, DateTimeOffset)>>(Array.Empty<(double, DateTimeOffset)>());
    }

    public Task EmitTelemetryAsync(string machineId, string metric, double value, Dictionary<string, object?>? metadata = null, CancellationToken cancellationToken = default)
    {
        RecordTelemetry(machineId, metric, value);
        return Task.CompletedTask;
    }

    // --- TICKETS ---
    public Task<MaintenanceTicket?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        _tickets.TryGetValue(ticketId, out var ticket);
        return Task.FromResult(ticket);
    }

    public Task<IReadOnlyList<MaintenanceTicket>> ListTicketsAsync(string? status = null, string? machineId = null, string? priority = null, bool? isEscalated = null, CancellationToken cancellationToken = default)
    {
        var query = _tickets.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(machineId) && Guid.TryParse(machineId, out var mGuid))
            query = query.Where(t => t.MachineId == mGuid);

        if (!string.IsNullOrEmpty(priority))
            query = query.Where(t => string.Equals(t.Priority, priority, StringComparison.OrdinalIgnoreCase));

        if (isEscalated.HasValue)
            query = query.Where(t => t.IsEscalated == isEscalated.Value);

        return Task.FromResult<IReadOnlyList<MaintenanceTicket>>(query.OrderByDescending(t => t.CreatedAt).ToList());
    }

    public Task<MaintenanceTicket> CreateTicketAsync(MaintenanceTicket ticket, CancellationToken cancellationToken = default)
    {
        if (ticket.Id == Guid.Empty)
            ticket.Id = Guid.NewGuid();

        if (ticket.CreatedAt == default)
            ticket.CreatedAt = DateTimeOffset.UtcNow;

        _tickets[ticket.Id] = ticket;
        return Task.FromResult(ticket);
    }

    public Task<bool> UpdateTicketStatusAsync(Guid ticketId, string status, string? notes = null, CancellationToken cancellationToken = default)
    {
        if (_tickets.TryGetValue(ticketId, out var ticket))
        {
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
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> EscalateTicketAsync(Guid ticketId, string reason, string escalatedBy, string? target = null, CancellationToken cancellationToken = default)
    {
        if (_tickets.TryGetValue(ticketId, out var ticket))
        {
            ticket.IsEscalated = true;
            ticket.EscalationReason = reason;
            ticket.EscalatedBy = escalatedBy;
            ticket.EscalatedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(target))
            {
                ticket.AssignedTo = target;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
