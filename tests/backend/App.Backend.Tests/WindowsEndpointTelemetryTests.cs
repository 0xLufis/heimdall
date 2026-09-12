using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.Backend.Tests;

public class WindowsEndpointTelemetryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WindowsEndpointTelemetryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReportSystemInfo_WindowsPayload_PersistsWindowsInvariantsAndTelemetry()
    {
        // Arrange
        var client = _factory.CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client
        });
        var grpcClient = new SystemInfoCollector.SystemInfoCollectorClient(channel);

        const string windowsHostname = "WIN-LTSC-OT01";
        const string windowsUuid = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890";
        const string windowsMac = "00:15:5D:8A:2F:99";

        var request = new SystemInfoRequest
        {
            Hostname = windowsHostname,
            MachineIdentifier = windowsUuid,
            MacAddress = windowsMac,
            LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            DiskInfo = new DiskInfo
            {
                TotalFreeGb = 18.5,
                OsDriveFreeGb = 14.2
            }
        };
        request.DiskInfo.Drives.Add("C:\\", 14.2);
        request.DiskInfo.Drives.Add("D:\\", 4.3);

        // 1. Hardware component (Win32_Processor, Win32_BaseBoard)
        request.Components.Add(new InventoryComponent
        {
            Name = "Hardware",
            Technology = "Agent",
            Type = "hardware",
            DataJson = JsonSerializer.Serialize(new
            {
                Cpu = "Intel(R) Core(TM) i5-11320H @ 3.20GHz",
                Ram = "3221225472",
                Motherboard = "Beckhoff Industrial PC C6030",
                NetworkAdapters = new[] { "Ethernet (Intel I210 Gigabit)" }
            })
        });

        // 2. Windows OS Environment component (Registry & Version)
        request.Components.Add(new InventoryComponent
        {
            Name = "OS Environment",
            Technology = "Agent",
            Type = "software",
            DataJson = JsonSerializer.Serialize(new
            {
                OsVersion = "Microsoft Windows 10 Enterprise LTSC 2021",
                Domain = "WORKGROUP",
                InstalledPackages = new[]
                {
                    "TwinCAT 3.1",
                    ".NET 10.0.11 Runtime (x64)",
                    "OpenSSH Server"
                }
            })
        });

        // 3. Beckhoff RT Driver component (Win32_PnPEntity)
        request.Components.Add(new InventoryComponent
        {
            Name = "Beckhoff RT Drivers",
            Technology = "Agent",
            Type = "driver",
            DataJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    DeviceName = "TwinCAT Real-Time Driver",
                    Service = "TcRTime",
                    DriverVersion = "3.1.4024.55",
                    Provider = "Beckhoff Automation GmbH",
                    Status = "Running",
                    IsBound = true
                }
            })
        });

        // 4. Windows EventLog records
        request.Components.Add(new InventoryComponent
        {
            Name = "Events",
            Technology = "Agent",
            Type = "events",
            DataJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    Source = "Microsoft-Windows-Kernel-General",
                    Message = "The operating system started at system time.",
                    Level = "Information",
                    Time = DateTime.UtcNow
                }
            })
        });

        // Act
        var response = await grpcClient.ReportSystemInfoAsync(request);

        // Assert gRPC response
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Contains(windowsHostname, response.Message);

        // Verify Database persistence of Windows Invariants
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var clientPc = await dbContext.ClientPcs
            .FirstOrDefaultAsync(p => p.MacAddress == windowsMac);

        Assert.NotNull(clientPc);
        Assert.Equal(windowsHostname, clientPc.Hostname);
        Assert.Equal(windowsUuid, clientPc.MachineIdentifier);
        Assert.NotNull(clientPc.FreeDiskSpace);
        Assert.Equal(14.2, clientPc.FreeDiskSpace.OsDriveFreeGB);
        Assert.True(clientPc.FreeDiskSpace.Drives.ContainsKey("C:\\"));
        Assert.Equal(14.2, clientPc.FreeDiskSpace.Drives["C:\\"]);

        // Verify Windows OS Environment in SystemMetadata
        Assert.NotNull(clientPc.SystemMetadata);
        var osVersion = clientPc.SystemMetadata.RootElement.GetProperty("OsVersion").GetString();
        Assert.Contains("Windows 10", osVersion);

        // Verify Windows EventLog records were queued into AgentEvents table
        var savedEvents = await dbContext.AgentEvents
            .Where(e => e.ClientPcId == clientPc.Id)
            .ToListAsync();

        Assert.NotEmpty(savedEvents);
        Assert.Contains(savedEvents, e => e.Source == "Microsoft-Windows-Kernel-General");
    }

    [Fact]
    public async Task ReportSystemInfo_WindowsEndpoint_ReceivesAndAcknowledgesQueuedCommands()
    {
        // Arrange
        var client = _factory.CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client
        });
        var grpcClient = new SystemInfoCollector.SystemInfoCollectorClient(channel);

        const string windowsHostname = "WIN-LTSC-CMD01";
        const string windowsMac = "00:15:5D:8A:2F:AA";
        const string targetFilePath = @"C:\ProgramData\Heimdall\agent.json";

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Pre-seed the Windows ClientPc
            var pc = new ClientPc
            {
                Id = Guid.NewGuid(),
                Name = windowsHostname,
                Hostname = windowsHostname,
                MacAddress = windowsMac,
                MachineIdentifier = Guid.NewGuid().ToString(),
                LastOnline = DateTimeOffset.UtcNow
            };
            dbContext.ClientPcs.Add(pc);

            // Queue a Windows-specific FILE_CHECK command
            var cmd = new QueuedAgentCommand
            {
                Id = Guid.NewGuid(),
                ClientPcId = pc.Id,
                Type = "FILE_CHECK",
                Payload = targetFilePath,
                Signature = "MOCK_TEST_SIG",
                IsProcessed = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.QueuedAgentCommands.Add(cmd);
            await dbContext.SaveChangesAsync();
        }

        var request = new SystemInfoRequest
        {
            Hostname = windowsHostname,
            MachineIdentifier = Guid.NewGuid().ToString(),
            MacAddress = windowsMac,
            LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };

        // Act
        var response = await grpcClient.ReportSystemInfoAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotEmpty(response.Commands);

        var receivedCmd = response.Commands.FirstOrDefault(c => c.Type == "FILE_CHECK");
        Assert.NotNull(receivedCmd);
        Assert.Equal(targetFilePath, receivedCmd.Payload);
        Assert.Equal("MOCK_TEST_SIG", receivedCmd.Signature);

        // Verify the command was marked processed in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processedCmd = await dbContext.QueuedAgentCommands
                .FirstOrDefaultAsync(c => c.Payload == targetFilePath);

            Assert.NotNull(processedCmd);
            Assert.True(processedCmd.IsProcessed);
        }
    }
}
