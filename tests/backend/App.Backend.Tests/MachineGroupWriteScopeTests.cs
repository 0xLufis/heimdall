using System.Security.Claims;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Security;
using App.Contracts.Enums;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// Machine groups: reads are global, writes are scoped to the group's organization (platform
/// administrators: any group; organization members: that organization's groups; groups without an
/// organization: platform and engineering administrators).
/// </summary>
public class MachineGroupWriteScopeTests
{
    private static readonly Guid LineA = Guid.NewGuid();
    private static readonly Guid LineB = Guid.NewGuid();
    private static readonly Guid Fleet = Guid.NewGuid();

    internal static IAuthorizationService BuildAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(o =>
        {
            o.AddPolicy(AuthorizationPolicies.SystemAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.PlantDirector));
            o.AddPolicy(AuthorizationPolicies.EngineeringAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.SeniorEngineeringManager));
        });
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    /// <summary>Controller acting as the given caller (default: a system administrator).</summary>
    internal static MachineGroupController ControllerFor(
        IMachineGroupRepository repository, string role = HeimdallRoles.SystemAdmin, string userId = "u-admin", string? activeOrganizationId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Role, role) };
        if (activeOrganizationId != null) claims.Add(new Claim(TenantScopeMiddleware.OrgClaimType, activeOrganizationId));
        return new MachineGroupController(repository, BuildAuthorization())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
    }

    private static async Task<AppDbContext> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"MgScope_{Guid.NewGuid():N}").Options);
        db.AuthOrganizations.AddRange(new AuthOrganization { Id = "org-a", Name = "Line A" }, new AuthOrganization { Id = "org-b", Name = "Line B" });
        db.AuthUsers.AddRange(
            new AuthUser { Id = "u-a", Name = "Engineer A", Email = "a@factory.test", Role = HeimdallRoles.Engineer },
            new AuthUser { Id = "u-b", Name = "Engineer B", Email = "b@factory.test", Role = HeimdallRoles.Engineer },
            new AuthUser { Id = "u-eng", Name = "Engineering Admin", Email = "eng@factory.test", Role = HeimdallRoles.EngineeringAdmin });
        db.AuthMembers.AddRange(
            new AuthMember { Id = "m-a", OrganizationId = "org-a", UserId = "u-a" },
            new AuthMember { Id = "m-b", OrganizationId = "org-b", UserId = "u-b" });
        db.MachineGroups.AddRange(
            new MachineGroup { Id = LineA, Name = "Line A", GroupType = MachineGroupType.Line, OrganizationId = "org-a", MachineIdsJson = "[]" },
            new MachineGroup { Id = LineB, Name = "Line B", GroupType = MachineGroupType.Line, OrganizationId = "org-b", MachineIdsJson = "[]" },
            new MachineGroup { Id = Fleet, Name = "Robot Fleet", GroupType = MachineGroupType.Technological, OrganizationId = null, MachineIdsJson = "[]" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static MachineGroup Edit(string name, string? organizationId = null, string machineIdsJson = "[]") =>
        new() { Name = name, OrganizationId = organizationId, MachineIdsJson = machineIdsJson };

    private static int Status(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => throw new InvalidOperationException(result.GetType().Name)
    };

    private static int Status<T>(ActionResult<T> result) => result.Result == null ? 200 : Status(result.Result);

    [Fact]
    public async Task Reads_AreGlobal_ForEverySignedInUser()
    {
        await using var db = await SeedAsync();
        var controller = ControllerFor(new MachineGroupRepository(db), HeimdallRoles.Technician, "u-b", "org-b");

        var all = Assert.IsType<List<MachineGroup>>(Assert.IsType<OkObjectResult>((await controller.GetAll(default)).Result).Value);
        Assert.Equal(3, all.Count);
        Assert.IsType<OkObjectResult>((await controller.GetById(LineA, default)).Result);
    }

    [Fact]
    public async Task OrganizationMembers_ChangeOnlyTheirOrganizationsGroups()
    {
        await using var db = await SeedAsync();
        var engineerA = ControllerFor(new MachineGroupRepository(db), HeimdallRoles.Engineer, "u-a", "org-a");

        Assert.Equal(200, Status(await engineerA.Update(LineA, Edit("Line A (renamed)", machineIdsJson: "[\"" + Guid.NewGuid() + "\"]"), default)));
        Assert.Equal(403, Status(await engineerA.Update(LineB, Edit("Hijacked", machineIdsJson: "[\"" + Guid.NewGuid() + "\"]"), default)));
        Assert.Equal(403, Status(await engineerA.Update(Fleet, Edit("Hijacked fleet"), default)));
        Assert.Equal(403, Status(await engineerA.Delete(LineB, default)));
        Assert.Equal(403, Status(await engineerA.Delete(Fleet, default)));

        db.ChangeTracker.Clear();
        Assert.Equal("Line B", (await db.MachineGroups.SingleAsync(g => g.Id == LineB)).Name);
        Assert.Equal("[]", (await db.MachineGroups.SingleAsync(g => g.Id == LineB)).MachineIdsJson);
        Assert.Equal("Line A (renamed)", (await db.MachineGroups.SingleAsync(g => g.Id == LineA)).Name);
        Assert.Equal(204, Status(await engineerA.Delete(LineA, default)));
    }

    [Fact]
    public async Task Members_CannotMoveAGroupToAnotherOrganization()
    {
        await using var db = await SeedAsync();
        var engineerA = ControllerFor(new MachineGroupRepository(db), HeimdallRoles.Engineer, "u-a", "org-a");

        Assert.Equal(200, Status(await engineerA.Update(LineA, Edit("Line A", organizationId: "org-b"), default)));
        Assert.Equal(200, Status(await engineerA.Update(LineA, Edit("Line A", organizationId: null), default)));

        db.ChangeTracker.Clear();
        Assert.Equal("org-a", (await db.MachineGroups.SingleAsync(g => g.Id == LineA)).OrganizationId);
    }

    [Fact]
    public async Task PlatformManagedGroups_AreChangedByPlatformAndEngineeringAdministratorsOnly()
    {
        await using var db = await SeedAsync();
        var engAdmin = ControllerFor(new MachineGroupRepository(db), HeimdallRoles.EngineeringAdmin, "u-eng");

        Assert.Equal(200, Status(await engAdmin.Update(Fleet, Edit("Robot Fleet (renamed)"), default)));
        // Not a member of org-a: an engineering administrator does not change a line's own groups.
        Assert.Equal(403, Status(await engAdmin.Update(LineA, Edit("Line A?"), default)));

        var leadEngineerB = ControllerFor(new MachineGroupRepository(db), HeimdallRoles.LeadEngineer, "u-b", "org-b");
        Assert.Equal(403, Status(await leadEngineerB.Update(Fleet, Edit("Robot Fleet?"), default)));
    }

    [Theory]
    [InlineData(HeimdallRoles.SystemAdmin)]
    [InlineData(HeimdallRoles.HeimdallAdmin)]
    [InlineData(HeimdallRoles.Admin)]
    public async Task PlatformAdministrators_ChangeAnyGroup_AndMoveItBetweenOrganizations(string role)
    {
        await using var db = await SeedAsync();
        var admin = ControllerFor(new MachineGroupRepository(db), role);

        Assert.Equal(200, Status(await admin.Update(LineB, Edit("Line B", organizationId: "org-a"), default)));
        Assert.Equal(200, Status(await admin.Update(Fleet, Edit("Robot Fleet", organizationId: ""), default)));
        Assert.Equal(400, Status(await admin.Update(LineA, Edit("Line A", organizationId: "org-nowhere"), default)));
        Assert.Equal(204, Status(await admin.Delete(Fleet, default)));

        db.ChangeTracker.Clear();
        Assert.Equal("org-a", (await db.MachineGroups.SingleAsync(g => g.Id == LineB)).OrganizationId);
        Assert.Equal("org-a", (await db.MachineGroups.SingleAsync(g => g.Id == LineA)).OrganizationId);
    }

    private static async Task<(int Status, string? OrganizationId)> CreateAsync(
        AppDbContext db, string role, string userId, string? activeOrganizationId, string? postedOrganizationId)
    {
        var result = await ControllerFor(new MachineGroupRepository(db), role, userId, activeOrganizationId)
            .Create(new MachineGroup { Name = $"New {Guid.NewGuid():N}", OrganizationId = postedOrganizationId }, default);
        return result.Result is CreatedAtActionResult created
            ? (201, ((MachineGroup)created.Value!).OrganizationId)
            : (Status(result), null);
    }

    [Fact]
    public async Task NewGroups_BelongToTheCallersActiveOrganization_UnlessAPlatformAdministratorPicksOne()
    {
        await using var db = await SeedAsync();

        // Members: always the active organization, a posted one is ignored.
        Assert.Equal((201, "org-a"), await CreateAsync(db, HeimdallRoles.Engineer, "u-a", "org-a", null));
        Assert.Equal((201, "org-a"), await CreateAsync(db, HeimdallRoles.Engineer, "u-a", "org-a", "org-b"));
        // No active organization: platform-managed, which only platform / engineering administrators may create.
        Assert.Equal(403, (await CreateAsync(db, HeimdallRoles.Engineer, "u-a", null, null)).Status);
        Assert.Equal((201, null), await CreateAsync(db, HeimdallRoles.EngineeringAdmin, "u-eng", null, null));

        // Platform administrators: the posted organization, empty for platform-managed, else the active one.
        Assert.Equal((201, "org-b"), await CreateAsync(db, HeimdallRoles.SystemAdmin, "u-admin", "org-a", "org-b"));
        Assert.Equal((201, null), await CreateAsync(db, HeimdallRoles.SystemAdmin, "u-admin", "org-a", ""));
        Assert.Equal((201, "org-a"), await CreateAsync(db, HeimdallRoles.HeimdallAdmin, "u-admin", "org-a", null));
        Assert.Equal(400, (await CreateAsync(db, HeimdallRoles.SystemAdmin, "u-admin", null, "org-nowhere")).Status);
        // The development fallback principal's synthetic organization is not a real one.
        Assert.Equal((201, null), await CreateAsync(db, HeimdallRoles.SystemAdmin, "dev-admin", "Heimdall Root", null));
    }

    [Fact]
    public async Task Writes_FailClosed_WithoutAnAuthorizationService()
    {
        await using var db = await SeedAsync();
        var controller = new MachineGroupController(new MachineGroupRepository(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.Equal(403, Status(await controller.Update(Fleet, Edit("x"), default)));
        Assert.Equal(403, Status(await controller.Delete(LineA, default)));
        Assert.Equal(403, Status(await controller.Create(Edit("x"), default)));
    }
}
