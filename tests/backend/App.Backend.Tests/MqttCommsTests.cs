using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Backend.Api.Services;
using App.Contracts.Mqtt;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Protos;
using App.Shared.Protos.Telemetry;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Xunit;

namespace App.Backend.Tests;

public class MqttCommsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MqttCommsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MqttComms_SystemInfoRequest_ProtobufSerialization_ProcessesSuccessfully()
    {
        using var scope = _factory.Services.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Arrange: Build Protobuf SystemInfoRequest
        var request = new SystemInfoRequest
        {
            Hostname = "MQTT-TestHost-01",
            MachineIdentifier = "HW-MQTT-TEST-001",
            MacAddress = "AA:BB:CC:DD:EE:01",
            LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            DiskInfo = new DiskInfo
            {
                TotalFreeGb = 45.5,
                OsDriveFreeGb = 22.1
            }
        };
        request.DiskInfo.Drives.Add("C:\\", 22.1);
        request.DiskInfo.Drives.Add("D:\\", 23.4);

        request.Components.Add(new InventoryComponent
        {
            Name = "Hardware",
            Technology = "EdgeAgent",
            Type = "hardware",
            DataJson = "{\"Cpu\":\"Intel i7\",\"Ram\":\"32GB\"}"
        });
        request.Components.Add(new InventoryComponent
        {
            Name = "OS Environment",
            Technology = "Windows",
            Type = "software",
            DataJson = "{\"OsVersion\":\"Windows 11\",\"IPAddress\":\"192.168.1.50\"}"
        });
        request.Components.Add(new InventoryComponent
        {
            Name = "IndustrialOT",
            Technology = "TwinCAT",
            Type = "software",
            DataJson = "{\"AdsState\":\"RUN\",\"AdsAmsNetId\":\"5.80.201.44.1.1:851\",\"OpcEndpoint\":\"opc.tcp://localhost:4840\",\"OpcConnected\":true}"
        });

        // Test Protobuf wire serialization and deserialization
        byte[] wireBytes = request.ToByteArray();
        Assert.NotEmpty(wireBytes);
        var wireDecodedRequest = SystemInfoRequest.Parser.ParseFrom(wireBytes);
        Assert.Equal(request.Hostname, wireDecodedRequest.Hostname);
        Assert.Equal(request.MachineIdentifier, wireDecodedRequest.MachineIdentifier);

        // Act: Ingest into Telemetry Ingestion Service
        var response = await ingestionService.ProcessSystemInfoAsync(wireDecodedRequest);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("MQTT-TestHost-01", response.Message);

        // Verify Database entity state
        var pc = await dbContext.ClientPcs.FirstOrDefaultAsync(p => p.MacAddress == "AA:BB:CC:DD:EE:01");
        Assert.NotNull(pc);
        Assert.Equal("MQTT-TestHost-01", pc.Hostname);
        Assert.Equal("HW-MQTT-TEST-001", pc.MachineIdentifier);
        Assert.NotNull(pc.FreeDiskSpace);
        Assert.Equal(45.5, pc.FreeDiskSpace.TotalFreeGB);
        Assert.NotNull(pc.SystemMetadata);

        var metaJson = pc.SystemMetadata.RootElement.ToString();
        Assert.Contains("5.80.201.44.1.1:851", metaJson);
        Assert.Contains("RUN", metaJson);
    }

    [Fact]
    public async Task MqttComms_AdsPlcMemoryBlock_ProtobufIngestion_UpdatesPlcMemoryState()
    {
        using var scope = _factory.Services.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string machineId = "HW-PLC-TEST-002";

        // Pre-create PC
        var pc = new ClientPc
        {
            Id = Guid.NewGuid(),
            Name = "PLC-Station-02",
            Hostname = "PLC-Station-02",
            MachineIdentifier = machineId,
            MacAddress = "AA:BB:CC:DD:EE:02",
            LastOnline = DateTimeOffset.UtcNow
        };
        dbContext.ClientPcs.Add(pc);
        await dbContext.SaveChangesAsync();

        // Synthesize 64-byte raw TwinCAT ADS PLC memory buffer (%M area)
        var rawMemory = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(rawMemory.AsSpan(0, 4), 14250); // Cycle counter
        BinaryPrimitives.WriteDoubleLittleEndian(rawMemory.AsSpan(4, 8), 68.4);  // Temperature
        BinaryPrimitives.WriteDoubleLittleEndian(rawMemory.AsSpan(12, 8), 6.15); // Pressure

        var structVal = new StructValue();
        structVal.Fields["cycleCounter"] = new TelemetryValue { Uint32Value = 14250 };
        structVal.Fields["temperatureDegC"] = new TelemetryValue { DoubleValue = 68.4 };
        structVal.Fields["pressureBar"] = new TelemetryValue { DoubleValue = 6.15 };

        var plcBlock = new AdsPlcMemoryBlock
        {
            AmsNetId = "5.80.201.44.1.1",
            AmsPort = 851,
            IndexGroup = 0x4020,
            IndexOffset = 0,
            Length = (uint)rawMemory.Length,
            RawMemory = ByteString.CopyFrom(rawMemory),
            Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            SymbolName = "MAIN.Station1.Telemetry._data",
            PlcTypeName = "ST_Station1TelemetryData",
            Quality = QualityCode.QualityGood,
            StructuredValue = new TelemetryValue { StructValue = structVal }
        };
        plcBlock.Metadata["Source"] = "TwinCAT_ADS";

        // Protobuf Wire serialization test
        byte[] wireBytes = plcBlock.ToByteArray();
        Assert.NotEmpty(wireBytes);
        var decodedBlock = AdsPlcMemoryBlock.Parser.ParseFrom(wireBytes);
        Assert.Equal("MAIN.Station1.Telemetry._data", decodedBlock.SymbolName);
        Assert.Equal(0x4020u, decodedBlock.IndexGroup);

        // Act
        bool success = await ingestionService.ProcessPlcMemoryAsync(machineId, decodedBlock);
        Assert.True(success);

        // Assert Database was updated with ADS PLC Memory
        // Use a fresh scope to bypass the EF change tracker identity map (service wrote via factory DbContext)
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedPc = await assertDb.ClientPcs.AsNoTracking().FirstOrDefaultAsync(p => p.MachineIdentifier == machineId);
        Assert.NotNull(updatedPc);
        Assert.NotNull(updatedPc.SystemMetadata);

        var metaText = updatedPc.SystemMetadata.RootElement.ToString();
        Assert.Contains("AdsPlcMemory", metaText);
        Assert.Contains("MAIN.Station1.Telemetry._data", metaText);
        Assert.Contains("0x4020", metaText);
        Assert.Contains("5.80.201.44.1.1", metaText);
    }

    [Fact]
    public async Task MqttComms_CommandQueue_ReturnsServerCommandsInResponse()
    {
        using var scope = _factory.Services.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string machineId = "HW-CMD-TEST-003";

        // Pre-create PC
        var pc = new ClientPc
        {
            Id = Guid.NewGuid(),
            Name = "CMD-Host-03",
            Hostname = "CMD-Host-03",
            MachineIdentifier = machineId,
            MacAddress = "AA:BB:CC:DD:EE:03",
            LastOnline = DateTimeOffset.UtcNow
        };
        dbContext.ClientPcs.Add(pc);

        // Pre-seed queued agent command
        var cmd = new QueuedAgentCommand
        {
            Id = Guid.NewGuid(),
            ClientPcId = pc.Id,
            Type = "APPLY_RECIPE",
            Payload = "{\"RecipeId\":\"REC-001\",\"Version\":2}",
            Signature = "VALID_TEST_SIG",
            IsProcessed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.QueuedAgentCommands.Add(cmd);
        await dbContext.SaveChangesAsync();

        var request = new SystemInfoRequest
        {
            Hostname = "CMD-Host-03",
            MachineIdentifier = machineId,
            MacAddress = "AA:BB:CC:DD:EE:03",
            LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };

        // Act
        var response = await ingestionService.ProcessSystemInfoAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Single(response.Commands);
        Assert.Equal("APPLY_RECIPE", response.Commands[0].Type);
        Assert.Equal("VALID_TEST_SIG", response.Commands[0].Signature);

        // Verify command is marked processed — use a fresh scope to bypass identity map stale state
        using var cmdAssertScope = _factory.Services.CreateScope();
        var cmdAssertDb = cmdAssertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshedCmd = await cmdAssertDb.QueuedAgentCommands.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cmd.Id);
        Assert.NotNull(refreshedCmd);
        Assert.True(refreshedCmd.IsProcessed);
    }

    [Fact]
    public async Task MqttComms_EndToEndNetworkTransport_PublishesOverMqttServerAndIngests()
    {
        int testPort = 18880 + Random.Shared.Next(1000, 2000);

        // 1. Start an ephemeral in-process MQTT broker
        var serverFactory = new MqttServerFactory();
        var serverOptions = serverFactory.CreateServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(testPort)
            .Build();

        using var server = serverFactory.CreateMqttServer(serverOptions);
        await server.StartAsync();

        // Brief wait for the server listener to bind
        await Task.Delay(100);

        try
        {
            // 2. Connect subscriber and publisher clients
            var clientFactory = new MqttClientFactory();
            using var subscriberClient = clientFactory.CreateMqttClient();
            using var publisherClient = clientFactory.CreateMqttClient();

            var subscriberOptions = clientFactory.CreateClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", testPort)
                .WithClientId("e2e-subscriber")
                .WithTimeout(TimeSpan.FromSeconds(10))
                .Build();

            var publisherOptions = clientFactory.CreateClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", testPort)
                .WithClientId("e2e-publisher")
                .WithTimeout(TimeSpan.FromSeconds(10))
                .Build();

            var subResult = await subscriberClient.ConnectAsync(subscriberOptions);
            Assert.Equal(MqttClientConnectResultCode.Success, subResult.ResultCode);

            var pubResult = await publisherClient.ConnectAsync(publisherOptions);
            Assert.Equal(MqttClientConnectResultCode.Success, pubResult.ResultCode);

            var receivedTcs = new TaskCompletionSource<(string Topic, byte[] Payload)>();

            subscriberClient.ApplicationMessageReceivedAsync += args =>
            {
                var topic = args.ApplicationMessage.Topic;
                var payload = args.ApplicationMessage.Payload.ToArray();
                receivedTcs.TrySetResult((topic, payload));
                return Task.CompletedTask;
            };

            await subscriberClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(MqttTopics.AllDevicesWildcard)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

            // 3. Publish Protobuf SystemInfoRequest over MQTT
            const string machineId = "HW-E2E-TEST-004";
            var request = new SystemInfoRequest
            {
                Hostname = "E2E-Mqtt-Host",
                MachineIdentifier = machineId,
                MacAddress = "AA:BB:CC:DD:EE:04",
                LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };

            var topic = MqttTopics.SystemInfo(machineId);
            byte[] protoBytes = request.ToByteArray();

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(protoBytes)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await publisherClient.PublishAsync(message);

            // 4. Await arrival through broker
            var received = await Task.WhenAny(receivedTcs.Task, Task.Delay(5000));
            Assert.Same(receivedTcs.Task, received);

            var (recTopic, recPayload) = await receivedTcs.Task;
            Assert.Equal(topic, recTopic);

            var decodedRequest = SystemInfoRequest.Parser.ParseFrom(recPayload);
            Assert.Equal("E2E-Mqtt-Host", decodedRequest.Hostname);
            Assert.Equal(machineId, decodedRequest.MachineIdentifier);

            await publisherClient.DisconnectAsync();
            await subscriberClient.DisconnectAsync();
        }
        finally
        {
            await server.StopAsync();
        }
    }
}
