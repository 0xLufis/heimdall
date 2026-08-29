using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing the unified inventory components using repository interfaces.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "admin")]
public class InventoryController : ControllerBase
{
    private readonly IAssetRepository _assetRepository;
    private readonly IControllerRepository _controllerRepository;

    public InventoryController(IAssetRepository assetRepository, IControllerRepository controllerRepository)
    {
        _assetRepository = assetRepository;
        _controllerRepository = controllerRepository;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<BaseInventoryItem>>> GetInventory()
    {
        var tree = await _assetRepository.GetInventoryTreeAsync();
        return Ok(tree);
    }

    [HttpGet("keys")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> GetSearchKeys()
    {
        var coreKeys = new 
        { 
            Group = "Core Attributes", 
            Keys = new List<string> { "Name", "DisplayName", "SerialNumber", "Manufacturer", "Supplier", "Cost", "Team", "Type" } 
        };

        var metadataKeys = await _assetRepository.GetSearchKeysAsync();
        var result = new List<object> { coreKeys };
        
        if (metadataKeys.Any()) 
            result.Add(new { Group = "Custom Metadata", Keys = metadataKeys.OrderBy(k => k).ToList() });

        return Ok(result);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.SearchResultDto>>> Search(
        [FromQuery] string? query, 
        [FromQuery] int limit = 20)
    {
        var items = await _assetRepository.SearchAsync(query, limit);
        var pcs = await _controllerRepository.GetAllAsync();

        var itemResults = items.Select(c => new App.Backend.Api.Dtos.SearchResultDto
        {
            Id = c.Id,
            Name = c.Name,
            DisplayName = c.DisplayName,
            ItemType = c.GetType().Name,
            TypeLabel = c is Machine ? "Production Station" : 
                        c is HardwareComponent ? "Hardware Asset" :
                        c is SoftwareComponent ? "Software/License" : "Inventory Item",
            ManufacturerName = c.Manufacturer != null ? c.Manufacturer.Name : null
        }).ToList();

        var pcResults = pcs.Take(limit).Select(c => new App.Backend.Api.Dtos.SearchResultDto
        {
            Id = c.Id,
            Name = c.Hostname ?? c.Name,
            DisplayName = c.Hostname,
            ItemType = "ClientPc",
            TypeLabel = "Edge PC",
            ManufacturerName = "Industrial PC"
        }).ToList();

        var combined = itemResults.Concat(pcResults).OrderBy(r => r.Name).Take(limit).ToList();
        return Ok(combined);
    }

    [HttpGet("teams")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ResponsibleTeam>>> GetTeams()
    {
        var teams = await _assetRepository.GetTeamsAsync();
        return Ok(teams);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<BaseInventoryItem>> GetById(Guid id)
    {
        var item = await _assetRepository.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("manufacturers")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Manufacturer>>> GetManufacturers()
    {
        var manufacturers = await _assetRepository.GetManufacturersAsync();
        return Ok(manufacturers);
    }

    [HttpGet("suppliers")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
    {
        var suppliers = await _assetRepository.GetSuppliersAsync();
        return Ok(suppliers);
    }

    [HttpGet("machines")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Machine>>> GetMachines()
    {
        var machines = await _assetRepository.GetMachinesAsync();
        return Ok(machines);
    }

    [HttpGet("client-pcs")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ClientPc>>> GetClientPcs()
    {
        var pcs = await _controllerRepository.GetAllAsync();
        return Ok(pcs);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<BaseInventoryItem>> Create([FromBody] JsonElement payload)
    {
        var itemType = payload.TryGetProperty("itemType", out var it) ? it.GetString() : "HardwareComponent";
        BaseInventoryItem item;

        if (itemType != null && itemType.Equals("SoftwareComponent", StringComparison.OrdinalIgnoreCase))
        {
            item = new SoftwareComponent();
        }
        else if (itemType != null && itemType.Equals("Machine", StringComparison.OrdinalIgnoreCase))
        {
            item = new Machine { CustomIdentifier = payload.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "" };
        }
        else
        {
            item = new HardwareComponent();
        }

        item.Id = Guid.NewGuid();
        item.Name = payload.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
        item.DisplayName = payload.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
        item.SerialNumber = payload.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null;
        
        if (payload.TryGetProperty("costInHUF", out var cost) && cost.ValueKind == JsonValueKind.Number)
        {
            item.CostInHUF = cost.GetDecimal();
        }

        if (payload.TryGetProperty("manufacturerId", out var mIdProperty)) 
        {
            var mIdStr = mIdProperty.GetString();
            if (!string.IsNullOrEmpty(mIdStr))
            {
                var manufacturer = await _assetRepository.GetOrCreateManufacturerAsync(mIdStr);
                item.ManufacturerId = manufacturer.Id;
            }
        }

        if (payload.TryGetProperty("supplierId", out var sIdProperty)) 
        {
            var sIdStr = sIdProperty.GetString();
            if (!string.IsNullOrEmpty(sIdStr))
            {
                var supplier = await _assetRepository.GetOrCreateSupplierAsync(sIdStr);
                item.SupplierId = supplier.Id;
            }
        }

        if (payload.TryGetProperty("clientPcId", out var pcIdProperty)) 
        {
            var pcIdStr = pcIdProperty.GetString();
            if (Guid.TryParse(pcIdStr, out var clientPcId)) 
            {
                item.ClientPcId = clientPcId;
            }
        }

        if (payload.TryGetProperty("parentId", out var parentIdProperty)) 
        {
            var pIdStr = parentIdProperty.GetString();
            if (Guid.TryParse(pIdStr, out var pId)) 
            {
                item.ParentId = pId;
            }
        }

        if (payload.TryGetProperty("data", out var data))
        {
            item.Metadata = JsonDocument.Parse(data.GetRawText());
        }

        var created = await _assetRepository.CreateAsync(item);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _assetRepository.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
