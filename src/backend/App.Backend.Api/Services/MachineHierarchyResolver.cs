namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using App.Contracts.Enums;
using App.Shared.Entities;

/// <summary>
/// The group a machine belongs to at one level of the plant hierarchy. <see cref="Id"/> is null
/// for a legacy line that only exists as the free-text <see cref="Machine.GroupId"/>.
/// </summary>
public readonly record struct HierarchyLevel(Guid? Id, string? Name)
{
    public static readonly HierarchyLevel None = new(null, null);
    public bool IsAssigned => Id.HasValue || !string.IsNullOrWhiteSpace(Name);
    /// <summary>Stable grouping key: the group id, else the legacy name, else null.</summary>
    public string? Key => Id?.ToString() ?? (string.IsNullOrWhiteSpace(Name) ? null : Name);
}

/// <summary>Business unit &gt; project &gt; line &gt; sub-line placement of one machine.</summary>
public sealed record MachineHierarchy(
    HierarchyLevel BusinessUnit,
    HierarchyLevel Project,
    HierarchyLevel Line,
    HierarchyLevel SubLine);

/// <summary>
/// Resolves where a machine sits in the machine-group hierarchy from stored rows only. A level is
/// taken, in this order, from:
/// <list type="number">
///   <item>the machine's foreign key for that level (sub-line / line / project / business unit);</item>
///   <item>the legacy <see cref="Machine.GroupId"/> when it holds the id of an existing group;</item>
///   <item>groups that list the machine as a member (id list or membership rows), by the group's type;</item>
///   <item>typed ancestors (<see cref="MachineGroup.ParentId"/> chain) of an already resolved level;</item>
///   <item>for the line only: the legacy <see cref="Machine.GroupId"/> free text, used as the line name.</item>
/// </list>
/// Ancestors are matched by their <see cref="MachineGroupType"/> only; an untyped (Manual) parent
/// is never guessed into a level.
/// </summary>
public sealed class MachineHierarchyResolver
{
    private static readonly MachineGroupType[] MostSpecificFirst =
    {
        MachineGroupType.SubLine, MachineGroupType.Line, MachineGroupType.Project, MachineGroupType.BusinessUnit
    };

    private readonly Dictionary<Guid, MachineGroup> _groups;
    private readonly Dictionary<Guid, List<MachineGroup>> _groupsByMember = new();

    public MachineHierarchyResolver(
        IEnumerable<MachineGroup>? groups,
        IEnumerable<(Guid GroupId, Guid MachineId)>? memberships = null)
    {
        _groups = (groups ?? Enumerable.Empty<MachineGroup>())
            .GroupBy(g => g.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var group in _groups.Values.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var machineId in group.GetMachineIds())
            {
                AddMember(machineId, group);
            }
        }

        foreach (var (groupId, machineId) in memberships ?? Enumerable.Empty<(Guid, Guid)>())
        {
            if (_groups.TryGetValue(groupId, out var group)) AddMember(machineId, group);
        }
    }

    /// <summary>Name of a known group, null when the id is unknown (deleted or not visible).</summary>
    public string? GroupName(Guid? id) =>
        id.HasValue && _groups.TryGetValue(id.Value, out var group) ? group.Name : null;

    public MachineHierarchy Resolve(Machine machine)
    {
        var assigned = new Dictionary<MachineGroupType, MachineGroup>();

        // 1. The machine's own foreign keys define the level, whatever the group's type says.
        AssignById(assigned, MachineGroupType.SubLine, machine.SubLineGroupId);
        AssignById(assigned, MachineGroupType.Line, machine.LineGroupId);
        AssignById(assigned, MachineGroupType.Project, machine.ProjectGroupId);
        AssignById(assigned, MachineGroupType.BusinessUnit, machine.BusinessUnitGroupId);

        // 2. Legacy line field holding a group id (documented as the "line envelope identifier").
        var legacy = machine.GroupId?.Trim();
        var legacyIsGuid = Guid.TryParse(legacy, out var legacyId);
        if (legacyIsGuid && _groups.TryGetValue(legacyId, out var legacyGroup))
        {
            assigned.TryAdd(LevelOf(legacyGroup.GroupType) ?? MachineGroupType.Line, legacyGroup);
        }

        // 3. Membership lists, by the listing group's own type.
        if (_groupsByMember.TryGetValue(machine.Id, out var memberOf))
        {
            foreach (var group in memberOf)
            {
                if (LevelOf(group.GroupType) is { } level) assigned.TryAdd(level, group);
            }
        }

        // 4. Typed ancestors of what is known so far, most specific level first.
        foreach (var level in MostSpecificFirst)
        {
            if (!assigned.TryGetValue(level, out var start)) continue;
            var visited = new HashSet<Guid> { start.Id };
            var parentId = start.ParentId;
            while (parentId.HasValue && visited.Add(parentId.Value) && _groups.TryGetValue(parentId.Value, out var parent))
            {
                if (LevelOf(parent.GroupType) is { } parentLevel && IsAbove(parentLevel, level))
                {
                    assigned.TryAdd(parentLevel, parent);
                }
                parentId = parent.ParentId;
            }
        }

        var line = ToLevel(assigned, MachineGroupType.Line);
        if (!line.IsAssigned && !legacyIsGuid && !string.IsNullOrWhiteSpace(legacy))
        {
            // 5. Free-text legacy line name (no machine group row exists for it).
            line = new HierarchyLevel(null, legacy);
        }

        return new MachineHierarchy(
            ToLevel(assigned, MachineGroupType.BusinessUnit),
            ToLevel(assigned, MachineGroupType.Project),
            line,
            ToLevel(assigned, MachineGroupType.SubLine));
    }

    private void AddMember(Guid machineId, MachineGroup group)
    {
        if (!_groupsByMember.TryGetValue(machineId, out var list))
        {
            _groupsByMember[machineId] = list = new List<MachineGroup>();
        }
        if (!list.Contains(group)) list.Add(group);
    }

    private void AssignById(Dictionary<MachineGroupType, MachineGroup> assigned, MachineGroupType level, Guid? groupId)
    {
        if (groupId.HasValue && _groups.TryGetValue(groupId.Value, out var group)) assigned.TryAdd(level, group);
    }

    private static HierarchyLevel ToLevel(Dictionary<MachineGroupType, MachineGroup> assigned, MachineGroupType level) =>
        assigned.TryGetValue(level, out var group) ? new HierarchyLevel(group.Id, group.Name) : HierarchyLevel.None;

    private static MachineGroupType? LevelOf(MachineGroupType type) => type switch
    {
        MachineGroupType.BusinessUnit or MachineGroupType.Project or MachineGroupType.Line or MachineGroupType.SubLine => type,
        _ => null
    };

    /// <summary>Enum order is BusinessUnit(1) &lt; Project(2) &lt; Line(3) &lt; SubLine(4): smaller is higher up.</summary>
    private static bool IsAbove(MachineGroupType candidate, MachineGroupType reference) => (int)candidate < (int)reference;
}
