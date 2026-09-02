using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using App.Shared.Data;
using App.Shared.Entities;
using App.Backend.Api.Security;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "SystemAdministration")]
public class SecurityGroupMappingController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SecurityGroupMappingController> _logger;

    public SecurityGroupMappingController(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IMemoryCache memoryCache,
        ILogger<SecurityGroupMappingController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var list = await db.SecurityGroupMappings
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SecurityGroupMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.GroupIdentifier) || string.IsNullOrWhiteSpace(mapping.MappedRole))
        {
            return BadRequest(new { Message = "GroupIdentifier and MappedRole are required." });
        }

        mapping.Id = Guid.NewGuid();
        mapping.CreatedAt = DateTimeOffset.UtcNow;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.SecurityGroupMappings.Add(mapping);
        await db.SaveChangesAsync();

        DynamicSecurityGroupClaimsTransformer.InvalidateCache(_memoryCache);
        _logger.LogInformation("Created security group mapping: {Group} -> {Role}", mapping.GroupIdentifier, mapping.MappedRole);

        return CreatedAtAction(nameof(GetAll), new { id = mapping.Id }, mapping);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SecurityGroupMapping update)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var existing = await db.SecurityGroupMappings.FindAsync(id);
        if (existing == null) return NotFound();

        existing.DisplayName = update.DisplayName;
        existing.IdentityProvider = update.IdentityProvider;
        existing.GroupIdentifier = update.GroupIdentifier;
        existing.MappedRole = update.MappedRole;
        existing.OrganizationId = update.OrganizationId;
        existing.IsEnabled = update.IsEnabled;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        DynamicSecurityGroupClaimsTransformer.InvalidateCache(_memoryCache);
        _logger.LogInformation("Updated security group mapping {Id}", id);

        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var existing = await db.SecurityGroupMappings.FindAsync(id);
        if (existing == null) return NotFound();

        db.SecurityGroupMappings.Remove(existing);
        await db.SaveChangesAsync();

        DynamicSecurityGroupClaimsTransformer.InvalidateCache(_memoryCache);
        _logger.LogInformation("Deleted security group mapping {Id}", id);

        return NoContent();
    }

    [HttpPost("test-evaluate")]
    public async Task<IActionResult> TestEvaluate([FromBody] TestEvaluateRequest request)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var activeMappings = await db.SecurityGroupMappings
            .Where(m => m.IsEnabled)
            .ToListAsync();

        var inputGroups = request.GroupIdentifiers?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        var matches = activeMappings
            .Where(m => inputGroups.Contains(m.GroupIdentifier))
            .ToList();

        var resolvedRoles = matches.Select(m => m.MappedRole).Distinct().ToList();
        var resolvedOrg = matches.FirstOrDefault(m => !string.IsNullOrEmpty(m.OrganizationId))?.OrganizationId;

        return Ok(new
        {
            InputCount = inputGroups.Count,
            MatchedCount = matches.Count,
            MatchedMappings = matches,
            ResolvedRoles = resolvedRoles,
            ResolvedOrganizationId = resolvedOrg
        });
    }
}

public class TestEvaluateRequest
{
    public List<string> GroupIdentifiers { get; set; } = new();
}
