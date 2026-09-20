namespace App.Agent.Daemon.Infrastructure.Beckhoff;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon.Interfaces;
using App.Agent.Daemon.Runtime.Evaluation;
using App.Contracts.Mqtt;
using App.Shared.Protos.Telemetry;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

/// <summary>
/// Captures PLC memory blocks via Beckhoff TwinCAT ADS and publishes them as strongly-typed
/// Protobuf AdsPlcMemoryBlock messages over MQTT.
/// </summary>
public class AdsMemoryReporter : IAdsMemoryReporter
{
    private readonly ILogger<AdsMemoryReporter> _logger;
    private readonly IAdsSimulationServer _adsServer;
    private readonly IMqttAgentClient _mqttClient;

    public AdsMemoryReporter(
        ILogger<AdsMemoryReporter> logger,
        IAdsSimulationServer adsServer,
        IMqttAgentClient mqttClient)
    {
        _logger = logger;
        _adsServer = adsServer;
        _mqttClient = mqttClient;
    }

    public async Task<AdsPlcMemoryBlock?> ReportPlcMemoryAsync(string machineIdentifier, CancellationToken cancellationToken = default)
    {
        try
        {
            // Synthesize binary PLC memory buffer representing TwinCAT ADS %M block
            var buffer = new byte[64];
            uint cycle = 0;
            if (_adsServer.SimulatedVariables.TryGetValue("MAIN.CycleCounter", out var cObj) && cObj is uint c)
                cycle = c;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), cycle);

            double temp = 60.0;
            if (_adsServer.SimulatedVariables.TryGetValue("MAIN.TemperatureDegC", out var tObj) && tObj is double t)
                temp = t;
            BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(4, 8), temp);

            double press = 6.0;
            if (_adsServer.SimulatedVariables.TryGetValue("MAIN.PressureBar", out var pObj) && pObj is double p)
                press = p;
            BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(12, 8), press);

            bool running = _adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN;
            buffer[20] = running ? (byte)1 : (byte)0;

            uint parts = 0;
            if (_adsServer.SimulatedVariables.TryGetValue("MAIN.PartsProduced", out var ppObj) && ppObj is uint pp)
                parts = pp;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(21, 4), parts);

            // Convert in-memory dictionary to strongly-typed Protobuf TelemetryValue
            var dict = new Dictionary<string, object>(_adsServer.SimulatedVariables);
            dict["AdsState"] = _adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN ? "RUN" : "STOP";
            var structuredValue = TypedTelemetryConverter.ToTelemetryValue(dict);

            var block = new AdsPlcMemoryBlock
            {
                AmsNetId = _adsServer.AmsNetId,
                AmsPort = _adsServer.AmsPort,
                IndexGroup = 0x4020, // ADS standard PLC memory area (%M)
                IndexOffset = 0x0000,
                Length = (uint)buffer.Length,
                RawMemory = ByteString.CopyFrom(buffer),
                Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                SymbolName = "MAIN.Station1.Telemetry._data",
                PlcTypeName = "ST_Station1TelemetryData",
                Quality = QualityCode.QualityGood,
                StructuredValue = structuredValue
            };
            block.Metadata["SimulationMode"] = "InMemoryTwinCAT";
            block.Metadata["AmsPort"] = _adsServer.AmsPort.ToString();

            var topic = MqttTopics.PlcMemory(machineIdentifier);
            bool published = await _mqttClient.PublishProtobufAsync(topic, block, qos: 1, cancellationToken);
            if (published)
            {
                _logger.LogInformation("Successfully published ADS PLC memory block via MQTT topic {Topic} (64 bytes raw memory)", topic);
            }
            else
            {
                _logger.LogWarning("Failed to publish ADS PLC memory block to topic {Topic}", topic);
            }

            return block;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assembling and publishing ADS PLC memory block for {MachineId}", machineIdentifier);
            return null;
        }
    }
}
