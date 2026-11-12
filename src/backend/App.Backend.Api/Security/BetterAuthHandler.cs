using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using App.Backend.Api.Services;
using App.Contracts.Configuration;
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
    /// <summary>Explicit per-user permission grants, comma-separated (auth.user.permissions).</summary>
    public string? Permissions { get; set; }
    public string? OrgId { get; set; }
}

public class BetterAuthHandler : AuthenticationHandler<BetterAuthOptions>
{
    public const string SessionCookieName = "better-auth.session_token";

    /// <summary>
    /// Comma-separated list of outside-IT automation API keys the backend accepts
    /// (each must start with <c>hmd_</c> and be at least <see cref="MinAutomationApiKeyLength"/> characters).
    /// When unset, no API key is accepted.
    /// </summary>
    public const string AutomationApiKeysConfigKey = "HEIMDALL_AUTOMATION_API_KEYS";
    public const string AutomationApiKeyPrefix = "hmd_";
    public const int MinAutomationApiKeyLength = 32;

    public const string DevAdminUserId = "dev-admin-id";
    public const string AutomationBotUserId = "outside-it-automation-bot";

    /// <summary>How long a resolved session (role, active organization) is cached before the database is asked again.</summary>
    public static readonly TimeSpan SessionCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _environment;
    private readonly ICacheService? _cacheService;
    private readonly IConfiguration? _configuration;
    private readonly BackendFeatureFlags? _featureFlags;

    public BetterAuthHandler(
        IOptionsMonitor<BetterAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDbContextFactory<AppDbContext> dbContextFactory,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment,
        ICacheService? cacheService = null,
        IConfiguration? configuration = null,
        BackendFeatureFlags? featureFlags = null)
        : base(options, logger, encoder)
    {
        _dbContextFactory = dbContextFactory;
        _environment = environment;
        _cacheService = cacheService;
        _configuration = configuration;
        _featureFlags = featureFlags;
    }

    /// <summary>Cache key under which a resolved session is stored (L1 memory + Redis).</summary>
    public static string SessionCacheKey(string token) => $"better-auth:session:{token}";

