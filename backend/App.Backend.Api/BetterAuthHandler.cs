using System.Security.Claims;
using System.Text.Encodings.Web;
using App.Shared.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace App.Backend.Api.Security;

public class BetterAuthOptions : AuthenticationSchemeOptions
{
    // Better-Auth defaults to this cookie name
    public string CookieName { get; set; } = "better-auth.session_token";
}

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
        // 1. Try to extract the token from the Better-Auth cookie
        string? token = Request.Cookies[Options.CookieName];

        // 2. Fallback: Check the Authorization header (useful for API-to-API calls or native clients)
        if (string.IsNullOrEmpty(token) && Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var bearerPrefix = "Bearer ";
            var headerValue = authHeader.ToString();
            if (headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                token = headerValue.Substring(bearerPrefix.Length).Trim();
            }
        }

        // If no token is found, skip authentication for this scheme
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        // 3. Query the database to validate the session
        // We use AsNoTracking() for a performance boost since we are only reading
        var session = await _dbContext.Sessions
            .Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Token == token);

        // 4. Validate session existence and expiration
        if (session == null || session.ExpiresAt < DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Invalid or expired session.");
        }

        // 5. Construct the Identity Claims for the authorized user
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId),
            new Claim(ClaimTypes.Email, session.User.Email),
            new Claim(ClaimTypes.Name, session.User.Name)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
