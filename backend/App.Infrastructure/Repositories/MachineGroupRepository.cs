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

        _context.MachineGroups.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
