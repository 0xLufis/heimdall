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
            .Include(pc => pc.Machines)
            .Include(pc => pc.Components)
            .FirstOrDefaultAsync(pc => pc.Id == id);
    }

    public async Task<List<ClientPc>> GetAllAsync()
    {
        return await _context.ClientPcs
            .Include(pc => pc.Machines)
            .ToListAsync();
    }

    /// <summary>
    /// Inserts a new ClientPc or updates an existing one based on its MAC address.
    /// </summary>
    public async Task<ClientPc> UpsertByMacAddressAsync(ClientPc pc)
    {
        var existingByMac = await _context.ClientPcs
            .Include(x => x.Machines)
            .FirstOrDefaultAsync(x => x.MacAddress == pc.MacAddress);

        var existingByHostname = await _context.ClientPcs
            .FirstOrDefaultAsync(x => x.Hostname == pc.Hostname && x.MacAddress != pc.MacAddress);

        if (existingByHostname != null)
        {
            existingByHostname.Hostname = $"{existingByHostname.Hostname}-OLD-{DateTime.UtcNow:yyyyMMddHHmmss}";
            await _context.SaveChangesAsync();
        }

        if (existingByMac == null)
        {
            _context.ClientPcs.Add(pc);
            await _context.SaveChangesAsync();
            return pc;
        }

        existingByMac.Hostname = pc.Hostname;
        existingByMac.MachineIdentifier = pc.MachineIdentifier;
        existingByMac.LastOnline = pc.LastOnline;
        existingByMac.FreeDiskSpace = pc.FreeDiskSpace;

        // Update components
        if (pc.Components != null && pc.Components.Count > 0)
        {
            existingByMac.Components.Clear();
            existingByMac.Components.AddRange(pc.Components);
        }

        if (pc.CustomDataPoints != null)
            existingByMac.CustomDataPoints = pc.CustomDataPoints;

        await _context.SaveChangesAsync();
        return existingByMac;
    }
}
