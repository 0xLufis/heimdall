using System.Security.Claims;
using App.Contracts.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BackendFeatureFlags _featureFlags;

    public AuthController(BackendFeatureFlags? featureFlags = null)
    {
        _featureFlags = featureFlags ?? new BackendFeatureFlags { EnableDevFeatures = true, EnableDebugFeatures = true };
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetMe()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(new
        {
            IsAuthenticated = true,
            User = User.Identity?.Name,
            Roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value),
            Claims = claims
        });
    }

    /// <summary>
    /// Drops the caller's own cached session (see <see cref="App.Backend.Api.Security.BetterAuthHandler"/>).
    /// Better-Auth owns the <c>auth</c> schema, so a role, active-organization, membership or ban
    /// change is invisible to this API until the cached entry expires. The BFF calls this with the
    /// affected session's token right after such a change. A caller can only ever evict its own
    /// entry, which merely forces the next request to re-read the database.
    /// </summary>
    [HttpPost("session/evict")]
    [Authorize]
    public async Task<IActionResult> EvictSessionCache([FromServices] App.Backend.Api.Services.ICacheService? cache = null)
    {
        var token = App.Backend.Api.Security.BetterAuthHandler.ResolveSessionToken(Request);
        if (token != null && cache != null)
        {
            await cache.RemoveAsync(App.Backend.Api.Security.BetterAuthHandler.SessionCacheKey(token));
        }
        return NoContent();
    }

    /// <summary>Policies whose holders administer identities across organizations (same rule as SearchController).</summary>
    private static readonly string[] DirectoryWidePolicies =
    {
        App.Contracts.Security.AuthorizationPolicies.SystemAdministration,
        App.Contracts.Security.AuthorizationPolicies.ItAdministration,
        App.Contracts.Security.AuthorizationPolicies.EngineeringAdministration
    };

    /// <summary>
    /// User directory (id, name, e-mail, role). Requires a signed-in caller (it used to be anonymous).
    /// System, IT and engineering administrators see every user; everyone else only the users
    /// they share an organization with, and themselves.
    /// </summary>
    [HttpGet("users")]
    [Authorize]
    public async Task<IActionResult> GetUsers(
        [FromServices] App.Shared.Data.AppDbContext db,
        [FromServices] IAuthorizationService? authorization = null)
    {
        var seesEveryone = false;
        if (authorization != null)
        {
            foreach (var policy in DirectoryWidePolicies)
            {
                if ((await authorization.AuthorizeAsync(User, policy)).Succeeded) { seesEveryone = true; break; }
            }
        }

        IQueryable<App.Shared.Entities.AuthUser> visible = db.AuthUsers;
        if (!seesEveryone)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            visible = visible.Where(u => u.Id == callerId || u.Members.Any(m =>
                db.AuthMembers.Any(mine => mine.UserId == callerId && mine.OrganizationId == m.OrganizationId)));
        }

        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            visible.Select(u => new
            {
                id = u.Id,
                name = u.Name,
                email = u.Email,
                role = u.Role
            }));
        return Ok(new { success = true, users });
    }

    /// <summary>
    /// Dev-only mock authentication endpoint for integration testing and local development.
    /// Strictly guarded behind EnableDevFeatures.
    /// </summary>
    [HttpPost("dev-login")]
    public IActionResult DevLogin([FromBody] DevLoginRequest? request)
    {
        if (!_featureFlags.EnableDevFeatures)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new App.Shared.Errors.ApiError(
                App.Shared.Errors.ErrorCode.AccessDenied,
                "Dev login bypass is disabled in production mode. Set HEIMDALL_ENABLE_DEV=true to enable."));
        }

        var username = request?.Username ?? "admin@factory.corp";
        var role = request?.Role ?? "system_admin";

        return Ok(new
        {
            Success = true,
            Message = "Dev authentication bypass granted.",
            User = username,
            Role = role,
            Token = $"dev_mock_token_{Guid.NewGuid():N}"
        });
    }

    /// <summary>
    /// Dev-only user impersonation endpoint for frontend testing.
    /// Strictly guarded behind EnableDevFeatures.
    /// </summary>
    [HttpPost("dev-impersonate")]
    public IActionResult DevImpersonate([FromBody] DevLoginRequest request)
    {
        if (!_featureFlags.EnableDevFeatures)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new App.Shared.Errors.ApiError(
                App.Shared.Errors.ErrorCode.AccessDenied,
                "Dev role impersonation is disabled in production mode. Set HEIMDALL_ENABLE_DEV=true to enable."));
        }

        return Ok(new
        {
            Success = true,
            Message = $"Impersonating {request.Username} with role {request.Role}.",
            ActiveUser = request.Username,
            ActiveRole = request.Role
        });
    }
}

public class DevLoginRequest
{
    public string? Username { get; set; }
    public string? Role { get; set; }
}
