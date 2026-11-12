using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

using App.Contracts.Enums;

namespace App.Shared.Entities;

// --- SHARED RESPONSIBILITY ---

/// <summary>
/// Represents an engineering team responsible for certain assets.
/// </summary>
public partial class ResponsibleTeam
{
    public Guid Id { get; set; }
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty; // Mechanical, Controls, Vision, Dispensing
    public string? Description { get; set; }
    
    public List<BaseInventoryItem> ManagedItems { get; set; } = new();
}

// --- CORE ABSTRACTION (The "Abstract Class") ---

/// <summary>
/// The base class for all inventory items, following an Object-Oriented Programming (OOP) approach.
/// Provides common attributes such as identity, financial data, and hierarchical relationships.
/// </summary>
public abstract partial class BaseInventoryItem
{
    /// <summary>Unique identifier for the inventory item.</summary>
    public Guid Id { get; set; }

    /// <summary>The name of the asset.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional user-friendly display name.</summary>
    public string? DisplayName { get; set; }
    
    /// <summary>Identifier for the organization unit that owns this asset.</summary>
    public string? OrganizationId { get; set; }
    
    /// <summary>The financial cost of the asset in Hungarian Forint (HUF).</summary>
    public decimal? CostInHUF { get; set; }

    /// <summary>The date when the asset was purchased.</summary>
    public DateTimeOffset? PurchaseDate { get; set; }

    /// <summary>The hardware or license serial number.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>The ID of the manufacturer of the asset.</summary>
    public Guid? ManufacturerId { get; set; }
    /// <summary>Navigation property for the manufacturer.</summary>
    public Manufacturer? Manufacturer { get; set; }

    /// <summary>The ID of the supplier of the asset.</summary>
    public Guid? SupplierId { get; set; }
    /// <summary>Navigation property for the supplier.</summary>
    public Supplier? Supplier { get; set; }

    /// <summary>The ID of the Client PC this asset is associated with (e.g., if it is a component of a PC).</summary>
    public Guid? ClientPcId { get; set; }
    /// <summary>Navigation property for the associated Client PC.</summary>
    public ClientPc? ClientPc { get; set; }

    /// <summary>List of engineering teams responsible for this specific asset.</summary>
    public List<ResponsibleTeam> ResponsibleTeams { get; set; } = new();

    /// <summary>The ID of the parent inventory item (for hierarchical tree structures).</summary>
    public Guid? ParentId { get; set; }
    /// <summary>Navigation property for the parent item.</summary>
    public BaseInventoryItem? Parent { get; set; }
    /// <summary>List of child items nested under this asset.</summary>
    public List<BaseInventoryItem> Children { get; set; } = new();

    /// <summary>Flexible JSONB metadata for storing domain-specific attributes (e.g., CPU, RAM, Version).</summary>
    public JsonDocument? Metadata { get; set; }

    /// <summary>Storage shelf, cabinet, or warehouse bin location (e.g. "Shelf B3").</summary>
    [MaxLength(255)]
    public string? StorageLocation { get; set; }

    /// <summary>Equipment lifecycle status: InStorage (spare part in storage), InMachine (installed on a station), UnderRepair, Decommissioned.</summary>
    [MaxLength(50)]
    public string EquipmentStatus { get; set; } = "InStorage";

    /// <summary>The ID of the Machine / Station this piece of equipment is installed on (if EquipmentStatus == InMachine).</summary>
    public Guid? MachineId { get; set; }
    /// <summary>Navigation property to the Machine where this component is installed.</summary>
    public Machine? Machine { get; set; }

    /// <summary>Quantity on hand for bulk stock items (e.g. 9 parts in stock). Null for discrete one-of serialized assets.</summary>
    public int? StockQuantity { get; set; }

    /// <summary>Minimum stock threshold for reorder alerts.</summary>
    public int? MinStockThreshold { get; set; }

    /// <summary>Whether this is a bulk quantity-tracked stock item (true) or a high-value discrete serialized asset (false).</summary>
    public bool IsStockItem { get; set; } = false;

    /// <summary>Industrial technology domain (e.g. Assembly, Test, SMT, Welding, Dispensing, Fastening, Robotics).</summary>
    [MaxLength(100)]
    public string? Technology { get; set; }

    /// <summary>Helper property for identifying the concrete class name in UI layers.</summary>
    [NotMapped]
    public virtual string ItemType => this.GetType().Name;
}

// --- MACHINE GROUPING ABSTRACTIONS & HIERARCHY ---

/// <summary>
/// Universal interface representing any grouping of machines across the industrial hierarchy:
/// Business Unit > Project > Line > Sub-Line > Machine.
/// </summary>
public interface IMachineGroup
{
    Guid Id { get; set; }
    string Name { get; set; }
    string? Description { get; set; }
    MachineGroupType GroupType { get; }
    Guid? ParentId { get; set; }
    List<Guid> GetMachineIds();
    bool MatchesMachine(Machine machine);
}

// --- CONCRETE IMPLEMENTATIONS (The "Different Models") ---

/// <summary>
/// Represents a Production Station or Operational Machine on the factory floor.
/// First-class Aggregate Root decoupled from inventory items.
/// Implements IMachineGroup to allow direct machine dedication in delegations.
/// </summary>
[Table("stations", Schema = "backend")]
public partial class Machine : BaseInventoryItem, IMachineGroup
{
    [NotMapped]
    public string? PlantId { get; set; }

    /// <summary>A custom, user-defined identifier for the station (e.g., "LINE-A-OP10").</summary>
    [Required, MaxLength(255)]
    public string CustomIdentifier { get; set; } = string.Empty;

    [NotMapped]
    public string? Barcode { get; set; }

    /// <summary>The functional technology / machine type (e.g. "Automatic Optical Inspection", "Gap Filler", "Screwing Station", "Soldering", "Milling", "Fitting", "Pressing", "Manipulator", "Tester Cell", "Painting").</summary>
    public string? MachineType { get; set; }

    /// <summary>Machine operational lifecycle status: active, inactive, maintenance, decommissioned, dismantled.</summary>
    [MaxLength(64)]
    public string Status { get; set; } = "active";

    [NotMapped]
    public string OperationalState { get; set; } = "Idle";

    [NotMapped]
    public string Criticality { get; set; } = "Tier2_Major";

    /// <summary>Documented user justification for decommissioning.</summary>
    public string? DecommissionReason { get; set; }

    /// <summary>Timestamp when the machine was decommissioned.</summary>
    public DateTimeOffset? DecommissionDate { get; set; }

    /// <summary>Disposition of installed parts on decommission (salvage_donor, scrap, retain).</summary>
    [MaxLength(64)]
    public string? DecommissionDisposition { get; set; }

    /// <summary>List of technologies defined by this machine (e.g. ["Welding", "Robotics", "Vision"]).</summary>
    public List<string>? Technologies { get; set; } = new();

