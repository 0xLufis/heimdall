namespace App.Backend.Api.Services.Mqtt;

using System;
using System.Threading;
using System.Threading.Tasks;
using App.Contracts.Mqtt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.Server;

/// <summary>
/// Manages the embedded in-process MQTT broker.
/// </summary>
public class MqttBrokerService : IMqttBrokerService
{
    private readonly ILogger<MqttBrokerService> _logger;
    private readonly MqttOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    private MqttServer? _server;
    private int _connectedClients;

    public bool IsRunning => _server?.IsStarted ?? false;
    public int Port => _options.EmbeddedBrokerPort;
    public int ConnectedClientsCount => _connectedClients;

    public MqttBrokerService(
        ILogger<MqttBrokerService> logger,
        IOptions<MqttOptions> options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _logger = logger;
        _options = options.Value;
        _configuration = configuration;
        _environment = environment;

        // Allow environment variable overrides
        if (!string.IsNullOrEmpty(_configuration["MQTT_EMBEDDED_BROKER"]))
        {
            if (bool.TryParse(_configuration["MQTT_EMBEDDED_BROKER"], out var eb))
            {
                _options.EmbeddedBrokerEnabled = eb;
            }
        }

        if (!string.IsNullOrEmpty(_configuration["MQTT_EMBEDDED_PORT"]))
        {
            if (int.TryParse(_configuration["MQTT_EMBEDDED_PORT"], out var ep))
            {
                _options.EmbeddedBrokerPort = ep;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EmbeddedBrokerEnabled)
        {
            _logger.LogInformation("Embedded MQTT broker is disabled via configuration. Operating in external broker mode.");
            return;
        }

        if (_server != null && _server.IsStarted)
        {
            return;
        }

        try
        {
            var serverFactory = new MqttServerFactory();
            var serverOptions = serverFactory.CreateServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(_options.EmbeddedBrokerPort)
                .Build();

            _server = serverFactory.CreateMqttServer(serverOptions);

            _server.ClientConnectedAsync += OnClientConnectedAsync;
            _server.ClientDisconnectedAsync += OnClientDisconnectedAsync;
            _server.ValidatingConnectionAsync += OnValidatingConnectionAsync;

            await _server.StartAsync();
            _logger.LogInformation("Heimdall Embedded MQTT Broker started on port {Port} (TCP)", _options.EmbeddedBrokerPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start embedded MQTT broker on port {Port}. Continuing in external broker mode.",
                _options.EmbeddedBrokerPort);
        }
    }

    private Task OnClientConnectedAsync(ClientConnectedEventArgs args)
    {
        Interlocked.Increment(ref _connectedClients);
        _logger.LogInformation("MQTT Client connected: {ClientId} from {Endpoint}", args.ClientId, args.RemoteEndPoint);
        return Task.CompletedTask;
    }

    private Task OnClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        Interlocked.Decrement(ref _connectedClients);
        _logger.LogInformation("MQTT Client disconnected: {ClientId} (Type: {Type})", args.ClientId, args.DisconnectType);
        return Task.CompletedTask;
    }

    private Task OnValidatingConnectionAsync(ValidatingConnectionEventArgs args)
    {
        // Optional client credentials authentication
        var requiredKey = _configuration["HEIMDALL_AGENT_KEY"];
        if (!string.IsNullOrEmpty(requiredKey))
        {
            // Allow dev bypass in Dev or Test environments if key is default or empty
            if (_environment.IsDevelopment() || _environment.IsEnvironment("Test"))
            {
                if (string.IsNullOrEmpty(args.Password) || args.Password == requiredKey || args.Password == "heimdall-dev-agent-key")
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success;
                    return Task.CompletedTask;
                }
            }

            if (args.Password != requiredKey)
            {
                _logger.LogWarning("Rejecting unauthorized MQTT connection from ClientId={ClientId}", args.ClientId);
                args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                return Task.CompletedTask;
            }
        }

        args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_server != null && _server.IsStarted)
        {
            _logger.LogInformation("Stopping Heimdall Embedded MQTT Broker...");
            await _server.StopAsync();
            _server.Dispose();
            _server = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}
