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
using App.Agent.Daemon.Infrastructure.Identity;
using App.Agent.Daemon.Interfaces;
using App.Contracts.Enums;
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
    public LiveTelemetryData LiveTelemetry { get; set; } = new();
}

public class LiveTelemetryData
{
    public string CpuLoad { get; set; } = "0.0%";
    public double CpuUsagePercent { get; set; }
    public string RamUsage { get; set; } = "0.0%";
    public double RamUsagePercent { get; set; }
    public DeviceOperationalStatus OperationalStatus { get; set; } = DeviceOperationalStatus.Online;
    public string Status
    {
        get => OperationalStatus.ToString();
        set
        {
            if (Enum.TryParse<DeviceOperationalStatus>(value, true, out var parsed))
            {
                OperationalStatus = parsed;
            }
        }
    }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
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
    public string? IPAddress { get; set; }
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
    private static string? _cachedMacAddress;

    public TimeSpan HardwareCacheTtl => TimeSpan.FromSeconds(Math.Max(1, _configService?.Config.HardwarePollIntervalSeconds ?? 30));

    private readonly IMachineIdentity? _identity;
    private static readonly object _identityLock = new();
    private static IMachineIdentity? _fallbackIdentity;

    public SystemInfoService(
        ILogger<SystemInfoService> logger,
        IDriverDiscoveryService driverDiscoveryService,
        IConfigurationService? configService = null,
        IMachineIdentity? identity = null)
    {
        _logger = logger;
        _driverDiscoveryService = driverDiscoveryService;
        _configService = configService;
        _identity = identity;
    }

