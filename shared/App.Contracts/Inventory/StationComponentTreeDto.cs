namespace App.Contracts.Inventory;

using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Strongly-typed Station Component Tree representation.
/// Replaces untyped anonymous objects to ensure type safety across layers.
/// </summary>
public class StationComponentTreeDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? CustomIdentifier { get; set; }
    public string? MachineType { get; set; }
    public string? GroupId { get; set; }
    public List<StationControllerNodeDto> Controllers { get; set; } = new();
    public List<StationInstalledPartDto> InstalledParts { get; set; } = new();
    public int TotalComponentsCount { get; set; }
}

public class StationControllerNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public int? VlanId { get; set; }
    public string? PinnedObjectHandle { get; set; }
    public DateTimeOffset? LastOnline { get; set; }
    public List<StationInternalComponentDto> InternalComponents { get; set; } = new();
}

public class StationInternalComponentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? SerialNumber { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public Guid? ParentId { get; set; }
    public JsonDocument? Metadata { get; set; }
    public bool IsSigned { get; set; }
    public bool IsSandboxed { get; set; }
    public object? CustomData { get; set; }
}

public class StationInstalledPartDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? SerialNumber { get; set; }
    public string? EquipmentStatus { get; set; }
    public string? Technology { get; set; }
    public string? StorageLocation { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? ManufacturerName { get; set; }
}
