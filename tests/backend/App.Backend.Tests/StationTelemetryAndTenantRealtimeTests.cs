namespace App.Backend.Tests;

using System.Security.Claims;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Hubs;
using App.Backend.Api.Services;
using App.Infrastructure.Repositories;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Lua.Core;
using App.Shared.Lua.Models;
using App.Shared.Protos.Telemetry;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Real data path for station telemetry (PLC block → snapshot → org-scoped SignalR → KPIs) and
/// tenant isolation of every maintenance-hub broadcast.
/// </summary>
public class StationTelemetryAndTenantRealtimeTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Heimdall.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>Authorizes a policy only for principals carrying the "platform-admin" role.</summary>
    private sealed class RoleAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(user.IsInRole("platform-admin") ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(user.IsInRole("platform-admin") ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private sealed class RecordingLuaAutomation : ILuaAutomationService
    {
        public List<(string MachineId, string Metric, double Value)> TelemetryCalls { get; } = new();
        public bool Throw { get; set; }

        public Task<IReadOnlyList<LuaExecutionResult>> EvaluateTelemetryChangeAsync(string machineId, string metric, double value, CancellationToken cancellationToken = default)
        {
            TelemetryCalls.Add((machineId, metric, value));
            if (Throw) throw new InvalidOperationException("script exploded");
            return Task.FromResult<IReadOnlyList<LuaExecutionResult>>(Array.Empty<LuaExecutionResult>());
        }

        public Task<IReadOnlyList<LuaScriptRecord>> ListScriptsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LuaScriptRecord?> GetScriptAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LuaScriptRecord> CreateScriptAsync(LuaScriptRecord script, string? createdBy = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LuaScriptRecord?> UpdateScriptAsync(Guid id, LuaScriptRecord script, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteScriptAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LuaExecutionResult?> RunScriptAsync(Guid id, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LuaExecutionResult> TestExecuteAsync(string code, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LuaUiExtension>> GetUiExtensionsAsync(LuaUiTargetZone? zone = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LuaExecutionResult?> InvokeUiActionAsync(string extensionId, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<LuaExecutionResult>> EvaluateSlaPreventiveRulesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static DbContextOptions<AppDbContext> NewDb() =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static ClaimsPrincipal Principal(string? orgId = null, bool admin = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "u1") };
        if (orgId != null) claims.Add(new Claim("OrgId", orgId));
        if (admin) claims.Add(new Claim(ClaimTypes.Role, "platform-admin"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // ---------------------------------------------------------------- SignalR tenant isolation

    [Fact]
    public async Task Hub_PlacesConnectionsInTheirTenantGroupOnly()
    {
        var auth = new RoleAuthorizationService();

        Assert.Equal(new[] { "org:org-a" }, await MaintenanceHub.ResolveGroupsAsync(Principal("org-a"), auth));
        Assert.Equal(new[] { MaintenanceHubGroups.PlatformAdminGroup }, await MaintenanceHub.ResolveGroupsAsync(Principal("org-a", admin: true), auth));
        Assert.Empty(await MaintenanceHub.ResolveGroupsAsync(Principal(), auth));
        Assert.Empty(await MaintenanceHub.ResolveGroupsAsync(new ClaimsPrincipal(new ClaimsIdentity()), auth));
    }

    [Fact]
    public void Hub_ExposesNoClientInvokableGroupOrBroadcastMethods()
    {
        var invokable = typeof(MaintenanceHub)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.Name != nameof(MaintenanceHub.OnConnectedAsync))
            .Select(m => m.Name)
            .ToList();
        Assert.Empty(invokable);
    }

    [Fact]
    public async Task TicketEvents_GoToTheOwningOrganizationAndAdmins_NeverToAll()
    {
        await using var context = new AppDbContext(NewDb());
        var hub = new TestHubContext();
        var cache = new FakeMemoryCacheService();
        var controller = new MaintenanceTicketController(new MaintenanceTicketRepository(context), hub, cache);

        var ticket = new MaintenanceTicket { Title = "Leak", Priority = "High", OrganizationId = "org-a" };
        await controller.CreateTicket(ticket);
        await controller.DeleteTicket(ticket.Id);

        var global = new MaintenanceTicket { Title = "Global", Priority = "Low" };
        await controller.CreateTicket(global);

        Assert.DoesNotContain("All", hub.TestClients.Audiences);
        Assert.Equal(new[]
        {
            $"Groups:org:org-a,{MaintenanceHubGroups.PlatformAdminGroup}",
            $"Groups:org:org-a,{MaintenanceHubGroups.PlatformAdminGroup}",
            $"Group:{MaintenanceHubGroups.PlatformAdminGroup}"
        }, hub.TestClients.Audiences);
        Assert.Contains(ticket.Id, hub.TestClients.TestClient.DeletedTickets);
    }

    // ---------------------------------------------------------------- Ingestion → publisher

    private static async Task<(TestDbContextFactory Factory, Machine Machine, ClientPc Pc)> SeedStationAsync(string? orgId = "org-a")
    {
        var factory = new TestDbContextFactory(NewDb());
        await using var db = factory.CreateDbContext();
        var machine = new Machine { Id = Guid.NewGuid(), Name = "Weld Cell", CustomIdentifier = "L01-OP020", PinnedObjectHandle = "3B4F", OrganizationId = orgId };
        var pc = new ClientPc { Id = Guid.NewGuid(), Name = "IPC-20", Hostname = "IPC-20", MachineIdentifier = "IPC-20", MacAddress = "00:11:22:33:44:55", OrganizationId = orgId };
        db.Machines.Add(machine);
        db.ClientPcs.Add(pc);
        db.StationControllers.Add(new StationController { MachineId = machine.Id, ClientPcId = pc.Id, Role = "Primary" });
        await db.SaveChangesAsync();
        return (factory, machine, pc);
    }

    private static AdsPlcMemoryBlock StationBlock(DateTimeOffset at)
    {
        var data = new StructValue();
        data.Fields["fVibrationRms"] = new TelemetryValue { DoubleValue = 1.85 };
        data.Fields["fSpindleRpm"] = new TelemetryValue { DoubleValue = 2950 };
        var production = new StructValue();
        production.Fields["nPartsProduced"] = new TelemetryValue { Int64Value = 1000 };
        production.Fields["nScrapParts"] = new TelemetryValue { Int32Value = 20 };
        production.Fields["Availability"] = new TelemetryValue { DoubleValue = 90 };
        production.Fields["Performance"] = new TelemetryValue { DoubleValue = 80 };
        production.Fields["eState"] = new TelemetryValue { StateValue = new DeviceStateValue { StateCode = 3, StateLabel = "Running" } };
        data.Fields["stProduction"] = new TelemetryValue { StructValue = production };

        return new AdsPlcMemoryBlock
        {
            AmsNetId = "5.80.201.44.1.1",
            AmsPort = 851,
            SymbolName = "MAIN.Station.Telemetry._data",
            PlcTypeName = "ST_StationTelemetry",
            Timestamp = Timestamp.FromDateTimeOffset(at),
            StructuredValue = new TelemetryValue { StructValue = data }
        };
    }

    private static TelemetryIngestionService CreateIngestion(TestDbContextFactory factory, TestHubContext hub, ICacheService cache, ILuaAutomationService? lua) =>
        new(NullLogger<TelemetryIngestionService>.Instance,
            new FakeControllerRepository(),
            factory,
            new TestHostEnvironment(),
            new ConfigurationBuilder().Build(),
            hub,
            cache,
            lua);

    [Fact]
    public async Task PlcStationBlock_IsStored_PublishedToOrgGroup_AndFedToLua()
    {
        var (factory, machine, _) = await SeedStationAsync("org-a");
        var hub = new TestHubContext();
        var cache = new FakeMemoryCacheService();
        var lua = new RecordingLuaAutomation();
        var at = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

        Assert.True(await CreateIngestion(factory, hub, cache, lua).ProcessPlcMemoryAsync("IPC-20", StationBlock(at)));

        // Snapshot persisted for the KPI endpoint
        var snap = await cache.GetAsync<StationTelemetrySnapshot>(StationTelemetryStore.Key(machine.Id));
        Assert.NotNull(snap);
        Assert.Equal(1.85, snap!.VibrationMmS);
        Assert.Equal(1000, snap.TotalPartsProduced);
        Assert.Equal(98.0, snap.Yield);
        Assert.Equal(Math.Round(90 * 80 * 98.0 / 10000.0, 2), snap.EffectiveOee);
        Assert.Equal("RUNNING", snap.OperationalStatus);

        // Every broadcast (PLC memory + both station events) addressed to org-a + admins only
        Assert.DoesNotContain("All", hub.TestClients.Audiences);
        Assert.All(hub.TestClients.Audiences, a => Assert.Equal($"Groups:org:org-a,{MaintenanceHubGroups.PlatformAdminGroup}", a));

        var (oscStation, osc) = Assert.Single(hub.TestClients.TestClient.StationOscillations);
        Assert.Equal(machine.Id.ToString(), oscStation);
        Assert.Equal("3B4F", osc.Handle);
        Assert.Equal(1.85, osc.VibrationMmS);
        Assert.Null(osc.PowerKw); // not reported → null, never a default
        Assert.Equal(at.ToUnixTimeMilliseconds(), osc.Timestamp);

        var (_, kpi) = Assert.Single(hub.TestClients.TestClient.StationKpis);
        Assert.Equal(98.0, kpi.Yield);
        Assert.Equal(20, kpi.ScrapParts);

        // Lua ring buffer / TelemetryChange scripts receive the raw metrics
        Assert.Contains((machine.Id.ToString(), "vibration_mms", 1.85), lua.TelemetryCalls);
        Assert.Contains((machine.Id.ToString(), "parts_produced", 1000.0), lua.TelemetryCalls);
    }

    [Fact]
    public async Task LuaFailures_DoNotBreakIngestion()
    {
        var (factory, machine, _) = await SeedStationAsync();
        var cache = new FakeMemoryCacheService();
        var lua = new RecordingLuaAutomation { Throw = true };

        Assert.True(await CreateIngestion(factory, new TestHubContext(), cache, lua).ProcessPlcMemoryAsync("IPC-20", StationBlock(DateTimeOffset.UtcNow)));
        Assert.NotEmpty(lua.TelemetryCalls);
        Assert.NotNull(await cache.GetAsync<StationTelemetrySnapshot>(StationTelemetryStore.Key(machine.Id)));
    }

    [Fact]
    public async Task PlcBlockWithoutStationFields_PublishesNoStationEvents()
    {
        var (factory, _, _) = await SeedStationAsync();
        var hub = new TestHubContext();
        var block = new AdsPlcMemoryBlock { SymbolName = "MAIN.Unrelated.nCounter", StructuredValue = new TelemetryValue { Int32Value = 5 } };

        Assert.True(await CreateIngestion(factory, hub, new FakeMemoryCacheService(), null).ProcessPlcMemoryAsync("IPC-20", block));
        Assert.Empty(hub.TestClients.TestClient.StationOscillations);
        Assert.Empty(hub.TestClients.TestClient.StationKpis);
    }

    [Fact]
    public async Task OrgLessStation_IsPublishedToAdminsOnly()
    {
        var (factory, _, _) = await SeedStationAsync(orgId: null);
        var hub = new TestHubContext();

        await CreateIngestion(factory, hub, new FakeMemoryCacheService(), null).ProcessPlcMemoryAsync("IPC-20", StationBlock(DateTimeOffset.UtcNow));

        Assert.NotEmpty(hub.TestClients.Audiences);
        Assert.All(hub.TestClients.Audiences, a => Assert.Equal($"Group:{MaintenanceHubGroups.PlatformAdminGroup}", a));
    }

    // ---------------------------------------------------------------- KPIs from stored rows

    [Fact]
    public async Task StationKpis_AreComputedFromSnapshotsAndTickets_AndNullWithoutTelemetry()
    {
        var options = NewDb();
        await using var db = new AppDbContext(options);
        var line = new LineMachineGroup { Id = Guid.NewGuid(), Name = "Line 1" };
        db.MachineGroups.Add(line);
        await db.SaveChangesAsync();

        var reporting = new Machine { Id = Guid.NewGuid(), Name = "OP10", CustomIdentifier = "L01-OP010", LineGroupId = line.Id };
        var silent = new Machine { Id = Guid.NewGuid(), Name = "OP20", CustomIdentifier = "L01-OP020", PinnedObjectHandle = "9C1D", LineGroupId = line.Id };
        var assets = new FakeAssetRepository { Machines = { reporting, silent } };
        var now = DateTimeOffset.UtcNow;
        var tickets = new FakeMaintenanceTicketRepository
        {
            Tickets =
            {
                new MaintenanceTicket { MachineId = silent.Id, Priority = "Critical", Status = "Open", CreatedAt = now.AddHours(-13) },
                new MaintenanceTicket { MachineId = silent.Id, Priority = "Medium", Status = "InProgress", CreatedAt = now.AddHours(-1) },
                new MaintenanceTicket { MachineId = silent.Id, Priority = "High", Status = "Closed", CreatedAt = now.AddHours(-30), ResolvedAt = now.AddHours(-29) }
            }
        };
        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(reporting.Id), new StationTelemetrySnapshot
        {
            MachineId = reporting.Id,
            Availability = 95, Performance = 90, TotalPartsProduced = 500, ScrapParts = 5,
            PartsPerHour = 142, CycleTimeMs = 25400, OperationalStatus = "RUNNING", LastTelemetryAt = now
        });

        var service = new PredictiveMaintenanceService(assets, new FakeControllerRepository(), tickets, cache,
            NullLogger<PredictiveMaintenanceService>.Instance, new MachineGroupRepository(db));

        var kpis = await service.GetStationTelemetryKpisAsync();
        Assert.Equal(2, kpis.Count);

        var r = kpis.Single(k => k.StationId == reporting.Id.ToString());
        Assert.Equal("L01-OP010", r.Handle);
        Assert.Equal("Line 1", r.LineName);
        Assert.Equal(99.0, r.Yield);
        Assert.Equal(99.0, r.Quality);
        Assert.Equal(Math.Round(95 * 90 * 99.0 / 10000.0, 2), r.Oee);
        Assert.Equal(25.4, r.CycleTimeSeconds);
        Assert.Equal("RUNNING", r.OperationalStatus);
        Assert.Equal(0, r.ActiveTicketCount);

        var s = kpis.Single(k => k.StationId == silent.Id.ToString());
        Assert.Equal("9C1D", s.Handle);
        Assert.Null(s.Oee);
        Assert.Null(s.PartsPerHour);
        Assert.Null(s.LastTelemetryAt);
        Assert.Equal("UNKNOWN", s.OperationalStatus);
        Assert.Equal(2, s.ActiveTicketCount);
        Assert.Equal(1, s.CriticalTicketCount);
        Assert.True(s.HasSlaBreach);

        Assert.Single(await service.GetStationTelemetryKpisAsync(stationId: "9C1D"));
        Assert.Equal(2, (await service.GetStationTelemetryKpisAsync(lineId: line.Id.ToString())).Count);
        Assert.Empty(await service.GetStationTelemetryKpisAsync(lineId: "unknown-line"));
    }

    [Fact]
    public async Task LineKpis_ComputeMttrMtbfAndIncidentsFromTickets()
    {
        await using var db = new AppDbContext(NewDb());
        var line = new LineMachineGroup { Id = Guid.NewGuid(), Name = "Line 2" };
        db.MachineGroups.Add(line);
        await db.SaveChangesAsync();

        var m = new Machine { Id = Guid.NewGuid(), Name = "OP50", LineGroupId = line.Id };
        var now = DateTimeOffset.UtcNow;
        var tickets = new FakeMaintenanceTicketRepository
        {
            Tickets =
            {
                new MaintenanceTicket { MachineId = m.Id, Status = "Resolved", CreatedAt = now.AddHours(-48), ResolvedAt = now.AddHours(-47) },
                new MaintenanceTicket { MachineId = m.Id, Status = "Resolved", CreatedAt = now.AddHours(-24), ResolvedAt = now.AddHours(-23.5) },
                new MaintenanceTicket { MachineId = m.Id, Status = "Open", CreatedAt = now },
                new MaintenanceTicket { MachineId = m.Id, Status = "Resolved", CreatedAt = now.AddDays(-60), ResolvedAt = now.AddDays(-59) } // outside window
            }
        };
        var service = new PredictiveMaintenanceService(new FakeAssetRepository { Machines = { m } }, new FakeControllerRepository(), tickets,
            new FakeMemoryCacheService(), NullLogger<PredictiveMaintenanceService>.Instance, new MachineGroupRepository(db));

        var kpi = Assert.Single(await service.GetLineKpisAsync());
        Assert.Equal("Line 2", kpi.LineName);
        Assert.Equal(3, kpi.IncidentCount);
        Assert.Equal(45.0, kpi.MttrMinutes);   // (60 + 30) / 2
        Assert.Equal(24.0, kpi.MtbfHours);     // 48h span over 2 gaps
        Assert.Null(kpi.Oee);                  // no station telemetry
    }

    [Fact]
    public async Task MachineTrends_ComeFromIngestedTelemetryHistory_ForVisibleMachinesOnly()
    {
        await using var db = new AppDbContext(NewDb());
        var m = new Machine { Id = Guid.NewGuid(), Name = "OP30", CustomIdentifier = "L01-OP030" };
        var history = new DbPlantDataProvider(db, new MachineGroupRepository(db), new FakeMaintenanceTicketRepository(), NullLogger<DbPlantDataProvider>.Instance);
        foreach (var v in new[] { 1.4, 1.5, 1.45, 1.42, 1.48 })
        {
            await history.EmitTelemetryAsync(m.Id.ToString(), "vibration_mms", v);
        }

        var service = new PredictiveMaintenanceService(new FakeAssetRepository { Machines = { m } }, new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(), new FakeMemoryCacheService(), NullLogger<PredictiveMaintenanceService>.Instance,
            telemetryHistory: history);

        var trends = await service.GetMachineTrendsAsync("L01-OP030", "vibration", TimeSpan.FromHours(1));
        Assert.Equal(new[] { 1.4, 1.5, 1.45, 1.42, 1.48 }, trends.Points.Select(p => p.Value));
        Assert.Equal(5, trends.StatisticalTrends.MovingAverages.Count);

        // A machine the caller cannot see (not in the tenant-filtered repository) yields nothing.
        var other = await service.GetMachineTrendsAsync(Guid.NewGuid().ToString(), "vibration", TimeSpan.FromHours(1));
        Assert.Empty(other.Points);
    }
}
