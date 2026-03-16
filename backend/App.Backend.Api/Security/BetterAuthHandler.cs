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
            // Since we haven't mapped the session table in AppDbContext yet,
            // we'll use a raw SQL query or add it to the DbContext.
            // For now, let's use raw SQL to verify existence and expiration.
            
            // Note: Better-auth tables are in "heimdall_dev_db" schema based on Nuxt config.
            
            var session = await _dbContext.Database
                .SqlQueryRaw<SessionInfo>(
                    @"SELECT s.id, s.user_id as UserId, u.email as Email, u.name as Name, u.role as Role
                      FROM heimdall_dev_db.session s
                      JOIN heimdall_dev_db.user u ON s.user_id = u.id
                      WHERE s.token = {0} AND s.expires_at > NOW()", 
                    token)
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

    private class SessionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
