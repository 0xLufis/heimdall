namespace App.Agent.Daemon.Interfaces;

using System.Collections.Generic;
using App.Shared.Drivers;

/// <summary>
/// Service for gathering endpoint hardware, software, driver, and system invariants.
/// </summary>
public interface ISystemInfoService
{
    /// <summary>
    /// Gathers complete system information for the endpoint.
    /// </summary>
    SystemInfoData GetSystemInfo();

    /// <summary>
    /// Enumerates installed device drivers on the endpoint.
    /// </summary>
    List<DeviceDriver> GetInstalledDrivers();
}
