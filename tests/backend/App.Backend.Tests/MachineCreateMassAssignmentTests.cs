using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using App.Backend.Api.Controllers.V1;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Tests;

/// <summary>POST /api/v1/machine must not persist server-controlled fields from the request body.</summary>
public class MachineCreateMassAssignmentTests
{
    private static (MachineController Controller, AppDbContext Db) Build(string orgId, params string[] roles)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MachineCreate_{Guid.NewGuid():N}").Options);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AuthorizationPolicies.SystemAdministration,
            p => p.RequireRole(HeimdallRoles.SystemAdmin)));
        var authz = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "u1"), new("OrgId", orgId) };
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
        var controller = new MachineController(new StationRepository(db), db, null, authz)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
        return (controller, db);
    }

    [Fact]
    public async Task ServerControlledFields_AreIgnored()
    {
        var (controller, db) = Build("org-a", HeimdallRoles.Engineer);
        var attackerId = Guid.NewGuid();
        var body = new Machine
        {
            Id = attackerId,
            Name = "OP-050 Press",
            CustomIdentifier = "L01-OP050",
            Status = "decommissioned",
            DecommissionReason = "forged",
            CreatedAt = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
            SystemTags = new List<string> { "forged-system-tag" },
        };

        var result = await controller.CreateMachine(body);

        var created = Assert.IsType<Machine>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.NotEqual(attackerId, created.Id);
        Assert.Equal("org-a", created.OrganizationId);
        Assert.Equal("active", created.Status);
        Assert.Null(created.DecommissionReason);
        Assert.DoesNotContain("forged-system-tag", created.SystemTags);
        Assert.Equal("L01-OP050", (await db.Machines.SingleAsync()).CustomIdentifier);
    }

    [Fact]
    public async Task CreatingInAnotherTenant_IsForbiddenForNonAdmins()
    {
        var (controller, _) = Build("org-a", HeimdallRoles.Engineer);
        var result = await controller.CreateMachine(new Machine { Name = "x", CustomIdentifier = "x", OrganizationId = "org-b" });
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task ParentMachineId_IsRejected_InFavourOfSubMachineEndpoint()
    {
        var (controller, _) = Build("org-a", HeimdallRoles.Engineer);
        var result = await controller.CreateMachine(new Machine { Name = "x", CustomIdentifier = "x", ParentMachineId = Guid.NewGuid() });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
