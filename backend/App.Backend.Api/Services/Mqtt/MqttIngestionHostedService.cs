namespace App.Backend.Api.Services.Mqtt;

using System;
using System.Buffers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Contracts.Mqtt;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Protos;
using App.Shared.Protos.Telemetry;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Background hosted service that orchestrates MQTT ingestion subscriptions and command dispatching.
/// Bridges incoming binary Protobuf streams into the strongly typed ITelemetryIngestionService.
/// </summary>
public class MqttIngestionHostedService : BackgroundService, IMqttIngestionService
{
    private readonly ILogger<MqttIngestionHostedService> _logger;
    private readonly IMqttBrokerService _brokerService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttOptions _options;
    private readonly IConfiguration _configuration;

    private IMqttClient? _mqttClient;
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    public MqttIngestionHostedService(
        ILogger<MqttIngestionHostedService> logger,
        IMqttBrokerService brokerService,
        IServiceScopeFactory scopeFactory,
        IOptions<MqttOptions> options,
        IConfiguration configuration)
    {
        _logger = logger;
        _brokerService = brokerService;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _configuration = configuration;

        // Apply configuration overrides
        var envHost = _configuration["MQTT_BROKER_HOST"];
        if (!string.IsNullOrEmpty(envHost)) _options.BrokerHost = envHost;

        var envPort = _configuration["MQTT_BROKER_PORT"];
        if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out var p)) _options.BrokerPort = p;

        var envKey = _configuration["HEIMDALL_AGENT_KEY"];
        if (!string.IsNullOrEmpty(envKey)) _options.AgentKey = envKey;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Start embedded MQTT broker if enabled
        await _brokerService.StartAsync(stoppingToken);

        // 2. Connect client and maintain subscription loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_mqttClient == null || !_mqttClient.IsConnected)
                {
                    await ConnectAndSubscribeAsync(stoppingToken);
                }

                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT Ingestion loop encountered an issue. Reconnecting in 5s...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient != null)
        {
            try
            {
                _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
                await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
                _mqttClient.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        var clientOptionsBuilder = factory.CreateClientOptionsBuilder()
            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
            .WithClientId(_options.ClientId ?? $"heimdall_backend_ingestor_{Guid.NewGuid():N}")
            .WithCleanSession()
            .WithTimeout(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

        if (!string.IsNullOrEmpty(_options.AgentKey))
        {
            clientOptionsBuilder.WithCredentials("heimdall_backend", _options.AgentKey);
        }

        var clientOptions = clientOptionsBuilder.Build();

        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        _logger.LogInformation("Connecting Heimdall MQTT Ingestion Client to {Host}:{Port}...",
            _options.BrokerHost, _options.BrokerPort);

        var connectResult = await _mqttClient.ConnectAsync(clientOptions, cancellationToken);
        if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
        {
            _logger.LogWarning("Failed to connect to MQTT broker: {ResultCode}", connectResult.ResultCode);
            return;
        }

        _logger.LogInformation("MQTT Ingestion Client connected. Subscribing to telemetry and PLC memory topics...");

        // Subscribe to all device telemetry topics with QoS 1
        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(MqttTopics.SystemInfoWildcard).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .WithTopicFilter(f => f.WithTopic(MqttTopics.PlcMemoryWildcard).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .WithTopicFilter(f => f.WithTopic(MqttTopics.TelemetryWildcard).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .WithTopicFilter(f => f.WithTopic(MqttTopics.EventsWildcard).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .WithTopicFilter(f => f.WithTopic(MqttTopics.RecipeRequestWildcard).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build();

        await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
        _logger.LogInformation("Successfully subscribed to MQTT industrial wildcards ({Root}/+)", MqttTopics.RootPrefix);
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payloadSegment = args.ApplicationMessage.Payload;
        byte[] payload = payloadSegment.ToArray();

        string? machineId = MqttTopics.ExtractMachineIdentifier(topic);
        string? channel = MqttTopics.ExtractChannel(topic);

        if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(channel))
        {
            _logger.LogWarning("Ignoring MQTT message with unparseable topic: {Topic}", topic);
            return;
        }

        // Extract authentication key from MQTT 5.0 UserProperties if present
        string? callerAuthKey = null;
        if (args.ApplicationMessage.UserProperties != null)
        {
            callerAuthKey = args.ApplicationMessage.UserProperties
                .FirstOrDefault(p => string.Equals(p.Name, "x-agent-key", StringComparison.OrdinalIgnoreCase))?.ReadValueAsString();
        }

        // Create a fresh scope per message so scoped services (EF, ITelemetryIngestionService) are isolated
        await using var scope = _scopeFactory.CreateAsyncScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>();

        try
        {
            switch (channel)
            {
                case MqttTopics.SystemInfoChannel:
                {
                    var request = SystemInfoRequest.Parser.ParseFrom(payload);
                    var response = await ingestionService.ProcessSystemInfoAsync(request, callerAuthKey);

                    // If commands are queued for this device, push them back immediately via MQTT
                    if (response.Commands != null && response.Commands.Count > 0)
                    {
                        var commandsTopic = MqttTopics.Commands(machineId);
                        byte[] responseBytes = response.ToByteArray();
                        await PublishRawAsync(commandsTopic, responseBytes, qos: 1);
                        _logger.LogInformation("Dispatched {Count} commands to {MachineId} via topic {Topic}",
                            response.Commands.Count, machineId, commandsTopic);
                    }
                    break;
                }

                case MqttTopics.PlcMemoryChannel:
                {
                    try
                    {
                        var memoryBlock = AdsPlcMemoryBlock.Parser.ParseFrom(payload);
                        await ingestionService.ProcessPlcMemoryAsync(machineId, memoryBlock, callerAuthKey);
                    }
                    catch (InvalidProtocolBufferException)
                    {
                        // Fallback attempt to parse as AdsPlcMemoryBatch
                        var memoryBatch = AdsPlcMemoryBatch.Parser.ParseFrom(payload);
                        await ingestionService.ProcessPlcMemoryBatchAsync(memoryBatch, callerAuthKey);
                    }
                    break;
                }

                case MqttTopics.TelemetryChannel:
                {
                    var batch = TelemetryBatchRequest.Parser.ParseFrom(payload);
                    await ingestionService.ProcessTelemetryBatchAsync(batch, callerAuthKey);
                    break;
                }

                case MqttTopics.EventsChannel:
                {
                    var ev = AgentEventMessage.Parser.ParseFrom(payload);
                    await ingestionService.ProcessAgentEventAsync(ev, callerAuthKey);
                    break;
                }

                default:
                    _logger.LogWarning("Unrecognized MQTT subchannel: {Channel} on topic {Topic}", channel, topic);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode/process MQTT Protobuf message from topic {Topic}. Quarantining.", topic);

            // AGENTS.MD Rule 43: Dead-letter quarantine — resolve factory from the scope created above
            try
            {
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var db = await dbFactory.CreateDbContextAsync();
                var pc = await db.ClientPcs.FirstOrDefaultAsync(p => p.MachineIdentifier == machineId);

                db.MalformedTelemetryRecords.Add(new MalformedTelemetryRecord
                {
                    SourceIdentifier = machineId,
                    IngestionChannel = $"MQTT_{channel}",
                    ErrorReason = ex.Message,
                    RawPayload = Convert.ToBase64String(payload),
                    OrganizationId = pc?.OrganizationId ?? string.Empty,
                    QuarantinedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception qEx)
            {
                _logger.LogError(qEx, "Failed to store malformed payload into quarantine record.");
            }
        }
    }

    public async Task<bool> PublishCommandAsync(string machineIdentifier, ServerCommand command, CancellationToken cancellationToken = default)
    {
        var response = new SystemInfoResponse
        {
            Success = true,
            Message = "Server command dispatch"
        };
        response.Commands.Add(command);

        var topic = MqttTopics.Commands(machineIdentifier);
        return await PublishRawAsync(topic, response.ToByteArray(), qos: 1, cancellationToken);
    }

    public async Task<bool> PublishRawAsync(string topic, byte[] payload, int qos = 1, CancellationToken cancellationToken = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            _logger.LogWarning("Cannot publish to {Topic}: MQTT client is not connected.", topic);
            return false;
        }

        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            var qosLevel = qos switch
            {
                0 => MqttQualityOfServiceLevel.AtMostOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtLeastOnce
            };

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qosLevel)
                .Build();

            var result = await _mqttClient.PublishAsync(message, cancellationToken);
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message to topic {Topic}", topic);
            return false;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient != null)
        {
            try
            {
                _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
                if (_mqttClient.IsConnected)
                {
                    await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
                }
            }
            catch
            {
                // Ignore disconnect errors
            }
        }

        await _brokerService.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _publishLock.Dispose();
        _mqttClient?.Dispose();
        base.Dispose();
    }
}
