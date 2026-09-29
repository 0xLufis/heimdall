using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Hubs;
using App.Backend.Api.Services;
using App.Contracts.Enums;
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

public class TicketingComprehensiveTests
{
    private readonly AppDbContext _context;
    private readonly MaintenanceTicketRepository _repository;
    private readonly TestHubContext _hubContext;
    private readonly CacheService _cacheService;
    private readonly MaintenanceTicketController _controller;

    public TicketingComprehensiveTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repository = new MaintenanceTicketRepository(_context);

        _hubContext = new TestHubContext();

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _cacheService = new CacheService(distributedCache, memoryCache, NullLogger<CacheService>.Instance);

        var factory = new TestDbContextFactory(options);

        _controller = new MaintenanceTicketController(
            _repository,
            _hubContext,
            _cacheService,
            factory
        );
    }

    [Fact]
    public async Task CreateTicket_WithRobotCollision_TriggersAutomaticEscalationAndHandOff()
    {
        var ticket = new MaintenanceTicket
        {
            Title = "Emergency: KUKA Robot Collision on Axis 2",
            Description = "Impact sensor triggered during welding fixture movement",
            MachineId = Guid.NewGuid(),
            ResponsibleDepartment = "Robotics",
            IsLineStop = true
        };

        var result = await _controller.CreateTicket(ticket);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var createdTicket = Assert.IsType<MaintenanceTicket>(created.Value);

        Assert.True(createdTicket.IsEscalated);
        Assert.Equal("DedicatedEngineer", createdTicket.EscalationTarget);
        Assert.Equal(EscalationHandoverState.HandOff, createdTicket.EscalationHandoverState);
        Assert.Contains("Robot collision detected", createdTicket.EscalationReason);
        Assert.NotNull(createdTicket.TelemetrySnapshot);
        Assert.Contains("Plc_State", createdTicket.TelemetrySnapshot);
    }

    [Fact]
    public async Task EscalateTicket_SupportsHandoverStates_NotificationAndParallelWork()
    {
        var ticket = await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "Spindle bearing thermal drift",
            Status = "InProgress",
            ResponsibleDepartment = "Mechanical"
        });

        var request = new EscalateTicketRequest(
            Reason: "Needs secondary vibration analysis by OEM specialist",
            EscalatedBy: "Gabor Varga",
            HandoverState: EscalationHandoverState.ParallelWork,
            Target: "OEMSupport"
        );

        var actionResult = await _controller.EscalateTicket(ticket.Id, request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var updated = Assert.IsType<MaintenanceTicket>(okResult.Value);

        Assert.True(updated.IsEscalated);
        Assert.Equal(EscalationHandoverState.ParallelWork, updated.EscalationHandoverState);
        Assert.Equal("OEMSupport", updated.EscalationTarget);
        Assert.Equal("Gabor Varga", updated.EscalatedBy);
    }

    [Fact]
    public async Task ReserveTicket_AllowsTechnicianToReserveOpenTicket()
    {
        var ticket = await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "Conveyor belt tension adjustment",
            Status = "Open",
            ResponsibleDepartment = "Assy"
        });

        var result = await _controller.ReserveTicket(ticket.Id, new ReserveTicketRequest("Zoltan Nemeth"));
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<MaintenanceTicket>(okResult.Value);

        Assert.Equal("Zoltan Nemeth", updated.ReservedBy);
        Assert.Equal("Zoltan Nemeth", updated.AssignedTo);
        Assert.Equal("Open", updated.Status);
    }

    [Fact]
    public async Task QrPickupTicket_SetsStartedAtAndCalculatesReactionTime()
    {
        var ticket = await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "SMT Feeder jam on lane 4",
            Status = "Open",
            ResponsibleDepartment = "SMT",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-12)
        });

        var result = await _controller.QrPickupTicket(ticket.Id, new QrPickupRequest("Peter Kovacs"));
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<MaintenanceTicket>(okResult.Value);

        Assert.Equal("InProgress", updated.Status);
        Assert.NotNull(updated.QrScannedAt);
        Assert.NotNull(updated.StartedAt);
        Assert.Equal("Peter Kovacs", updated.AssignedTo);
        Assert.NotNull(updated.ReactionTimeMinutes);
        Assert.True(updated.ReactionTimeMinutes >= 11.9);
    }

    [Fact]
    public async Task StoppageStats_CalculatesWeeklyStoppageAndDepartmentBreakdown()
    {
        var machineId = Guid.NewGuid();
        var machine = new Machine
        {
            Id = machineId,
            Name = "Milling CNC 01",
            CustomIdentifier = "CNC-01"
        };
        _context.Machines.Add(machine);
        await _context.SaveChangesAsync();

        // Ticket 1: Process Engineering line stop
        await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "Tool fracture",
            MachineId = machineId,
            IsLineStop = true,
            LineStopDurationMinutes = 45.0,
            ResponsibleDepartment = "ProcessEngineering",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        // Ticket 2: IT network outage line stop
        await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "MES switch failure",
            MachineId = machineId,
            IsLineStop = true,
            LineStopDurationMinutes = 30.0,
            ResponsibleDepartment = "IT",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var statsResult = await _controller.GetStoppageStats();
        var okResult = Assert.IsType<OkObjectResult>(statsResult.Result);
        var stats = Assert.IsType<StoppageStatsResponse>(okResult.Value);

        Assert.Equal(75.0, stats.TotalStoppageMinutesThisWeek);
        Assert.Equal(2, stats.TotalLineStopIncidents);
        Assert.Equal(2, stats.DepartmentBreakdown.Count);

        var procEng = stats.DepartmentBreakdown.FirstOrDefault(d => d.Department == "ProcessEngineering");
        Assert.NotNull(procEng);
        Assert.Equal(45.0, procEng.StoppageMinutes);

        var it = stats.DepartmentBreakdown.FirstOrDefault(d => d.Department == "IT");
        Assert.NotNull(it);
        Assert.Equal(30.0, it.StoppageMinutes);
    }

    [Fact]
    public async Task SetPending_SupportsExtendedReasons_SignOffAndCustom()
    {
        var ticket = await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "Safety guard light curtain replacement",
            Status = "InProgress"
        });

        var result = await _controller.SetPending(ticket.Id, new SetPendingRequest(
            PendingReason.SignOff,
            "Awaiting EHS officer safety sign-off before restart"
        ));
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<MaintenanceTicket>(okResult.Value);

        Assert.Equal("Pending", updated.Status);
        Assert.Equal(PendingReason.SignOff, updated.PendingReason);
        Assert.Equal("Awaiting EHS officer safety sign-off before restart", updated.PendingDetails);
    }

    [Fact]
    public async Task GetTicketsByDepartment_FiltersCorrectly()
    {
        await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "Vision inspection lighting fault",
            ResponsibleDepartment = "Vision"
        });
        await _repository.CreateAsync(new MaintenanceTicket
        {
            Title = "MES barcoding scanner timeout",
            ResponsibleDepartment = "MES"
        });

        var result = await _controller.GetTicketsByDepartment("Vision");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var tickets = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<MaintenanceTicket>>(okResult.Value);

        Assert.Single(tickets);
        Assert.Equal("Vision inspection lighting fault", tickets.First().Title);
    }
}
