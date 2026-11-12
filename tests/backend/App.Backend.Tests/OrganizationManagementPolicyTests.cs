using System.Security.Claims;
using System.Text.Encodings.Web;
using App.Backend.Api.Security;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// The OrganizationManagement policy as registered in Program.cs: Heimdall administrators and middle
/// managers and up by role, everyone else only through the explicit per-user "organizations.manage"
/// grant, which BetterAuthHandler turns into a permission claim.
/// </summary>
public class OrganizationManagementPolicyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrganizationManagementPolicyTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<bool> AllowedAsync(string role, params string[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "u"), new(ClaimTypes.Role, role) };
        claims.AddRange(permissions.Select(p => new Claim(HeimdallClaimTypes.Permission, p)));
        var authorization = _factory.Services.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), AuthorizationPolicies.OrganizationManagement)).Succeeded;
    }

    [Theory]
    [InlineData(HeimdallRoles.SystemAdmin)]
    [InlineData(HeimdallRoles.HeimdallAdmin)]
    [InlineData(HeimdallRoles.Admin)]
    [InlineData(HeimdallRoles.PlantDirector)]
    [InlineData(HeimdallRoles.EngineeringAdmin)]
    [InlineData(HeimdallRoles.PlantEngineeringManager)]
    [InlineData(HeimdallRoles.SeniorEngineeringManager)]
    [InlineData(HeimdallRoles.GroupLeader)]
    [InlineData(HeimdallRoles.Manager)]
    public async Task HeimdallAdministratorsAndMiddleManagers_HoldTheRightByRole(string role)
    {
        Assert.True(await AllowedAsync(role));
        Assert.Contains(role, OrganizationManagementRules.Roles);
    }

    [Theory]
    [InlineData(HeimdallRoles.ShiftLeader)]
    [InlineData(HeimdallRoles.LeadEngineer)]
    [InlineData(HeimdallRoles.ItAdmin)]
    [InlineData(HeimdallRoles.ItSiteAdmin)]
    [InlineData(HeimdallRoles.Technician)]
    [InlineData(HeimdallRoles.User)]
    public async Task OtherRoles_NeedTheExplicitGrant(string role)
    {
        Assert.False(await AllowedAsync(role));
        Assert.False(await AllowedAsync(role, "organizations.read", "system:settings:manage"));
        Assert.True(await AllowedAsync(role, HeimdallPermissions.OrganizationsManage));
    }

    [Fact]
    public void HeimdallAdministrators_AreASubsetOfThePolicyRoles()
    {
        Assert.Equal(new[] { HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin }, OrganizationManagementRules.HeimdallAdminRoles);
        Assert.All(OrganizationManagementRules.HeimdallAdminRoles, r => Assert.Contains(r, OrganizationManagementRules.Roles));
    }

    // ---------------------------------------------------------------------- session -> claims

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
    }

    private sealed class Env : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Monitor : IOptionsMonitor<BetterAuthOptions>
    {
        public BetterAuthOptions CurrentValue { get; } = new();
        public BetterAuthOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<BetterAuthOptions, string?> listener) => null;
    }

    [Fact]
    public async Task SessionOfAGrantedUser_CarriesOnePermissionClaimPerGrant()
    {
        var factory = new Factory(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"Perm_{Guid.NewGuid():N}").Options);
        await using (var db = factory.CreateDbContext())
        {
            db.AuthUsers.Add(new AuthUser { Id = "u-shift", Name = "Shift", Email = "shift@factory.test", Role = HeimdallRoles.ShiftLeader, Permissions = " Organizations.Manage, organizations.manage ,other.thing" });
            db.AuthSessions.Add(new AuthSession { Id = "s1", Token = "tok-shift", UserId = "u-shift", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
            await db.SaveChangesAsync();
        }
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "Bearer tok-shift";
        var handler = new BetterAuthHandler(new Monitor(), NullLoggerFactory.Instance, UrlEncoder.Default, factory, new Env(), null, null, null);
        await handler.InitializeAsync(new AuthenticationScheme("BetterAuth", null, typeof(BetterAuthHandler)), ctx);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "organizations.manage", "other.thing" },
            result.Principal!.FindAll(HeimdallClaimTypes.Permission).Select(c => c.Value));
        Assert.Empty(BetterAuthHandler.PermissionClaims(null));
        Assert.Empty(BetterAuthHandler.PermissionClaims(" , "));
    }
}
