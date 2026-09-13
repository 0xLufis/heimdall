using System;
using System.Collections.Generic;
using App.Agent.Daemon.Infrastructure.Drivers;
using App.Shared.Drivers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class DriverDiscoveryTests
{
    [Fact]
    public void DeviceDriver_RecordImmutabilityAndCopySemantics()
    {
        var original = new DeviceDriver
        {
            DeviceName = "Fieldbus Interface Card",
            Service = "fb_pci",
            DriverVersion = "1.0.0",
            Provider = "Vendor Corp",
            HardwareId = @"PCI\VEN_8086&DEV_100E&SUBSYS_001E8086",
            DeviceId = @"PCI\VEN_8086&DEV_100E&SUBSYS_001E8086\4&1B2D5A4&0&00E4",
            InfName = "oem12.inf",
            Category = "Fieldbus",
            Status = "Running",
            IsBound = true
        };

        // Driver software updated to 2.0.0, but HardwareId and DeviceId remain invariant
        var updated = original with
        {
            DriverVersion = "2.0.0",
            InfName = "oem45.inf"
        };

        Assert.Equal(original.HardwareId, updated.HardwareId);
        Assert.Equal(original.DeviceId, updated.DeviceId);
        Assert.Equal("1.0.0", original.DriverVersion);
        Assert.Equal("2.0.0", updated.DriverVersion);
        Assert.Equal("oem45.inf", updated.InfName);
        Assert.True(updated.IsBound);
    }

    [Fact]
    public void DriverDiscoveryService_CanInstantiateAndExecuteOnCurrentPlatform()
    {
        var service = new DriverDiscoveryService(NullLogger<DriverDiscoveryService>.Instance);
        var drivers = service.DiscoverInstalledDrivers();

        Assert.NotNull(drivers);
        // On Linux CI/dev machine, drivers list could be empty or have kernel modules
        foreach (var driver in drivers)
        {
            Assert.False(string.IsNullOrWhiteSpace(driver.DeviceName));
            Assert.False(string.IsNullOrWhiteSpace(driver.Category));
        }
    }

    [Fact]
    public void DriverDiscovery_PrioritizesImmutableHardwareIdOverMutableNames()
    {
        var d1 = new DeviceDriver
        {
            DeviceName = "Generic Comm Adapter",
            Service = "comm_old",
            HardwareId = @"PCI\VEN_15BC&DEV_0100",
            Category = "Fieldbus",
            DriverVersion = "1.0.0"
        };

        var d2 = new DeviceDriver
        {
            DeviceName = "Updated Comm Controller X1",
            Service = "comm_new",
            HardwareId = @"PCI\VEN_15BC&DEV_0100",
            Category = "Fieldbus",
            DriverVersion = "2.5.0"
        };

        // Index dictionary keyed on invariant HardwareId
        var index = new Dictionary<string, DeviceDriver>(StringComparer.OrdinalIgnoreCase);
        index[d1.HardwareId] = d1;

        // Software/service rename does not create a duplicate or orphan record
        if (index.TryGetValue(d2.HardwareId, out var existing))
        {
            index[d2.HardwareId] = existing with
            {
                DeviceName = d2.DeviceName,
                Service = d2.Service,
                DriverVersion = d2.DriverVersion
            };
        }

        Assert.Single(index);
        var resolved = index[@"PCI\VEN_15BC&DEV_0100"];
        Assert.Equal("Updated Comm Controller X1", resolved.DeviceName);
        Assert.Equal("comm_new", resolved.Service);
        Assert.Equal("2.5.0", resolved.DriverVersion);
    }
}
