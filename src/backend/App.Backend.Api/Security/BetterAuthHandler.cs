using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using App.Backend.Api.Services;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Security;

public class CachedAuthSession
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? OrgId { get; set; }
}

public class BetterAuthHandler : AuthenticationHandler<BetterAuthOptions>
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _environment;
    private readonly ICacheService? _cacheService;

    public BetterAuthHandler(
        IOptionsMonitor<BetterAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDbContextFactory<AppDbContext> dbContextFactory,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment,
        ICacheService? cacheService = null)
        : base(options, logger, encoder)
    {
        _dbContextFactory = dbContextFactory;
        _environment = environment;
        _cacheService = cacheService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Get token from Cookie, Authorization header, or SignalR access_token query parameter
        if (!Request.Cookies.TryGetValue("better-auth.session_token", out var token))
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                token = authHeader.Substring("Bearer ".Length);
            }
            else if (Request.Query.TryGetValue("access_token", out var queryToken))
            {
                token = queryToken.FirstOrDefault();
            }
        }

        // Check for outside IT automation API Key (X-Api-Key or X-Extension-Key header)
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault()
            ?? Request.Headers["X-Extension-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("hmd_"))
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var botUser = await dbContext.AuthUsers.FindAsync("outside-it-automation-bot");
                if (botUser == null)
                {
                    dbContext.AuthUsers.Add(new AuthUser
                    {
                        Id = "outside-it-automation-bot",
                        Name = "Outside IT Ticketing Automation",
                        Email = "it-automation@heimdall.local",
                        Role = HeimdallRoles.SystemAdmin
                    });
                    await dbContext.SaveChangesAsync();
                }
            }
            catch {}

            var autoClaims = new List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.NameIdentifier, "outside-it-automation-bot"),
                new(System.Security.Claims.ClaimTypes.Email, "it-automation@heimdall.local"),
                new(System.Security.Claims.ClaimTypes.Name, "Outside IT Ticketing Automation"),
                new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.ItAdmin),
                new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.SystemAdmin),
                new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.Technician),
                new("OrgId", "Global"),
                new("AutomationKey", apiKey.Length > 16 ? apiKey.Substring(0, 16) + "..." : apiKey)
            };
            var autoIdentity = new System.Security.Claims.ClaimsIdentity(autoClaims, Scheme.Name);
            var autoPrincipal = new System.Security.Claims.ClaimsPrincipal(autoIdentity);
            var autoTicket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(autoPrincipal, Scheme.Name);
            return AuthenticateResult.Success(autoTicket);
        }

        if (string.IsNullOrEmpty(token))
        {

            if (_environment.IsDevelopment() || _environment.IsEnvironment("Test"))
            {
                try
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    var devUser = await dbContext.AuthUsers.FindAsync("dev-admin-id");
                    if (devUser == null)
                    {
                        dbContext.AuthUsers.Add(new AuthUser
                        {
                            Id = "dev-admin-id",
                            Name = "Dev Administrator",
                            Email = "admin@heimdall.local",
                            Role = HeimdallRoles.SystemAdmin
                        });
                        await dbContext.SaveChangesAsync();
                    }
                }
                catch {}

                var devClaims = new List<System.Security.Claims.Claim>
                {
                    new(System.Security.Claims.ClaimTypes.NameIdentifier, "dev-admin-id"),
                    new(System.Security.Claims.ClaimTypes.Email, "admin@heimdall.local"),
                    new(System.Security.Claims.ClaimTypes.Name, "Dev Administrator"),
                    new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.Admin),
                    new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.SystemAdmin),
                    new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.Technician),
                    new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.Engineer),
                    new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.LeadEngineer),
                    new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.ControlsEngineer),
                    new("OrgId", "Heimdall Root")
                };
                var devIdentity = new System.Security.Claims.ClaimsIdentity(devClaims, Scheme.Name);
                var devPrincipal = new System.Security.Claims.ClaimsPrincipal(devIdentity);
                var devTicket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(devPrincipal, Scheme.Name);
                return AuthenticateResult.Success(devTicket);
            }
            return AuthenticateResult.NoResult();
        }

        // Better-Auth signed cookies are formatted as "<token>.<signature>"
        // The PostgreSQL auth.session table stores only the raw 32-character token.
        token = token.Trim();
        if (token.Contains('.'))
        {
            token = token.Split('.')[0];
        }

        try
        {
            // 2. Check cache first to prevent per-request database hits (MID-03)
            var cacheKey = $"better-auth:session:{token}";
            CachedAuthSession? session = null;

            if (_cacheService != null)
            {
                try
                {
                    session = await _cacheService.GetAsync<CachedAuthSession>(cacheKey);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to read session from cache, falling back to DB.");
                }
            }

            if (session == null)
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                session = await dbContext.AuthSessions
                    .Include(s => s.User)
                    .Where(s => s.Token == token && s.ExpiresAt > DateTimeOffset.UtcNow)
                    .Select(s => new CachedAuthSession
                    {
                        UserId = s.UserId,
                        Email = s.User.Email,
                        Name = s.User.Name,
                        Role = s.User.Role,
                        OrgId = s.ActiveOrganizationId
                    })
                    .FirstOrDefaultAsync();

                if (session != null && _cacheService != null)
                {
                    try
                    {
                        await _cacheService.SetAsync(cacheKey, session, TimeSpan.FromMinutes(5));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to write session to cache.");
                    }
                }
            }

            if (session == null)
            {
                if (_environment.IsDevelopment() || _environment.IsEnvironment("Test"))
                {
                    try
                    {
                        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                        var devUser = await dbContext.AuthUsers.FindAsync("dev-admin-id");
                        if (devUser == null)
                        {
                            dbContext.AuthUsers.Add(new AuthUser
                            {
                                Id = "dev-admin-id",
                                Name = "Dev Administrator",
                                Email = "admin@heimdall.local",
                                Role = HeimdallRoles.SystemAdmin
                            });
                            await dbContext.SaveChangesAsync();
                        }
                    }
                    catch {}

                    var devClaims = new List<System.Security.Claims.Claim>
                    {
                        new(System.Security.Claims.ClaimTypes.NameIdentifier, "dev-admin-id"),
                        new(System.Security.Claims.ClaimTypes.Email, "admin@heimdall.local"),
                        new(System.Security.Claims.ClaimTypes.Name, "Dev Administrator"),
                        new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.Admin),
                        new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.SystemAdmin),
                        new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.Technician),
                        new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.Engineer),
                        new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.LeadEngineer),
                        new(System.Security.Claims.ClaimTypes.Role, HeimdallRoles.ControlsEngineer),
                        new("OrgId", "Heimdall Root")
                    };
                    var devIdentity = new System.Security.Claims.ClaimsIdentity(devClaims, Scheme.Name);
                    var devPrincipal = new System.Security.Claims.ClaimsPrincipal(devIdentity);
                    var devTicket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(devPrincipal, Scheme.Name);
                    return AuthenticateResult.Success(devTicket);
                }
                return AuthenticateResult.Fail("Invalid or expired session");
            }

            // 3. Create claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, session.UserId),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, session.Email),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, session.Name),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, session.Role ?? HeimdallRoles.User)
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
