using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "SystemAdministration")]
public class SystemSettingsController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<SystemSettingsController> _logger;

    public SystemSettingsController(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<SystemSettingsController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var settings = await db.SystemSettings.ToListAsync();
        return Ok(settings);
    }

    [HttpGet("{category}")]
    public async Task<IActionResult> GetByCategory(string category)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Category.ToLower() == category.ToLower() || s.Key.ToLower() == category.ToLower());
        
        if (setting == null)
        {
            return Ok(new { Key = category, Category = category, ValueJson = "{}" });
        }
        return Ok(setting);
    }

    [HttpPut("{category}")]
    public async Task<IActionResult> UpdateCategory(string category, [FromBody] UpdateSettingDto dto)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Category.ToLower() == category.ToLower() || s.Key.ToLower() == category.ToLower());

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = category,
                Category = category,
                ValueJson = dto.ValueJson,
                UpdatedBy = User.Identity?.Name ?? "system",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.SystemSettings.Add(setting);
        }
        else
        {
            setting.ValueJson = dto.ValueJson;
            setting.UpdatedBy = User.Identity?.Name ?? "system";
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("System setting {Category} updated by {User}", category, setting.UpdatedBy);
        return Ok(setting);
    }

    [HttpPost("push-agent-master-policy")]
    public async Task<IActionResult> PushAgentMasterPolicy([FromBody] PushMasterPolicyDto dto)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var activePcs = await db.ClientPcs
            .Select(p => p.Id)
            .ToListAsync();

        var commands = activePcs.Select(pcId => new QueuedAgentCommand
        {
            ClientPcId = pcId,
            Type = "SET_MASTER_POLICY",
            Payload = dto.PolicyJson ?? "{}",
            Signature = dto.Signature,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        db.QueuedAgentCommands.AddRange(commands);
        await db.SaveChangesAsync();

        _logger.LogInformation("Dispatched SET_MASTER_POLICY to {Count} edge agents.", commands.Count);
        return Ok(new { Message = $"Master policy queued for {commands.Count} edge nodes.", DispatchedCount = commands.Count });
    }
}

public class UpdateSettingDto
{
    public string ValueJson { get; set; } = "{}";
}

public class PushMasterPolicyDto
{
    public string PolicyJson { get; set; } = "{}";
    public string? Signature { get; set; }
}
