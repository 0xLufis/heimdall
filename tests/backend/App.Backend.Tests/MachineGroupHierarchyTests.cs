using App.Backend.Api.Controllers.V1;
using App.Contracts.Enums;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace App.Backend.Tests;

/// <summary>Machine group hierarchy rules: no parent cycles, and deleting a group keeps its sub-groups.</summary>
public class MachineGroupHierarchyTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static MachineGroup Group(string name, Guid? parentId = null) =>
        new() { Id = Guid.NewGuid(), Name = name, ParentId = parentId, GroupType = MachineGroupType.Manual };

    [Fact]
    public async Task Update_RejectsMovingAGroupUnderItselfOrItsOwnSubGroups()
    {
        await using var db = NewDb();
        var plant = Group("Plant");
        var line = Group("Line", plant.Id);
        var cell = Group("Cell", line.Id);
        var other = Group("Other");
        db.MachineGroups.AddRange(plant, line, cell, other);
        await db.SaveChangesAsync();
        var controller = MachineGroupWriteScopeTests.ControllerFor(new MachineGroupRepository(db));

        var underGrandchild = await controller.Update(plant.Id, new MachineGroup { Name = "Plant", ParentId = cell.Id }, default);
        Assert.IsType<BadRequestObjectResult>(underGrandchild.Result);
        var underItself = await controller.Update(line.Id, new MachineGroup { Name = "Line", ParentId = line.Id }, default);
        Assert.IsType<BadRequestObjectResult>(underItself.Result);
        Assert.Null((await db.MachineGroups.AsNoTracking().SingleAsync(g => g.Id == plant.Id)).ParentId);

        // A legal move still works.
        var moved = await controller.Update(cell.Id, new MachineGroup { Name = "Cell", ParentId = other.Id }, default);
        Assert.IsType<OkObjectResult>(moved.Result);
        Assert.Equal(other.Id, (await db.MachineGroups.AsNoTracking().SingleAsync(g => g.Id == cell.Id)).ParentId);
    }

    [Fact]
    public async Task Delete_MovesSubGroupsUpToTheDeletedGroupsParent()
    {
        await using var db = NewDb();
        var plant = Group("Plant");
        var line = Group("Line", plant.Id);
        var cellA = Group("Cell A", line.Id);
        var cellB = Group("Cell B", line.Id);
        db.MachineGroups.AddRange(plant, line, cellA, cellB);
        await db.SaveChangesAsync();
        var controller = MachineGroupWriteScopeTests.ControllerFor(new MachineGroupRepository(db));

        Assert.IsType<NoContentResult>(await controller.Delete(line.Id, default));

        var remaining = await db.MachineGroups.AsNoTracking().ToListAsync();
        Assert.Equal(3, remaining.Count);
        Assert.All(remaining.Where(g => g.Name.StartsWith("Cell")), g => Assert.Equal(plant.Id, g.ParentId));

        // Deleting a root leaves its former sub-groups as roots.
        Assert.IsType<NoContentResult>(await controller.Delete(plant.Id, default));
        Assert.All(await db.MachineGroups.AsNoTracking().ToListAsync(), g => Assert.Null(g.ParentId));
        Assert.IsType<NotFoundResult>(await controller.Delete(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Create_IgnoresAPostedNavigationGraph()
    {
        await using var db = NewDb();
        var controller = MachineGroupWriteScopeTests.ControllerFor(new MachineGroupRepository(db));
        var posted = Group("Line");
        posted.Children.Add(Group("Smuggled child"));
        posted.Parent = Group("Smuggled parent");

        Assert.IsType<CreatedAtActionResult>((await controller.Create(posted, default)).Result);

        Assert.Equal(new[] { "Line" }, await db.MachineGroups.AsNoTracking().Select(g => g.Name).ToListAsync());
    }
}
