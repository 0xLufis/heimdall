namespace App.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

public class MachineGroupRepository : IMachineGroupRepository
{
    private readonly AppDbContext _context;

    public MachineGroupRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<MachineGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MachineGroups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<MachineGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MachineGroups
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<MachineGroup> CreateAsync(MachineGroup group, CancellationToken cancellationToken = default)
    {
        if (group.Id == Guid.Empty)
            group.Id = Guid.NewGuid();
        group.CreatedAt = DateTimeOffset.UtcNow;
        group.UpdatedAt = DateTimeOffset.UtcNow;

        _context.MachineGroups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
        return group;
    }

    public async Task<MachineGroup?> UpdateAsync(Guid id, MachineGroup updates, CancellationToken cancellationToken = default)
    {
        var existing = await _context.MachineGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (existing == null) return null;

        if (updates.ParentId.HasValue && await WouldCreateCycleAsync(id, updates.ParentId.Value, cancellationToken))
        {
            // A cycle has no root: the whole branch would vanish from every tree view.
            throw new InvalidOperationException("A machine group cannot be moved under itself or one of its own sub-groups.");
        }

        existing.Name = updates.Name;
        existing.Description = updates.Description;
        existing.ParentId = updates.ParentId;
        existing.Color = updates.Color;
        existing.Icon = updates.Icon;
        existing.LeadEngineerId = updates.LeadEngineerId;
        existing.LeadEngineerName = updates.LeadEngineerName;
        existing.MachineIdsJson = updates.MachineIdsJson;
        existing.MachineTypesJson = updates.MachineTypesJson;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.MachineGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (existing == null) return false;

        // parent_id is ON DELETE RESTRICT, so deleting a group that still has sub-groups failed with
        // a database error. The sub-groups move up to the deleted group's own parent instead.
        var children = await _context.MachineGroups.Where(g => g.ParentId == id).ToListAsync(cancellationToken);
        foreach (var child in children)
        {
            child.ParentId = existing.ParentId;
            child.UpdatedAt = DateTimeOffset.UtcNow;
        }
        // Saved on its own so the references are gone before the DELETE reaches the database.
        if (children.Count > 0) await _context.SaveChangesAsync(cancellationToken);

        _context.MachineGroups.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>True when making <paramref name="newParentId"/> the parent of <paramref name="id"/> would close a loop.</summary>
    private async Task<bool> WouldCreateCycleAsync(Guid id, Guid newParentId, CancellationToken cancellationToken)
    {
        if (newParentId == id) return true;
        var parents = await _context.MachineGroups.AsNoTracking()
            .Select(g => new { g.Id, g.ParentId })
            .ToDictionaryAsync(g => g.Id, g => g.ParentId, cancellationToken);

        var visited = new HashSet<Guid>();
        Guid? cursor = newParentId;
        while (cursor.HasValue && visited.Add(cursor.Value))
        {
            if (cursor.Value == id) return true;
            cursor = parents.TryGetValue(cursor.Value, out var parent) ? parent : null;
        }
        return false;
    }
}
