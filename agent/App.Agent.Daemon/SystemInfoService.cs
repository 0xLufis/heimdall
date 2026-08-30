using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace App.Agent.Daemon;

public class SystemInfoData
{
    public string Hostname { get; set; } = string.Empty;
    public string MachineIdentifier { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public DateTimeOffset LastOnline { get; set; }
    public HardwareInfo Hardware { get; set; } = new();
    public SoftwareInfo Software { get; set; } = new();
    public DiskData Disk { get; set; } = new();
    public List<EventLogInfo> Events { get; set; } = new();
}

public class DiskData
{
    public double TotalFreeGB { get; set; }
    public double OsDriveFreeGB { get; set; }
    public Dictionary<string, double> Drives { get; set; } = new();
    public List<PhysicalDriveInfo> PhysicalDrives { get; set; } = new();
}

public class PhysicalDriveInfo
{
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string InterfaceType { get; set; } = string.Empty;
}

public class HardwareInfo
{
    public string Cpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Motherboard { get; set; } = string.Empty;
    public List<string> NetworkAdapters { get; set; } = new();
    public List<BeckhoffDriverInfo> BeckhoffRtDrivers { get; set; } = new();
}

public class BeckhoffDriverInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsBound { get; set; }
}

public class SoftwareInfo
{
    public string OsVersion { get; set; } = string.Empty;
    public List<string> InstalledPackages { get; set; } = new();
    public string Domain { get; set; } = string.Empty;
}

public class EventLogInfo
{
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime Time { get; set; }
}

