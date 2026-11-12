using System.Text.Json;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// Station freshness: arrival tracking (including blocks the change gate drops), learned sample
/// intervals, per-station stale thresholds, the fixed override, and OFFLINE after an agent's
/// MQTT last will / graceful stop. Time is always explicit (fake clock), never the wall clock.
/// </summary>
public class StationFreshnessTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public FakeClock(DateTimeOffset now) => Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static StationFreshnessTracker TrackerWithArrivals(Guid station, params double[] secondsAfterT0)
    {
        var tracker = new StationFreshnessTracker();
        foreach (var s in secondsAfterT0) tracker.RecordArrival(station, T0.AddSeconds(s));
        return tracker;
    }

    // ---------------------------------------------------------------- interval learning

    [Fact]
    public void Tracker_LearnsTheMedianGap_IgnoringSameSampleBurstsExtraPublishesAndOutages()
    {
        var station = Guid.NewGuid();
        // 4 s cadence; a second block of the same cycle 20 ms later (8.02), one extra publish (13.5),
        // one missed sample (gap 8 s), then a 10-minute outage and the cadence again.
        var tracker = TrackerWithArrivals(station, 0, 4, 8, 8.02, 12, 13.5, 16, 24, 28, 628, 632, 636);

        var arrivals = tracker.Get(station)!.Value;
        Assert.Equal(T0.AddSeconds(636), arrivals.LastArrivalAt);
        Assert.Equal(TimeSpan.FromSeconds(4), arrivals.SampleInterval);
        Assert.Equal(9, arrivals.GapCount);       // 4,4,4,1.5,2.5,8,4,4,4 — not the burst, not the outage
    }

    [Fact]
    public void Tracker_NeedsThreeGapsBeforeItTrustsAnInterval_AndForgetsAfterRetention()
    {
        var station = Guid.NewGuid();
        var tracker = TrackerWithArrivals(station, 0, 4, 8);
        Assert.Null(tracker.Get(station)!.Value.SampleInterval);   // two gaps
        tracker.RecordArrival(station, T0.AddSeconds(12));
        Assert.Equal(TimeSpan.FromSeconds(4), tracker.Get(station)!.Value.SampleInterval);

        Assert.Null(tracker.Get(Guid.NewGuid()));
        tracker.Prune(T0.AddSeconds(12) + StationFreshnessTracker.Retention + TimeSpan.FromSeconds(1));
        Assert.Null(tracker.Get(station));
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void Tracker_KeepsBoundedMemory_AndAdaptsToANewCadence()
    {
        var station = Guid.NewGuid();
        var tracker = new StationFreshnessTracker();
        var t = 0.0;
        for (var i = 0; i < 100; i++) tracker.RecordArrival(station, T0.AddSeconds(t += 4));
        for (var i = 0; i < 10; i++) tracker.RecordArrival(station, T0.AddSeconds(t += 10));

        var arrivals = tracker.Get(station)!.Value;
        Assert.Equal(StationFreshnessTracker.GapWindow, arrivals.GapCount);
        Assert.Equal(TimeSpan.FromSeconds(10), arrivals.SampleInterval);   // 10 of the last 15 gaps are 10 s
    }

    // ---------------------------------------------------------------- thresholds

    [Fact]
    public void Threshold_IsMissedSamplesTimesInterval_ClampedToFloorAndCeiling()
    {
        var options = StationFreshnessOptions.Default;
        Assert.Equal(TimeSpan.FromSeconds(12), options.StaleAfter(TimeSpan.FromSeconds(4)));
        Assert.Equal(TimeSpan.FromSeconds(3), options.StaleAfter(TimeSpan.FromSeconds(0.5)));     // floor
        Assert.Equal(TimeSpan.FromMinutes(5), options.StaleAfter(TimeSpan.FromSeconds(200)));     // ceiling
        Assert.Equal(TimeSpan.FromSeconds(60), options.StaleAfter(null));                         // not learned: fallback

        var fixedOverride = options with { FixedThreshold = TimeSpan.FromSeconds(30) };
        Assert.Equal(TimeSpan.FromSeconds(30), fixedOverride.StaleAfter(TimeSpan.FromSeconds(4)));
        Assert.Equal(TimeSpan.FromSeconds(30), fixedOverride.StaleAfter(null));
    }

    [Fact]
    public void Options_ComeFromConfiguration()
    {
        StationFreshnessOptions From(Dictionary<string, string?> values) =>
            StationFreshnessOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        var defaults = From(new());
        Assert.Equal(3, defaults.MissedSamples);
        Assert.Equal(TimeSpan.FromSeconds(3), defaults.Floor);
        Assert.Equal(TimeSpan.FromMinutes(5), defaults.Ceiling);
        Assert.Equal(TimeSpan.FromSeconds(60), defaults.Fallback);
        Assert.Null(defaults.FixedThreshold);

        var tuned = From(new()
        {
            ["Analytics:Freshness:MissedSamples"] = "5",
            ["Analytics:Freshness:FloorSeconds"] = "10",
            ["Analytics:Freshness:CeilingSeconds"] = "2",            // below the floor: raised to it
            ["Telemetry:PlcMemory:KeepAliveSeconds"] = "40"
        });
        Assert.Equal(5, tuned.MissedSamples);
        Assert.Equal(TimeSpan.FromSeconds(10), tuned.Ceiling);
        Assert.Equal(TimeSpan.FromSeconds(120), tuned.Fallback);     // three keep-alive intervals
        Assert.Equal(TimeSpan.FromSeconds(10), tuned.StaleAfter(TimeSpan.FromSeconds(1)));

        Assert.Equal(TimeSpan.FromSeconds(30), From(new() { ["Analytics:TelemetryStaleSeconds"] = "30" }).FixedThreshold);
    }

    // ---------------------------------------------------------------- live state transitions

    private static StationTelemetrySnapshot Running(DateTimeOffset at) => new() { OperationalStatus = "RUNNING", LastTelemetryAt = at };

    [Fact]
    public void LiveState_GoesStaleAfterThreeMissedSamples_AndBackToRunningOnTheNextArrival()
    {
        var station = Guid.NewGuid();
        var tracker = TrackerWithArrivals(station, 0, 4, 8, 12, 16);
        // The snapshot is older than the arrivals: later blocks were unchanged and dropped by the gate.
        var snapshot = Running(T0.AddSeconds(4));
        var none = Array.Empty<ControllerConnection>();
        var options = StationFreshnessOptions.Default;

        var fresh = StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), none, T0.AddSeconds(27.9), options);
        Assert.Equal("RUNNING", fresh.LiveState);
        Assert.Equal(T0.AddSeconds(16), fresh.LastDataAt);
        Assert.Equal(TimeSpan.FromSeconds(12), fresh.StaleAfter);
        Assert.Equal(TimeSpan.FromSeconds(4), fresh.SampleInterval);

        Assert.Equal("STALE", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), none, T0.AddSeconds(28.1), options).LiveState);

        tracker.RecordArrival(station, T0.AddSeconds(30));
        Assert.Equal("RUNNING", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), none, T0.AddSeconds(31), options).LiveState);

        // The fixed override wins over the learned threshold.
        var fixedOptions = options with { FixedThreshold = TimeSpan.FromSeconds(60) };
        Assert.Equal("RUNNING", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), none, T0.AddSeconds(80), fixedOptions).LiveState);
        Assert.Equal("STALE", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), none, T0.AddSeconds(91), fixedOptions).LiveState);
    }

    [Fact]
    public void LiveState_WithoutArrivals_UsesTheSnapshotAndTheFallbackThreshold()
    {
        var none = Array.Empty<ControllerConnection>();
        var snapshot = Running(T0);
        Assert.Equal("RUNNING", StationFreshnessPolicy.Evaluate(snapshot, null, none, T0.AddSeconds(59), StationFreshnessOptions.Default).LiveState);
        Assert.Equal("STALE", StationFreshnessPolicy.Evaluate(snapshot, null, none, T0.AddSeconds(61), StationFreshnessOptions.Default).LiveState);
        Assert.Equal("NO_DATA", StationFreshnessPolicy.Evaluate(null, null, none, T0, StationFreshnessOptions.Default).LiveState);
        Assert.Equal("UNKNOWN", StationFreshnessPolicy.Evaluate(new StationTelemetrySnapshot { LastTelemetryAt = T0 }, null, none, T0, StationFreshnessOptions.Default).LiveState);
    }

    [Fact]
    public void ConnectionLost_MakesTheStationOfflineImmediately_UnlessNewerDataArrived()
    {
        var station = Guid.NewGuid();
        var tracker = TrackerWithArrivals(station, 0, 4, 8, 12);
        var snapshot = Running(T0.AddSeconds(12));
        var options = StationFreshnessOptions.Default;
        var lost = new ControllerConnection("offline", T0.AddSeconds(13));

        // One second after the last sample (well inside the 12 s threshold) the will arrived: OFFLINE.
        Assert.Equal("OFFLINE", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), new[] { lost }, T0.AddSeconds(13), options).LiveState);
        // A graceful stop says the same.
        Assert.Equal("OFFLINE", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), new[] { new ControllerConnection("stopped", T0.AddSeconds(13)) }, T0.AddSeconds(13), options).LiveState);

        // Another bound controller is still online, or one never reports lifecycle: no evidence, no OFFLINE.
        Assert.Equal("RUNNING", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), new[] { lost, new ControllerConnection("online", T0) }, T0.AddSeconds(13), options).LiveState);
        Assert.Equal("RUNNING", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), new[] { lost, default(ControllerConnection) }, T0.AddSeconds(13), options).LiveState);

        // Data arrived after the will (agent reconnected before its "online" report was processed).
        tracker.RecordArrival(station, T0.AddSeconds(16));
        Assert.Equal("RUNNING", StationFreshnessPolicy.Evaluate(snapshot, tracker.Get(station), new[] { lost }, T0.AddSeconds(17), options).LiveState);

        // A station that never reported stays NO_DATA.
        Assert.Equal("NO_DATA", StationFreshnessPolicy.Evaluate(null, null, new[] { lost }, T0.AddSeconds(17), options).LiveState);
    }

    [Fact]
    public void ControllerConnection_ReadsTheLifecycleStateFromSystemMetadata()
    {
        var doc = JsonDocument.Parse("""{"AgentLifecycle":{"State":"offline","LastEventAtUtc":"2026-11-12T08:00:13Z"},"AdsPlcMemory":{}}""");
        var connection = ControllerConnection.FromSystemMetadata(doc);
        Assert.True(connection.IsDisconnected);
        Assert.Equal(T0.AddSeconds(13), connection.LifecycleChangedAt);

        Assert.False(ControllerConnection.FromSystemMetadata(JsonDocument.Parse("""{"AgentLifecycle":{"State":"online"}}""")).IsDisconnected);
        Assert.Null(ControllerConnection.FromSystemMetadata(JsonDocument.Parse("""{"Other":1}""")).LifecycleState);
        Assert.Null(ControllerConnection.FromSystemMetadata(null).LifecycleState);
    }

    // ---------------------------------------------------------------- fleet-stations endpoint

    private static ClientPc Controller(string name, Machine machine, string? lifecycleJson = null) => new()
    {
        Id = Guid.NewGuid(), Name = name, MachineIdentifier = name, MacAddress = "00:00:00:00:00:01", LastOnline = T0,
        ControlledMachines = { machine },
        SystemMetadata = lifecycleJson != null ? JsonDocument.Parse(lifecycleJson) : null
    };

    [Fact]
    public async Task FleetStations_ReportPerStationThresholdsAndIntervals_AndFollowTheClock()
    {
        var machine = new Machine { Id = Guid.NewGuid(), Name = "OP10" };
        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(machine.Id), Running(T0));
        var tracker = TrackerWithArrivals(machine.Id, 0, 4, 8, 12, 16, 20);
        var clock = new FakeClock(T0.AddSeconds(22));
        var service = new PredictiveMaintenanceService(new FakeAssetRepository { Machines = { machine } }, new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(), cache, NullLogger<PredictiveMaintenanceService>.Instance,
            freshness: tracker, clock: clock);

        var result = await service.GetFleetStationsAsync();
        Assert.Equal(3, result.StaleAfterMissedSamples);
        Assert.Equal(3, result.StaleFloorSeconds);
        Assert.Equal(300, result.StaleCeilingSeconds);
        Assert.Equal(60, result.TelemetryStaleAfterSeconds);            // stations without a learned interval
        var row = result.Stations.Single();
        Assert.Equal("RUNNING", row.LiveState);
        Assert.Equal(12, row.StaleAfterSeconds);
        Assert.Equal(4, row.SampleIntervalSeconds);
        Assert.Equal(2, row.TelemetryAgeSeconds);                       // from the last arrival, not the snapshot
        Assert.Equal(T0.AddSeconds(20), row.LastTelemetryAt);
        Assert.Equal(T0.AddSeconds(20), (await service.GetStationTelemetryKpisAsync()).Single().LastTelemetryAt);

        clock.Now = T0.AddSeconds(33);
        Assert.Equal("STALE", (await service.GetFleetStationsAsync()).Stations.Single().LiveState);

        var fixedResult = await service.GetFleetStationsAsync(freshness: new StationFreshnessOptions { FixedThreshold = TimeSpan.FromSeconds(60) });
        Assert.Null(fixedResult.StaleAfterMissedSamples);
        Assert.Equal(60, fixedResult.TelemetryStaleAfterSeconds);
        Assert.Equal(60, fixedResult.Stations.Single().StaleAfterSeconds);
        Assert.Equal("RUNNING", fixedResult.Stations.Single().LiveState);
    }

    [Fact]
    public async Task FleetStations_StationOfAControllerThatLostItsConnection_IsOffline()
    {
        var lostStation = new Machine { Id = Guid.NewGuid(), Name = "OP10" };
        var liveStation = new Machine { Id = Guid.NewGuid(), Name = "OP20" };
        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(lostStation.Id), Running(T0));
        await cache.SetAsync(StationTelemetryStore.Key(liveStation.Id), Running(T0));
        var controllers = new FakeControllerRepository
        {
            Items =
            {
                Controller("IPC-1", lostStation, """{"AgentLifecycle":{"State":"offline","LastEventAtUtc":"2026-11-12T08:00:01Z"}}"""),
                Controller("IPC-2", liveStation, """{"AgentLifecycle":{"State":"online","LastEventAtUtc":"2026-11-12T07:00:00Z"}}""")
            }
        };
        var service = new PredictiveMaintenanceService(new FakeAssetRepository { Machines = { lostStation, liveStation } }, controllers,
            new FakeMaintenanceTicketRepository(), cache, NullLogger<PredictiveMaintenanceService>.Instance,
            freshness: new StationFreshnessTracker(), clock: new FakeClock(T0.AddSeconds(2)));

        var rows = (await service.GetFleetStationsAsync()).Stations;
        Assert.Equal("OFFLINE", rows.Single(r => r.StationId == lostStation.Id.ToString()).LiveState);
        Assert.Equal("RUNNING", rows.Single(r => r.StationId == liveStation.Id.ToString()).LiveState);
    }

    [Fact]
    public async Task FleetStations_WithDbContext_ReadTheLifecycleStateOfStoredControllers()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var machine = new Machine { Id = Guid.NewGuid(), Name = "OP30" };
        var pc = new ClientPc
        {
            Id = Guid.NewGuid(), Name = "IPC-3", MachineIdentifier = "IPC-3", MacAddress = "00:00:00:00:00:03", LastOnline = T0,
            SystemMetadata = JsonDocument.Parse("""{"AgentLifecycle":{"State":"stopped","LastEventAtUtc":"2026-11-12T08:00:05Z"}}""")
        };
        db.Machines.Add(machine);
        db.ClientPcs.Add(pc);
        db.StationControllers.Add(new StationController { MachineId = machine.Id, ClientPcId = pc.Id, Role = "Primary" });
        await db.SaveChangesAsync();
        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(machine.Id), Running(T0));
        var service = new PredictiveMaintenanceService(new FakeAssetRepository { Machines = { machine } }, new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(), cache, NullLogger<PredictiveMaintenanceService>.Instance,
            db: db, freshness: new StationFreshnessTracker(), clock: new FakeClock(T0.AddSeconds(6)));

        Assert.Equal("OFFLINE", (await service.GetFleetStationsAsync()).Stations.Single().LiveState);
    }

    [Fact]
    public async Task AnalyticsController_PassesConfiguredFreshnessSettings()
    {
        var machine = new Machine { Id = Guid.NewGuid(), Name = "OP40" };
        var tracker = TrackerWithArrivals(machine.Id, 0, 2, 4, 6);
        var service = new PredictiveMaintenanceService(new FakeAssetRepository { Machines = { machine } }, new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(), new FakeMemoryCacheService(), NullLogger<PredictiveMaintenanceService>.Instance,
            freshness: tracker, clock: new FakeClock(T0.AddSeconds(8)));
        var controller = new AnalyticsController(service,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Analytics:Freshness:MissedSamples"] = "5" }).Build(),
            NullLogger<AnalyticsController>.Instance);

        var dto = Assert.IsType<FleetStationsDto>(Assert.IsType<OkObjectResult>((await controller.GetFleetStations()).Result).Value);
        Assert.Equal(5, dto.StaleAfterMissedSamples);
        Assert.Equal(10, dto.Stations.Single().StaleAfterSeconds);      // 5 × 2 s
        Assert.Equal("UNKNOWN", dto.Stations.Single().LiveState);       // arrivals, no snapshot state
    }
}
