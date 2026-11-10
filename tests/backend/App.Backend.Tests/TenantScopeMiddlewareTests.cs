using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using App.Backend.Api.Security;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Tests;

/// <summary>
/// Regression tests for the multi-tenant query filters. Previously AppDbContext.CurrentOrganizationId
/// was never assigned, so every tenant could read every other tenant's rows.
/// </summary>
public class TenantScopeMiddlewareTests
{
    private static IAuthorizationService BuildAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AuthorizationPolicies.SystemAdministration,
            p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.PlantDirector)));
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal User(string? org, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "u1") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (org != null) claims.Add(new Claim(TenantScopeMiddleware.OrgClaimType, org));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static AppDbContext SeededContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantScope_{Guid.NewGuid():N}").Options;
        var db = new AppDbContext(options);
        db.MaintenanceTickets.AddRange(
            new MaintenanceTicket { Title = "org-a ticket", OrganizationId = "org-a" },
            new MaintenanceTicket { Title = "org-b ticket", OrganizationId = "org-b" },
            new MaintenanceTicket { Title = "global ticket", OrganizationId = null });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<List<string>> VisibleTitlesFor(ClaimsPrincipal user)
    {
        using var db = SeededContext();
        var ctx = new DefaultHttpContext { User = user };
        var middleware = new TenantScopeMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(ctx, db, BuildAuthorization());
        return await db.MaintenanceTickets.Select(t => t.Title).OrderBy(t => t).ToListAsync();
    }

    [Fact]
    public async Task OrgUser_SeesOnlyOwnAndGlobalRows()
    {
        var titles = await VisibleTitlesFor(User("org-a", HeimdallRoles.Technician));
        Assert.Equal(new[] { "global ticket", "org-a ticket" }, titles);
    }

    [Fact]
    public async Task PlatformAdmin_IsUnscoped()
    {
        var titles = await VisibleTitlesFor(User("org-a", HeimdallRoles.SystemAdmin));
        Assert.Equal(3, titles.Count);
    }

    [Fact]
    public async Task NonAdminWithoutOrg_FailsClosedToGlobalRows()
    {
        var titles = await VisibleTitlesFor(User(null, HeimdallRoles.Technician));
        Assert.Equal(new[] { "global ticket" }, titles);
    }

    [Fact]
    public async Task AnonymousRequest_LeavesScopeUntouched()
    {
        using var db = SeededContext();
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        await new TenantScopeMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx, db, BuildAuthorization());
        Assert.Null(db.CurrentOrganizationId);
    }
}
