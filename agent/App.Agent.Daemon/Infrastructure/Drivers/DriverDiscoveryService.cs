namespace App.Agent.Daemon.Infrastructure.Drivers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using App.Shared.Drivers;
using App.Shared.Sanitization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for dynamically discovering and classifying installed device and kernel drivers.
/// Keys on immutable hardware identifiers (PCI/USB/ACPI Vendor and Device IDs) so that
/// driver records are never lost across driver software updates or service renames.
/// </summary>
public class DriverDiscoveryService : IDriverDiscoveryService
{
    private readonly ILogger<DriverDiscoveryService> _logger;

    public DriverDiscoveryService(ILogger<DriverDiscoveryService> logger)
    {
        _logger = logger;
    }

    public List<DeviceDriver> DiscoverInstalledDrivers()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return DiscoverWindowsDrivers();
        }

        return DiscoverLinuxDrivers();
    }

    [SupportedOSPlatform("windows")]
    private List<DeviceDriver> DiscoverWindowsDrivers()
    {
        var driverIndex = new Dictionary<string, DeviceDriver>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 1. SetupAPI Hardware-bound Driver Enumeration
            var setupApiDrivers = DiscoverSetupApiDrivers();
            foreach (var d in setupApiDrivers)
            {
                string key = GetDriverKey(d);
                driverIndex[key] = d;
            }

            // 2. WMI PnP Signed Driver Store Enumeration
            var wmiDrivers = DiscoverWmiSignedDrivers();
            foreach (var d in wmiDrivers)
            {
                string key = GetDriverKey(d);
                if (driverIndex.TryGetValue(key, out var existing))
                {
                    // Merge properties, prioritizing SetupAPI binding status while keeping WMI version info
                    driverIndex[key] = existing with
                    {
                        DriverVersion = !string.IsNullOrEmpty(d.DriverVersion) ? d.DriverVersion : existing.DriverVersion,
                        Provider = !string.IsNullOrEmpty(d.Provider) ? d.Provider : existing.Provider,
                        HardwareId = !string.IsNullOrEmpty(d.HardwareId) ? d.HardwareId : existing.HardwareId,
                        Service = !string.IsNullOrEmpty(d.Service) ? d.Service : existing.Service,
                        InfName = !string.IsNullOrEmpty(d.InfName) ? d.InfName : existing.InfName,
                        Category = d.Category != "System" ? d.Category : existing.Category,
                        IsBound = existing.IsBound || d.IsBound
                    };
                }
                else
                {
                    driverIndex[key] = d;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Windows device drivers.");
        }

        return driverIndex.Values.OrderBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
    }

    [SupportedOSPlatform("windows")]
    private List<DeviceDriver> DiscoverSetupApiDrivers()
    {
        var list = new List<DeviceDriver>();

        try
        {
            Guid netClassGuid = SetupApiNative.GUID_DEVCLASS_NET;
            IntPtr deviceInfoSet = SetupApiNative.SetupDiGetClassDevs(
                ref netClassGuid, null, IntPtr.Zero, SetupApiNative.DIGCF_PRESENT);

            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
                return list;

            try
            {
                var deviceInfoData = new SetupApiNative.SP_DEVINFO_DATA();
                deviceInfoData.cbSize = (uint)Marshal.SizeOf(deviceInfoData);

                uint memberIndex = 0;
                while (SetupApiNative.SetupDiEnumDeviceInfo(deviceInfoSet, memberIndex, ref deviceInfoData))
                {
                    memberIndex++;

                    string deviceDesc = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SetupApiNative.SPDRP_DEVICEDESC);
                    string friendlyName = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SetupApiNative.SPDRP_FRIENDLYNAME);
                    string service = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SetupApiNative.SPDRP_SERVICE);
                    string hardwareId = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SetupApiNative.SPDRP_HARDWAREID);
                    string mfg = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SetupApiNative.SPDRP_MFG);

                    string name = !string.IsNullOrEmpty(friendlyName) ? friendlyName : deviceDesc;
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(service))
                        continue;

                    string status = "Unknown";
                    bool isBound = false;

                    if (SetupApiNative.CM_Get_DevNode_Status(out uint devStatus, out uint problemNum, deviceInfoData.DevInst, 0) == 0)
                    {
                        bool isStarted = (devStatus & 0x00000008) != 0;
                        status = isStarted ? "Running" : $"Stopped (Code: {problemNum})";
                        isBound = isStarted;
                    }

                    string category = ClassifyDriver(name, service, mfg);

                    list.Add(new DeviceDriver
                    {
                        DeviceName = name,
                        Service = service,
                        DriverVersion = string.Empty,
                        Provider = mfg,
                        HardwareId = hardwareId,
                        DeviceId = $"DEVINST_{deviceInfoData.DevInst}",
                        Category = category,
                        Status = status,
                        IsBound = isBound
                    });
                }
            }
            finally
            {
                SetupApiNative.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetupAPI driver enumeration encountered an issue.");
        }

        return list;
    }

    [SupportedOSPlatform("windows")]
    private List<DeviceDriver> DiscoverWmiSignedDrivers()
    {
        var list = new List<DeviceDriver>();

        try
        {
            // Query signed drivers without hardcoded vendor restrictions
            string query = "SELECT DeviceName, DriverVersion, ProviderName, Service, HardwareID, DeviceID, Status, InfName FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL";
            using var searcher = new ManagementObjectSearcher(query);

            foreach (var obj in searcher.Get())
            {
                var deviceName = obj["DeviceName"]?.ToString() ?? string.Empty;
                var service = obj["Service"]?.ToString() ?? string.Empty;
                var provider = obj["ProviderName"]?.ToString() ?? string.Empty;
                var hardwareId = obj["HardwareID"]?.ToString() ?? string.Empty;
                var deviceId = obj["DeviceID"]?.ToString() ?? string.Empty;
                var version = obj["DriverVersion"]?.ToString() ?? string.Empty;
                var status = obj["Status"]?.ToString() ?? "OK";
                var infName = obj["InfName"]?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(deviceName) && string.IsNullOrWhiteSpace(service))
                    continue;

                string category = ClassifyDriver(deviceName, service, provider);

                // Include all classified non-system drivers, or any with explicit services
                if (category != "System" || !string.IsNullOrEmpty(service))
                {
                    bool isBound = status.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                                   status.Equals("Running", StringComparison.OrdinalIgnoreCase);

                    list.Add(new DeviceDriver
                    {
                        DeviceName = deviceName,
                        Service = service,
                        DriverVersion = version,
                        Provider = provider,
                        HardwareId = hardwareId,
                        DeviceId = deviceId,
                        InfName = infName,
                        Category = category,
                        Status = status,
                        IsBound = isBound
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI signed driver query encountered an issue.");
        }

        return list;
    }

    private List<DeviceDriver> DiscoverLinuxDrivers()
    {
        var list = new List<DeviceDriver>();

        try
        {
            if (File.Exists("/proc/modules"))
            {
                var lines = File.ReadAllLines("/proc/modules");
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1) continue;

                    string moduleName = parts[0];
                    string category = ClassifyLinuxModule(moduleName);

                    if (category != "System")
                    {
                        list.Add(new DeviceDriver
                        {
                            DeviceName = moduleName,
                            Service = moduleName,
                            HardwareId = $"MODULE_{moduleName}",
                            DeviceId = $"SYS_MODULE_{moduleName}",
                            Category = category,
                            Status = "Loaded",
                            IsBound = true
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Linux kernel module enumeration encountered an issue.");
        }

        return list;
    }

    [SupportedOSPlatform("windows")]
    private static string GetDeviceProperty(IntPtr deviceInfoSet, ref SetupApiNative.SP_DEVINFO_DATA deviceInfoData, uint property)
    {
        byte[] buffer = new byte[1024];
        if (SetupApiNative.SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, out _, buffer, (uint)buffer.Length, out _))
        {
            string val = System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
            int nullIdx = val.IndexOf('\0');
            return nullIdx >= 0 ? val.Substring(0, nullIdx) : val;
        }
        return string.Empty;
    }

    /// <summary>
    /// Computes the persistent lookup key using immutable hardware or device instance identifiers.
    /// </summary>
    private static string GetDriverKey(DeviceDriver d)
    {
        if (!string.IsNullOrEmpty(d.HardwareId)) return d.HardwareId;
        if (!string.IsNullOrEmpty(d.DeviceId)) return d.DeviceId;
        if (!string.IsNullOrEmpty(d.Service)) return $"SVC_{d.Service}";
        return d.DeviceName;
    }

    /// <summary>
    /// Classifies drivers into functional categories based on hardware characteristics and subsystem indicators.
    /// </summary>
    private static string ClassifyDriver(string name, string service, string provider)
    {
        string combined = $"{name} {service} {provider}".ToLowerInvariant();

        if (combined.Contains("twincat") || combined.Contains("tcrtime") || combined.Contains("tcrth") || combined.Contains("beckhoff"))
            return "PlcRuntime";

        if (combined.Contains("simatic") || combined.Contains("profinet") || combined.Contains("winac") || combined.Contains("cp1613"))
            return "Fieldbus";

        if (combined.Contains("rslinx") || combined.Contains("1784") || combined.Contains("allen-bradley") || combined.Contains("rockwell"))
            return "Fieldbus";

        if (combined.Contains("ni-") || combined.Contains("national instruments") || combined.Contains("daqmx"))
            return "DataAcquisition";

        if (combined.Contains("codesys") || combined.Contains("cmpschedule") || combined.Contains("cmpplc"))
            return "PlcRuntime";

        if (combined.Contains("ethercat") || combined.Contains("profibus") || combined.Contains("canbus") || combined.Contains("moxa"))
            return "Fieldbus";

        if (combined.Contains("realtek") || combined.Contains("intel") || combined.Contains("ethernet") || combined.Contains("gigabit") || combined.Contains("ndis"))
            return "Network";

        return "System";
    }

    private static string ClassifyLinuxModule(string moduleName)
    {
        string lower = moduleName.ToLowerInvariant();
        if (lower.Contains("ec_master") || lower.Contains("soem") || lower.Contains("ethercat")) return "Fieldbus";
        if (lower.Contains("can") || lower.Contains("vcan") || lower.Contains("c_can") || lower.Contains("peak_pci")) return "Fieldbus";
        if (lower.Contains("iio")) return "DataAcquisition";
        if (lower.Contains("e1000") || lower.Contains("igb") || lower.Contains("r8169")) return "Network";
        return "System";
    }
}
