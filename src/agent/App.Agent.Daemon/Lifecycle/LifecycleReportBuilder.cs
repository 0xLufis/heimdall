namespace App.Agent.Daemon.Lifecycle;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using App.Agent.Daemon.Infrastructure.Persistence;
using App.Shared.Protos;
using Google.Protobuf.WellKnownTypes;

/// <summary>Builds <c>AgentEventMessage</c>s of the "heimdall.lifecycle/1" contract (see system_info.proto).</summary>
public static class LifecycleReportBuilder
{
    public const int MaxMessageLength = 1000;

    public static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AgentEventMessage Started(string machineIdentifier, string hostname, AgentSession session, BootInfo boot,
        PreviousRunAssessment previous, IReadOnlyList<RecoveryNotice> recoveries)
    {
        var payload = Envelope(LifecycleContract.Phase.Started, session, boot);
        payload["previous"] = JsonSerializer.SerializeToNode(previous, PayloadOptions);
        if (recoveries.Count > 0)
        {
            payload["recoveries"] = new JsonArray(recoveries.Select(r => (JsonNode?)JsonSerializer.SerializeToNode(new
            {
                r.Component, r.File, r.Defect, r.RecoveredFrom, r.Severity, r.Detail, r.AtUtc
            }, PayloadOptions)).ToArray());
        }

        string level = WorstOf(previous.Level, recoveries.Select(r => r.Severity == "Critical" ? "Critical" : r.Severity == "Error" ? "Error" : "Warning"));
        string text = $"Agent started. Previous run: {previous.Classification}"
                      + (previous.Cause != null ? $" ({previous.Cause})" : string.Empty)
                      + (previous.OsAction != null ? $", OS {previous.OsAction}" : string.Empty)
                      + (previous.InitiatedBy != null ? $" by {previous.InitiatedBy}" : string.Empty)
                      + (previous.DowntimeSeconds.HasValue && previous.Classification != LifecycleContract.Classification.FirstStart ? $", down {previous.DowntimeSeconds} s" : string.Empty)
                      + (string.IsNullOrWhiteSpace(previous.Reason) ? string.Empty : $" - {previous.Reason}")
                      + (recoveries.Count > 0 ? $". {recoveries.Count} damaged file(s) recovered: {string.Join(", ", recoveries.Select(r => $"{r.Component} {r.Defect} -> {r.RecoveredFrom}").Distinct())}" : string.Empty);

        return Message(machineIdentifier, hostname, $"lifecycle:{session.SessionId}:{LifecycleContract.Phase.Started}", level, text, DateTimeOffset.UtcNow, payload);
    }

    public static AgentEventMessage Stopping(string machineIdentifier, string hostname, AgentSession session, BootInfo boot, StopInfo stop)
    {
        var payload = Envelope(LifecycleContract.Phase.Stopping, session, boot);
        payload["stop"] = JsonSerializer.SerializeToNode(stop, PayloadOptions);

        string level = stop.Reason switch
        {
            LifecycleContract.StopReason.AgentFault => "Error",
            LifecycleContract.StopReason.Unknown => "Warning",
            _ => "Info"
        };
        string text = $"Agent stopping: {stop.Reason} (initiator {stop.Initiator}"
                      + (stop.InitiatedBy != null ? $", by {stop.InitiatedBy}" : string.Empty)
                      + $", via {stop.Signal ?? LifecycleContract.Signal.Host})"
                      + (string.IsNullOrWhiteSpace(stop.Detail) ? string.Empty : $" - {stop.Detail}");
        return Message(machineIdentifier, hostname, $"lifecycle:{session.SessionId}:{LifecycleContract.Phase.Stopping}", level, text, stop.AtUtc, payload);
    }

    public static AgentEventMessage ConnectionLost(string machineIdentifier, string hostname, AgentSession session, BootInfo boot, int connection)
    {
        var payload = Envelope(LifecycleContract.Phase.ConnectionLost, session, boot);
        return Message(machineIdentifier, hostname, $"lifecycle:{session.SessionId}:{LifecycleContract.Phase.ConnectionLost}:{connection}", "Warning",
            "Agent connection lost without a goodbye (agent crash, power loss, OS crash or network outage; the next start report tells which)",
            DateTimeOffset.UtcNow, payload);
    }

    public static AgentEventMessage Online(string machineIdentifier, string hostname, AgentSession session, BootInfo boot, int connection)
    {
        var payload = Envelope(LifecycleContract.Phase.Online, session, boot);
        return Message(machineIdentifier, hostname, $"lifecycle:{session.SessionId}:{LifecycleContract.Phase.Online}:{connection}", "Info",
            "Agent reconnected", DateTimeOffset.UtcNow, payload);
    }

    private static JsonObject Envelope(string phase, AgentSession session, BootInfo boot) => new()
    {
        ["schema"] = LifecycleContract.Schema,
        ["phase"] = phase,
        ["sessionId"] = session.SessionId,
        ["bootId"] = boot.BootId,
        ["agentVersion"] = session.AgentVersion,
        ["processStartedAtUtc"] = session.ProcessStartedAtUtc.UtcDateTime,
        ["osBootedAtUtc"] = boot.BootTimeUtc.UtcDateTime,
        ["atUtc"] = DateTime.UtcNow
    };

    private static AgentEventMessage Message(string machineIdentifier, string hostname, string eventId, string level, string text,
        DateTimeOffset at, JsonObject payload) => new()
    {
        MachineIdentifier = machineIdentifier,
        Hostname = hostname,
        EventId = eventId,
        Source = LifecycleContract.Source,
        Level = level,
        Message = text.Length > MaxMessageLength ? text[..MaxMessageLength] : text,
        Timestamp = Timestamp.FromDateTimeOffset(at),
        PayloadJson = payload.ToJsonString()
    };

    private static string WorstOf(string level, IEnumerable<string> others)
    {
        static int Rank(string l) => l switch { "Critical" => 3, "Error" => 2, "Warning" => 1, _ => 0 };
        return others.Append(level).OrderByDescending(Rank).First();
    }
}