    /// <summary>Single Point of Contact (SPOC) name or designated contact for this machine.</summary>
    [MaxLength(255)]
    [Column("spoc")]
    public string? Spoc { get; set; }

    /// <summary>Optional user ID for SPOC.</summary>
    [MaxLength(128)]
    [Column("spoc_user_id")]
    public string? SpocUserId { get; set; }

    /// <summary>Machine Owner (e.g. Lead, Department, or Person who owns this asset).</summary>
    [MaxLength(255)]
    [Column("owner")]
    public string? Owner { get; set; }

    /// <summary>Optional user ID for machine owner.</summary>
    [MaxLength(128)]
    [Column("owner_user_id")]
    public string? OwnerUserId { get; set; }

    /// <summary>Hierarchical group / line envelope identifier (legacy string).</summary>
    public string? GroupId { get; set; }

    /// <summary>Foreign key referencing the parent Sub-Line MachineGroup.</summary>
    [Column("sub_line_group_id")]
    public Guid? SubLineGroupId { get; set; }
    public MachineGroup? SubLineGroup { get; set; }

    /// <summary>Foreign key referencing the parent Line MachineGroup.</summary>
    [Column("line_group_id")]
    public Guid? LineGroupId { get; set; }
    public MachineGroup? LineGroup { get; set; }

    /// <summary>Foreign key referencing the parent Project MachineGroup.</summary>
    [Column("project_group_id")]
    public Guid? ProjectGroupId { get; set; }
    public MachineGroup? ProjectGroup { get; set; }

    /// <summary>Foreign key referencing the parent Business Unit MachineGroup.</summary>
    [Column("business_unit_group_id")]
    public Guid? BusinessUnitGroupId { get; set; }
    public MachineGroup? BusinessUnitGroup { get; set; }

    /// <summary>Hierarchical parent machine ID if this station is part of a larger super-machine, cell, or roundtable.</summary>
    [Column("parent_machine_id")]
    public Guid? ParentMachineId { get; set; }
    public Machine? ParentMachine { get; set; }

    /// <summary>Sub-machines nested under this multi-machine cell or roundtable.</summary>
    public List<Machine> SubMachines { get; set; } = new();

    /// <summary>Station slot index or angular position on a roundtable/dial table (e.g. 1, 2, 3...).</summary>
    [Column("sub_machine_index")]
    public int? SubMachineIndex { get; set; }

    /// <summary>Cell classification: Standard, MultiMachineCell, Roundtable.</summary>
    [MaxLength(64)]
    [Column("cell_type")]
    public string? CellType { get; set; }

    /// <summary>Returns true if this machine is a sub-machine within a cell or roundtable.</summary>
    [NotMapped]
    public bool IsSubMachine => ParentMachineId.HasValue;

    /// <summary>Returns true if this machine is a multi-machine cell or roundtable with child stations.</summary>
    [NotMapped]
    public bool IsCellOrRoundtable => SubMachines.Count > 0 || string.Equals(CellType, "Roundtable", StringComparison.OrdinalIgnoreCase) || string.Equals(CellType, "MultiMachineCell", StringComparison.OrdinalIgnoreCase);

    // IMachineGroup Implementation
    MachineGroupType IMachineGroup.GroupType => MachineGroupType.Machine;
    string? IMachineGroup.Description { get => DisplayName; set => DisplayName = value; }
    Guid? IMachineGroup.ParentId
    {
        get => SubLineGroupId ?? LineGroupId ?? ParentMachineId;
        set => SubLineGroupId = value;
    }
    List<Guid> IMachineGroup.GetMachineIds() => new List<Guid> { Id };
    bool IMachineGroup.MatchesMachine(Machine machine) => machine.Id == Id;

    /// <summary>Foreign key to the active floor plan DXF/SVG.</summary>
    [NotMapped]
    public Guid? FloorPlanId { get; set; }


    /// <summary>The CAD/DXF object handle for mapping this station to a spatial layout.</summary>
    public string? PinnedObjectHandle { get; set; }

    /// <summary>Spatial bounding box and coordinates (JSON).</summary>
    [NotMapped]
    public string? SpatialTransformJson { get; set; }

    /// <summary>Automated system classification tags.</summary>
    [NotMapped]
    public List<string> SystemTags { get; set; } = new();

    /// <summary>User-managed operational tags.</summary>
    [NotMapped]
    public List<string> UserTags { get; set; } = new();

    /// <summary>Dynamic user custom fields (JSON).</summary>
    [NotMapped]
    public string? CustomFieldsJson { get; set; }

    [NotMapped]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [NotMapped]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [NotMapped]
    public DateTimeOffset? CommissionedAt { get; set; }
    [NotMapped]
    public DateTimeOffset? DecommissionedAt { get; set; }
    [NotMapped]
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    [NotMapped]
    public DateTimeOffset? LastAuditAt { get; set; }
    [NotMapped]
    public int SchemaVersion { get; set; } = 2;

    /// <summary>Optional identifier for dedicated preferred technician.</summary>
    public string? PreferredTechnicianId { get; set; }

    /// <summary>Optional name of dedicated preferred technician.</summary>
    public string? PreferredTechnicianName { get; set; }

    /// <summary>List of operational component slots installed on this machine.</summary>
    public List<MachineComponent> Components { get; set; } = new();

    /// <summary>List of Client PCs that control or monitor this station.</summary>
    public List<ClientPc> Controllers { get; set; } = new();

    /// <summary>List of StationController junction entries for graph relationships.</summary>
    public List<StationController> StationControllers { get; set; } = new();

    /// <summary>Multiple delegation recipe bindings governing responsibilities across disciplines.</summary>
    public List<MachineDelegationBinding> DelegationBindings { get; set; } = new();

    /// <summary>Maintenance tickets associated with this machine.</summary>
    public List<MaintenanceTicket> MaintenanceTickets { get; set; } = new();

    /// <summary>Operational journal service logs.</summary>
    public List<MachineServiceLog> ServiceLogs { get; set; } = new();
}

/// <summary>
/// Represents a physical PC/Terminal on the factory floor.
/// Acts as a standalone entity that can control machines and contain inventory items.
/// </summary>
[Table("client_pcs")]
public partial class ClientPc
{
    /// <summary>Unique identifier for the Client PC.</summary>
    public Guid Id { get; set; }
    
    /// <summary>The name given to the Client PC.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The physical MAC address of the network interface.</summary>
    [Required, MaxLength(17)]
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>The last reported IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>A unique hardware-based machine identifier.</summary>
    public string? MachineIdentifier { get; set; }

    /// <summary>The network hostname of the device.</summary>
    public string? Hostname { get; set; } 

    /// <summary>Timestamp of the last successful heartbeat communication.</summary>
    public DateTimeOffset? LastOnline { get; set; }

    /// <summary>The CAD/DXF object handle for mapping this PC to a spatial layout.</summary>
    public string? PinnedObjectHandle { get; set; }

