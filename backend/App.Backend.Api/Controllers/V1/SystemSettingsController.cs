using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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
                UpdatedBy = User?.Identity?.Name ?? "system",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.SystemSettings.Add(setting);
        }
        else
        {
            setting.ValueJson = dto.ValueJson;
            setting.UpdatedBy = User?.Identity?.Name ?? "system";
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

    [HttpGet("mfa-policy")]
    public async Task<IActionResult> GetMfaPolicy()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "Auth.MfaPolicy");

        if (setting == null || string.IsNullOrWhiteSpace(setting.ValueJson))
        {
            var defaultPolicy = GetDefaultMfaPolicy();
            return Ok(defaultPolicy);
        }

        try
        {
            var policy = JsonSerializer.Deserialize<MfaPolicyDto>(setting.ValueJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(policy ?? GetDefaultMfaPolicy());
        }
        catch
        {
            return Ok(GetDefaultMfaPolicy());
        }
    }

    [HttpPut("mfa-policy")]
    public async Task<IActionResult> UpdateMfaPolicy([FromBody] MfaPolicyDto dto)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "Auth.MfaPolicy");

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = "Auth.MfaPolicy",
                Category = "Auth",
                ValueJson = json,
                UpdatedBy = User?.Identity?.Name ?? "system",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.SystemSettings.Add(setting);
        }
        else
        {
            setting.ValueJson = json;
            setting.UpdatedBy = User?.Identity?.Name ?? "system";
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("MFA policy updated with {Count} rules by {User}", dto.Rules.Count, setting.UpdatedBy);
        return Ok(dto);
    }

    [HttpPost("mfa-policy/evaluate")]
    public async Task<IActionResult> EvaluateMfa([FromBody] MfaEvaluationRequestDto request)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "Auth.MfaPolicy");

        var policy = GetDefaultMfaPolicy();
        if (setting != null && !string.IsNullOrWhiteSpace(setting.ValueJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<MfaPolicyDto>(setting.ValueJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) policy = parsed;
            }
            catch { }
        }

        var result = EvaluateMfaInternal(policy, request);
        return Ok(result);
    }

    private static MfaPolicyDto GetDefaultMfaPolicy()
    {
        return new MfaPolicyDto
        {
            Enabled = true,
            DefaultThreshold = "7d",
            Rules = new List<MfaGroupRuleDto>
            {
                new()
                {
                    Id = "rule-sysadmin",
                    TargetType = "role",
                    TargetName = "SystemAdministrator",
                    ForceMfa = true,
                    TimeoutThreshold = "always",
                    Description = "System Administrators must re-verify MFA on every sign-in ('always')"
                },
                new()
                {
                    Id = "rule-engineer",
                    TargetType = "role",
                    TargetName = "Engineer",
                    ForceMfa = true,
                    TimeoutThreshold = "7d",
                    Description = "Engineers must re-verify MFA once a week (7 days)"
                },
                new()
                {
                    Id = "rule-technician",
                    TargetType = "role",
                    TargetName = "Technician",
                    ForceMfa = true,
                    TimeoutThreshold = "30d",
                    Description = "Technicians must re-verify MFA once a month (30 days)"
                },
                new()
                {
                    Id = "rule-maint-leads",
                    TargetType = "group",
                    TargetName = "Maintenance Leads",
                    ForceMfa = true,
                    TimeoutThreshold = "14d",
                    Description = "Maintenance group & shift leaders must re-verify MFA bi-weekly (14 days)"
                }
            }
        };
    }

    private static MfaEvaluationResultDto EvaluateMfaInternal(MfaPolicyDto policy, MfaEvaluationRequestDto request)
    {
        if (!policy.Enabled)
        {
            return new MfaEvaluationResultDto
            {
                MfaRequired = false,
                Reason = "Global MFA enforcement is currently disabled.",
                IsExpired = false
            };
        }

        var userRole = request.Role?.Trim() ?? string.Empty;
        var userGroups = request.Groups?.Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).ToList() ?? new List<string>();

        MfaGroupRuleDto? matchedRule = null;

        if (!string.IsNullOrEmpty(userRole))
        {
            matchedRule = policy.Rules.FirstOrDefault(r => 
                r.TargetType.Equals("role", StringComparison.OrdinalIgnoreCase) &&
                r.TargetName.Equals(userRole, StringComparison.OrdinalIgnoreCase) &&
                r.ForceMfa);
        }

        if (matchedRule == null && userGroups.Count > 0)
        {
            matchedRule = policy.Rules.FirstOrDefault(r =>
                r.TargetType.Equals("group", StringComparison.OrdinalIgnoreCase) &&
                userGroups.Any(ug => ug.Equals(r.TargetName, StringComparison.OrdinalIgnoreCase)) &&
                r.ForceMfa);
        }

        var threshold = matchedRule?.TimeoutThreshold ?? policy.DefaultThreshold;

        if (matchedRule != null && !matchedRule.ForceMfa)
        {
            return new MfaEvaluationResultDto
            {
                MfaRequired = false,
                Reason = $"MFA explicitly exempt for {matchedRule.TargetType} '{matchedRule.TargetName}'.",
                MatchedRuleTarget = matchedRule.TargetName,
                AppliedThreshold = "exempt",
                IsExpired = false
            };
        }

        if (threshold.Equals("always", StringComparison.OrdinalIgnoreCase))
        {
            return new MfaEvaluationResultDto
            {
                MfaRequired = true,
                Reason = matchedRule != null
                    ? $"Policy requires MFA challenge on every login for '{matchedRule.TargetName}'."
                    : "Default policy requires MFA challenge on every login.",
                MatchedRuleTarget = matchedRule?.TargetName,
                AppliedThreshold = "always",
                IsExpired = true
            };
        }

        if (threshold.Equals("never", StringComparison.OrdinalIgnoreCase))
        {
            return new MfaEvaluationResultDto
            {
                MfaRequired = false,
                Reason = "Policy threshold is set to 'never'.",
                MatchedRuleTarget = matchedRule?.TargetName,
                AppliedThreshold = "never",
                IsExpired = false
            };
        }

        if (request.LastMfaAt == null)
        {
            return new MfaEvaluationResultDto
            {
                MfaRequired = true,
                Reason = "No previous MFA verification timestamp recorded for session.",
                MatchedRuleTarget = matchedRule?.TargetName,
                AppliedThreshold = threshold,
                IsExpired = true
            };
        }

        var minutes = threshold switch
        {
            "12h" => 12 * 60,
            "24h" => 24 * 60,
            "7d" => 7 * 24 * 60,
            "14d" => 14 * 24 * 60,
            "30d" => 30 * 24 * 60,
            "90d" => 90 * 24 * 60,
            "custom" => (matchedRule?.CustomDays ?? 7) * 24 * 60,
            _ => 7 * 24 * 60
        };

        var expiresAt = request.LastMfaAt.Value.AddMinutes(minutes);
        var now = DateTimeOffset.UtcNow;
        var isExpired = now >= expiresAt;

        return new MfaEvaluationResultDto
        {
            MfaRequired = isExpired,
            Reason = isExpired
                ? $"MFA session expired after {threshold} interval (expired at {expiresAt:yyyy-MM-dd HH:mm:ss} UTC)."
                : $"MFA session valid until {expiresAt:yyyy-MM-dd HH:mm:ss} UTC ({Math.Round((expiresAt - now).TotalHours, 1)} hours remaining).",
            MatchedRuleTarget = matchedRule?.TargetName,
            AppliedThreshold = threshold,
            ExpiresAt = expiresAt,
            IsExpired = isExpired
        };
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

public class MfaPolicyDto
{
    public bool Enabled { get; set; } = true;
    public string DefaultThreshold { get; set; } = "7d";
    public List<MfaGroupRuleDto> Rules { get; set; } = new();
}

public class MfaGroupRuleDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TargetType { get; set; } = "role";
    public string TargetName { get; set; } = string.Empty;
    public bool ForceMfa { get; set; } = true;
    public string TimeoutThreshold { get; set; } = "7d";
    public int? CustomDays { get; set; }
    public string? Description { get; set; }
}

public class MfaEvaluationRequestDto
{
    public string? Role { get; set; }
    public List<string>? Groups { get; set; }
    public DateTimeOffset? LastMfaAt { get; set; }
}

public class MfaEvaluationResultDto
{
    public bool MfaRequired { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? MatchedRuleTarget { get; set; }
    public string? AppliedThreshold { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
}
