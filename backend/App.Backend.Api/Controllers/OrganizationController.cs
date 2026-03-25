using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for retrieving organization data from Better-Auth tables.
/// This allows the backend to serve as a proxy or source for organization info.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrganizationController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a list of organizations the current user is a member of.
    /// </summary>
    [HttpGet("my-organizations")]
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

    /// <summary>
    /// Retrieves details for a specific organization if the user is a member.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AuthOrganization>> GetOrganization(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Check membership
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

    /// <summary>
    /// Retrieves all organizations (Admin only).
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<AuthOrganization>>> GetAllOrganizations()
    {
        return await _context.AuthOrganizations.ToListAsync();
    }
}
