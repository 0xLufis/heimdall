namespace App.Agent.Daemon.Infrastructure;

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon.Interfaces;
using App.Contracts.Mqtt;
using App.Shared.Protos;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

/// <summary>
/// Resilient MQTT client for edge daemon communicating with Heimdall Backend over MQTT using Protobufs.
/// </summary>
public class MqttAgentClient : IMqttAgentClient
{
    private readonly ILogger<MqttAgentClient> _logger;
    private readonly IConfigurationService _configService;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    private IMqttClient? _client;
    private Func<ServerCommand, Task>? _commandCallback;
    private string? _subscribedMachineId;

    public bool IsConnected => _client?.IsConnected ?? false;
    public string BrokerHost => ResolveBrokerEndpoint().Host;
    public int BrokerPort => ResolveBrokerEndpoint().Port;

    public MqttAgentClient(
        ILogger<MqttAgentClient> logger,
        IConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    private (string Host, int Port) ResolveBrokerEndpoint()
    {
        // 1. Explicit environment variables
        var envHost = Environment.GetEnvironmentVariable("MQTT_BROKER_HOST");
        var envPortStr = Environment.GetEnvironmentVariable("MQTT_BROKER_PORT");

        if (!string.IsNullOrEmpty(envHost))
        {
            int port = 1883;
            if (!string.IsNullOrEmpty(envPortStr) && int.TryParse(envPortStr, out var parsedPort))
            {
                port = parsedPort;
            }
            return (envHost, port);
        }

        // 2. Parse from config.BackendUrl
        var config = _configService.Config;
        if (!string.IsNullOrEmpty(config.BackendUrl))
        {
            try
            {
                var uri = new Uri(config.BackendUrl);
                return (uri.Host, 1883);
            }
            catch
            {
                // Fall through to default
            }
        }

        return ("127.0.0.1", 1883);
    }

    public async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return true;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected) return true;

            var (host, port) = ResolveBrokerEndpoint();
            var config = _configService.Config;
            var agentKey = Environment.GetEnvironmentVariable("HEIMDALL_AGENT_KEY") ?? "heimdall-dev-agent-key";

            _logger.LogInformation("Connecting Edge Agent to MQTT Broker at {Host}:{Port}...", host, port);

            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
            _client.DisconnectedAsync += OnDisconnectedAsync;

            var clientOptionsBuilder = factory.CreateClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithClientId($"heimdall_agent_{Environment.MachineName}_{Guid.NewGuid():N}")
                .WithCleanSession()
                .WithTimeout(TimeSpan.FromSeconds(10));

            if (!string.IsNullOrEmpty(agentKey))
            {
                clientOptionsBuilder.WithCredentials("heimdall_agent", agentKey);
            }

            // Handle client certificates if configured
            if (config.AuthType == "HeimdallCert" || config.AuthType == "UserCert")
            {
                var certificates = new X509Certificate2Collection();
                if (!string.IsNullOrEmpty(config.ClientCertificatePath))
                {
                    try
                    {
                        var cert = X509CertificateLoader.LoadCertificateFromFile(config.ClientCertificatePath);
                        certificates.Add(cert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load client certificate from {Path}", config.ClientCertificatePath);
                    }
                }
                else if (config.AuthType == "UserCert" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly);
                    var certs = store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, true);
                    if (certs.Count > 0)
                    {
                        certificates.Add(certs[0]);
                    }
                }

                if (certificates.Count > 0)
                {
                    clientOptionsBuilder.WithTlsOptions(o =>
                    {
                        o.UseTls();
                        o.WithClientCertificates(certificates);
                    });
                }
            }

            var connectResult = await _client.ConnectAsync(clientOptionsBuilder.Build(), cancellationToken);
            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger.LogInformation("Edge Agent successfully connected to MQTT Broker ({Host}:{Port})", host, port);

                // Re-subscribe to commands if machine identifier was previously registered
                if (!string.IsNullOrEmpty(_subscribedMachineId) && _commandCallback != null)
                {
                    await SubscribeToCommandsInternalAsync(_subscribedMachineId, cancellationToken);
                }

                return true;
            }

            _logger.LogWarning("Failed to connect to MQTT broker: {ResultCode}", connectResult.ResultCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while connecting to MQTT broker.");
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("Edge Agent disconnected from MQTT Broker: {Reason}", args.Reason);
        return Task.CompletedTask;
    }

    public async Task SubscribeToCommandsAsync(string machineIdentifier, Func<ServerCommand, Task> onCommandReceived, CancellationToken cancellationToken = default)
    {
        _subscribedMachineId = machineIdentifier;
        _commandCallback = onCommandReceived;

        if (IsConnected)
        {
            await SubscribeToCommandsInternalAsync(machineIdentifier, cancellationToken);
        }
    }

    private async Task SubscribeToCommandsInternalAsync(string machineIdentifier, CancellationToken cancellationToken)
    {
        if (_client == null || !_client.IsConnected) return;

        var topic = MqttTopics.Commands(machineIdentifier);
        var factory = new MqttClientFactory();
        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build();

        await _client.SubscribeAsync(subscribeOptions, cancellationToken);
        _logger.LogInformation("Edge Agent subscribed to commands topic: {Topic}", topic);
    }

    private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.Payload.ToArray();

        _logger.LogInformation("Received MQTT message on {Topic} ({Length} bytes)", topic, payload.Length);

        if (_commandCallback == null) return;

        try
        {
            // Try parse as SystemInfoResponse first
            try
            {
                var response = SystemInfoResponse.Parser.ParseFrom(payload);
                if (response.Commands != null && response.Commands.Count > 0)
                {
                    foreach (var cmd in response.Commands)
                    {
                        await _commandCallback(cmd);
                    }
                    return;
                }
            }
            catch (InvalidProtocolBufferException)
            {
                // Try parse as direct ServerCommand
                var directCmd = ServerCommand.Parser.ParseFrom(payload);
                await _commandCallback(directCmd);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse and execute command from topic {Topic}", topic);
        }
    }

    public async Task<bool> PublishProtobufAsync<T>(string topic, T message, int qos = 1, CancellationToken cancellationToken = default) where T : IMessage
    {
        byte[] payload = message.ToByteArray();
        return await PublishRawAsync(topic, payload, qos, cancellationToken);
    }

    public async Task<bool> PublishRawAsync(string topic, byte[] payload, int qos = 1, CancellationToken cancellationToken = default)
    {
        if (!await EnsureConnectedAsync(cancellationToken))
        {
            _logger.LogWarning("Cannot publish to {Topic}: Broker connection unavailable.", topic);
            return false;
        }

        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            var agentKey = Environment.GetEnvironmentVariable("HEIMDALL_AGENT_KEY") ?? "heimdall-dev-agent-key";
            var qosLevel = qos switch
            {
                0 => MqttQualityOfServiceLevel.AtMostOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtLeastOnce
            };

            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qosLevel)
                .WithUserProperty("x-agent-key", (ReadOnlyMemory<byte>)System.Text.Encoding.UTF8.GetBytes(agentKey))
                .Build();

            var result = await _client!.PublishAsync(mqttMsg, cancellationToken);
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing MQTT message to {Topic}", topic);
            return false;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            try
            {
                if (_client.IsConnected)
                {
                    await _client.DisconnectAsync();
                }
                _client.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        _connectionLock.Dispose();
        _publishLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
