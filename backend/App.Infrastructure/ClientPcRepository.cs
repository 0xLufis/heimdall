using System.Text.Json;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

//Vibe coded examples to see have some scaffolding
//TODO: READ AND FIX THIS, ALL OF THIS


namespace App.Infrastructure.Repositories;

public class ClientPcRepository
{
    private readonly AppDbContext _context;

    public ClientPcRepository(AppDbContext context)
    {
        _context = context;
    }

    // --- 1. Basic CRUD ---
    public async Task<ClientPc> CreateAsync(ClientPc pc)
    {
        _context.ClientPcs.Add(pc);
        await _context.SaveChangesAsync();
        return pc;
    }

    public async Task<ClientPc?> GetByIdAsync(Guid id)
    {
        return await _context.ClientPcs.FindAsync(id);
    }

    public async Task<List<ClientPc>> GetAllAsync()
    {
        return await _context.ClientPcs.ToListAsync();
    }

    public async Task<ClientPc> UpsertByMacAddressAsync(ClientPc pc)
    {
        // 1. Check if we already have this specific PC by MAC
        var existingByMac = await _context.ClientPcs
            .FirstOrDefaultAsync(x => x.MacAddress == pc.MacAddress);

        // 2. Check if the hostname is taken by ANOTHER PC
        var existingByHostname = await _context.ClientPcs
            .FirstOrDefaultAsync(x => x.Hostname == pc.Hostname && x.MacAddress != pc.MacAddress);

        // 3. Resolve hostname conflict if it exists
        if (existingByHostname != null)
        {
            // The hostname is taken by a different MAC address. 
            // This happens when a PC is replaced but keeps the same name, or when an agent is re-imaged with a new MAC.
            // We'll rename the old record to free up the hostname for the new one.
            existingByHostname.Hostname = $"{existingByHostname.Hostname}-OLD-{DateTime.UtcNow:yyyyMMddHHmmss}";
            // We don't save yet, let the next SaveChanges handle it in one transaction if possible,
            // or save now to ensure the unique index is freed.
            await _context.SaveChangesAsync();
        }

        if (existingByMac == null)
        {
            _context.ClientPcs.Add(pc);
            await _context.SaveChangesAsync();
            return pc;
        }

        // 4. Update existing record by MAC
        existingByMac.Hostname = pc.Hostname;
        existingByMac.MachineIdentifier = pc.MachineIdentifier;
        existingByMac.LastOnline = pc.LastOnline;
        existingByMac.HardwareConfig = pc.HardwareConfig;
        existingByMac.SoftwareConfig = pc.SoftwareConfig;
        
        if (pc.CustomDataPoints != null)
            existingByMac.CustomDataPoints = pc.CustomDataPoints;

        await _context.SaveChangesAsync();
        return existingByMac;
    }

    // --- 2. Querying Strongly-Typed JSONB (HardwareConfig) ---
    public async Task<List<ClientPc>> GetPcsByCpuAsync(string cpuModel)
    {
        // MAGIC: Npgsql translates this C# property access into a Postgres JSONB path query!
        // SQL generated: SELECT * FROM client_pcs WHERE hardware_config ->> 'Cpu' LIKE '%Intel%'
        return await _context.ClientPcs
            .Where(pc => pc.HardwareConfig.Cpu.Contains(cpuModel))
            .ToListAsync();
    }

    // --- 3. Querying Raw JsonDocument (CustomDataPoints) ---
    public async Task<List<ClientPc>> GetPcsByCustomFlagAsync(string flagKey, string expectedValue)
    {
        // For dynamic raw JSON, we use Npgsql's JsonContains function.
        // This checks if the JSONB column contains the specific key/value pair.
        string searchJson = $"{{\"{flagKey}\": \"{expectedValue}\"}}";

        return await _context.ClientPcs
            .Where(pc => EF.Functions.JsonContains(pc.CustomDataPoints, searchJson))
            .ToListAsync();
    }

    // --- 4. Updating Raw JsonDocument Safely ---
    public async Task UpdateCustomDataPointAsync(Guid pcId, string key, string value)
    {
        var pc = await _context.ClientPcs.FindAsync(pcId);
        if (pc == null) return;

        // 1. Parse existing JSON into a mutable dictionary (or create new if null)
        var dataPoints = pc.CustomDataPoints != null
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(pc.CustomDataPoints.RootElement.GetRawText())
            : new Dictionary<string, object>();

        // 2. Add or update the dynamic flag
        dataPoints![key] = value;

        // 3. Serialize it back into a new JsonDocument
        pc.CustomDataPoints = JsonSerializer.SerializeToDocument(dataPoints);

        // 4. Important: Explicitly tell EF Core the JSON property changed.
        // EF Core struggles to track mutations inside dynamic JSON objects automatically.
        _context.Entry(pc).Property(x => x.CustomDataPoints).IsModified = true;

        await _context.SaveChangesAsync();
    }
}
