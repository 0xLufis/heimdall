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

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromServices] App.Shared.Data.AppDbContext db)
    {
        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.AuthUsers.Select(u => new
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
