namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using App.Shared.Protos;

/// <summary>A validated lifecycle report (contract "heimdall.lifecycle/1", see system_info.proto).</summary>
public sealed record AgentLifecycleReport(
    string EventId,
    string Phase,
    string SessionId,
    string Level,
    string Message,
    DateTimeOffset At,
    JsonObject Payload);

/// <summary>
/// Validation and state folding for agent lifecycle / power reports, i.e. <c>AgentEventMessage</c>s with
/// <c>Source = "AgentLifecycle"</c>. Pure: persistence is done by <see cref="TelemetryIngestionService"/>.
/// </summary>
public static class AgentLifecycleIngestor
{
    public const string Source = "AgentLifecycle";
    public const string Schema = "heimdall.lifecycle/1";
    public const string MetadataKey = "AgentLifecycle";
    public const string QuarantineChannel = "MQTT_events_lifecycle";
    public const int MaxPayloadBytes = 256 * 1024;
    public const int MaxMessageLength = 2000;
    public const int RecentEventIdsKept = 50;

    public static readonly IReadOnlyList<string> Phases = new[] { "started", "online", "stopping", "connection_lost" };
    public static readonly IReadOnlyList<string> Levels = new[] { "Info", "Warning", "Error", "Critical" };

    public static readonly IReadOnlyList<string> Classifications = new[]
    {
        "first_start", "os_shutdown", "service_stop", "agent_crash", "agent_killed",
        "unexpected_shutdown", "os_shutdown_unreported", "unknown"
    };

    public static readonly IReadOnlyList<string> StopReasons = new[]
    {
        "os_shutdown", "os_restart", "service_stop", "service_restart", "agent_fault", "unknown"
    };

    public static bool IsLifecycleEvent(AgentEventMessage message) =>
        string.Equals(message.Source, Source, StringComparison.OrdinalIgnoreCase);

    /// <summary>Validates a lifecycle event. Returns false with a reason when it must be quarantined.</summary>
    public static bool TryParse(AgentEventMessage message, DateTimeOffset nowUtc, out AgentLifecycleReport? report, out string? error)
    {
        report = null;
        error = null;

        if (string.IsNullOrWhiteSpace(message.MachineIdentifier))
        {
            error = "machine_identifier is empty";
            return false;
        }
        if (string.IsNullOrWhiteSpace(message.EventId) || message.EventId.Length > 200)
        {
            error = "event_id is empty or longer than 200 characters";
            return false;
        }
        if (string.IsNullOrEmpty(message.PayloadJson) || message.PayloadJson.Length > MaxPayloadBytes)
        {
            error = $"payload_json is empty or larger than {MaxPayloadBytes} bytes";
            return false;
        }

        JsonObject? payload;
        try
        {
            payload = JsonNode.Parse(message.PayloadJson) as JsonObject;
        }
        catch (JsonException ex)
        {
            error = "payload_json is not valid JSON: " + ex.Message;
            return false;
        }
        if (payload == null)
        {
            error = "payload_json is not a JSON object";
            return false;
        }

        string? schema = Text(payload, "schema");
        if (schema != Schema)
        {
            error = $"unsupported schema '{schema}' (expected '{Schema}')";
            return false;
        }

        string? phase = Text(payload, "phase");
        if (phase == null || !Phases.Contains(phase))
        {
            error = $"unknown phase '{phase}'";
            return false;
        }

        string? sessionId = Text(payload, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 100)
        {
            error = "sessionId is missing";
            return false;
        }

        if (phase == "started")
        {
            string? classification = payload["previous"] is JsonObject previous ? Text(previous, "classification") : null;
            if (classification == null || !Classifications.Contains(classification))
            {
                error = $"started report with unknown previous.classification '{classification}'";
                return false;
            }
        }
        else if (phase == "stopping")
        {
            string? reason = payload["stop"] is JsonObject stop ? Text(stop, "reason") : null;
            if (reason == null || !StopReasons.Contains(reason))
            {
                error = $"stopping report with unknown stop.reason '{reason}'";
                return false;
            }
        }

        string level = Levels.FirstOrDefault(l => string.Equals(l, message.Level?.Trim(), StringComparison.OrdinalIgnoreCase))
                       ?? (string.Equals(message.Level, "Information", StringComparison.OrdinalIgnoreCase) ? "Info" : "Warning");

        // The agent's clock is not trusted blindly: epoch zero or far in the future means "unknown".
        DateTimeOffset at = message.Timestamp?.ToDateTimeOffset() ?? nowUtc;
        if (at.Year < 2000 || at > nowUtc.AddMinutes(10)) at = nowUtc;

        string text = string.IsNullOrWhiteSpace(message.Message) ? $"Agent lifecycle: {phase}" : message.Message.Trim();
        if (text.Length > MaxMessageLength) text = text[..MaxMessageLength];

        report = new AgentLifecycleReport(message.EventId.Trim(), phase, sessionId, level, text, at, payload);
        return true;
    }

