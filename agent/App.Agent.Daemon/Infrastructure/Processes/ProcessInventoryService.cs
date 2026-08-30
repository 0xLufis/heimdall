namespace App.Agent.Daemon.Infrastructure.Processes;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using App.Agent.Daemon.Infrastructure.Security;

public record ProcessSnapshot(
    int Pid,
    string ProcessName,
    string ExecutablePath,
    string CommandLine,
    double CpuPercent,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    DateTime? StartTimeUtc,
    bool IsProtectedOrSystem);

/// <summary>
/// Enumerate running OS processes, computes delta CPU %, and redacts secrets/PII.
/// </summary>
public sealed class ProcessInventoryService
{
    private readonly ConcurrentDictionary<int, ProcessSampleState> _previousSamples = new();

    private sealed record ProcessSampleState(DateTime SampleUtcTime, TimeSpan TotalProcessorTime, DateTime ProcessStartTime);

    public List<ProcessSnapshot> CaptureRunningProcesses()
    {
        var result = new List<ProcessSnapshot>();
        var currentPids = new HashSet<int>();
        var now = DateTime.UtcNow;
        int processorCount = Math.Max(1, Environment.ProcessorCount);

        var processes = Process.GetProcesses();

        foreach (var proc in processes)
        {
            using (proc)
            {
                int pid = proc.Id;
                currentPids.Add(pid);
                string procName = proc.ProcessName;

                long workingSet = 0;
                long privateBytes = 0;
                DateTime startTime = DateTime.MinValue;
                TimeSpan totalCpuTime = TimeSpan.Zero;
                string executablePath = "[Access Denied]";
                string commandLine = string.Empty;
                bool isElevatedOrSystem = false;

                // 1. Safe Memory Metrics
                try
                {
                    workingSet = proc.WorkingSet64;
                    privateBytes = proc.PrivateMemorySize64;
                }
                catch { /* Ignored */ }

                // 2. Safe Timing Metrics
                try
                {
                    startTime = proc.StartTime;
                    totalCpuTime = proc.TotalProcessorTime;
                }
                catch
                {
                    isElevatedOrSystem = true;
                }

                // 3. Safe Module / Exe Path Extraction
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    executablePath = GetWindowsExecutablePath(proc, pid);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    executablePath = GetLinuxExecutablePath(pid);
                    commandLine = GetLinuxCommandLine(pid);
                }

                // 4. Calculate Delta CPU %
                double cpuPercent = 0.0;
                if (!isElevatedOrSystem && totalCpuTime > TimeSpan.Zero)
                {
                    if (_previousSamples.TryGetValue(pid, out var oldState) && oldState.ProcessStartTime == startTime)
                    {
                        var timeDeltaMs = (now - oldState.SampleUtcTime).TotalMilliseconds;
                        var cpuDeltaMs = (totalCpuTime - oldState.TotalProcessorTime).TotalMilliseconds;

                        if (timeDeltaMs > 100)
                        {
                            cpuPercent = Math.Clamp((cpuDeltaMs / (timeDeltaMs * processorCount)) * 100.0, 0.0, 100.0);
                        }
                    }

                    _previousSamples[pid] = new ProcessSampleState(now, totalCpuTime, startTime);
                }

                // 5. Scrub PII and Secrets from Command Line
                string sanitizedCommandLine = ProcessSecretScrubber.Scrub(commandLine);

                result.Add(new ProcessSnapshot(
                    Pid: pid,
                    ProcessName: procName,
                    ExecutablePath: executablePath,
                    CommandLine: sanitizedCommandLine,
                    CpuPercent: Math.Round(cpuPercent, 2),
                    WorkingSetBytes: workingSet,
                    PrivateMemoryBytes: privateBytes,
                    StartTimeUtc: startTime == DateTime.MinValue ? null : startTime.ToUniversalTime(),
                    IsProtectedOrSystem: isElevatedOrSystem
                ));
            }
        }

        // Cleanup stale dead processes from cache
        foreach (var cachedPid in _previousSamples.Keys)
        {
            if (!currentPids.Contains(cachedPid))
            {
                _previousSamples.TryRemove(cachedPid, out _);
            }
        }

        return result;
    }

    private static string GetWindowsExecutablePath(Process proc, int pid)
    {
        try
        {
            return proc.MainModule?.FileName ?? "[Unknown]";
        }
        catch
        {
            return "[Protected Process / Access Denied]";
        }
    }

    private static string GetLinuxExecutablePath(int pid)
    {
        try
        {
            string exeLink = $"/proc/{pid}/exe";
            if (File.Exists(exeLink))
            {
                return new FileInfo(exeLink).LinkTarget ?? exeLink;
            }
        }
        catch { }
        return "[Access Denied]";
    }

    private static string GetLinuxCommandLine(int pid)
    {
        try
        {
            string cmdPath = $"/proc/{pid}/cmdline";
            if (File.Exists(cmdPath))
            {
                var bytes = File.ReadAllBytes(cmdPath);
                if (bytes.Length == 0) return string.Empty;
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0) bytes[i] = (byte)' ';
                }
                return Encoding.UTF8.GetString(bytes).Trim();
            }
        }
        catch { }
        return string.Empty;
    }
}
