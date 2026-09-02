using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
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
public class InventoryController : ControllerBase
{
    private readonly IAssetRepository _assetRepository;
    private readonly IControllerRepository _controllerRepository;
    private readonly ICacheService _cache;
    private readonly IDbContextFactory<AppDbContext>? _dbContextFactory;

    public InventoryController(
        IAssetRepository assetRepository,
        IControllerRepository controllerRepository,
        ICacheService cache,
        IDbContextFactory<AppDbContext>? dbContextFactory = null)
    {
        _assetRepository = assetRepository;
        _controllerRepository = controllerRepository;
        _cache = cache;
        _dbContextFactory = dbContextFactory;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BaseInventoryItem>>> GetInventory()
    {
        var tree = await _cache.GetOrSetAsync("inventory:tree", () => _assetRepository.GetInventoryTreeAsync(), TimeSpan.FromMinutes(15));
        return Ok(tree);
    }

    [HttpGet("keys")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableKeys()
    {
        var keys = await _cache.GetOrSetAsync("inventory:metadata_keys", () => _assetRepository.GetSearchKeysAsync(), TimeSpan.FromHours(1));
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
                item = JsonSerializer.Deserialize<Machine>(rawItem.GetRawText(), options);
                break;
            default:
                return BadRequest($"Unsupported itemType: {itemType}");
        }

        if (item == null) return BadRequest("Invalid payload.");

        item.Id = Guid.NewGuid();
        var created = await _assetRepository.CreateAsync(item);
        
        await _cache.RemoveAsync("inventory:tree");
        await _cache.RemoveAsync("inventory:metadata_keys");

        return CreatedAtAction(nameof(GetInventory), new { id = created.Id }, created);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var success = await _assetRepository.DeleteAsync(id);
        if (!success) return NotFound();

        await _cache.RemoveAsync("inventory:tree");
        return NoContent();
    }
}
