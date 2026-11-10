namespace App.Backend.Api.Services.Mqtt;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service governing an embedded high-performance MQTT server/broker within the Heimdall Backend.
/// Enables edge nodes and simulators to connect directly without mandatory external broker dependencies.
/// </summary>
public interface IMqttBrokerService : IAsyncDisposable
{
    /// <summary>
    /// Whether the embedded MQTT broker is currently running and accepting connections.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// The port the embedded broker is listening on.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Number of connected MQTT clients.
    /// </summary>
    int ConnectedClientsCount { get; }

    /// <summary>
    /// Starts the embedded MQTT broker if enabled in configuration.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops the embedded MQTT broker.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
