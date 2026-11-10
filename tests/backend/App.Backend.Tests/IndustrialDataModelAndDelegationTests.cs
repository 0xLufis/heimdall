namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using App.Contracts.Enums;
using App.Contracts.Machine;
using App.Contracts.Personnel;
using App.Contracts.Telemetry;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;

public class IndustrialDataModelAndDelegationTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"IndustrialTestDb_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ComponentRecipe_AutoTagsAndInstantiation_InheritsTagsCorrectly()
    {
        using var context = CreateContext();
        var bridge = new ComponentInventoryBridge(context);

        // 1. Create a machine
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Laser Welding Cell #1",
            CustomIdentifier = "LWC-01",
            Technologies = new List<string> { "Laser", "Welding", "Robotics" }
        };
        context.Machines.Add(machine);

        // 2. Create Component Recipe with AutoTags
        var recipe = new ComponentRecipe
        {
            Id = Guid.NewGuid(),
            RecipeCode = "REC-SMC-VALVE-500",
            Name = "SMC Pneumatic Directional Valve 5/2",
            Category = ComponentCategory.Hardware,
            AutoTags = new List<string> { "Pneumatic", "Valve", "SMC", "SafetyCircuit" }
        };
        context.ComponentRecipes.Add(recipe);

        // 3. Create Inventory Item (warehouse part)
        var part = new HardwareComponent
        {
            Id = Guid.NewGuid(),
            Name = "SMC SY5120-5LZD-01",
            SerialNumber = "SN-SMC-99412",
            IsStockItem = true,
            StockQuantity = 5,
            EquipmentStatus = "InStock"
        };
        context.InventoryItems.Add(part);
        await context.SaveChangesAsync();

        // 4. Assign part to create new slot
        var assignRequest = new AssignPartToComponentRequestDto
        {
            MachineId = machine.Id,
            NewComponentSlotIdentifier = "=LINE1+OP10-1V1",
            NewComponentName = "Main Pneumatic Clamp Valve",
            Category = ComponentCategory.Hardware,
            ComponentRecipeId = recipe.Id,
            PartId = part.Id,
            SwappedByUserId = "tech-john",
            SwappedByUserName = "John Doe",
            AdditionalTags = new List<string> { "CriticalSpares" }
        };

        var result = await bridge.AssignPartAsync(assignRequest);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsNewComponentCreated);

        var component = await bridge.GetComponentByIdAsync(result.ComponentId);
        Assert.NotNull(component);
        Assert.Equal("=LINE1+OP10-1V1", component.FunctionalSlotIdentifier);
        Assert.Equal(part.Id, component.CurrentPartId);

        // Verify AutoTags inheritance + additional tags
        Assert.Contains("Pneumatic", component.Tags);
        Assert.Contains("SafetyCircuit", component.Tags);
        Assert.Contains("CriticalSpares", component.Tags);

        // Verify warehouse stock decremented
        var updatedPart = await context.InventoryItems.FindAsync(part.Id);
        Assert.Equal(4, updatedPart?.StockQuantity);
    }

    [Fact]
    public async Task ComponentPartHistory_TracksPartSwaps_LineageLedgerMaintained()
    {
        using var context = CreateContext();
        var bridge = new ComponentInventoryBridge(context);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "CNC Milling Station 3",
            CustomIdentifier = "CNC-03"
        };
        context.Machines.Add(machine);

        var oldPart = new HardwareComponent
        {
            Id = Guid.NewGuid(),
            Name = "Siemens Spindle Drive Motor 15kW",
            SerialNumber = "SN-SIEMENS-100",
            EquipmentStatus = "Operational",
            MachineId = machine.Id
        };
        context.InventoryItems.Add(oldPart);

        var slot = new MachineComponent
        {
            Id = Guid.NewGuid(),
            MachineId = machine.Id,
            Name = "Spindle Drive Axis Z",
            FunctionalSlotIdentifier = "AXIS-Z-SPINDLE",
            Category = ComponentCategory.Hardware,
            CurrentPartId = oldPart.Id,
            Tags = new List<string> { "Drive", "AxisZ" }
        };
        context.MachineComponents.Add(slot);

        var newPart = new HardwareComponent
        {
            Id = Guid.NewGuid(),
            Name = "Siemens Spindle Drive Motor 15kW Rebuilt",
            SerialNumber = "SN-SIEMENS-200",
            IsStockItem = true,
            StockQuantity = 1,
            EquipmentStatus = "InStock"
        };
        context.InventoryItems.Add(newPart);
        await context.SaveChangesAsync();

        // Swap part on existing slot
        var swapRequest = new AssignPartToComponentRequestDto
        {
            MachineId = machine.Id,
            ExistingComponentId = slot.Id,
            PartId = newPart.Id,
            SwappedByUserId = "usr-maint-04",
            SwappedByUserName = "Bob Maintenance",
            Reason = PartSwapReason.WearDepreciation,
            PreviousPartDisposition = PartDisposition.SentForRepair,
            Notes = "Bearing noise detected during vibration diagnostic."
        };

        var swapResult = await bridge.AssignPartAsync(swapRequest);

        // Assert
        Assert.True(swapResult.Success);
        Assert.False(swapResult.IsNewComponentCreated);

        var historyList = await bridge.GetComponentPartHistoryAsync(slot.Id);
        Assert.Single(historyList);

        var history = historyList.First();
        Assert.Equal(oldPart.Id, history.PreviousPartId);
        Assert.Equal("SN-SIEMENS-100", history.PreviousPartSerialNumber);
        Assert.Equal(newPart.Id, history.NewPartId);
        Assert.Equal("SN-SIEMENS-200", history.NewPartSerialNumber);
        Assert.Equal(PartSwapReason.WearDepreciation, history.Reason);
        Assert.Equal(PartDisposition.SentForRepair, history.PreviousPartDisposition);

        // Verify slot currently references new part
        var updatedSlot = await context.MachineComponents.FindAsync(slot.Id);
        Assert.Equal(newPart.Id, updatedSlot?.CurrentPartId);

        // Verify service log was recorded
        var log = await context.MachineServiceLogs.FirstOrDefaultAsync(l => l.MachineId == machine.Id);
        Assert.NotNull(log);
        Assert.Equal("PartReplacement", log.EntryType);
    }

    [Fact]
    public async Task MachineDelegation_MultipleBindingsPerMachine_ResolvedCorrectly()
    {
        using var context = CreateContext();
        var delegationService = new DelegationRecipeService(context);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "SMT Placement Line #4",
            CustomIdentifier = "SMT-04"
        };
        context.Machines.Add(machine);

        var mechanicalRecipe = new DelegationRecipe
        {
            Id = Guid.NewGuid(),
            Name = "Mechanical Preventive Maintenance Crew",
            Discipline = DelegationDiscipline.Mechanical,
            PrimaryUserId = "tech-mech-lead",
            PrimaryUserName = "Mike Miller"
        };

        var controlsRecipe = new DelegationRecipe
        {
            Id = Guid.NewGuid(),
            Name = "Controls and PLC Diagnostics Team",
            Discipline = DelegationDiscipline.Controls,
            PrimaryUserId = "eng-plc-01",
            PrimaryUserName = "Sarah Controls"
        };

        context.DelegationRecipes.AddRange(mechanicalRecipe, controlsRecipe);
        await context.SaveChangesAsync();

        // Bind both recipes to the single machine
        await delegationService.BindRecipeToMachineAsync(machine.Id, mechanicalRecipe.Id, DelegationDiscipline.Mechanical);
        await delegationService.BindRecipeToMachineAsync(machine.Id, controlsRecipe.Id, DelegationDiscipline.Controls);

        // Act 1: Resolve mechanical
        var resolvedMech = await delegationService.ResolvePersonnelForMachineAsync(
            machine.Id, DelegationDiscipline.Mechanical, WorkerScheduleType.Shift);

        // Act 2: Resolve controls
        var resolvedControls = await delegationService.ResolvePersonnelForMachineAsync(
            machine.Id, DelegationDiscipline.Controls, WorkerScheduleType.Shift);

        // Assert
        Assert.Equal("tech-mech-lead", resolvedMech.EffectiveAssigneeId);
        Assert.Equal("Mike Miller", resolvedMech.EffectiveAssigneeName);
        Assert.Equal(DelegationDiscipline.Mechanical, resolvedMech.Discipline);

        Assert.Equal("eng-plc-01", resolvedControls.EffectiveAssigneeId);
        Assert.Equal("Sarah Controls", resolvedControls.EffectiveAssigneeName);
        Assert.Equal(DelegationDiscipline.Controls, resolvedControls.Discipline);
    }

    [Fact]
    public async Task DelegationRecipe_SkillsFiltering_PicksQualifiedWorker()
    {
        using var context = CreateContext();
        var delegationService = new DelegationRecipeService(context);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Trumpf TruLaser Cell",
            CustomIdentifier = "TRU-01"
        };
        context.Machines.Add(machine);

        // Skill definition for laser class 4 safety
        var laserSkill = new SkillDefinition
        {
            Id = Guid.NewGuid(),
            Code = "CERT-LASER-4",
            Name = "Laser Class 4 Safety Officer Certification",
            Category = SkillCategory.SafetyClearance
        };
        context.SkillDefinitions.Add(laserSkill);

        // Personnel group with 2 technicians: Alice and Bob
        var group = new PersonnelGroup
        {
            Id = Guid.NewGuid(),
            Name = "Laser Maintenance Team",
            Discipline = DelegationDiscipline.Safety
        };
        group.Members.Add(new PersonnelGroupMember { Id = Guid.NewGuid(), PersonnelGroupId = group.Id, UserId = "user-bob", UserName = "Bob Regular" });
        group.Members.Add(new PersonnelGroupMember { Id = Guid.NewGuid(), PersonnelGroupId = group.Id, UserId = "user-alice", UserName = "Alice Certified" });
        context.PersonnelGroups.Add(group);

        // Alice is certified, Bob is not
        var aliceCert = new PersonnelSkill
        {
            Id = Guid.NewGuid(),
            UserId = "user-alice",
            SkillId = laserSkill.Id,
            CertifiedAt = DateTimeOffset.UtcNow.AddMonths(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddMonths(10),
            CertificateNumber = "TUV-LAS-2026-99"
        };
        context.PersonnelSkills.Add(aliceCert);

        // Delegation recipe requires CERT-LASER-4
        var recipe = new DelegationRecipe
        {
            Id = Guid.NewGuid(),
            Name = "Laser Safety Intervention Recipe",
            Discipline = DelegationDiscipline.Safety,
            PrimaryPersonnelGroupId = group.Id,
            RequiredSkillCodesJson = JsonSerializer.Serialize(new List<string> { "CERT-LASER-4" })
        };
        context.DelegationRecipes.Add(recipe);
        await context.SaveChangesAsync();

        await delegationService.BindRecipeToMachineAsync(machine.Id, recipe.Id, DelegationDiscipline.Safety);

        // Act
        var resolved = await delegationService.ResolvePersonnelForMachineAsync(
            machine.Id, DelegationDiscipline.Safety, WorkerScheduleType.Shift);

        // Assert: Alice should be picked because only she has CERT-LASER-4
        Assert.Equal("user-alice", resolved.EffectiveAssigneeId);
        Assert.Equal("Alice Certified", resolved.EffectiveAssigneeName);
        Assert.Contains("CERT-LASER-4", resolved.ResolutionReason);
    }

    [Fact]
    public async Task DelegationRecipe_OffHoursOnCallFallback_RoutesToOnCallWorker()
    {
        using var context = CreateContext();
        var delegationService = new DelegationRecipeService(context);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "High Speed Bottling Line",
            CustomIdentifier = "BOT-01"
        };
        context.Machines.Add(machine);

        var recipe = new DelegationRecipe
        {
            Id = Guid.NewGuid(),
            Name = "Emergency Mechanical Response",
            Discipline = DelegationDiscipline.Mechanical,
            PrimaryUserId = "regular-shift-worker",
            PrimaryUserName = "Tom Shift",
            OnCallFallbackUserId = "oncall-standby-99",
            OnCallFallbackUserName = "Dr. Standby Engineer"
        };
        context.DelegationRecipes.Add(recipe);
        await context.SaveChangesAsync();

        await delegationService.BindRecipeToMachineAsync(machine.Id, recipe.Id, DelegationDiscipline.Mechanical);

        // Act: Evaluate with OnCall schedule type (night shift / weekend)
        var resolved = await delegationService.ResolvePersonnelForMachineAsync(
            machine.Id, DelegationDiscipline.Mechanical, WorkerScheduleType.OnCall);

        // Assert
        Assert.True(resolved.IsOnCallFallback);
        Assert.Equal("oncall-standby-99", resolved.EffectiveAssigneeId);
        Assert.Equal("Dr. Standby Engineer", resolved.EffectiveAssigneeName);
    }

    [Fact]
    public async Task MachineRaisedTicket_IngestsProbesAndRoutesDelegation_CreatesIncident()
    {
        using var context = CreateContext();
        var delegationService = new DelegationRecipeService(context);
        var machineTicketService = new MachineRaisedTicketService(context, delegationService);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Injection Molding Press #7",
            CustomIdentifier = "IMP-07",
            OrganizationId = "org-automotive"
        };
        context.Machines.Add(machine);

        // Install component on machine with BMK tag
        var component = new MachineComponent
        {
            Id = Guid.NewGuid(),
            MachineId = machine.Id,
            Name = "Hydraulic Pressure Proportional Valve",
            FunctionalSlotIdentifier = "=LINE1+HYD-Y1",
            Category = ComponentCategory.Hardware,
            EplanCadRefJson = JsonSerializer.Serialize(new EplanCadReference { DeviceTag = "-Y1", DrawingPage = "Page 22.4" })
        };
        context.MachineComponents.Add(component);

        // Bound delegation recipe for controls
        var recipe = new DelegationRecipe
        {
            Id = Guid.NewGuid(),
            Name = "Hydraulics & Controls Response",
            Discipline = DelegationDiscipline.Controls,
            PrimaryUserId = "controls-specialist-01",
            PrimaryUserName = "Alex Controls"
        };
        context.DelegationRecipes.Add(recipe);
        await context.SaveChangesAsync();

        await delegationService.BindRecipeToMachineAsync(machine.Id, recipe.Id, DelegationDiscipline.Controls);

        // Machine/PLC dispatches alarm
        var request = new MachineRaisedTicketRequestDto
        {
            MachineId = machine.Id,
            TargetComponentId = component.Id,
            AlarmCode = "ALM-PLC-COMM-FAULT-902",
            Title = "Loss of Profinet IO on Hydraulic Sub-Rack",
            Description = "WDT tripped on communication cycle. Hydraulic pressure collapsed to 0 bar.",
            Priority = "Critical",
            IsLineStop = true,
            ControllerSource = "192.168.10.45",
            Probes = new List<TicketTelemetryProbe>
            {
                new TicketTelemetryProbe
                {
                    ProbeId = "PROBE-HYD-PRESSURE",
                    DatapointKey = "HYD.MainHeader.PressureBar",
                    MetricName = "Main Hydraulic Header Pressure",
                    NumericValue = 1.2,
                    Unit = "bar",
                    NominalLow = 140.0,
                    NominalHigh = 210.0,
                    TriggerStatus = "ThresholdExceeded",
                    SampledAt = DateTimeOffset.UtcNow
                }
            }
        };

        // Act
        var result = await machineTicketService.CreateMachineRaisedTicketAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.TicketId);
        Assert.Equal("Open", result.Status);

        var createdTicket = await context.MaintenanceTickets
            .Include(t => t.TargetComponent)
            .FirstOrDefaultAsync(t => t.Id == result.TicketId);

        Assert.NotNull(createdTicket);
        Assert.Equal("MachineAutomatic", createdTicket.OriginatorType);
        Assert.Equal("ALM-PLC-COMM-FAULT-902", createdTicket.AlarmCode);
        Assert.True(createdTicket.IsLineStop);
        Assert.Equal(component.Id, createdTicket.TargetComponentId);

        // Verify probes stored in JSON
        Assert.NotNull(createdTicket.TelemetryProbesJson);
        Assert.Contains("PROBE-HYD-PRESSURE", createdTicket.TelemetryProbesJson);
        Assert.Contains("1.2", createdTicket.TelemetryProbesJson);

        // Verify delegation assignment
        Assert.Equal("Alex Controls", createdTicket.AssignedTo);
        Assert.Equal("controls-specialist-01", createdTicket.ReservedBy);

        // Verify machine service log journal entry was created
        var journalEntry = await context.MachineServiceLogs
            .FirstOrDefaultAsync(l => l.MachineId == machine.Id && l.LinkedTicketId == createdTicket.Id);
        Assert.NotNull(journalEntry);
        Assert.Equal("EmergencyRepair", journalEntry.EntryType);
    }

    [Fact]
    public async Task MachineGrouping_LineAndTechnologyMemberships_QueriesCorrectly()
    {
        using var context = CreateContext();

        // 1. Line Group
        var lineGroup = new MachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Battery Assembly Line A",
            GroupType = MachineGroupType.Line,
            InterfaceContract = "CustomerLineInterface"
        };
        context.MachineGroups.Add(lineGroup);

        // 2. Technology Group
        var techGroup = new MachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "All Ultrasonic Welding Stations",
            GroupType = MachineGroupType.Technological,
            TechnologyKey = "UltrasonicWelding"
        };
        context.MachineGroups.Add(techGroup);

        // 3. Machines
        var m1 = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Station OP10 Ultrasonic Weld",
            LineGroupId = lineGroup.Id,
            Technologies = new List<string> { "UltrasonicWelding", "Vision" }
        };
        var m2 = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Station OP20 Fastening",
            LineGroupId = lineGroup.Id,
            Technologies = new List<string> { "Fastening" }
        };

        context.Machines.AddRange(m1, m2);
        await context.SaveChangesAsync();

        // Verify line group machines query
        var lineMachines = await context.Machines
            .Where(m => m.LineGroupId == lineGroup.Id)
            .ToListAsync();
        Assert.Equal(2, lineMachines.Count);

        // Verify dynamic technological group query via machine technologies
        var techMachines = await context.Machines
            .Where(m => m.Technologies.Contains(techGroup.TechnologyKey!))
            .ToListAsync();
        Assert.Single(techMachines);
        Assert.Equal("Station OP10 Ultrasonic Weld", techMachines.First().Name);
    }
}
