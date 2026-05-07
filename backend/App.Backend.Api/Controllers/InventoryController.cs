using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing the unified inventory components using TPT hierarchy.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "admin")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public InventoryController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all top-level inventory items with their full tree structure.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<BaseInventoryItem>>> GetInventory()
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

    /// <summary>
    /// Retrieves all unique keys found in metadata for suggestions.
    /// </summary>
    [HttpGet("keys")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> GetSearchKeys()
    {
        var coreKeys = new 
        { 
            Group = "Core Attributes", 
            Keys = new List<string> { "Name", "DisplayName", "SerialNumber", "Manufacturer", "Supplier", "Cost", "Team", "Type" } 
        };

        // Keys found in metadata JSONB
        var metadataKeys = await _context.Database
            .SqlQueryRaw<string>(@"SELECT DISTINCT jsonb_object_keys(metadata) FROM backend.inventory_items WHERE metadata IS NOT NULL AND jsonb_typeof(metadata) = 'object'")
            .ToListAsync();

        var result = new List<object> { coreKeys };
        
        if (metadataKeys.Any()) 
            result.Add(new { Group = "Custom Metadata", Keys = metadataKeys.OrderBy(k => k).ToList() });

        return Ok(result);
    }

    /// <summary>
    /// Advanced search for items. Supports tagged search (key:value) and general text search.
    /// </summary>
    /// <param name="query">The search string (supports tags like manufacturer:dell).</param>
    /// <param name="limit">Max number of results to return (default 20).</param>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.SearchResultDto>>> Search(
        [FromQuery] string? query, 
        [FromQuery] int limit = 20)
    {
        var dbQuery = _context.InventoryItems
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.ResponsibleTeams)
            .AsQueryable();

        var pcQuery = _context.ClientPcs
            .Include(c => c.ResponsibleTeams)
            .AsQueryable();

        if (string.IsNullOrEmpty(query))
        {
            var results = await ProjectToSearchResult(dbQuery.Take(limit / 2));
            var pcResults = await ProjectPcsToSearchResult(pcQuery.Take(limit / 2));
            results.AddRange(pcResults);
            return results;
        }

        // Parse query for tags (key:value)
        var tags = new Dictionary<string, string>();
        var remainingQuery = query;

        var tagMatches = System.Text.RegularExpressions.Regex.Matches(query, @"(\w+):""?([^""\s]+)""?");
        foreach (System.Text.RegularExpressions.Match match in tagMatches)
        {
            tags[match.Groups[1].Value] = match.Groups[2].Value;
            remainingQuery = remainingQuery.Replace(match.Value, "").Trim();
        }

        // Apply tag filters with partial matching
        foreach (var tag in tags)
        {
            var key = tag.Key.ToLower();
            var val = tag.Value.ToLower();

            switch (key)
            {
                case "name":
                    dbQuery = dbQuery.Where(c => c.Name.ToLower().Contains(val));
                    pcQuery = pcQuery.Where(c => c.Name.ToLower().Contains(val));
                    break;
                case "displayname":
                    dbQuery = dbQuery.Where(c => c.DisplayName != null && c.DisplayName.ToLower().Contains(val));
                    break;
                case "manufacturer":
                    dbQuery = dbQuery.Where(c => c.Manufacturer != null && c.Manufacturer.Name.ToLower().Contains(val));
                    break;
                case "team":
                    dbQuery = dbQuery.Where(c => c.ResponsibleTeams.Any(t => t.Name.ToLower().Contains(val)));
                    pcQuery = pcQuery.Where(c => c.ResponsibleTeams.Any(t => t.Name.ToLower().Contains(val)));
                    break;
                case "type":
                    if (val.StartsWith("stat") || val.StartsWith("mach")) {
                        dbQuery = dbQuery.OfType<Machine>();
                        pcQuery = pcQuery.Where(c => false);
                    }
                    else if (val.StartsWith("pc") || val.StartsWith("client")) {
                        dbQuery = dbQuery.Where(c => false);
                    }
                    else if (val.StartsWith("hard")) {
                        dbQuery = dbQuery.OfType<HardwareComponent>();
                        pcQuery = pcQuery.Where(c => false);
                    }
                    break;
            }
        }

        // Apply remaining query as a general "fuzzy" search
        if (!string.IsNullOrEmpty(remainingQuery))
        {
            var q = remainingQuery.ToLower();
            
            // Subquery for related items
            var relatedIds = new List<Guid>();
            var parentMatches = await _context.InventoryItems
                .Where(p => p.Name.ToLower().Contains(q) || (p.DisplayName != null && p.DisplayName.ToLower().Contains(q)))
                .SelectMany(p => p.Children.Select(c => c.Id))
                .ToListAsync();
            relatedIds.AddRange(parentMatches);

            dbQuery = dbQuery.Where(c => 
                c.Name.ToLower().Contains(q) || 
                (c.DisplayName != null && c.DisplayName.ToLower().Contains(q)) ||
                (c.SerialNumber != null && c.SerialNumber.ToLower().Contains(q)) ||
                (c.Manufacturer != null && c.Manufacturer.Name.ToLower().Contains(q)) ||
                relatedIds.Contains(c.Id)
            );

            pcQuery = pcQuery.Where(c => 
                c.Name.ToLower().Contains(q) || 
                (c.Hostname != null && c.Hostname.ToLower().Contains(q)) ||
                (c.MacAddress != null && c.MacAddress.ToLower().Contains(q))
            );
        }

        var finalResults = await ProjectToSearchResult(dbQuery.Take(limit));
        var finalPcResults = await ProjectPcsToSearchResult(pcQuery.Take(limit));
        finalResults.AddRange(finalPcResults);

        return finalResults.OrderBy(r => r.Name).Take(limit).ToList();
    }

    private async Task<List<App.Backend.Api.Dtos.SearchResultDto>> ProjectPcsToSearchResult(IQueryable<ClientPc> query)
    {
        return await query.Select(c => new App.Backend.Api.Dtos.SearchResultDto
        {
            Id = c.Id,
            Name = c.Hostname ?? c.Name, // Use Hostname as primary human-readable name
            DisplayName = c.Hostname,
            ItemType = "ClientPc",
            TypeLabel = "Edge PC",
            ManufacturerName = "Industrial PC"
        }).ToListAsync();
    }

    private async Task<List<App.Backend.Api.Dtos.SearchResultDto>> ProjectToSearchResult(IQueryable<BaseInventoryItem> query)
    {
        return await query.Select(c => new App.Backend.Api.Dtos.SearchResultDto
        {
            Id = c.Id,
            Name = c.Name,
            DisplayName = c.DisplayName,
            ItemType = c.GetType().Name,
            TypeLabel = c is Machine ? "Production Station" : 
                        c is HardwareComponent ? "Hardware Asset" :
                        c is SoftwareComponent ? "Software/License" : "Inventory Item",
            ManufacturerName = c.Manufacturer != null ? c.Manufacturer.Name : null
        }).ToListAsync();
    }

    [HttpGet("teams")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ResponsibleTeam>>> GetTeams()
    {
        return await _context.ResponsibleTeams.ToListAsync();
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<BaseInventoryItem>> GetById(Guid id)
    {
        var item = await _context.InventoryItems
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.ResponsibleTeams)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("manufacturers")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Manufacturer>>> GetManufacturers()
    {
        return await _context.Manufacturers.OrderBy(m => m.Name).ToListAsync();
    }

    [HttpGet("suppliers")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
    {
        return await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
    }

    [HttpGet("machines")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Machine>>> GetMachines()
    {
        return await _context.InventoryItems.OfType<Machine>().OrderBy(m => m.Name).ToListAsync();
    }

    [HttpGet("client-pcs")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ClientPc>>> GetClientPcs()
    {
        return await _context.ClientPcs.OrderBy(c => c.Name).ToListAsync();
    }

    [HttpPost]
    [AllowAnonymous] // Allowing for demo/prototype simplicity, should be admin-only in production
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

        // Map basic fields
        item.Id = Guid.NewGuid();
        item.Name = payload.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
        item.DisplayName = payload.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
        item.SerialNumber = payload.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null;
        
        if (payload.TryGetProperty("costInHUF", out var cost) && cost.ValueKind == JsonValueKind.Number)
        {
            item.CostInHUF = cost.GetDecimal();
        }
        
        // Handling Manufacturer (ID or Name)
        if (payload.TryGetProperty("manufacturerId", out var mIdProperty)) 
        {
            var mIdStr = mIdProperty.GetString();
            if (Guid.TryParse(mIdStr, out var manufacturerId)) 
            {
                item.ManufacturerId = manufacturerId;
            }
            else if (!string.IsNullOrEmpty(mIdStr))
            {
                var existingM = await _context.Manufacturers.FirstOrDefaultAsync(m => m.Name.ToLower() == mIdStr.ToLower());
                if (existingM != null)
                {
                    item.ManufacturerId = existingM.Id;
                }
                else
                {
                    var newM = new Manufacturer { Id = Guid.NewGuid(), Name = mIdStr };
                    _context.Manufacturers.Add(newM);
                    item.ManufacturerId = newM.Id;
                }
            }
        }

        // Handling Supplier (ID or Name)
        if (payload.TryGetProperty("supplierId", out var sIdProperty)) 
        {
            var sIdStr = sIdProperty.GetString();
            if (Guid.TryParse(sIdStr, out var supplierId)) 
            {
                item.SupplierId = supplierId;
            }
            else if (!string.IsNullOrEmpty(sIdStr))
            {
                var existingS = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name.ToLower() == sIdStr.ToLower());
                if (existingS != null)
                {
                    item.SupplierId = existingS.Id;
                }
                else
                {
                    var newS = new Supplier { Id = Guid.NewGuid(), Name = sIdStr };
                    _context.Suppliers.Add(newS);
                    item.SupplierId = newS.Id;
                }
            }
        }

        if (payload.TryGetProperty("clientPcId", out var pcIdProperty)) 
        {
            var pcIdStr = pcIdProperty.GetString();
            if (Guid.TryParse(pcIdStr, out var clientPcId)) 
            {
                item.ClientPcId = clientPcId;
            }
            else if (!string.IsNullOrEmpty(pcIdStr))
            {
                var existingPC = await _context.ClientPcs.FirstOrDefaultAsync(pc => pc.Hostname.ToLower() == pcIdStr.ToLower() || pc.Name.ToLower() == pcIdStr.ToLower());
                if (existingPC != null) item.ClientPcId = existingPC.Id;
            }
        }

        if (payload.TryGetProperty("parentId", out var parentIdProperty)) 
        {
            var pIdStr = parentIdProperty.GetString();
            if (Guid.TryParse(pIdStr, out var pId)) 
            {
                item.ParentId = pId;
            }
            else if (!string.IsNullOrEmpty(pIdStr) && !pIdStr.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                var existingP = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Name.ToLower() == pIdStr.ToLower());
                if (existingP != null) item.ParentId = existingP.Id;
            }
        }

        if (payload.TryGetProperty("machineId", out var machineIdProperty))
        {
             var mIdStr = machineIdProperty.GetString();
             if (Guid.TryParse(mIdStr, out var mId))
             {
                 // Machines are BaseInventoryItems, so we check if this ID exists in the stations table
                 var machine = await _context.InventoryItems.OfType<Machine>().FirstOrDefaultAsync(m => m.Id == mId);
                 if (machine != null) 
                 {
                    // Logic for linking Machine (Custom logic might be needed depending on the relationship)
                 }
             }
             else if (!string.IsNullOrEmpty(mIdStr))
             {
                 var existingM = await _context.InventoryItems.OfType<Machine>().FirstOrDefaultAsync(m => m.CustomIdentifier.ToLower() == mIdStr.ToLower());
                 if (existingM != null)
                 {
                     // Logic for linking Machine
                 }
             }
        }

        if (payload.TryGetProperty("data", out var data))
        {
            item.Metadata = JsonDocument.Parse(data.GetRawText());
        }

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null) return NotFound();
        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