public class SystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;

    public SystemInfoService(ILogger<SystemInfoService> logger)
    {
        _logger = logger;
    }

    public SystemInfoData GetSystemInfo()
    {
        return new SystemInfoData
        {
            Hostname = Environment.MachineName,
            MachineIdentifier = GetMachineIdentifier(),
            MacAddress = GetMacAddress(),
            LastOnline = DateTimeOffset.UtcNow,
            Hardware = GetHardwareConfig(),
            Software = GetSoftwareConfig(),
            Disk = GetDiskConfig(),
            Events = GetRecentEvents()
        };
    }

    private DiskData GetDiskConfig()
    {
        var data = new DiskData();
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
            foreach (var drive in drives)
            {
                double freeGB = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 2);
                data.Drives[drive.Name] = freeGB;
                data.TotalFreeGB += freeGB;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && drive.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
                {
                    data.OsDriveFreeGB = freeGB;
                }
                else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (drive.Name == "/" || drive.Name == "root"))
                {
                    data.OsDriveFreeGB = freeGB;
                }
            }
            data.TotalFreeGB = Math.Round(data.TotalFreeGB, 2);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT Model, SerialNumber, Size, InterfaceType FROM Win32_DiskDrive");
                foreach (var obj in searcher.Get())
                {
                    data.PhysicalDrives.Add(new PhysicalDriveInfo
                    {
                        Model = obj["Model"]?.ToString() ?? "Unknown",
                        SerialNumber = obj["SerialNumber"]?.ToString()?.Trim() ?? "Unknown",
                        SizeBytes = Convert.ToInt64(obj["Size"]),
                        InterfaceType = obj["InterfaceType"]?.ToString() ?? "Unknown"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting disk config");
        }
        return data;
    }

    private string GetMachineIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
                foreach (var obj in searcher.Get())
                {
                    return obj["UUID"]?.ToString() ?? "Unknown UUID";
                }
            }
            catch { }
        }
        else
        {
            try
            {
                if (File.Exists("/etc/machine-id")) return File.ReadAllText("/etc/machine-id").Trim();
                if (File.Exists("/var/lib/dbus/machine-id")) return File.ReadAllText("/var/lib/dbus/machine-id").Trim();
            }
            catch { }
        }
        return $"{Environment.MachineName}-{Environment.OSVersion}";
    }

    private string GetMacAddress()
    {
        var nic = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderByDescending(nic => nic.Speed)
            .FirstOrDefault();

        if (nic != null)
        {
            return string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
        }

        return "00:00:00:00:00:00";
    }

    private HardwareInfo GetHardwareConfig()
    {
        var config = new HardwareInfo();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var cpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in cpuSearcher.Get()) config.Cpu = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";

                using var memSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in memSearcher.Get())
                {
                    var totalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    config.Ram = $"{Math.Round(totalMemory / (1024.0 * 1024.0 * 1024.0), 0)} GB";
                }

                using var baseSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (var obj in baseSearcher.Get()) config.Motherboard = $"{obj["Manufacturer"]} {obj["Product"]}";

                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up)
                        config.NetworkAdapters.Add($"{nic.Name} ({nic.Description})");
                }

                config.BeckhoffRtDrivers = GetBeckhoffRtDrivers();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error querying WMI for hardware"); }
        }
        else
        {
            config.Cpu = GetLinuxCpuInfo();
            config.Ram = GetLinuxRamInfo();
        }

        return config;
    }

    public List<BeckhoffDriverInfo> GetBeckhoffRtDrivers()
    {
        var drivers = new List<BeckhoffDriverInfo>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return drivers;
        }

        try
        {
            var wmiDrivers = GetBeckhoffRtDriversWmi();
            var setupApiDrivers = GetBeckhoffRtDriversSetupApi();

            var driverDict = new Dictionary<string, BeckhoffDriverInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var driver in setupApiDrivers)
            {
                string key = string.IsNullOrEmpty(driver.DeviceName) ? driver.DeviceId : driver.DeviceName;
                driverDict[key] = driver;
            }

            foreach (var driver in wmiDrivers)
            {
                string key = string.IsNullOrEmpty(driver.DeviceName) ? driver.DeviceId : driver.DeviceName;
                if (driverDict.TryGetValue(key, out var existing))
                {
                    if (!string.IsNullOrEmpty(driver.DriverVersion)) existing.DriverVersion = driver.DriverVersion;
                    if (!string.IsNullOrEmpty(driver.Provider)) existing.Provider = driver.Provider;
                    if (!string.IsNullOrEmpty(driver.HardwareId)) existing.HardwareId = driver.HardwareId;
                    if (!string.IsNullOrEmpty(driver.Service)) existing.Service = driver.Service;
                    if (!string.IsNullOrEmpty(driver.DeviceId)) existing.DeviceId = driver.DeviceId;
                    existing.IsBound = existing.IsBound || driver.IsBound;
                }
                else
                {
                    driverDict[key] = driver;
                }
            }

            drivers = driverDict.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting Beckhoff RT drivers");
        }

        return drivers;
    }

    [SupportedOSPlatform("windows")]
    private List<BeckhoffDriverInfo> GetBeckhoffRtDriversWmi()
    {
        var list = new List<BeckhoffDriverInfo>();

        try
        {
            string query = "SELECT DeviceName, DriverVersion, ProviderName, Service, HardwareID, DeviceID, Status FROM Win32_PnPSignedDriver WHERE DeviceName LIKE '%Beckhoff%' OR Service = 'TcRTEthernet' OR Service = 'TcEth'";
            using var searcher = new ManagementObjectSearcher(query);
            foreach (var obj in searcher.Get())
            {
                var service = obj["Service"]?.ToString() ?? string.Empty;
                var deviceName = obj["DeviceName"]?.ToString() ?? string.Empty;
                bool isBound = service.Equals("TcRTEthernet", StringComparison.OrdinalIgnoreCase) ||
                               service.Equals("TcEth", StringComparison.OrdinalIgnoreCase) ||
                               deviceName.Contains("Beckhoff", StringComparison.OrdinalIgnoreCase) ||
                               deviceName.Contains("TwinCAT", StringComparison.OrdinalIgnoreCase);

                list.Add(new BeckhoffDriverInfo
                {
                    DeviceName = deviceName,
                    Service = service,
                    DriverVersion = obj["DriverVersion"]?.ToString() ?? string.Empty,
                    Provider = obj["ProviderName"]?.ToString() ?? string.Empty,
                    HardwareId = obj["HardwareID"]?.ToString() ?? string.Empty,
                    DeviceId = obj["DeviceID"]?.ToString() ?? string.Empty,
                    Status = obj["Status"]?.ToString() ?? "OK",
                    IsBound = isBound
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI query for Beckhoff RT drivers failed");
        }

        return list;
    }

    [SupportedOSPlatform("windows")]
    private List<BeckhoffDriverInfo> GetBeckhoffRtDriversSetupApi()
    {
        var list = new List<BeckhoffDriverInfo>();

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

                    bool matchesBeckhoff = name.Contains("Beckhoff", StringComparison.OrdinalIgnoreCase) ||
                                          name.Contains("TwinCAT", StringComparison.OrdinalIgnoreCase) ||
                                          service.Equals("TcRTEthernet", StringComparison.OrdinalIgnoreCase) ||
                                          service.Equals("TcEth", StringComparison.OrdinalIgnoreCase);

                    if (matchesBeckhoff)
                    {
                        string status = "Unknown";
                        if (SetupApiNative.CM_Get_DevNode_Status(out uint devStatus, out uint problemNum, deviceInfoData.DevInst, 0) == 0)
                        {
                            status = (devStatus & 0x00000008) != 0 ? "Started / Running" : $"Stopped (Problem: {problemNum})";
                        }

                        list.Add(new BeckhoffDriverInfo
                        {
                            DeviceName = name,
                            Service = service,
                            DriverVersion = string.Empty,
                            Provider = mfg,
                            HardwareId = hardwareId,
                            DeviceId = $"DEVINST_{deviceInfoData.DevInst}",
                            Status = status,
                            IsBound = service.Equals("TcRTEthernet", StringComparison.OrdinalIgnoreCase) || service.Equals("TcEth", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }
            finally
            {
                SetupApiNative.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetupAPI P/Invoke query for Beckhoff RT drivers failed");
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

    private string GetLinuxCpuInfo()
    {
        try
        {
            var line = File.ReadAllLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name"));
            return line?.Split(':')[1].Trim() ?? "Linux CPU";
        }
        catch { return "Linux CPU"; }
    }

    private string GetLinuxRamInfo()
    {
        try
        {
            var line = File.ReadAllLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemTotal"));
            if (line != null)
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    return $"{Math.Round(kb / (1024.0 * 1024.0), 0)} GB";
            }
        }
        catch { }
        return "Linux RAM";
    }

    private SoftwareInfo GetSoftwareConfig()
    {
        var info = new SoftwareInfo
        {
            OsVersion = RuntimeInformation.OSDescription,
            InstalledPackages = GetInstalledPackages()
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Domain FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get()) info.Domain = obj["Domain"]?.ToString() ?? string.Empty;
            }
            catch { }
        }

        return info;
    }

    private List<string> GetInstalledPackages()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ScanWindowsRegistryInstalledSoftware();
        }

        var packages = new HashSet<string>();
        try
        {
            if (File.Exists("/usr/bin/dpkg"))
            {
                packages.Add("dpkg-based system");
            }
            else if (File.Exists("/usr/bin/rpm"))
            {
                packages.Add("rpm-based system");
            }
        }
        catch { }

        return packages.OrderBy(p => p).ToList();
    }

    [SupportedOSPlatform("windows")]
    private List<string> ScanWindowsRegistryInstalledSoftware()
    {
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var registryLocations = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.CurrentUser, RegistryView.Registry32)
        };

        const string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        foreach (var (hive, view) in registryLocations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = baseKey.OpenSubKey(uninstallKeyPath);
                if (uninstallKey == null) continue;

                foreach (var subkeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var subkey = uninstallKey.OpenSubKey(subkeyName);
                        if (subkey == null) continue;

                        var isSystemComponent = subkey.GetValue("SystemComponent");
                        if (isSystemComponent is int sysComp && sysComp == 1) continue;

                        var parentKeyName = subkey.GetValue("ParentKeyName")?.ToString();
                        if (!string.IsNullOrEmpty(parentKeyName)) continue;

                        var displayName = subkey.GetValue("DisplayName")?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        var displayVersion = subkey.GetValue("DisplayVersion")?.ToString()?.Trim();
                        var publisher = subkey.GetValue("Publisher")?.ToString()?.Trim();

                        string entry = displayName;
                        if (!string.IsNullOrEmpty(displayVersion))
                        {
                            entry += $" (v{displayVersion})";
                        }
                        if (!string.IsNullOrEmpty(publisher) && !displayName.Contains(publisher, StringComparison.OrdinalIgnoreCase))
                        {
                            entry += $" - {publisher}";
                        }

                        packages.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error reading registry subkey {Subkey}", subkeyName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning registry hive {Hive} ({View})", hive, view);
            }
        }

        return packages.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<EventLogInfo> GetRecentEvents()
    {
        var events = new List<EventLogInfo>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // In a real scenario, we'd query EventLog specifically for BSOD (System Error 1001) or Update errors
            }
            catch { }
        }
        return events;
    }
}

internal static class SetupApiNative
{
    public static readonly Guid GUID_DEVCLASS_NET = new Guid("{4d36e972-e325-11ce-bfc1-08002be10318}");

    public const uint DIGCF_PRESENT = 0x00000002;

    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_SERVICE = 0x00000004;
    public const uint SPDRP_CLASS = 0x00000007;
    public const uint SPDRP_DRIVER = 0x00000009;
    public const uint SPDRP_MFG = 0x0000000B;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        string? Enumerator,
        IntPtr hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(
        IntPtr DeviceInfoSet,
        uint MemberIndex,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        out uint PropertyRegDataType,
        byte[] PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("cfgmgr32.dll", SetLastError = true)]
    public static extern uint CM_Get_DevNode_Status(
        out uint pdwStatus,
        out uint pdwProblemNumber,
        uint dnDevInst,
        uint ulFlags);
}
