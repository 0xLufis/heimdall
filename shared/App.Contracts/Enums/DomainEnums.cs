namespace App.Contracts.Enums;

/// <summary>
/// Lifecycle status of factory equipment and inventory assets.
/// </summary>
public enum EquipmentStatus
{
    InStorage = 1,
    InMachine = 2,
    UnderRepair = 3,
    Decommissioned = 4,
    Scrapped = 5
}

/// <summary>
/// Operational status of an industrial edge node or device.
/// </summary>
public enum DeviceOperationalStatus
{
    Online = 1,
    Offline = 2,
    Degraded = 3,
    Maintenance = 4,
    Faulted = 5,
    Unknown = 6
}



/// <summary>
/// Industrial operational technologies.
/// </summary>
public enum IndustrialTechnology
{
    Assembly = 1,
    Testing = 2,
    Smt = 3,
    Welding = 4,
    Dispensing = 5,
    Fastening = 6,
    Robotics = 7,
    Controls = 8,
    Packaging = 9
}

/// <summary>
/// Dynamic runtime registry for custom industrial technology extensions.
/// </summary>
public static class TechnologyRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _customTechnologies = new(System.StringComparer.OrdinalIgnoreCase);

    public static void RegisterTechnology(string name, int code)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _customTechnologies[name.Trim()] = code;
        }
    }

    public static bool TryGetTechnologyCode(string name, out int code)
    {
        if (System.Enum.TryParse<IndustrialTechnology>(name, true, out var parsed))
        {
            code = (int)parsed;
            return true;
        }

        return _customTechnologies.TryGetValue(name, out code);
    }
}

/// <summary>
/// Industrial communication protocol between equipment and controllers.
/// </summary>
public enum InterconnectProtocol
{
    Ethernet = 1,
    OpcUa = 2,
    Profinet = 3,
    EtherCat = 4,
    ModbusTcp = 5,
    ModbusRtu = 6,
    EtherNetIp = 7,
    Serial = 8
}

/// <summary>
/// Operational status of a maintenance ticket. Strictly 5 canonical states.
/// </summary>
public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    Pending = 3,
    Resolved = 4,
    Closed = 5
}

/// <summary>
/// Sub-status reason describing why a maintenance ticket is in Pending status.
/// </summary>
public enum PendingReason
{
    None = 0,
    Parts = 1,
    ExternalOk = 2,
    Action = 3,
    SignOff = 4,
    Custom = 5
}

/// <summary>
/// Sub-state for ticket escalation handover (Notification only, Hand-off of work, Parallel work).
/// </summary>
public enum EscalationHandoverState
{
    None = 0,
    Notification = 1,
    HandOff = 2,
    ParallelWork = 3
}

/// <summary>
/// Originator classification for maintenance tickets.
/// </summary>
public enum TicketOriginatorType
{
    ManualUser = 1,
    MachineAutomatic = 2,
    ScheduledMaintenance = 3
}

/// <summary>
/// Nature or category of ticket issue.
/// </summary>
public enum TicketIssueType
{
    Transient = 1,
    Maintenance = 2,
    Improvement = 3,
    Other = 4
}

/// <summary>
/// Priority level of a maintenance ticket.
/// </summary>
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Log severity level for agent audit events.
/// </summary>
public enum EventSeverity
{
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// IT Governance access level for Active Directory Organizational Units.
/// </summary>
public enum OuAccessLevel
{
    Unapproved = 0,
    ReadOnly = 1,
    ReadWrite = 2
}

/// <summary>
/// Status of an issued X.509 client certificate.
/// </summary>
public enum CertificateStatus
{
    Active = 1,
    Revoked = 2,
    Expired = 3,
    Superseded = 4
}

/// <summary>
/// Target type for Multi-Factor Authentication enforcement rules.
/// </summary>
public enum MfaTargetType
{
    Role = 1,
    Group = 2
}

/// <summary>
/// Re-authentication timeout thresholds for MFA enforcement.
/// </summary>
public enum MfaTimeoutThreshold
{
    Always = 1,
    Hours12 = 2,
    Hours24 = 3,
    Days7 = 4,
    Days14 = 5,
    Days30 = 6,
    Days90 = 7,
    Custom = 8,
    Never = 9
}
