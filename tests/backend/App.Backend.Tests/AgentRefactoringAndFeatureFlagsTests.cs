using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.CommandHandling;
using App.Agent.Daemon.Infrastructure.Beckhoff;
using App.Agent.Daemon.Infrastructure.Drivers;
using App.Agent.Daemon.Infrastructure.FileSystem;
using App.Agent.Daemon.Infrastructure.Opc;
using App.Agent.Daemon.Interfaces;
using App.Agent.Daemon.Reporting.Triggers;
using App.Contracts.Configuration;
using App.Contracts.Enums;
using App.Shared.Errors;
using App.Shared.Protos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class AgentRefactoringAndFeatureFlagsTests
{
    [Fact]
    public void AgentFeatureFlags_DefaultsToFalse_InProduction()
    {
        var flags = AgentFeatureFlags.FromEnvironment("Production");
        Assert.False(flags.EnableDevFeatures);
        Assert.False(flags.EnableDebugFeatures);
    }

    [Fact]
    public void AgentFeatureFlags_EnablesDevFeatures_WhenEnvironmentIsDevelopment()
    {
        var flags = AgentFeatureFlags.FromEnvironment("Development");
        Assert.True(flags.EnableDevFeatures);
        Assert.False(flags.EnableDebugFeatures);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    public void AgentFeatureFlags_IsTruthy_ParsesCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, AgentFeatureFlags.IsTruthy(input));
    }

    [Fact]
    public void AgentFeatureFlags_FromProvider_CascadesSettingsProperly()
    {
        var dict = new Dictionary<string, string?>
        {
            ["AgentFeatureFlags:EnableDevFeatures"] = "true",
            ["AgentFeatureFlags:EnableDebugFeatures"] = "true"
        };

        var flags = AgentFeatureFlags.FromProvider(key => dict.GetValueOrDefault(key), "Production");
        Assert.True(flags.EnableDevFeatures);
        Assert.True(flags.EnableDebugFeatures);
    }

#pragma warning disable CA1416 // Testing Windows PInvoke constants across cross-platform test runner
    [Fact]
    public void SetupApiNative_DeviceClassGuids_MatchStandardMicrosoftConstants()
    {
        Assert.Equal(new Guid("{4d36e972-e325-11ce-bfc1-08002be10318}"), SetupApiNative.DeviceSetupClasses.Net);
        Assert.Equal(new Guid("{4d36e97d-e325-11ce-bfc1-08002be10318}"), SetupApiNative.DeviceSetupClasses.System);
        Assert.Equal(new Guid("{4d36e978-e325-11ce-bfc1-08002be10318}"), SetupApiNative.DeviceSetupClasses.Ports);
        Assert.Equal(new Guid("{36fc9e60-c465-11cf-8056-444553540000}"), SetupApiNative.DeviceSetupClasses.Usb);
        Assert.Equal(new Guid("{86e0d1e0-8089-11d0-9ce4-08003e301f73}"), SetupApiNative.DeviceInterfaceClasses.ComPort);

        // Verify backward-compatibility aliases
        Assert.Equal(SetupApiNative.DeviceSetupClasses.Net, SetupApiNative.GUID_DEVCLASS_NET);
        Assert.Equal(SetupApiNative.DeviceSetupClasses.System, SetupApiNative.GUID_DEVCLASS_SYSTEM);
        Assert.Equal(SetupApiNative.DeviceInterfaceClasses.ComPort, SetupApiNative.GUID_DEVINTERFACE_COMPORT);
    }
