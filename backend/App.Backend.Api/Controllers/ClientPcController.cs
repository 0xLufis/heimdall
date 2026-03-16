using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require BetterAuth session for all endpoints
public class ClientPcController : ControllerBase
{
    private readonly ClientPcRepository _repository;
    private readonly ILogger<ClientPcController> _logger;

    public ClientPcController(ClientPcRepository repository, ILogger<ClientPcController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous] // Allow testing without full auth
    public async Task<ActionResult<IEnumerable<ClientPc>>> GetClientPcs(
        [FromServices] AppDbContext dbContext)
    {
        var pcs = await dbContext.ClientPcs
            .Include(c => c.Machines)
            .ToListAsync();
        return Ok(pcs);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClientPc>> GetClientPc(Guid id)
    {
        var pc = await _repository.GetByIdAsync(id);
        if (pc == null)
        {
            return NotFound();
        }
        return Ok(pc);
    }

    [HttpPost]
    public async Task<ActionResult<ClientPc>> CreateClientPc(ClientPc pc)
    {
        var createdPc = await _repository.CreateAsync(pc);
        return CreatedAtAction(nameof(GetClientPc), new { id = createdPc.Id }, createdPc);
    }

    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateClientPc(Guid id, [FromBody] ClientPc update, [FromServices] AppDbContext dbContext)
    {
        var pc = await dbContext.ClientPcs
            .Include(c => c.Machines)
            .FirstOrDefaultAsync(c => c.Id == id);
            
        if (pc == null) return NotFound();

        pc.PinnedObjectHandle = update.PinnedObjectHandle;
        
        // Update many-to-many relationship
        if (update.Machines != null)
        {
            pc.Machines.Clear();
            foreach (var machineUpdate in update.Machines)
            {
                var existingMachine = await dbContext.Machines.FindAsync(machineUpdate.Id);
                if (existingMachine != null)
                {
                    pc.Machines.Add(existingMachine);
                }
            }
        }
        
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
