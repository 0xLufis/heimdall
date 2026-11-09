using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class StationRepository : IStationRepository
{
    private readonly AppDbContext _context;

    public StationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Machine>> GetAllAsync()
    {
        return await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.ResponsibleTeams)
            .Include(m => m.SubMachines)
            .Include(m => m.ParentMachine)
            .Include(m => m.SubLineGroup)
            .Include(m => m.LineGroup)
            .Include(m => m.ProjectGroup)
            .Include(m => m.BusinessUnitGroup)
            .Include(m => m.Components)
                .ThenInclude(c => c.ComponentRecipe)
            .Include(m => m.DelegationBindings)
                .ThenInclude(b => b.DelegationRecipe)
            .ToListAsync();
    }

    public async Task<Machine?> GetByIdAsync(Guid id)
    {
        return await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.ResponsibleTeams)
            .Include(m => m.SubMachines)
            .Include(m => m.ParentMachine)
            .Include(m => m.SubLineGroup)
            .Include(m => m.LineGroup)
            .Include(m => m.ProjectGroup)
            .Include(m => m.BusinessUnitGroup)
            .Include(m => m.Components)
                .ThenInclude(c => c.ComponentRecipe)
            .Include(m => m.Components)
                .ThenInclude(c => c.PartHistory)
            .Include(m => m.DelegationBindings)
                .ThenInclude(b => b.DelegationRecipe)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Machine> CreateAsync(Machine machine)
    {
        _context.Machines.Add(machine);
        await _context.SaveChangesAsync();
        return machine;
    }

    public async Task<Machine?> UpdateAsync(
        Guid id, 
        string? name, 
        string? customIdentifier, 
        string? pinnedObjectHandle, 
        string? organizationId, 
        List<Guid>? controllerIds,
        string? spoc = null,
        string? spocUserId = null,
        string? owner = null,
        string? ownerUserId = null,
        Guid? subLineGroupId = null,
        Guid? lineGroupId = null,
        Guid? projectGroupId = null,
        Guid? businessUnitGroupId = null,
        Guid? parentMachineId = null,
        int? subMachineIndex = null,
        string? cellType = null)
    {
        var machine = await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.ResponsibleTeams)
            .Include(m => m.SubMachines)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (machine == null) return null;

        if (!string.IsNullOrEmpty(name)) machine.Name = name;
        if (!string.IsNullOrEmpty(customIdentifier)) machine.CustomIdentifier = customIdentifier;
        if (!string.IsNullOrEmpty(pinnedObjectHandle)) machine.PinnedObjectHandle = pinnedObjectHandle;
        if (!string.IsNullOrEmpty(organizationId)) machine.OrganizationId = organizationId;
        if (spoc != null) machine.Spoc = spoc;
        if (spocUserId != null) machine.SpocUserId = spocUserId;
        if (owner != null) machine.Owner = owner;
        if (ownerUserId != null) machine.OwnerUserId = ownerUserId;
        if (subLineGroupId.HasValue) machine.SubLineGroupId = subLineGroupId;
        if (lineGroupId.HasValue) machine.LineGroupId = lineGroupId;
        if (projectGroupId.HasValue) machine.ProjectGroupId = projectGroupId;
        if (businessUnitGroupId.HasValue) machine.BusinessUnitGroupId = businessUnitGroupId;
        if (parentMachineId.HasValue) machine.ParentMachineId = parentMachineId;
        if (subMachineIndex.HasValue) machine.SubMachineIndex = subMachineIndex;
        if (cellType != null) machine.CellType = cellType;

        if (controllerIds != null)
        {
            machine.Controllers.Clear();
            foreach (var pcId in controllerIds)
            {
                var existingPc = await _context.ClientPcs.FindAsync(pcId);
                if (existingPc != null)
                {
                    machine.Controllers.Add(existingPc);
                }
            }
        }

        await _context.SaveChangesAsync();
        return machine;
    }

    public async Task<Machine?> UpdateStatusAsync(Guid id, string status)
    {
        var machine = await _context.Machines.FindAsync(id);
        if (machine == null) return null;
        machine.Status = status;
        await _context.SaveChangesAsync();
        return machine;
    }

    public async Task<Machine?> DecommissionAsync(Guid id, string reason, string targetDisposition, string partsHandling, DateTimeOffset? date)
    {
        var machine = await _context.Machines.FindAsync(id);
        if (machine == null) return null;
        machine.Status = targetDisposition == "dismantled" ? "dismantled" : "decommissioned";
        machine.DecommissionReason = reason;
        machine.DecommissionDate = date ?? DateTimeOffset.UtcNow;
        machine.DecommissionDisposition = partsHandling;
        await _context.SaveChangesAsync();
        return machine;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var machine = await _context.Machines.FindAsync(id);
        if (machine == null) return false;
        _context.Machines.Remove(machine);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.Machines.CountAsync();
    }

    public async Task<List<Machine>> GetSubMachinesAsync(Guid parentMachineId)
    {
        return await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.ResponsibleTeams)
            .Where(m => m.ParentMachineId == parentMachineId)
            .OrderBy(m => m.SubMachineIndex ?? int.MaxValue)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<Machine?> AddSubMachineAsync(Guid parentMachineId, Machine subMachine)
    {
        var parent = await _context.Machines.FindAsync(parentMachineId);
        if (parent == null) return null;

        subMachine.ParentMachineId = parentMachineId;
        if (string.IsNullOrWhiteSpace(subMachine.OrganizationId))
            subMachine.OrganizationId = parent.OrganizationId;
        if (!subMachine.LineGroupId.HasValue)
            subMachine.LineGroupId = parent.LineGroupId;
        if (!subMachine.SubLineGroupId.HasValue)
            subMachine.SubLineGroupId = parent.SubLineGroupId;

        if (subMachine.Id == Guid.Empty)
            subMachine.Id = Guid.NewGuid();

        _context.Machines.Add(subMachine);
        await _context.SaveChangesAsync();
        return subMachine;
    }

    public async Task<bool> RemoveSubMachineAsync(Guid parentMachineId, Guid subMachineId)
    {
        var subMachine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == subMachineId && m.ParentMachineId == parentMachineId);
        if (subMachine == null) return false;

        subMachine.ParentMachineId = null;
        subMachine.SubMachineIndex = null;
        await _context.SaveChangesAsync();
        return true;
    }
}
