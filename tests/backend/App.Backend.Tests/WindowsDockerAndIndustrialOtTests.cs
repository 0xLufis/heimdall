namespace App.Backend.Tests;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using App.Agent.Daemon.Domain.Recipes;
using App.Agent.Daemon.Infrastructure.Beckhoff;
using App.Agent.Daemon.Infrastructure.Opc;
using App.Agent.Daemon.Reporting.Triggers;
using App.Agent.Daemon;

public class WindowsDockerAndIndustrialOtTests
{
    [Fact]
    public void AdsSimulationServer_ReadDeviceInfo_ReturnsTwinCAT3RuntimeInfo()
    {
        // Arrange
        using var server = new AdsSimulationServer(port: 49991);
        
        // Build ADS Request Packet for ADSCMD_READ_DEVICE_INFO (0x0001)
        var request = new byte[32]; // 32-byte AMS Header with 0 payload
        byte[] targetNetId = new byte[] { 5, 80, 201, 44, 1, 1 };
        byte[] sourceNetId = new byte[] { 192, 168, 1, 50, 1, 1 };
        
        targetNetId.CopyTo(request.AsSpan(0, 6));
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6, 2), 851);
        sourceNetId.CopyTo(request.AsSpan(8, 6));
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(14, 2), 10000);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(16, 2), AdsSimulationServer.ADSCMD_READ_DEVICE_INFO);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(18, 2), 0x0000); // Request
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(20, 4), 0);      // DataLength
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(24, 4), 0);      // ErrorCode
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(28, 4), 12345);  // InvokeId

        // Act
        byte[] response = server.ProcessAmsPacket(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Length >= 32 + 24);

        // Verify AMS Header in response
        ushort responseCmd = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(16, 2));
        ushort responseFlags = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(18, 2));
        uint responseInvokeId = BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(28, 4));
        
        Assert.Equal(AdsSimulationServer.ADSCMD_READ_DEVICE_INFO, responseCmd);
        Assert.Equal(0x0001, responseFlags); // Response flag set
        Assert.Equal(12345u, responseInvokeId);

        // Verify Device Info Payload (Major 3, Minor 1, Build 4026)
        ReadOnlySpan<byte> payload = response.AsSpan(32);
        uint adsResult = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(0, 4));
        byte major = payload[4];
        byte minor = payload[5];
        ushort build = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(6, 2));
        string deviceName = Encoding.ASCII.GetString(payload.Slice(8, 16)).TrimEnd('\0');

        Assert.Equal(0u, adsResult);
        Assert.Equal(3, major);
        Assert.Equal(1, minor);
        Assert.Equal(4026, build);
        Assert.Contains("TwinCAT 3.1 PLC", deviceName);
    }

    [Fact]
    public void AdsSimulationServer_ReadState_ReturnsAdsStateRun()
    {
        // Arrange
        using var server = new AdsSimulationServer(port: 49992);
        server.CurrentAdsState = AdsSimulationServer.ADSSTATE_RUN;

        var request = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(16, 2), AdsSimulationServer.ADSCMD_READ_STATE);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(28, 4), 999);

        // Act
        byte[] response = server.ProcessAmsPacket(request);

        // Assert
        ReadOnlySpan<byte> payload = response.AsSpan(32);
        uint result = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(0, 4));
        ushort state = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(4, 2));

        Assert.Equal(0u, result);
        Assert.Equal(AdsSimulationServer.ADSSTATE_RUN, state);
    }

    [Fact]
    public void AdsSimulationServer_WriteControl_TransitionsAdsState()
    {
        // Arrange
        using var server = new AdsSimulationServer(port: 49993);
        Assert.Equal(AdsSimulationServer.ADSSTATE_RUN, server.CurrentAdsState);

        // Build Write Control payload to request ADSSTATE_STOP (6)
        var writeData = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(writeData.AsSpan(0, 2), AdsSimulationServer.ADSSTATE_STOP);
        BinaryPrimitives.WriteUInt16LittleEndian(writeData.AsSpan(2, 2), 0);

        var request = new byte[32 + writeData.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(16, 2), AdsSimulationServer.ADSCMD_WRITE_CONTROL);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(20, 4), (uint)writeData.Length);
        writeData.CopyTo(request.AsSpan(32));

        // Act
        byte[] response = server.ProcessAmsPacket(request);

        // Assert
        Assert.Equal(AdsSimulationServer.ADSSTATE_STOP, server.CurrentAdsState);
        uint result = BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(32, 4));
        Assert.Equal(0u, result);
    }

    [Fact]
    public async Task MinimalOpcClient_PollOnce_PopulatesMonitoredIndustrialNodes()
    {
        // Arrange
        using var client = new MinimalOpcClient("opc.tcp://127.0.0.1:4840");

        // Act
        await client.PollOnceAsync();

        // Assert
        Assert.True(client.TotalPollCycles > 0);
        Assert.True(client.IsConnected);
        Assert.True(client.MonitoredNodes.ContainsKey("ns=2;s=Line01.DriveSpeed"));
        Assert.True(client.MonitoredNodes.ContainsKey("ns=2;s=Line01.MotorCurrent"));
        Assert.True(client.MonitoredNodes.ContainsKey("ns=2;s=Line01.QualityOk"));
        Assert.True(client.MonitoredNodes.ContainsKey("ns=2;s=Line01.PartCount"));

        var speed = Convert.ToDouble(client.MonitoredNodes["ns=2;s=Line01.DriveSpeed"]);
        Assert.InRange(speed, 1400.0, 1600.0);
    }

    [Fact]
    public void TelemetryTriggerEngine_HeartbeatTrigger_FiresWhenIntervalExceeded()
    {
        // Arrange
        var engine = new TelemetryTriggerEngine();
        var context = new TriggerContext
        {
            LastReportedTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            CurrentAdsState = AdsSimulationServer.ADSSTATE_RUN,
            PreviousAdsState = AdsSimulationServer.ADSSTATE_RUN
        };

        // Act
        var result = engine.Evaluate(context);

        // Assert
        Assert.True(result.Triggered);
        Assert.Contains("Heartbeat", result.Reason);
        Assert.Equal(EgressPriority.P2_MediumMetrics, result.Priority);
    }

    [Fact]
    public void TelemetryTriggerEngine_ThresholdTrigger_EscalatesToP0OnDiskDepletion()
    {
        // Arrange
        var engine = new TelemetryTriggerEngine();
        var sysInfo = new SystemInfoData
        {
            Disk = new DiskData { OsDriveFreeGB = 3.2 } // Under 5.0 GB threshold
        };
        var context = new TriggerContext
        {
            CurrentSystemInfo = sysInfo,
            LastReportedTime = DateTimeOffset.UtcNow, // Heartbeat not expired
            CurrentAdsState = AdsSimulationServer.ADSSTATE_RUN,
            PreviousAdsState = AdsSimulationServer.ADSSTATE_RUN
        };

        // Act
        var result = engine.Evaluate(context);

        // Assert
        Assert.True(result.Triggered);
        Assert.Equal(EgressPriority.P0_CriticalAlarm, result.Priority);
        Assert.Contains("CRITICAL: OS Drive free space depleted", result.Reason);
        Assert.True(result.SlicesToReport.IncludeDisk);
    }

    [Fact]
    public void TelemetryTriggerEngine_StateChangeTrigger_DetectsTwinCatStopTransition()
    {
        // Arrange
        var engine = new TelemetryTriggerEngine();
        var context = new TriggerContext
        {
            PreviousAdsState = AdsSimulationServer.ADSSTATE_RUN,
            CurrentAdsState = AdsSimulationServer.ADSSTATE_STOP,
            LastReportedTime = DateTimeOffset.UtcNow
        };

        // Act
        var result = engine.Evaluate(context);

        // Assert
        Assert.True(result.Triggered);
        Assert.Equal(EgressPriority.P0_CriticalAlarm, result.Priority);
        Assert.Contains("RUN (5) to STOP (6)", result.Reason);
        Assert.True(result.SlicesToReport.IncludePlcTelemetry);
    }

    [Fact]
    public void TelemetryTriggerEngine_OnDemandTrigger_FiresImmediatelyWithFullSnapshot()
    {
        // Arrange
        var engine = new TelemetryTriggerEngine();
        var context = new TriggerContext
        {
            ForceReport = true,
            ForceReason = "Operator manual dispatch",
            LastReportedTime = DateTimeOffset.UtcNow,
            CurrentAdsState = AdsSimulationServer.ADSSTATE_RUN,
            PreviousAdsState = AdsSimulationServer.ADSSTATE_RUN
        };

        // Act
        var result = engine.Evaluate(context);

        // Assert
        Assert.True(result.Triggered);
        Assert.Contains("Operator manual dispatch", result.Reason);
        Assert.True(result.SlicesToReport.IncludeHardware);
        Assert.True(result.SlicesToReport.IncludeSoftware);
        Assert.True(result.SlicesToReport.IncludePlcTelemetry);
    }

    [Fact]
    public void WindowsDockerfileAndSetupPs1_ContainTwinCatAndOpcSubsystems()
    {
        // Find repo root dynamically
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !Directory.Exists(Path.Combine(dir, "infra", "windows")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        string repoRoot = dir;
        string dockerfilePath = Path.Combine(repoRoot, "infra", "windows", "Dockerfile");
        string setupPs1Path = Path.Combine(repoRoot, "infra", "windows", "oem", "setup.ps1");
        string trayPs1Path = Path.Combine(repoRoot, "infra", "windows", "tray", "HeimdallTrayRunner.ps1");

        // Assert Dockerfile exposes 48898 (ADS) and 4840 (OPC UA)
        Assert.True(File.Exists(dockerfilePath), $"Dockerfile not found at {dockerfilePath}");
        string dockerfileContent = File.ReadAllText(dockerfilePath);
        Assert.Contains("48898", dockerfileContent);
        Assert.Contains("4840", dockerfileContent);

        // Assert setup.ps1 contains firewall rules and Beckhoff TwinCAT OT registry keys
        Assert.True(File.Exists(setupPs1Path), $"setup.ps1 not found at {setupPs1Path}");
        string setupContent = File.ReadAllText(setupPs1Path);
        Assert.Contains("Heimdall_TwinCAT_ADS", setupContent);
        Assert.Contains("48898", setupContent);
        Assert.Contains("Heimdall_OPC_UA", setupContent);
        Assert.Contains("4840", setupContent);
        Assert.Contains("HKLM:\\SOFTWARE\\Beckhoff\\TwinCAT3", setupContent);
        Assert.Contains("EK1100", setupContent);
        Assert.Contains("EL1008", setupContent);
        Assert.Contains("EL3104", setupContent);
        Assert.Contains("HeimdallTrayRunner.ps1", setupContent);

        // Assert HeimdallTrayRunner.ps1 exists and has context menu items
        Assert.True(File.Exists(trayPs1Path), $"HeimdallTrayRunner.ps1 not found at {trayPs1Path}");
        string trayContent = File.ReadAllText(trayPs1Path);
        Assert.Contains("Open Agent Web Dashboard", trayContent);
        Assert.Contains("Trigger Immediate Telemetry Report", trayContent);
        Assert.Contains("Toggle TwinCAT ADS State", trayContent);
        Assert.Contains("http://localhost:5998", trayContent);
    }

    [Fact]
    public async Task MinimalOpcServer_HelHandshake_ReturnsAckfPacket()
    {
        int testPort = 49994;
        using var server = new MinimalOpcServer(port: testPort);
        server.Start();
        Assert.True(server.IsListening);

        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync("127.0.0.1", testPort);
        using var stream = client.GetStream();

        // Send HELF (32 bytes)
        var hel = new byte[32];
        hel[0] = (byte)'H';
        hel[1] = (byte)'E';
        hel[2] = (byte)'L';
        hel[3] = (byte)'F';
        BinaryPrimitives.WriteUInt32LittleEndian(hel.AsSpan(4, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(hel.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(hel.AsSpan(12, 4), 65535);
        BinaryPrimitives.WriteUInt32LittleEndian(hel.AsSpan(16, 4), 65535);
        BinaryPrimitives.WriteUInt32LittleEndian(hel.AsSpan(20, 4), 16777216);
        BinaryPrimitives.WriteUInt32LittleEndian(hel.AsSpan(24, 4), 5000);

        await stream.WriteAsync(hel);
        await stream.FlushAsync();

        // Read ACKF response (28 bytes)
        var ack = new byte[28];
        int total = 0;
        using var cts = new System.Threading.CancellationTokenSource(3000);
        while (total < 28)
        {
            int r = await stream.ReadAsync(ack.AsMemory(total, 28 - total), cts.Token);
            Assert.True(r > 0, "Server closed stream unexpectedly");
            total += r;
        }

        Assert.Equal(28, total);
        string ackType = Encoding.ASCII.GetString(ack, 0, 4);
        Assert.Equal("ACKF", ackType);
        uint msgSize = BinaryPrimitives.ReadUInt32LittleEndian(ack.AsSpan(4, 4));
        Assert.Equal(28u, msgSize);
    }

    [Fact]
    public void LiveTelemetryComponentContributor_ProducesTelemetryComponentWithMetrics()
    {
        var contributor = new App.Agent.Daemon.Reporting.LiveTelemetryComponentContributor();
        var sysInfo = new SystemInfoData
        {
            LiveTelemetry = new LiveTelemetryData
            {
                CpuLoad = "23.5%",
                CpuUsagePercent = 23.5,
                RamUsage = "45.0%",
                RamUsagePercent = 45.0,
                Status = "Online"
            }
        };

        var component = contributor.CreateComponent(sysInfo);
        Assert.NotNull(component);
        Assert.Equal("Live Telemetry", component.Name);
        Assert.Equal("telemetry", component.Type);
        Assert.Contains("23.5%", component.DataJson);
        Assert.Contains("45.0%", component.DataJson);
    }

    [Fact]
    public void SystemInfoReporter_MergesAllBaseContributorsAndInjectedContributors()
    {
        var configService = new ConfigurationService(Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigurationService>.Instance);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemInfoReporter>.Instance;

        // Injected contributor (e.g. IndustrialOtComponentContributor)
        var injected = new List<App.Agent.Daemon.Reporting.IComponentContributor>
        {
            new App.Agent.Daemon.Reporting.IndustrialOtComponentContributor()
        };

        var reporter = new SystemInfoReporter(logger, configService, injected);

        // Ensure contributors list retained base contributors plus injected
        var contributors = reporter.Contributors;
        Assert.Contains(contributors, c => c is App.Agent.Daemon.Reporting.HardwareComponentContributor);
        Assert.Contains(contributors, c => c is App.Agent.Daemon.Reporting.SoftwareComponentContributor);
        Assert.Contains(contributors, c => c is App.Agent.Daemon.Reporting.LiveTelemetryComponentContributor);
        Assert.Contains(contributors, c => c is App.Agent.Daemon.Reporting.IndustrialOtComponentContributor);
    }
}
