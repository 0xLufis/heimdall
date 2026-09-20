namespace App.Backend.Api.Services;

using System.Threading.Tasks;
using App.Shared.Protos;
using App.Shared.Protos.Telemetry;

/// <summary>
/// Service interface for processing incoming industrial telemetry, system inventory reports,
/// ADS PLC memory blocks, and diagnostic events over transport protocols (MQTT).
/// </summary>
public interface ITelemetryIngestionService
{
    /// <summary>
    /// Processes a full system information and inventory snapshot from an edge device.
    /// </summary>
    /// <param name="request">The Protobuf SystemInfoRequest payload.</param>
    /// <param name="callerAuthKey">Optional authentication key or token presented by caller.</param>
    /// <returns>A SystemInfoResponse containing processing status and any pending commands for the device.</returns>
    Task<SystemInfoResponse> ProcessSystemInfoAsync(SystemInfoRequest request, string? callerAuthKey = null);

    /// <summary>
    /// Processes a high-speed Beckhoff TwinCAT ADS PLC memory block unmarshalled into typed data.
    /// </summary>
    /// <param name="machineIdentifier">Machine identifier of the reporting edge device.</param>
    /// <param name="memoryBlock">The Protobuf AdsPlcMemoryBlock payload containing raw bytes and structured telemetry.</param>
    /// <param name="callerAuthKey">Optional authentication key presented by caller.</param>
    Task<bool> ProcessPlcMemoryAsync(string machineIdentifier, AdsPlcMemoryBlock memoryBlock, string? callerAuthKey = null);

    /// <summary>
    /// Processes a batch of Beckhoff TwinCAT ADS PLC memory blocks.
    /// </summary>
    Task<bool> ProcessPlcMemoryBatchAsync(AdsPlcMemoryBatch memoryBatch, string? callerAuthKey = null);

    /// <summary>
    /// Processes an agent event message (alarms, EtherCAT CRC errors, status notifications).
    /// </summary>
    Task<bool> ProcessAgentEventAsync(AgentEventMessage eventMessage, string? callerAuthKey = null);

    /// <summary>
    /// Processes a compressed or uncompressed telemetry batch request.
    /// </summary>
    Task<TelemetryBatchResponse> ProcessTelemetryBatchAsync(TelemetryBatchRequest batchRequest, string? callerAuthKey = null);
}
