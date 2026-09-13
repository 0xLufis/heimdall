using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class FakeAssetRepository : IAssetRepository
{
    public List<BaseInventoryItem> Items { get; set; } = new();
    public List<Machine> Machines { get; set; } = new();

    public Task<List<BaseInventoryItem>> GetInventoryTreeAsync() => Task.FromResult(Items.ToList());
    public Task<BaseInventoryItem?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task<List<string>> GetSearchKeysAsync() => Task.FromResult(new List<string>());
    public Task<List<BaseInventoryItem>> SearchAsync(string? query, int limit) => Task.FromResult(Items.Take(limit).ToList());
    public Task<List<ResponsibleTeam>> GetTeamsAsync() => Task.FromResult(new List<ResponsibleTeam>());
    public Task<List<Manufacturer>> GetManufacturersAsync() => Task.FromResult(new List<Manufacturer>());
    public Task<List<Supplier>> GetSuppliersAsync() => Task.FromResult(new List<Supplier>());
    public Task<List<Machine>> GetMachinesAsync() => Task.FromResult(Machines.ToList());
    public Task<List<ClientPc>> GetClientPcsAsync() => Task.FromResult(new List<ClientPc>());
    public Task<BaseInventoryItem> CreateAsync(BaseInventoryItem item) { Items.Add(item); return Task.FromResult(item); }
    public Task<Manufacturer> GetOrCreateManufacturerAsync(string nameOrId) => Task.FromResult(new Manufacturer { Name = nameOrId });
    public Task<Supplier> GetOrCreateSupplierAsync(string nameOrId) => Task.FromResult(new Supplier { Name = nameOrId });
    public Task<bool> DeleteAsync(Guid id) => Task.FromResult(true);
    public Task<int> GetCountAsync() => Task.FromResult(Items.Count);
    public Task<int> GetAuthUsersCountAsync() => Task.FromResult(5);
    public Task<List<BaseInventoryItem>> GetPartsAsync() => Task.FromResult(Items.ToList());
    public Task<List<BaseInventoryItem>> GetStockAsync() => Task.FromResult(Items.ToList());
    public Task<object?> GetStationComponentTreeAsync(Guid stationId) => Task.FromResult<object?>(null);
}

public class FakeMaintenanceTicketRepository : IMaintenanceTicketRepository
{
    public List<MaintenanceTicket> Tickets { get; set; } = new();

    public Task<List<MaintenanceTicket>> GetAllAsync() => Task.FromResult(Tickets.ToList());
    public Task<MaintenanceTicket?> GetByIdAsync(Guid id) => Task.FromResult(Tickets.FirstOrDefault(t => t.Id == id));
    public Task<List<MaintenanceTicket>> GetByStatusAsync(string status) => Task.FromResult(Tickets.Where(t => t.Status == status).ToList());
    public Task<MaintenanceTicket> CreateAsync(MaintenanceTicket ticket) { Tickets.Add(ticket); return Task.FromResult(ticket); }
    public Task<MaintenanceTicket?> UpdateAsync(MaintenanceTicket ticket) => Task.FromResult<MaintenanceTicket?>(ticket);
    public Task<MaintenanceTicket?> UpdateStatusAsync(Guid id, string status) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<bool> DeleteAsync(Guid id) => Task.FromResult(true);
    public Task<int> GetPendingAlertsCountAsync(TimeSpan timeSpan) => Task.FromResult(Tickets.Count(t => t.Status == "Open"));
    public Task<List<AgentEvent>> GetRecentAgentEventsAsync(int count) => Task.FromResult(new List<AgentEvent>());
}

public class FakeMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _store = new();

    public Task<T?> GetAsync<T>(string key) => Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);
    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null) { _store[key] = value; return Task.CompletedTask; }
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) => factory();
    public Task RemoveAsync(string key) { _store.Remove(key); return Task.CompletedTask; }
    public Task RemoveByPatternAsync(string pattern) { _store.Clear(); return Task.CompletedTask; }
}

