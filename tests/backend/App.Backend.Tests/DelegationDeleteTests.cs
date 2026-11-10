using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using App.Contracts.Enums;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Tests;

public class DelegationDeleteTests
{
    private static AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"DelegationDelete_{Guid.NewGuid():N}").Options);

    [Fact]
    public async Task DeleteRecipe_RemovesBindingsAndJunctions()
    {
        using var db = NewContext();
        var machine = new Machine { Id = Guid.NewGuid(), Name = "M1", CustomIdentifier = "L01-OP010" };
        var group = new PersonnelGroup { Id = Guid.NewGuid(), Name = "Electrical A" };
        var recipe = new DelegationRecipe { Id = Guid.NewGuid(), Name = "R1" };
        db.AddRange(machine, group, recipe);
        db.MachineDelegationBindings.Add(new MachineDelegationBinding { MachineId = machine.Id, DelegationRecipeId = recipe.Id });
        db.DelegationMaintenanceGroups.Add(new DelegationMaintenanceGroup { DelegationRecipeId = recipe.Id, MaintenanceGroupId = group.Id });
        await db.SaveChangesAsync();

        var service = new DelegationRecipeService(db);
        Assert.True(await service.DeleteRecipeAsync(recipe.Id));

        Assert.Empty(await db.DelegationRecipes.ToListAsync());
        Assert.Empty(await db.MachineDelegationBindings.ToListAsync());
        Assert.Empty(await db.DelegationMaintenanceGroups.ToListAsync());
        Assert.Single(await db.PersonnelGroups.ToListAsync()); // the group itself survives
        Assert.False(await service.DeleteRecipeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeletePersonnelGroup_RefusedWhileReferencedByRecipe()
    {
        using var db = NewContext();
        var group = new PersonnelGroup { Id = Guid.NewGuid(), Name = "Mechanical B" };
        db.PersonnelGroups.Add(group);
        db.DelegationRecipes.Add(new DelegationRecipe { Name = "R1", EscalationGroupId = group.Id });
        await db.SaveChangesAsync();

        var service = new DelegationRecipeService(db);
        Assert.Equal(PersonnelGroupDeleteResult.InUse, await service.DeletePersonnelGroupAsync(group.Id));
        Assert.Single(await db.PersonnelGroups.ToListAsync());
    }

    [Fact]
    public async Task DeletePersonnelGroup_RemovesMembersWhenUnreferenced()
    {
        using var db = NewContext();
        var group = new PersonnelGroup { Id = Guid.NewGuid(), Name = "Night shift" };
        group.Members.Add(new PersonnelGroupMember { PersonnelGroupId = group.Id, UserId = "u1" });
        db.PersonnelGroups.Add(group);
        await db.SaveChangesAsync();

        var service = new DelegationRecipeService(db);
        Assert.Equal(PersonnelGroupDeleteResult.Deleted, await service.DeletePersonnelGroupAsync(group.Id));
        Assert.Empty(await db.PersonnelGroupMembers.ToListAsync());
        Assert.Equal(PersonnelGroupDeleteResult.NotFound, await service.DeletePersonnelGroupAsync(group.Id));
    }
}
