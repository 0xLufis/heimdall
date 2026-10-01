namespace App.Backend.Tests;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.Infrastructure.FileSystem;
using App.Agent.Daemon.Infrastructure.Supervisor;
using App.Agent.Daemon.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class DaemonSupervisorAndRecipeWatcherTests : IDisposable
{
    private readonly string _testSandboxDir;

    public DaemonSupervisorAndRecipeWatcherTests()
    {
        _testSandboxDir = Path.Combine(Path.GetTempPath(), "heimdall_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testSandboxDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testSandboxDir))
            {
                Directory.Delete(_testSandboxDir, true);
            }
        }
        catch { }
    }

    private class MockSystemInfoService : ISystemInfoService
    {
        public SystemInfoData GetSystemInfo() => new()
        {
            Hostname = "IPC-STATION-01",
            MachineIdentifier = "TEST-LINE1-OP10"
        };
        public List<App.Shared.Drivers.DeviceDriver> GetInstalledDrivers() => new();
    }

    [Fact]
    public void DaemonSupervisorService_GeneratesAndReadsCrashDumps()
    {
        var config = new AgentConfig
        {
            WatchdogEnabled = true,
            MaxMemoryMb = 512
        };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var sysInfo = new MockSystemInfoService();
        var supervisor = new DaemonSupervisorService(configService, NullLogger<DaemonSupervisorService>.Instance, sysInfo);

        // Generate mock unhandled exit crash dump
        supervisor.GenerateCrashDump(
            "Fatal PLC Memory Overrun",
            new InvalidOperationException("ADS driver memory bus corrupted"),
            exitCode: 139
        );

        var pendingDumps = supervisor.GetPendingCrashDumps();
        Assert.NotEmpty(pendingDumps);

        var lastDump = pendingDumps[pendingDumps.Count - 1];
        Assert.Equal("TEST-LINE1-OP10", lastDump.MachineIdentifier);
        Assert.Equal("IPC-STATION-01", lastDump.Hostname);
        Assert.Equal(139, lastDump.ExitCode);
        Assert.Equal("Fatal PLC Memory Overrun", lastDump.Reason);
        Assert.Equal("System.InvalidOperationException", lastDump.ExceptionType);
    }

    [Fact]
    public void DaemonSupervisorService_CheckMemoryLimit_ReturnsFalseUnderThreshold()
    {
        var config = new AgentConfig
        {
            WatchdogEnabled = true,
            MaxMemoryMb = 8192 // Very large limit
        };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var supervisor = new DaemonSupervisorService(configService, NullLogger<DaemonSupervisorService>.Instance);

        bool withinLimit = supervisor.CheckMemoryLimit();
        Assert.True(withinLimit);
    }

    [Fact]
    public async Task RecipeWatcherService_ComputesAccurateSha256Checksum()
    {
        var config = new AgentConfig();
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var watcherService = new RecipeWatcherService(configService, NullLogger<RecipeWatcherService>.Instance);

        string testFile = Path.Combine(_testSandboxDir, "recipe_op10.xml");
        string fileContent = "<Recipe name='PrecisionFasten' torque='14.5' speed='2200' />";
        await File.WriteAllTextAsync(testFile, fileContent);

        string computedHash = await watcherService.ComputeChecksumSha256Async(testFile);

        using var sha256 = SHA256.Create();
        byte[] expectedHashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fileContent));
        string expectedHex = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        Assert.Equal(expectedHex, computedHash);
    }

    [Fact]
    public async Task RecipeWatcherService_DetectsCryptographicDriftOnModify()
    {
        string watchSubdir = Path.Combine(_testSandboxDir, "recipes");
        Directory.CreateDirectory(watchSubdir);

        var config = new AgentConfig
        {
            RecipeWatchDirectories = new() { watchSubdir }
        };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var sysInfo = new MockSystemInfoService();
        var watcherService = new RecipeWatcherService(configService, NullLogger<RecipeWatcherService>.Instance, sysInfo);

        string recipePath = Path.Combine(watchSubdir, "laser_weld.json");
        await File.WriteAllTextAsync(recipePath, "{\"power\": 1500, \"weld_speed\": 45}");

        await watcherService.StartAsync(CancellationToken.None);

        var baseline = watcherService.GetBaselineChecksums();
        Assert.True(baseline.ContainsKey(recipePath));
        string originalHash = baseline[recipePath];

        // Modify recipe content to simulate parameter drift / unauthorized change
        await File.WriteAllTextAsync(recipePath, "{\"power\": 1850, \"weld_speed\": 55}");
        string updatedHash = await watcherService.ComputeChecksumSha256Async(recipePath);

        Assert.NotEqual(originalHash, updatedHash);

        await watcherService.StopAsync(CancellationToken.None);
    }
}
