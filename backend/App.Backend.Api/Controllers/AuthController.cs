using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for handling authentication-related API requests.
/// Provides endpoints for user authentication and authorization information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Retrieves information about the currently authenticated user.
    /// Requires authentication.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> containing user authentication status, name, roles, and claims.
    /// </returns>
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
}
