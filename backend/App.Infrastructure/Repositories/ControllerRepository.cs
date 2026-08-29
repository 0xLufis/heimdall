using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class ControllerRepository : IControllerRepository
{
    private readonly AppDbContext _context;

    public ControllerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClientPc>> GetAllAsync()
    {
        return await _context.ClientPcs
            .Include(c => c.ControlledMachines)
            .Include(c => c.ResponsibleTeams)
            .Include(c => c.InventoryItems)
                .ThenInclude(i => i.Children)
                    .ThenInclude(c => c.Children)
                        .ThenInclude(c => c.Children)
                            .ThenInclude(c => c.Children)
            .ToListAsync();
    }

    public async Task<ClientPc?> GetByIdAsync(Guid id)
    {
        return await _context.ClientPcs
            .Include(pc => pc.ControlledMachines)
            .Include(pc => pc.ResponsibleTeams)
            .Include(pc => pc.InventoryItems)
            .FirstOrDefaultAsync(pc => pc.Id == id);
    }

    public async Task<ClientPc> CreateAsync(ClientPc pc)
    {
        _context.ClientPcs.Add(pc);
        await _context.SaveChangesAsync();
        return pc;
    }

    public async Task<ClientPc> UpsertByMacAddressAsync(ClientPc pc)
    {
        var existingByMac = await _context.ClientPcs
            .Include(x => x.InventoryItems)
            .FirstOrDefaultAsync(x => x.MacAddress == pc.MacAddress);

        var existingByHostname = await _context.ClientPcs
            .Where(x => x.Hostname == pc.Hostname && x.MacAddress != pc.MacAddress)
            .FirstOrDefaultAsync();

        if (existingByHostname != null)
        {
            existingByHostname.Hostname = $"{existingByHostname.Hostname}-OLD-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        if (existingByMac == null)
        {
            _context.ClientPcs.Add(pc);
            await _context.SaveChangesAsync();
            return pc;
        }

        bool changed = false;
        if (existingByMac.Hostname != pc.Hostname) { existingByMac.Hostname = pc.Hostname; changed = true; }
        if (existingByMac.MachineIdentifier != pc.MachineIdentifier) { existingByMac.MachineIdentifier = pc.MachineIdentifier; changed = true; }
        
        existingByMac.LastOnline = pc.LastOnline;
        changed = true;
        
        if (pc.FreeDiskSpace != null)
        {
            existingByMac.FreeDiskSpace = pc.FreeDiskSpace;
            changed = true;
        }

        if (pc.InventoryItems != null)
        {
            var reportedHardwares = pc.InventoryItems.OfType<PcHardware>().ToList();
            var existingHardwares = existingByMac.InventoryItems.OfType<PcHardware>().ToList();
            var reportedNames = new HashSet<string>(reportedHardwares.Select(h => h.Name));
            
            int removedCount = existingByMac.InventoryItems.RemoveAll(i => i is PcHardware h && !reportedNames.Contains(h.Name));
            if (removedCount > 0) changed = true;

            foreach (var reported in reportedHardwares)
            {
                var existing = existingHardwares.FirstOrDefault(h => h.Name == reported.Name);
                if (existing != null)
                {
                    if (existing.Type != reported.Type || existing.Capacity != reported.Capacity)
                    {
                        existing.Type = reported.Type;
                        existing.Capacity = reported.Capacity;
                        existing.Metadata = reported.Metadata;
                        changed = true;
                    }
                }
                else
                {
                    existingByMac.InventoryItems.Add(reported);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
        return existingByMac;
    }

    public async Task<ClientPc?> UpdateAsync(
        Guid id, 
        string? name, 
        string? hostname, 
        string? macAddress, 
        string? pinnedObjectHandle, 
        List<Guid>? controlledMachineIds)
    {
        var pc = await _context.ClientPcs
            .Include(c => c.ControlledMachines)
            .Include(c => c.ResponsibleTeams)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (pc == null) return null;

        if (!string.IsNullOrEmpty(pinnedObjectHandle)) pc.PinnedObjectHandle = pinnedObjectHandle;
        if (!string.IsNullOrEmpty(name)) pc.Name = name;
        if (!string.IsNullOrEmpty(hostname)) pc.Hostname = hostname;
        if (!string.IsNullOrEmpty(macAddress)) pc.MacAddress = macAddress;

        if (controlledMachineIds != null)
        {
            pc.ControlledMachines.Clear();
            foreach (var machineId in controlledMachineIds)
            {
                var existingMachine = await _context.Machines.FindAsync(machineId);
                if (existingMachine != null)
                {
                    pc.ControlledMachines.Add(existingMachine);
                }
            }
        }

        await _context.SaveChangesAsync();
        return pc;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var pc = await _context.ClientPcs.FindAsync(id);
        if (pc == null) return false;
        _context.ClientPcs.Remove(pc);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.ClientPcs.CountAsync();
    }

    public async Task<int> GetActiveCountAsync(TimeSpan activeThreshold)
    {
        var threshold = DateTimeOffset.UtcNow.Subtract(activeThreshold);
        return await _context.ClientPcs.CountAsync(c => c.LastOnline >= threshold);
    }

    public async Task<List<ClientPc>> GetRecentClientsAsync(int count)
    {
        return await _context.ClientPcs
            .OrderByDescending(c => c.LastOnline)
            .Take(count)
            .ToListAsync();
    }
}
