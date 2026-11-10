namespace App.Agent.Daemon.Infrastructure.Beckhoff;

using System.Threading;
using System.Threading.Tasks;
using App.Shared.Protos.Telemetry;

/// <summary>
/// Service for capturing PLC memory blocks via ADS, packaging them into strongly-typed Protobuf
/// messages, and dispatching them over MQTT.
/// </summary>
public interface IAdsMemoryReporter
{
    /// <summary>
    /// Samples simulated or real Beckhoff TwinCAT ADS PLC memory, builds a strongly-typed
    /// AdsPlcMemoryBlock Protobuf message, and publishes it over MQTT topic:
    /// heimdall/devices/{machineIdentifier}/plc_memory.
    /// </summary>
    Task<AdsPlcMemoryBlock?> ReportPlcMemoryAsync(string machineIdentifier, CancellationToken cancellationToken = default);
}
