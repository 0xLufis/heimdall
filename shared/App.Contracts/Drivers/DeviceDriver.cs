namespace App.Shared.Drivers;

/// <summary>
/// Domain model representing an installed system or kernel device driver.
/// Uses invariant hardware and device identifiers that persist across software version upgrades.
/// </summary>
public record DeviceDriver
{
    /// <summary>Human-readable device or driver name.</summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>Associated kernel or Windows service name.</summary>
    public string Service { get; init; } = string.Empty;

    /// <summary>Driver file or package version string.</summary>
    public string DriverVersion { get; init; } = string.Empty;

    /// <summary>Driver vendor, manufacturer, or publisher.</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Invariant PCI/USB/ACPI hardware identifier (e.g. PCI\VEN_15EC&DEV_5000).
    /// This hardware signature does not change across driver software updates.
    /// </summary>
    public string HardwareId { get; init; } = string.Empty;

    /// <summary>Unique device instance ID from the OS hardware tree.</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Original INF file name in the system DriverStore (e.g. oem12.inf).</summary>
    public string InfName { get; init; } = string.Empty;

    /// <summary>Driver category (e.g. Network, RealTime, Fieldbus, Storage, System).</summary>
    public string Category { get; init; } = "System";

    /// <summary>Operating status (e.g. Running, Stopped, OK).</summary>
    public string Status { get; init; } = "Unknown";

    /// <summary>Indicates whether the driver is actively bound to an operational device node.</summary>
    public bool IsBound { get; init; }
}
