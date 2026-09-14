namespace App.Agent.Daemon;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using App.Agent.Daemon.Interfaces;
using App.Shared.Drivers;
using App.Shared.Sanitization;

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
    public IndustrialOtData IndustrialOt { get; set; } = new();
}

public class IndustrialOtData
{
    public string AdsAmsNetId { get; set; } = string.Empty;
    public int AdsPort { get; set; }
    public string AdsState { get; set; } = string.Empty;
    public Dictionary<string, object> AdsSymbols { get; set; } = new();
    public string OpcEndpoint { get; set; } = string.Empty;
    public bool OpcConnected { get; set; }
    public Dictionary<string, object> OpcNodes { get; set; } = new();
    public string LastTriggerReason { get; set; } = string.Empty;
    public string TriggerPriority { get; set; } = string.Empty;
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
    public List<DeviceDriver> Drivers { get; set; } = new();
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

public class SystemInfoService : ISystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;
    private readonly IConfigurationService? _configService;
    private readonly IDriverDiscoveryService _driverDiscoveryService;

    // In-memory cache for stable hardware & software metrics
    private static HardwareInfo? _cachedHardware;
    private static DateTimeOffset _hardwareCachedAt = DateTimeOffset.MinValue;
    private static SoftwareInfo? _cachedSoftware;
    private static DateTimeOffset _softwareCachedAt = DateTimeOffset.MinValue;
    private static string? _cachedMachineIdentifier;
    private static string? _cachedMacAddress;

    public TimeSpan HardwareCacheTtl => TimeSpan.FromSeconds(_configService?.Config.HardwarePollIntervalSeconds ?? 30);

    public SystemInfoService(
        ILogger<SystemInfoService> logger,
        IDriverDiscoveryService driverDiscoveryService,
        IConfigurationService? configService = null)
    {
        _logger = logger;
        _driverDiscoveryService = driverDiscoveryService;
        _configService = configService;
    }

    public SystemInfoData GetSystemInfo()
    {
        return new SystemInfoData
        {
            Hostname = StringSanitizer.ToSafeToken(Environment.MachineName, "endpoint"),
            MachineIdentifier = GetMachineIdentifier(),
            MacAddress = GetMacAddress(),
            LastOnline = DateTimeOffset.UtcNow,
            Hardware = GetHardwareConfig(),
            Software = GetSoftwareConfig(),
            Disk = GetDiskConfig(),
            Events = GetRecentEvents()
        };
    }

    public List<DeviceDriver> GetInstalledDrivers()
    {
        return _driverDiscoveryService.DiscoverInstalledDrivers();
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
            _logger.LogError(ex, "Error gathering disk telemetry");
        }
        return data;
    }

    private string GetMachineIdentifier()
    {
        if (!string.IsNullOrEmpty(_cachedMachineIdentifier)) return _cachedMachineIdentifier;

        string id = $"{Environment.MachineName}-{Environment.OSVersion}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
                foreach (var obj in searcher.Get())
                {
                    id = obj["UUID"]?.ToString() ?? "Unknown UUID";
                    break;
                }
            }
            catch { }
        }
        else
        {
            try
            {
                if (File.Exists("/etc/machine-id")) id = File.ReadAllText("/etc/machine-id").Trim();
                else if (File.Exists("/var/lib/dbus/machine-id")) id = File.ReadAllText("/var/lib/dbus/machine-id").Trim();
            }
            catch { }
        }

        _cachedMachineIdentifier = id;
        return id;
    }

    private string GetMacAddress()
    {
        if (!string.IsNullOrEmpty(_cachedMacAddress)) return _cachedMacAddress;

        var nic = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderByDescending(nic => nic.Speed)
            .FirstOrDefault();

        string mac = "00:00:00:00:00:00";
        if (nic != null)
        {
            mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
        }

        _cachedMacAddress = mac;
        return mac;
    }

    private HardwareInfo GetHardwareConfig()
    {
        if (_cachedHardware != null && (DateTimeOffset.UtcNow - _hardwareCachedAt) < HardwareCacheTtl)
        {
            return _cachedHardware;
        }

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

                // Dynamic device driver discovery
                config.Drivers = _driverDiscoveryService.DiscoverInstalledDrivers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying WMI for hardware configuration");
            }
        }
        else
        {
            config.Cpu = GetLinuxCpuInfo();
            config.Ram = GetLinuxRamInfo();
            config.Drivers = _driverDiscoveryService.DiscoverInstalledDrivers();
        }

        _cachedHardware = config;
        _hardwareCachedAt = DateTimeOffset.UtcNow;
        return config;
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
        if (_cachedSoftware != null && (DateTimeOffset.UtcNow - _softwareCachedAt) < HardwareCacheTtl)
        {
            return _cachedSoftware;
        }

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

        _cachedSoftware = info;
        _softwareCachedAt = DateTimeOffset.UtcNow;
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
        return new List<EventLogInfo>();
    }
}
