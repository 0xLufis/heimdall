namespace App.Agent.Daemon.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Protos;
using Google.Protobuf;

/// <summary>
/// Client interface for edge daemon MQTT communication, strongly-typed Protobuf publishing,
/// and server command dispatching.
/// </summary>
public interface IMqttAgentClient : IAsyncDisposable
{
    /// <summary>
    /// Indicates whether the edge client is actively connected to the MQTT broker.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Configured broker hostname or IP.
    /// </summary>
    string BrokerHost { get; }

    /// <summary>
    /// Configured broker port.
    /// </summary>
    int BrokerPort { get; }

    /// <summary>
    /// Connects or verifies active connection to the configured MQTT broker.
    /// </summary>
    Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes a Protobuf message to binary bytes and publishes over MQTT.
    /// </summary>
    Task<bool> PublishProtobufAsync<T>(string topic, T message, int qos = 1, CancellationToken cancellationToken = default) where T : IMessage;

    /// <summary>
    /// Publishes raw binary payload to a specified topic.
    /// </summary>
    Task<bool> PublishRawAsync(string topic, byte[] payload, int qos = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to the device's command topic: heimdall/devices/{machineIdentifier}/commands.
    /// </summary>
    Task SubscribeToCommandsAsync(string machineIdentifier, Func<ServerCommand, Task> onCommandReceived, CancellationToken cancellationToken = default);
}
