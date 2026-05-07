using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
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
        var packages = new HashSet<string>();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                string[] registryKeys = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var keyPath in registryKeys)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            using var subkey = key.OpenSubKey(subkeyName);
                            var name = subkey?.GetValue("DisplayName")?.ToString();
                            if (!string.IsNullOrEmpty(name)) packages.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error reading Windows registry for packages"); }
        }
        else
        {
            // Try common linux package managers
            try
            {
                if (File.Exists("/usr/bin/dpkg"))
                {
                    // This is a simplified version, ideally run 'dpkg-query -l'
                    packages.Add("dpkg-based system");
                }
                else if (File.Exists("/usr/bin/rpm"))
                {
                    packages.Add("rpm-based system");
                }
            }
            catch { }
        }

        return packages.OrderBy(p => p).ToList();
    }

    private List<EventLogInfo> GetRecentEvents()
    {
        var events = new List<EventLogInfo>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // In a real scenario, we'd query EventLog specifically for BSOD (System Error 1001) or Update errors
                // This is a placeholder for that logic
            }
            catch { }
        }
        return events;
    }
}