public class PredictiveMaintenanceAndAnalyticsTests
{
    [Fact]
    public void DetectAnomalies_IdentifiesExtremeZScoreSpikesAccurately()
    {
        var service = new PredictiveMaintenanceService(
            new FakeAssetRepository(),
            new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(),
            new FakeMemoryCacheService(),
            NullLogger<PredictiveMaintenanceService>.Instance);

        var now = DateTime.UtcNow;
        var points = new List<TelemetryPointDto>();

        // Generate 30 stable baseline points at ~1000ms with small jitter (+-5ms)
        for (int i = 0; i < 30; i++)
        {
            points.Add(new TelemetryPointDto
            {
                Timestamp = now.AddSeconds(i * 10),
                Value = 1000.0 + (i % 3 - 1) * 2.0
            });
        }

        // Inject high anomaly point (+30ms spike on a sigma ~1.6)
        points.Add(new TelemetryPointDto
        {
            Timestamp = now.AddSeconds(300),
            Value = 1030.0
        });

        var anomalies = service.DetectAnomalies("Cycle Time", points);

        Assert.NotEmpty(anomalies);
        var outlier = anomalies.First();
        Assert.Equal("Critical", outlier.Severity);
        Assert.True(outlier.ZScore > 3.0);
        Assert.Equal(1030.0, outlier.Value);
    }

    [Fact]
    public void CalculateHealthIndex_DeductsForAnomaliesAndClampsWithinBounds()
    {
        var service = new PredictiveMaintenanceService(
            new FakeAssetRepository(),
            new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(),
            new FakeMemoryCacheService(),
            NullLogger<PredictiveMaintenanceService>.Instance);

        double healthy = service.CalculateHealthIndex("m-healthy", 0, 0);
        Assert.InRange(healthy, 80.0, 100.0);

        double degraded = service.CalculateHealthIndex("m-degraded", 4, 3);
        Assert.True(degraded < healthy);
        Assert.InRange(degraded, 15.0, 80.0);

        // Extreme stress testing clamping
        double extreme = service.CalculateHealthIndex("m-broken", 20, 10);
        Assert.Equal(15.0, extreme); // Clamped at 15% floor
    }

    [Fact]
    public void GetMachineTrends_GeneratesNominalTolerancesAndPoints()
    {
        var service = new PredictiveMaintenanceService(
            new FakeAssetRepository(),
            new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(),
            new FakeMemoryCacheService(),
            NullLogger<PredictiveMaintenanceService>.Instance);

        var trends = service.GetMachineTrends("m-op20", "temperature", TimeSpan.FromHours(8));

        Assert.Equal("m-op20", trends.MachineId);
        Assert.Equal("°C", trends.Unit);
        Assert.Equal(48.5, trends.NominalValue);
        Assert.Equal(65.0, trends.UpperTolerance);
        Assert.Equal(25.0, trends.LowerTolerance);
        Assert.NotEmpty(trends.Points);
        Assert.NotEmpty(trends.DetectedAnomalies);
    }

    [Fact]
    public void GetLineKpis_CalculatesValidOeeAndLineBreakdowns()
    {
        var service = new PredictiveMaintenanceService(
            new FakeAssetRepository(),
            new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(),
            new FakeMemoryCacheService(),
            NullLogger<PredictiveMaintenanceService>.Instance);

        var kpis = service.GetLineKpis();

        Assert.Equal(8, kpis.Count);
        foreach (var line in kpis)
        {
            Assert.InRange(line.Oee, 80.0, 99.0);
            Assert.InRange(line.Availability, 90.0, 100.0);
            Assert.InRange(line.Performance, 85.0, 100.0);
            Assert.InRange(line.Quality, 95.0, 100.0);
            Assert.True(line.MtbfHours > 200);
            Assert.True(line.MttrMinutes > 15);
        }
    }

    [Fact]
    public async Task AnalyticsController_EndpointsReturnSuccess()
    {
        var service = new PredictiveMaintenanceService(
            new FakeAssetRepository(),
            new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(),
            new FakeMemoryCacheService(),
            NullLogger<PredictiveMaintenanceService>.Instance);

        var config = new ConfigurationBuilder().Build();
        var controller = new AnalyticsController(service, config, NullLogger<AnalyticsController>.Instance);

        var fleetResult = await controller.GetFleetSummary(default);
        var okFleet = Assert.IsType<OkObjectResult>(fleetResult.Result);
        var fleetDto = Assert.IsType<FleetSummaryDto>(okFleet.Value);
        Assert.True(fleetDto.AvailabilityPercentage > 90);
        Assert.NotEmpty(fleetDto.TopFaultingMachines);

        var trendsResult = controller.GetMachineTrends("m-op10", "vibration", "1h");
        var okTrends = Assert.IsType<OkObjectResult>(trendsResult.Result);
        var trendsDto = Assert.IsType<MachineTrendSeriesDto>(okTrends.Value);
        Assert.Equal("m-op10", trendsDto.MachineId);

        var pbiResult = controller.GetPowerBiConfig();
        var okPbi = Assert.IsType<OkObjectResult>(pbiResult.Result);
        var pbiDto = Assert.IsType<PowerBiConfigDto>(okPbi.Value);
        Assert.NotEmpty(pbiDto.EmbedUrl);
    }
}
