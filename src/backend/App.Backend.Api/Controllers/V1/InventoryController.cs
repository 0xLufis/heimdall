using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using App.Contracts.Inventory;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for managing the unified inventory components with multi-tier Redis caching.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IAssetRepository _assetRepository;
    private readonly IControllerRepository _controllerRepository;
    private readonly ICacheService _cache;

    public InventoryController(
        IAssetRepository assetRepository,
        IControllerRepository controllerRepository,
        ICacheService cache,
        IDbContextFactory<AppDbContext>? dbContextFactory = null)
    {
        _assetRepository = assetRepository;
        _controllerRepository = controllerRepository;
        _cache = cache;
    }


    /// <summary>
    /// Inventory rows are tenant-filtered, so their cached results are kept per tenant scope.
    /// With shared keys one tenant was served another tenant's (or a platform administrator's
    /// unfiltered) cached list.
    /// </summary>
    private string Scoped(string key) => App.Backend.Api.Security.TenantScopeMiddleware.ScopedCacheKey(HttpContext, key);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BaseInventoryItem>>> GetInventory()
    {
        var tree = await _cache.GetOrSetAsync(Scoped("inventory:tree"), () => _assetRepository.GetInventoryTreeAsync(), TimeSpan.FromMinutes(10));
        return Ok(tree);
    }

    [HttpGet("machines")]
    public async Task<ActionResult<IEnumerable<Machine>>> GetMachines()
    {
        var machines = await _cache.GetOrSetAsync(Scoped("inventory:machines"), () => _assetRepository.GetMachinesAsync(), TimeSpan.FromMinutes(10));
        return Ok(machines);
    }

    [HttpGet("teams")]
    public async Task<ActionResult<IEnumerable<ResponsibleTeam>>> GetTeams()
    {
        var teams = await _cache.GetOrSetAsync("inventory:teams", () => _assetRepository.GetTeamsAsync(), TimeSpan.FromHours(1));
        return Ok(teams);
    }

    [HttpGet("manufacturers")]
    public async Task<ActionResult<IEnumerable<Manufacturer>>> GetManufacturers()
    {
        var manufacturers = await _cache.GetOrSetAsync("inventory:manufacturers", () => _assetRepository.GetManufacturersAsync(), TimeSpan.FromHours(1));
        return Ok(manufacturers);
    }

    [HttpGet("suppliers")]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
    {
        var suppliers = await _cache.GetOrSetAsync("inventory:suppliers", () => _assetRepository.GetSuppliersAsync(), TimeSpan.FromHours(1));
        return Ok(suppliers);
    }

    [HttpGet("client-pcs")]
    public async Task<ActionResult<IEnumerable<ClientPc>>> GetClientPcs()
    {
        var pcs = await _cache.GetOrSetAsync(Scoped("inventory:client_pcs"), () => _assetRepository.GetClientPcsAsync(), TimeSpan.FromMinutes(2));
        return Ok(pcs);
    }

    [HttpGet("keys")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableKeys()
    {
        var keys = await _cache.GetOrSetAsync(Scoped("inventory:metadata_keys"), () => _assetRepository.GetSearchKeysAsync(), TimeSpan.FromHours(1));
        return Ok(keys);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<SearchResultDto>>> Search([FromQuery] string query)
    {
        var items = await _assetRepository.SearchAsync(query, 50);
        var results = items.Select(i => new SearchResultDto
        {
            Id = i.Id,
            Name = i.Name,
            DisplayName = i.DisplayName,
            ItemType = i.GetType().Name,
            TypeLabel = i.GetType().Name,
            ManufacturerName = i.Manufacturer?.Name
        }).ToList();

        return Ok(results);
    }

    [HttpGet("parts")]
    public async Task<ActionResult<IEnumerable<BaseInventoryItem>>> GetParts()
    {
        var parts = await _cache.GetOrSetAsync(Scoped("inventory:parts"), () => _assetRepository.GetPartsAsync(), TimeSpan.FromMinutes(10));
        return Ok(parts);
    }

    [HttpGet("stock")]
    public async Task<ActionResult<IEnumerable<BaseInventoryItem>>> GetStock()
    {
        var stock = await _cache.GetOrSetAsync(Scoped("inventory:stock"), () => _assetRepository.GetStockAsync(), TimeSpan.FromMinutes(10));
        return Ok(stock);
    }

    [HttpGet("station-tree/{id}")]
    public async Task<ActionResult<StationComponentTreeDto>> GetStationTree(string id)
    {
        var tree = await _assetRepository.GetStationComponentTreeAsync(id);
        if (tree == null) return NotFound();
        return Ok(tree);
    }

    [NonAction]
    public Task<ActionResult<StationComponentTreeDto>> GetStationTree(Guid id)
        => GetStationTree(id.ToString());

    /// <summary>
    /// Server-side filtered, sorted, paginated inventory search with global KPIs.
    /// </summary>
    [HttpGet("filter")]
    public async Task<ActionResult<InventoryFilterResultDto>> FilterInventoryGet([FromQuery] InventoryFilterRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = $"inventory:filter:{request.Query}:{request.Type}:{request.Classification}:{request.EquipmentStatus}:{request.Technology}:{request.StorageLocation}:{request.ManufacturerId}:{request.TeamId}:{request.SortBy}:{request.SortOrder}:{request.Page}:{request.PageSize}";
        var result = await _cache.GetOrSetAsync(Scoped(cacheKey), () => _assetRepository.FilterInventoryAsync(request, cancellationToken), TimeSpan.FromSeconds(30));
        return Ok(result);
    }

    /// <summary>
    /// Server-side filtered, sorted, paginated inventory search with global KPIs (POST payload support).
    /// </summary>
    [HttpPost("filter")]
    public async Task<ActionResult<InventoryFilterResultDto>> FilterInventoryPost([FromBody] InventoryFilterRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = $"inventory:filter:{request.Query}:{request.Type}:{request.Classification}:{request.EquipmentStatus}:{request.Technology}:{request.StorageLocation}:{request.ManufacturerId}:{request.TeamId}:{request.SortBy}:{request.SortOrder}:{request.Page}:{request.PageSize}";
        var result = await _cache.GetOrSetAsync(Scoped(cacheKey), () => _assetRepository.FilterInventoryAsync(request, cancellationToken), TimeSpan.FromSeconds(30));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<BaseInventoryItem>> CreateItem([FromBody] JsonElement rawItem)
    {
        if (!rawItem.TryGetProperty("itemType", out var typeProp))
        {
            return BadRequest("Missing itemType field.");
        }

        string itemType = typeProp.GetString() ?? "";
        BaseInventoryItem? item = null;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        switch (itemType)
        {
            case nameof(HardwareComponent):
                item = JsonSerializer.Deserialize<HardwareComponent>(rawItem.GetRawText(), options);
                break;
            case nameof(SoftwareAsset):
                item = JsonSerializer.Deserialize<SoftwareAsset>(rawItem.GetRawText(), options);
                break;
            case nameof(SoftwareComponent):
                item = JsonSerializer.Deserialize<SoftwareComponent>(rawItem.GetRawText(), options);
                break;
            case nameof(PcHardware):
                item = JsonSerializer.Deserialize<PcHardware>(rawItem.GetRawText(), options);
                break;
            case nameof(Machine):
                return BadRequest("Machines are managed via /api/v1/Machine endpoints.");
            default:
                return BadRequest($"Unsupported itemType: {itemType}");
        }

        if (item == null) return BadRequest("Invalid payload.");

        item.Id = Guid.NewGuid();
        var created = await _assetRepository.CreateAsync(item);
        
        await _cache.RemoveByPatternAsync("inventory:*");

        return CreatedAtAction(nameof(GetInventory), new { id = created.Id }, created);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BaseInventoryItem>> GetItem(Guid id)
    {
        var item = await _cache.GetOrSetAsync(Scoped($"inventory:item:{id}"), () => _assetRepository.GetByIdAsync(id), TimeSpan.FromMinutes(5));
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPut("{id}")]
    [HttpPut("components/{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<BaseInventoryItem>> UpdateItem(Guid id, [FromBody] JsonElement rawItem)
    {
        var existing = await _assetRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        if (rawItem.TryGetProperty("name", out var nameProp) && nameProp.GetString() is string name && !string.IsNullOrWhiteSpace(name))
        {
            existing.Name = name;
        }
        if (rawItem.TryGetProperty("displayName", out var dispProp))
        {
            existing.DisplayName = dispProp.GetString();
        }
        if ((rawItem.TryGetProperty("quantity", out var qtyProp) || rawItem.TryGetProperty("stockQuantity", out qtyProp)) && qtyProp.TryGetInt32(out var qty))
        {
            existing.StockQuantity = qty;
        }
        if (rawItem.TryGetProperty("storageLocation", out var locProp))
        {
            existing.StorageLocation = locProp.GetString();
        }
        if (rawItem.TryGetProperty("serialNumber", out var snProp))
        {
            existing.SerialNumber = snProp.GetString();
        }
        if (rawItem.TryGetProperty("technology", out var techProp))
        {
            existing.Technology = techProp.GetString();
        }

        await _assetRepository.UpdateAsync(existing);
        await _cache.RemoveByPatternAsync("inventory:*");

        return Ok(existing);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var success = await _assetRepository.DeleteAsync(id);
        if (!success) return NotFound();

        await _cache.RemoveByPatternAsync("inventory:*");
        return NoContent();
    }
}
