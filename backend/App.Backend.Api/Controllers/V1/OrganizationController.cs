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

    public OrganizationController(AppDbContext context)
    {
        _context = context;
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

        if (!isMember) return Forbid();

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
