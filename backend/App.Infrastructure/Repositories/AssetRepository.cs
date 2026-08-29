using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace App.Infrastructure.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _context;

    public AssetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<BaseInventoryItem>> GetInventoryTreeAsync()
    {
        return await _context.InventoryItems
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.ResponsibleTeams)
            .Include(c => c.Children)
                .ThenInclude(c => c.Children)
                    .ThenInclude(c => c.Children)
                        .ThenInclude(c => c.Children)
                            .ThenInclude(c => c.Children)
            .Where(c => c.ParentId == null)
            .ToListAsync();
    }

    public async Task<BaseInventoryItem?> GetByIdAsync(Guid id)
    {
        return await _context.InventoryItems
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.ResponsibleTeams)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<string>> GetSearchKeysAsync()
    {
        if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var items = await _context.InventoryItems.Where(i => i.Metadata != null).ToListAsync();
            var keys = new HashSet<string>();
            foreach (var item in items)
            {
                if (item.Metadata != null)
                {
                    foreach (var prop in item.Metadata.RootElement.EnumerateObject())
                    {
                        keys.Add(prop.Name);
                    }
                }
            }
            return keys.OrderBy(k => k).ToList();
        }

        try
        {
            return await _context.Database
                .SqlQueryRaw<string>(@"SELECT DISTINCT jsonb_object_keys(metadata) FROM backend.inventory_items WHERE metadata IS NOT NULL AND jsonb_typeof(metadata) = 'object'")
                .ToListAsync();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<List<BaseInventoryItem>> SearchAsync(string? query, int limit)
    {
        var dbQuery = _context.InventoryItems
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.ResponsibleTeams)
            .AsQueryable();

        if (string.IsNullOrEmpty(query))
        {
            return await dbQuery.Take(limit).ToListAsync();
        }

        var tags = new Dictionary<string, string>();
        var remainingQuery = query;

        var tagMatches = Regex.Matches(query, @"(\w+):""?([^""\s]+)""?");
        foreach (Match match in tagMatches)
        {
            tags[match.Groups[1].Value] = match.Groups[2].Value;
            remainingQuery = remainingQuery.Replace(match.Value, "").Trim();
        }

        foreach (var tag in tags)
        {
            var key = tag.Key.ToLower();
            var val = tag.Value.ToLower();

            switch (key)
            {
                case "name":
                    dbQuery = dbQuery.Where(c => c.Name.ToLower().Contains(val));
                    break;
                case "displayname":
                    dbQuery = dbQuery.Where(c => c.DisplayName != null && c.DisplayName.ToLower().Contains(val));
                    break;
                case "manufacturer":
                    dbQuery = dbQuery.Where(c => c.Manufacturer != null && c.Manufacturer.Name.ToLower().Contains(val));
                    break;
                case "team":
                    dbQuery = dbQuery.Where(c => c.ResponsibleTeams.Any(t => t.Name.ToLower().Contains(val)));
                    break;
                case "type":
                    if (val.StartsWith("stat") || val.StartsWith("mach"))
                    {
                        dbQuery = dbQuery.OfType<Machine>();
                    }
                    else if (val.StartsWith("hard"))
                    {
                        dbQuery = dbQuery.OfType<HardwareComponent>();
                    }
                    else if (val.StartsWith("soft"))
                    {
                        dbQuery = dbQuery.OfType<SoftwareComponent>();
                    }
                    break;
            }
        }

        if (!string.IsNullOrEmpty(remainingQuery))
        {
            var q = remainingQuery.ToLower();
            var relatedIds = await _context.InventoryItems
                .Where(p => p.Name.ToLower().Contains(q) || (p.DisplayName != null && p.DisplayName.ToLower().Contains(q)))
                .SelectMany(p => p.Children.Select(c => c.Id))
                .ToListAsync();

            dbQuery = dbQuery.Where(c =>
                c.Name.ToLower().Contains(q) ||
                (c.DisplayName != null && c.DisplayName.ToLower().Contains(q)) ||
                (c.SerialNumber != null && c.SerialNumber.ToLower().Contains(q)) ||
                (c.Manufacturer != null && c.Manufacturer.Name.ToLower().Contains(q)) ||
                relatedIds.Contains(c.Id)
            );
        }

        return await dbQuery.Take(limit).ToListAsync();
    }

    public async Task<List<ResponsibleTeam>> GetTeamsAsync()
    {
        return await _context.ResponsibleTeams.ToListAsync();
    }

    public async Task<List<Manufacturer>> GetManufacturersAsync()
    {
        return await _context.Manufacturers.OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<List<Supplier>> GetSuppliersAsync()
    {
        return await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<List<Machine>> GetMachinesAsync()
    {
        return await _context.InventoryItems.OfType<Machine>().OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<List<ClientPc>> GetClientPcsAsync()
    {
        return await _context.ClientPcs.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<BaseInventoryItem> CreateAsync(BaseInventoryItem item)
    {
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<Manufacturer> GetOrCreateManufacturerAsync(string nameOrId)
    {
        if (Guid.TryParse(nameOrId, out var id))
        {
            var existing = await _context.Manufacturers.FindAsync(id);
            if (existing != null) return existing;
        }

        var existingByName = await _context.Manufacturers.FirstOrDefaultAsync(m => m.Name.ToLower() == nameOrId.ToLower());
        if (existingByName != null) return existingByName;

        var newManufacturer = new Manufacturer { Id = Guid.NewGuid(), Name = nameOrId };
        _context.Manufacturers.Add(newManufacturer);
        await _context.SaveChangesAsync();
        return newManufacturer;
    }

    public async Task<Supplier> GetOrCreateSupplierAsync(string nameOrId)
    {
        if (Guid.TryParse(nameOrId, out var id))
        {
            var existing = await _context.Suppliers.FindAsync(id);
            if (existing != null) return existing;
        }

        var existingByName = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name.ToLower() == nameOrId.ToLower());
        if (existingByName != null) return existingByName;

        var newSupplier = new Supplier { Id = Guid.NewGuid(), Name = nameOrId };
        _context.Suppliers.Add(newSupplier);
        await _context.SaveChangesAsync();
        return newSupplier;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null) return false;
        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.InventoryItems.CountAsync();
    }

    public async Task<int> GetAuthUsersCountAsync()
    {
        try
        {
            return await _context.AuthUsers.CountAsync();
        }
        catch
        {
            return 0;
        }
    }
}
