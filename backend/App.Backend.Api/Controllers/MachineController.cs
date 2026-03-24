using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing Machine entities.
/// Provides API endpoints for retrieving, creating, and updating machine information.
/// Requires authentication for all endpoints by default, but some can be overridden with [AllowAnonymous].
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Require BetterAuth session for all endpoints
public class MachineController : ControllerBase
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineController"/> class.
    /// </summary>
    /// <param name="context">The application's database context.</param>
    public MachineController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a list of all Machines.
    /// Can be accessed anonymously for testing purposes.
    /// </summary>
    /// <returns>A list of <see cref="Machine"/> objects.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Machine>>> GetMachines()
    {
        return await _context.Machines.ToListAsync();
    }

    /// <summary>
    /// Creates a new Machine.
    /// </summary>
    /// <param name="machine">The <see cref="Machine"/> object to create.</param>
    /// <returns>The newly created <see cref="Machine"/> object.</returns>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<Machine>> CreateMachine(Machine machine)
    {
        _context.Machines.Add(machine);
        await _context.SaveChangesAsync();
        return Ok(machine);
    }

    /// <summary>
    /// Updates an existing Machine.
    /// </summary>
    /// <param name="id">The unique identifier of the Machine to update.</param>
    /// <param name="machine">The <see cref="Machine"/> object with updated data.</param>
    /// <returns>A <see cref="NoContentResult"/> if successful, <see cref="BadRequestResult"/> if IDs do not match.</returns>
    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateMachine(Guid id, Machine machine)
    {
        if (id != machine.Id) return BadRequest();
        _context.Entry(machine).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
