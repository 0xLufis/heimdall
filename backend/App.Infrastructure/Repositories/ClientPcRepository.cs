using System.Text.Json;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// Repository for managing <see cref="ClientPc"/> entities.
/// Provides data access operations including CRUD and upsert by MAC address.
/// </summary>
public class ClientPcRepository
{
    private readonly AppDbContext _context;

    public ClientPcRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ClientPc> CreateAsync(ClientPc pc)
    {
        _context.ClientPcs.Add(pc);
        await _context.SaveChangesAsync();
        return pc;
    }

    public async Task<ClientPc?> GetByIdAsync(Guid id)
    {
        return await _context.ClientPcs
            .Include(pc => pc.ControlledMachines)
            .Include(pc => pc.InventoryItems)
            .FirstOrDefaultAsync(pc => pc.Id == id);
    }

    public async Task<List<ClientPc>> GetAllAsync()
    {
        return await _context.ClientPcs
            .Include(pc => pc.ControlledMachines)
            .ToListAsync();
    }

    /// <summary>
    /// Inserts a new ClientPc or updates an existing one based on its MAC address.
    /// </summary>
    public async Task<ClientPc> UpsertByMacAddressAsync(ClientPc pc)
    {
        // 1. Fetch existing PC by MAC address, including only what we need to update
        var existingByMac = await _context.ClientPcs
            .Include(x => x.InventoryItems)
            .FirstOrDefaultAsync(x => x.MacAddress == pc.MacAddress);

        // 2. Check for hostname collision with OTHER PCs
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

        // 3. Update top-level properties only if they changed
        bool changed = false;
        
        if (existingByMac.Hostname != pc.Hostname) { existingByMac.Hostname = pc.Hostname; changed = true; }
        if (existingByMac.MachineIdentifier != pc.MachineIdentifier) { existingByMac.MachineIdentifier = pc.MachineIdentifier; changed = true; }
        
        // Always update last online as it's a timestamp of the report
        existingByMac.LastOnline = pc.LastOnline;
        changed = true; // LastOnline always changes in a report
        
        if (pc.FreeDiskSpace != null)
        {
            // Simple comparison for DiskSpaceInfo
            existingByMac.FreeDiskSpace = pc.FreeDiskSpace;
            changed = true;
        }

        // 4. Efficiently update PcHardware components
        if (pc.InventoryItems != null)
        {
            var reportedHardwares = pc.InventoryItems.OfType<PcHardware>().ToList();
            var existingHardwares = existingByMac.InventoryItems.OfType<PcHardware>().ToList();
            
            var reportedNames = new HashSet<string>(reportedHardwares.Select(h => h.Name));
            
            // Remove components no longer reported
            int removedCount = existingByMac.InventoryItems.RemoveAll(i => i is PcHardware h && !reportedNames.Contains(h.Name));
            if (removedCount > 0) changed = true;

            foreach (var reported in reportedHardwares)
            {
                var existing = existingHardwares.FirstOrDefault(h => h.Name == reported.Name);
                if (existing != null)
                {
                    // Update existing only if different
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
                    // Add new
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
}
