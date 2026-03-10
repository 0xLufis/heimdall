using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using App.Shared.Entities;

namespace App.Agent.Daemon;

public class SystemInfoService
{
    public ClientPc GetSystemInfo()
    {
        var pc = new ClientPc
        {
            Hostname = Environment.MachineName,
            MachineIdentifier = GetMachineIdentifier(),
            MacAddress = GetMacAddress(),
            LastOnline = DateTimeOffset.UtcNow,
            HardwareConfig = GetHardwareConfig(),
            SoftwareConfig = GetSoftwareConfig()
        };

        return pc;
    }

    private string GetMachineIdentifier()
    {
        // TODO: Implement more robust machine identifier (e.g. UUID from BIOS)
        return $"{Environment.MachineName}-{Environment.OSVersion}";
    }

    private string GetMacAddress()
    {
        // TODO: Implement real MAC address retrieval using NetworkInterface
        return "00:00:00:00:00:00";
    }

    private HardwareConfig GetHardwareConfig()
    {
        var config = new HardwareConfig();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // TODO: Implement storage info gathering
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    config.Cpu = obj["Name"]?.ToString() ?? "Unknown CPU";
                }

                using var memSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in memSearcher.Get())
                {
                    var totalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    config.Ram = $"{totalMemory / (1024 * 1024 * 1024)} GB";
                }
            }
            catch
            {
                config.Cpu = "Error querying WMI";
                config.Ram = "Error querying WMI";
            }
        }
        else
        {
            config.Cpu = "Linux/Other CPU";
            config.Ram = "Linux/Other RAM";
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