    /// <summary>Optional organization or plant floor assignment for multi-tenant scoping.</summary>
    [MaxLength(128)]
    public string? OrganizationId { get; set; }

    /// <summary>List of production stations controlled by this PC.</summary>
    public List<Machine> ControlledMachines { get; set; } = new();

    /// <summary>List of StationController junction entries for graph relationships.</summary>
    public List<StationController> StationControllers { get; set; } = new();

    /// <summary>List of internal hardware and software components assigned to this PC.</summary>
    public List<BaseInventoryItem> InventoryItems { get; set; } = new();

    /// <summary>List of engineering teams responsible for the maintenance of this PC.</summary>
    public List<ResponsibleTeam> ResponsibleTeams { get; set; } = new();

    /// <summary>Commands queued to be processed by the Agent on this PC.</summary>
    public List<QueuedAgentCommand> PendingCommands { get; set; } = new();

    /// <summary>Audit events reported by the Agent running on this PC.</summary>
    public List<AgentEvent> Events { get; set; } = new();

    /// <summary>Real-time disk space telemetry (JSONB).</summary>
    public DiskSpaceInfo? FreeDiskSpace { get; set; }

    /// <summary>Abstract system metadata (OS, IP, Security Level) that are properties of the node, not physical assets.</summary>
    public JsonDocument? SystemMetadata { get; set; }

    /// <summary>Configuration settings for resource monitoring on this node (JSONB).</summary>
    public ResourceMonitoringConfig? MonitoringConfig { get; set; }

    /// <summary>Aggregated CPU and RAM usage averages (JSONB).</summary>
    public ResourceAverages? ResourceAverages { get; set; }

    /// <summary>Thresholds for system health alerting (JSONB).</summary>
    public AlertingLimits? AlertingLimits { get; set; }

    /// <summary>VLAN identifier to which this host interface belongs.</summary>
    public int? VlanId { get; set; }

    /// <summary>VLAN network name (e.g. VLAN 10 - Production Line).</summary>
    [MaxLength(128)]
    public string? VlanName { get; set; }

    /// <summary>Network subnet CIDR (e.g. 10.10.10.0/24).</summary>
    [MaxLength(64)]
    public string? Subnet { get; set; }

    /// <summary>Active Directory Organizational Unit Distinguished Name path.</summary>
    [MaxLength(500)]
    public string? AdOuPath { get; set; }

    /// <summary>Extracted or templated OU tags (e.g. {"location": "Line 06", "purpose": "Fastening"}).</summary>
    public JsonDocument? OuTags { get; set; }

    /// <summary>Active mTLS client certificate thumbprint bound to this host.</summary>
    [MaxLength(128)]
    public string? CertificateThumbprint { get; set; }

    /// <summary>Name of the PKI certificate profile assigned via AD OU.</summary>
    [MaxLength(128)]
    public string? CertificateProfileName { get; set; }
}

/// <summary>
/// Explicit M:N junction entity linking Production Stations (Machines) with Industrial Controllers (Client PCs).
/// Represents control edges in the industrial graph-relational model.
/// </summary>
[Table("StationControllers")]
public partial class StationController
{
    /// <summary>Unique identifier for the station controller edge.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The ID of the controlled Machine / ProductionStation.</summary>
    public Guid MachineId { get; set; }
    /// <summary>Navigation property for the controlled Machine / ProductionStation.</summary>
    public Machine? Machine { get; set; }

    /// <summary>The ID of the controlling ClientPc / IndustrialController.</summary>
    public Guid ClientPcId { get; set; }
    /// <summary>Navigation property for the controlling ClientPc / IndustrialController.</summary>
    public ClientPc? ClientPc { get; set; }

    /// <summary>The role of this controller (e.g., "Primary", "Secondary", "Gateway", "Safety").</summary>
    public string? Role { get; set; }

    /// <summary>Timestamp when the association was established.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Flexible JSONB metadata for edge properties.</summary>
    public JsonDocument? Metadata { get; set; }
}

/// <summary>
/// Represents a security or system event reported by an Agent.
/// </summary>
[Table("agent_events")]
public class AgentEvent
{
    /// <summary>Unique identifier for the event.</summary>
    public Guid Id { get; set; }
    /// <summary>The ID of the Client PC that reported the event.</summary>
    public Guid ClientPcId { get; set; }
    /// <summary>The source of the event (e.g., "System", "Agent", "Security").</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>The detailed event message.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>The severity level (Information, Warning, Error, Critical).</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>The UTC timestamp when the event occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    /// <summary>Optional organization or plant floor assignment for multi-tenant scoping.</summary>
    [MaxLength(128)]
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Represents a command queued to be sent to a specific Agent.
/// </summary>
[Table("queued_agent_commands")]
public class QueuedAgentCommand
{
    /// <summary>Unique identifier for the command.</summary>
    public Guid Id { get; set; }
    /// <summary>The target Client PC for the command.</summary>
    public Guid ClientPcId { get; set; }
    /// <summary>Optional organization or plant floor assignment for multi-tenant scoping.</summary>
    [MaxLength(128)]
    public string? OrganizationId { get; set; }
    /// <summary>The type of command (e.g., "UPDATE_CONFIG", "RESTART").</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>The command payload (usually a JSON string).</summary>
    public string Payload { get; set; } = string.Empty;
    /// <summary>Optional cryptographic signature for command validation.</summary>
    public string? Signature { get; set; }
    /// <summary>Timestamp when the command was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Whether the command has been retrieved and processed by the Agent.</summary>
    public bool IsProcessed { get; set; }
}



/// <summary>
/// Represents station-level physical equipment (e.g., Valves, Sensors, Motors).
/// </summary>
[Table("hardware_assets")]
public partial class HardwareComponent : BaseInventoryItem
{
    /// <summary>The hardware revision version.</summary>
    public string? Revision { get; set; }
    /// <summary>The specific manufacturer model number.</summary>
    public string? ModelNumber { get; set; }

    /// <summary>List of software/firmware components associated with this hardware.</summary>
    public List<SoftwareAsset> Firmware { get; set; } = new();
}

/// <summary>
/// Represents logical assets such as PLC Programs, Software Licenses, or Firmware.
/// Contains encrypted sensitive properties such as LicenseKey and discriminator for specialized SW assets.
/// </summary>
[Table("software_assets")]
public partial class SoftwareAsset : BaseInventoryItem
{
    /// <summary>Software asset discriminator: Binary, Runtime, Config, Program, Recipe.</summary>
    public SoftwareAssetType SoftwareType { get; set; } = SoftwareAssetType.Binary;

    /// <summary>The software version string or commit SHA.</summary>
    public string? Version { get; set; }

    /// <summary>Author, engineer, or team responsible for the software asset.</summary>
    [MaxLength(255)]
    public string? Author { get; set; }

    /// <summary>Cryptographic SHA-256 digest of the binary, configuration, or recipe payload.</summary>
    [MaxLength(64)]
    public string? ChecksumSha256 { get; set; }

