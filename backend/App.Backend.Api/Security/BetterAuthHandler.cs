using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using App.Shared.Data;

namespace App.Backend.Api.Security;

public class BetterAuthHandler : AuthenticationHandler<BetterAuthOptions>
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public BetterAuthHandler(
        IOptionsMonitor<BetterAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDbContextFactory<AppDbContext> dbContextFactory)
        : base(options, logger, encoder)
    {
        _dbContextFactory = dbContextFactory;
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
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            // 2. Query the session table in PostgreSQL
            var session = await dbContext.AuthSessions
                .Include(s => s.User)
                .Where(s => s.Token == token && s.ExpiresAt > DateTimeOffset.UtcNow)
                .Select(s => new {
                    UserId = s.UserId,
                    Email = s.User.Email,
                    Name = s.User.Name,
                    Role = s.User.Role,
                    OrgId = s.ActiveOrganizationId
                })
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return AuthenticateResult.Fail("Invalid or expired session");
            }

            // 3. Create claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, session.UserId),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, session.Email),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, session.Name),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, session.Role ?? "user")
            };

            if (!string.IsNullOrEmpty(session.OrgId))
            {
                claims.Add(new System.Security.Claims.Claim("OrgId", session.OrgId));
            }

            var identity = new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name);
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating session token");
            return AuthenticateResult.Fail("Error validating session token");
        }
    }
}
