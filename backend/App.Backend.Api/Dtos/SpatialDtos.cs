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
    /// <summary>Summary of production stations controlled by this PC.</summary>
    public List<MachineSummaryDto> Machines { get; set; } = new();
    /// <summary>List of teams responsible for this node.</summary>
    public List<TeamSummaryDto> ResponsibleTeams { get; set; } = new();
    /// <summary>Tree of hardware and software components assigned to this PC.</summary>
    public List<InventoryItemDto> InventoryItems { get; set; } = new();
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
    /// <summary>CAD object handle for spatial mapping.</summary>
    public string? PinnedObjectHandle { get; set; }
    /// <summary>Summary of Client PCs controlling this station.</summary>
    public List<ClientPcSummaryDto> Controllers { get; set; } = new();
    /// <summary>List of teams responsible for this station.</summary>
    public List<TeamSummaryDto> ResponsibleTeams { get; set; } = new();
    /// <summary>Tree of hardware components installed at this station.</summary>
    public List<InventoryItemDto> Children { get; set; } = new();
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
