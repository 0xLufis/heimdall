using App.Contracts.Inventory;
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
            .AsNoTracking()
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.ResponsibleTeams)
            .Include(c => c.Children)
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
            var entityType = _context.Model.FindEntityType(typeof(BaseInventoryItem));
            var schema = entityType?.GetSchema() ?? "backend";
            var tableName = entityType?.GetTableName() ?? "inventory_items";
            var fullTableName = string.IsNullOrEmpty(schema) ? $"\"{tableName}\"" : $"\"{schema}\".\"{tableName}\"";

            var sql = $"SELECT DISTINCT jsonb_object_keys(metadata) FROM {fullTableName} WHERE metadata IS NOT NULL AND jsonb_typeof(metadata) = 'object'";
            return await _context.Database
                .SqlQueryRaw<string>(sql)
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
                case "status":
                    dbQuery = dbQuery.Where(c => c.EquipmentStatus.ToLower() == val);
                    break;
                case "isstock":
                    bool isStock = val == "true" || val == "1" || val == "yes";
                    dbQuery = dbQuery.Where(c => c.IsStockItem == isStock);
                    break;
                case "tech":
                case "technology":
                    dbQuery = dbQuery.Where(c => c.Technology != null && c.Technology.ToLower().Contains(val));
                    break;
                case "location":
                case "shelf":
                    dbQuery = dbQuery.Where(c => c.StorageLocation != null && c.StorageLocation.ToLower().Contains(val));
                    break;
                case "serial":
                case "serialnumber":
                    dbQuery = dbQuery.Where(c => c.SerialNumber != null && c.SerialNumber.ToLower().Contains(val));
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

    public async Task<List<BaseInventoryItem>> GetPartsAsync()
    {
        return await _context.InventoryItems
            .AsNoTracking()
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.Machine)
            .Include(c => c.ClientPc)
            .Where(c => !c.IsStockItem)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<BaseInventoryItem>> GetStockAsync()
    {
        return await _context.InventoryItems
            .AsNoTracking()
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Where(c => c.IsStockItem)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<StationComponentTreeDto?> GetStationComponentTreeAsync(Guid stationId)
    {
        var station = await _context.InventoryItems
            .OfType<Machine>()
            .Include(m => m.Manufacturer)
            .Include(m => m.ResponsibleTeams)
            .Include(m => m.Controllers)
                .ThenInclude(c => c.InventoryItems)
            .FirstOrDefaultAsync(m => m.Id == stationId);

        if (station == null) return null;

        var installedParts = await _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Manufacturer)
            .Where(i => i.MachineId == stationId)
            .Select(i => new StationInstalledPartDto
            {
                Id = i.Id,
                Name = i.Name,
                DisplayName = i.DisplayName,
                SerialNumber = i.SerialNumber,
                EquipmentStatus = i.EquipmentStatus,
                Technology = i.Technology,
                StorageLocation = i.StorageLocation,
                ItemType = i.ItemType,
                ManufacturerName = i.Manufacturer != null ? i.Manufacturer.Name : null
            })
            .ToListAsync();

        var controllers = station.Controllers.Select(c => new StationControllerNodeDto
        {
            Id = c.Id,
            Name = c.Name,
            Hostname = c.Hostname,
            IpAddress = c.IpAddress,
            MacAddress = c.MacAddress,
            VlanId = c.VlanId,
            PinnedObjectHandle = c.PinnedObjectHandle,
            LastOnline = c.LastOnline,
            InternalComponents = c.InventoryItems.Select(item => new StationInternalComponentDto
            {
                Id = item.Id,
                Name = item.Name,
                DisplayName = item.DisplayName,
                SerialNumber = item.SerialNumber,
                ItemType = item.ItemType,
                Technology = item.Technology,
                ParentId = item.ParentId,
                Metadata = item.Metadata,
                IsSigned = item.Metadata != null && item.Metadata.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && item.Metadata.RootElement.TryGetProperty("IsSigned", out var isSig) && isSig.ValueKind == System.Text.Json.JsonValueKind.True,
                IsSandboxed = item.Metadata != null && item.Metadata.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && item.Metadata.RootElement.TryGetProperty("IsSandboxed", out var isSb) && isSb.ValueKind == System.Text.Json.JsonValueKind.True,
                CustomData = item.Metadata != null && item.Metadata.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && item.Metadata.RootElement.TryGetProperty("Data", out var dt) ? dt : (object?)null
            }).ToList()
        }).ToList();

        return new StationComponentTreeDto
        {
            StationId = station.Id,
            StationName = station.Name,
            DisplayName = station.DisplayName,
            CustomIdentifier = station.CustomIdentifier,
            MachineType = station.MachineType,
            GroupId = station.GroupId,
            Controllers = controllers,
            InstalledParts = installedParts,
            TotalComponentsCount = installedParts.Count + controllers.Sum(ctrl => ctrl.InternalComponents.Count)
        };
    }

    public async Task<InventoryFilterResultDto> FilterInventoryAsync(InventoryFilterRequest request, CancellationToken cancellationToken = default)
    {
        var allItemsQuery = _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Manufacturer)
            .Include(i => i.ResponsibleTeams)
            .AsQueryable();

        // Compute KPIs across complete inventory
        var totalGlobalCount = await allItemsQuery.CountAsync(cancellationToken);
        var totalGlobalHardware = await allItemsQuery.CountAsync(i => i is HardwareComponent, cancellationToken);
        var totalGlobalSoftware = await allItemsQuery.CountAsync(i => i is SoftwareComponent || i is SoftwareAsset, cancellationToken);
        var totalGlobalParts = await allItemsQuery.CountAsync(i => i.MachineId != null, cancellationToken);
        var totalGlobalStock = await allItemsQuery.CountAsync(i => i.IsStockItem, cancellationToken);
        var totalGlobalCost = await allItemsQuery.SumAsync(i => (double?)i.CostInHUF ?? 0.0, cancellationToken);

        var inStorage = await allItemsQuery.CountAsync(i => i.EquipmentStatus == "InStorage", cancellationToken);
        var inMachine = await allItemsQuery.CountAsync(i => i.EquipmentStatus == "InMachine", cancellationToken);
        var underRepair = await allItemsQuery.CountAsync(i => i.EquipmentStatus == "UnderRepair", cancellationToken);
        var decommissioned = await allItemsQuery.CountAsync(i => i.EquipmentStatus == "Decommissioned", cancellationToken);
        var lowStockCount = await allItemsQuery.CountAsync(i => i.IsStockItem && (i.StockQuantity ?? 0) <= (i.MinStockThreshold ?? 0), cancellationToken);

        var kpis = new InventoryKpisDto
        {
            TotalGlobalCount = totalGlobalCount,
            TotalGlobalHardware = totalGlobalHardware,
            TotalGlobalSoftware = totalGlobalSoftware,
            TotalGlobalParts = totalGlobalParts,
            TotalGlobalStock = totalGlobalStock,
            TotalGlobalCost = totalGlobalCost,
            InStorage = inStorage,
            InMachine = inMachine,
            UnderRepair = underRepair,
            Decommissioned = decommissioned,
            LowStockCount = lowStockCount
        };

        // Filter filtered query
        var query = allItemsQuery;

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLower();
            query = query.Where(i =>
                (i.Name != null && i.Name.ToLower().Contains(term)) ||
                (i.DisplayName != null && i.DisplayName.ToLower().Contains(term)) ||
                (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(term)) ||
                (i.StorageLocation != null && i.StorageLocation.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Type) && !string.Equals(request.Type, "all", StringComparison.OrdinalIgnoreCase))
        {
            var typeLower = request.Type.ToLowerInvariant();
            if (typeLower is "hardware" or "hardwarecomponent")
            {
                query = query.Where(i => i is HardwareComponent);
            }
            else if (typeLower is "software" or "softwarecomponent" or "softwareasset")
            {
                query = query.Where(i => i is SoftwareComponent || i is SoftwareAsset);
            }
            else if (typeLower is "machine" or "station")
            {
                query = query.Where(i => i is Machine);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.EquipmentStatus) && !string.Equals(request.EquipmentStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.EquipmentStatus == request.EquipmentStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.Technology) && !string.Equals(request.Technology, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.Technology == request.Technology);
        }

        if (!string.IsNullOrWhiteSpace(request.StorageLocation) && !string.Equals(request.StorageLocation, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.StorageLocation == request.StorageLocation);
        }

        if (!string.IsNullOrWhiteSpace(request.ManufacturerId))
        {
            if (Guid.TryParse(request.ManufacturerId, out var mId))
            {
                query = query.Where(i => i.ManufacturerId == mId);
            }
            else
            {
                query = query.Where(i => i.Manufacturer != null && i.Manufacturer.Name.ToLower() == request.ManufacturerId.ToLower());
            }
        }

        if (!string.IsNullOrWhiteSpace(request.TeamId))
        {
            if (Guid.TryParse(request.TeamId, out var tId))
            {
                query = query.Where(i => i.ResponsibleTeams.Any(t => t.Id == tId));
            }
            else
            {
                query = query.Where(i => i.ResponsibleTeams.Any(t => t.Name.ToLower() == request.TeamId.ToLower()));
            }
        }

        // Sorting
        bool desc = string.Equals(request.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = (request.SortBy?.ToLowerInvariant()) switch
        {
            "displayname" => desc ? query.OrderByDescending(i => i.DisplayName) : query.OrderBy(i => i.DisplayName),
            "serialnumber" => desc ? query.OrderByDescending(i => i.SerialNumber) : query.OrderBy(i => i.SerialNumber),
            "equipmentstatus" => desc ? query.OrderByDescending(i => i.EquipmentStatus) : query.OrderBy(i => i.EquipmentStatus),
            "cost" or "costinhuf" => desc ? query.OrderByDescending(i => i.CostInHUF) : query.OrderBy(i => i.CostInHUF),
            _ => desc ? query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name)
        };

        var filteredCount = await query.CountAsync(cancellationToken);
        var totalCostHuf = await query.SumAsync(i => (double?)i.CostInHUF ?? 0.0, cancellationToken);

        int page = request.Page > 0 ? request.Page : 1;
        int pageSize = request.PageSize > 0 ? request.PageSize : 50;
        int totalPages = (int)Math.Ceiling((double)filteredCount / pageSize);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(i => new InventoryItemSummaryDto
            {
                Id = i.Id,
                Name = i.Name,
                DisplayName = i.DisplayName,
                SerialNumber = i.SerialNumber,
                ItemType = i.ItemType,
                CustomIdentifier = (i as Machine)?.CustomIdentifier,
                CostInHUF = (double?)i.CostInHUF,
                PurchaseDate = i.PurchaseDate,
                Manufacturer = i.Manufacturer != null ? new InventoryRelationSummaryDto { Id = i.Manufacturer.Id.ToString(), Name = i.Manufacturer.Name } : null,
                ResponsibleTeams = i.ResponsibleTeams.Select(t => new InventoryRelationSummaryDto { Id = t.Id.ToString(), Name = t.Name }).ToList(),
                IsStockItem = i.IsStockItem,
                EquipmentStatus = i.EquipmentStatus,
                StorageLocation = i.StorageLocation,
                StockQuantity = i.StockQuantity,
                MinStockThreshold = i.MinStockThreshold,
                Technology = i.Technology,
                Metadata = i.Metadata,
                MachineId = i.MachineId
            })
            .ToList();

        return new InventoryFilterResultDto
        {
            Items = items,
            TotalCount = filteredCount,
            TotalPages = totalPages,
            Page = page,
            PageSize = pageSize,
            TotalCostHuf = totalCostHuf,
            Kpis = kpis
        };
    }
}
