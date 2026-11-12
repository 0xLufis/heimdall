using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using App.Contracts.Enums;
using App.Infrastructure.Repositories;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// Fleet analytics: hierarchy resolution, the grouped station list, the maintenance summary and
/// the trend metadata. Every expectation is derived from the rows the test stores; a figure
/// without rows must come back null / zero, never a default.
/// </summary>
public class FleetAnalyticsTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static PredictiveMaintenanceService Service(
        FakeAssetRepository assets,
        FakeMaintenanceTicketRepository? tickets = null,
        FakeControllerRepository? controllers = null,
        FakeMemoryCacheService? cache = null,
        AppDbContext? db = null,
        bool dbFacts = false) =>
        new(assets,
            controllers ?? new FakeControllerRepository(),
            tickets ?? new FakeMaintenanceTicketRepository(),
            cache ?? new FakeMemoryCacheService(),
            NullLogger<PredictiveMaintenanceService>.Instance,
            db != null ? new MachineGroupRepository(db) : null,
            db: dbFacts ? db : null);

    private static MachineGroup Group(string name, MachineGroupType type, MachineGroup? parent = null, params Guid[] machineIds) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        GroupType = type,
        ParentId = parent?.Id,
        MachineIdsJson = System.Text.Json.JsonSerializer.Serialize(machineIds)
    };

    // ---------------------------------------------------------------- hierarchy resolver

    [Fact]
    public void Resolver_FillsUpperLevelsFromTypedAncestorsOfTheForeignKey()
    {
        var bu = Group("E-Drive", MachineGroupType.BusinessUnit);
        var project = Group("EDU-3", MachineGroupType.Project, bu);
        var line = Group("Line 2", MachineGroupType.Line, project);
        var subLine = Group("Joining cell", MachineGroupType.SubLine, line);
        var resolver = new MachineHierarchyResolver(new[] { bu, project, line, subLine });

        var placement = resolver.Resolve(new Machine { Id = Guid.NewGuid(), Name = "OP40", SubLineGroupId = subLine.Id });

        Assert.Equal(new HierarchyLevel(subLine.Id, "Joining cell"), placement.SubLine);
        Assert.Equal(new HierarchyLevel(line.Id, "Line 2"), placement.Line);
        Assert.Equal(new HierarchyLevel(project.Id, "EDU-3"), placement.Project);
        Assert.Equal(new HierarchyLevel(bu.Id, "E-Drive"), placement.BusinessUnit);
    }

    [Fact]
    public void Resolver_ForeignKeyDefinesTheLevel_AndUntypedParentsAreNotGuessed()
    {
        // Groups created in the UI are all "Manual": only the machine's own column says what they are.
        var plant = Group("Plant 1", MachineGroupType.Manual);
        var line = Group("Line A", MachineGroupType.Manual, plant);
        var resolver = new MachineHierarchyResolver(new[] { plant, line });

        var placement = resolver.Resolve(new Machine { Id = Guid.NewGuid(), Name = "OP10", LineGroupId = line.Id });

        Assert.Equal(new HierarchyLevel(line.Id, "Line A"), placement.Line);
        Assert.False(placement.Project.IsAssigned);
        Assert.False(placement.BusinessUnit.IsAssigned);
        Assert.False(placement.SubLine.IsAssigned);
    }

    [Fact]
    public void Resolver_UsesMembershipListsAndMembershipRows()
    {
        var machineId = Guid.NewGuid();
        var line = Group("Line 2", MachineGroupType.Line);
        var subLine = Group("Joining cell", MachineGroupType.SubLine, line, machineId);   // id list
        var technology = Group("Laser fleet", MachineGroupType.Technological, null, machineId);
        var project = Group("EDU-3", MachineGroupType.Project);
        var resolver = new MachineHierarchyResolver(
            new[] { line, subLine, technology, project },
            new[] { (project.Id, machineId), (Guid.NewGuid(), machineId) });       // membership rows (one for an unknown group)

        var placement = resolver.Resolve(new Machine { Id = machineId, Name = "OP50" });

        Assert.Equal("Joining cell", placement.SubLine.Name);
        Assert.Equal("Line 2", placement.Line.Name);              // typed parent of the sub-line
        Assert.Equal("EDU-3", placement.Project.Name);            // membership row
        Assert.False(placement.BusinessUnit.IsAssigned);          // a technology group is not a hierarchy level
    }

    [Fact]
    public void Resolver_LegacyGroupId_IsAGroupIdOrALineNameButNeverADanglingGuid()
    {
        var line = Group("NV Line 1", MachineGroupType.Line);
        var resolver = new MachineHierarchyResolver(new[] { line });

        var byId = resolver.Resolve(new Machine { Id = Guid.NewGuid(), Name = "A", GroupId = line.Id.ToString() });
        Assert.Equal(new HierarchyLevel(line.Id, "NV Line 1"), byId.Line);

        var byText = resolver.Resolve(new Machine { Id = Guid.NewGuid(), Name = "B", GroupId = "  Line 07 – Metrology " });
        Assert.Equal(new HierarchyLevel(null, "Line 07 – Metrology"), byText.Line);
        Assert.Equal("Line 07 – Metrology", byText.Line.Key);

        var dangling = resolver.Resolve(new Machine { Id = Guid.NewGuid(), Name = "C", GroupId = Guid.NewGuid().ToString() });
        Assert.False(dangling.Line.IsAssigned);

        var none = resolver.Resolve(new Machine { Id = Guid.NewGuid(), Name = "D" });
        Assert.False(none.Line.IsAssigned);
        Assert.Null(none.Line.Key);
    }

    [Fact]
    public void Resolver_TerminatesOnParentCycles()
    {
        var a = Group("A", MachineGroupType.Line);
        var b = Group("B", MachineGroupType.Project);
        a.ParentId = b.Id;
        b.ParentId = a.Id;
        var resolver = new MachineHierarchyResolver(new[] { a, b });

        var placement = resolver.Resolve(new Machine { Id = Guid.NewGuid(), Name = "OP", LineGroupId = a.Id });

        Assert.Equal("A", placement.Line.Name);
        Assert.Equal("B", placement.Project.Name);
    }

    // ---------------------------------------------------------------- station list (repository facts)

    [Fact]
    public async Task FleetStations_ReportDimensionsLiveStateTicketFiguresAndControllers()
    {
        await using var db = NewDb();
        var line = Group("Line 1", MachineGroupType.Line);
        db.MachineGroups.Add(line);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var fresh = new Machine { Id = Guid.NewGuid(), Name = "OP10", DisplayName = "OP10 – Press", CustomIdentifier = "L01-OP010", PinnedObjectHandle = "9C1D", OrganizationId = "org-a", MachineType = "ServoPress", LineGroupId = line.Id };
        var stale = new Machine { Id = Guid.NewGuid(), Name = "OP20", CustomIdentifier = "L02-OP020", OrganizationId = "org-b", MachineType = "RobotCell", GroupId = "Line 02 – legacy", Status = "Maintenance" };
        var silent = new Machine { Id = Guid.NewGuid(), Name = "OP30", CustomIdentifier = "L02-OP030" };
        var retired = new Machine { Id = Guid.NewGuid(), Name = "OP99", Status = "decommissioned" };
        var assets = new FakeAssetRepository { Machines = { fresh, stale, silent, retired } };

        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(fresh.Id), new StationTelemetrySnapshot
        {
            MachineId = fresh.Id, Availability = 90, Performance = 80, Quality = 100, PartsPerHour = 120, TargetPartsPerHour = 150,
            TemperatureC = 41.5, OperationalStatus = "RUNNING", LastTelemetryAt = now.AddSeconds(-4)
        });
        await cache.SetAsync(StationTelemetryStore.Key(stale.Id), new StationTelemetrySnapshot
        {
            MachineId = stale.Id, Oee = 71, OperationalStatus = "RUNNING", LastTelemetryAt = now.AddMinutes(-10)
        });

        var tickets = new FakeMaintenanceTicketRepository
        {
            Tickets =
            {
                new MaintenanceTicket { MachineId = stale.Id, Priority = "Critical", Status = "Open", CreatedAt = now.AddHours(-13) },
                new MaintenanceTicket { MachineId = stale.Id, Priority = "Low", Status = "Resolved", CreatedAt = now.AddDays(-2), ResolvedAt = now.AddDays(-2).AddMinutes(90), LineStopDurationMinutes = 30 },
                new MaintenanceTicket { MachineId = stale.Id, Priority = "Low", Status = "Closed", CreatedAt = now.AddDays(-40), ResolvedAt = now.AddDays(-40).AddMinutes(600), LineStopDurationMinutes = 45 },
                new MaintenanceTicket { MachineId = null, Priority = "High", Status = "Open", CreatedAt = now.AddHours(-1) }
            }
        };
        var controllers = new FakeControllerRepository
        {
            Items =
            {
                new ClientPc { Id = Guid.NewGuid(), Name = "IPC-1", MacAddress = "00:00:00:00:00:01", LastOnline = now.AddSeconds(-20), ControlledMachines = { fresh } },
                new ClientPc { Id = Guid.NewGuid(), Name = "IPC-2", MacAddress = "00:00:00:00:00:02", LastOnline = now.AddHours(-1), ControlledMachines = { fresh, stale } }
            }
        };

        var result = await Service(assets, tickets, controllers, cache, db).GetFleetStationsAsync();

        Assert.Equal(30, result.WindowDays);
        Assert.Equal(60, result.TelemetryStaleAfterSeconds);
        Assert.Equal(300, result.ControllerOnlineWindowSeconds);
        Assert.Equal(12, result.CriticalSlaHours);
        Assert.Equal(3, result.Stations.Count);                         // the decommissioned machine is left out
        Assert.DoesNotContain(result.Stations, s => s.StationId == retired.Id.ToString());

        var a = result.Stations.Single(s => s.StationId == fresh.Id.ToString());
        Assert.Equal("OP10 – Press", a.Name);
        Assert.Equal("OP10", a.MachineName);
        Assert.Equal("L01-OP010", a.CustomIdentifier);
        Assert.Equal("9C1D", a.Handle);
        Assert.Equal("9C1D", a.PinnedObjectHandle);
        Assert.Equal("org-a", a.OrganizationId);
        Assert.Null(a.OrganizationName);                                // no organization rows were given
        Assert.Equal("ServoPress", a.MachineType);
        Assert.Equal("active", a.LifecycleStatus);
        Assert.Equal(line.Id.ToString(), a.LineId);
        Assert.Equal("Line 1", a.LineName);
        Assert.Equal("RUNNING", a.LiveState);
        Assert.InRange(a.TelemetryAgeSeconds!.Value, 3, 30);
        Assert.Equal(72.0, a.Oee);                                      // 90 x 80 x 100 / 10000
        Assert.Equal(120, a.PartsPerHour);
        Assert.Equal(150, a.TargetPartsPerHour);
        Assert.Equal(41.5, a.TemperatureC);
        Assert.Equal(0, a.ActiveTicketCount);
        Assert.Equal(0, a.IncidentCount);
        Assert.Null(a.MttrMinutes);
        Assert.Null(a.DowntimeMinutes);
        Assert.Null(a.LastIncidentAt);
        Assert.Equal(2, a.ControllerCount);
        Assert.Equal(1, a.ControllersOnline);
        Assert.NotNull(a.ControllerLastSeenAt);

        var b = result.Stations.Single(s => s.StationId == stale.Id.ToString());
        Assert.Null(b.LineId);
        Assert.Equal("Line 02 – legacy", b.LineName);                   // legacy line text, no machine group row
        Assert.Equal("maintenance", b.LifecycleStatus);
        Assert.Equal("STALE", b.LiveState);                             // last sample 10 minutes ago
        Assert.Equal("RUNNING", b.OperationalStatus);                   // the last *reported* state stays visible
        Assert.Equal(71, b.Oee);
        Assert.Equal(1, b.ActiveTicketCount);
        Assert.Equal(1, b.CriticalTicketCount);
        Assert.True(b.HasSlaBreach);                                    // open Critical ticket older than 12 h
        Assert.Equal(2, b.IncidentCount);                               // the 40-day-old ticket is outside the window
        Assert.Equal(1, b.ResolvedIncidentCount);
        Assert.Equal(90.0, b.MttrMinutes);
        Assert.Equal(30.0, b.DowntimeMinutes);
        Assert.NotNull(b.OldestOpenTicketAt);
        Assert.Equal(1, b.ControllerCount);
        Assert.Equal(0, b.ControllersOnline);

        var c = result.Stations.Single(s => s.StationId == silent.Id.ToString());
        Assert.Equal("NO_DATA", c.LiveState);
        Assert.Null(c.TelemetryAgeSeconds);
        Assert.Null(c.Oee);
        Assert.Null(c.LineName);
        Assert.Null(c.MachineType);
        Assert.Equal(0, c.ControllerCount);
        Assert.Null(c.ControllerLastSeenAt);
    }

    [Fact]
    public async Task FleetStations_WindowOnlyMovesTicketFigures_AndDecommissionedIsOptIn()
    {
        var now = DateTimeOffset.UtcNow;
        var machine = new Machine { Id = Guid.NewGuid(), Name = "OP20" };
        var retired = new Machine { Id = Guid.NewGuid(), Name = "OP99", Status = "dismantled" };
        var tickets = new FakeMaintenanceTicketRepository
        {
            Tickets =
            {
                new MaintenanceTicket { MachineId = machine.Id, Status = "Resolved", CreatedAt = now.AddDays(-2), ResolvedAt = now.AddDays(-2).AddMinutes(90) },
                new MaintenanceTicket { MachineId = machine.Id, Status = "Closed", CreatedAt = now.AddDays(-40), ResolvedAt = now.AddDays(-40).AddMinutes(600), LineStopDurationMinutes = 45 }
            }
        };
        var service = Service(new FakeAssetRepository { Machines = { machine, retired } }, tickets);

        var week = (await service.GetFleetStationsAsync(TimeSpan.FromDays(7))).Stations.Single();
        Assert.Equal(1, week.IncidentCount);
        Assert.Equal(90.0, week.MttrMinutes);
        Assert.Null(week.DowntimeMinutes);

        var quarter = await service.GetFleetStationsAsync(TimeSpan.FromDays(90), includeDecommissioned: true);
        Assert.Equal(90, quarter.WindowDays);
        Assert.Equal(2, quarter.Stations.Count);
        var row = quarter.Stations.Single(s => s.StationId == machine.Id.ToString());
        Assert.Equal(2, row.IncidentCount);
        Assert.Equal(345.0, row.MttrMinutes);                           // (90 + 600) / 2
        Assert.Equal(45.0, row.DowntimeMinutes);
        Assert.Equal("dismantled", quarter.Stations.Single(s => s.StationId == retired.Id.ToString()).LifecycleStatus);

        // Out-of-range windows are clamped, not rejected.
        Assert.Equal(1, (await service.GetFleetStationsAsync(TimeSpan.FromHours(2))).WindowDays);
        Assert.Equal(365, (await service.GetFleetStationsAsync(TimeSpan.FromDays(5000))).WindowDays);
        Assert.Equal(30, (await service.GetFleetStationsAsync(TimeSpan.Zero)).WindowDays);
    }

    [Fact]
    public async Task FleetStations_StaleThresholdIsConfigurable_AndFreshTelemetryWithoutStateIsUnknown()
    {
        var now = DateTimeOffset.UtcNow;
        var machine = new Machine { Id = Guid.NewGuid(), Name = "OP70" };
        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(machine.Id), new StationTelemetrySnapshot
        {
            MachineId = machine.Id, VibrationMmS = 1.2, LastTelemetryAt = now.AddSeconds(-90)
        });
        var service = Service(new FakeAssetRepository { Machines = { machine } }, cache: cache);

        Assert.Equal("STALE", (await service.GetFleetStationsAsync()).Stations.Single().LiveState);
        var relaxed = await service.GetFleetStationsAsync(telemetryStaleAfter: TimeSpan.FromMinutes(5));
        Assert.Equal(300, relaxed.TelemetryStaleAfterSeconds);
        Assert.Equal("UNKNOWN", relaxed.Stations.Single().LiveState);   // reporting, but the PLC sends no state

        var controller = new AnalyticsController(service,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:PlcMemory:KeepAliveSeconds"] = "40" }).Build(),
            NullLogger<AnalyticsController>.Instance);
        var viaKeepAlive = Assert.IsType<FleetStationsDto>(Assert.IsType<OkObjectResult>((await controller.GetFleetStations()).Result).Value);
        Assert.Equal(120, viaKeepAlive.TelemetryStaleAfterSeconds);     // three keep-alive intervals
        Assert.Equal("UNKNOWN", viaKeepAlive.Stations.Single().LiveState);

        var explicitConfig = new AnalyticsController(service,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Analytics:TelemetryStaleSeconds"] = "30" }).Build(),
            NullLogger<AnalyticsController>.Instance);
        var viaSetting = Assert.IsType<FleetStationsDto>(Assert.IsType<OkObjectResult>((await explicitConfig.GetFleetStations(windowDays: 7)).Result).Value);
        Assert.Equal(30, viaSetting.TelemetryStaleAfterSeconds);
        Assert.Equal(7, viaSetting.WindowDays);
        Assert.Equal("STALE", viaSetting.Stations.Single().LiveState);
    }

    // ---------------------------------------------------------------- station list (DbContext facts)

    [Fact]
    public async Task FleetStations_WithDbContext_ReadStoredTicketsControllersOrganizationsAndMemberships()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;
        var bu = Group("E-Drive", MachineGroupType.BusinessUnit);
        var line = Group("NV Line 2", MachineGroupType.Line, bu);
        var subLine = Group("Joining cell", MachineGroupType.SubLine, line);
        db.MachineGroups.AddRange(bu, line, subLine);
        db.AuthOrganizations.Add(new AuthOrganization { Id = "org-a", Name = "Plant A" });

        var machine = new Machine { Id = Guid.NewGuid(), Name = "NV-L2-OP040", CustomIdentifier = "NV-L2-OP040", OrganizationId = "org-a", LineGroupId = line.Id };
        var other = new Machine { Id = Guid.NewGuid(), Name = "NV-L2-OP010", CustomIdentifier = "NV-L2-OP010", OrganizationId = "org-zz" };
        db.Machines.AddRange(machine, other);
        var online = new ClientPc { Id = Guid.NewGuid(), Name = "IPC-A", MacAddress = "00:00:00:00:00:0A", LastOnline = now.AddSeconds(-5) };
        var offline = new ClientPc { Id = Guid.NewGuid(), Name = "IPC-B", MacAddress = "00:00:00:00:00:0B" };
        db.ClientPcs.AddRange(online, offline);
        await db.SaveChangesAsync();

        db.StationControllers.AddRange(
            new StationController { MachineId = machine.Id, ClientPcId = online.Id, Role = "Primary" },
            new StationController { MachineId = machine.Id, ClientPcId = offline.Id, Role = "Backup" });
        db.MachineGroupMemberships.Add(new MachineGroupMembership { GroupId = subLine.Id, MachineId = machine.Id });
        db.MaintenanceTickets.AddRange(
            new MaintenanceTicket { Title = "Jam", MachineId = machine.Id, Priority = "High", Status = "InProgress", CreatedAt = now.AddHours(-5), AlarmCode = "E-JAM" },
            new MaintenanceTicket { Title = "Old", MachineId = machine.Id, Priority = "Low", Status = "Resolved", CreatedAt = now.AddDays(-3), ResolvedAt = now.AddDays(-3).AddMinutes(30) });
        await db.SaveChangesAsync();

        // Repositories hold nothing: every ticket / controller fact must come from the context.
        var service = Service(new FakeAssetRepository { Machines = { machine, other } }, db: db, dbFacts: true);

        var result = await service.GetFleetStationsAsync(TimeSpan.FromDays(7));

        var row = result.Stations.Single(s => s.StationId == machine.Id.ToString());
        Assert.Equal("Plant A", row.OrganizationName);
        Assert.Equal("NV Line 2", row.LineName);
        Assert.Equal(subLine.Id.ToString(), row.SubLineId);             // membership row
        Assert.Equal("Joining cell", row.SubLineName);
        Assert.Equal("E-Drive", row.BusinessUnitName);                  // typed ancestor of the line
        Assert.Null(row.ProjectName);
        Assert.Equal(1, row.ActiveTicketCount);
        Assert.Equal(2, row.IncidentCount);
        Assert.Equal(30.0, row.MttrMinutes);
        Assert.Equal(2, row.ControllerCount);
        Assert.Equal(1, row.ControllersOnline);

        var unassigned = result.Stations.Single(s => s.StationId == other.Id.ToString());
        Assert.Equal("org-zz", unassigned.OrganizationId);
        Assert.Null(unassigned.OrganizationName);                       // unknown organization: no invented name
        Assert.Null(unassigned.LineName);
        Assert.Equal(0, unassigned.ControllerCount);

        // The summary reads the same stored facts.
        var summary = await service.GetFleetSummaryAsync(TimeSpan.FromDays(7));
        Assert.Equal(2, summary.ControllerCount);
        Assert.Equal(1, summary.ControllersOnline);
        Assert.Equal(50.0, summary.ControllerOnlinePercentage);
        Assert.Equal(2, summary.IncidentCount);
        var top = Assert.Single(summary.TopFaultingMachines);
        Assert.Equal("NV Line 2", top.Line);
        Assert.Equal("NV-L2-OP040", top.Identifier);
        Assert.Equal(1, top.OpenTicketCount);
        Assert.Equal("E-JAM", top.PrimaryAlarmCode);
    }

    // ---------------------------------------------------------------- fleet summary

    [Fact]
    public async Task FleetSummary_IsComputedFromTicketsControllersAndStock_ForTheRequestedWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var repeat = new Machine { Id = Guid.NewGuid(), Name = "OP50", CustomIdentifier = "L03-OP050", GroupId = "Line 03" };
        var single = new Machine { Id = Guid.NewGuid(), Name = "OP60", CustomIdentifier = "L03-OP060", GroupId = "Line 03" };
        var assets = new FakeAssetRepository
        {
            Machines = { repeat, single },
            Items =
            {
                new StockItemStub("Fuse 10A", 0, 5),
                new StockItemStub("Bearing 6204", 2, 4),
                new StockItemStub("Gripper pad", 9, 3),            // above its threshold: not listed
                new StockItemStub("Untracked", 0, null)            // no threshold: not listed
            }
        };
        var tickets = new FakeMaintenanceTicketRepository
        {
            Tickets =
            {
                new MaintenanceTicket { MachineId = repeat.Id, Priority = "Critical", Status = "Open", CreatedAt = now.AddHours(-20), AlarmCode = "E-TORQUE" },
                new MaintenanceTicket { MachineId = repeat.Id, Priority = "Low", Status = "Resolved", CreatedAt = now.AddHours(-68), ResolvedAt = now.AddHours(-67), LineStopDurationMinutes = 25, AlarmCode = "E-TORQUE" },
                new MaintenanceTicket { MachineId = single.Id, Priority = "Medium", Status = "Closed", CreatedAt = now.AddDays(-5), ResolvedAt = now.AddDays(-5).AddMinutes(30) },
                new MaintenanceTicket { MachineId = single.Id, Priority = "Medium", Status = "Closed", CreatedAt = now.AddDays(-50), ResolvedAt = now.AddDays(-50).AddMinutes(300) },
                new MaintenanceTicket { MachineId = null, Priority = "Low", Status = "Open", CreatedAt = now.AddHours(-2) }
            }
        };
        var controllers = new FakeControllerRepository
        {
            Items =
            {
                new ClientPc { Id = Guid.NewGuid(), Name = "A", MacAddress = "00:00:00:00:00:01", LastOnline = now.AddMinutes(-1) },
                new ClientPc { Id = Guid.NewGuid(), Name = "B", MacAddress = "00:00:00:00:00:02", LastOnline = now.AddMinutes(-30) },
                new ClientPc { Id = Guid.NewGuid(), Name = "C", MacAddress = "00:00:00:00:00:03" }
            }
        };
        var service = Service(assets, tickets, controllers);

        var summary = await service.GetFleetSummaryAsync();

        Assert.Equal(30, summary.WindowDays);
        Assert.Equal(2, summary.TotalMachines);
        Assert.Equal(3, summary.ControllerCount);
        Assert.Equal(1, summary.ControllersOnline);
        Assert.Equal(33.3, summary.ControllerOnlinePercentage);
        Assert.Equal(4, summary.IncidentCount);                       // the 50-day-old ticket is outside the window
        Assert.Equal(2, summary.ResolvedIncidentCount);
        Assert.Equal(45.0, summary.AverageMttrMinutes);               // (60 + 30) / 2
        Assert.Equal(48.0, summary.AverageMtbfHours);                 // one machine with two tickets 48 h apart
        Assert.Equal(1, summary.MtbfMachineCount);
        Assert.Equal(12, summary.CriticalSlaHours);

        Assert.Equal(2, summary.MaintenanceBacklog.Under24Hours);     // 20 h and 2 h old
        Assert.Equal(0, summary.MaintenanceBacklog.OneToThreeDays);
        Assert.Equal(0, summary.MaintenanceBacklog.OverThreeDays);
        Assert.Equal(1, summary.MaintenanceBacklog.CriticalBreached);

        Assert.Equal(2, summary.TopFaultingMachines.Count);
        var top = summary.TopFaultingMachines[0];
        Assert.Equal(repeat.Id.ToString(), top.MachineId);
        Assert.Equal("L03-OP050", top.Identifier);
        Assert.Equal("Line 03", top.Line);                            // legacy line text
        Assert.Equal(2, top.IncidentCount);
        Assert.Equal(1, top.OpenTicketCount);
        Assert.Equal(25.0, top.TotalDowntimeMinutes);
        Assert.Equal("E-TORQUE", top.PrimaryAlarmCode);
        Assert.Null(summary.TopFaultingMachines[1].TotalDowntimeMinutes);
        Assert.Equal(string.Empty, summary.TopFaultingMachines[1].PrimaryAlarmCode);

        Assert.Equal(2, summary.StockDepletionTotal);
        Assert.Equal(new[] { "Fuse 10A", "Bearing 6204" }, summary.StockDepletion.Select(s => s.PartNumber));
        Assert.Equal("Critical", summary.StockDepletion[0].Criticality);
        Assert.Equal("Warning", summary.StockDepletion[1].Criticality);

        var wide = await service.GetFleetSummaryAsync(TimeSpan.FromDays(90));
        Assert.Equal(90, wide.WindowDays);
        Assert.Equal(5, wide.IncidentCount);
        Assert.Equal(2, wide.MtbfMachineCount);
        Assert.Equal(130.0, wide.AverageMttrMinutes);                 // (60 + 30 + 300) / 3
    }

    private sealed class StockItemStub : BaseInventoryItem
    {
        public StockItemStub(string name, int quantity, int? threshold)
        {
            Id = Guid.NewGuid();
            Name = name;
            StockQuantity = quantity;
            MinStockThreshold = threshold;
        }
    }

    // ---------------------------------------------------------------- lines without machine groups

    [Fact]
    public async Task LineKpisAndStationKpis_FallBackToTheLegacyLineText()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new Machine { Id = Guid.NewGuid(), Name = "OP10", GroupId = "Line 01 – Module" };
        var b = new Machine { Id = Guid.NewGuid(), Name = "OP20", GroupId = "Line 01 – Module" };
        var c = new Machine { Id = Guid.NewGuid(), Name = "OP30", GroupId = "Line 02 – Pack" };
        var unassigned = new Machine { Id = Guid.NewGuid(), Name = "OP40" };
        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(a.Id), new StationTelemetrySnapshot { MachineId = a.Id, Oee = 80, LastTelemetryAt = now });
        await cache.SetAsync(StationTelemetryStore.Key(b.Id), new StationTelemetrySnapshot { MachineId = b.Id, Oee = 90, LastTelemetryAt = now });
        var tickets = new FakeMaintenanceTicketRepository
        {
            Tickets = { new MaintenanceTicket { MachineId = c.Id, Status = "Open", CreatedAt = now.AddHours(-3) } }
        };
        var service = Service(new FakeAssetRepository { Machines = { a, b, c, unassigned } }, tickets, cache: cache);

        var lines = await service.GetLineKpisAsync();
        Assert.Equal(new[] { "Line 01 – Module", "Line 02 – Pack" }, lines.Select(l => l.LineName));
        Assert.Equal("Line 01 – Module", lines[0].LineId);            // no group row: the name is the key
        Assert.Equal(85.0, lines[0].Oee);
        Assert.Equal(0, lines[0].IncidentCount);
        Assert.Null(lines[1].Oee);
        Assert.Equal(1, lines[1].IncidentCount);

        var stations = await service.GetStationTelemetryKpisAsync();
        Assert.Equal("Line 01 – Module", stations.Single(s => s.StationId == a.Id.ToString()).LineName);
        Assert.Null(stations.Single(s => s.StationId == a.Id.ToString()).LineId);
        Assert.Null(stations.Single(s => s.StationId == unassigned.Id.ToString()).LineName);
        Assert.Equal(2, (await service.GetStationTelemetryKpisAsync(lineId: "line 01 – module")).Count);
    }

    // ---------------------------------------------------------------- trends

    [Fact]
    public async Task Trends_ResolveCatalogKeys_ReportBufferCoverageAndTheStationsOwnTarget()
    {
        await using var db = NewDb();
        var machine = new Machine { Id = Guid.NewGuid(), Name = "OP30", CustomIdentifier = "L01-OP030" };
        var history = new DbPlantDataProvider(db, new MachineGroupRepository(db), new FakeMaintenanceTicketRepository(), NullLogger<DbPlantDataProvider>.Instance);
        foreach (var v in new[] { 16480.0, 16510.0, 16495.0 }) await history.EmitTelemetryAsync(machine.Id.ToString(), "cycle_time_ms", v);
        foreach (var v in new[] { 36.5, 36.9 }) await history.EmitTelemetryAsync(machine.Id.ToString(), "temperature_c", v);

        var cache = new FakeMemoryCacheService();
        await cache.SetAsync(StationTelemetryStore.Key(machine.Id), new StationTelemetrySnapshot
        {
            MachineId = machine.Id, TargetCycleTimeSeconds = 16, LastTelemetryAt = DateTimeOffset.UtcNow
        });
        var service = new PredictiveMaintenanceService(new FakeAssetRepository { Machines = { machine } }, new FakeControllerRepository(),
            new FakeMaintenanceTicketRepository(), cache, NullLogger<PredictiveMaintenanceService>.Instance, telemetryHistory: history);

        // "cycle_time" is the BFF catalog key of the buffered "cycle_time_ms" series.
        var cycle = await service.GetMachineTrendsAsync(machine.Id.ToString(), "cycle_time", TimeSpan.FromHours(1));
        Assert.Equal("cycle_time_ms", cycle.MetricKey);
        Assert.Equal("ms", cycle.Unit);
        Assert.Equal(new[] { 16480.0, 16510.0, 16495.0 }, cycle.Points.Select(p => p.Value));
        Assert.Equal(16000.0, cycle.NominalValue);                    // the station's reported target, not a constant
        Assert.Null(cycle.UpperTolerance);
        Assert.Null(cycle.LowerTolerance);
        Assert.Equal(PredictiveMaintenanceService.TrendBufferCapacity, cycle.BufferCapacity);
        Assert.Equal(3, cycle.BufferedSampleCount);
        Assert.NotNull(cycle.BufferedFrom);
        Assert.True(cycle.BufferedTo >= cycle.BufferedFrom);
        Assert.NotNull(cycle.StatisticalTrends);
        Assert.Equal("Stable", cycle.StatisticalTrends!.TrendDirection);  // a few ms on 16.5 s is no drift
        Assert.Equal(new[] { "cycle_time_ms", "temperature_c" }, cycle.AvailableMetrics.Select(m => m.Key));
        Assert.Equal(new[] { 3, 2 }, cycle.AvailableMetrics.Select(m => m.SampleCount));

        // Catalog key for the temperature series; the station reports no temperature target.
        var temperature = await service.GetMachineTrendsAsync("L01-OP030", "motor_temp_c", TimeSpan.FromHours(1));
        Assert.Equal("temperature_c", temperature.MetricKey);
        Assert.Equal(2, temperature.Points.Count);
        Assert.Null(temperature.NominalValue);

        // A metric the station never reported: no points, no statistics, but the options still say what exists.
        var oee = await service.GetMachineTrendsAsync(machine.Id.ToString(), "oee_percentage", TimeSpan.FromHours(1));
        Assert.Equal("oee_pct", oee.MetricKey);
        Assert.Empty(oee.Points);
        Assert.Null(oee.StatisticalTrends);
        Assert.Equal(0, oee.BufferedSampleCount);
        Assert.Equal(2, oee.AvailableMetrics.Count);

        // A key without any history source at all.
        var unknown = await service.GetMachineTrendsAsync(machine.Id.ToString(), "hydraulic_pressure_bar", TimeSpan.FromHours(1));
        Assert.Equal(string.Empty, unknown.MetricKey);
        Assert.Empty(unknown.Points);
        Assert.Equal(2, unknown.AvailableMetrics.Count);

        // A window that ends before the samples: buffered, but nothing inside the window.
        var controller = new AnalyticsController(service, new ConfigurationBuilder().Build(), NullLogger<AnalyticsController>.Instance);
        var all = Assert.IsType<MachineTrendSeriesDto>(Assert.IsType<OkObjectResult>((await controller.GetMachineTrends(machine.Id.ToString(), "cycle_time", "all")).Result).Value);
        Assert.Equal(3, all.Points.Count);
        var fiveMinutes = Assert.IsType<MachineTrendSeriesDto>(Assert.IsType<OkObjectResult>((await controller.GetMachineTrends(machine.Id.ToString(), "cycle_time", "5m")).Result).Value);
        Assert.Equal(3, fiveMinutes.Points.Count);
    }
}
