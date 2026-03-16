using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")] // Only admins can manage inventory components
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public InventoryController(AppDbContext context)
    {
        _context = context;
    }

    // --- Hardware Components ---

    [HttpGet("hardware")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<HardwareComponent>>> GetHardware()
    {
        return await _context.HardwareComponents
            .Include(h => h.Manufacturer)
            .Include(h => h.Supplier)
            .ToListAsync();
    }

    [HttpGet("hardware/search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<HardwareComponent>>> SearchHardware(
        [FromQuery] string? category, 
        [FromQuery] string? manufacturer,
        [FromQuery] double? minTorque,
        [FromQuery] string? interfaceType)
    {
        var query = _context.HardwareComponents
            .Include(h => h.Manufacturer)
            .Include(h => h.Supplier)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(h => h.TechnicalSpecs != null && h.TechnicalSpecs.Category == category);

        if (!string.IsNullOrEmpty(manufacturer))
            query = query.Where(h => h.Manufacturer != null && h.Manufacturer.Name.Contains(manufacturer));

        if (minTorque.HasValue)
            query = query.Where(h => h.TechnicalSpecs != null && h.TechnicalSpecs.TorqueMax >= minTorque.Value);

        if (!string.IsNullOrEmpty(interfaceType))
            query = query.Where(h => h.TechnicalSpecs != null && h.TechnicalSpecs.InterfaceType == interfaceType);

        return await query.ToListAsync();
    }

    [HttpPost("hardware")]
    public async Task<ActionResult<HardwareComponent>> CreateHardware(HardwareComponent component)
    {
        _context.HardwareComponents.Add(component);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetHardware), new { id = component.Id }, component);
    }

    [HttpPut("hardware/{id}")]
    public async Task<IActionResult> UpdateHardware(Guid id, HardwareComponent component)
    {
        if (id != component.Id) return BadRequest();
        _context.Entry(component).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("hardware/{id}")]
    public async Task<IActionResult> DeleteHardware(Guid id)
    {
        var component = await _context.HardwareComponents.FindAsync(id);
        if (component == null) return NotFound();
        _context.HardwareComponents.Remove(component);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // --- Software Components ---

    [HttpGet("software")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<SoftwareComponent>>> GetSoftware()
    {
        return await _context.SoftwareComponents
            .Include(s => s.Manufacturer)
            .Include(s => s.Supplier)
            .ToListAsync();
    }

    [HttpPost("software")]
    public async Task<ActionResult<SoftwareComponent>> CreateSoftware(SoftwareComponent component)
    {
        _context.SoftwareComponents.Add(component);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSoftware), new { id = component.Id }, component);
    }

    [HttpPut("software/{id}")]
    public async Task<IActionResult> UpdateSoftware(Guid id, SoftwareComponent component)
    {
        if (id != component.Id) return BadRequest();
        _context.Entry(component).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("software/{id}")]
    public async Task<IActionResult> DeleteSoftware(Guid id)
    {
        var component = await _context.SoftwareComponents.FindAsync(id);
        if (component == null) return NotFound();
        _context.SoftwareComponents.Remove(component);
        await _context.SaveChangesAsync();
        return NoContent();
    }

}
