using System;
using System.Collections.Generic;

namespace App.Backend.Api.Dtos;

/// <summary>
/// Data Transfer Object representing a Client PC with its relationships and inventory.
/// Flattened to optimize payload size for high-concurrency spatial views.
/// </summary>
public class ClientPcDto
{
    /// <summary>Unique identifier for the PC.</summary>
    public Guid Id { get; set; }
    /// <summary>Internal name of the device.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Friendly display name.</summary>
    public string? DisplayName { get; set; }
    /// <summary>Owning organization unit.</summary>
    public string? OrganizationId { get; set; }
    /// <summary>Physical MAC address.</summary>
    public string MacAddress { get; set; } = string.Empty;
    /// <summary>Network hostname.</summary>
    public string? Hostname { get; set; }
    /// <summary>Hardware machine identifier.</summary>
    public string? MachineIdentifier { get; set; }
    /// <summary>CAD object handle for spatial mapping.</summary>
    public string? PinnedObjectHandle { get; set; }
    /// <summary>Timestamp of the last heartbeat.</summary>
    public DateTimeOffset? LastSeen { get; set; }
    /// <summary>VLAN identifier to which this host interface belongs.</summary>
    public int? VlanId { get; set; }
    /// <summary>VLAN network name.</summary>
    public string? VlanName { get; set; }
    /// <summary>Network subnet CIDR.</summary>
    public string? Subnet { get; set; }
    /// <summary>Active Directory Organizational Unit Distinguished Name path.</summary>
    public string? AdOuPath { get; set; }
    /// <summary>Extracted or templated OU tags (JSON string or object).</summary>
    public string? OuTagsJson { get; set; }
    /// <summary>Active mTLS client certificate thumbprint bound to this host.</summary>
    public string? CertificateThumbprint { get; set; }
    /// <summary>Name of the PKI certificate profile assigned via AD OU.</summary>
    public string? CertificateProfileName { get; set; }
    /// <summary>Summary of production stations controlled by this PC.</summary>
    public List<MachineSummaryDto> Machines { get; set; } = new();
    /// <summary>List of teams responsible for this node.</summary>
    public List<TeamSummaryDto> ResponsibleTeams { get; set; } = new();
    /// <summary>Tree of hardware and software components assigned to this PC.</summary>
    public List<InventoryItemDto> InventoryItems { get; set; } = new();
    /// <summary>Real-time disk space telemetry.</summary>
    public App.Shared.Entities.DiskSpaceInfo? FreeDiskSpace { get; set; }
    /// <summary>Abstract system metadata (OS, IP, etc.).</summary>
    public System.Text.Json.JsonDocument? SystemMetadata { get; set; }
    /// <summary>Aggregated CPU and RAM usage averages.</summary>
    public App.Shared.Entities.ResourceAverages? ResourceAverages { get; set; }
}

/// <summary>
/// Minimalist representation of a Production Station for nested summary views.
/// </summary>
public class MachineSummaryDto
{
    /// <summary>Unique identifier for the station.</summary>
    public Guid Id { get; set; }
    /// <summary>Custom identifier (e.g., OP10).</summary>
    public string CustomIdentifier { get; set; } = string.Empty;
    /// <summary>CAD object handle for spatial mapping.</summary>
    public string? PinnedObjectHandle { get; set; }
    /// <summary>The name of the station.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// Data Transfer Object representing a Production Station (Machine).
/// </summary>
public class MachineDto
{
    /// <summary>Unique identifier for the station.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the station.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Friendly display name.</summary>
    public string? DisplayName { get; set; }
    /// <summary>Owning organization unit.</summary>
    public string? OrganizationId { get; set; }
    /// <summary>Custom identifier (e.g., OP10).</summary>
    public string CustomIdentifier { get; set; } = string.Empty;
    /// <summary>Machine technology type (e.g. "Milling", "Screwing Station", "Automatic Optical Inspection").</summary>
    public string? MachineType { get; set; }
    /// <summary>Hierarchical group / line identifier.</summary>
    public string? GroupId { get; set; }
    /// <summary>Preferred dedicated technician identifier.</summary>
    public string? PreferredTechnicianId { get; set; }
    /// <summary>Preferred dedicated technician display name.</summary>
    public string? PreferredTechnicianName { get; set; }
    /// <summary>CAD object handle for spatial mapping.</summary>
    public string? PinnedObjectHandle { get; set; }
    /// <summary>Machine operational lifecycle status: active, inactive, maintenance, decommissioned, dismantled.</summary>
    public string Status { get; set; } = "active";
    /// <summary>Documented user justification for decommissioning.</summary>
    public string? DecommissionReason { get; set; }
    /// <summary>Timestamp when the machine was decommissioned.</summary>
    public DateTimeOffset? DecommissionDate { get; set; }
    /// <summary>Disposition of installed parts on decommission.</summary>
    public string? DecommissionDisposition { get; set; }
    /// <summary>Single Point of Contact (SPOC) for the machine.</summary>
    public string? Spoc { get; set; }
    public string? SpocUserId { get; set; }

    /// <summary>Machine owner.</summary>
    public string? Owner { get; set; }
    public string? OwnerUserId { get; set; }

    /// <summary>Foreign key referencing the parent Sub-Line MachineGroup.</summary>
    public Guid? SubLineGroupId { get; set; }
    public string? SubLineGroupName { get; set; }

