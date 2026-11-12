using App.Infrastructure.Repositories;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// The Lua bridge follows MachineGroupController's organisation write scope. Scripts run without a
/// user or organisation, so they may change the members of platform-managed groups (no
/// organisation, EngineeringAdministration's remit) and are refused for organisation-owned groups.
/// </summary>
public class LuaMachineGroupWriteScopeTests
{
    private static async Task<(AppDbContext Db, DbPlantDataProvider Provider, MachineGroup OrgGroup, MachineGroup PlatformGroup, Machine Machine)> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var orgGroup = new MachineGroup { Id = Guid.NewGuid(), Name = "Org Line", OrganizationId = "org-a" };
        var platformGroup = new MachineGroup { Id = Guid.NewGuid(), Name = "Laser fleet" };
        var machine = new Machine { Id = Guid.NewGuid(), Name = "OP10", OrganizationId = "org-a" };
        db.MachineGroups.AddRange(orgGroup, platformGroup);
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var provider = new DbPlantDataProvider(db, new MachineGroupRepository(db), new FakeMaintenanceTicketRepository(), NullLogger<DbPlantDataProvider>.Instance);
        return (db, provider, orgGroup, platformGroup, machine);
    }

    [Fact]
    public void Rule_AllowsOnlyGroupsWithoutOrganization()
    {
        Assert.True(DbPlantDataProvider.ScriptMayChangeMachineGroupMembers(new MachineGroup { Name = "platform" }));
        Assert.True(DbPlantDataProvider.ScriptMayChangeMachineGroupMembers(new MachineGroup { Name = "blank", OrganizationId = " " }));
        Assert.False(DbPlantDataProvider.ScriptMayChangeMachineGroupMembers(new MachineGroup { Name = "owned", OrganizationId = "org-a" }));
    }

    [Fact]
    public async Task Script_ChangesPlatformManagedGroupMembers_ById_AndByName()
    {
        var (db, provider, _, platformGroup, machine) = await SeedAsync();
        await using var _ = db;

        Assert.True(await provider.AddGroupMemberAsync(platformGroup.Id.ToString(), machine.Id.ToString()));
        Assert.True(await db.MachineGroupMemberships.AnyAsync(m => m.GroupId == platformGroup.Id && m.MachineId == machine.Id));
        Assert.True(await provider.IsGroupMemberAsync("Laser fleet", machine.Id.ToString()));

        Assert.True(await provider.RemoveGroupMemberAsync("Laser fleet", machine.Id.ToString()));
        Assert.False(await db.MachineGroupMemberships.AnyAsync(m => m.GroupId == platformGroup.Id));
    }

    [Fact]
    public async Task Script_IsRefusedForOrganizationOwnedGroups_AndLeavesTheirMembershipsAlone()
    {
        var (db, provider, orgGroup, _, machine) = await SeedAsync();
        await using var _ = db;

        Assert.False(await provider.AddGroupMemberAsync(orgGroup.Id.ToString(), machine.Id.ToString()));
        Assert.False(await provider.AddGroupMemberAsync("Org Line", machine.Id.ToString()));
        Assert.False(await db.MachineGroupMemberships.AnyAsync());

        // A membership set through the API stays: the script cannot remove it either.
        db.MachineGroupMemberships.Add(new MachineGroupMembership { GroupId = orgGroup.Id, MachineId = machine.Id, AssignedBy = "api" });
        await db.SaveChangesAsync();
        Assert.False(await provider.RemoveGroupMemberAsync(orgGroup.Id.ToString(), machine.Id.ToString()));
        Assert.True(await db.MachineGroupMemberships.AnyAsync(m => m.GroupId == orgGroup.Id && m.MachineId == machine.Id));

        // Reads are unaffected.
        Assert.Contains(machine.Id.ToString(), await provider.GetGroupMembersAsync(orgGroup.Id.ToString()));
    }
}
