using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Hubs;
using App.Backend.Api.Services;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace App.Backend.Tests;

public class TestMaintenanceClient : IMaintenanceClient
{
    public List<MaintenanceTicket> CreatedTickets { get; } = new();
    public List<MaintenanceTicket> UpdatedTickets { get; } = new();
    public List<Guid> DeletedTickets { get; } = new();
    public List<(Guid Id, string Status)> StatusChanges { get; } = new();

    public Task TicketCreated(MaintenanceTicket ticket)
    {
        CreatedTickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task TicketUpdated(MaintenanceTicket ticket)
    {
        UpdatedTickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task TicketDeleted(Guid ticketId)
    {
        DeletedTickets.Add(ticketId);
        return Task.CompletedTask;
    }

    public Task StatusChanged(Guid ticketId, string newStatus)
    {
        StatusChanges.Add((ticketId, newStatus));
        return Task.CompletedTask;
    }

    public Task ReceiveNotification(string message) => Task.CompletedTask;
}

public class TestHubClients : IHubClients<IMaintenanceClient>
{
    public TestMaintenanceClient TestClient { get; } = new();
    public IMaintenanceClient All => TestClient;
    public IMaintenanceClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => TestClient;
    public IMaintenanceClient Client(string connectionId) => TestClient;
    public IMaintenanceClient Clients(IReadOnlyList<string> connectionIds) => TestClient;
    public IMaintenanceClient Group(string groupName) => TestClient;
    public IMaintenanceClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => TestClient;
    public IMaintenanceClient Groups(IReadOnlyList<string> groupNames) => TestClient;
    public IMaintenanceClient User(string userId) => TestClient;
    public IMaintenanceClient Users(IReadOnlyList<string> userIds) => TestClient;
}

public class TestHubContext : IHubContext<MaintenanceHub, IMaintenanceClient>
{
    public TestHubClients TestClients { get; } = new();
    public IHubClients<IMaintenanceClient> Clients => TestClients;
    public IGroupManager Groups => null!;
}

public class CacheAndRealTimeTicketingTests
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
    public async Task CacheService_StoresAndRetrievesValues()
    {
        var cache = CreateCacheService();
        var testItem = new { Name = "Test PLC", Model = "S7-1500" };

        await cache.SetAsync("test:plc:1", testItem, TimeSpan.FromMinutes(5));
        var retrieved = await cache.GetAsync<dynamic>("test:plc:1");

        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task CacheService_GetOrSetAsync_ExecutesFactoryOnlyOnce()
    {
        var cache = CreateCacheService();
        int factoryCalls = 0;

        Task<string> Factory()
        {
            factoryCalls++;
            return Task.FromResult("Cached Result");
        }

        var res1 = await cache.GetOrSetAsync("test:key", Factory);
        var res2 = await cache.GetOrSetAsync("test:key", Factory);

        Assert.Equal("Cached Result", res1);
        Assert.Equal("Cached Result", res2);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task MaintenanceTicketController_CreateTicket_InvalidatesCacheAndBroadcastsSignalR()
    {
        using var context = CreateContext();
        var ticketRepo = new MaintenanceTicketRepository(context);
        var cache = CreateCacheService();
        var hubContext = new TestHubContext();

        var controller = new MaintenanceTicketController(ticketRepo, hubContext, cache);

        // Pre-populate ticket list cache
        await cache.SetAsync("tickets:all:all", new List<MaintenanceTicket>(), TimeSpan.FromMinutes(10));
        await cache.SetAsync("dashboard:metrics", new { Total = 0 }, TimeSpan.FromMinutes(10));

        var newTicket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            Title = "Thermal Overheat on Spindle",
            Status = "Open",
            Priority = "High",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = await controller.CreateTicket(newTicket);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var created = Assert.IsType<MaintenanceTicket>(createdResult.Value);
        Assert.Equal("Thermal Overheat on Spindle", created.Title);

        // Verify SignalR broadcast was sent
        Assert.Single(hubContext.TestClients.TestClient.CreatedTickets);
        Assert.Equal(created.Id, hubContext.TestClients.TestClient.CreatedTickets[0].Id);

        // Verify ticket detail is cached write-through
        var cachedItem = await cache.GetAsync<MaintenanceTicket>($"tickets:item:{created.Id}");
        Assert.NotNull(cachedItem);
        Assert.Equal("Thermal Overheat on Spindle", cachedItem.Title);

        // Verify dashboard metrics and list caches were invalidated
        var cachedDashboard = await cache.GetAsync<object>("dashboard:metrics");
        Assert.Null(cachedDashboard);
    }

    [Fact]
    public async Task MaintenanceTicketController_UpdateStatus_InvalidatesAndBroadcasts()
    {
        using var context = CreateContext();
        var ticketRepo = new MaintenanceTicketRepository(context);
        var cache = CreateCacheService();
        var hubContext = new TestHubContext();

        var initialTicket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            Title = "Hydraulic Pressure Alarm",
            Status = "Open",
            Priority = "Critical",
            CreatedAt = DateTime.UtcNow
        };
        await ticketRepo.CreateAsync(initialTicket);

        var controller = new MaintenanceTicketController(ticketRepo, hubContext, cache);

        // Act
        var result = await controller.UpdateStatus(initialTicket.Id, "In_Progress");

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Single(hubContext.TestClients.TestClient.StatusChanges);
        Assert.Equal("In_Progress", hubContext.TestClients.TestClient.StatusChanges[0].Status);
        Assert.Single(hubContext.TestClients.TestClient.UpdatedTickets);
        Assert.Equal("In_Progress", hubContext.TestClients.TestClient.UpdatedTickets[0].Status);

        var cachedItem = await cache.GetAsync<MaintenanceTicket>($"tickets:item:{initialTicket.Id}");
        Assert.NotNull(cachedItem);
        Assert.Equal("In_Progress", cachedItem.Status);
    }
}
