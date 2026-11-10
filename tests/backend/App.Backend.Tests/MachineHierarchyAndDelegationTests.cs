namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;
using App.Contracts.Enums;
using App.Contracts.Personnel;
using App.Infrastructure.Repositories;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;

public class MachineHierarchyAndDelegationTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"HierarchyDelegationTestDb_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Machine_SpocAndOwner_StoredAndRetrievedCorrectly()
    {
        using var context = CreateContext();
        var repository = new StationRepository(context);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Automated Laser Cell 1",
            CustomIdentifier = "ALC-01",
            Spoc = "John Doe (Lead Systems Engineer)",
            SpocUserId = "usr-johndoe",
            Owner = "Battery Production Department",
            OwnerUserId = "dept-battery-prod"
        };

        var created = await repository.CreateAsync(machine);
        Assert.NotNull(created);

        var retrieved = await repository.GetByIdAsync(machine.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("John Doe (Lead Systems Engineer)", retrieved.Spoc);
        Assert.Equal("usr-johndoe", retrieved.SpocUserId);
        Assert.Equal("Battery Production Department", retrieved.Owner);
        Assert.Equal("dept-battery-prod", retrieved.OwnerUserId);

        // Test Update
        var updated = await repository.UpdateAsync(
            machine.Id,
            name: "Automated Laser Cell 1 (Renamed)",
            customIdentifier: "ALC-01-REV",
            pinnedObjectHandle: null,
            organizationId: "org-1",
            controllerIds: null,
            spoc: "Jane Smith",
            spocUserId: "usr-janesmith",
            owner: "Powertrain Operations",
            ownerUserId: "dept-powertrain");

        Assert.NotNull(updated);
        Assert.Equal("Jane Smith", updated.Spoc);
        Assert.Equal("usr-janesmith", updated.SpocUserId);
        Assert.Equal("Powertrain Operations", updated.Owner);
        Assert.Equal("dept-powertrain", updated.OwnerUserId);
    }

    [Fact]
    public async Task MachineHierarchy_BusinessUnit_Project_Line_SubLine_Machine_Traversal()
    {
        using var context = CreateContext();

        // 1. Business Unit
        var bu = new BusinessUnitMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Automotive E-Mobility BU",
            BusinessUnitCode = "BU-EMOB",
            HeadOfBusinessUnit = "Dr. Michael Weber"
        };
        context.MachineGroups.Add(bu);

        // 2. Project (extends BusinessUnitMachineGroup)
        var project = new ProjectMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Audi PPE Battery System Program",
            BusinessUnitId = bu.Id,
            ProjectCode = "PRJ-PPE-2026",
            CustomerName = "Audi AG"
        };
        context.MachineGroups.Add(project);

        // 3. Line (extends ProjectMachineGroup)
        var line = new LineMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Line 06 - Module Assembly",
            ProjectId = project.Id,
            LineCode = "LINE-06",
            TargetTaktTimeSeconds = 45.0
        };
        context.MachineGroups.Add(line);

        // 4. Sub-Line (extends LineMachineGroup)
        var subLine = new SubLineMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Sub-Line A - Cell Infeed & Stacking",
            LineId = line.Id,
            SubLineCode = "SUBLINE-06A",
            StageSequence = 1
        };
        context.MachineGroups.Add(subLine);

        // 5. Machine
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "OP10 Ultrasonic Cell Stacking Station",
            CustomIdentifier = "L06-OP10",
            MachineType = "UltrasonicWelding",
            BusinessUnitGroupId = bu.Id,
            ProjectGroupId = project.Id,
            LineGroupId = line.Id,
            SubLineGroupId = subLine.Id
        };
        context.Machines.Add(machine);
        await context.SaveChangesAsync();

        // Assert hierarchy references on machine
        var loaded = await context.Machines
            .Include(m => m.SubLineGroup)
            .Include(m => m.LineGroup)
            .Include(m => m.ProjectGroup)
            .Include(m => m.BusinessUnitGroup)
            .FirstOrDefaultAsync(m => m.Id == machine.Id);

        Assert.NotNull(loaded);
        Assert.Equal(subLine.Id, loaded.SubLineGroupId);
        Assert.Equal("Sub-Line A - Cell Infeed & Stacking", loaded.SubLineGroup?.Name);
        Assert.Equal(line.Id, loaded.LineGroupId);
        Assert.Equal("Line 06 - Module Assembly", loaded.LineGroup?.Name);
        Assert.Equal(project.Id, loaded.ProjectGroupId);
        Assert.Equal("Audi PPE Battery System Program", loaded.ProjectGroup?.Name);
        Assert.Equal(bu.Id, loaded.BusinessUnitGroupId);
        Assert.Equal("Automotive E-Mobility BU", loaded.BusinessUnitGroup?.Name);

        // Verify MatchesMachine logic across all hierarchy levels
        Assert.True(subLine.MatchesMachine(loaded));
        Assert.True(line.MatchesMachine(loaded));
        Assert.True(project.MatchesMachine(loaded));
        Assert.True(bu.MatchesMachine(loaded));
    }

    [Fact]
    public void MachineGroup_IMachineGroup_ImplementationAndInheritance_HierarchyExtendsPrevious()
    {
        // Level 1: BusinessUnitMachineGroup implements IMachineGroup
        var bu = new BusinessUnitMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "BU Electronics"
        };
        Assert.IsAssignableFrom<IMachineGroup>(bu);
        Assert.Equal(MachineGroupType.BusinessUnit, bu.GroupType);

        // Level 2: ProjectMachineGroup extends BusinessUnitMachineGroup
        var project = new ProjectMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Project Alpha",
            BusinessUnitId = bu.Id
        };
        Assert.IsAssignableFrom<BusinessUnitMachineGroup>(project);
        Assert.IsAssignableFrom<IMachineGroup>(project);
        Assert.Equal(MachineGroupType.Project, project.GroupType);
        Assert.Equal(bu.Id, project.ParentId);

        // Level 3: LineMachineGroup extends ProjectMachineGroup
        var line = new LineMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Line 01",
            ProjectId = project.Id
        };
        Assert.IsAssignableFrom<ProjectMachineGroup>(line);
        Assert.IsAssignableFrom<IMachineGroup>(line);
        Assert.Equal(MachineGroupType.Line, line.GroupType);
        Assert.Equal(project.Id, line.ParentId);

        // Level 4: SubLineMachineGroup extends LineMachineGroup
        var subLine = new SubLineMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Sub-Line 1A",
            LineId = line.Id
        };
        Assert.IsAssignableFrom<LineMachineGroup>(subLine);
        Assert.IsAssignableFrom<IMachineGroup>(subLine);
        Assert.Equal(MachineGroupType.SubLine, subLine.GroupType);
        Assert.Equal(line.Id, subLine.ParentId);

        // Level 5: MachineUnitGroup extends SubLineMachineGroup
        var machineNode = new MachineUnitGroup
        {
            Id = Guid.NewGuid(),
            Name = "Workstation 10",
            SubLineId = subLine.Id
        };
        Assert.IsAssignableFrom<SubLineMachineGroup>(machineNode);
        Assert.IsAssignableFrom<IMachineGroup>(machineNode);
        Assert.Equal(MachineGroupType.Machine, machineNode.GroupType);
        Assert.Equal(subLine.Id, machineNode.ParentId);

        // Machine itself implements IMachineGroup
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Robot Station",
            CustomIdentifier = "ROB-01"
        };
        Assert.IsAssignableFrom<IMachineGroup>(machine);
        var asMachineGroup = (IMachineGroup)machine;
        Assert.Equal(MachineGroupType.Machine, asMachineGroup.GroupType);
        Assert.Contains(machine.Id, asMachineGroup.GetMachineIds());
        Assert.True(asMachineGroup.MatchesMachine(machine));
    }

    [Fact]
    public async Task Delegation_DecoupledFromGeneralGroupings_ReferencesMultipleMaintenanceAndMachineGroups()
    {
        using var context = CreateContext();
        var delegationService = new DelegationRecipeService(context);

        // Maintenance Groups (1 or more)
        var mechTeam = new PersonnelGroup
        {
            Id = Guid.NewGuid(),
            Name = "Mechanical Rapid Response",
            Discipline = DelegationDiscipline.Mechanical
        };
        var roboticsTeam = new PersonnelGroup
        {
            Id = Guid.NewGuid(),
            Name = "Robotics & Automation Crew",
            Discipline = DelegationDiscipline.Controls
        };
        context.PersonnelGroups.AddRange(mechTeam, roboticsTeam);

        // Machine Groups (1 or more, e.g. Line 01 and Line 02)
        var line1 = new LineMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Battery Line 01"
        };
        var line2 = new LineMachineGroup
        {
            Id = Guid.NewGuid(),
            Name = "Battery Line 02"
        };
        context.MachineGroups.AddRange(line1, line2);

        // Machines on each line
        var m1 = new Machine { Id = Guid.NewGuid(), Name = "L1 Machine", CustomIdentifier = "L1-01", LineGroupId = line1.Id };
        var m2 = new Machine { Id = Guid.NewGuid(), Name = "L2 Machine", CustomIdentifier = "L2-01", LineGroupId = line2.Id };
        context.Machines.AddRange(m1, m2);
        await context.SaveChangesAsync();

        // Create delegation recipe referencing multiple maintenance groups and multiple machine groups
        var recipeDto = new DelegationRecipeDto
        {
            Name = "Cross-Line Robotics & Mechanical Delegation",
            Discipline = DelegationDiscipline.Controls,
            Technology = "Robotics",
            IsTechnologyPrimary = true,
            MaintenanceGroupIds = new List<Guid> { mechTeam.Id, roboticsTeam.Id },
            MachineGroupIds = new List<Guid> { line1.Id, line2.Id }
        };

        var saved = await delegationService.CreateOrUpdateRecipeAsync(recipeDto);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(2, saved.MaintenanceGroupIds.Count);
        Assert.Equal(2, saved.MachineGroupIds.Count);

        // Verify applicable recipes query finds this recipe for both line 1 and line 2 machines
        var applicableForM1 = await delegationService.GetRecipesApplicableToMachineAsync(m1.Id);
        var applicableForM2 = await delegationService.GetRecipesApplicableToMachineAsync(m2.Id);

        Assert.Contains(applicableForM1, r => r.Id == saved.Id);
        Assert.Contains(applicableForM2, r => r.Id == saved.Id);
    }

    [Fact]
    public async Task Delegation_PrimaryTechnologyDedication_PrioritizesTechnologyOverCompleteMachineDedication()
    {
        using var context = CreateContext();
        var delegationService = new DelegationRecipeService(context);

        // Machine has a dedicated complete-machine technician
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Multi-Function High Precision Cell",
            CustomIdentifier = "MF-CELL-01",
            MachineType = "Robotics",
            Technologies = new List<string> { "Robotics", "Dispensing" },
            PreferredTechnicianId = "tech-general-machine-owner",
            PreferredTechnicianName = "George Generalist"
        };
        context.Machines.Add(machine);

        // But there is a dedicated technology delegation rule for Robotics
        var roboticsRule = new TechnicianRule
        {
            Id = Guid.NewGuid(),
            Name = "Certified Robotics Specialist Routing",
            ScopeType = "technology",
            TargetId = "Robotics",
            TechnicianId = "tech-robotics-specialist",
            TechnicianName = "Robert Robotics Expert"
        };
        context.TechnicianRules.Add(roboticsRule);
        await context.SaveChangesAsync();

        // Act: Resolve for the machine with technology context "Robotics"
        var resolved = await delegationService.ResolvePersonnelForMachineAsync(
            machine.Id,
            discipline: DelegationDiscipline.Controls,
            scheduleType: WorkerScheduleType.Shift,
            technology: "Robotics");

        // Assert: Technology dedication takes primary precedence over general machine dedication!
        Assert.Equal("technology_dedication", resolved.DedicationSource);
        Assert.Equal("tech-robotics-specialist", resolved.EffectiveAssigneeId);
        Assert.Equal("Robert Robotics Expert", resolved.EffectiveAssigneeName);
        Assert.Contains("Robotics", resolved.ResolutionReason);
    }

    [Fact]
    public async Task Delegation_CompleteMachineDedication_FallbackWhenNoTechnologyDedication()
    {
        using var context = CreateContext();
        var delegationService = new DelegationRecipeService(context);

        // Machine has a dedicated complete-machine technician
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Standard Pressing Station",
            CustomIdentifier = "PRS-01",
            MachineType = "Pressing",
            PreferredTechnicianId = "tech-dedicated-press-technician",
            PreferredTechnicianName = "Patricia Press"
        };
        context.Machines.Add(machine);
        await context.SaveChangesAsync();

        // Act: Resolve for a technology that has no technology-specific rule
        var resolved = await delegationService.ResolvePersonnelForMachineAsync(
            machine.Id,
            discipline: DelegationDiscipline.General,
            scheduleType: WorkerScheduleType.Shift,
            technology: "Pressing");

        // Assert: Falls back to complete machine dedication!
        Assert.Equal("complete_machine_dedication", resolved.DedicationSource);
        Assert.Equal("tech-dedicated-press-technician", resolved.EffectiveAssigneeId);
        Assert.Equal("Patricia Press", resolved.EffectiveAssigneeName);
        Assert.Contains("complete machine dedicated", resolved.ResolutionReason);
    }

    [Fact]
    public async Task SubMachine_RoundtableAndMultiMachineCell_HierarchicalManagement()
    {
        using var context = CreateContext();
        var repository = new StationRepository(context);
        var delegationService = new DelegationRecipeService(context);

        // 1. Create Roundtable Parent Machine
        var roundtable = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "6-Station Rotary Index Dial Table",
            CustomIdentifier = "RT-01",
            MachineType = "Roundtable",
            CellType = "Roundtable",
            PreferredTechnicianId = "tech-roundtable-lead",
            PreferredTechnicianName = "Ronald Roundtable Lead",
            Spoc = "Sarah Dial (Cell SPOC)",
            Owner = "Powertrain Assembly"
        };
        await repository.CreateAsync(roundtable);

        // 2. Add Sub-Machines (e.g. stations on the rotary table)
        var station1 = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "RT-01 Station 1 - Raw Infeed Pick & Place",
            CustomIdentifier = "RT-01-ST01",
            SubMachineIndex = 1,
            MachineType = "Infeed",
            Spoc = "Sarah Dial"
        };
        var station2 = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "RT-01 Station 2 - Induction Pre-Heat",
            CustomIdentifier = "RT-01-ST02",
            SubMachineIndex = 2,
            MachineType = "Thermal",
            Spoc = "Sarah Dial"
        };
        var station3 = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "RT-01 Station 3 - High Power Laser Welding",
            CustomIdentifier = "RT-01-ST03",
            SubMachineIndex = 3,
            MachineType = "Laser",
            Spoc = "Sarah Dial"
        };

        // The repository persists a sanitized copy (server-assigned id), so use the returned entities.
        station1 = (await repository.AddSubMachineAsync(roundtable.Id, station1))!;
        station2 = (await repository.AddSubMachineAsync(roundtable.Id, station2))!;
        station3 = (await repository.AddSubMachineAsync(roundtable.Id, station3))!;

        // Assert sub-machines query returns 3 stations ordered by SubMachineIndex
        var subMachines = await repository.GetSubMachinesAsync(roundtable.Id);
        Assert.Equal(3, subMachines.Count);
        Assert.Equal("RT-01-ST01", subMachines[0].CustomIdentifier);
        Assert.Equal("RT-01-ST02", subMachines[1].CustomIdentifier);
        Assert.Equal("RT-01-ST03", subMachines[2].CustomIdentifier);
        Assert.True(subMachines[0].IsSubMachine);

        // 3. Delegation resolution: Station 1 has no preferred technician, so it inherits parent roundtable's complete dedication!
        var resolvedSt1 = await delegationService.ResolvePersonnelForMachineAsync(
            station1.Id,
            discipline: DelegationDiscipline.General,
            scheduleType: WorkerScheduleType.Shift);

        Assert.Equal("complete_machine_dedication", resolvedSt1.DedicationSource);
        Assert.Equal("tech-roundtable-lead", resolvedSt1.EffectiveAssigneeId);
        Assert.Equal("Ronald Roundtable Lead", resolvedSt1.EffectiveAssigneeName);
        Assert.Contains("parent cell/roundtable", resolvedSt1.ResolutionReason);

        // 4. Test removing a sub-machine
        var removed = await repository.RemoveSubMachineAsync(roundtable.Id, station3.Id);
        Assert.True(removed);

        var remaining = await repository.GetSubMachinesAsync(roundtable.Id);
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, m => m.Id == station3.Id);
    }

    [Fact]
    public async Task TechnologicalGroup_AutomaticallyDedicated_AndSupersededByDirectMember()
    {
        using var context = CreateContext();
        var repository = new StationRepository(context);
        var delegationService = new DelegationRecipeService(context);

        // 1. Create a Technological Group: Controls Specialists
        var controlsGroup = new PersonnelGroup
        {
            Id = Guid.NewGuid(),
            Name = "Automation Controls Crew",
            Discipline = DelegationDiscipline.Controls,
            ShiftPattern = "Shift 1"
        };
        controlsGroup.Members.Add(new PersonnelGroupMember
        {
            PersonnelGroupId = controlsGroup.Id,
            UserId = "usr-controls-specialist-1",
            UserName = "Alex Controls"
        });
        controlsGroup.Members.Add(new PersonnelGroupMember
        {
            PersonnelGroupId = controlsGroup.Id,
            UserId = "usr-controls-specialist-2",
            UserName = "Sam Controls"
        });
        context.PersonnelGroups.Add(controlsGroup);
        await context.SaveChangesAsync();

        // 2. Create Machine possessing "Controls" technology
        var controlsMachine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Beckhoff IPC Assembly Station",
            CustomIdentifier = "IPC-CTRL-01",
            MachineType = "Controls & PLC Automation",
            Spoc = "Mark Controls SPOC",
            Owner = "Stamping Dept"
        };
        await repository.CreateAsync(controlsMachine);

        // 3. Query Machine Dedications: Technological group should be AUTOMATICALLY dedicated, NOT superseded
        var initialDedications = await delegationService.GetMachineDedicationsAsync(controlsMachine.Id);
        Assert.NotNull(initialDedications);
        Assert.Null(initialDedications.DedicatedPerson);
        Assert.Null(initialDedications.DedicatedGroup);
        Assert.Single(initialDedications.TechnologicalGroupDedications);

        var techGroupDed = initialDedications.TechnologicalGroupDedications[0];
        Assert.Equal("Automation Controls Crew", techGroupDed.GroupName);
        Assert.Contains("Controls", techGroupDed.Technology);
        Assert.True(techGroupDed.IsAutomaticallyDedicated);
        Assert.False(techGroupDed.IsSuperseded);
        Assert.Null(techGroupDed.SupersededByUserId);

        // Resolution should yield technological_group_auto
        var resAuto = await delegationService.ResolvePersonnelForMachineAsync(controlsMachine.Id, discipline: DelegationDiscipline.Controls);
        Assert.Equal("technological_group_auto", resAuto.DedicationSource);
        Assert.Equal("usr-controls-specialist-1", resAuto.EffectiveAssigneeId);
        Assert.Equal(controlsGroup.Id, resAuto.EffectiveGroupId);

        // 4. Directly dedicate a member of the Controls Group (usr-controls-specialist-1) to the machine!
        var assignReq = new DedicationAssignmentRequest
        {
            TargetType = "person",
            TargetId = "usr-controls-specialist-1",
            TargetName = "Alex Controls",
            TargetEmail = "alex.controls@heimdall.io",
            Technology = "Controls"
        };
        var updatedDedications = await delegationService.DedicateToMachineAsync(controlsMachine.Id, assignReq);
        Assert.NotNull(updatedDedications);
        Assert.NotNull(updatedDedications.DedicatedPerson);
        Assert.Equal("usr-controls-specialist-1", updatedDedications.DedicatedPerson.UserId);
        Assert.Equal("Alex Controls", updatedDedications.DedicatedPerson.UserName);

        // The technological group is now SUPERSEDED by the dedicated member!
        Assert.Single(updatedDedications.TechnologicalGroupDedications);
        var supersededGroup = updatedDedications.TechnologicalGroupDedications[0];
        Assert.True(supersededGroup.IsSuperseded);
        Assert.Equal("usr-controls-specialist-1", supersededGroup.SupersededByUserId);
        Assert.Equal("Alex Controls", supersededGroup.SupersededByUserName);

        // Resolution should now yield technology_member_override
        var resOverride = await delegationService.ResolvePersonnelForMachineAsync(controlsMachine.Id, discipline: DelegationDiscipline.Controls);
        Assert.Equal("technology_member_override", resOverride.DedicationSource);
        Assert.Equal("usr-controls-specialist-1", resOverride.EffectiveAssigneeId);
        Assert.Contains("supersedes technological group", resOverride.ResolutionReason);

        // 5. Check User Dedications vice-versa view for Alex Controls
        var userDedications = await delegationService.GetUserDedicationsAsync("usr-controls-specialist-1");
        Assert.NotNull(userDedications);
        Assert.Equal("usr-controls-specialist-1", userDedications.UserId);
        Assert.Single(userDedications.DirectlyDedicatedMachines);
        Assert.Equal(controlsMachine.Id, userDedications.DirectlyDedicatedMachines[0].MachineId);
        Assert.True(userDedications.DirectlyDedicatedMachines[0].SupersedesTechnologicalGroup);
        Assert.Equal("Automation Controls Crew", userDedications.DirectlyDedicatedMachines[0].SupersededGroupName);

        // 6. Remove dedication and verify restoration of automatic tech group
        var removed = await delegationService.RemoveMachineDedicationAsync(controlsMachine.Id);
        Assert.True(removed);

        var restoredDedications = await delegationService.GetMachineDedicationsAsync(controlsMachine.Id);
        Assert.Null(restoredDedications.DedicatedPerson);
        Assert.Single(restoredDedications.TechnologicalGroupDedications);
        Assert.False(restoredDedications.TechnologicalGroupDedications[0].IsSuperseded);
    }

    [Fact]
    public async Task MultipleDedications_PerTechnology_WithAoiMachineVisionSupport()
    {
        using var context = CreateContext();
        var repository = new StationRepository(context);
        var delegationService = new DelegationRecipeService(context);

        // 1. Create an AOI machine (which requires Controls, Mechanical, and Vision support)
        var aoiMachine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Automated Optical Inspection Station OP30",
            CustomIdentifier = "AOI-OP30-01",
            MachineType = "AOI Inspection",
            Spoc = "Alice Vision SPOC",
            Owner = "Quality Assurance"
        };
        await repository.CreateAsync(aoiMachine);

        // 2. Query initial dedications: Verify baseline technologies Controls, Mechanical, and Vision are inferred
        var initialDedications = await delegationService.GetMachineDedicationsAsync(aoiMachine.Id);
        Assert.NotNull(initialDedications);
        Assert.Contains("Controls", initialDedications.Technologies);
        Assert.Contains("Mechanical", initialDedications.Technologies);
        Assert.Contains("Vision", initialDedications.Technologies);
        Assert.Empty(initialDedications.DirectDedications);

        // 3. Assign 3 separate specialist dedications per technology:
        // - Specialist A for Controls
        // - Specialist B for Mechanical
        // - Specialist C for Vision
        await delegationService.DedicateToMachineAsync(aoiMachine.Id, new DedicationAssignmentRequest
        {
            TargetType = "person",
            TargetId = "usr-controls-tech",
            TargetName = "Charlie Controls",
            TargetEmail = "charlie.controls@heimdall.io",
            Technology = "Controls"
        });

        await delegationService.DedicateToMachineAsync(aoiMachine.Id, new DedicationAssignmentRequest
        {
            TargetType = "person",
            TargetId = "usr-mechanical-tech",
            TargetName = "Max Mechanical",
            TargetEmail = "max.mechanical@heimdall.io",
            Technology = "Mechanical"
        });

        var dedicationsWithAllThree = await delegationService.DedicateToMachineAsync(aoiMachine.Id, new DedicationAssignmentRequest
        {
            TargetType = "person",
            TargetId = "usr-vision-tech",
            TargetName = "Vera Vision",
            TargetEmail = "vera.vision@heimdall.io",
            Technology = "Vision"
        });

        // 4. Verify all 3 direct dedications are registered on the machine
        Assert.Equal(3, dedicationsWithAllThree.DirectDedications.Count);
        Assert.Contains(dedicationsWithAllThree.DirectDedications, d => d.TargetId == "usr-controls-tech" && d.Technology == "Controls");
        Assert.Contains(dedicationsWithAllThree.DirectDedications, d => d.TargetId == "usr-mechanical-tech" && d.Technology == "Mechanical");
        Assert.Contains(dedicationsWithAllThree.DirectDedications, d => d.TargetId == "usr-vision-tech" && d.Technology == "Vision");

        // 5. Verify delegation resolution per technology:
        var resolvedControls = await delegationService.ResolvePersonnelForMachineAsync(aoiMachine.Id, technology: "Controls");
        Assert.Equal("usr-controls-tech", resolvedControls.EffectiveAssigneeId);
        Assert.Equal("Charlie Controls", resolvedControls.EffectiveAssigneeName);
        Assert.Equal("Controls", resolvedControls.Technology);

        var resolvedMechanical = await delegationService.ResolvePersonnelForMachineAsync(aoiMachine.Id, technology: "Mechanical");
        Assert.Equal("usr-mechanical-tech", resolvedMechanical.EffectiveAssigneeId);
        Assert.Equal("Max Mechanical", resolvedMechanical.EffectiveAssigneeName);
        Assert.Equal("Mechanical", resolvedMechanical.Technology);

        var resolvedVision = await delegationService.ResolvePersonnelForMachineAsync(aoiMachine.Id, technology: "Vision");
        Assert.Equal("usr-vision-tech", resolvedVision.EffectiveAssigneeId);
        Assert.Equal("Vera Vision", resolvedVision.EffectiveAssigneeName);
        Assert.Equal("Vision", resolvedVision.Technology);

        // 6. Check User Dedications vice-versa view for Vera Vision
        var userDedications = await delegationService.GetUserDedicationsAsync("usr-vision-tech");
        Assert.NotNull(userDedications);
        Assert.Single(userDedications.DirectlyDedicatedMachines);
        Assert.Equal(aoiMachine.Id, userDedications.DirectlyDedicatedMachines[0].MachineId);
        Assert.Equal("Vision", userDedications.DirectlyDedicatedMachines[0].Technology);

        // 7. Remove only the Controls dedication targeting targetId and technology
        var removedControls = await delegationService.RemoveMachineDedicationAsync(aoiMachine.Id, targetId: "usr-controls-tech", technology: "Controls");
        Assert.True(removedControls);

        // Verify that Mechanical and Vision dedications remain active
        var dedicationsAfterRemove = await delegationService.GetMachineDedicationsAsync(aoiMachine.Id);
        Assert.Equal(2, dedicationsAfterRemove.DirectDedications.Count);
        Assert.DoesNotContain(dedicationsAfterRemove.DirectDedications, d => d.Technology == "Controls");
        Assert.Contains(dedicationsAfterRemove.DirectDedications, d => d.Technology == "Mechanical");
        Assert.Contains(dedicationsAfterRemove.DirectDedications, d => d.Technology == "Vision");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Security & correctness regression tests
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DedicateToMachine_UnknownMachine_ThrowsAndDoesNotCreatePhantomMachine()
    {
        using var context = CreateContext();
        var service = new DelegationRecipeService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DedicateToMachineAsync("NOT-A-MACHINE", new DedicationAssignmentRequest
        {
            TargetType = "person",
            TargetId = "usr-1",
            TargetName = "User One"
        }));
        Assert.False(await service.DedicateUserToMachineAsync("usr-1", "NOT-A-MACHINE"));

        Assert.Equal(0, await context.Machines.CountAsync());
        Assert.Equal(0, await context.TechnicianRules.CountAsync());
    }

    [Fact]
    public async Task BindRecipe_UnknownMachineOrRecipe_ThrowsKeyNotFound()
    {
        using var context = CreateContext();
        var machine = new Machine { Id = Guid.NewGuid(), Name = "M1", CustomIdentifier = "M1" };
        var recipe = new DelegationRecipe { Id = Guid.NewGuid(), Name = "R1" };
        context.Machines.Add(machine);
        context.DelegationRecipes.Add(recipe);
        await context.SaveChangesAsync();
        var service = new DelegationRecipeService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.BindRecipeToMachineAsync(Guid.NewGuid(), recipe.Id, DelegationDiscipline.General));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.BindRecipeToMachineAsync(machine.Id, Guid.NewGuid(), DelegationDiscipline.General));

        var bound = await service.BindRecipeToMachineAsync(machine.Id, recipe.Id, DelegationDiscipline.General);
        Assert.Equal("R1", bound.DelegationRecipeName);
        var rebound = await service.BindRecipeToMachineAsync(machine.Id, recipe.Id, DelegationDiscipline.General);
        Assert.Equal(bound.Id, rebound.Id);
        Assert.Equal("R1", rebound.DelegationRecipeName);
    }

    [Fact]
    public async Task UpdateMachine_ParentAssignment_RejectsSelfAndDescendantCycles()
    {
        using var context = CreateContext();
        var repository = new StationRepository(context);
        var cell = new Machine { Id = Guid.NewGuid(), Name = "CELL", CustomIdentifier = "CELL" };
        var station = new Machine { Id = Guid.NewGuid(), Name = "ST", CustomIdentifier = "ST", ParentMachineId = cell.Id };
        var nested = new Machine { Id = Guid.NewGuid(), Name = "NESTED", CustomIdentifier = "NESTED", ParentMachineId = station.Id };
        context.Machines.AddRange(cell, station, nested);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.UpdateAsync(cell.Id, null, null, null, null, null, parentMachineId: cell.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.UpdateAsync(cell.Id, null, null, null, null, null, parentMachineId: nested.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.UpdateAsync(cell.Id, null, null, null, null, null, parentMachineId: Guid.NewGuid()));

        // A legitimate re-parenting still works.
        var other = new Machine { Id = Guid.NewGuid(), Name = "OTHER", CustomIdentifier = "OTHER" };
        context.Machines.Add(other);
        await context.SaveChangesAsync();
        var updated = await repository.UpdateAsync(nested.Id, null, null, null, null, null, parentMachineId: other.Id);
        Assert.Equal(other.Id, updated!.ParentMachineId);
    }

    [Fact]
    public async Task AddSubMachine_IgnoresClientControlledIdentityTenantAndGraph()
    {
        using var context = CreateContext();
        var repository = new StationRepository(context);
        var parent = new Machine { Id = Guid.NewGuid(), Name = "RT", CustomIdentifier = "RT", OrganizationId = "Plant-A" };
        var victim = new Machine { Id = Guid.NewGuid(), Name = "VICTIM", CustomIdentifier = "VICTIM", OrganizationId = "Plant-B" };
        context.Machines.AddRange(parent, victim);
        await context.SaveChangesAsync();

        var payload = new Machine
        {
            Id = victim.Id,
            Name = "ST-01",
            CustomIdentifier = "RT-ST01",
            OrganizationId = "Plant-B",
            ParentMachineId = victim.Id,
            SubMachineIndex = 1,
            CellType = "Standard",
            SubMachines = new List<Machine> { new() { Id = Guid.NewGuid(), Name = "SMUGGLED", CustomIdentifier = "SMUGGLED" } }
        };

        var created = await repository.AddSubMachineAsync(parent.Id, payload);

        Assert.NotNull(created);
        Assert.NotEqual(victim.Id, created!.Id);
        Assert.Equal(parent.Id, created.ParentMachineId);
        Assert.Equal("Plant-A", created.OrganizationId);
        Assert.Equal(1, created.SubMachineIndex);
        Assert.Equal(3, await context.Machines.CountAsync());
        Assert.False(await context.Machines.AnyAsync(m => m.Name == "SMUGGLED"));
    }

    [Theory]
    [InlineData(typeof(App.Backend.Api.Controllers.V1.MachineController), new[] { "AddSubMachine", "RemoveSubMachine", "CreateMachine", "UpdateMachine", "UpdateMachineStatus", "DecommissionMachine", "UpdateOrganizationMachineTypes" })]
    [InlineData(typeof(App.Backend.Api.Controllers.V1.DelegationController), new[] { "BindRecipe", "UnbindRecipe", "CreateOrUpdateRecipe", "CreatePersonnelGroup", "DedicateToMachine", "RemoveMachineDedication", "DedicateUserToMachine", "RemoveUserDedicationFromMachine" })]
    public void MutatingEndpoints_DeclareAuthorizationPolicies(Type controller, string[] actions)
    {
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        foreach (var name in actions)
        {
            var routed = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == name && m.GetCustomAttribute<Microsoft.AspNetCore.Mvc.NonActionAttribute>() == null)
                .ToList();
            Assert.NotEmpty(routed);
            Assert.All(routed, m => Assert.False(
                string.IsNullOrEmpty(m.GetCustomAttribute<AuthorizeAttribute>()?.Policy),
                $"{controller.Name}.{name} must declare an authorization policy."));
        }
    }
}
