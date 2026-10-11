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
            .Include(m => m.Components)
                .ThenInclude(c => c.ComponentRecipe)
            .Include(m => m.DelegationBindings)
                .ThenInclude(b => b.DelegationRecipe)
            .Include(m => m.LineGroup)
            .ToListAsync();
    }

    public async Task<Machine?> GetByIdAsync(Guid id)
    {
        return await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.ResponsibleTeams)
            .Include(m => m.Components)
                .ThenInclude(c => c.ComponentRecipe)
            .Include(m => m.Components)
                .ThenInclude(c => c.PartHistory)
            .Include(m => m.DelegationBindings)
                .ThenInclude(b => b.DelegationRecipe)
            .Include(m => m.LineGroup)
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
        List<Guid>? controllerIds)
    {
        var machine = await _context.Machines
            .Include(m => m.Controllers)
            .Include(m => m.ResponsibleTeams)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (machine == null) return null;

        if (!string.IsNullOrEmpty(name)) machine.Name = name;
        if (!string.IsNullOrEmpty(customIdentifier)) machine.CustomIdentifier = customIdentifier;
        if (!string.IsNullOrEmpty(pinnedObjectHandle)) machine.PinnedObjectHandle = pinnedObjectHandle;
        if (!string.IsNullOrEmpty(organizationId)) machine.OrganizationId = organizationId;

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
}