    /// <summary>True when <paramref name="eventId"/> was already applied to this device (QoS 1 redelivery, agent retry).</summary>
    public static bool AlreadyApplied(JsonObject? lifecycleState, string eventId) =>
        lifecycleState?["RecentEventIds"] is JsonArray recent && recent.Any(n => n?.GetValue<string>() == eventId);

    /// <summary>
    /// Folds a report into the device's lifecycle state (<c>system_metadata.AgentLifecycle</c>):
    /// <c>State</c> online / stopping / stopped / offline, the last report of each phase, and the
    /// ids of recently applied events for de-duplication.
    /// </summary>
    public static JsonObject Apply(JsonObject? current, AgentLifecycleReport report, DateTimeOffset receivedAtUtc)
    {
        var state = current?.DeepClone() as JsonObject ?? new JsonObject();
        string? currentSession = state["SessionId"]?.GetValue<string>();
        DateTimeOffset? lastEventAt = state["LastEventAtUtc"] is JsonValue last && last.TryGetValue<DateTime>(out var lastTime)
            ? new DateTimeOffset(DateTime.SpecifyKind(lastTime, DateTimeKind.Utc))
            : null;

        // A last will carries the time its connection was opened; it is published when that connection
        // is lost. A will of another session opened before the current state was reached is late news
        // (e.g. delivered after the agent already restarted) and must not mark a running agent offline;
        // a will of the session that already said goodbye does not turn "stopped" into "offline".
        bool isWill = report.Phase == "connection_lost";
        bool staleForState = isWill && currentSession != null &&
                             ((currentSession != report.SessionId && lastEventAt.HasValue && report.At <= lastEventAt.Value) ||
                              (currentSession == report.SessionId && state["State"]?.GetValue<string>() == "stopped"));
        var eventTime = isWill ? receivedAtUtc : report.At;

        var summary = new JsonObject
        {
            ["EventId"] = report.EventId,
            ["SessionId"] = report.SessionId,
            ["Level"] = report.Level,
            ["Message"] = report.Message,
            ["AtUtc"] = eventTime.UtcDateTime,
            ["ReceivedAtUtc"] = receivedAtUtc.UtcDateTime,
            ["Payload"] = report.Payload.DeepClone()
        };

        switch (report.Phase)
        {
            case "started":
                state["LastStart"] = summary;
                if (report.Payload["previous"] is JsonObject previous)
                {
                    state["PreviousRunClassification"] = previous["classification"]?.DeepClone();
                    state["PreviousRunInitiator"] = previous["initiator"]?.DeepClone();
                    state["PreviousRunInitiatedBy"] = previous["initiatedBy"]?.DeepClone();
                }
                break;
            case "stopping":
                state["LastStop"] = summary;
                if (report.Payload["stop"] is JsonObject stop)
                {
                    state["LastStopReason"] = stop["reason"]?.DeepClone();
                    state["LastStopInitiator"] = stop["initiator"]?.DeepClone();
                }
                break;
            case "connection_lost":
                state["LastConnectionLost"] = summary;
                break;
            case "online":
                state["LastOnline"] = summary;
                break;
        }

        if (!staleForState)
        {
            state["State"] = report.Phase switch
            {
                "started" or "online" => "online",
                "stopping" => "stopped",
                _ => "offline"
            };
            state["SessionId"] = report.SessionId;
            state["AgentVersion"] = report.Payload["agentVersion"]?.DeepClone();
            state["BootId"] = report.Payload["bootId"]?.DeepClone();
            state["LastPhase"] = report.Phase;
            state["LastEventAtUtc"] = eventTime.UtcDateTime;
        }

        var recent = state["RecentEventIds"] as JsonArray ?? new JsonArray();
        recent.Add(report.EventId);
        while (recent.Count > RecentEventIdsKept) recent.RemoveAt(0);
        state["RecentEventIds"] = recent;

        return state;
    }

    private static string? Text(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
