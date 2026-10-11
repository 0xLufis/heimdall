namespace App.Infrastructure.Services;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using App.Contracts.Enums;
using App.Contracts.Telemetry;
using App.Shared.Data;
using App.Shared.Entities;

public class MachineRaisedTicketService : IMachineRaisedTicketService
{
    private readonly AppDbContext _context;
    private readonly IDelegationRecipeService _delegationService;

    public MachineRaisedTicketService(AppDbContext context, IDelegationRecipeService delegationService)
    {
        _context = context;
        _delegationService = delegationService;
    }

    public async Task<MachineRaisedTicketResponseDto> CreateMachineRaisedTicketAsync(
        MachineRaisedTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == request.MachineId, cancellationToken);
        if (machine == null)
        {
            throw new KeyNotFoundException($"Machine with ID '{request.MachineId}' was not found.");
        }

        MachineComponent? targetComponent = null;
        if (request.TargetComponentId.HasValue && request.TargetComponentId.Value != Guid.Empty)
        {
            targetComponent = await _context.MachineComponents
                .FirstOrDefaultAsync(c => c.Id == request.TargetComponentId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.TargetComponentDeviceTag))
        {
            var deviceTag = request.TargetComponentDeviceTag.Trim();
            targetComponent = await _context.MachineComponents
                .FirstOrDefaultAsync(c => c.MachineId == request.MachineId &&
                                         (c.FunctionalSlotIdentifier == deviceTag ||
                                         (c.EplanCadRefJson != null && c.EplanCadRefJson.Contains(deviceTag))), cancellationToken);
        }

        // Determine discipline based on alarm and target component
        var discipline = DelegationDiscipline.General;
        if (targetComponent != null)
        {
            discipline = targetComponent.Category switch
            {
                ComponentCategory.Hardware => DelegationDiscipline.Mechanical,
                ComponentCategory.SoftwareBinary or
                ComponentCategory.SoftwareRuntime or
                ComponentCategory.SoftwareConfig or
                ComponentCategory.SoftwareProgram or
                ComponentCategory.SoftwareRecipe => DelegationDiscipline.Controls,
                _ => DelegationDiscipline.General
            };
        }

        if (request.AlarmCode.Contains("PLC", StringComparison.OrdinalIgnoreCase) ||
            request.AlarmCode.Contains("COMM", StringComparison.OrdinalIgnoreCase) ||
            request.AlarmCode.Contains("NET", StringComparison.OrdinalIgnoreCase))
        {
            discipline = DelegationDiscipline.Controls;
        }
        else if (request.AlarmCode.Contains("ELEC", StringComparison.OrdinalIgnoreCase) ||
                 request.AlarmCode.Contains("FUSE", StringComparison.OrdinalIgnoreCase) ||
                 request.AlarmCode.Contains("VOLT", StringComparison.OrdinalIgnoreCase))
        {
            discipline = DelegationDiscipline.Electrical;
        }

        // Determine current schedule type based on plant hour
        var now = DateTimeOffset.UtcNow;
        var scheduleType = WorkerScheduleType.Shift;
        if (now.Hour is < 6 or >= 22)
        {
            scheduleType = WorkerScheduleType.OnCall;
        }

        // Resolve responsible personnel
        var resolved = await _delegationService.ResolvePersonnelForMachineAsync(
            request.MachineId,
            discipline,
            scheduleType,
            cancellationToken);

        string effectiveAssignee = resolved.EffectiveAssigneeName
            ?? resolved.EffectiveAssigneeId
            ?? resolved.EffectiveGroupName
            ?? machine.PreferredTechnicianName
            ?? machine.PreferredTechnicianId
            ?? "Unassigned";

        string title = !string.IsNullOrWhiteSpace(request.Title)
            ? request.Title
            : $"[AUTO-ALARM] {request.AlarmCode} on {machine.Name}";

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = request.Description ?? $"Automated alarm {request.AlarmCode} raised by controller {request.ControllerSource ?? "PLC"}.",
            Status = "Open",
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "High" : request.Priority,
            IsLineStop = request.IsLineStop,
            OriginatorType = "MachineAutomatic",
            IssueType = "Maintenance",
            MachineId = request.MachineId,
            TargetComponentId = targetComponent?.Id ?? request.TargetComponentId,
            TargetComponentBmk = request.TargetComponentDeviceTag ?? targetComponent?.FunctionalSlotIdentifier,
            AlarmCode = request.AlarmCode,
            ControllerSource = request.ControllerSource,
            AffectedComponentIdsJson = request.AffectedComponentIds != null ? JsonSerializer.Serialize(request.AffectedComponentIds) : null,
            TelemetryProbesJson = request.Probes != null ? JsonSerializer.Serialize(request.Probes) : null,
            AssignedTo = effectiveAssignee,
            ReservedBy = resolved.EffectiveAssigneeId,
            OrganizationId = machine.OrganizationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.MaintenanceTickets.Add(ticket);

        // Add service log to Machine journal
        var serviceLog = new MachineServiceLog
        {
            Id = Guid.NewGuid(),
            MachineId = request.MachineId,
            Timestamp = DateTimeOffset.UtcNow,
            Author = request.ControllerSource ?? "MachineController",
            EntryType = "EmergencyRepair",
            Summary = $"Machine-raised incident ticket created: {ticket.Title}",
            Details = $"Alarm: {request.AlarmCode}, Priority: {ticket.Priority}, Assigned: {effectiveAssignee}",
            LinkedTicketId = ticket.Id
        };
        _context.MachineServiceLogs.Add(serviceLog);

        await _context.SaveChangesAsync(cancellationToken);

        return new MachineRaisedTicketResponseDto
        {
            TicketId = ticket.Id,
            TicketTitle = ticket.Title,
            Status = ticket.Status,
            AssignedTo = ticket.AssignedTo,
            AssignedGroupName = resolved.EffectiveGroupName,
            IsEscalated = ticket.IsEscalated,
            DelegationRecipeName = resolved.RecipeName,
            CreatedAt = ticket.CreatedAt
        };
    }
}
