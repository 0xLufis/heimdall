namespace App.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using App.Contracts.Enums;
using App.Contracts.Personnel;
using App.Shared.Data;
using App.Shared.Entities;

public class DelegationRecipeService : IDelegationRecipeService
{
    private readonly AppDbContext _context;

    public DelegationRecipeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ResolvedPersonnelDto> ResolvePersonnelForMachineAsync(
        Guid machineId,
        DelegationDiscipline discipline = DelegationDiscipline.General,
        WorkerScheduleType scheduleType = WorkerScheduleType.Shift,
        string? technology = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch Machine with its complete hierarchy and context
        var machine = await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.SubMachines)
            .Include(m => m.ParentMachine)
            .Include(m => m.SubLineGroup)
            .Include(m => m.LineGroup)
            .Include(m => m.ProjectGroup)
            .Include(m => m.BusinessUnitGroup)
            .Include(m => m.DelegationBindings)
                .ThenInclude(b => b.DelegationRecipe)
                    .ThenInclude(r => r!.PrimaryPersonnelGroup)
                        .ThenInclude(g => g!.Members)
            .Include(m => m.DelegationBindings)
                .ThenInclude(b => b.DelegationRecipe)
                    .ThenInclude(r => r!.MaintenanceGroups)
                        .ThenInclude(mg => mg.MaintenanceGroup)
                            .ThenInclude(g => g!.Members)
            .Include(m => m.DelegationBindings)
                .ThenInclude(b => b.DelegationRecipe)
                    .ThenInclude(r => r!.EscalationGroup)
            .FirstOrDefaultAsync(m => m.Id == machineId, cancellationToken);

