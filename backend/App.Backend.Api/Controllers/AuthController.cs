using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
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