    /// <summary>Deployment lifecycle status: Active, Staged, Deprecated, Draft.</summary>
    [MaxLength(64)]
    public string Status { get; set; } = "Active";

    /// <summary>The encrypted license key or activation code.</summary>
    public string? LicenseKey { get; set; }

    /// <summary>Type-specific payload JSON for binaries, runtimes, configs, programs, and recipes.</summary>
    public string? SpecificDataJson { get; set; }
}

/// <summary>
/// Subclass / Alias entity for SoftwareAsset for backward compatibility.
/// </summary>
[Table("software_components")]
public partial class SoftwareComponent : SoftwareAsset
{
}

/// <summary>
/// Represents internal components of a Client PC (e.g., RAM sticks, CPUs, Storage Drives).
/// </summary>
[Table("pc_hardware")]
public partial class PcHardware : BaseInventoryItem
{
    /// <summary>The capacity of the component (e.g., "16GB", "1TB").</summary>
    public string? Capacity { get; set; }
    /// <summary>The specific type/standard (e.g., "DDR4", "NVMe").</summary>
    public string? Type { get; set; }
}

/// <summary>
/// Represents a graph edge / connectivity link between two equipment / inventory items.
/// Maps physical or logical network interconnectivity (e.g., OPC UA, EtherNet/IP, PROFINET, Modbus).
/// </summary>
[Table("equipment_interconnects")]
public partial class EquipmentInterconnect
{
    /// <summary>Unique identifier for the interconnect link.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The ID of the source inventory item / equipment.</summary>
    public Guid SourceEquipmentId { get; set; }
    /// <summary>Navigation property for the source equipment.</summary>
    public BaseInventoryItem? SourceEquipment { get; set; }

    /// <summary>The ID of the target inventory item / equipment.</summary>
    public Guid TargetEquipmentId { get; set; }
    /// <summary>Navigation property for the target equipment.</summary>
    public BaseInventoryItem? TargetEquipment { get; set; }

    /// <summary>The interconnection type (e.g., "OPC UA", "PROFINET", "Modbus", "EtherNet/IP", "Serial").</summary>
    public string InterconnectType { get; set; } = "Ethernet";

    /// <summary>Optional connection string or endpoint URL.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Physical port, tag path, or IP address information.</summary>
    public string? PortOrAddress { get; set; }

    /// <summary>Communication protocol identifier.</summary>
    public string? Protocol { get; set; }

    /// <summary>Operational status of the connection (e.g., "Active", "Inactive", "Degraded").</summary>
    public string? Status { get; set; }

    /// <summary>Flexible JSONB metadata for custom edge properties.</summary>
    public JsonDocument? Metadata { get; set; }

    /// <summary>Timestamp when the interconnect was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a maintenance ticket recorded against equipment, machines, or client PCs.
/// Part of the graph-relational incident management system.
/// </summary>
[Table("maintenance_tickets")]
public partial class MaintenanceTicket
{
    /// <summary>Unique identifier for the maintenance ticket.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Short summary title of the issue or maintenance requirement.</summary>
    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description of the issue or ticket work.</summary>
    public string? Description { get; set; }

    /// <summary>Current status (strictly 5 canonical statuses: "Open", "InProgress", "Pending", "Resolved", "Closed").</summary>
    public string Status { get; set; } = "Open";

    /// <summary>Sub-status reason for tickets in Pending status.</summary>
    public PendingReason PendingReason { get; set; } = PendingReason.None;

    /// <summary>Contextual detail for pending status (parts required, external party, action needed).</summary>
    public string? PendingDetails { get; set; }

    /// <summary>Orthogonal escalation flag; a ticket can be escalated in any status.</summary>
    public bool IsEscalated { get; set; } = false;

    /// <summary>Reason for ticket escalation.</summary>
    public string? EscalationReason { get; set; }

    /// <summary>Timestamp when ticket was escalated.</summary>
    public DateTimeOffset? EscalatedAt { get; set; }

    /// <summary>User who initiated the escalation.</summary>
    public string? EscalatedBy { get; set; }

    /// <summary>Timestamp when the escalation was closed/cleared.</summary>
    public DateTimeOffset? EscalationClosedAt { get; set; }

    /// <summary>User who closed/cleared the escalation.</summary>
    public string? EscalationClosedBy { get; set; }

    /// <summary>Priority level (e.g., "Low", "Medium", "High", "Critical").</summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>Flag indicating whether this incident causes plant line stoppage.</summary>
    public bool IsLineStop { get; set; } = false;

    /// <summary>Duration in minutes of line stoppage caused by this ticket.</summary>
    public double? LineStopDurationMinutes { get; set; }

    /// <summary>Responsible department (Assy, SMT, Test, IT, MES, SAP, ProcessEngineering, ProductOwner, etc.).</summary>
    [MaxLength(128)]
    public string? ResponsibleDepartment { get; set; }

    /// <summary>Originator classification (MachineAutomatic, ScheduledMaintenance, ManualUser).</summary>
    [MaxLength(64)]
    public string OriginatorType { get; set; } = "ManualUser";

    /// <summary>Issue classification (Transient, Maintenance, Improvement, Other).</summary>
    [MaxLength(64)]
    public string IssueType { get; set; } = "Maintenance";

    /// <summary>External system operator identifier or badge ID.</summary>
    [MaxLength(128)]
    public string? ExternalOperatorId { get; set; }

    /// <summary>External system operator display name.</summary>
    [MaxLength(255)]
    public string? ExternalOperatorName { get; set; }

    /// <summary>Timestamp when work started on the ticket.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Timestamp when technician scanned the ticket QR code.</summary>
    public DateTimeOffset? QrScannedAt { get; set; }

    /// <summary>Calculated reaction time in minutes from ticket creation to start / QR pickup.</summary>
    public double? ReactionTimeMinutes { get; set; }

    /// <summary>Technician who reserved the ticket while in Open status.</summary>
    [MaxLength(128)]
    public string? ReservedBy { get; set; }

    /// <summary>Escalation target (DedicatedEngineer, Management, etc.).</summary>
    [MaxLength(128)]
    public string? EscalationTarget { get; set; }

    /// <summary>Handover sub-state for escalation (Notification, HandOff, ParallelWork).</summary>
    public EscalationHandoverState EscalationHandoverState { get; set; } = EscalationHandoverState.None;

    /// <summary>Snapshot of real-time telemetry data-points recorded at ticket raise.</summary>
    public string? TelemetrySnapshot { get; set; }

    /// <summary>Comma-separated or JSON list of grouping tags.</summary>
    public string? Tags { get; set; }

    /// <summary>JSON audit log of ticket modifications allowing historical corrections.</summary>
    public string? ChangeHistory { get; set; }

    /// <summary>Optional organization or plant floor assignment for multi-tenant scoping.</summary>
    [MaxLength(128)]
    public string? OrganizationId { get; set; }

