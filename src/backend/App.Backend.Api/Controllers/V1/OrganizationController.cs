using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class OrganizationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuthorizationService? _authorizationService;

    public OrganizationController(AppDbContext context, IAuthorizationService? authorizationService = null)
    {
        _context = context;
        _authorizationService = authorizationService;
    }

    /// <summary>Same definition of "platform administrator" as <see cref="App.Backend.Api.Security.TenantScopeMiddleware"/>.</summary>
    private async Task<bool> IsPlatformAdminAsync() =>
        _authorizationService != null && User != null &&
        (await _authorizationService.AuthorizeAsync(User, App.Contracts.Security.AuthorizationPolicies.SystemAdministration)).Succeeded;

    /// <summary>
    /// Organization directory with member counts. Platform administrators see every organization;
    /// everyone else only the organizations they are a member of. It used to be anonymous and
    /// returned all tenants to any caller.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult> GetOrganizations()
    {
        IQueryable<AuthOrganization> visible = _context.AuthOrganizations;
        if (!await IsPlatformAdminAsync())
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            visible = visible.Where(o => o.Members.Any(m => m.UserId == userId));
        }

        var orgs = await visible
            .Select(o => new
            {
                id = o.Id,
                name = o.Name,
                slug = o.Id,
                memberCount = o.Members.Count
            })
            .ToListAsync();
        return Ok(new { success = true, organizations = orgs });
    }

    [HttpGet("my-organizations")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AuthOrganization>>> GetMyOrganizations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var organizations = await _context.AuthMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Organization)
            .Select(m => m.Organization)
            .ToListAsync();

        return Ok(organizations);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<AuthOrganization>> GetOrganization(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var isMember = await _context.AuthMembers
            .AnyAsync(m => m.OrganizationId == id && m.UserId == userId);

        // Platform administrators govern every organization, so they do not need a membership row.
        if (!isMember && !await IsPlatformAdminAsync()) return Forbid();

        var organization = await _context.AuthOrganizations
            .Include(o => o.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (organization == null) return NotFound();

        return Ok(organization);
    }

    [HttpGet("all")]
    [Authorize(Policy = "SystemAdministration")]
    public async Task<ActionResult<IEnumerable<AuthOrganization>>> GetAllOrganizations()
    {
        return await _context.AuthOrganizations.ToListAsync();
    }
}
