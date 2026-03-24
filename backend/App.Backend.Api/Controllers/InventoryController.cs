using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing inventory components, both hardware and software.
/// Access to these endpoints is restricted to users with the "admin" role.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")] // Only admins can manage inventory components
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryController"/> class.
    /// </summary>
    /// <param name="context">The application's database context.</param>
    public InventoryController(AppDbContext context)
    {
        _context = context;
    }

    // --- Hardware Components ---

    /// <summary>
    /// Retrieves a list of all Hardware Components, including their manufacturer and supplier.
    /// Can be accessed anonymously for testing purposes.
    /// </summary>
    /// <returns>A list of <see cref="HardwareComponent"/> objects.</returns>
    [HttpGet("hardware")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<HardwareComponent>>> GetHardware()
    {
        return await _context.HardwareComponents
            .Include(h => h.Manufacturer)
            .Include(h => h.Supplier)
            .ToListAsync();
    }

    /// <summary>
    /// Searches for Hardware Components based on various criteria.
    /// </summary>
    /// <param name="query">General search query for name, description, serial or model.</param>
    /// <param name="category">Filter by technical specification category.</param>
    /// <param name="manufacturer">Filter by manufacturer name (partial match).</param>
    /// <param name="minTorque">Filter by minimum maximum torque in technical specifications.</param>
    /// <param name="interfaceType">Filter by technical specification interface type.</param>
    /// <returns>A filtered list of <see cref="HardwareComponent"/> objects.</returns>
    [HttpGet("hardware/search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<HardwareComponent>>> SearchHardware(
        [FromQuery] string? query,
        [FromQuery] string? category, 
        [FromQuery] string? manufacturer,
        [FromQuery] double? minTorque,
        [FromQuery] string? interfaceType)
    {
        var dbQuery = _context.HardwareComponents
            .Include(h => h.Manufacturer)
            .Include(h => h.Supplier)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
        {
            var q = query.ToLower();
            dbQuery = dbQuery.Where(h => 
                h.Name.ToLower().Contains(q) || 
                (h.Description != null && h.Description.ToLower().Contains(q)) ||
                (h.SerialNumber != null && h.SerialNumber.ToLower().Contains(q)) ||
                (h.ModelNumber != null && h.ModelNumber.ToLower().Contains(q))
            );
        }

        if (!string.IsNullOrEmpty(category))
            dbQuery = dbQuery.Where(h => h.TechnicalSpecs != null && h.TechnicalSpecs.Category == category);

        if (!string.IsNullOrEmpty(manufacturer))
            dbQuery = dbQuery.Where(h => h.Manufacturer != null && h.Manufacturer.Name.ToLower().Contains(manufacturer.ToLower()));

        if (minTorque.HasValue)
            dbQuery = dbQuery.Where(h => h.TechnicalSpecs != null && h.TechnicalSpecs.TorqueMax >= minTorque.Value);

        if (!string.IsNullOrEmpty(interfaceType))
            dbQuery = dbQuery.Where(h => h.TechnicalSpecs != null && h.TechnicalSpecs.InterfaceType == interfaceType);

        return await dbQuery.ToListAsync();
    }

    /// <summary>
    /// Searches for Software Components based on various criteria.
    /// </summary>
    /// <param name="query">General search query for name, description, serial or version.</param>
    /// <returns>A filtered list of <see cref="SoftwareComponent"/> objects.</returns>
    [HttpGet("software/search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<SoftwareComponent>>> SearchSoftware(
        [FromQuery] string? query)
    {
        var dbQuery = _context.SoftwareComponents
            .Include(s => s.Manufacturer)
            .Include(s => s.Supplier)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
        {
            var q = query.ToLower();
            dbQuery = dbQuery.Where(s => 
                s.Name.ToLower().Contains(q) || 
                (s.Description != null && s.Description.ToLower().Contains(q)) ||
                (s.SerialNumber != null && s.SerialNumber.ToLower().Contains(q)) ||
                (s.Version != null && s.Version.ToLower().Contains(q))
            );
        }

        return await dbQuery.ToListAsync();
    }

    /// <summary>
    /// Creates a new Hardware Component.
    /// </summary>
    /// <param name="component">The <see cref="HardwareComponent"/> object to create.</param>
    /// <returns>The newly created <see cref="HardwareComponent"/> object.</returns>
    [HttpPost("hardware")]
    public async Task<ActionResult<HardwareComponent>> CreateHardware(HardwareComponent component)
    {
        _context.HardwareComponents.Add(component);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetHardware), new { id = component.Id }, component);
    }

    /// <summary>
    /// Updates an existing Hardware Component.
    /// </summary>
    /// <param name="id">The unique identifier of the Hardware Component to update.</param>
    /// <param name="component">The <see cref="HardwareComponent"/> object with updated data.</param>
    /// <returns>A <see cref="NoContentResult"/> if successful, <see cref="BadRequestResult"/> if IDs do not match.</returns>
    [HttpPut("hardware/{id}")]
    public async Task<IActionResult> UpdateHardware(Guid id, HardwareComponent component)
    {
        if (id != component.Id) return BadRequest();
        _context.Entry(component).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Deletes a specific Hardware Component by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the Hardware Component to delete.</param>
    /// <returns>A <see cref="NoContentResult"/> if successful, <see cref="NotFoundResult"/> if the component is not found.</returns>
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

    /// <summary>
    /// Retrieves a list of all Software Components, including their manufacturer and supplier.
    /// Can be accessed anonymously for testing purposes.
    /// </summary>
    /// <returns>A list of <see cref="SoftwareComponent"/> objects.</returns>
    [HttpGet("software")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<SoftwareComponent>>> GetSoftware()
    {
        return await _context.SoftwareComponents
            .Include(s => s.Manufacturer)
            .Include(s => s.Supplier)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new Software Component.
    /// </summary>
    /// <param name="component">The <see cref="SoftwareComponent"/> object to create.</param>
    /// <returns>The newly created <see cref="SoftwareComponent"/> object.</returns>
    [HttpPost("software")]
    public async Task<ActionResult<SoftwareComponent>> CreateSoftware(SoftwareComponent component)
    {
        _context.SoftwareComponents.Add(component);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSoftware), new { id = component.Id }, component);
    }

    /// <summary>
    /// Updates an existing Software Component.
    /// </summary>
    /// <param name="id">The unique identifier of the Software Component to update.</param>
    /// <param name="component">The <see cref="SoftwareComponent"/> object with updated data.</param>
    /// <returns>A <see cref="NoContentResult"/> if successful, <see cref="BadRequestResult"/> if IDs do not match.</returns>
    [HttpPut("software/{id}")]
    public async Task<IActionResult> UpdateSoftware(Guid id, SoftwareComponent component)
    {
        if (id != component.Id) return BadRequest();
        _context.Entry(component).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Deletes a specific Software Component by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the Software Component to delete.</param>
    /// <returns>A <see cref="NoContentResult"/> if successful, <see cref="NotFoundResult"/> if the component is not found.</returns>
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