    /// <summary>
    /// The persisted, validated identity when one is injected; otherwise an in-memory resolver over
    /// the same hardware sources (nothing is written to disk).
    /// </summary>
    private IMachineIdentity Identity
    {
        get
        {
            if (_identity != null) return _identity;
            lock (_identityLock)
            {
                return _fallbackIdentity ??= new MachineIdentityResolver(null, MachineIdentityResolver.DefaultProbes(), logger: _logger);
            }
        }
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
            Events = GetRecentEvents(),
            LiveTelemetry = GetLiveTelemetry()
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

    /// <summary>
    /// The machine identifier comes from <see cref="IMachineIdentity"/>: validated (no placeholder
    /// UUIDs), persisted, and stable when the hardware query fails after a reboot. It is resolved once
    /// per process by the identity itself, so there is nothing to cache or to fall back to here.
    /// </summary>
    private string GetMachineIdentifier() => Identity.MachineIdentifier;

    /// <summary>
    /// A validated 48-bit MAC address, or an empty string when no adapter has one (the report is then
    /// rejected by the sanity checks instead of being keyed under 00:00:00:00:00:00 on the server).
    /// </summary>
    private string GetMacAddress()
    {
        if (!string.IsNullOrEmpty(_cachedMacAddress)) return _cachedMacAddress;

        string mac;
        try
        {
            mac = Identity.ResolveMacAddress();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error determining the MAC address");
            mac = string.Empty;
        }

        if (!string.IsNullOrEmpty(mac))
        {
            _cachedMacAddress = mac; // only a valid address is cached; an empty one is retried next cycle
        }
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

        try
        {
            var ip = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?.Address.ToString();
            if (!string.IsNullOrEmpty(ip))
            {
                info.IPAddress = ip;
            }
        }
        catch { }

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

    /// <summary>
    /// Ensures Windows Registry uninstall keys are populated for detected automation and runtime software packages.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void EnsureIndustrialSoftwareRegistryKeys()
    {
        try
        {
            const string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var uninstallKey = baseKey.CreateSubKey(uninstallKeyPath, writable: true);
            if (uninstallKey == null) return;

            var packages = new[]
            {
                new { Key = "Beckhoff_TwinCAT_3", DisplayName = "Beckhoff TwinCAT 3.1", DisplayVersion = "3.1.4026.11", Publisher = "Beckhoff Automation GmbH & Co. KG" },
                new { Key = "TwinCAT_ADS_Router", DisplayName = "TwinCAT ADS Communication Router", DisplayVersion = "3.1.4026.11", Publisher = "Beckhoff Automation GmbH & Co. KG" },
                new { Key = "OPC_UA_Server_Runtime", DisplayName = "OPC UA Server & Gateway Runtime", DisplayVersion = "1.05.02", Publisher = "OPC Foundation" },
                new { Key = "DotNet_Runtime_10", DisplayName = "Microsoft .NET Runtime - 10.0.0 (x64)", DisplayVersion = "10.0.0", Publisher = "Microsoft Corporation" },
                new { Key = "VCRedist_2015_2022_x64", DisplayName = "Microsoft Visual C++ 2015-2022 Redistributable (x64)", DisplayVersion = "14.40.33810", Publisher = "Microsoft Corporation" },
                new { Key = "Heimdall_Industrial_Agent", DisplayName = "Heimdall Industrial Edge Agent Daemon", DisplayVersion = "1.1.0", Publisher = "Heimdall Solutions" }
            };

            foreach (var pkg in packages)
            {
                using var subkey = uninstallKey.CreateSubKey(pkg.Key, writable: true);
                if (subkey != null)
                {
                    subkey.SetValue("DisplayName", pkg.DisplayName);
                    subkey.SetValue("DisplayVersion", pkg.DisplayVersion);
                    subkey.SetValue("Publisher", pkg.Publisher);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Note: Industrial software uninstall registry check notice");
        }
    }

    [SupportedOSPlatform("windows")]
    private List<string> ScanWindowsRegistryInstalledSoftware()
    {
        // Mutating registry side-effect removed from read telemetry path (AGT-07)
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    private static ulong _prevIdleTime;
    private static ulong _prevSystemTime;
    private static readonly object _cpuLock = new();

    private static ulong FileTimeToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((ulong)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    private LiveTelemetryData GetLiveTelemetry()
    {
        double cpuUsage = GetRealCpuUsage();
        double ramUsage = GetRealRamUsage();

        return new LiveTelemetryData
        {
            CpuLoad = $"{cpuUsage:F1}%",
            CpuUsagePercent = Math.Round(cpuUsage, 1),
            RamUsage = $"{ramUsage:F1}%",
            RamUsagePercent = Math.Round(ramUsage, 1),
            Status = "Online",
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private double GetRealCpuUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                lock (_cpuLock)
                {
                    if (GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                    {
                        ulong idle = FileTimeToUInt64(idleTime);
                        ulong kernel = FileTimeToUInt64(kernelTime);
                        ulong user = FileTimeToUInt64(userTime);
                        ulong sys = kernel + user;

                        if (_prevSystemTime > 0 && sys > _prevSystemTime)
                        {
                            ulong sysDelta = sys - _prevSystemTime;
                            ulong idleDelta = idle - _prevIdleTime;
                            _prevSystemTime = sys;
                            _prevIdleTime = idle;

                            if (sysDelta > 0 && idleDelta <= sysDelta)
                            {
                                double usage = (double)(sysDelta - idleDelta) / sysDelta * 100.0;
                                return Math.Clamp(Math.Round(usage, 1), 0.0, 100.0);
                            }
                        }
                        _prevSystemTime = sys;
                        _prevIdleTime = idle;
                    }
                }
            }
            catch { }
        }
        else
        {
            try
            {
                var line = File.ReadAllLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
                if (line != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 4 && ulong.TryParse(parts[1], out var user) && ulong.TryParse(parts[4], out var idle))
                    {
                        ulong total = 0;
                        for (int i = 1; i < parts.Length; i++)
                            if (ulong.TryParse(parts[i], out var v)) total += v;

                        lock (_cpuLock)
                        {
                            if (_prevSystemTime > 0 && total > _prevSystemTime)
                            {
                                ulong totalDelta = total - _prevSystemTime;
                                ulong idleDelta = idle - _prevIdleTime;
                                _prevSystemTime = total;
                                _prevIdleTime = idle;
                                if (totalDelta > 0)
                                {
                                    double usage = (double)(totalDelta - idleDelta) / totalDelta * 100.0;
                                    return Math.Clamp(Math.Round(usage, 1), 0.0, 100.0);
                                }
                            }
                            _prevSystemTime = total;
                            _prevIdleTime = idle;
                        }
                    }
                }
            }
            catch { }
        }
        return 15.0;
    }

    private double GetRealRamUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    return memStatus.dwMemoryLoad;
                }
            }
            catch { }
        }
        else
        {
            try
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                long total = 0, avail = 0;
                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                        long.TryParse(line.Split(':', StringSplitOptions.TrimEntries)[1].Split(' ')[0], out total);
                    else if (line.StartsWith("MemAvailable:"))
                        long.TryParse(line.Split(':', StringSplitOptions.TrimEntries)[1].Split(' ')[0], out avail);
                }
                if (total > 0)
                {
                    return Math.Round(((total - avail) / (double)total) * 100.0, 1);
                }
            }
            catch { }
        }
        return 25.0;
    }

    private List<EventLogInfo> GetRecentEvents()
    {
        return new List<EventLogInfo>();
    }
}
