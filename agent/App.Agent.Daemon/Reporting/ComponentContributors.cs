namespace App.Agent.Daemon.Reporting;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.Shared.Protos;

/// <summary>
/// Contributes the Hardware inventory component.
/// </summary>
public class HardwareComponentContributor : IComponentContributor
{
    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        if (data.Hardware == null) return null;

        return new InventoryComponent
        {
            Name = "Hardware",
            Technology = "Agent",
            Type = "hardware",
            DataJson = JsonSerializer.Serialize(data.Hardware)
        };
    }
}

/// <summary>
/// Contributes the Software environment and installed packages component.
/// </summary>
public class SoftwareComponentContributor : IComponentContributor
{
    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        if (data.Software == null) return null;

        return new InventoryComponent
        {
            Name = "Software",
            Technology = "Agent",
            Type = "software",
            DataJson = JsonSerializer.Serialize(data.Software)
        };
    }
}

/// <summary>
/// Contributes physical storage drive metadata.
/// </summary>
public class PhysicalDrivesComponentContributor : IComponentContributor
{
    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        if (data.Disk?.PhysicalDrives == null || !data.Disk.PhysicalDrives.Any()) return null;

        return new InventoryComponent
        {
            Name = "PhysicalDrives",
            Technology = "Agent",
            Type = "hardware",
            DataJson = JsonSerializer.Serialize(data.Disk.PhysicalDrives)
        };
    }
}

/// <summary>
/// Contributes dynamically discovered device and kernel drivers.
/// </summary>
public class DriversComponentContributor : IComponentContributor
{
    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        if (data.Hardware?.Drivers == null || !data.Hardware.Drivers.Any()) return null;

        return new InventoryComponent
        {
            Name = "Drivers",
            Technology = "Kernel",
            Type = "driver",
            DataJson = JsonSerializer.Serialize(data.Hardware.Drivers)
        };
    }
}

/// <summary>
/// Contributes endpoint system event logs.
/// </summary>
public class EventsComponentContributor : IComponentContributor
{
    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        if (data.Events == null || !data.Events.Any()) return null;

        return new InventoryComponent
        {
            Name = "Events",
            Technology = "Agent",
            Type = "logs",
            DataJson = JsonSerializer.Serialize(data.Events)
        };
    }
}

/// <summary>
/// Contributes Beckhoff TwinCAT ADS runtime and OPC UA telemetry.
/// </summary>
public class IndustrialOtComponentContributor : IComponentContributor
{
    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        if (data.IndustrialOt == null || string.IsNullOrEmpty(data.IndustrialOt.AdsAmsNetId)) return null;

        return new InventoryComponent
        {
            Name = "IndustrialOT",
            Technology = "Beckhoff TwinCAT ADS / OPC UA",
            Type = "ot_runtime",
            DataJson = JsonSerializer.Serialize(data.IndustrialOt)
        };
    }
}

/// <summary>
/// Contributes live telemetry metrics (CPU load, RAM usage).
/// </summary>
public class LiveTelemetryComponentContributor : IComponentContributor
{
    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        if (data.LiveTelemetry == null) return null;

        return new InventoryComponent
        {
            Name = "Live Telemetry",
            Technology = "Agent",
            Type = "telemetry",
            DataJson = JsonSerializer.Serialize(data.LiveTelemetry)
        };
    }
}

