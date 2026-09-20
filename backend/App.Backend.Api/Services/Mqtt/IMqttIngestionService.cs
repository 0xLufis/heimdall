namespace App.Backend.Api.Services.Mqtt;

using System.Threading;
using System.Threading.Tasks;
using App.Shared.Protos;

/// <summary>
/// Service managing MQTT ingestion subscriptions and command dispatch to connected edge agents.
/// </summary>
public interface IMqttIngestionService
{
    /// <summary>
    /// Whether the ingestion client is actively connected to the MQTT broker.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Publishes a strongly-typed Protobuf ServerCommand to a target edge device over MQTT.
    /// Topic: heimdall/devices/{machineIdentifier}/commands
    /// </summary>
    Task<bool> PublishCommandAsync(string machineIdentifier, ServerCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes raw binary payload to a specific MQTT topic with requested QoS.
    /// </summary>
    Task<bool> PublishRawAsync(string topic, byte[] payload, int qos = 1, CancellationToken cancellationToken = default);
}