    /// <summary>Optional reference to associated inventory item.</summary>
    public Guid? EquipmentId { get; set; }
    /// <summary>Navigation property for associated equipment.</summary>
    public BaseInventoryItem? Equipment { get; set; }

    [NotMapped]
    public Guid? AssetId { get => EquipmentId; set => EquipmentId = value; }
    [NotMapped]
    public BaseInventoryItem? Asset { get => Equipment; set => Equipment = value; }

    /// <summary>Optional reference to associated Client PC.</summary>
    public Guid? ClientPcId { get; set; }
    /// <summary>Navigation property for associated Client PC.</summary>
    public ClientPc? ClientPc { get; set; }

    /// <summary>Optional reference to associated Machine / Production Station.</summary>
    public Guid? MachineId { get; set; }
    /// <summary>Navigation property for associated Machine.</summary>
    public Machine? Machine { get; set; }

    /// <summary>Optional reference to specific target MachineComponent slot.</summary>
    public Guid? TargetComponentId { get; set; }
    /// <summary>Navigation property for associated MachineComponent slot.</summary>
    public MachineComponent? TargetComponent { get; set; }

    /// <summary>Device tag (BMK / identifier) of the target component at time of incident.</summary>
    [MaxLength(128)]
    public string? TargetComponentBmk { get; set; }

    /// <summary>Alarm code from PLC or industrial controller if machine-raised.</summary>
    [MaxLength(128)]
    public string? AlarmCode { get; set; }

    /// <summary>PLC or industrial controller error ID.</summary>
    [MaxLength(128)]
    public string? PlcErrorId { get; set; }

    /// <summary>Controller source address or hostname (e.g. TwinCAT AMS NetId, OPC UA endpoint).</summary>
    [MaxLength(255)]
    public string? ControllerSource { get; set; }

    /// <summary>JSON array of affected component GUIDs if multiple sub-components were involved.</summary>
    public string? AffectedComponentIdsJson { get; set; }

    /// <summary>JSON array of strongly-typed TicketTelemetryProbe data captures.</summary>
    public string? TelemetryProbesJson { get; set; }

    /// <summary>User or team assigned to resolve the ticket.</summary>
    public string? AssignedTo { get; set; }

    /// <summary>User who created the ticket.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Timestamp when ticket was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp when ticket was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Flexible JSONB metadata for extra ticket context.</summary>
    public JsonDocument? Metadata { get; set; }

    /// <summary>Collection of comments associated with this ticket.</summary>
    public List<TicketComment> Comments { get; set; } = new();

    /// <summary>Collection of file attachments associated with this ticket.</summary>
    public List<TicketAttachment> Attachments { get; set; } = new();
}

/// <summary>
/// Represents a comment posted on a maintenance ticket.
/// </summary>
[Table("ticket_comments")]
public partial class TicketComment
{
    /// <summary>Unique identifier for the comment.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The maintenance ticket ID.</summary>
    public Guid MaintenanceTicketId { get; set; }
    /// <summary>Navigation property for the maintenance ticket.</summary>
    public MaintenanceTicket? MaintenanceTicket { get; set; }

    /// <summary>Author of the comment.</summary>
    [Required, MaxLength(255)]
    public string Author { get; set; } = string.Empty;

    /// <summary>Markdown or plain text content of the comment.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Timestamp when the comment was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a file attachment on a maintenance ticket.
/// </summary>
[Table("ticket_attachments")]
public partial class TicketAttachment
{
    /// <summary>Unique identifier for the attachment.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The maintenance ticket ID.</summary>
    public Guid MaintenanceTicketId { get; set; }
    /// <summary>Navigation property for the maintenance ticket.</summary>
    public MaintenanceTicket? MaintenanceTicket { get; set; }

    /// <summary>Optional associated comment ID if attachment belongs to a specific comment.</summary>
    public Guid? CommentId { get; set; }

    /// <summary>Original filename of the attachment.</summary>
    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Internal storage key or file path.</summary>
    [Required]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>MIME content type (e.g. "image/png", "application/pdf").</summary>
    public string? ContentType { get; set; }

