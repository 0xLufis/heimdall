using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for managing Client PCs.
/// Provides API endpoints for retrieving, creating, updating, and deleting client PC information.
/// Requires authentication for all endpoints by default, but some can be overridden with [AllowAnonymous].
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Require BetterAuth session for all endpoints
public class ClientPcController : ControllerBase
{
    private readonly ClientPcRepository _repository;
    private readonly ILogger<ClientPcController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientPcController"/> class.
    /// </summary>
    /// <param name="repository">The repository for Client PC data operations.</param>
    /// <param name="logger">The logger for the controller.</param>
    public ClientPcController(ClientPcRepository repository, ILogger<ClientPcController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a list of all Client PCs, including their associated Machines.
    /// Can be accessed anonymously for testing purposes.
    /// </summary>
    /// <param name="dbContext">The application's database context.</param>
    /// <returns>A list of <see cref="ClientPc"/> objects.</returns>
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

    /// <summary>
    /// Retrieves a specific Client PC by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the Client PC.</param>
    /// <returns>A <see cref="ClientPc"/> object if found, otherwise <see cref="NotFoundResult"/>.</returns>
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

    /// <summary>
    /// Creates a new Client PC.
    /// </summary>
    /// <param name="pc">The <see cref="ClientPc"/> object to create.</param>
    /// <returns>The newly created <see cref="ClientPc"/> object.</returns>
    [HttpPost]
    public async Task<ActionResult<ClientPc>> CreateClientPc(ClientPc pc)
    {
        var createdPc = await _repository.CreateAsync(pc);
        return CreatedAtAction(nameof(GetClientPc), new { id = createdPc.Id }, createdPc);
    }

    /// <summary>
    /// Updates an existing Client PC's pinned object handle and associated machines.
    /// Can be accessed anonymously for testing purposes.
    /// </summary>
    /// <param name="id">The unique identifier of the Client PC to update.</param>
    /// <param name="update">The <see cref="ClientPc"/> object containing the updated data.</param>
    /// <param name="dbContext">The application's database context.</param>
    /// <returns>A <see cref="NoContentResult"/> if successful, <see cref="NotFoundResult"/> if the PC is not found.</returns>
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
