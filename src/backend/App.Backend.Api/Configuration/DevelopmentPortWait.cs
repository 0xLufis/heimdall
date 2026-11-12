namespace App.Backend.Api.Configuration;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using App.Contracts.Mqtt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Development only. A restart (<c>dotnet watch</c> on save, <c>just restart backend</c>) can start this
/// instance while the previous one is still releasing its ports. Kestrel then dies with "address already in
/// use", and the embedded MQTT broker fails quietly: it logs the error and the backend runs without a broker
/// until the next restart. Waiting a few seconds for the ports avoids both.
/// </summary>
public static class DevelopmentPortWait
{
    /// <summary>The ports a Development backend binds: 5099, 5001 and the embedded broker's port when enabled.</summary>
    public static IReadOnlyList<int> BackendPorts(IConfiguration configuration)
    {
        var ports = new List<int> { 5099, 5001 };
        var mqtt = configuration.GetSection(MqttOptions.SectionName).Get<MqttOptions>() ?? new MqttOptions();
        // Same overrides as MqttBrokerService.
        var enabled = bool.TryParse(configuration["MQTT_EMBEDDED_BROKER"], out var embedded) ? embedded : mqtt.EmbeddedBrokerEnabled;
        var brokerPort = int.TryParse(configuration["MQTT_EMBEDDED_PORT"], out var port) ? port : mqtt.EmbeddedBrokerPort;
        if (enabled && !ports.Contains(brokerPort)) ports.Add(brokerPort);
        return ports;
    }

    /// <summary>
    /// Whether a listener could bind the port on all interfaces (the scope of Kestrel's ListenAnyIP and the
    /// broker's default endpoint): a dual-mode socket on [::] conflicts with listeners on 0.0.0.0 and [::].
    /// </summary>
    public static bool IsPortFree(int port)
    {
        try
        {
            using var socket = Socket.OSSupportsIPv6
                ? new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp) { DualMode = true }
                : new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, port));
            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return false;
        }
        catch (SocketException)
        {
            return true; // anything else (e.g. permission) is not a previous instance we could wait for
        }
    }

    /// <summary>
    /// Blocks until every port is free or <paramref name="timeout"/> has passed. Returns the ports still in
    /// use (empty when all are free); the caller starts anyway, so a port held for good fails as before.
    /// </summary>
    public static IReadOnlyList<int> WaitForFreePorts(
        IEnumerable<int> ports, TimeSpan timeout, ILogger logger, TimeSpan? pollInterval = null, Func<int, bool>? isPortFree = null)
    {
        isPortFree ??= IsPortFree;
        var busy = ports.Distinct().Where(p => !isPortFree(p)).ToList();
        if (busy.Count == 0) return busy;

        logger.LogWarning("Port(s) {Ports} still in use (previous instance still shutting down?); waiting up to {Seconds:0.#} s before starting.",
            string.Join(", ", busy), timeout.TotalSeconds);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        var watch = Stopwatch.StartNew();
        while (busy.Count > 0 && watch.Elapsed < timeout)
        {
            Thread.Sleep(interval);
            busy = busy.Where(p => !isPortFree(p)).ToList();
        }

        if (busy.Count == 0)
            logger.LogInformation("Ports free after {Milliseconds} ms; starting.", watch.ElapsedMilliseconds);
        else
            logger.LogError("Port(s) {Ports} still in use after {Seconds:0.#} s; starting anyway (stop the other process: just stop backend).",
                string.Join(", ", busy), timeout.TotalSeconds);
        return busy;
    }
}
