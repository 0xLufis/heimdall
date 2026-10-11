namespace App.Shared.Drivers;

using System.Collections.Generic;

/// <summary>
/// Service contract for dynamically enumerating and identifying installed device drivers.
/// </summary>
public interface IDriverDiscoveryService
{
    /// <summary>
    /// Discovers and returns all installed drivers, keyed by persistent hardware identifiers.
    /// </summary>
    List<DeviceDriver> DiscoverInstalledDrivers();
}
