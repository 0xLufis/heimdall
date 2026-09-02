using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Dtos;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using App.Backend.Api.Services;

namespace App.Backend.Tests;

public class InventorySearchTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private ICacheService CreateCacheService()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new CacheService(distributedCache, memoryCache, NullLogger<CacheService>.Instance);
    }

    [Fact]
    public async Task Search_FiltersByNameTag()
    {
        // Arrange
        using var context = CreateContext();
        context.InventoryItems.AddRange(new List<BaseInventoryItem>
        {
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Target Component" },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Other" }
        });
        await context.SaveChangesAsync();

        var assetRepo = new AssetRepository(context);
        var controllerRepo = new ControllerRepository(context);
        var controller = new InventoryController(assetRepo, controllerRepo, CreateCacheService());

        // Act
        var result = await controller.Search("name:target");

        // Assert
        var actionResult = Assert.IsType<ActionResult<IEnumerable<SearchResultDto>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var components = Assert.IsAssignableFrom<IEnumerable<SearchResultDto>>(okResult.Value);
        Assert.Single(components);
        Assert.Contains(components, c => c.Name == "Target Component");
    }

    [Fact]
    public async Task Search_FiltersByManufacturerTag()
    {
        // Arrange
        using var context = CreateContext();
        var manufacturer = new Manufacturer { Id = Guid.NewGuid(), Name = "VisionCorp" };
        context.Manufacturers.Add(manufacturer);
        context.InventoryItems.AddRange(new List<BaseInventoryItem>
        {
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Comp1", Manufacturer = manufacturer },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Comp2", Manufacturer = new Manufacturer { Name = "Other" } }
        });
        await context.SaveChangesAsync();

        var assetRepo = new AssetRepository(context);
        var controllerRepo = new ControllerRepository(context);
        var controller = new InventoryController(assetRepo, controllerRepo, CreateCacheService());

        // Act
        var result = await controller.Search("manufacturer:vision");

        // Assert
        var actionResult = Assert.IsType<ActionResult<IEnumerable<SearchResultDto>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var components = Assert.IsAssignableFrom<IEnumerable<SearchResultDto>>(okResult.Value);
        Assert.Single(components);
        Assert.Equal("VisionCorp", components.First().ManufacturerName);
    }

    [Fact]
    public async Task Search_FiltersByTypeTag()
    {
        // Arrange
        using var context = CreateContext();
        context.InventoryItems.AddRange(new List<BaseInventoryItem>
        {
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Hardware" },
            new SoftwareComponent { Id = Guid.NewGuid(), Name = "Software" }
        });
        await context.SaveChangesAsync();

        var assetRepo = new AssetRepository(context);
        var controllerRepo = new ControllerRepository(context);
        var controller = new InventoryController(assetRepo, controllerRepo, CreateCacheService());

        // Act
        var result = await controller.Search("type:hardware");

        // Assert
        var actionResult = Assert.IsType<ActionResult<IEnumerable<SearchResultDto>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var components = Assert.IsAssignableFrom<IEnumerable<SearchResultDto>>(okResult.Value);
        Assert.Single(components);
        Assert.Equal("HardwareComponent", components.First().ItemType);
        Assert.Equal("Hardware", components.First().Name);
    }

    [Fact]
    public async Task Search_HandlesMultipleTags()
    {
        // Arrange
        using var context = CreateContext();
        var manufacturer = new Manufacturer { Id = Guid.NewGuid(), Name = "VisionCorp" };
        context.Manufacturers.Add(manufacturer);
        context.InventoryItems.AddRange(new List<BaseInventoryItem>
        {
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Target", Manufacturer = manufacturer },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Target", Manufacturer = new Manufacturer { Name = "Other" } },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Other", Manufacturer = manufacturer }
        });
        await context.SaveChangesAsync();

        var assetRepo = new AssetRepository(context);
        var controllerRepo = new ControllerRepository(context);
        var controller = new InventoryController(assetRepo, controllerRepo, CreateCacheService());

        // Act
        var result = await controller.Search("name:target manufacturer:vision");

        // Assert
        var actionResult = Assert.IsType<ActionResult<IEnumerable<SearchResultDto>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var components = Assert.IsAssignableFrom<IEnumerable<SearchResultDto>>(okResult.Value);
        Assert.Single(components);
        Assert.Equal("Target", components.First().Name);
        Assert.Equal("VisionCorp", components.First().ManufacturerName);
    }
}
