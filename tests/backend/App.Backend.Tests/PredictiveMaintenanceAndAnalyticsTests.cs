using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using App.Contracts.Inventory;
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
    public Task<StationComponentTreeDto?> GetStationComponentTreeAsync(Guid stationId) => Task.FromResult<StationComponentTreeDto?>(null);
    public Task<StationComponentTreeDto?> GetStationComponentTreeAsync(string stationIdentifier) => Task.FromResult<StationComponentTreeDto?>(null);
    public Task<InventoryFilterResultDto> FilterInventoryAsync(InventoryFilterRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new InventoryFilterResultDto());
    public Task<BaseInventoryItem?> UpdateAsync(BaseInventoryItem item) => Task.FromResult<BaseInventoryItem?>(item);
}

public class FakeMaintenanceTicketRepository : IMaintenanceTicketRepository
{
    public List<MaintenanceTicket> Tickets { get; set; } = new();

    public Task<List<MaintenanceTicket>> GetAllAsync() => Task.FromResult(Tickets.ToList());
    public Task<MaintenanceTicket?> GetByIdAsync(Guid id) => Task.FromResult(Tickets.FirstOrDefault(t => t.Id == id));
    public Task<List<MaintenanceTicket>> GetByStatusAsync(string status) => Task.FromResult(Tickets.Where(t => t.Status == status).ToList());
    public Task<List<MaintenanceTicket>> GetByDepartmentAsync(string department) => Task.FromResult(Tickets.Where(t => t.ResponsibleDepartment?.Equals(department, StringComparison.OrdinalIgnoreCase) == true).ToList());
    public Task<MaintenanceTicket> CreateAsync(MaintenanceTicket ticket) { Tickets.Add(ticket); return Task.FromResult(ticket); }
    public Task<MaintenanceTicket?> UpdateAsync(MaintenanceTicket ticket) => Task.FromResult<MaintenanceTicket?>(ticket);
    public Task<MaintenanceTicket?> UpdateStatusAsync(Guid id, string status) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<MaintenanceTicket?> EscalateAsync(Guid id, string reason, string escalatedBy, App.Contracts.Enums.EscalationHandoverState handoverState = App.Contracts.Enums.EscalationHandoverState.Notification, string? target = null) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<MaintenanceTicket?> DeescalateAsync(Guid id, string resolvedBy, string? resolutionNotes = null) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<MaintenanceTicket?> SetPendingAsync(Guid id, App.Contracts.Enums.PendingReason reason, string? details) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<MaintenanceTicket?> ReserveAsync(Guid id, string technician) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<MaintenanceTicket?> QrPickupAsync(Guid id, string technician) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<MaintenanceTicket?> AutoCloseAsync(Guid id, string actor, string reason, string? resolutionNotes = null) => Task.FromResult<MaintenanceTicket?>(null);
    public Task<MaintenanceTicket?> RetractAsync(Guid id, string retractedBy, string reason) => Task.FromResult<MaintenanceTicket?>(null);
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

    private static PredictiveMaintenanceService CreateService(
        FakeAssetRepository? assets = null,
        FakeMaintenanceTicketRepository? tickets = null,
        FakeMemoryCacheService? cache = null) =>
        new(assets ?? new FakeAssetRepository(),
            new FakeControllerRepository(),
            tickets ?? new FakeMaintenanceTicketRepository(),
            cache ?? new FakeMemoryCacheService(),
            NullLogger<PredictiveMaintenanceService>.Instance);

    [Fact]
    public void CalculateHealthIndex_IsDerivedOnlyFromCountsAndClamped()
    {
        var service = CreateService();

        Assert.Equal(100.0, service.CalculateHealthIndex("a", 0, 0));
        // Identical inputs give identical results regardless of the machine id (no hash noise).
        Assert.Equal(service.CalculateHealthIndex("m-1", 2, 1), service.CalculateHealthIndex("m-2", 2, 1));
        Assert.Equal(100.0 - 2 * 6.5 - 12.0, service.CalculateHealthIndex("m-1", 2, 1));
        Assert.Equal(0.0, service.CalculateHealthIndex("m-broken", 20, 10));
    }

    [Fact]
    public async Task GetMachineTrends_WithoutTelemetryHistory_ReturnsEmptySeriesWithCatalogLimits()
    {
        var service = CreateService();

        var trends = await service.GetMachineTrendsAsync("m-op20", "temperature", TimeSpan.FromHours(8));

        Assert.Equal("m-op20", trends.MachineId);
        Assert.Equal("°C", trends.Unit);
        Assert.Equal(48.5, trends.NominalValue);
        Assert.Empty(trends.Points);
        Assert.Empty(trends.DetectedAnomalies);
        Assert.Null(trends.RecipeForecast);
        Assert.Empty(trends.AvailableRecipes);
    }

    [Fact]
    public async Task GetLineKpis_WithoutMachines_ReturnsEmpty()
    {
        Assert.Empty(await CreateService().GetLineKpisAsync());
    }

    [Fact]
    public async Task AnalyticsController_WithNoStoredData_ReturnsEmptyOrNullInsteadOfDemoData()
    {
        var service = CreateService();
        var controller = new AnalyticsController(service, new ConfigurationBuilder().Build(), NullLogger<AnalyticsController>.Instance);

        var okFleet = Assert.IsType<OkObjectResult>((await controller.GetFleetSummary(default)).Result);
        var fleetDto = Assert.IsType<FleetSummaryDto>(okFleet.Value);
        Assert.Equal(0, fleetDto.TotalMachines);
        Assert.Null(fleetDto.AvailabilityPercentage);
        Assert.Null(fleetDto.AverageMtbfHours);
        Assert.Null(fleetDto.AverageMttrMinutes);
        Assert.Null(fleetDto.SlaCompliancePercentage);
        Assert.Empty(fleetDto.TopFaultingMachines);
        Assert.Empty(fleetDto.StockDepletion);
        Assert.Equal(0, fleetDto.MaintenanceBacklog.Under24Hours + fleetDto.MaintenanceBacklog.OneToThreeDays + fleetDto.MaintenanceBacklog.OverThreeDays);

        var okTrends = Assert.IsType<OkObjectResult>((await controller.GetMachineTrends("m-op10", "vibration", "1h")).Result);
        Assert.Empty(Assert.IsType<MachineTrendSeriesDto>(okTrends.Value).Points);

        var okLines = Assert.IsType<OkObjectResult>((await controller.GetLineKpis()).Result);
        Assert.Empty(Assert.IsType<List<LineKpiDto>>(okLines.Value));

        var okStations = Assert.IsType<OkObjectResult>((await controller.GetStationKpis()).Result);
        Assert.Empty(Assert.IsType<List<StationKpiDto>>(okStations.Value));
    }
}
