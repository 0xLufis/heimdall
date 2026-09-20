namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Services;
using App.Contracts.Inventory;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext() => new AppDbContext(_options);
    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AppDbContext(_options));
}

public class InventoryFilterAndDomainEndpointsTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    private ICacheService CreateCacheService()
    {
        return new FakeMemoryCacheService();
    }

    [Fact]
    public async Task InventoryFilter_ReturnsPaginatedAndCalculatedKpis()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateInMemoryDbContext(dbName);

        // Seed assets
        context.InventoryItems.Add(new HardwareComponent
        {
            Id = Guid.NewGuid(),
            Name = "Spindle Motor 15kW",
            DisplayName = "CNC Spindle",
            SerialNumber = "SN-SPINDLE-01",
            EquipmentStatus = "InMachine",
            CostInHUF = 1500000m,
            IsStockItem = false,
            Technology = "Assembly"
        });
        context.InventoryItems.Add(new HardwareComponent
        {
            Id = Guid.NewGuid(),
            Name = "Servo Motor 5kW",
            DisplayName = "Robot Axis 1",
            SerialNumber = "SN-SERVO-02",
            EquipmentStatus = "InStorage",
            CostInHUF = 500000m,
            IsStockItem = true,
            StockQuantity = 2,
            MinStockThreshold = 5,
            Technology = "Robotics"
        });
        context.InventoryItems.Add(new SoftwareAsset
        {
            Id = Guid.NewGuid(),
            Name = "TwinCAT PLC Runtime",
            DisplayName = "TwinCAT 3.1",
            EquipmentStatus = "InMachine",
            CostInHUF = 300000m,
            IsStockItem = false,
            Technology = "Controls"
        });
        await context.SaveChangesAsync();

        var assetRepo = new AssetRepository(context);
        var controllerRepo = new ControllerRepository(context);
        var cache = CreateCacheService();
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

        var controller = new InventoryController(assetRepo, controllerRepo, cache, factory);

        // Act: Filter query
        var req = new InventoryFilterRequest
        {
            Query = "Spindle",
            Page = 1,
            PageSize = 10
        };

        var actionResult = await controller.FilterInventoryGet(req, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var result = Assert.IsType<InventoryFilterResultDto>(okResult.Value);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Spindle Motor 15kW", result.Items[0].Name);
        Assert.Equal(3, result.Kpis.TotalGlobalCount);
        Assert.Equal(2, result.Kpis.TotalGlobalHardware);
        Assert.Equal(1, result.Kpis.TotalGlobalSoftware);
        Assert.Equal(2, result.Kpis.InMachine);
        Assert.Equal(1, result.Kpis.InStorage);
        Assert.Equal(1, result.Kpis.LowStockCount);
    }

    [Fact]
    public async Task StationComponentTree_ReturnsStronglyTypedDto()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateInMemoryDbContext(dbName);

        var stationId = Guid.NewGuid();
        var station = new Machine
        {
            Id = stationId,
            Name = "OP10-LoadStation",
            DisplayName = "Station OP10",
            CustomIdentifier = "LINE1-OP10",
            MachineType = "Assembly",
            GroupId = "Line 1"
        };
        context.Machines.Add(station);

        var part = new HardwareComponent
        {
            Id = Guid.NewGuid(),
            Name = "Gripper Arm",
            MachineId = stationId,
            EquipmentStatus = "InMachine"
        };
        context.InventoryItems.Add(part);
        await context.SaveChangesAsync();

        var assetRepo = new AssetRepository(context);
        var controllerRepo = new ControllerRepository(context);
        var cache = CreateCacheService();
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

        var controller = new InventoryController(assetRepo, controllerRepo, cache, factory);

        var actionResult = await controller.GetStationTree(stationId);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<StationComponentTreeDto>(okResult.Value);

        Assert.Equal(stationId, dto.StationId);
        Assert.Equal("OP10-LoadStation", dto.StationName);
        Assert.Single(dto.InstalledParts);
        Assert.Equal("Gripper Arm", dto.InstalledParts[0].Name);
    }

    [Fact]
    public async Task MachineGroupController_PerformsCrud()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateInMemoryDbContext(dbName);

        var repo = new MachineGroupRepository(context);
        var controller = new MachineGroupController(repo);

        // Create
        var newGroup = new MachineGroup
        {
            Name = "Battery EOL Assembly Line",
            Description = "Automated high voltage test cells",
            Color = "#3b82f6",
            MachineIdsJson = "[\"m-01\", \"m-02\"]"
        };

        var createResult = await controller.Create(newGroup, CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var createdGroup = Assert.IsType<MachineGroup>(created.Value);
        Assert.NotEqual(Guid.Empty, createdGroup.Id);

        // Read All
        var getAllResult = await controller.GetAll(CancellationToken.None);
        var okList = Assert.IsType<OkObjectResult>(getAllResult.Result);
        var list = Assert.IsType<List<MachineGroup>>(okList.Value);
        Assert.Single(list);

        // Update
        createdGroup.Description = "Updated line description";
        var updateResult = await controller.Update(createdGroup.Id, createdGroup, CancellationToken.None);
        var okUpdate = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updated = Assert.IsType<MachineGroup>(okUpdate.Value);
        Assert.Equal("Updated line description", updated.Description);

        // Delete
        var delResult = await controller.Delete(createdGroup.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(delResult);

        var emptyListResult = await controller.GetAll(CancellationToken.None);
        var emptyList = Assert.IsType<List<MachineGroup>>(((OkObjectResult)emptyListResult.Result!).Value);
        Assert.Empty(emptyList);
    }

    [Fact]
    public async Task TechnicianController_ManagesRulesAbsencesAndCandidates()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateInMemoryDbContext(dbName);

        context.AuthUsers.Add(new AuthUser
        {
            Id = "tech-alice",
            Name = "Alice Smith",
            Email = "alice@heimdall.local",
            Role = HeimdallRoles.Technician
        });
        await context.SaveChangesAsync();

        var repo = new TechnicianRepository(context);
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);
        var controller = new TechnicianController(repo, factory);

        // 1. Create Rule
        var rule = new TechnicianRule
        {
            Name = "Robotics Escalation",
            TechnicianId = "tech-alice",
            TechnicianName = "Alice Smith",
            ScopeType = "technology",
            TargetId = "Robotics"
        };
        var createRuleResult = await controller.CreateRule(rule, CancellationToken.None);
        var createdRule = Assert.IsType<TechnicianRule>(((CreatedAtActionResult)createRuleResult.Result!).Value);
        Assert.NotEqual(Guid.Empty, createdRule.Id);

        // 2. Create Absence
        var absence = new ShiftAbsence
        {
            TechnicianId = "tech-alice",
            TechnicianName = "Alice Smith",
            Reason = "Vacation",
            StartDate = DateTimeOffset.UtcNow.AddHours(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Active = true
        };
        var createAbsenceResult = await controller.CreateAbsence(absence, CancellationToken.None);
        var createdAbsence = Assert.IsType<ShiftAbsence>(((CreatedAtActionResult)createAbsenceResult.Result!).Value);
        Assert.NotEqual(Guid.Empty, createdAbsence.Id);

        // 3. Query Candidates (should indicate Alice is OutOfOffice and has 1 assigned rule)
        var candidatesResult = await controller.GetCandidates(role: null, search: "Alice", CancellationToken.None);
        var candidates = Assert.IsType<List<TechnicianCandidateDto>>(((OkObjectResult)candidatesResult.Result!).Value);
        Assert.Single(candidates);
        Assert.Equal("Alice Smith", candidates[0].Name);
        Assert.True(candidates[0].IsOutOfOffice);
        Assert.Equal(1, candidates[0].AssignedRulesCount);
    }
}