    /// <summary>Foreign key referencing the parent Line MachineGroup.</summary>
    public Guid? LineGroupId { get; set; }
    public string? LineGroupName { get; set; }

    /// <summary>Foreign key referencing the parent Project MachineGroup.</summary>
    public Guid? ProjectGroupId { get; set; }
    public string? ProjectGroupName { get; set; }

    /// <summary>Foreign key referencing the parent Business Unit MachineGroup.</summary>
    public Guid? BusinessUnitGroupId { get; set; }
    public string? BusinessUnitGroupName { get; set; }

    /// <summary>Parent machine ID for sub-machines in multi-machine cells or roundtables.</summary>
    public Guid? ParentMachineId { get; set; }
    public string? ParentMachineName { get; set; }

    /// <summary>Station slot index or angular position on a roundtable/dial table.</summary>
    public int? SubMachineIndex { get; set; }

    /// <summary>Cell classification: Standard, MultiMachineCell, Roundtable.</summary>
    public string? CellType { get; set; }

    /// <summary>Whether this machine acts as a multi-machine cell or roundtable containing child stations.</summary>
    public bool IsCellOrRoundtable { get; set; }

    /// <summary>Sub-machines nested under this cell or roundtable.</summary>
    public List<SubMachineSummaryDto> SubMachines { get; set; } = new();

    /// <summary>Summary of Client PCs controlling this station.</summary>
    public List<ClientPcSummaryDto> Controllers { get; set; } = new();
    /// <summary>List of teams responsible for this station.</summary>
    public List<TeamSummaryDto> ResponsibleTeams { get; set; } = new();
    /// <summary>Tree of hardware components installed at this station.</summary>
    public List<InventoryItemDto> Children { get; set; } = new();
}

/// <summary>
/// Lightweight representation of a Sub-Machine within a multi-machine cell or roundtable.
/// </summary>
public class SubMachineSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomIdentifier { get; set; } = string.Empty;
    public int? SubMachineIndex { get; set; }
    public string? MachineType { get; set; }
    public string Status { get; set; } = "active";
    public string? Spoc { get; set; }
    public string? Owner { get; set; }
    public string? PreferredTechnicianName { get; set; }
}

public class MachineDecommissionDto
{
    public string Reason { get; set; } = string.Empty;
    public string TargetDisposition { get; set; } = "decommissioned"; // decommissioned or dismantled
    public string PartsHandling { get; set; } = "salvage_donor"; // salvage_donor, scrap, retain
    public DateTimeOffset? DecommissionDate { get; set; }
}

public class MachineStatusUpdateDto
{
    public string Status { get; set; } = "active";
}

/// <summary>
/// Minimalist representation of a Client PC for nested summary views.
/// </summary>
public class ClientPcSummaryDto
{
    /// <summary>Unique identifier for the PC.</summary>
    public Guid Id { get; set; }
    /// <summary>Network hostname.</summary>
    public string? Hostname { get; set; }
    /// <summary>CAD object handle for spatial mapping.</summary>
    public string? PinnedObjectHandle { get; set; }
    /// <summary>The name of the PC.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// Generic summary for a responsible engineering team.
/// </summary>
public class TeamSummaryDto
{
    /// <summary>Unique identifier for the team.</summary>
    public Guid Id { get; set; }
    /// <summary>The team name (e.g., Mechanical, Controls).</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Generic Data Transfer Object for any inventory item in a hierarchical tree.
/// </summary>
public class InventoryItemDto
{
    /// <summary>Unique identifier for the asset.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the asset.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Friendly display name.</summary>
    public string? DisplayName { get; set; }
    /// <summary>The concrete type of the item (e.g., HardwareComponent, SoftwareComponent).</summary>
    public string ItemType { get; set; } = string.Empty;
    /// <summary>Dynamic metadata attributes (e.g., CPU specs, Software version).</summary>
    public object? Metadata { get; set; }
    /// <summary>Nested child assets in the inventory tree.</summary>
    public List<InventoryItemDto> Children { get; set; } = new();
}

/// <summary>
/// Optimized DTO for global search results.
/// </summary>
public class SearchResultDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? TypeLabel { get; set; }
    public string? ManufacturerName { get; set; }
}

/// <summary>
/// Lightweight DTO for updating a Machine's spatial mapping and associations.
/// </summary>
public class MachineUpdateDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? CustomIdentifier { get; set; }
    public string? PinnedObjectHandle { get; set; }
    public string? OrganizationId { get; set; }
    public List<Guid>? ControllerIds { get; set; }
    public string? Spoc { get; set; }
    public string? SpocUserId { get; set; }
    public string? Owner { get; set; }
    public string? OwnerUserId { get; set; }
    public Guid? SubLineGroupId { get; set; }
    public Guid? LineGroupId { get; set; }
    public Guid? ProjectGroupId { get; set; }
    public Guid? BusinessUnitGroupId { get; set; }
    public Guid? ParentMachineId { get; set; }
    public int? SubMachineIndex { get; set; }
    public string? CellType { get; set; }
}

/// <summary>
/// Lightweight DTO for updating a Client PC's spatial mapping and associations.
/// </summary>
public class ClientPcUpdateDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? PinnedObjectHandle { get; set; }
    public List<Guid>? ControlledMachineIds { get; set; }
}
