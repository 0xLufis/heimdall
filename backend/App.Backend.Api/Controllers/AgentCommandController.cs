using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Controllers;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/[controller]")]
public class AgentCommandController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<AgentCommandController> _logger;

    public AgentCommandController(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<AgentCommandController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    [HttpPost("{clientPcId}/update-config")]
    public async Task<IActionResult> UpdateConfig(Guid clientPcId, [FromBody] AgentConfigUpdateDto update)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var command = new QueuedAgentCommand
        {
            ClientPcId = clientPcId,
            Type = "UPDATE_CONFIG",
            Payload = System.Text.Json.JsonSerializer.Serialize(update.Config),
            Signature = update.Signature,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.QueuedAgentCommands.Add(command);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Admin queued UPDATE_CONFIG for agent {ClientPcId}", clientPcId);
        return Ok(new { Message = "Command queued successfully" });
    }

    [HttpPost("{clientPcId}/file-check")]
    public async Task<IActionResult> FileCheck(Guid clientPcId, [FromBody] string filePath)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var command = new QueuedAgentCommand
        {
            ClientPcId = clientPcId,
            Type = "FILE_CHECK",
            Payload = filePath,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.QueuedAgentCommands.Add(command);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Admin queued FILE_CHECK for agent {ClientPcId}", clientPcId);
        return Ok(new { Message = "Command queued successfully" });
    }
}

public class AgentConfigUpdateDto
{
    public object Config { get; set; } = null!;
    public string? Signature { get; set; }
}
