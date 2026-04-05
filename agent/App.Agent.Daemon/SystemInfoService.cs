using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

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
}

public class DiskData
{
    public double TotalFreeGB { get; set; }
    public double OsDriveFreeGB { get; set; }
    public Dictionary<string, double> Drives { get; set; } = new();
}

public class HardwareInfo
{
    public string Cpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
}

public class SoftwareInfo
{
    public string OsVersion { get; set; } = string.Empty;
    public List<string> InstalledPackages { get; set; } = new();
}

public class SystemInfoService
{
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
            Disk = GetDiskConfig()
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

                // Identify OS drive
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
        }
        catch { }
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
                // CPU Info
                using var cpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in cpuSearcher.Get())
                {
                    config.Cpu = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                }

                // RAM Info
                using var memSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in memSearcher.Get())
                {
                    var totalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    config.Ram = $"{Math.Round(totalMemory / (1024.0 * 1024.0 * 1024.0), 0)} GB";
                }

                // Storage Info
                using var diskSearcher = new ManagementObjectSearcher("SELECT Size FROM Win32_LogicalDisk WHERE DeviceID = 'C:'");
                foreach (var obj in diskSearcher.Get())
                {
                    var size = Convert.ToInt64(obj["Size"]);
                    config.Storage = $"{Math.Round(size / (1024.0 * 1024.0 * 1024.0), 0)} GB (C:)";
                }
            }
            catch (Exception ex)
            {
                config.Cpu = $"Error querying WMI: {ex.Message}";
                config.Ram = "Error querying WMI";
                config.Storage = "Error querying WMI";
            }
        }
        else
        {
            config.Cpu = "Linux/Other CPU";
            config.Ram = "Linux/Other RAM";
            config.Storage = "Linux/Other Storage";
        }

        return config;
    }

    private SoftwareInfo GetSoftwareConfig()
    {
        return new SoftwareInfo
        {
            OsVersion = RuntimeInformation.OSDescription,
            InstalledPackages = GetInstalledPackages()
        };
    }

    private List<string> GetInstalledPackages()
    {
        var packages = new List<string>();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (Directory.Exists(programFiles))
            {
                packages.AddRange(Directory.GetDirectories(programFiles).Select(Path.GetFileName).Take(10)!);
            }
        }
        else
        {
            if (Directory.Exists("/usr/bin"))
            {
                 packages.AddRange(Directory.GetFiles("/usr/bin").Select(Path.GetFileName).Take(10)!);
            }
        }

        return packages;
    }
}
