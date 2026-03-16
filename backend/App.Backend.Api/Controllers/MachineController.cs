using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require BetterAuth session for all endpoints
public class MachineController : ControllerBase
{
    private readonly AppDbContext _context;

    public MachineController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Machine>>> GetMachines()
    {
        return await _context.Machines.ToListAsync();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<Machine>> CreateMachine(Machine machine)
    {
        _context.Machines.Add(machine);
        await _context.SaveChangesAsync();
        return Ok(machine);
    }

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
