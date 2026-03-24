using System.Text.Json;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql; // Add this using directive for NpgsqlJsonDbFunctionsExtensions

namespace App.Infrastructure.Repositories;

/// <summary>
/// Repository for managing <see cref="ClientPc"/> entities.
/// Provides data access operations including CRUD, upsert by MAC address,
/// and querying/updating strongly-typed and raw JSONB fields.
/// </summary>
public class ClientPcRepository
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientPcRepository"/> class.
    /// </summary>
    /// <param name="context">The application's database context.</param>
    public ClientPcRepository(AppDbContext context)
    {
        _context = context;
    }

    // --- 1. Basic CRUD ---

    /// <summary>
    /// Creates a new <see cref="ClientPc"/> in the database.
    /// </summary>
    /// <param name="pc">The <see cref="ClientPc"/> entity to add.</param>
    /// <returns>The created <see cref="ClientPc"/> entity with its assigned ID.</returns>
    public async Task<ClientPc> CreateAsync(ClientPc pc)
    {
        _context.ClientPcs.Add(pc);
        await _context.SaveChangesAsync();
        return pc;
    }

    /// <summary>
    /// Retrieves a <see cref="ClientPc"/> by its unique identifier.
    /// </summary>
    /// <param name="id">The <see cref="Guid"/> of the Client PC to retrieve.</param>
    /// <returns>The <see cref="ClientPc"/> entity if found, otherwise <c>null</c>.</returns>
    public async Task<ClientPc?> GetByIdAsync(Guid id)
    {
        return await _context.ClientPcs.FindAsync(id);
    }

    /// <summary>
    /// Retrieves all <see cref="ClientPc"/> entities from the database.
    /// </summary>
    /// <returns>A list of all <see cref="ClientPc"/> entities.</returns>
    public async Task<List<ClientPc>> GetAllAsync()
    {
        return await _context.ClientPcs.ToListAsync();
    }

    /// <summary>
    /// Inserts a new <see cref="ClientPc"/> or updates an existing one based on its MAC address.
    /// Handles potential hostname conflicts by renaming old records if a new PC uses an existing hostname.
    /// </summary>
    /// <param name="pc">The <see cref="ClientPc"/> entity to upsert.</param>
    /// <returns>The upserted <see cref="ClientPc"/> entity.</returns>
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

    /// <summary>
    /// Retrieves a list of <see cref="ClientPc"/> entities filtered by a specific CPU model in their HardwareConfig.
    /// This leverages Npgsql's JSONB querying capabilities.
    /// </summary>
    /// <param name="cpuModel">The CPU model to search for (partial match).</param>
    /// <returns>A list of <see cref="ClientPc"/> entities matching the CPU model.</returns>
    public async Task<List<ClientPc>> GetPcsByCpuAsync(string cpuModel)
    {
        // MAGIC: Npgsql translates this C# property access into a Postgres JSONB path query!
        // SQL generated: SELECT * FROM client_pcs WHERE hardware_config ->> 'Cpu' LIKE '%Intel%'
        return await _context.ClientPcs
            .Where(pc => pc.HardwareConfig != null && pc.HardwareConfig.Cpu.Contains(cpuModel))
            .ToListAsync();
    }

    // --- 3. Querying Raw JsonDocument (CustomDataPoints) ---

    /// <summary>
    /// Retrieves a list of <see cref="ClientPc"/> entities that contain a specific key-value pair
    /// within their dynamic <see cref="ClientPc.CustomDataPoints"/> JSONB field.
    /// </summary>
    /// <param name="flagKey">The key of the custom data point.</param>
    /// <param name="expectedValue">The expected string value of the custom data point.</param>
    /// <returns>A list of <see cref="ClientPc"/> entities matching the custom data point.</returns>
    public async Task<List<ClientPc>> GetPcsByCustomFlagAsync(string flagKey, string expectedValue)
    {
        // For dynamic raw JSON, we use Npgsql's JsonContains function.
        // This checks if the JSONB column contains the specific key/value pair.
        // string searchJson = $"{{"{flagKey}": "{expectedValue}"}";
        string searchJson = $"{{flagkey}}: {{expectedValue}}";

        return await _context.ClientPcs
            .Where(pc => pc.CustomDataPoints != null && NpgsqlJsonDbFunctionsExtensions.JsonContains(EF.Functions, pc.CustomDataPoints, searchJson))
            .ToListAsync();
    }

    // --- 4. Updating Raw JsonDocument Safely ---

    /// <summary>
    /// Updates a specific key-value pair within a <see cref="ClientPc"/>'s dynamic
    /// <see cref="ClientPc.CustomDataPoints"/> JSONB field.
    /// </summary>
    /// <param name="pcId">The unique identifier of the Client PC to update.</param>
    /// <param name="key">The key of the custom data point to update.</param>
    /// <param name="value">The new string value for the custom data point.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
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