    /// <summary>Size of the file in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Timestamp when the file was uploaded.</summary>
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

// --- REFERENCE ENTITIES ---

/// <summary>
/// Represents an Original Equipment Manufacturer (OEM).
/// </summary>
public partial class Manufacturer
{
    /// <summary>Unique identifier for the manufacturer.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the company.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>Official website URL.</summary>
    public string? Website { get; set; }
    /// <summary>Contact information for technical support.</summary>
    public string? SupportContact { get; set; }
}

[Table("suppliers", Schema = "backend")]
public partial class Supplier
{
    /// <summary>Unique identifier for the supplier.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>The name of the supplier company.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>Official website URL.</summary>
    public string? Website { get; set; }
    /// <summary>Primary contact person.</summary>
    public string? ContactPerson { get; set; }
    /// <summary>Contact email address.</summary>
    public string? Email { get; set; }
    /// <summary>Direct telephone or support line.</summary>
    [MaxLength(64)]
    public string? PhoneNumber { get; set; }
    /// <summary>Average component delivery lead time in calendar days.</summary>
    public int LeadTimeDays { get; set; } = 7;
    /// <summary>Physical headquarters or distribution warehouse address.</summary>
    public string? Address { get; set; }
    /// <summary>Procurement notes or vendor tier comments.</summary>
    public string? Notes { get; set; }
    /// <summary>Record creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Relational tag definition to avoid slow JSONB table scans.
/// </summary>
[Table("known_tags", Schema = "backend")]
public class KnownTag
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(128)]
    public string Key { get; set; } = string.Empty;
    [Required, MaxLength(255)]
    public string Value { get; set; } = string.Empty;
    [MaxLength(64)]
    public string Category { get; set; } = "custom";
    public int UsageCount { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Secondary relational junction table linking inventory items to known tags.
/// </summary>
[Table("inventory_item_tags", Schema = "backend")]
public class InventoryItemTag
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public Guid TagId { get; set; }
    [Required, MaxLength(128)]
    public string TagKey { get; set; } = string.Empty;
    [Required, MaxLength(255)]
    public string TagValue { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public KnownTag? Tag { get; set; }
}

// --- LAYOUT & VISUALS ---

/// <summary>
/// Represents a physical factory floor plan, stored as a DXF/SVG.
/// </summary>
public partial class FloorPlan
{
    /// <summary>Unique identifier for the floor plan.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the floor or building.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>The SVG content for rendering the layout in the browser.</summary>
    [Required]
    public string SvgContent { get; set; } = string.Empty;
    /// <summary>List of predefined anchor points within the layout (JSONB).</summary>
    public List<FloorPlanAnchor> Anchors { get; set; } = new();
    /// <summary>Timestamp when the plan was uploaded.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a coordinate-based anchor point on a floor plan.
/// </summary>
public partial class FloorPlanAnchor
{
    /// <summary>The CAD object handle associated with this anchor.</summary>
    public string Handle { get; set; } = string.Empty;
    /// <summary>A descriptive name for the anchor point.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Horizontal coordinate.</summary>
    public double? X { get; set; }
    /// <summary>Vertical coordinate.</summary>
    public double? Y { get; set; }
}

// --- SYSTEM POCOs (JSONB Mapping) ---

/// <summary>
/// Telemetry data for disk space usage on a node.
/// </summary>
public class DiskSpaceInfo
{
    /// <summary>Total free space across all drives in GB.</summary>
    public double TotalFreeGB { get; set; }
    /// <summary>Available space on the OS (primary) drive in GB.</summary>
    public double OsDriveFreeGB { get; set; }
    /// <summary>Dictionary mapping drive labels to their free space in GB.</summary>
    public Dictionary<string, double> Drives { get; set; } = new();
}

/// <summary>
/// Configuration for the monitoring agent resource collection behavior.
/// </summary>
public class ResourceMonitoringConfig
{
    /// <summary>How often the agent should sample resource usage (in seconds).</summary>
    public int SamplingIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Aggregated resource usage telemetry.
/// </summary>
public class ResourceAverages
{
    /// <summary>Average CPU usage percentage over the last sampling period.</summary>
    public double CpuUsageAverage { get; set; }
    /// <summary>Average RAM usage percentage over the last sampling period.</summary>
    public double RamUsageAverage { get; set; }
}

/// <summary>
/// Thresholds for triggering system health alerts.
/// </summary>
public class AlertingLimits
{
    /// <summary>CPU usage percentage threshold for triggering an alert.</summary>
    public double CpuThreshold { get; set; } = 90.0;
}

// --- AUTH (Better-Auth) ---

/// <summary>
/// Represents a user role with specific access privileges.
/// </summary>
public partial class UserRole
{
    /// <summary>Unique identifier for the role.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the role (e.g., "admin", "operator").</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>JSON document containing the fine-grained permissions for this role.</summary>
    public JsonDocument? Privileges { get; set; }
}

/// <summary>
/// Represents a user within the system, managed by the Better-Auth framework.
/// </summary>
[Table("user", Schema = "auth")]
public class AuthUser
{
    /// <summary>Unique identifier for the user (Better-Auth ID).</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>Full name of the user.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Email address for authentication and notifications.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>The assigned system-wide role.</summary>
    public string? Role { get; set; }
    /// <summary>
    /// Explicit per-user permission grants, comma-separated (e.g. "organizations.manage"). Written by
    /// the BFF (Drizzle migration 0007 adds the column); the backend turns them into permission claims.
    /// </summary>
    public string? Permissions { get; set; }
    /// <summary>List of organizations this user is a member of.</summary>
    public List<AuthMember> Members { get; set; } = new();
}

/// <summary>
/// Represents an active authentication session.
/// </summary>
[Table("session", Schema = "auth")]
public class AuthSession
{
    /// <summary>Unique identifier for the session.</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>The session token used for authentication.</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>Timestamp when the session will expire.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>The ID of the user associated with this session.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Navigation property for the user.</summary>
    public AuthUser User { get; set; } = null!;
    /// <summary>The organization currently selected in this session.</summary>
    public string? ActiveOrganizationId { get; set; }
}

/// <summary>
/// Represents a tenant/organization unit within the system.
/// </summary>
[Table("organization", Schema = "auth")]
public class AuthOrganization
{
    /// <summary>Unique identifier for the organization.</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>The name of the organization.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>List of members belonging to this organization.</summary>
    public List<AuthMember> Members { get; set; } = new();
}

/// <summary>
/// Represents the membership relationship between a user and an organization.
/// </summary>
[Table("member", Schema = "auth")]
public class AuthMember
{
    /// <summary>Unique identifier for the membership record.</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>The ID of the organization.</summary>
    public string OrganizationId { get; set; } = string.Empty;
    /// <summary>Navigation property for the organization.</summary>
    public AuthOrganization Organization { get; set; } = null!;
    /// <summary>The ID of the user.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Navigation property for the user.</summary>
    public AuthUser User { get; set; } = null!;
}

// --- SYSTEM GOVERNANCE, RBAC & ENTERPRISE IDENTITY ENTITIES ---

/// <summary>
/// Maps Active Directory and Microsoft Entra ID Security Groups to Heimdall roles and tenant boundaries.
/// </summary>
[Table("security_group_mappings")]
public class SecurityGroupMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>Identity provider identifier (EntraID, ActiveDirectory, LDAP, OIDC).</summary>
    [Required, MaxLength(100)]
    public string IdentityProvider { get; set; } = "EntraID";
    
    /// <summary>Group Object ID, SID, or Distinguished Name.</summary>
    [Required, MaxLength(255)]
    public string GroupIdentifier { get; set; } = string.Empty;
    
    /// <summary>Human-readable display name (e.g., "OT Controls Engineers").</summary>
    [Required, MaxLength(255)]
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>Mapped Heimdall role (admin, system_admin, lead_engineer, engineer, technician, operator).</summary>
    [Required, MaxLength(100)]
    public string MappedRole { get; set; } = "engineer";
    
    /// <summary>Optional organization/plant floor assignment.</summary>
    public string? OrganizationId { get; set; }
    
    /// <summary>Whether this mapping rule is active.</summary>
    public bool IsEnabled { get; set; } = true;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// System-wide master settings and governance parameters.
/// </summary>
[Table("system_settings")]
public class SystemSetting
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty; // e.g. "AgentMasterTemplate", "AuthPolicy", "OpcUaConfig"
    
    [Required]
    public string ValueJson { get; set; } = "{}";
    
    [MaxLength(50)]
    public string Category { get; set; } = "General"; // Auth, Integrations, Certificates, AgentMaster
    
    [MaxLength(100)]
    public string UpdatedBy { get; set; } = "system";
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Registry of issued X.509 client certificates for mTLS gRPC telemetry channels.
/// </summary>
[Table("client_certificates")]
public class ClientCertificateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ClientPcId { get; set; }
    
    [Required, MaxLength(255)]
    public string CommonName { get; set; } = string.Empty;
    
    [Required, MaxLength(128)]
    public string Thumbprint { get; set; } = string.Empty;
    
    public DateTimeOffset ValidFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ValidTo { get; set; } = DateTimeOffset.UtcNow.AddYears(1);
    
    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Revoked, Expired
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(500)]
    public string? AdOuPath { get; set; }

    [MaxLength(128)]
    public string? ProfileName { get; set; }

    public bool IsRootCa { get; set; } = false;

    public string? RawPem { get; set; }

    [MaxLength(255)]
    public string? Issuer { get; set; }

    [MaxLength(64)]
    public string? KeyAlgorithm { get; set; }

    [MaxLength(128)]
    public string? SerialNumber { get; set; }
}

/// <summary>
/// Rule governing automated certificate issuance and profile assignment based on Active Directory OU paths.
/// </summary>
[Table("ou_certificate_rules")]
public class OuCertificateRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(500)]
    public string OuPath { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string ProfileName { get; set; } = string.Empty;

    public int ValidityYears { get; set; } = 2;

    public bool AutoEnroll { get; set; } = true;

    [MaxLength(50)]
    public string KeyAlgorithm { get; set; } = "RSA-2048";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Tracks database schema milestones and data definition versions.
/// </summary>
[Table("schema_version_manifest")]
public class SchemaVersionManifest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(50)]
    public string SchemaVersion { get; set; } = "1.0.0";
    
