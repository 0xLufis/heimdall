namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'tickets' table.
/// Exposes automatic ticket generation, predictive analytics integration,
/// SLA preventive maintenance triggers, status transitions, and escalation workflows.
/// </summary>
public class TicketsLuaApi
{
    private readonly ILuaPlantDataProvider _provider;

    public TicketsLuaApi(ILuaPlantDataProvider provider)
    {
        _provider = provider;
    }

    public void Register(Script script)
    {
        var table = new Table(script);

        table["create"] = (Func<Table, DynValue>)((opts) => CreateTicketInternal(script, opts));

        table["get"] = (Func<string, DynValue>)((idStr) =>
        {
            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id))
                return DynValue.Nil;

            var ticket = _provider.GetTicketAsync(id).GetAwaiter().GetResult();
            return ticket == null ? DynValue.Nil : DynValue.NewTable(TicketToTable(script, ticket));
        });

        table["list"] = (Func<Table, DynValue>)((filter) =>
        {
            string? status = filter?["status"] as string;
            string? machineId = filter?["machine_id"] as string ?? filter?["machineId"] as string;
            string? priority = filter?["priority"] as string;
            bool? isEscalated = filter?["is_escalated"] != null ? (bool?)Convert.ToBoolean(filter["is_escalated"]) : null;

            var tickets = _provider.ListTicketsAsync(status, machineId, priority, isEscalated).GetAwaiter().GetResult();
            var result = new Table(script);
            int idx = 1;
            foreach (var t in tickets)
            {
                result[idx++] = TicketToTable(script, t);
            }
            return DynValue.NewTable(result);
        });

        table["update_status"] = (Func<string, string, string, bool>)((idStr, status, notes) =>
        {
            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id)) return false;
            return _provider.UpdateTicketStatusAsync(id, status, notes).GetAwaiter().GetResult();
        });

        table["escalate"] = (Func<string, string, string, string, bool>)((idStr, reason, escalatedBy, target) =>
        {
            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id)) return false;
            return _provider.EscalateTicketAsync(id, reason, escalatedBy ?? "LuaAutomation", target).GetAwaiter().GetResult();
        });

        // SLA Evaluation helper: Evaluates ticket response SLA
        table["check_sla"] = (Func<string, double, DynValue>)((idStr, maxHours) =>
        {
            var res = new Table(script);
            res["is_breached"] = false;
            res["elapsed_minutes"] = 0.0;
            res["remaining_minutes"] = 0.0;

            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id))
                return DynValue.NewTable(res);

            var ticket = _provider.GetTicketAsync(id).GetAwaiter().GetResult();
            if (ticket == null) return DynValue.NewTable(res);

            double allowedHours = maxHours > 0 ? maxHours : ticket.Priority.ToLowerInvariant() switch
            {
                "critical" => 2.0,
                "high" => 4.0,
                "medium" => 8.0,
                "low" => 24.0,
                _ => 8.0
            };

            var elapsed = DateTimeOffset.UtcNow - ticket.CreatedAt;
            var remaining = TimeSpan.FromHours(allowedHours) - elapsed;

            res["is_breached"] = remaining.TotalMinutes <= 0 && ticket.Status != "Resolved" && ticket.Status != "Closed";
            res["elapsed_minutes"] = Math.Round(elapsed.TotalMinutes, 1);
            res["remaining_minutes"] = Math.Round(remaining.TotalMinutes, 1);
            res["allowed_hours"] = allowedHours;
            res["priority"] = ticket.Priority;
            res["status"] = ticket.Status;

            return DynValue.NewTable(res);
        });

        // SLA Preventive Maintenance Trigger: Directly fulfills spec
        table["trigger_preventive_maintenance"] = (Func<string, string, Table, DynValue>)((machineId, reason, opts) =>
        {
            string title = opts?.Get("title").String ?? $"[SLA-PM] Preventive Maintenance: {reason}";
            string priority = opts?.Get("priority").String ?? "Medium";
            double slaHours = opts?.Get("sla_hours").Type == DataType.Number ? opts.Get("sla_hours").Number : 24.0;
            string? assignedGroup = opts?.Get("assigned_group").String ?? opts?.Get("assigned_to").String;

            var createOpts = new Table(script);
            createOpts["title"] = title;
            createOpts["description"] = $"Preventive maintenance triggered via SLA rule. Reason: {reason}";
            createOpts["machine_id"] = machineId;
            createOpts["priority"] = priority;
            createOpts["issue_type"] = "Maintenance";
            createOpts["assigned_to"] = assignedGroup ?? "Maintenance Team";
            createOpts["sla_hours"] = slaHours;

            return CreateTicketInternal(script, createOpts);
        });

        // Analytics-based ticket trigger
        table["create_from_analytics"] = (Func<Table, DynValue>)((analyticsData) =>
        {
            if (analyticsData == null) return DynValue.Nil;

            string machineId = analyticsData.Get("machine_id").String ?? string.Empty;
            string anomalyType = analyticsData.Get("anomaly_type").String ?? "Anomaly Detected";
            double confidence = analyticsData.Get("confidence").Type == DataType.Number ? analyticsData.Get("confidence").Number : 0.85;
            string recommendation = analyticsData.Get("recommended_action").String ?? "Inspect component";
            string severity = analyticsData.Get("severity").String ?? "High";

            var createOpts = new Table(script);
            createOpts["title"] = $"[ANALYTICS] {anomalyType} on {machineId}";
            createOpts["description"] = $"Predictive Analytics Alert: {anomalyType} (Confidence: {confidence * 100:F0}%).\nRecommended Action: {recommendation}";
            createOpts["machine_id"] = machineId;
            createOpts["priority"] = severity;
            createOpts["issue_type"] = "Maintenance";
            createOpts["sla_hours"] = severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? 2.0 : 6.0;

            return CreateTicketInternal(script, createOpts);
        });

        script.Globals["tickets"] = table;
    }

    private DynValue CreateTicketInternal(Script script, Table opts)
    {
        if (opts == null) return DynValue.Nil;

        string title = opts.Get("title").String ?? "Automated Maintenance Ticket";
        string? description = opts.Get("description").String;
        string? machineIdStr = opts.Get("machine_id").String ?? opts.Get("machineId").String;
        string priority = opts.Get("priority").String ?? "Medium";
        string issueType = opts.Get("issue_type").String ?? "Maintenance";
        bool isLineStop = opts.Get("is_line_stop").Boolean;
        string? alarmCode = opts.Get("alarm_code").String;
        string? assignedTo = opts.Get("assigned_to").String;
        double slaHours = opts.Get("sla_hours").Type == DataType.Number ? opts.Get("sla_hours").Number : 8.0;

        Guid? machineGuid = null;
        if (!string.IsNullOrWhiteSpace(machineIdStr))
        {
            if (Guid.TryParse(machineIdStr, out var parsedGuid))
            {
                machineGuid = parsedGuid;
            }
            else
            {
                var machine = _provider.GetMachineAsync(machineIdStr).GetAwaiter().GetResult();
                if (machine != null) machineGuid = machine.Id;
            }
        }

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description ?? $"Automated ticket generated via Lua rule. Alarm: {alarmCode ?? "N/A"}",
            Status = "Open",
            Priority = priority,
            IssueType = issueType,
            IsLineStop = isLineStop,
            AlarmCode = alarmCode,
            OriginatorType = "MachineAutomatic",
            MachineId = machineGuid,
            AssignedTo = assignedTo ?? "Unassigned",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = _provider.CreateTicketAsync(ticket).GetAwaiter().GetResult();
        return DynValue.NewTable(TicketToTable(script, created, slaHours));
    }

    private static Table TicketToTable(Script script, MaintenanceTicket t, double slaHours = 8.0)
    {
        var tbl = new Table(script);
        tbl["id"] = t.Id.ToString();
        tbl["title"] = t.Title;
        tbl["description"] = t.Description ?? string.Empty;
        tbl["status"] = t.Status;
        tbl["priority"] = t.Priority;
        tbl["issue_type"] = t.IssueType;
        tbl["is_line_stop"] = t.IsLineStop;
        tbl["is_escalated"] = t.IsEscalated;
        tbl["escalation_reason"] = t.EscalationReason ?? string.Empty;
        tbl["alarm_code"] = t.AlarmCode ?? string.Empty;
        tbl["assigned_to"] = t.AssignedTo ?? string.Empty;
        tbl["machine_id"] = t.MachineId?.ToString() ?? string.Empty;
        tbl["created_at"] = t.CreatedAt.ToString("o");
        tbl["sla_hours"] = slaHours;
        return tbl;
    }
}
