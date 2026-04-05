using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing the unified inventory components.
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
    /// Retrieves all top-level inventory components with their full tree structure.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<InventoryComponent>>> GetInventory()
    {
        return await _context.InventoryComponents
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.Children)
            .Where(c => c.ParentId == null)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<InventoryComponent>> GetById(Guid id)
    {
        var component = await _context.InventoryComponents
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (component == null) return NotFound();
        return Ok(component);
    }

    /// <summary>
    /// Basic search for components. Advanced filtering is handled on the client side.
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<InventoryComponent>>> Search([FromQuery] string? query)
    {
        var dbQuery = _context.InventoryComponents
            .Include(c => c.Manufacturer)
            .Include(c => c.Supplier)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
        {
            var q = query.ToLower();
            dbQuery = dbQuery.Where(c => 
                c.Name.ToLower().Contains(q) || 
                (c.Technology != null && c.Technology.ToLower().Contains(q))
            );
        }

        return await dbQuery.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<InventoryComponent>> Create(InventoryComponent component)
    {
        _context.InventoryComponents.Add(component);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = component.Id }, component);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, InventoryComponent component)
    {
        if (id != component.Id) return BadRequest();
        _context.Entry(component).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var component = await _context.InventoryComponents.FindAsync(id);
        if (component == null) return NotFound();
        _context.InventoryComponents.Remove(component);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