        if (machine == null)
        {
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = Guid.Empty,
                RecipeName = "UnknownMachine",
                Discipline = discipline,
                ResolutionReason = $"Machine '{machineId}' was not found.",
                DedicationSource = "fallback"
            };
        }

        // Determine effective technology
        string? effectiveTech = technology;
        if (string.IsNullOrWhiteSpace(effectiveTech))
        {
            if (machine.Technologies != null && machine.Technologies.Count > 0)
                effectiveTech = machine.Technologies[0];
            else if (!string.IsNullOrWhiteSpace(machine.MachineType))
                effectiveTech = machine.MachineType;
            else if (discipline != DelegationDiscipline.General)
                effectiveTech = discipline.ToString();
        }

        // Load active technician rules
        var techRules = await _context.TechnicianRules
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Load all active delegation recipes (with maintenance & machine group junctions)
        var allRecipes = await _context.DelegationRecipes
            .Include(r => r.PrimaryPersonnelGroup).ThenInclude(g => g!.Members)
            .Include(r => r.EscalationGroup)
            .Include(r => r.MaintenanceGroups).ThenInclude(mg => mg.MaintenanceGroup).ThenInclude(g => g!.Members)
            .Include(r => r.MachineGroups)
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken);

        // Group IDs applicable to this machine
        var machineGroupIds = new HashSet<Guid>();
        if (machine.SubLineGroupId.HasValue) machineGroupIds.Add(machine.SubLineGroupId.Value);
        if (machine.LineGroupId.HasValue) machineGroupIds.Add(machine.LineGroupId.Value);
        if (machine.ProjectGroupId.HasValue) machineGroupIds.Add(machine.ProjectGroupId.Value);
        if (machine.BusinessUnitGroupId.HasValue) machineGroupIds.Add(machine.BusinessUnitGroupId.Value);

        // Include any explicit manual machine group memberships
        var manualMemberships = await _context.MachineGroupMemberships
            .Where(m => m.MachineId == machine.Id)
            .Select(m => m.GroupId)
            .ToListAsync(cancellationToken);
        foreach (var mgId in manualMemberships) machineGroupIds.Add(mgId);

        // ─────────────────────────────────────────────────────────────────────
        // PRIORITY 1: Technology Dedication Primarily & Technological Group Autosuperseding
        // "Technological groups are automatically dedicated to machines if the machine possesses
        // their tech but this can be superseded when a member of the group is dedicated"
        // ─────────────────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(effectiveTech))
        {
            // Find any technological personnel groups matching effectiveTech
            var techPersonnelGroups = await _context.PersonnelGroups
                .Include(g => g.Members)
                .Where(g => g.Discipline.ToString().ToLower() == effectiveTech.ToLower() ||
                            g.Name.ToLower().Contains(effectiveTech.ToLower()) ||
                            effectiveTech.ToLower().Contains(g.Discipline.ToString().ToLower()))
                .ToListAsync(cancellationToken);

            // Check if a dedicated technician on the machine for this technology (or general) exists:
            var directMachineTechRule = techRules.FirstOrDefault(r =>
                string.Equals(r.ScopeType, "machine", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.TargetId, machine.Id.ToString(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.CategoryFilter, effectiveTech, StringComparison.OrdinalIgnoreCase))
                ?? techRules.FirstOrDefault(r =>
                string.Equals(r.ScopeType, "machine", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.TargetId, machine.Id.ToString(), StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(r.CategoryFilter) || string.Equals(r.CategoryFilter, "General", StringComparison.OrdinalIgnoreCase)));

            string? machineDedicatedUserId = directMachineTechRule?.TechnicianId ?? machine.PreferredTechnicianId;
            string? machineDedicatedUserName = directMachineTechRule?.TechnicianName ?? machine.PreferredTechnicianName;

            if (!string.IsNullOrWhiteSpace(machineDedicatedUserId))
            {
                var overriddenGroup = techPersonnelGroups.FirstOrDefault(g =>
                    g.Members.Any(m => m.UserId == machineDedicatedUserId));

                if (overriddenGroup != null)
                {
                    return new ResolvedPersonnelDto
                    {
                        DelegationRecipeId = Guid.Empty,
                        RecipeName = $"TechMemberDedication_{effectiveTech}",
                        Discipline = discipline,
                        Technology = effectiveTech,
                        EffectiveAssigneeId = machineDedicatedUserId,
                        EffectiveAssigneeName = machineDedicatedUserName ?? machineDedicatedUserId,
                        EffectiveGroupId = overriddenGroup.Id,
                        EffectiveGroupName = overriddenGroup.Name,
                        IsOnCallFallback = false,
                        IsEscalated = false,
                        DedicationSource = "technology_member_override",
                        ResolutionReason = $"Dedicated specialist '{machineDedicatedUserName ?? machineDedicatedUserId}' supersedes technological group '{overriddenGroup.Name}' for '{effectiveTech}' on this machine."
                    };
                }

                // If explicitly dedicated to this machine for this specific technology, resolve directly
                if (directMachineTechRule != null && string.Equals(directMachineTechRule.CategoryFilter, effectiveTech, StringComparison.OrdinalIgnoreCase))
                {
                    return new ResolvedPersonnelDto
                    {
                        DelegationRecipeId = Guid.Empty,
                        RecipeName = $"TechDedication_{effectiveTech}_{directMachineTechRule.TechnicianName}",
                        Discipline = discipline,
                        Technology = effectiveTech,
                        EffectiveAssigneeId = directMachineTechRule.TechnicianId,
                        EffectiveAssigneeName = directMachineTechRule.TechnicianName,
                        EffectiveGroupId = null,
                        EffectiveGroupName = null,
                        IsOnCallFallback = false,
                        IsEscalated = false,
                        DedicationSource = "technology_machine_dedication",
                        ResolutionReason = $"Dedicated directly to this machine for technology '{effectiveTech}'."
                    };
                }
            }

            // 1a. Check technology-specific technician routing rule
            var techRule = techRules.FirstOrDefault(r =>
                string.Equals(r.ScopeType, "technology", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.TargetId, effectiveTech, StringComparison.OrdinalIgnoreCase));

            if (techRule != null)
            {
                return new ResolvedPersonnelDto
                {
                    DelegationRecipeId = Guid.Empty,
                    RecipeName = $"TechDedication_{effectiveTech}",
                    Discipline = discipline,
                    Technology = effectiveTech,
                    EffectiveAssigneeId = techRule.TechnicianId,
                    EffectiveAssigneeName = techRule.TechnicianName,
                    EffectiveGroupId = null,
                    EffectiveGroupName = null,
                    IsOnCallFallback = false,
                    IsEscalated = false,
                    DedicationSource = "technology_dedication",
                    ResolutionReason = $"Resolved primarily by dedicated technology technician rule for '{effectiveTech}'."
                };
            }

            // 1b. Check recipes with technology dedication matching effectiveTech or discipline
            var matchingTechRecipes = allRecipes
                .Where(r => (string.Equals(r.Technology, effectiveTech, StringComparison.OrdinalIgnoreCase) ||
                            (r.Discipline == discipline && discipline != DelegationDiscipline.General)) &&
                            r.IsTechnologyPrimary)
                .ToList();

            // Order by specificity: directly bound to machine > targeting machine's groups > global technology
            var directBindingRecipe = machine.DelegationBindings
                .Where(b => b.IsActive && b.DelegationRecipe != null &&
                           (string.Equals(b.DelegationRecipe.Technology, effectiveTech, StringComparison.OrdinalIgnoreCase) ||
                            b.DelegationRecipe.Discipline == discipline ||
                            b.Discipline == discipline))
                .Select(b => b.DelegationRecipe!)
                .FirstOrDefault();

            var techRecipe = directBindingRecipe
                ?? matchingTechRecipes.FirstOrDefault(r => r.MachineGroups.Any(mg => machineGroupIds.Contains(mg.MachineGroupId)) ||
                                                          r.MachineGroupIds.Any(id => machineGroupIds.Contains(id)))
                ?? matchingTechRecipes.FirstOrDefault(r => r.MachineGroups.Count == 0 && r.MachineGroupIds.Count == 0);

            if (techRecipe != null)
            {
                var resolvedTech = await EvaluateRecipePersonnelAsync(techRecipe, scheduleType, cancellationToken);
                resolvedTech.Technology = effectiveTech;
                resolvedTech.DedicationSource = "technology_dedication";
                resolvedTech.ResolutionReason = !string.IsNullOrWhiteSpace(resolvedTech.ResolutionReason)
                    ? $"Resolved primarily by technology dedication recipe '{techRecipe.Name}' for '{effectiveTech}': {resolvedTech.ResolutionReason}"
                    : $"Resolved primarily by technology dedication recipe '{techRecipe.Name}' for '{effectiveTech}'.";
                return resolvedTech;
            }

            // 1c. Automatic dedication of technological group matching machine's technology
            if (techPersonnelGroups.Count > 0)
            {
                var autoTechGroup = techPersonnelGroups.First();
                var firstMember = autoTechGroup.Members.FirstOrDefault();
                return new ResolvedPersonnelDto
                {
                    DelegationRecipeId = Guid.Empty,
                    RecipeName = $"TechnologicalGroup_{autoTechGroup.Name}",
                    Discipline = discipline,
                    Technology = effectiveTech,
                    EffectiveAssigneeId = firstMember?.UserId,
                    EffectiveAssigneeName = firstMember?.UserName ?? autoTechGroup.Name,
                    EffectiveGroupId = autoTechGroup.Id,
                    EffectiveGroupName = autoTechGroup.Name,
                    IsOnCallFallback = false,
                    IsEscalated = false,
                    DedicationSource = "technological_group_auto",
                    ResolutionReason = $"Automatically dedicated via technological group '{autoTechGroup.Name}' matching machine technology '{effectiveTech}'."
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIORITY 2: Complete Machine Dedication
        // "still allow for complete machine dedications"
        // ─────────────────────────────────────────────────────────────────────
        // 2a. Direct machine preferred technician
        if (!string.IsNullOrWhiteSpace(machine.PreferredTechnicianId))
        {
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = Guid.Empty,
                RecipeName = "DefaultMachineTechnician",
                Discipline = discipline,
                Technology = effectiveTech,
                EffectiveAssigneeId = machine.PreferredTechnicianId,
                EffectiveAssigneeName = machine.PreferredTechnicianName ?? machine.PreferredTechnicianId,
                EffectiveGroupId = null,
                EffectiveGroupName = null,
                IsOnCallFallback = false,
                IsEscalated = false,
                DedicationSource = "complete_machine_dedication",
                ResolutionReason = "Resolved to complete machine dedicated preferred technician."
            };
        }

        // 2b. Machine-scoped technician rule
        var machineRule = techRules.FirstOrDefault(r =>
            string.Equals(r.ScopeType, "machine", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.TargetId, machine.Id.ToString(), StringComparison.OrdinalIgnoreCase));

        if (machineRule != null)
        {
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = Guid.Empty,
                RecipeName = $"MachineDedication_{machine.Name}",
                Discipline = discipline,
                Technology = effectiveTech,
                EffectiveAssigneeId = machineRule.TechnicianId,
                EffectiveAssigneeName = machineRule.TechnicianName,
                EffectiveGroupId = null,
                EffectiveGroupName = null,
                IsOnCallFallback = false,
                IsEscalated = false,
                DedicationSource = "complete_machine_dedication",
                ResolutionReason = "Resolved to complete machine dedication rule."
            };
        }

        // 2c. Direct machine delegation binding (general or marked as complete dedication)
        var completeMachineRecipe = machine.DelegationBindings
            .Where(b => b.IsActive && b.DelegationRecipe != null &&
                       (b.DelegationRecipe.IsCompleteMachineDedication || b.Discipline == DelegationDiscipline.General || b.DelegationRecipe.Discipline == DelegationDiscipline.General))
            .Select(b => b.DelegationRecipe!)
            .FirstOrDefault();

        if (completeMachineRecipe != null)
        {
            var resolvedComplete = await EvaluateRecipePersonnelAsync(completeMachineRecipe, scheduleType, cancellationToken);
            resolvedComplete.Technology = effectiveTech;
            resolvedComplete.DedicationSource = "complete_machine_dedication";
            resolvedComplete.ResolutionReason = !string.IsNullOrWhiteSpace(resolvedComplete.ResolutionReason)
                ? $"Resolved to complete machine delegation recipe '{completeMachineRecipe.Name}': {resolvedComplete.ResolutionReason}"
                : $"Resolved to complete machine delegation recipe '{completeMachineRecipe.Name}'.";
            return resolvedComplete;
        }

        // 2d. Sub-machine inheritance: if this is a sub-machine, check parent cell / roundtable complete dedication
        if (machine.ParentMachineId.HasValue && machine.ParentMachine != null)
        {
            if (!string.IsNullOrWhiteSpace(machine.ParentMachine.PreferredTechnicianId))
            {
                return new ResolvedPersonnelDto
                {
                    DelegationRecipeId = Guid.Empty,
                    RecipeName = $"ParentCellTechnician_{machine.ParentMachine.Name}",
                    Discipline = discipline,
                    Technology = effectiveTech,
                    EffectiveAssigneeId = machine.ParentMachine.PreferredTechnicianId,
                    EffectiveAssigneeName = machine.ParentMachine.PreferredTechnicianName ?? machine.ParentMachine.PreferredTechnicianId,
                    EffectiveGroupId = null,
                    EffectiveGroupName = null,
                    IsOnCallFallback = false,
                    IsEscalated = false,
                    DedicationSource = "complete_machine_dedication",
                    ResolutionReason = $"Resolved to parent cell/roundtable complete machine dedication ({machine.ParentMachine.Name})."
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIORITY 3: Machine Group Hierarchy Dedication
        // Hierarchy: Sub-Line > Line > Project > Business Unit
        // ─────────────────────────────────────────────────────────────────────
        var hierarchyGroups = new List<MachineGroup?>();
        if (machine.SubLineGroup != null) hierarchyGroups.Add(machine.SubLineGroup);
        if (machine.LineGroup != null) hierarchyGroups.Add(machine.LineGroup);
        if (machine.ProjectGroup != null) hierarchyGroups.Add(machine.ProjectGroup);
        if (machine.BusinessUnitGroup != null) hierarchyGroups.Add(machine.BusinessUnitGroup);

        foreach (var group in hierarchyGroups.Where(g => g != null))
        {
            // Lead engineer on the group
            if (!string.IsNullOrWhiteSpace(group!.LeadEngineerId))
            {
                return new ResolvedPersonnelDto
                {
                    DelegationRecipeId = Guid.Empty,
                    RecipeName = $"LeadEngineer_{group.Name}",
                    Discipline = discipline,
                    Technology = effectiveTech,
                    EffectiveAssigneeId = group.LeadEngineerId,
                    EffectiveAssigneeName = group.LeadEngineerName ?? group.LeadEngineerId,
                    EffectiveGroupId = group.Id,
                    EffectiveGroupName = group.Name,
                    IsOnCallFallback = false,
                    IsEscalated = false,
                    DedicationSource = "machine_group",
                    ResolutionReason = $"Resolved to {group.GroupType} lead engineer for '{group.Name}'."
                };
            }

            // Recipe bound to this machine group
            var groupRecipe = allRecipes.FirstOrDefault(r =>
                r.MachineGroups.Any(mg => mg.MachineGroupId == group.Id) ||
                r.MachineGroupIds.Contains(group.Id));

            if (groupRecipe != null)
            {
                var resolvedGroup = await EvaluateRecipePersonnelAsync(groupRecipe, scheduleType, cancellationToken);
                resolvedGroup.Technology = effectiveTech;
                resolvedGroup.DedicationSource = "machine_group";
                resolvedGroup.ResolutionReason = !string.IsNullOrWhiteSpace(resolvedGroup.ResolutionReason)
                    ? $"Resolved to {group.GroupType} group recipe '{groupRecipe.Name}' for '{group.Name}': {resolvedGroup.ResolutionReason}"
                    : $"Resolved to {group.GroupType} group recipe '{groupRecipe.Name}' for '{group.Name}'.";
                return resolvedGroup;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIORITY 4: Any bound recipe on machine matching general discipline
        // ─────────────────────────────────────────────────────────────────────
        var generalBinding = machine.DelegationBindings.FirstOrDefault(b => b.IsActive && b.DelegationRecipe != null);
        if (generalBinding?.DelegationRecipe != null)
        {
            var resolvedGeneral = await EvaluateRecipePersonnelAsync(generalBinding.DelegationRecipe, scheduleType, cancellationToken);
            resolvedGeneral.Technology = effectiveTech;
            resolvedGeneral.DedicationSource = "fallback";
            return resolvedGeneral;
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIORITY 5: Fallback with SPOC / Owner information
        // ─────────────────────────────────────────────────────────────────────
        string fallbackReason = machine.PreferredTechnicianId != null
            ? "Resolved to machine dedicated preferred technician."
            : $"No delegation rule matched; Machine SPOC: {machine.Spoc ?? "Unassigned"}, Owner: {machine.Owner ?? "Unassigned"}.";

        return new ResolvedPersonnelDto
        {
            DelegationRecipeId = Guid.Empty,
            RecipeName = "DefaultFallback",
            Discipline = discipline,
            Technology = effectiveTech,
            EffectiveAssigneeId = machine.PreferredTechnicianId,
            EffectiveAssigneeName = machine.PreferredTechnicianName,
            EffectiveGroupId = null,
            EffectiveGroupName = null,
            IsOnCallFallback = false,
            IsEscalated = false,
            DedicationSource = "fallback",
            ResolutionReason = fallbackReason
        };
    }

    private async Task<ResolvedPersonnelDto> EvaluateRecipePersonnelAsync(
        DelegationRecipe recipe,
        WorkerScheduleType scheduleType,
        CancellationToken cancellationToken)
    {
        // Check OnCall schedule fallback
        if (scheduleType == WorkerScheduleType.OnCall && !string.IsNullOrWhiteSpace(recipe.OnCallFallbackUserId))
        {
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = recipe.Id,
                RecipeName = recipe.Name,
                Discipline = recipe.Discipline,
                EffectiveAssigneeId = recipe.OnCallFallbackUserId,
                EffectiveAssigneeName = recipe.OnCallFallbackUserName ?? recipe.OnCallFallbackUserId,
                EffectiveGroupId = recipe.PrimaryPersonnelGroupId,
                EffectiveGroupName = recipe.PrimaryPersonnelGroup?.Name,
                IsOnCallFallback = true,
                IsEscalated = false,
                ResolutionReason = "Resolved to recipe On-Call specialist fallback for off-hours event."
            };
        }

        // Collect all candidate personnel groups (1 or more maintenance groups + primary)
        var allGroups = new List<PersonnelGroup>();
        if (recipe.PrimaryPersonnelGroup != null) allGroups.Add(recipe.PrimaryPersonnelGroup);
        if (recipe.MaintenanceGroups != null)
        {
            foreach (var mg in recipe.MaintenanceGroups)
            {
                if (mg.MaintenanceGroup != null && !allGroups.Any(g => g.Id == mg.MaintenanceGroup.Id))
                    allGroups.Add(mg.MaintenanceGroup);
            }
        }

        // Collect all candidate member user IDs
        var allMembers = allGroups.SelectMany(g => g.Members).DistinctBy(m => m.UserId).ToList();
        var allMemberUserIds = allMembers.Select(m => m.UserId).ToList();

        // Check required skills against member user qualifications
        List<string> requiredSkills = new();
        if (!string.IsNullOrWhiteSpace(recipe.RequiredSkillCodesJson))
        {
            try
            {
                requiredSkills = JsonSerializer.Deserialize<List<string>>(recipe.RequiredSkillCodesJson) ?? new();
            }
            catch { }
        }

        if (allMemberUserIds.Count > 0 && requiredSkills.Count > 0)
        {
            var activeSkills = await _context.PersonnelSkills
                .Include(ps => ps.Skill)
                .Where(ps => allMemberUserIds.Contains(ps.UserId) &&
                             (ps.ExpiresAt == null || ps.ExpiresAt > DateTimeOffset.UtcNow) &&
                             requiredSkills.Contains(ps.Skill!.Code))
                .ToListAsync(cancellationToken);

            var qualifiedMemberId = activeSkills
                .GroupBy(ps => ps.UserId)
                .Where(g => requiredSkills.All(req => g.Any(s => s.Skill?.Code == req)))
                .Select(g => g.Key)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(qualifiedMemberId))
            {
                var qualifiedMember = allMembers.FirstOrDefault(m => m.UserId == qualifiedMemberId);
                var parentGroup = allGroups.FirstOrDefault(g => g.Members.Any(m => m.UserId == qualifiedMemberId));

                return new ResolvedPersonnelDto
                {
                    DelegationRecipeId = recipe.Id,
                    RecipeName = recipe.Name,
                    Discipline = recipe.Discipline,
                    EffectiveAssigneeId = qualifiedMemberId,
                    EffectiveAssigneeName = qualifiedMember?.UserName ?? qualifiedMemberId,
                    EffectiveGroupId = parentGroup?.Id ?? recipe.PrimaryPersonnelGroupId,
                    EffectiveGroupName = parentGroup?.Name ?? recipe.PrimaryPersonnelGroup?.Name,
                    IsOnCallFallback = false,
                    IsEscalated = false,
                    ResolutionReason = $"Resolved to certified group member matching required skills ({string.Join(", ", requiredSkills)})."
                };
            }
        }

        // Primary designated direct user on recipe
        if (!string.IsNullOrWhiteSpace(recipe.PrimaryUserId))
        {
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = recipe.Id,
                RecipeName = recipe.Name,
                Discipline = recipe.Discipline,
                EffectiveAssigneeId = recipe.PrimaryUserId,
                EffectiveAssigneeName = recipe.PrimaryUserName ?? recipe.PrimaryUserId,
                EffectiveGroupId = recipe.PrimaryPersonnelGroupId,
                EffectiveGroupName = recipe.PrimaryPersonnelGroup?.Name,
                IsOnCallFallback = false,
                IsEscalated = false,
                ResolutionReason = "Resolved to primary designated technician within group."
            };
        }

        // Primary personnel group or first maintenance group
        if (allGroups.Count > 0)
        {
            var primaryGroup = allGroups[0];
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = recipe.Id,
                RecipeName = recipe.Name,
                Discipline = recipe.Discipline,
                EffectiveAssigneeId = null,
                EffectiveAssigneeName = null,
                EffectiveGroupId = primaryGroup.Id,
                EffectiveGroupName = primaryGroup.Name,
                IsOnCallFallback = false,
                IsEscalated = false,
                ResolutionReason = $"Resolved to primary personnel group '{primaryGroup.Name}'."
            };
        }

        // Direct user fallback on recipe
        return new ResolvedPersonnelDto
        {
            DelegationRecipeId = recipe.Id,
            RecipeName = recipe.Name,
            Discipline = recipe.Discipline,
            EffectiveAssigneeId = recipe.PrimaryUserId,
            EffectiveAssigneeName = recipe.PrimaryUserName,
            EffectiveGroupId = null,
            EffectiveGroupName = null,
            IsOnCallFallback = false,
            IsEscalated = false,
            ResolutionReason = "Resolved to recipe primary designated technician."
        };
    }

    public async Task<List<DelegationRecipeDto>> GetRecipesForMachineAsync(Guid machineId, CancellationToken cancellationToken = default)
    {
        var bindings = await _context.MachineDelegationBindings
            .AsNoTracking()
            .Include(b => b.DelegationRecipe)
                .ThenInclude(r => r!.PrimaryPersonnelGroup)
            .Include(b => b.DelegationRecipe)
                .ThenInclude(r => r!.EscalationGroup)
            .Include(b => b.DelegationRecipe)
                .ThenInclude(r => r!.MaintenanceGroups)
                    .ThenInclude(mg => mg.MaintenanceGroup)
            .Include(b => b.DelegationRecipe)
                .ThenInclude(r => r!.MachineGroups)
                    .ThenInclude(mg => mg.MachineGroup)
            .Where(b => b.MachineId == machineId && b.IsActive)
            .ToListAsync(cancellationToken);

        return bindings
            .Where(b => b.DelegationRecipe != null)
            .Select(b => MapToDto(b.DelegationRecipe!))
            .ToList();
    }

    public async Task<List<DelegationRecipeDto>> GetRecipesForMachineAsync(string machineIdentifier, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(machineIdentifier, out var g))
            return await GetRecipesForMachineAsync(g, cancellationToken);

        var mach = await _context.Machines.FirstOrDefaultAsync(m => m.CustomIdentifier == machineIdentifier || m.Name == machineIdentifier, cancellationToken);
        if (mach != null)
            return await GetRecipesForMachineAsync(mach.Id, cancellationToken);

        return new List<DelegationRecipeDto>();
    }

    public async Task<List<DelegationRecipeDto>> GetRecipesApplicableToMachineAsync(Guid machineId, CancellationToken cancellationToken = default)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == machineId, cancellationToken);
        if (machine == null) return new();

        var groupIds = new HashSet<Guid>();
        if (machine.SubLineGroupId.HasValue) groupIds.Add(machine.SubLineGroupId.Value);
        if (machine.LineGroupId.HasValue) groupIds.Add(machine.LineGroupId.Value);
        if (machine.ProjectGroupId.HasValue) groupIds.Add(machine.ProjectGroupId.Value);
        if (machine.BusinessUnitGroupId.HasValue) groupIds.Add(machine.BusinessUnitGroupId.Value);

        var directRecipes = await GetRecipesForMachineAsync(machineId, cancellationToken);

        var groupRecipes = await _context.DelegationRecipes
            .AsNoTracking()
            .Include(r => r.PrimaryPersonnelGroup)
            .Include(r => r.EscalationGroup)
            .Include(r => r.MaintenanceGroups).ThenInclude(mg => mg.MaintenanceGroup)
            .Include(r => r.MachineGroups).ThenInclude(mg => mg.MachineGroup)
            .Where(r => r.IsActive && r.MachineGroups.Any(mg => groupIds.Contains(mg.MachineGroupId)))
            .ToListAsync(cancellationToken);

        var dtos = directRecipes;
        foreach (var gr in groupRecipes)
        {
            if (!dtos.Any(d => d.Id == gr.Id))
                dtos.Add(MapToDto(gr));
        }

        return dtos;
    }

    public async Task<DelegationRecipeDto?> GetRecipeByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await _context.DelegationRecipes
            .AsNoTracking()
            .Include(r => r.PrimaryPersonnelGroup)
            .Include(r => r.EscalationGroup)
            .Include(r => r.MaintenanceGroups).ThenInclude(mg => mg.MaintenanceGroup)
            .Include(r => r.MachineGroups).ThenInclude(mg => mg.MachineGroup)
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        return recipe == null ? null : MapToDto(recipe);
    }

    public async Task<List<DelegationRecipeDto>> GetAllRecipesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _context.DelegationRecipes
            .AsNoTracking()
            .Include(r => r.PrimaryPersonnelGroup)
            .Include(r => r.EscalationGroup)
            .Include(r => r.MaintenanceGroups).ThenInclude(mg => mg.MaintenanceGroup)
            .Include(r => r.MachineGroups).ThenInclude(mg => mg.MachineGroup)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    public async Task<DelegationRecipeDto> CreateOrUpdateRecipeAsync(DelegationRecipeDto dto, CancellationToken cancellationToken = default)
    {
        DelegationRecipe? entity = null;
        if (dto.Id != Guid.Empty)
        {
            entity = await _context.DelegationRecipes
                .Include(r => r.MaintenanceGroups)
                .Include(r => r.MachineGroups)
                .FirstOrDefaultAsync(r => r.Id == dto.Id, cancellationToken);
        }

        if (entity == null)
        {
            entity = new DelegationRecipe
            {
                Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.DelegationRecipes.Add(entity);
        }

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Discipline = dto.Discipline;
        entity.TriggerEventCategory = dto.TriggerEventCategory;
        entity.AllowedSchedules = dto.AllowedSchedules;
        entity.RequiredSkillIdsJson = JsonSerializer.Serialize(dto.RequiredSkillIds);
        entity.RequiredSkillCodesJson = JsonSerializer.Serialize(dto.RequiredSkillCodes);
        entity.PrimaryPersonnelGroupId = dto.PrimaryPersonnelGroupId ?? (dto.MaintenanceGroupIds.Count > 0 ? dto.MaintenanceGroupIds[0] : null);
        entity.PrimaryUserId = dto.PrimaryUserId;
        entity.PrimaryUserName = dto.PrimaryUserName;
        entity.OnCallFallbackUserId = dto.OnCallFallbackUserId;
        entity.OnCallFallbackUserName = dto.OnCallFallbackUserName;
        entity.EscalationGroupId = dto.EscalationGroupId;
        entity.EscalationTimeoutMinutes = dto.EscalationTimeoutMinutes;
        entity.IsActive = dto.IsActive;
        entity.Technology = dto.Technology;
        entity.IsTechnologyPrimary = dto.IsTechnologyPrimary;
        entity.IsCompleteMachineDedication = dto.IsCompleteMachineDedication;
        entity.MaintenanceGroupIdsJson = JsonSerializer.Serialize(dto.MaintenanceGroupIds);
        entity.MachineGroupIdsJson = JsonSerializer.Serialize(dto.MachineGroupIds);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Synchronize junction records for maintenance groups
        if (dto.MaintenanceGroupIds != null)
        {
            var existingMgIds = entity.MaintenanceGroups.Select(mg => mg.MaintenanceGroupId).ToList();
            var toAdd = dto.MaintenanceGroupIds.Where(id => !existingMgIds.Contains(id)).ToList();
            var toRemove = entity.MaintenanceGroups.Where(mg => !dto.MaintenanceGroupIds.Contains(mg.MaintenanceGroupId)).ToList();

            foreach (var r in toRemove) _context.DelegationMaintenanceGroups.Remove(r);
            foreach (var id in toAdd)
            {
                entity.MaintenanceGroups.Add(new DelegationMaintenanceGroup
                {
                    Id = Guid.NewGuid(),
                    DelegationRecipeId = entity.Id,
                    MaintenanceGroupId = id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Synchronize junction records for machine groups
        if (dto.MachineGroupIds != null)
        {
            var existingMgIds = entity.MachineGroups.Select(mg => mg.MachineGroupId).ToList();
            var toAdd = dto.MachineGroupIds.Where(id => !existingMgIds.Contains(id)).ToList();
            var toRemove = entity.MachineGroups.Where(mg => !dto.MachineGroupIds.Contains(mg.MachineGroupId)).ToList();

            foreach (var r in toRemove) _context.DelegationMachineGroups.Remove(r);
            foreach (var id in toAdd)
            {
                entity.MachineGroups.Add(new DelegationMachineGroup
                {
                    Id = Guid.NewGuid(),
                    DelegationRecipeId = entity.Id,
                    MachineGroupId = id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        dto.Id = entity.Id;
        return dto;
    }

    public async Task<MachineDelegationBindingDto> BindRecipeToMachineAsync(
        Guid machineId,
        Guid recipeId,
        DelegationDiscipline discipline,
        CancellationToken cancellationToken = default)
    {
        if (!await _context.Machines.AnyAsync(m => m.Id == machineId, cancellationToken))
            throw new KeyNotFoundException($"Machine '{machineId}' was not found.");

        var recipe = await _context.DelegationRecipes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Delegation recipe '{recipeId}' was not found.");

        var existing = await _context.MachineDelegationBindings
            .FirstOrDefaultAsync(b => b.MachineId == machineId && b.DelegationRecipeId == recipeId && b.Discipline == discipline, cancellationToken);

        if (existing != null)
        {
            existing.IsActive = true;
            await _context.SaveChangesAsync(cancellationToken);
            return new MachineDelegationBindingDto
            {
                Id = existing.Id,
                MachineId = existing.MachineId,
                DelegationRecipeId = existing.DelegationRecipeId,
                DelegationRecipeName = recipe.Name,
                Discipline = existing.Discipline,
                IsActive = existing.IsActive
            };
        }

        var binding = new MachineDelegationBinding
        {
            Id = Guid.NewGuid(),
            MachineId = machineId,
            DelegationRecipeId = recipeId,
            Discipline = discipline,
            IsActive = true,
            BoundAt = DateTimeOffset.UtcNow
        };

        _context.MachineDelegationBindings.Add(binding);
        await _context.SaveChangesAsync(cancellationToken);

        return new MachineDelegationBindingDto
        {
            Id = binding.Id,
            MachineId = binding.MachineId,
            DelegationRecipeId = binding.DelegationRecipeId,
            DelegationRecipeName = recipe.Name,
            Discipline = binding.Discipline,
            IsActive = binding.IsActive
        };
    }

    public async Task<bool> DeleteRecipeAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await _context.DelegationRecipes.FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);
        if (recipe == null) return false;

        // Remove dependents explicitly instead of relying on provider-specific cascade behaviour.
        _context.MachineDelegationBindings.RemoveRange(
            await _context.MachineDelegationBindings.Where(b => b.DelegationRecipeId == recipeId).ToListAsync(cancellationToken));
        _context.DelegationMaintenanceGroups.RemoveRange(
            await _context.DelegationMaintenanceGroups.Where(j => j.DelegationRecipeId == recipeId).ToListAsync(cancellationToken));
        _context.DelegationMachineGroups.RemoveRange(
            await _context.DelegationMachineGroups.Where(j => j.DelegationRecipeId == recipeId).ToListAsync(cancellationToken));
        _context.DelegationRecipes.Remove(recipe);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PersonnelGroupDeleteResult> DeletePersonnelGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _context.PersonnelGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group == null) return PersonnelGroupDeleteResult.NotFound;

        var idText = groupId.ToString();
        var inUse =
            await _context.DelegationRecipes.AnyAsync(r =>
                r.PrimaryPersonnelGroupId == groupId ||
                r.EscalationGroupId == groupId ||
                r.MaintenanceGroupIdsJson.Contains(idText), cancellationToken) ||
            await _context.DelegationMaintenanceGroups.AnyAsync(j => j.MaintenanceGroupId == groupId, cancellationToken);
        if (inUse) return PersonnelGroupDeleteResult.InUse;

        _context.PersonnelGroupMembers.RemoveRange(
            await _context.PersonnelGroupMembers.Where(m => m.PersonnelGroupId == groupId).ToListAsync(cancellationToken));
        _context.PersonnelGroups.Remove(group);
        await _context.SaveChangesAsync(cancellationToken);
        return PersonnelGroupDeleteResult.Deleted;
    }

    public async Task<bool> UnbindRecipeFromMachineAsync(Guid bindingId, CancellationToken cancellationToken = default)
    {
        var binding = await _context.MachineDelegationBindings.FindAsync(bindingId);
        if (binding == null) return false;

        _context.MachineDelegationBindings.Remove(binding);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static DelegationRecipeDto MapToDto(DelegationRecipe entity)
    {
        List<Guid> skillIds = new();
        List<string> skillCodes = new();
        List<Guid> maintenanceGroupIds = new();
        List<Guid> machineGroupIds = new();

        try
        {
            if (!string.IsNullOrWhiteSpace(entity.RequiredSkillIdsJson))
                skillIds = JsonSerializer.Deserialize<List<Guid>>(entity.RequiredSkillIdsJson) ?? new();
        }
        catch { }

        try
        {
            if (!string.IsNullOrWhiteSpace(entity.RequiredSkillCodesJson))
                skillCodes = JsonSerializer.Deserialize<List<string>>(entity.RequiredSkillCodesJson) ?? new();
        }
        catch { }

        try
        {
            if (!string.IsNullOrWhiteSpace(entity.MaintenanceGroupIdsJson))
                maintenanceGroupIds = JsonSerializer.Deserialize<List<Guid>>(entity.MaintenanceGroupIdsJson) ?? new();
        }
        catch { }

        try
        {
            if (!string.IsNullOrWhiteSpace(entity.MachineGroupIdsJson))
                machineGroupIds = JsonSerializer.Deserialize<List<Guid>>(entity.MachineGroupIdsJson) ?? new();
        }
        catch { }

        // Also incorporate junction references if loaded
        if (entity.MaintenanceGroups != null && entity.MaintenanceGroups.Count > 0)
        {
            foreach (var mg in entity.MaintenanceGroups)
            {
                if (!maintenanceGroupIds.Contains(mg.MaintenanceGroupId))
                    maintenanceGroupIds.Add(mg.MaintenanceGroupId);
            }
        }
        else if (entity.PrimaryPersonnelGroupId.HasValue && !maintenanceGroupIds.Contains(entity.PrimaryPersonnelGroupId.Value))
        {
            maintenanceGroupIds.Insert(0, entity.PrimaryPersonnelGroupId.Value);
        }

        if (entity.MachineGroups != null && entity.MachineGroups.Count > 0)
        {
            foreach (var mg in entity.MachineGroups)
            {
                if (!machineGroupIds.Contains(mg.MachineGroupId))
                    machineGroupIds.Add(mg.MachineGroupId);
            }
        }

        var maintenanceGroupNames = entity.MaintenanceGroups?
            .Where(mg => mg.MaintenanceGroup != null)
            .Select(mg => mg.MaintenanceGroup!.Name)
            .ToList() ?? new List<string>();

        if (maintenanceGroupNames.Count == 0 && entity.PrimaryPersonnelGroup != null)
            maintenanceGroupNames.Add(entity.PrimaryPersonnelGroup.Name);

        var machineGroupNames = entity.MachineGroups?
            .Where(mg => mg.MachineGroup != null)
            .Select(mg => mg.MachineGroup!.Name)
            .ToList() ?? new List<string>();

        return new DelegationRecipeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Discipline = entity.Discipline,
            TriggerEventCategory = entity.TriggerEventCategory,
            AllowedSchedules = entity.AllowedSchedules,
            RequiredSkillIds = skillIds,
            RequiredSkillCodes = skillCodes,
            PrimaryPersonnelGroupId = entity.PrimaryPersonnelGroupId,
            PrimaryPersonnelGroupName = entity.PrimaryPersonnelGroup?.Name,
            PrimaryUserId = entity.PrimaryUserId,
            PrimaryUserName = entity.PrimaryUserName,
            OnCallFallbackUserId = entity.OnCallFallbackUserId,
            OnCallFallbackUserName = entity.OnCallFallbackUserName,
            EscalationGroupId = entity.EscalationGroupId,
            EscalationGroupName = entity.EscalationGroup?.Name,
            EscalationTimeoutMinutes = entity.EscalationTimeoutMinutes,
            IsActive = entity.IsActive,
            Technology = entity.Technology,
            IsTechnologyPrimary = entity.IsTechnologyPrimary,
            IsCompleteMachineDedication = entity.IsCompleteMachineDedication,
            MaintenanceGroupIds = maintenanceGroupIds,
            MaintenanceGroupNames = maintenanceGroupNames,
            MachineGroupIds = machineGroupIds,
            MachineGroupNames = machineGroupNames,
            CreatedAt = entity.CreatedAt
        };
    }

    private async Task<Machine?> FindMachineByIdentifierAsync(string machineIdentifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(machineIdentifier)) return null;

        Guid? targetGuid = Guid.TryParse(machineIdentifier, out var parsed) ? parsed : null;
        return await _context.Machines
            .Include(m => m.SubLineGroup)
            .Include(m => m.LineGroup)
            .Include(m => m.ProjectGroup)
            .Include(m => m.BusinessUnitGroup)
            .FirstOrDefaultAsync(m => (targetGuid.HasValue && m.Id == targetGuid.Value) ||
                                      m.CustomIdentifier == machineIdentifier ||
                                      m.Name == machineIdentifier, cancellationToken);
    }

    public static List<string> GetEffectiveTechnologies(Machine machine)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (machine.Technologies != null)
        {
            foreach (var t in machine.Technologies)
            {
                if (!string.IsNullOrWhiteSpace(t)) set.Add(t.Trim());
            }
        }

        // Most machines need Controls and Mechanical support
        set.Add("Controls");
        set.Add("Mechanical");

        var sig = $"{machine.Name} {machine.CustomIdentifier} {machine.MachineType}".ToLowerInvariant();
        if (sig.Contains("aoi") ||
            sig.Contains("vision") ||
            sig.Contains("inspection") ||
            sig.Contains("camera") ||
            sig.Contains("cognex") ||
            sig.Contains("optical") ||
            sig.Contains("scanner"))
        {
            set.Add("Vision");
        }
        if (sig.Contains("robot"))
        {
            set.Add("Robotics");
        }
        if (sig.Contains("laser"))
        {
            set.Add("Laser");
        }
        if (sig.Contains("weld"))
        {
            set.Add("Welding");
        }
        if (!string.IsNullOrWhiteSpace(machine.MachineType) && !set.Contains(machine.MachineType))
        {
            bool alreadyCovered = set.Any(t => machine.MachineType.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                               t.Contains(machine.MachineType, StringComparison.OrdinalIgnoreCase));
            if (!alreadyCovered)
            {
                set.Add(machine.MachineType.Trim());
            }
        }

        return set.OrderBy(x => x).ToList();
    }

    public Task<MachineDedicationsDto> GetMachineDedicationsAsync(Guid machineId, CancellationToken cancellationToken = default)
        => GetMachineDedicationsAsync(machineId.ToString(), cancellationToken);

    public async Task<MachineDedicationsDto> GetMachineDedicationsAsync(string machineIdentifier, CancellationToken cancellationToken = default)
    {
        var machine = await FindMachineByIdentifierAsync(machineIdentifier, cancellationToken);

        if (machine == null)
        {
            return new MachineDedicationsDto
            {
                MachineId = Guid.TryParse(machineIdentifier, out var g) ? g : Guid.Empty,
                MachineName = machineIdentifier,
                CustomIdentifier = machineIdentifier,
                Technologies = new(),
                DirectDedications = new(),
                InheritedDedications = new(),
                TechnologicalGroupDedications = new()
            };
        }

        var techRules = await _context.TechnicianRules
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var personnelGroups = await _context.PersonnelGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .ToListAsync(cancellationToken);

        var techList = GetEffectiveTechnologies(machine);

        // Fetch all machine-level rules for this machine
        var machineRules = techRules
            .Where(r => string.Equals(r.ScopeType, "machine", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.TargetId, machine.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var directDedications = new List<MachineDirectDedicationDto>();

        foreach (var rule in machineRules)
        {
            string tech = string.IsNullOrWhiteSpace(rule.CategoryFilter) ? "General" : rule.CategoryFilter;
            string targetType = string.Equals(rule.AssignedByRole, "group", StringComparison.OrdinalIgnoreCase) ? "group" : "person";

            PersonnelGroup? matchedOverrideGroup = null;
            if (targetType == "person")
            {
                matchedOverrideGroup = personnelGroups.FirstOrDefault(g =>
                    (string.Equals(tech, "General", StringComparison.OrdinalIgnoreCase)
                        ? techList.Any(t => string.Equals(g.Discipline.ToString(), t, StringComparison.OrdinalIgnoreCase) ||
                                            g.Name.Contains(t, StringComparison.OrdinalIgnoreCase))
                        : string.Equals(g.Discipline.ToString(), tech, StringComparison.OrdinalIgnoreCase) ||
                          g.Name.Contains(tech, StringComparison.OrdinalIgnoreCase) ||
                          tech.Contains(g.Discipline.ToString(), StringComparison.OrdinalIgnoreCase) ||
                          tech.Contains(g.Name, StringComparison.OrdinalIgnoreCase)) &&
                    g.Members.Any(m => m.UserId == rule.TechnicianId));
            }

            directDedications.Add(new MachineDirectDedicationDto
            {
                Id = rule.Id,
                TargetType = targetType,
                TargetId = rule.TechnicianId,
                TargetName = rule.TechnicianName,
                TargetEmail = rule.TechnicianEmail,
                Technology = tech,
                IsTechnologySpecific = !string.Equals(tech, "General", StringComparison.OrdinalIgnoreCase),
                IsGroupMemberOverride = matchedOverrideGroup != null,
                OverriddenGroupName = matchedOverrideGroup?.Name,
                AssignedAt = rule.CreatedAt
            });
        }

        // Backward compatibility: If machine.PreferredTechnicianId is set but not yet in directDedications
        if (!string.IsNullOrWhiteSpace(machine.PreferredTechnicianId) &&
            !directDedications.Any(d => d.TargetId == machine.PreferredTechnicianId && d.TargetType == "person"))
        {
            var matchedOverrideGroup = personnelGroups.FirstOrDefault(g =>
                techList.Any(t => string.Equals(g.Discipline.ToString(), t, StringComparison.OrdinalIgnoreCase) ||
                                  g.Name.Contains(t, StringComparison.OrdinalIgnoreCase)) &&
                g.Members.Any(m => m.UserId == machine.PreferredTechnicianId));

            directDedications.Insert(0, new MachineDirectDedicationDto
            {
                Id = Guid.Empty,
                TargetType = "person",
                TargetId = machine.PreferredTechnicianId,
                TargetName = machine.PreferredTechnicianName ?? machine.PreferredTechnicianId,
                Technology = "General",
                IsTechnologySpecific = false,
                IsGroupMemberOverride = matchedOverrideGroup != null,
                OverriddenGroupName = matchedOverrideGroup?.Name,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        // Backward compatibility: Populate DedicatedPerson and DedicatedGroup
        var primaryPerson = directDedications.FirstOrDefault(d => d.TargetType == "person");
        DedicatedPersonDto? dedicatedPerson = primaryPerson != null ? new DedicatedPersonDto
        {
            UserId = primaryPerson.TargetId,
            UserName = primaryPerson.TargetName,
            Email = primaryPerson.TargetEmail,
            Source = "rule",
            Technology = primaryPerson.Technology,
            IsGroupMemberOverride = primaryPerson.IsGroupMemberOverride,
            OverriddenGroupName = primaryPerson.OverriddenGroupName
        } : null;

        var primaryGroup = directDedications.FirstOrDefault(d => d.TargetType == "group");
        DedicatedGroupDto? dedicatedGroup = null;
        if (primaryGroup != null)
        {
            var grp = personnelGroups.FirstOrDefault(g => g.Id.ToString() == primaryGroup.TargetId);
            dedicatedGroup = new DedicatedGroupDto
            {
                GroupId = grp?.Id ?? (Guid.TryParse(primaryGroup.TargetId, out var gid) ? gid : Guid.Empty),
                GroupName = primaryGroup.TargetName,
                Discipline = grp?.Discipline.ToString() ?? primaryGroup.Technology,
                Technology = primaryGroup.Technology,
                MemberCount = grp?.Members.Count ?? 0,
                MemberNames = grp?.Members.Select(m => m.UserName ?? m.UserId).ToList() ?? new()
            };
        }

        // Inherited from machine group hierarchy
        var inheritedList = new List<InheritedDedicationDto>();
        var hierarchyGroups = new List<(MachineGroup? Group, string Type)>
        {
            (machine.SubLineGroup, "SubLine"),
            (machine.LineGroup, "Line"),
            (machine.ProjectGroup, "Project"),
            (machine.BusinessUnitGroup, "BusinessUnit")
        };

        foreach (var (grp, grpType) in hierarchyGroups.Where(h => h.Group != null))
        {
            if (!string.IsNullOrWhiteSpace(grp!.LeadEngineerId))
            {
                inheritedList.Add(new InheritedDedicationDto
                {
                    MachineGroupId = grp.Id,
                    MachineGroupName = grp.Name,
                    GroupType = grpType,
                    DedicatedPerson = new DedicatedPersonDto
                    {
                        UserId = grp.LeadEngineerId,
                        UserName = grp.LeadEngineerName ?? grp.LeadEngineerId,
                        Source = "group_lead"
                    }
                });
            }
        }

        // Technological Groups (automatic dedication + per-technology superseding)
        var techDedications = new List<TechnologicalGroupDedicationDto>();
        var seenGroupIds = new HashSet<Guid>();
        foreach (var tech in techList)
        {
            var matchingGroups = personnelGroups.Where(g =>
                (string.Equals(g.Discipline.ToString(), tech, StringComparison.OrdinalIgnoreCase) ||
                 tech.Contains(g.Discipline.ToString(), StringComparison.OrdinalIgnoreCase) ||
                 g.Name.Contains(tech, StringComparison.OrdinalIgnoreCase) ||
                 tech.Contains(g.Name, StringComparison.OrdinalIgnoreCase) ||
                 (g.Description != null && g.Description.Contains(tech, StringComparison.OrdinalIgnoreCase))) &&
                !seenGroupIds.Contains(g.Id)).ToList();

            foreach (var tg in matchingGroups)
            {
                seenGroupIds.Add(tg.Id);
                var overridingPerson = directDedications.FirstOrDefault(d =>
                    d.TargetType == "person" &&
                    (string.Equals(d.Technology, tech, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(d.Technology, "General", StringComparison.OrdinalIgnoreCase)) &&
                    tg.Members.Any(m => m.UserId == d.TargetId));

                bool isSuperseded = overridingPerson != null;
                techDedications.Add(new TechnologicalGroupDedicationDto
                {
                    GroupId = tg.Id,
                    GroupName = tg.Name,
                    Technology = tech,
                    IsAutomaticallyDedicated = true,
                    IsSuperseded = isSuperseded,
                    SupersededByUserId = isSuperseded ? overridingPerson!.TargetId : null,
                    SupersededByUserName = isSuperseded ? overridingPerson!.TargetName : null,
                    MemberUserIds = tg.Members.Select(m => m.UserId).ToList(),
                    MemberNames = tg.Members.Select(m => m.UserName ?? m.UserId).ToList()
                });
            }
        }

        return new MachineDedicationsDto
        {
            MachineId = machine.Id,
            MachineName = machine.Name,
            CustomIdentifier = machine.CustomIdentifier,
            Technologies = techList,
            DirectDedications = directDedications,
            DedicatedPerson = dedicatedPerson,
            DedicatedGroup = dedicatedGroup,
            InheritedDedications = inheritedList,
            TechnologicalGroupDedications = techDedications
        };
    }

    public Task<MachineDedicationsDto> DedicateToMachineAsync(Guid machineId, DedicationAssignmentRequest request, CancellationToken cancellationToken = default)
        => DedicateToMachineAsync(machineId.ToString(), request, cancellationToken);

    public async Task<MachineDedicationsDto> DedicateToMachineAsync(string machineIdentifier, DedicationAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        // Never auto-create machines from a dedication request: an unknown identifier is a client error.
        var machine = await FindMachineByIdentifierAsync(machineIdentifier, cancellationToken)
            ?? throw new KeyNotFoundException($"Machine '{machineIdentifier}' was not found.");

        if (string.IsNullOrWhiteSpace(request.TargetId))
            throw new ArgumentException("A dedication target id is required.", nameof(request));

        string targetTech = string.IsNullOrWhiteSpace(request.Technology) ? "General" : request.Technology.Trim();
        string targetType = string.Equals(request.TargetType, "group", StringComparison.OrdinalIgnoreCase) ? "group" : "person";

        var existingRule = await _context.TechnicianRules
            .FirstOrDefaultAsync(r => r.ScopeType == "machine" &&
                                      r.TargetId == machine.Id.ToString() &&
                                      ((targetTech == "General" && (string.IsNullOrEmpty(r.CategoryFilter) || r.CategoryFilter == "General")) ||
                                       r.CategoryFilter == targetTech), cancellationToken);

        if (existingRule != null)
        {
            existingRule.TechnicianId = request.TargetId;
            existingRule.TechnicianName = request.TargetName;
            existingRule.TechnicianEmail = request.TargetEmail;
            existingRule.CategoryFilter = targetTech;
            existingRule.AssignedByRole = targetType;
        }
        else
        {
            _context.TechnicianRules.Add(new TechnicianRule
            {
                Id = Guid.NewGuid(),
                Name = $"Dedication_{machine.Name}_{targetTech}_{request.TargetName}",
                ScopeType = "machine",
                TargetId = machine.Id.ToString(),
                CategoryFilter = targetTech,
                AssignedByRole = targetType,
                TechnicianId = request.TargetId,
                TechnicianName = request.TargetName,
                TechnicianEmail = request.TargetEmail,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        if (targetType == "person")
        {
            if (targetTech == "General" || string.IsNullOrWhiteSpace(machine.PreferredTechnicianId))
            {
                machine.PreferredTechnicianId = request.TargetId;
                machine.PreferredTechnicianName = request.TargetName;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetMachineDedicationsAsync(machine.Id, cancellationToken);
    }

    public Task<bool> RemoveMachineDedicationAsync(Guid machineId, string? targetId = null, string? technology = null, CancellationToken cancellationToken = default)
        => RemoveMachineDedicationAsync(machineId.ToString(), targetId, technology, cancellationToken);

    public async Task<bool> RemoveMachineDedicationAsync(string machineIdentifier, string? targetId = null, string? technology = null, CancellationToken cancellationToken = default)
    {
        var machine = await FindMachineByIdentifierAsync(machineIdentifier, cancellationToken);
        if (machine == null) return false;

        var query = _context.TechnicianRules
            .Where(r => r.ScopeType == "machine" && r.TargetId == machine.Id.ToString());

        if (!string.IsNullOrWhiteSpace(targetId))
        {
            query = query.Where(r => r.TechnicianId == targetId);
        }

        if (!string.IsNullOrWhiteSpace(technology))
        {
            query = query.Where(r => r.CategoryFilter == technology || (technology == "General" && string.IsNullOrEmpty(r.CategoryFilter)));
        }

        var rulesToRemove = await query.ToListAsync(cancellationToken);
        if (rulesToRemove.Count > 0)
        {
            _context.TechnicianRules.RemoveRange(rulesToRemove);
        }

        // If targetId is null (remove all) or targetId matches PreferredTechnicianId, synchronize PreferredTechnicianId
        if (string.IsNullOrWhiteSpace(targetId) || machine.PreferredTechnicianId == targetId)
        {
            var remainingRules = await _context.TechnicianRules
                .Where(r => r.ScopeType == "machine" && r.TargetId == machine.Id.ToString() && !rulesToRemove.Select(x => x.Id).Contains(r.Id))
                .ToListAsync(cancellationToken);

            var remainingPerson = remainingRules.FirstOrDefault(r => string.IsNullOrEmpty(r.AssignedByRole) || r.AssignedByRole == "person");
            if (remainingPerson != null)
            {
                machine.PreferredTechnicianId = remainingPerson.TechnicianId;
                machine.PreferredTechnicianName = remainingPerson.TechnicianName;
            }
            else
            {
                machine.PreferredTechnicianId = null;
                machine.PreferredTechnicianName = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserDedicationsDto> GetUserDedicationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allMachines = await _context.Machines.AsNoTracking().ToListAsync(cancellationToken);
        var allMachineGroups = await _context.MachineGroups.AsNoTracking().ToListAsync(cancellationToken);
        var techRules = await _context.TechnicianRules.AsNoTracking().ToListAsync(cancellationToken);
        var personnelGroups = await _context.PersonnelGroups.AsNoTracking().Include(g => g.Members).ToListAsync(cancellationToken);

        var userMachineRules = techRules
            .Where(r => r.ScopeType == "machine" && r.TechnicianId == userId)
            .ToList();

        var directlyDedicatedMachines = new List<DedicatedMachineItemDto>();
        var seenMachineTechs = new HashSet<string>();

        foreach (var rule in userMachineRules)
        {
            var m = allMachines.FirstOrDefault(x => x.Id.ToString() == rule.TargetId);
            if (m == null) continue;

            string tech = string.IsNullOrWhiteSpace(rule.CategoryFilter) ? "General" : rule.CategoryFilter;
            string key = $"{m.Id}_{tech}";
            if (!seenMachineTechs.Add(key)) continue;

            var userTechGroups = personnelGroups.Where(g => g.Members.Any(mem => mem.UserId == userId)).ToList();
            var effectiveTechs = GetEffectiveTechnologies(m);

            var overriddenGroup = userTechGroups.FirstOrDefault(g =>
                string.Equals(tech, "General", StringComparison.OrdinalIgnoreCase)
                    ? effectiveTechs.Any(t => string.Equals(g.Discipline.ToString(), t, StringComparison.OrdinalIgnoreCase) ||
                                              g.Name.Contains(t, StringComparison.OrdinalIgnoreCase))
                    : string.Equals(g.Discipline.ToString(), tech, StringComparison.OrdinalIgnoreCase) ||
                      g.Name.Contains(tech, StringComparison.OrdinalIgnoreCase) ||
                      tech.Contains(g.Discipline.ToString(), StringComparison.OrdinalIgnoreCase));

            directlyDedicatedMachines.Add(new DedicatedMachineItemDto
            {
                MachineId = m.Id,
                MachineName = m.Name,
                CustomIdentifier = m.CustomIdentifier,
                MachineType = m.MachineType,
                Technology = tech,
                SupersedesTechnologicalGroup = overriddenGroup != null,
                SupersededGroupName = overriddenGroup?.Name
            });
        }

        var seenMachineIds = new HashSet<Guid>(userMachineRules.Select(r => Guid.TryParse(r.TargetId, out var g) ? g : Guid.Empty));

        foreach (var m in allMachines.Where(x => x.PreferredTechnicianId == userId && !seenMachineIds.Contains(x.Id)))
        {
            string key = $"{m.Id}_General";
            if (!seenMachineTechs.Add(key)) continue;

            var userTechGroups = personnelGroups.Where(g => g.Members.Any(mem => mem.UserId == userId)).ToList();
            var effectiveTechs = GetEffectiveTechnologies(m);

            var overriddenGroup = userTechGroups.FirstOrDefault(g =>
                effectiveTechs.Any(t => string.Equals(g.Discipline.ToString(), t, StringComparison.OrdinalIgnoreCase) ||
                                        g.Name.Contains(t, StringComparison.OrdinalIgnoreCase)));

            directlyDedicatedMachines.Add(new DedicatedMachineItemDto
            {
                MachineId = m.Id,
                MachineName = m.Name,
                CustomIdentifier = m.CustomIdentifier,
                MachineType = m.MachineType,
                Technology = "General",
                SupersedesTechnologicalGroup = overriddenGroup != null,
                SupersededGroupName = overriddenGroup?.Name
            });
        }

        var dedicatedGroups = allMachineGroups
            .Where(mg => mg.LeadEngineerId == userId)
            .Select(mg => new DedicatedMachineGroupItemDto
            {
                GroupId = mg.Id,
                GroupName = mg.Name,
                GroupType = mg.GroupType.ToString(),
                MachineCount = mg.GetMachineIds().Count
            }).ToList();

        var userGroups = personnelGroups.Where(g => g.Members.Any(m => m.UserId == userId)).ToList();
        var coverageList = new List<UserTechnologicalCoverageDto>();

        foreach (var g in userGroups)
        {
            string techKey = g.Discipline.ToString();
            var coveredMachines = allMachines
                .Where(m => {
                    var mTechs = GetEffectiveTechnologies(m);
                    return mTechs.Any(t => string.Equals(t, techKey, StringComparison.OrdinalIgnoreCase) ||
                                           t.Contains(techKey, StringComparison.OrdinalIgnoreCase) ||
                                           techKey.Contains(t, StringComparison.OrdinalIgnoreCase));
                })
                .Select(m => {
                    bool isSuperseded = directlyDedicatedMachines.Any(d => d.MachineId == m.Id &&
                        (string.Equals(d.Technology, techKey, StringComparison.OrdinalIgnoreCase) || string.Equals(d.Technology, "General", StringComparison.OrdinalIgnoreCase)));
                    return new DedicatedMachineItemDto
                    {
                        MachineId = m.Id,
                        MachineName = m.Name,
                        CustomIdentifier = m.CustomIdentifier,
                        MachineType = m.MachineType,
                        Technology = techKey,
                        SupersedesTechnologicalGroup = isSuperseded,
                        SupersededGroupName = g.Name
                    };
                }).ToList();

            coverageList.Add(new UserTechnologicalCoverageDto
            {
                GroupId = g.Id,
                GroupName = g.Name,
                Technology = techKey,
                CoveredMachines = coveredMachines
            });
        }

        return new UserDedicationsDto
        {
            UserId = userId,
            UserName = userId,
            DirectlyDedicatedMachines = directlyDedicatedMachines,
            DedicatedMachineGroups = dedicatedGroups,
            TechnologicalCoverage = coverageList
        };
    }

    public Task<bool> DedicateUserToMachineAsync(string userId, Guid machineId, string? technology = null, CancellationToken cancellationToken = default)
        => DedicateUserToMachineAsync(userId, machineId.ToString(), technology, cancellationToken);

    public async Task<bool> DedicateUserToMachineAsync(string userId, string machineIdentifier, string? technology = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await DedicateToMachineAsync(machineIdentifier, new DedicationAssignmentRequest
            {
                TargetType = "person",
                TargetId = userId,
                TargetName = userId,
                Technology = technology
            }, cancellationToken);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    public Task<bool> RemoveUserDedicationFromMachineAsync(string userId, Guid machineId, string? technology = null, CancellationToken cancellationToken = default)
        => RemoveUserDedicationFromMachineAsync(userId, machineId.ToString(), technology, cancellationToken);

    public async Task<bool> RemoveUserDedicationFromMachineAsync(string userId, string machineIdentifier, string? technology = null, CancellationToken cancellationToken = default)
    {
        return await RemoveMachineDedicationAsync(machineIdentifier, targetId: userId, technology: technology, cancellationToken: cancellationToken);
    }
}
