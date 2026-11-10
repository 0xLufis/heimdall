namespace App.Shared.Extensions;

using System;
using System.Collections.Generic;

/// <summary>
/// Data contract for submitting a custom reporting component into the agent.
/// </summary>
public class CustomComponentSubmission
{
    /// <summary>Machine-readable or display component name (e.g. "SpindleVibrationSensor").</summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>Technology or runtime source (e.g. "Python", "Modbus", "OPC-UA", "CustomPlugin").</summary>
    public string Technology { get; set; } = "CustomPlugin";

    /// <summary>Classification type: "sensor", "hardware", "software", "metrics", "diagnostic".</summary>
    public string ComponentType { get; set; } = "sensor";

    /// <summary>Arbitrary structured data payload.</summary>
    public object? Data { get; set; }

    /// <summary>Raw JSON string payload (used if Data is null or already serialized).</summary>
    public string? DataJson { get; set; }

    /// <summary>Optional name of the parent equipment in the station component tree (e.g. "Spindle Motor Assembly 15kW").</summary>
    public string? ParentComponentName { get; set; }

    /// <summary>Optional unique GUID of the parent inventory item if already known.</summary>
    public Guid? ParentComponentId { get; set; }

    /// <summary>Optional target Machine/Station GUID to bind this component to.</summary>
    public Guid? MachineId { get; set; }

    /// <summary>Time-to-live in seconds before this custom component expires from agent reporting. Default 3600 (1 hour).</summary>
    public int? TtlSeconds { get; set; } = 3600;

    /// <summary>If true, persists the component to disk so it survives agent daemon restarts.</summary>
    public bool Persist { get; set; } = false;

    /// <summary>If true, triggers an immediate gRPC synchronization cycle to the Heimdall backend.</summary>
    public bool ImmediateSync { get; set; } = false;

    /// <summary>Cryptographic trust status: true if reported by a verified signed plugin.</summary>
    public bool IsSigned { get; set; } = false;

    /// <summary>Sandboxing status: true if reported by an unsigned development plugin in sandbox isolation.</summary>
    public bool IsSandboxed { get; set; } = false;

    /// <summary>Originating plugin identifier if submitted by an installed plugin.</summary>
    public string? PluginId { get; set; }

    /// <summary>Timestamp when the component was submitted.</summary>
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Data contract for submitting custom time-series metrics.
/// </summary>
public class CustomTelemetrySubmission
{
    public string Source { get; set; } = "CustomSource";
    public List<CustomMetricPoint> Metrics { get; set; } = new();
}

/// <summary>
/// Individual telemetry data point submitted by custom software.
/// </summary>
public class CustomMetricPoint
{
    public string Key { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

/// <summary>
/// Data contract for submitting operational alarms or events.
/// </summary>
public class CustomEventSubmission
{
    public string Source { get; set; } = "CustomSource";
    public string Level { get; set; } = "Info"; // Info, Warning, High, Critical
    public string Message { get; set; } = string.Empty;
    public object? Payload { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Diagnostic status model returned by the agent status endpoint.
/// </summary>
public class AgentStatus
{
    public string Hostname { get; set; } = string.Empty;
    public string MachineIdentifier { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = "1.0.0";
    public string BackendUrl { get; set; } = string.Empty;
    public string AuthType { get; set; } = "NoAuth";
    public bool BackendConnected { get; set; }
    public DateTimeOffset? LastReportTimeUtc { get; set; }
    public int ActiveExtensionsCount { get; set; }
    public int ActivePluginsCount { get; set; }
    public string Environment { get; set; } = "Production";
}
