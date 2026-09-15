namespace App.Agent.Daemon.Infrastructure.Opc;

using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Lightweight in-memory OPC UA Simulation Server.
/// Listens on standard OPC UA TCP port 4840 and implements binary protocol handshakes:
/// - HEL / ACK: Connection establishment & parameter negotiation
/// - OPN / CLO: Channel lifecycle handling
/// </summary>
public class MinimalOpcServer : IDisposable
{
    public const int DefaultPort = 4840;
    public const string DefaultEndpointUrl = "opc.tcp://0.0.0.0:4840";

    private readonly ILogger<MinimalOpcServer>? _logger;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public int Port { get; }
    public string EndpointUrl { get; }
    public bool IsListening { get; private set; }
    public long TotalConnectionsHandled { get; private set; }
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    public ConcurrentDictionary<string, object> ServerNodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public MinimalOpcServer(int port = DefaultPort, ILogger<MinimalOpcServer>? logger = null)
    {
        Port = port;
        EndpointUrl = $"opc.tcp://0.0.0.0:{port}";
        _logger = logger;

        InitializeServerNodes();
    }

    private void InitializeServerNodes()
    {
        ServerNodes["ns=0;i=2256"] = "Heimdall OPC UA Embedded Server"; // Server_ServerArray
        ServerNodes["ns=0;i=2259"] = "Running";                          // Server_ServerStatus_State
        ServerNodes["ns=2;s=Line01.DriveSpeed"] = 1480.0;
        ServerNodes["ns=2;s=Line01.MotorCurrent"] = 12.4;
        ServerNodes["ns=2;s=Line01.QualityOk"] = true;
        ServerNodes["ns=2;s=Line01.PartCount"] = 2450L;
    }

    public void Start()
    {
        if (IsListening) return;

        _cts = new CancellationTokenSource();
        try
        {
            _tcpListener = new TcpListener(IPAddress.Any, Port);
            _tcpListener.Start();
            IsListening = true;
            _logger?.LogInformation("OPC UA Simulation Server listening on port {Port} ({Endpoint})", Port, EndpointUrl);

            _listenTask = Task.Run(() => AcceptClientsAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to bind OPC UA Simulation Server to port {Port}.", Port);
            IsListening = false;
        }
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(token);
                TotalConnectionsHandled++;
                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Exception accepting OPC client connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var headerBuffer = new byte[8];
            while (!token.IsCancellationRequested && client.Connected)
            {
                int read = await ReadExactAsync(stream, headerBuffer, 8, token);
                if (read < 8) break;

                string msgType = Encoding.ASCII.GetString(headerBuffer, 0, 3);
                char chunkType = (char)headerBuffer[3];
                uint msgLength = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.AsSpan(4, 4));

                if (msgLength < 8 || msgLength > 65536) break;

                int payloadLen = (int)msgLength - 8;
                byte[] payload = new byte[payloadLen];
                if (payloadLen > 0)
                {
                    int payloadRead = await ReadExactAsync(stream, payload, payloadLen, token);
                    if (payloadRead < payloadLen) break;
                }

                if (msgType == "HEL")
                {
                    // Respond with ACKF
                    // Header: "ACK" + 'F' + size (28)
                    // ProtocolVersion: 0 (4 bytes)
                    // ReceiveBufferSize: 65535 (4 bytes)
                    // SendBufferSize: 65535 (4 bytes)
                    // MaxMessageSize: 16777216 (4 bytes)
                    // MaxChunkCount: 5000 (4 bytes)
                    var ack = new byte[28];
                    ack[0] = (byte)'A';
                    ack[1] = (byte)'C';
                    ack[2] = (byte)'K';
                    ack[3] = (byte)'F';
                    BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(4, 4), 28);
                    BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(8, 4), 0);
                    BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(12, 4), 65535);
                    BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(16, 4), 65535);
                    BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(20, 4), 16777216);
                    BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(24, 4), 5000);

                    await stream.WriteAsync(ack, token);
                    await stream.FlushAsync(token);
                }
                else if (msgType == "CLO")
                {
                    break;
                }
                else
                {
                    // Generic acknowledgement / keepalive
                    break;
                }
            }
        }
    }

    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken token)
    {
        int total = 0;
        while (total < count)
        {
            int r = await stream.ReadAsync(buffer.AsMemory(total, count - total), token);
            if (r <= 0) return total;
            total += r;
        }
        return total;
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _tcpListener?.Stop();
            IsListening = false;
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
