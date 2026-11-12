using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Security;
using App.Backend.Api.Services;
using App.Contracts.Configuration;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Tests;

/// <summary>
/// Regression tests for the identity review: who the backend authenticates, which organization a
/// session is scoped to, and which identity endpoints require a signed-in caller.
/// </summary>
public class IdentityAuthorizationTests
{
    private const string ValidAutomationKey = "hmd_auto_0123456789abcdef0123456789abcdef";

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppDbContext(_options));
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public FakeEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "heimdall-tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<BetterAuthOptions>
    {
        public BetterAuthOptions CurrentValue { get; } = new();
        public BetterAuthOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<BetterAuthOptions, string?> listener) => null;
    }

    private static TestDbContextFactory NewDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"Identity_{Guid.NewGuid():N}").Options);

    private static ICacheService NewCache() => new CacheService(
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<CacheService>.Instance);

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    private static HttpContext Request(Action<HttpRequest>? configure = null)
    {
        var ctx = new DefaultHttpContext();
        configure?.Invoke(ctx.Request);
        return ctx;
    }

    private static HttpContext WithSessionCookie(string token) =>
        Request(r => r.Headers["Cookie"] = $"{BetterAuthHandler.SessionCookieName}={token}.c2lnbmF0dXJl");

    private static async Task<AuthenticateResult> AuthenticateAsync(
        HttpContext ctx,
        IDbContextFactory<AppDbContext> factory,
        string environment,
        ICacheService? cache = null,
        IConfiguration? configuration = null,
        BackendFeatureFlags? featureFlags = null)
    {
        var handler = new BetterAuthHandler(
            new StaticOptionsMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default,
            factory, new FakeEnvironment(environment), cache, configuration, featureFlags);
        await handler.InitializeAsync(new AuthenticationScheme("BetterAuth", null, typeof(BetterAuthHandler)), ctx);
        return await handler.AuthenticateAsync();
    }

    private static async Task SeedSessionAsync(
        IDbContextFactory<AppDbContext> factory, string token, string userId, string? role,
        string? activeOrganizationId = null, string[]? memberOf = null, DateTimeOffset? expiresAt = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.AuthUsers.Add(new AuthUser { Id = userId, Name = $"User {userId}", Email = $"{userId}@factory.test", Role = role });
        foreach (var org in (memberOf ?? Array.Empty<string>()).Append(activeOrganizationId).Where(o => o != null).Distinct())
        {
            if (!await db.AuthOrganizations.AnyAsync(o => o.Id == org)) db.AuthOrganizations.Add(new AuthOrganization { Id = org!, Name = org! });
        }
        foreach (var org in memberOf ?? Array.Empty<string>())
        {
            db.AuthMembers.Add(new AuthMember { Id = $"mem-{userId}-{org}", OrganizationId = org, UserId = userId });
        }
        db.AuthSessions.Add(new AuthSession
        {
            Id = $"sess-{token}",
            Token = token,
            UserId = userId,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            ActiveOrganizationId = activeOrganizationId
        });
        await db.SaveChangesAsync();
    }

    private static string[] Roles(AuthenticateResult result) =>
        result.Principal!.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

    // ------------------------------------------------------------------------------------------
    // 1. Automation API key: used to be "any header value starting with hmd_ is a system admin".
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Production", "X-Api-Key")]
    [InlineData("Staging", "X-Api-Key")]
    [InlineData("Production", "X-Extension-Key")]
    [InlineData("Development", "X-Api-Key")]
    public async Task ArbitraryAutomationKey_IsRejected_WhenNoKeyIsConfigured(string environment, string header)
    {
        var ctx = Request(r => r.Headers[header] = "hmd_anything");

        var result = await AuthenticateAsync(ctx, NewDatabase(), environment, configuration: Config());

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task WrongAutomationKey_IsRejected_EvenWithAValidSessionAndConfiguredKeys()
    {
        var factory = NewDatabase();
        await SeedSessionAsync(factory, "tok-admin", "u-admin", HeimdallRoles.SystemAdmin);
        var ctx = WithSessionCookie("tok-admin");
        ctx.Request.Headers["X-Api-Key"] = ValidAutomationKey + "x";

        var result = await AuthenticateAsync(ctx, factory, "Production",
            configuration: Config((BetterAuthHandler.AutomationApiKeysConfigKey, ValidAutomationKey)));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ConfiguredAutomationKey_AuthenticatesTheAutomationBot()
    {
        var ctx = Request(r => r.Headers["X-Api-Key"] = ValidAutomationKey);

        var result = await AuthenticateAsync(ctx, NewDatabase(), "Production",
            configuration: Config((BetterAuthHandler.AutomationApiKeysConfigKey, $"hmd_too_short, {ValidAutomationKey}")));

        Assert.True(result.Succeeded);
        Assert.Equal(BetterAuthHandler.AutomationBotUserId, result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Contains(HeimdallRoles.SystemAdmin, Roles(result));
    }

    [Theory]
    [InlineData("hmd_too_short")]                         // below the minimum length
    [InlineData("auto_0123456789abcdef0123456789abcdef")]  // configured without the hmd_ prefix
    public async Task WeakConfiguredAutomationKeys_AreIgnored(string configuredKey)
    {
        var presented = configuredKey.StartsWith("hmd_") ? configuredKey : "hmd_" + configuredKey;
        var ctx = Request(r => r.Headers["X-Api-Key"] = presented);

        var result = await AuthenticateAsync(ctx, NewDatabase(), "Production",
            configuration: Config((BetterAuthHandler.AutomationApiKeysConfigKey, configuredKey)));

        Assert.False(result.Succeeded);
    }

    // ------------------------------------------------------------------------------------------
    // 2. Development shortcuts
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task AnonymousRequest_IsNotAuthenticated_OutsideDevelopment(string environment)
    {
        var result = await AuthenticateAsync(Request(), NewDatabase(), environment);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task UnknownOrExpiredSession_Fails_OutsideDevelopment(string environment)
    {
        var factory = NewDatabase();
        await SeedSessionAsync(factory, "tok-expired", "u1", HeimdallRoles.SystemAdmin, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.NotNull((await AuthenticateAsync(WithSessionCookie("tok-unknown"), factory, environment)).Failure);
        Assert.NotNull((await AuthenticateAsync(WithSessionCookie("tok-expired"), factory, environment)).Failure);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public async Task AnonymousRequest_RunsAsDevAdministrator_InDevelopmentAndTest(string environment)
    {
        var result = await AuthenticateAsync(Request(), NewDatabase(), environment);

        Assert.True(result.Succeeded);
        Assert.Equal(BetterAuthHandler.DevAdminUserId, result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Contains(HeimdallRoles.SystemAdmin, Roles(result));
    }

    [Fact]
    public async Task DevAdministratorFallback_HonoursTheDevFeaturesKillSwitch()
    {
        var devFeaturesOff = new BackendFeatureFlags { EnableDevFeatures = false };

        var anonymous = await AuthenticateAsync(Request(), NewDatabase(), "Development", featureFlags: devFeaturesOff);
        var unknownToken = await AuthenticateAsync(WithSessionCookie("tok-unknown"), NewDatabase(), "Development", featureFlags: devFeaturesOff);

        Assert.False(anonymous.Succeeded);
        Assert.False(unknownToken.Succeeded);
    }

    // ------------------------------------------------------------------------------------------
    // 3. Session -> role / organization claims
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ValidSession_CarriesRoleAndActiveOrganization_WhenTheUserIsAMember()
    {
        var factory = NewDatabase();
        await SeedSessionAsync(factory, "tok-1", "u1", HeimdallRoles.Technician, activeOrganizationId: "org-a", memberOf: new[] { "org-a" });

        var result = await AuthenticateAsync(WithSessionCookie("tok-1"), factory, "Production");

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { HeimdallRoles.Technician }, Roles(result));
        Assert.Equal("org-a", result.Principal!.FindFirstValue(TenantScopeMiddleware.OrgClaimType));
    }

    [Fact]
    public async Task ActiveOrganization_IsDropped_WhenTheMembershipNoLongerExists()
    {
        var factory = NewDatabase();
        // Session still points at org-b, but the user was removed from it (only org-a remains).
        await SeedSessionAsync(factory, "tok-2", "u2", HeimdallRoles.Engineer, activeOrganizationId: "org-b", memberOf: new[] { "org-a" });

        var result = await AuthenticateAsync(WithSessionCookie("tok-2"), factory, "Production");

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst(TenantScopeMiddleware.OrgClaimType));
    }

    [Fact]
    public async Task UserWithoutRole_GetsTheLeastPrivilegedRole()
    {
        var factory = NewDatabase();
        await SeedSessionAsync(factory, "tok-3", "u3", role: null);

        var result = await AuthenticateAsync(WithSessionCookie("tok-3"), factory, "Production");

        Assert.Equal(new[] { HeimdallRoles.User }, Roles(result));
    }

    [Fact]
    public async Task BearerToken_IsAcceptedLikeTheCookie()
    {
        var factory = NewDatabase();
        await SeedSessionAsync(factory, "tok-4", "u4", HeimdallRoles.Operator);
        var ctx = Request(r => r.Headers["Authorization"] = "Bearer tok-4");

        var result = await AuthenticateAsync(ctx, factory, "Production");

        Assert.Equal(new[] { HeimdallRoles.Operator }, Roles(result));
        Assert.Equal("tok-4", BetterAuthHandler.ResolveSessionToken(ctx.Request));
    }

    // ------------------------------------------------------------------------------------------
    // 4. Session cache: role / organization changes must be visible once the entry is evicted
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task RoleAndOrganizationChange_BecomeVisible_AfterTheSessionCacheEntryIsEvicted()
    {
        var factory = NewDatabase();
        var cache = NewCache();
        await SeedSessionAsync(factory, "tok-cache", "u-cache", HeimdallRoles.Technician, activeOrganizationId: "org-a", memberOf: new[] { "org-a", "org-b" });

        var first = await AuthenticateAsync(WithSessionCookie("tok-cache"), factory, "Production", cache);
        Assert.Equal(new[] { HeimdallRoles.Technician }, Roles(first));

        // Better-Auth (Nitro) changes the role and the active organization directly in the auth schema.
        await using (var db = await factory.CreateDbContextAsync())
        {
            (await db.AuthUsers.SingleAsync(u => u.Id == "u-cache")).Role = HeimdallRoles.LeadEngineer;
            (await db.AuthSessions.SingleAsync(s => s.Token == "tok-cache")).ActiveOrganizationId = "org-b";
            await db.SaveChangesAsync();
        }

        // Without invalidation the backend keeps serving the cached role and organization.
        var stale = await AuthenticateAsync(WithSessionCookie("tok-cache"), factory, "Production", cache);
        Assert.Equal(new[] { HeimdallRoles.Technician }, Roles(stale));
        Assert.Equal("org-a", stale.Principal!.FindFirstValue(TenantScopeMiddleware.OrgClaimType));

        // The BFF evicts the entry by calling the endpoint with the affected session's token.
        var controller = new AuthController(new BackendFeatureFlags())
        {
            ControllerContext = new ControllerContext { HttpContext = Request(r => r.Headers["Authorization"] = "Bearer tok-cache") }
        };
        Assert.IsType<NoContentResult>(await controller.EvictSessionCache(cache));

        var fresh = await AuthenticateAsync(WithSessionCookie("tok-cache"), factory, "Production", cache);
        Assert.Equal(new[] { HeimdallRoles.LeadEngineer }, Roles(fresh));
        Assert.Equal("org-b", fresh.Principal!.FindFirstValue(TenantScopeMiddleware.OrgClaimType));
    }

    [Fact]
    public async Task RevokedSession_StopsAuthenticating_AfterEviction()
    {
        var factory = NewDatabase();
        var cache = NewCache();
        await SeedSessionAsync(factory, "tok-revoked", "u-revoked", HeimdallRoles.Engineer);
        Assert.True((await AuthenticateAsync(WithSessionCookie("tok-revoked"), factory, "Production", cache)).Succeeded);

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.AuthSessions.Remove(await db.AuthSessions.SingleAsync(s => s.Token == "tok-revoked"));
            await db.SaveChangesAsync();
        }
        await cache.RemoveAsync(BetterAuthHandler.SessionCacheKey("tok-revoked"));

        Assert.False((await AuthenticateAsync(WithSessionCookie("tok-revoked"), factory, "Production", cache)).Succeeded);
    }

    // ------------------------------------------------------------------------------------------
    // 5. Identity endpoints require a signed-in caller and respect tenant boundaries
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(typeof(AuthController), nameof(AuthController.GetUsers))]
    [InlineData(typeof(AuthController), nameof(AuthController.GetMe))]
    [InlineData(typeof(AuthController), nameof(AuthController.EvictSessionCache))]
    [InlineData(typeof(OrganizationController), nameof(OrganizationController.GetOrganizations))]
    [InlineData(typeof(OrganizationController), nameof(OrganizationController.GetMyOrganizations))]
    [InlineData(typeof(OrganizationController), nameof(OrganizationController.GetOrganization))]
    [InlineData(typeof(OrganizationController), nameof(OrganizationController.GetAllOrganizations))]
    public void IdentityEndpoints_RequireAuthentication(Type controller, string action)
    {
        var method = controller.GetMethod(action)!;
        var authorize = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.NotEmpty(authorize);
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
    }

    private static IAuthorizationService BuildAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AuthorizationPolicies.SystemAdministration,
            p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.PlantDirector)));
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static async Task<List<string>> VisibleOrganizationsAsync(string userId, string role)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"Orgs_{Guid.NewGuid():N}").Options;
        await using var db = new AppDbContext(options);
        db.AuthUsers.AddRange(
            new AuthUser { Id = "u-a", Name = "A", Email = "a@factory.test", Role = HeimdallRoles.Technician },
            new AuthUser { Id = "u-admin", Name = "Admin", Email = "admin@factory.test", Role = HeimdallRoles.SystemAdmin });
        db.AuthOrganizations.AddRange(
            new AuthOrganization { Id = "org-a", Name = "Line A" },
            new AuthOrganization { Id = "org-b", Name = "Line B" });
        db.AuthMembers.Add(new AuthMember { Id = "m1", OrganizationId = "org-a", UserId = "u-a" });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));
        var controller = new OrganizationController(db, BuildAuthorization())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

        var ok = Assert.IsType<OkObjectResult>(await controller.GetOrganizations());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        return json.RootElement.GetProperty("organizations").EnumerateArray()
            .Select(o => o.GetProperty("id").GetString()!)
            .OrderBy(id => id)
            .ToList();
    }

    // ------------------------------------------------------------------------------------------
    // 6. Cached tenant-filtered results are kept apart per tenant scope
    // ------------------------------------------------------------------------------------------

    private static async Task<string> ScopedKeyForAsync(ClaimsPrincipal user, string key)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"Scope_{Guid.NewGuid():N}").Options;
        await using var db = new AppDbContext(options);
        var ctx = new DefaultHttpContext { User = user };
        await new TenantScopeMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx, db, BuildAuthorization());
        return TenantScopeMiddleware.ScopedCacheKey(ctx, key);
    }

    private static ClaimsPrincipal Principal(string role, string? org, string userId = "u")
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Role, role) };
        if (org != null) claims.Add(new Claim(TenantScopeMiddleware.OrgClaimType, org));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task TenantFilteredCacheKeys_DifferPerOrganization_AndFromThePlatformWideEntry()
    {
        var orgA = await ScopedKeyForAsync(Principal(HeimdallRoles.Technician, "org-a"), "inventory:client_pcs");
        var orgB = await ScopedKeyForAsync(Principal(HeimdallRoles.Technician, "org-b"), "inventory:client_pcs");
        var noOrg = await ScopedKeyForAsync(Principal(HeimdallRoles.Technician, null), "inventory:client_pcs");
        var admin = await ScopedKeyForAsync(Principal(HeimdallRoles.SystemAdmin, "org-a"), "inventory:client_pcs");

        Assert.Equal("inventory:client_pcs:tenant:org-a", orgA);
        Assert.Equal(4, new HashSet<string> { orgA, orgB, noOrg, admin }.Count);
        Assert.Equal("inventory:client_pcs:tenant:all", admin);
        Assert.Equal(admin, TenantScopeMiddleware.ScopedCacheKey(null, "inventory:client_pcs"));
        // Pattern invalidation ("inventory:*") still covers every scoped entry.
        Assert.All(new[] { orgA, orgB, noOrg, admin }, k => Assert.StartsWith("inventory:", k));
    }

    private static async Task<List<string>> VisibleUsersAsync(string callerId, string role)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"Users_{Guid.NewGuid():N}").Options;
        await using var db = new AppDbContext(options);
        db.AuthUsers.AddRange(
            new AuthUser { Id = "u-a1", Name = "A1", Email = "a1@factory.test", Role = HeimdallRoles.Technician },
            new AuthUser { Id = "u-a2", Name = "A2", Email = "a2@factory.test", Role = HeimdallRoles.Engineer },
            new AuthUser { Id = "u-b1", Name = "B1", Email = "b1@factory.test", Role = HeimdallRoles.Technician },
            new AuthUser { Id = "u-admin", Name = "Admin", Email = "admin@factory.test", Role = HeimdallRoles.EngineeringAdmin });
        db.AuthOrganizations.AddRange(
            new AuthOrganization { Id = "org-a", Name = "Line A" },
            new AuthOrganization { Id = "org-b", Name = "Line B" });
        db.AuthMembers.AddRange(
            new AuthMember { Id = "m1", OrganizationId = "org-a", UserId = "u-a1" },
            new AuthMember { Id = "m2", OrganizationId = "org-a", UserId = "u-a2" },
            new AuthMember { Id = "m3", OrganizationId = "org-b", UserId = "u-b1" });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(o =>
        {
            o.AddPolicy(AuthorizationPolicies.SystemAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.PlantDirector));
            o.AddPolicy(AuthorizationPolicies.ItAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.ItAdmin, HeimdallRoles.ItSiteAdmin));
            o.AddPolicy(AuthorizationPolicies.EngineeringAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.SeniorEngineeringManager));
        });
        var authorization = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();

        var controller = new AuthController(new BackendFeatureFlags())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(role, null, callerId) } }
        };
        var ok = Assert.IsType<OkObjectResult>(await controller.GetUsers(db, authorization));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        return json.RootElement.GetProperty("users").EnumerateArray()
            .Select(u => u.GetProperty("id").GetString()!)
            .OrderBy(id => id)
            .ToList();
    }

    [Fact]
    public async Task UserDirectory_ShowsOnlyColleaguesOfTheSameOrganization_ToNonAdministrators()
    {
        Assert.Equal(new[] { "u-a1", "u-a2" }, await VisibleUsersAsync("u-a1", HeimdallRoles.Technician));
        Assert.Equal(new[] { "u-b1" }, await VisibleUsersAsync("u-b1", HeimdallRoles.Technician));
    }

    [Theory]
    [InlineData(HeimdallRoles.SystemAdmin)]
    [InlineData(HeimdallRoles.ItAdmin)]
    [InlineData(HeimdallRoles.EngineeringAdmin)]
    [InlineData(HeimdallRoles.PlantDirector)]
    public async Task UserDirectory_ShowsEveryUser_ToIdentityAdministrators(string role)
    {
        Assert.Equal(new[] { "u-a1", "u-a2", "u-admin", "u-b1" }, await VisibleUsersAsync("u-admin", role));
    }

    [Fact]
    public async Task OrganizationDirectory_ShowsOnlyOwnOrganizations_ToNonAdministrators()
    {
        Assert.Equal(new[] { "org-a" }, await VisibleOrganizationsAsync("u-a", HeimdallRoles.Technician));
    }

    [Theory]
    [InlineData(HeimdallRoles.SystemAdmin)]
    [InlineData(HeimdallRoles.HeimdallAdmin)]
    [InlineData(HeimdallRoles.Admin)]
    [InlineData(HeimdallRoles.PlantDirector)]
    public async Task OrganizationDirectory_ShowsEveryOrganization_ToPlatformAdministrators(string role)
    {
        Assert.Equal(new[] { "org-a", "org-b" }, await VisibleOrganizationsAsync("u-admin", role));
    }

    [Theory]
    [InlineData(HeimdallRoles.EngineeringAdmin)]
    [InlineData(HeimdallRoles.ItAdmin)]
    public async Task OrganizationDirectory_TreatsOtherAdminRolesAsTenantScoped(string role)
    {
        // "admin" in the role name does not make a platform administrator: only the
        // SystemAdministration policy does. A non-member therefore sees no organization.
        Assert.Empty(await VisibleOrganizationsAsync("u-admin", role));
    }
}
