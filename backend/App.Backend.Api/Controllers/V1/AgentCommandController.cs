using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
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
    [Authorize(Policy = "EndpointConfigManagement")]
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

        _logger.LogInformation("Authorized user queued UPDATE_CONFIG for agent {ClientPcId}", clientPcId);
        return Ok(new { Message = "Command queued successfully" });
    }

    [HttpPost("{clientPcId}/file-check")]
    [Authorize(Policy = "RemoteExecution")]
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

        _logger.LogInformation("Authorized user queued FILE_CHECK for agent {ClientPcId}", clientPcId);
        return Ok(new { Message = "Command queued successfully" });
    }
}

public class AgentConfigUpdateDto
{
    public object Config { get; set; } = null!;
    public string? Signature { get; set; }
}