    /// <summary>
    /// Extracts the bare Better-Auth session token from the cookie, the Authorization header or the
    /// SignalR <c>access_token</c> query parameter. Signed cookies are formatted as
    /// "&lt;token&gt;.&lt;signature&gt;"; the <c>auth.session</c> table stores only the raw token.
    /// </summary>
    public static string? ResolveSessionToken(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(SessionCookieName, out var token))
        {
            var authHeader = request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                token = authHeader.Substring("Bearer ".Length);
            }
            else if (request.Query.TryGetValue("access_token", out var queryToken))
            {
                token = queryToken.FirstOrDefault();
            }
        }

        if (string.IsNullOrWhiteSpace(token)) return null;

        token = token.Trim();
        if (token.Contains('.'))
        {
            token = token.Split('.')[0];
        }
        return string.IsNullOrEmpty(token) ? null : token;
    }

    /// <summary>
    /// The anonymous "Dev Administrator" principal exists for local development and the test host
    /// only. In Development it additionally honours the <c>HEIMDALL_ENABLE_DEV=false</c> kill switch,
    /// like every other dev shortcut.
    /// </summary>
    private bool DevFallbackEnabled =>
        _environment.IsEnvironment("Test")
        || (_environment.IsDevelopment() && (_featureFlags?.EnableDevFeatures ?? true));

    /// <summary>
    /// True when <paramref name="presentedKey"/> equals one of the configured automation keys.
    /// Compared in constant time; too-short or wrongly prefixed configured entries are ignored.
    /// </summary>
    private bool IsConfiguredAutomationKey(string presentedKey)
    {
        var configured = _configuration?[AutomationApiKeysConfigKey];
        if (string.IsNullOrWhiteSpace(configured)) return false;

        var presented = Encoding.UTF8.GetBytes(presentedKey);
        var matched = false;
        foreach (var candidate in configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (candidate.Length < MinAutomationApiKeyLength || !candidate.StartsWith(AutomationApiKeyPrefix, StringComparison.Ordinal))
            {
                continue;
            }
            var expected = Encoding.UTF8.GetBytes(candidate);
            if (expected.Length == presented.Length && CryptographicOperations.FixedTimeEquals(expected, presented))
            {
                matched = true;
            }
        }
        return matched;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Outside IT automation API key (X-Api-Key or X-Extension-Key header).
        // The key must be one of the configured secrets. Previously ANY value starting with "hmd_"
        // was accepted as a system administrator, in every environment.
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault()
            ?? Request.Headers["X-Extension-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey) && apiKey.StartsWith(AutomationApiKeyPrefix))
        {
            if (!IsConfiguredAutomationKey(apiKey.Trim()))
            {
                Logger.LogWarning("Rejected an automation API key that is not configured in {ConfigKey}.", AutomationApiKeysConfigKey);
                return AuthenticateResult.Fail("Invalid automation API key");
            }

            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var botUser = await dbContext.AuthUsers.FindAsync(AutomationBotUserId);
                if (botUser == null)
                {
                    dbContext.AuthUsers.Add(new AuthUser
                    {
                        Id = AutomationBotUserId,
                        Name = "Outside IT Ticketing Automation",
                        Email = "it-automation@heimdall.local",
                        Role = HeimdallRoles.SystemAdmin
                    });
                    await dbContext.SaveChangesAsync();
                }
            }
            catch {}

            var autoClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, AutomationBotUserId),
                new(ClaimTypes.Email, "it-automation@heimdall.local"),
                new(ClaimTypes.Name, "Outside IT Ticketing Automation"),
                new(ClaimTypes.Role, HeimdallRoles.ItAdmin),
                new(ClaimTypes.Role, HeimdallRoles.SystemAdmin),
                new(ClaimTypes.Role, HeimdallRoles.Technician),
                new(TenantScopeMiddleware.OrgClaimType, "Global"),
                new("AutomationKey", apiKey.Length > 16 ? apiKey.Substring(0, 16) + "..." : apiKey)
            };
            return Success(autoClaims);
        }

        // 2. Session token from Cookie, Authorization header, or SignalR access_token query parameter
        var token = ResolveSessionToken(Request);
        if (token == null)
        {
            return DevFallbackEnabled ? await DevAdminAsync() : AuthenticateResult.NoResult();
        }

        try
        {
            // 3. Check cache first to prevent per-request database hits (MID-03)
            var cacheKey = SessionCacheKey(token);
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
                    .Where(s => s.Token == token && s.ExpiresAt > DateTimeOffset.UtcNow)
                    .Select(s => new CachedAuthSession
                    {
                        UserId = s.UserId,
                        Email = s.User.Email,
                        Name = s.User.Name,
                        Role = s.User.Role,
                        Permissions = s.User.Permissions,
                        // The active organization is a session attribute and can outlive the membership
                        // (member removed, organization deleted, row written outside Better-Auth). Only
                        // honour it while the user is still a member, otherwise the tenant scope fails closed.
                        OrgId = s.ActiveOrganizationId != null
                                && dbContext.AuthMembers.Any(m => m.UserId == s.UserId && m.OrganizationId == s.ActiveOrganizationId)
                            ? s.ActiveOrganizationId
                            : null
                    })
                    .FirstOrDefaultAsync();

                if (session != null && _cacheService != null)
                {
                    try
                    {
                        await _cacheService.SetAsync(cacheKey, session, SessionCacheTtl);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to write session to cache.");
                    }
                }
            }

            if (session == null)
            {
                return DevFallbackEnabled
                    ? await DevAdminAsync()
                    : AuthenticateResult.Fail("Invalid or expired session");
            }

            // 4. Create claims
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.UserId),
                new(ClaimTypes.Email, session.Email),
                new(ClaimTypes.Name, session.Name),
                new(ClaimTypes.Role, session.Role ?? HeimdallRoles.User)
            };

            if (!string.IsNullOrEmpty(session.OrgId))
            {
                claims.Add(new Claim(TenantScopeMiddleware.OrgClaimType, session.OrgId));
            }

            claims.AddRange(PermissionClaims(session.Permissions));

            return Success(claims);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating session token");
            return DevFallbackEnabled
                ? await DevAdminAsync(ensureProfile: false)
                : AuthenticateResult.Fail("Error validating session token");
        }
    }

    /// <summary>
    /// One <see cref="HeimdallClaimTypes.Permission"/> claim per explicit grant stored (comma-separated)
    /// in <c>auth.user.permissions</c>, e.g. "organizations.manage" for the OrganizationManagement policy.
    /// </summary>
    public static IEnumerable<Claim> PermissionClaims(string? permissions) =>
        (permissions ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant())
            .Distinct()
            .Select(p => new Claim(HeimdallClaimTypes.Permission, p));

    private AuthenticateResult Success(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    /// <summary>Full-privilege principal used when no valid session is presented in Development / Test.</summary>
    private async Task<AuthenticateResult> DevAdminAsync(bool ensureProfile = true)
    {
        if (ensureProfile)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var devUser = await dbContext.AuthUsers.FindAsync(DevAdminUserId);
                if (devUser == null)
                {
                    dbContext.AuthUsers.Add(new AuthUser
                    {
                        Id = DevAdminUserId,
                        Name = "Dev Administrator",
                        Email = "admin@heimdall.local",
                        Role = HeimdallRoles.SystemAdmin
                    });
                    await dbContext.SaveChangesAsync();
                }
            }
            catch {}
        }

        return Success(new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, DevAdminUserId),
            new(ClaimTypes.Email, "admin@heimdall.local"),
            new(ClaimTypes.Name, "Dev Administrator"),
            new(ClaimTypes.Role, HeimdallRoles.Admin),
            new(ClaimTypes.Role, HeimdallRoles.SystemAdmin),
            new(ClaimTypes.Role, HeimdallRoles.Technician),
            new(ClaimTypes.Role, HeimdallRoles.Engineer),
            new(ClaimTypes.Role, HeimdallRoles.LeadEngineer),
            new(ClaimTypes.Role, HeimdallRoles.ControlsEngineer),
            new(TenantScopeMiddleware.OrgClaimType, "Heimdall Root")
        });
    }
}
