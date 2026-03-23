using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using App.Shared.Data;

namespace App.Backend.Api.Security;

public class BetterAuthHandler : AuthenticationHandler<BetterAuthOptions>
{
    private readonly AppDbContext _dbContext;

    public BetterAuthHandler(
        IOptionsMonitor<BetterAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext dbContext)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Get token from Cookie or Authorization header
        if (!Request.Cookies.TryGetValue("better-auth.session_token", out var token))
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                token = authHeader.Substring("Bearer ".Length);
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            // 2. Query the session table in PostgreSQL
            var session = await _dbContext.AuthSessions
                .Include(s => s.User)
                .Where(s => s.Token == token && s.ExpiresAt > DateTimeOffset.UtcNow)
                .Select(s => new {
                    UserId = s.UserId,
                    Email = s.User.Email,
                    Name = s.User.Name,
                    Role = s.User.Role
                })
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return AuthenticateResult.Fail("Invalid or expired session");
            }

            // 3. Create claims
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, session.UserId),
                new Claim(ClaimTypes.Email, session.Email),
                new Claim(ClaimTypes.Name, session.Name),
                new Claim(ClaimTypes.Role, session.Role ?? "user")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating session token");
            return AuthenticateResult.Fail("Error validating session token");
        }
    }
}
