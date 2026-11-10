namespace App.Contracts.Telemetry;

using System;
using System.Collections.Generic;

/// <summary>
/// Point-in-time or windowed diagnostic telemetry observation embedded directly into a Maintenance Ticket.
/// Enables immediate physical verification on the shop floor without querying external time-series storage.
/// </summary>
public class TicketTelemetryProbe
{
    /// <summary>Unique probe identifier or signal name (e.g. "PROBE-SPINDLE-TEMP").</summary>
    public string ProbeId { get; set; } = string.Empty;

    /// <summary>Target PLC variable or OPC UA node path (e.g. "MAIN.Station1.Spindle.Temperature").</summary>
    public string DatapointKey { get; set; } = string.Empty;

    /// <summary>Human-readable metric label (e.g. "Spindle Bearing Temperature").</summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>Measured numeric reading at trigger moment.</summary>
    public double? NumericValue { get; set; }

    /// <summary>Measured string or state code (e.g. "ERR_OVERHEAT_TRIPPED", "ESTOP_ENGAGED").</summary>
    public string? StringValue { get; set; }

    /// <summary>Engineering unit (e.g. "°C", "bar", "RPM", "mm/s", "kW").</summary>
    public string? Unit { get; set; }

    /// <summary>Configured lower tolerance bound.</summary>
    public double? NominalLow { get; set; }

    /// <summary>Configured upper tolerance bound.</summary>
    public double? NominalHigh { get; set; }

    /// <summary>Evaluation outcome: "ThresholdExceeded", "AlarmBitSet", "RateOfChangeSpike", "Nominal".</summary>
    public string TriggerStatus { get; set; } = "ThresholdExceeded";

    /// <summary>UTC timestamp of anomalous reading.</summary>
    public DateTimeOffset SampledAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional localized high-resolution time-series buffer (±30s around trigger).</summary>
    public List<TelemetryProbePoint>? ProbeBuffer { get; set; }
}

/// <summary>
/// Individual point in a probe's localized time-series buffer.
/// </summary>
public class TelemetryProbePoint
{
    public DateTimeOffset Timestamp { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Request payload for machine/PLC raised incident tickets.
/// </summary>
public class MachineRaisedTicketRequestDto
{
    /// <summary>ID of the reporting machine.</summary>
    public Guid MachineId { get; set; }

    /// <summary>Optional specific component ID triggering or affected by the alarm.</summary>
    public Guid? TargetComponentId { get; set; }

    /// <summary>Optional EPLAN device tag / BMK (e.g. "-KF1", "-M1").</summary>
    public string? TargetComponentDeviceTag { get; set; }

    /// <summary>Optional additional affected component IDs.</summary>
    public List<Guid>? AffectedComponentIds { get; set; }

    /// <summary>Alarm or error code from PLC / controller (e.g. "ALM-TEMP-OVERHEAT-851").</summary>
    public string AlarmCode { get; set; } = string.Empty;

    /// <summary>Short summary title for the incident.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed message or PLC diagnostic description.</summary>
    public string? Description { get; set; }

    /// <summary>Priority level: "Critical", "High", "Medium", "Low".</summary>
    public string Priority { get; set; } = "High";

    /// <summary>Whether this alarm halts the production line.</summary>
    public bool IsLineStop { get; set; } = false;

    /// <summary>Controller hostname or identifier that dispatched the alarm.</summary>
    public string? ControllerSource { get; set; }

    /// <summary>Captured telemetry probes at the time of the alarm.</summary>
    public List<TicketTelemetryProbe> Probes { get; set; } = new();
}

/// <summary>
/// Response returned after successfully processing a machine-raised ticket.
/// </summary>
public class MachineRaisedTicketResponseDto
{
    public Guid TicketId { get; set; }
    public string TicketTitle { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string? AssignedTo { get; set; }
    public string? AssignedGroupName { get; set; }
    public bool IsEscalated { get; set; }
    public string? DelegationRecipeName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