    [Required, MaxLength(255)]
    public string MigrationName { get; set; } = string.Empty;
    
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Immutable audit log capturing user mutations, administrative actions, and configuration changes.
/// Satisfies TISAX ISA 5.1 and NIS2 Article 21 auditability requirements.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>User ID or system identity that performed the action.</summary>
    [Required, MaxLength(128)]
    public string UserId { get; set; } = "system";

    /// <summary>Username or display name of the actor.</summary>
    [MaxLength(255)]
    public string? UserName { get; set; }

    /// <summary>Action performed (e.g., "CREATE", "UPDATE", "DELETE", "CONFIG_CHANGE", "ROLE_ASSIGN").</summary>
    [Required, MaxLength(64)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Target entity type or domain module (e.g., "InventoryItem", "SystemSetting", "ClientCertificate").</summary>
    [Required, MaxLength(128)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Identifier of the targeted entity.</summary>
    [MaxLength(128)]
    public string? EntityId { get; set; }

    /// <summary>Previous state or payload diff before mutation (JSON).</summary>
    public string? OldValuesJson { get; set; }

    /// <summary>New state or payload after mutation (JSON).</summary>
    public string? NewValuesJson { get; set; }

    /// <summary>Client IP address or network origin.</summary>
    [MaxLength(64)]
    public string? IpAddress { get; set; }

    /// <summary>Optional organization or plant floor boundary.</summary>
    [MaxLength(128)]
    public string? OrganizationId { get; set; }

    /// <summary>UTC timestamp when the change occurred.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dead-letter quarantine storage for malformed telemetry packets, unparseable payloads, and schema validation failures.
/// Satisfies AGENTS.MD Guideline 36.
/// </summary>
[Table("malformed_telemetry_quarantine")]
public class MalformedTelemetryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Source identifier (Hostname, MAC address, or IP).</summary>
    [MaxLength(255)]
    public string? SourceIdentifier { get; set; }

    /// <summary>Protocol or ingestion channel (e.g., "gRPC_SystemInfo", "SignalR_Telemetry", "OPC_UA", "Modbus").</summary>
    [Required, MaxLength(64)]
    public string IngestionChannel { get; set; } = string.Empty;

    /// <summary>Error reason or parsing failure message.</summary>
    [Required]
    public string ErrorReason { get; set; } = string.Empty;

    /// <summary>Raw unparsed or corrupt payload string.</summary>
    [Required]
    public string RawPayload { get; set; } = string.Empty;

    /// <summary>Optional organization ID if identifiable from metadata.</summary>
    [MaxLength(128)]
    public string? OrganizationId { get; set; }

    /// <summary>UTC timestamp when the malformed payload was received and quarantined.</summary>
    public DateTimeOffset QuarantinedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Organizational Unit governance rule approved by IT Administrators.
/// Governs whether an Active Directory / Entra OU is approved for read-only discovery or read-write ingestion in Heimdall.
/// </summary>
[Table("ad_ou_governances")]
public class AdOuGovernance
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("ou_path")]
    [MaxLength(256)]
    public string OuPath { get; set; } = string.Empty;

    [Required]
    [Column("access_level")]
    [MaxLength(32)]
    public string AccessLevel { get; set; } = "read_only"; // "read_write", "read_only", "unapproved"

    [Column("is_approved")]
    public bool IsApproved { get; set; } = false;

    [Column("approved_by")]
    [MaxLength(128)]
    public string? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTimeOffset? ApprovedAt { get; set; }

    [Column("notes")]
    [MaxLength(512)]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents an immutable point-in-time diagnostic snapshot of an industrial controller / endpoint.
/// Captures complete resource telemetry, hardware component states, active processes, and event logs.
/// Includes a SHA-256 cryptographic digest for tamper-detection.
/// </summary>
[Table("diagnostic_snapshots")]
public class DiagnosticSnapshot
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("client_pc_id")]
    public Guid ClientPcId { get; set; }

    public ClientPc? ClientPc { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("hostname")]
    public string Hostname { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("machine_identifier")]
    public string? MachineIdentifier { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("captured_by_user_id")]
    public string CapturedByUserId { get; set; } = "system";

    [MaxLength(255)]
    [Column("captured_by_user_name")]
    public string? CapturedByUserName { get; set; }

    [Column("captured_at_utc")]
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Raw JSON payload capturing complete diagnostic state.</summary>
    [Required]
    [Column("snapshot_payload_json")]
    public string SnapshotPayloadJson { get; set; } = string.Empty;

    /// <summary>SHA-256 cryptographic digest of SnapshotPayloadJson ensuring tamper detection.</summary>
    [Required]
    [MaxLength(64)]
    [Column("payload_hash_sha256")]
    public string PayloadHashSha256 { get; set; } = string.Empty;

    [MaxLength(128)]
    [Column("organization_id")]
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Machine Group entity representing line assignments, functional groups, and plant clusters.
/// Implements IMachineGroup across the industrial hierarchy:
/// Business Unit > Project > Line > Sub-Line > Machine.
/// </summary>
[Table("machine_groups", Schema = "backend")]
public class MachineGroup : IMachineGroup
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("parent_id")]
    public Guid? ParentId { get; set; }

    public MachineGroup? Parent { get; set; }
    public List<MachineGroup> Children { get; set; } = new();

    [MaxLength(64)]
    [Column("color")]
    public string? Color { get; set; }

    [MaxLength(64)]
    [Column("icon")]
    public string? Icon { get; set; }

    [MaxLength(128)]
    [Column("lead_engineer_id")]
    public string? LeadEngineerId { get; set; }

    [MaxLength(255)]
    [Column("lead_engineer_name")]
    public string? LeadEngineerName { get; set; }

    /// <summary>JSON array of machine IDs belonging to this group</summary>
    [Column("machine_ids_json")]
    public string MachineIdsJson { get; set; } = "[]";

    /// <summary>JSON array of machine types</summary>
    [Column("machine_types_json")]
    public string? MachineTypesJson { get; set; }

    /// <summary>Type of machine grouping: BusinessUnit, Project, Line, SubLine, Machine, Technological, Customer, Manual.</summary>
    [Column("group_type")]
    public MachineGroupType GroupType { get; set; } = MachineGroupType.Manual;

    /// <summary>Interface contract identifier for Line interfaces (e.g. "CustomerLineInterface", "SubLineInterface").</summary>
    [MaxLength(255)]
    [Column("interface_contract")]
    public string? InterfaceContract { get; set; }

