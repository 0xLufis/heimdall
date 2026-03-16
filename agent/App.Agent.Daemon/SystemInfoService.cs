using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using App.Shared.Entities;

namespace App.Agent.Daemon;

public class SystemInfoService
{
    public ClientPc GetSystemInfo()
    {
        return new ClientPc
        {
            Hostname = Environment.MachineName,
            MachineIdentifier = GetMachineIdentifier(),
            MacAddress = GetMacAddress(),
            LastOnline = DateTimeOffset.UtcNow,
            HardwareConfig = GetHardwareConfig(),
            SoftwareConfig = GetSoftwareConfig()
        };
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

    private HardwareConfig GetHardwareConfig()
    {
        var config = new HardwareConfig();

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

    private SoftwareConfig GetSoftwareConfig()
    {
        return new SoftwareConfig
        {
            OsVersion = RuntimeInformation.OSDescription,
            InstalledPackages = GetInstalledPackages()
        };
    }

    private List<string> GetInstalledPackages()
    {
        // TODO: Implement real package scanning (e.g. Registry on Windows, dpkg/rpm on Linux)
        var packages = new List<string>();
        
        // This is a placeholder for real FS logic
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
            // On Linux, maybe check /usr/bin or something
            if (Directory.Exists("/usr/bin"))
            {
                 packages.AddRange(Directory.GetFiles("/usr/bin").Select(Path.GetFileName).Take(10)!);
            }
        }

        return packages;
    }
}
