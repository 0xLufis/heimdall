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
        CancellationToken cancellationToken = default)
    {
        // 1. Find matching machine delegation binding
        var bindings = await _context.MachineDelegationBindings
            .Include(b => b.DelegationRecipe)
                .ThenInclude(r => r!.PrimaryPersonnelGroup)
                    .ThenInclude(g => g!.Members)
            .Include(b => b.DelegationRecipe)
                .ThenInclude(r => r!.EscalationGroup)
            .Where(b => b.MachineId == machineId && b.IsActive)
            .ToListAsync(cancellationToken);

        var matchedBinding = bindings.FirstOrDefault(b => b.Discipline == discipline)
            ?? bindings.FirstOrDefault(b => b.Discipline == DelegationDiscipline.General)
            ?? bindings.FirstOrDefault();

        // 2. If no binding on the machine, fall back to machine default preferred technician
        if (matchedBinding == null || matchedBinding.DelegationRecipe == null)
        {
            var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == machineId, cancellationToken);
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = Guid.Empty,
                RecipeName = "DefaultMachineTechnician",
                Discipline = discipline,
                EffectiveAssigneeId = machine?.PreferredTechnicianId,
                EffectiveAssigneeName = machine?.PreferredTechnicianName,
                EffectiveGroupId = null,
                EffectiveGroupName = null,
                IsOnCallFallback = false,
                IsEscalated = false,
                ResolutionReason = machine?.PreferredTechnicianId != null
                    ? "Resolved to machine dedicated preferred technician."
                    : "No delegation recipe bound and no preferred technician configured."
            };
        }

        var recipe = matchedBinding.DelegationRecipe;

        // 3. Check if schedule is OnCall or if outside regular shifts
        if (scheduleType == WorkerScheduleType.OnCall && !string.IsNullOrWhiteSpace(recipe.OnCallFallbackUserId))
        {
            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = recipe.Id,
                RecipeName = recipe.Name,
                Discipline = recipe.Discipline,
                EffectiveAssigneeId = recipe.OnCallFallbackUserId,
                EffectiveAssigneeName = recipe.OnCallFallbackUserName ?? recipe.OnCallFallbackUserId,
                EffectiveGroupId = null,
                EffectiveGroupName = null,
                IsOnCallFallback = true,
                IsEscalated = false,
                ResolutionReason = "Resolved to recipe On-Call specialist fallback for off-hours event."
            };
        }

        // 4. Check skills requirement against personnel group members
        List<string> requiredSkills = new();
        if (!string.IsNullOrWhiteSpace(recipe.RequiredSkillCodesJson))
        {
            try
            {
                requiredSkills = JsonSerializer.Deserialize<List<string>>(recipe.RequiredSkillCodesJson) ?? new();
            }
            catch { }
        }

        if (recipe.PrimaryPersonnelGroup != null && recipe.PrimaryPersonnelGroup.Members.Count > 0)
        {
            var memberUserIds = recipe.PrimaryPersonnelGroup.Members.Select(m => m.UserId).ToList();

            if (requiredSkills.Count > 0)
            {
                // Find member who holds the required valid skills
                var activeSkills = await _context.PersonnelSkills
                    .Include(ps => ps.Skill)
                    .Where(ps => memberUserIds.Contains(ps.UserId) &&
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
                    var qualifiedMember = recipe.PrimaryPersonnelGroup.Members.FirstOrDefault(m => m.UserId == qualifiedMemberId);
                    return new ResolvedPersonnelDto
                    {
                        DelegationRecipeId = recipe.Id,
                        RecipeName = recipe.Name,
                        Discipline = recipe.Discipline,
                        EffectiveAssigneeId = qualifiedMemberId,
                        EffectiveAssigneeName = qualifiedMember?.UserName ?? qualifiedMemberId,
                        EffectiveGroupId = recipe.PrimaryPersonnelGroupId,
                        EffectiveGroupName = recipe.PrimaryPersonnelGroup.Name,
                        IsOnCallFallback = false,
                        IsEscalated = false,
                        ResolutionReason = $"Resolved to certified group member matching required skills ({string.Join(", ", requiredSkills)})."
                    };
                }
            }

            // Fall back to primary group or primary user
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
                    EffectiveGroupName = recipe.PrimaryPersonnelGroup.Name,
                    IsOnCallFallback = false,
                    IsEscalated = false,
                    ResolutionReason = "Resolved to primary designated technician within group."
                };
            }

            return new ResolvedPersonnelDto
            {
                DelegationRecipeId = recipe.Id,
                RecipeName = recipe.Name,
                Discipline = recipe.Discipline,
                EffectiveAssigneeId = null,
                EffectiveAssigneeName = null,
                EffectiveGroupId = recipe.PrimaryPersonnelGroupId,
                EffectiveGroupName = recipe.PrimaryPersonnelGroup.Name,
                IsOnCallFallback = false,
                IsEscalated = false,
                ResolutionReason = $"Resolved to primary personnel group '{recipe.PrimaryPersonnelGroup.Name}'."
            };
        }

        // 5. Fallback to primary direct user on recipe
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
            .Where(b => b.MachineId == machineId && b.IsActive)
            .ToListAsync(cancellationToken);

        return bindings
            .Where(b => b.DelegationRecipe != null)
            .Select(b => MapToDto(b.DelegationRecipe!))
            .ToList();
    }

    public async Task<DelegationRecipeDto?> GetRecipeByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await _context.DelegationRecipes
            .AsNoTracking()
            .Include(r => r.PrimaryPersonnelGroup)
            .Include(r => r.EscalationGroup)
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        return recipe == null ? null : MapToDto(recipe);
    }

    public async Task<List<DelegationRecipeDto>> GetAllRecipesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _context.DelegationRecipes
            .AsNoTracking()
            .Include(r => r.PrimaryPersonnelGroup)
            .Include(r => r.EscalationGroup)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    public async Task<DelegationRecipeDto> CreateOrUpdateRecipeAsync(DelegationRecipeDto dto, CancellationToken cancellationToken = default)
    {
        DelegationRecipe? entity = null;
        if (dto.Id != Guid.Empty)
        {
            entity = await _context.DelegationRecipes.FirstOrDefaultAsync(r => r.Id == dto.Id, cancellationToken);
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
        entity.PrimaryPersonnelGroupId = dto.PrimaryPersonnelGroupId;
        entity.PrimaryUserId = dto.PrimaryUserId;
        entity.PrimaryUserName = dto.PrimaryUserName;
        entity.OnCallFallbackUserId = dto.OnCallFallbackUserId;
        entity.OnCallFallbackUserName = dto.OnCallFallbackUserName;
        entity.EscalationGroupId = dto.EscalationGroupId;
        entity.EscalationTimeoutMinutes = dto.EscalationTimeoutMinutes;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

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

        var recipe = await _context.DelegationRecipes.FindAsync(recipeId);

        return new MachineDelegationBindingDto
        {
            Id = binding.Id,
            MachineId = binding.MachineId,
            DelegationRecipeId = binding.DelegationRecipeId,
            DelegationRecipeName = recipe?.Name ?? string.Empty,
            Discipline = binding.Discipline,
            IsActive = binding.IsActive
        };
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
            CreatedAt = entity.CreatedAt
        };
    }
}