#pragma warning restore CA1416

    [Fact]
    public void FileSystemScanner_DiscoversVersionedSiemensTemplates_ByDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"heimdall_fs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Siemens dynamic versions: zal20, ap20, zap21
            File.WriteAllText(Path.Combine(tempDir, "Project_L1.zal20"), "content1");
            File.WriteAllText(Path.Combine(tempDir, "Project_L2.ap20"), "content2");
            File.WriteAllText(Path.Combine(tempDir, "Project_L3.zap21"), "content3");
            File.WriteAllText(Path.Combine(tempDir, "ignore.randomext"), "content4");

            var scanner = new FileSystemScanner();
            var assets = scanner.ScanDirectory(tempDir).ToList();

            var names = assets.Select(a => a.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Project_L1.zal20", names);
            Assert.Contains("Project_L2.ap20", names);
            Assert.Contains("Project_L3.zap21", names);
            Assert.DoesNotContain("ignore.randomext", names);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FileSystemScanner_RespectsCustomBannedExtensions_AndAllowedTemplates()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"heimdall_fs_custom_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "config.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "banned.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "special_recipe.custom10"), "recipe");
            File.WriteAllText(Path.Combine(tempDir, "unwanted.badext"), "bad");

            var options = new FileSystemScannerOptions
            {
                CustomBannedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "banned.json" },
                CustomBannedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".badext" },
                AllowedFileNamePatterns = new List<string> { @"^special_recipe\.custom\d+$" }
            };

            var scanner = new FileSystemScanner(options);
            var assets = scanner.ScanDirectory(tempDir).ToList();

            var names = assets.Select(a => a.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("config.json", names);
            Assert.Contains("special_recipe.custom10", names);
            Assert.DoesNotContain("banned.json", names);
            Assert.DoesNotContain("unwanted.badext", names);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CommandHandler_GuardsDiagnosticDumps_UnderFeatureFlags()
    {
        var config = new AgentConfig { AllowRemoteExecution = true, AllowUnsignedCommands = true };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var fileScanner = new FileSystemScanner();

        // Production flags (EnableDebugFeatures = false)
        var prodFlags = new AgentFeatureFlags { EnableDebugFeatures = false };
        var prodHandler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance, featureFlags: prodFlags);

        var dumpCommand = new ServerCommand
        {
            Type = "DIAGNOSTIC_DUMP",
            Payload = "{\"target\": \"memory\"}",
            Signature = "sig"
        };

        var prodResult = await prodHandler.HandleCommandAsync(dumpCommand);
        Assert.False(prodResult.Success);
        Assert.Equal(ErrorCode.RemoteExecutionDisabled, prodResult.ErrorCode);

        // Debug flags enabled (EnableDebugFeatures = true)
        var debugFlags = new AgentFeatureFlags { EnableDebugFeatures = true };
        var debugHandler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance, featureFlags: debugFlags);

        var debugResult = await debugHandler.HandleCommandAsync(dumpCommand);
        Assert.True(debugResult.Success);
        Assert.Equal(ErrorCode.None, debugResult.ErrorCode);
    }

    [Fact]
    public async Task Worker_GuardsMockSimulationServers_WhenDevFeaturesDisabled()
    {
        var prodFlags = new AgentFeatureFlags { EnableDevFeatures = false };
        var mockAds = new MockAdsServer();
        var mockOpc = new MockOpcServer();

        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig());
        var fileScanner = new FileSystemScanner();
        var cmdHandler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance, featureFlags: prodFlags);
        var sysInfo = new MockSystemInfoService();

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            sysInfo,
            new MockReporter(),
            cmdHandler,
            adsServer: mockAds,
            opcServer: mockOpc,
            featureFlags: prodFlags
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await worker.StartAsync(cts.Token);
        await Task.Delay(60);
        await worker.StopAsync(CancellationToken.None);

        // In production mode, mock ADS and OPC servers should NOT have been started
        Assert.False(mockAds.WasStarted);
        Assert.False(mockOpc.WasStarted);
    }

    [Fact]
    public async Task Worker_StartsMockSimulationServers_WhenDevFeaturesEnabled()
    {
        var devFlags = new AgentFeatureFlags { EnableDevFeatures = true };
        var mockAds = new MockAdsServer();
        var mockOpc = new MockOpcServer();

        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig());
        var fileScanner = new FileSystemScanner();
        var cmdHandler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance, featureFlags: devFlags);
        var sysInfo = new MockSystemInfoService();

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            sysInfo,
            new MockReporter(),
            cmdHandler,
            adsServer: mockAds,
            opcServer: mockOpc,
            featureFlags: devFlags
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await worker.StartAsync(cts.Token);
        await Task.Delay(60);
        await worker.StopAsync(CancellationToken.None);

        // In dev mode, mock ADS and OPC servers SHOULD have been started
        Assert.True(mockAds.WasStarted);
        Assert.True(mockOpc.WasStarted);
    }

    private class MockSystemInfoService : ISystemInfoService
    {
        public SystemInfoData GetSystemInfo() => new SystemInfoData();
        public List<App.Shared.Drivers.DeviceDriver> GetInstalledDrivers() => new();
    }

    private class MockAdsServer : IAdsSimulationServer
    {
        public bool WasStarted { get; private set; }
        public string AmsNetId => "127.0.0.1.1.1";
        public ushort AmsPort => 851;
        public int Port => 48898;
        public ushort CurrentAdsState { get; set; } = AdsSimulationServer.ADSSTATE_RUN;
        public bool IsListening => WasStarted;
        public long TotalRequestsHandled => 0;
        public System.Collections.Concurrent.ConcurrentDictionary<string, object> SimulatedVariables => new();

        public void Start() => WasStarted = true;
        public void Stop() => WasStarted = false;
        public void Dispose() { }
    }

    private class MockOpcServer : IMinimalOpcServer
    {
        public bool WasStarted { get; private set; }
        public int Port => 4840;
        public string EndpointUrl => $"opc.tcp://127.0.0.1:{Port}";
        public bool IsListening => WasStarted;
        public long TotalConnectionsHandled => 0;
        public System.Collections.Concurrent.ConcurrentDictionary<string, object> ServerNodes => new();

        public void Start() => WasStarted = true;
        public void Stop() => WasStarted = false;
        public void Dispose() { }
    }

    private class MockReporter : ISystemInfoReporter
    {
        public Task<SystemInfoResponse?> ReportInfoAsync(SystemInfoData data)
            => Task.FromResult<SystemInfoResponse?>(new SystemInfoResponse { Success = true });

        public IConfigurationService GetConfigService()
            => new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig());

        public Task<SystemInfoResponse?> TriggerSyncAsync()
            => Task.FromResult<SystemInfoResponse?>(new SystemInfoResponse { Success = true });
    }
}