    /// <summary>Target technology key for dynamic technological groups (e.g. "Laser", "Welding", "Robotics").</summary>
    [MaxLength(128)]
    [Column("technology_key")]
    public string? TechnologyKey { get; set; }

    /// <summary>
    /// Organization (Better-Auth <c>auth.organization.id</c>) that owns the group and may change it.
    /// Null: platform-managed (business units, projects, technology fleets spanning organizations).
    /// Scopes writes only - every signed-in user reads every group (no query filter).
    /// </summary>
    [MaxLength(128)]
    [Column("organization_id")]
    public string? OrganizationId { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Explicit memberships for manual machine groups.</summary>
    public List<MachineGroupMembership> Memberships { get; set; } = new();

    /// <summary>Machines directly attached to this line group.</summary>
    [NotMapped]
    public List<Machine> Machines { get; set; } = new();

    public virtual List<Guid> GetMachineIds()
    {
        var result = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(MachineIdsJson))
        {
            try
            {
                result = JsonSerializer.Deserialize<List<Guid>>(MachineIdsJson) ?? new();
            }
            catch { }
        }
        if (Memberships != null && Memberships.Count > 0)
        {
            foreach (var m in Memberships)
            {
                if (!result.Contains(m.MachineId))
                    result.Add(m.MachineId);
            }
        }
        return result;
    }

    public virtual bool MatchesMachine(Machine machine)
    {
        if (GetMachineIds().Contains(machine.Id)) return true;
        if (machine.SubLineGroupId == Id) return true;
        if (machine.LineGroupId == Id) return true;
        if (machine.ProjectGroupId == Id) return true;
        if (machine.BusinessUnitGroupId == Id) return true;
        if (GroupType == MachineGroupType.Technological && !string.IsNullOrWhiteSpace(TechnologyKey))
        {
            if (string.Equals(machine.MachineType, TechnologyKey, StringComparison.OrdinalIgnoreCase))
                return true;
            if (machine.Technologies != null && machine.Technologies.Any(t => string.Equals(t, TechnologyKey, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Level 1 in hierarchy: Business Unit grouping.
/// Root envelope for a plant operational or market business unit (e.g. Battery Systems, Powertrain, Electronics).
/// Implements IMachineGroup.
/// </summary>
[NotMapped]
public class BusinessUnitMachineGroup : MachineGroup, IMachineGroup
{
    public BusinessUnitMachineGroup()
    {
        GroupType = MachineGroupType.BusinessUnit;
    }

    public string? BusinessUnitCode { get; set; }
    public string? HeadOfBusinessUnit { get; set; }
}

/// <summary>
/// Level 2 in hierarchy: Project grouping.
/// Extends BusinessUnitMachineGroup (extends the previous grouping type).
/// Represents a customer vehicle program or product platform (e.g. Audi PPE, BMW Gen6).
/// </summary>
[NotMapped]
public class ProjectMachineGroup : BusinessUnitMachineGroup
{
    public ProjectMachineGroup()
    {
        GroupType = MachineGroupType.Project;
    }

    public Guid? BusinessUnitId
    {
        get => ParentId;
        set => ParentId = value;
    }

    public string? ProjectCode { get; set; }
    public string? CustomerName { get; set; }
    public DateTimeOffset? SopDate { get; set; }
}

/// <summary>
/// Level 3 in hierarchy: Line grouping.
/// Extends ProjectMachineGroup (extends the previous grouping type).
/// Represents an automated manufacturing assembly line (e.g. Line 06 Module Assembly).
/// </summary>
[NotMapped]
public class LineMachineGroup : ProjectMachineGroup
{
    public LineMachineGroup()
    {
        GroupType = MachineGroupType.Line;
    }

    public Guid? ProjectId
    {
        get => ParentId;
        set => ParentId = value;
    }

    public string? LineCode { get; set; }
    public double? TargetTaktTimeSeconds { get; set; }
}

/// <summary>
/// Level 4 in hierarchy: Sub-Line grouping.
/// Extends LineMachineGroup (extends the previous grouping type).
/// Represents a production stage, automation segment, or cell cluster inside a Line (e.g. Sub-Line A - Cell Infeed & Stacking).
/// </summary>
[NotMapped]
public class SubLineMachineGroup : LineMachineGroup
{
    public SubLineMachineGroup()
    {
        GroupType = MachineGroupType.SubLine;
    }

    public Guid? LineId
    {
        get => ParentId;
        set => ParentId = value;
    }

    public string? SubLineCode { get; set; }
    public int StageSequence { get; set; } = 1;
}

/// <summary>
/// Level 5 in hierarchy: Machine Unit grouping node.
/// Extends SubLineMachineGroup (extends the previous grouping type).
/// Represents a dedicated machine or workstation node inside a Sub-Line.
/// </summary>
[NotMapped]
public class MachineUnitGroup : SubLineMachineGroup
{
    public MachineUnitGroup()
    {
        GroupType = MachineGroupType.Machine;
    }

    public Guid? SubLineId
    {
        get => ParentId;
        set => ParentId = value;
    }

    public Guid? TargetMachineId { get; set; }
}

/// <summary>
/// Technician assignment and routing rule entity.
/// Replaces in-memory shadow store.
/// </summary>
[Table("technician_rules", Schema = "backend")]
public class TechnicianRule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("technician_id")]
    public string TechnicianId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("technician_name")]
    public string TechnicianName { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("technician_email")]
    public string? TechnicianEmail { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("scope_type")]
    public string ScopeType { get; set; } = "technology";

    [Required]
    [MaxLength(128)]
    [Column("target_id")]
    public string TargetId { get; set; } = string.Empty;

    [MaxLength(128)]
    [Column("category_filter")]
    public string? CategoryFilter { get; set; }

    [MaxLength(128)]
    [Column("backup_technician_id")]
    public string? BackupTechnicianId { get; set; }

    [MaxLength(255)]
    [Column("backup_technician_name")]
    public string? BackupTechnicianName { get; set; }

    [MaxLength(64)]
    [Column("assigned_by_role")]
    public string? AssignedByRole { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Shift absence record for technician availability tracking.
/// Replaces in-memory shadow store.
/// </summary>
[Table("shift_absences", Schema = "backend")]
public class ShiftAbsence
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(128)]
    [Column("technician_id")]
    public string TechnicianId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("technician_name")]
    public string TechnicianName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("reason")]
    public string Reason { get; set; } = "Sick";

    [Column("start_date")]
    public DateTimeOffset StartDate { get; set; }

    [Column("end_date")]
    public DateTimeOffset EndDate { get; set; }

    [MaxLength(255)]
    [Column("marked_by")]
    public string MarkedBy { get; set; } = "System";

    [MaxLength(128)]
    [Column("backup_technician_id")]
    public string? BackupTechnicianId { get; set; }

    [MaxLength(255)]
    [Column("backup_technician_name")]
    public string? BackupTechnicianName { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;
}
