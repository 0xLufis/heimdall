namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MachineGroupController : ControllerBase
{
    private readonly IMachineGroupRepository _repository;

    public MachineGroupController(IMachineGroupRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<List<MachineGroup>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await _repository.GetAllAsync(cancellationToken);
        return Ok(groups);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MachineGroup>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var group = await _repository.GetByIdAsync(id, cancellationToken);
        if (group == null) return NotFound();
        return Ok(group);
    }

    [HttpPost]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<MachineGroup>> Create([FromBody] MachineGroup group, CancellationToken cancellationToken)
    {
        var created = await _repository.CreateAsync(group, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<MachineGroup>> Update(Guid id, [FromBody] MachineGroup updates, CancellationToken cancellationToken)
    {
        var updated = await _repository.UpdateAsync(id, updates, cancellationToken);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var success = await _repository.DeleteAsync(id, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }
}
