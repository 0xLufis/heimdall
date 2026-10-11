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
    ParallelWork = 3,
    HandedOffToField = 4
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

/// <summary>
/// Architectural type of machine group.
/// </summary>
public enum MachineGroupType
{
    Customer = 1,
    Line = 2,
    Technological = 3,
    Manual = 4
}

/// <summary>
/// Operational availability and schedule profile of plant personnel.
/// </summary>
[System.Flags]
public enum WorkerScheduleType
{
    Shift = 1,
    OfficeTime = 2,
    OnCall = 4
}

/// <summary>
/// Category of personnel skill, certificate, or authorization.
/// </summary>
public enum SkillCategory
{
    Certification = 1,
    TechnologyKnowHow = 2,
    MachineAuthorization = 3,
    SafetyClearance = 4
}

/// <summary>
/// Engineering discipline or responsibility area for delegation recipes.
/// </summary>
public enum DelegationDiscipline
{
    Mechanical = 1,
    Controls = 2,
    Electrical = 3,
    Safety = 4,
    Quality = 5,
    Process = 6,
    Tooling = 7,
    General = 99
}

/// <summary>
/// Broad category of machine components.
/// </summary>
public enum ComponentCategory
{
    Hardware = 1,
    SoftwareBinary = 2,
    SoftwareRuntime = 3,
    SoftwareConfig = 4,
    SoftwareProgram = 5,
    SoftwareRecipe = 6,
    License = 7,
    Document = 8
}

/// <summary>
/// Justification reason for swapping or replacing a physical part or component.
/// </summary>
public enum PartSwapReason
{
    FailureBreakdown = 1,
    WearDepreciation = 2,
    ScheduledPM = 3,
    RetrofitUpgrade = 4,
    CalibrationSwap = 5,
    ScheduledReplacement = 6,
    InitialCommissioning = 7
}

/// <summary>
/// Physical disposition of a removed component part.
/// </summary>
public enum PartDisposition
{
    Scrapped = 1,
    SentToRepair = 2,
    DonorStock = 3,
    RetainedForInvestigation = 4,
    SentForRepair = 5,
    ReturnedToStock = 6,
    RetainedForAnalysis = 7
}

/// <summary>
/// Discriminator for software asset types.
/// </summary>
public enum SoftwareAssetType
{
    Binary = 1,
    Runtime = 2,
    Config = 3,
    Program = 4,
    Recipe = 5
}

/// <summary>
/// Dynamic operational runtime condition of a machine.
/// </summary>
public enum MachineOperationalState
{
    Running = 1,
    Idle = 2,
    Starved = 3,
    Blocked = 4,
    Faulted = 5,
    Changeover = 6,
    Offline = 7
}

/// <summary>
/// Production criticality classification.
/// </summary>
public enum MachineCriticalityTier
{
    Tier1_CriticalLineStop = 1,
    Tier2_Major = 2,
    Tier3_Auxiliary = 3
}

