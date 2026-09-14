namespace App.Agent.Daemon.Infrastructure.Beckhoff;

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
/// Lightweight in-memory Beckhoff TwinCAT ADS simulation server.
/// Listens on standard ADS TCP port 48898 and implements AMS/ADS commands:
/// - 0x0001: Read Device Info (TwinCAT 3.1 PLC Runtime)
/// - 0x0002: Read PLC Variable Buffer
/// - 0x0003: Write PLC Variable Buffer
/// - 0x0004: Read State (ADSSTATE_RUN / STOP)
/// - 0x0005: Write Control (Change ADS State)
/// - 0x0009: Read/Write by Symbol Name or Index Group
/// </summary>
public class AdsSimulationServer : IDisposable
{
    public const int DefaultAdsPort = 48898;
    public const string DefaultAmsNetId = "5.80.201.44.1.1";
    public const ushort DefaultAmsPort = 851; // TwinCAT 3 PLC Runtime 1

    // ADS State constants
    public const ushort ADSSTATE_INVALID = 0;
    public const ushort ADSSTATE_IDLE = 1;
    public const ushort ADSSTATE_RESET = 2;
    public const ushort ADSSTATE_INIT = 3;
    public const ushort ADSSTATE_START = 4;
    public const ushort ADSSTATE_RUN = 5;
    public const ushort ADSSTATE_STOP = 6;
    public const ushort ADSSTATE_SAVECFG = 7;
    public const ushort ADSSTATE_LOADCFG = 8;
    public const ushort ADSSTATE_POWERFAILURE = 9;
    public const ushort ADSSTATE_POWERGOOD = 10;
    public const ushort ADSSTATE_ERROR = 11;
    public const ushort ADSSTATE_SHUTDOWN = 12;
    public const ushort ADSSTATE_SUSPEND = 13;
    public const ushort ADSSTATE_RESUME = 14;
    public const ushort ADSSTATE_CONFIG = 15;
    public const ushort ADSSTATE_RECONFIG = 16;

    // ADS Command IDs
    public const ushort ADSCMD_READ_DEVICE_INFO = 0x0001;
    public const ushort ADSCMD_READ = 0x0002;
    public const ushort ADSCMD_WRITE = 0x0003;
    public const ushort ADSCMD_READ_STATE = 0x0004;
    public const ushort ADSCMD_WRITE_CONTROL = 0x0005;
    public const ushort ADSCMD_ADD_DEVICE_NOTIFICATION = 0x0006;
    public const ushort ADSCMD_DEL_DEVICE_NOTIFICATION = 0x0007;
    public const ushort ADSCMD_DEVICE_NOTIFICATION = 0x0008;
    public const ushort ADSCMD_READ_WRITE = 0x0009;

    private readonly ILogger<AdsSimulationServer>? _logger;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Timer? _simulationTicker;

    public int Port { get; }
    public string AmsNetId { get; }
    public ushort AmsPort { get; }
    public ushort CurrentAdsState { get; set; } = ADSSTATE_RUN;
    public ushort CurrentDeviceState { get; set; } = 0;
    public bool IsListening { get; private set; }
    public long TotalRequestsHandled { get; private set; }
    public DateTimeOffset LastRequestTime { get; private set; } = DateTimeOffset.MinValue;

    // In-memory simulated PLC variables
    public ConcurrentDictionary<string, object> SimulatedVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public AdsSimulationServer(
        int port = DefaultAdsPort,
        string amsNetId = DefaultAmsNetId,
        ushort amsPort = DefaultAmsPort,
        ILogger<AdsSimulationServer>? logger = null)
    {
        Port = port;
        AmsNetId = amsNetId;
        AmsPort = amsPort;
        _logger = logger;

        InitializeSimulatedTags();
    }

    private void InitializeSimulatedTags()
    {
        SimulatedVariables["MAIN.CycleCounter"] = 10420u;
        SimulatedVariables["MAIN.TemperatureDegC"] = 63.45;
        SimulatedVariables["MAIN.PressureBar"] = 6.18;
        SimulatedVariables["MAIN.MachineRunning"] = true;
        SimulatedVariables["MAIN.PartsProduced"] = 8520u;
        SimulatedVariables["MAIN.EmergencyStopActive"] = false;
        SimulatedVariables["MAIN.DriveSpeedRpm"] = 1450.0;
        SimulatedVariables["MAIN.LineCurrentAmps"] = 14.8;
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
            _logger?.LogInformation("Beckhoff TwinCAT ADS Simulation Server started on port {Port} (AmsNetId: {NetId}:{AmsPort})",
                Port, AmsNetId, AmsPort);

            _listenTask = Task.Run(() => AcceptClientsAsync(_cts.Token));

            // Start PLC simulation ticker (updating sine wave variables every 1000ms)
            _simulationTicker = new Timer(OnSimulationTick, null, 1000, 1000);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to bind ADS Simulation Server to port {Port}. Will run in virtual mode.", Port);
            IsListening = false;
        }
    }

    private void OnSimulationTick(object? state)
    {
        if (CurrentAdsState != ADSSTATE_RUN) return;

        if (SimulatedVariables.TryGetValue("MAIN.CycleCounter", out var cc) && cc is uint ccu)
        {
            SimulatedVariables["MAIN.CycleCounter"] = ccu + 1;
        }
        if (SimulatedVariables.TryGetValue("MAIN.PartsProduced", out var pp) && pp is uint ppu && Random.Shared.Next(0, 5) == 0)
        {
            SimulatedVariables["MAIN.PartsProduced"] = ppu + 1;
        }

        double phase = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 360) * (Math.PI / 180.0);
        SimulatedVariables["MAIN.TemperatureDegC"] = Math.Round(62.0 + (8.5 * Math.Sin(phase)), 2);
        SimulatedVariables["MAIN.PressureBar"] = Math.Round(6.0 + (0.35 * Math.Cos(phase)), 2);
        SimulatedVariables["MAIN.LineCurrentAmps"] = Math.Round(14.0 + (1.2 * Math.Sin(phase * 2)), 2);
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error accepting ADS client connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var headerBuffer = new byte[6]; // AMS TCP Header: 2 bytes reserved, 4 bytes length

            while (!token.IsCancellationRequested && client.Connected)
            {
                int read = await ReadExactAsync(stream, headerBuffer, 0, 6, token);
                if (read < 6) break;

                uint amsPacketLength = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.AsSpan(2, 4));
                if (amsPacketLength < 32 || amsPacketLength > 65536) break;

                var amsBuffer = new byte[amsPacketLength];
                int amsRead = await ReadExactAsync(stream, amsBuffer, 0, (int)amsPacketLength, token);
                if (amsRead < (int)amsPacketLength) break;

                TotalRequestsHandled++;
                LastRequestTime = DateTimeOffset.UtcNow;

                byte[] responseAms = ProcessAmsPacket(amsBuffer);

                // Send back AMS TCP Header + AMS Response
                var responseTcpHeader = new byte[6];
                BinaryPrimitives.WriteUInt16LittleEndian(responseTcpHeader.AsSpan(0, 2), 0);
                BinaryPrimitives.WriteUInt32LittleEndian(responseTcpHeader.AsSpan(2, 4), (uint)responseAms.Length);

                await stream.WriteAsync(responseTcpHeader, 0, 6, token);
                await stream.WriteAsync(responseAms, 0, responseAms.Length, token);
                await stream.FlushAsync(token);
            }
        }
    }

    public byte[] ProcessAmsPacket(byte[] requestAms)
    {
        if (requestAms.Length < 32) return Array.Empty<byte>();

        // Parse Request AMS Header
        byte[] targetNetId = requestAms[0..6];
        ushort targetPort = BinaryPrimitives.ReadUInt16LittleEndian(requestAms.AsSpan(6, 2));
        byte[] sourceNetId = requestAms[8..14];
        ushort sourcePort = BinaryPrimitives.ReadUInt16LittleEndian(requestAms.AsSpan(14, 2));
        ushort commandId = BinaryPrimitives.ReadUInt16LittleEndian(requestAms.AsSpan(16, 2));
        ushort stateFlags = BinaryPrimitives.ReadUInt16LittleEndian(requestAms.AsSpan(18, 2));
        uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(requestAms.AsSpan(20, 4));
        uint errorCode = BinaryPrimitives.ReadUInt32LittleEndian(requestAms.AsSpan(24, 4));
        uint invokeId = BinaryPrimitives.ReadUInt32LittleEndian(requestAms.AsSpan(28, 4));

        ReadOnlySpan<byte> requestData = (dataLength > 0 && requestAms.Length >= 32 + (int)dataLength)
            ? requestAms.AsSpan(32, (int)dataLength)
            : ReadOnlySpan<byte>.Empty;

        byte[] responseData;
        uint responseErrorCode = 0;

        switch (commandId)
        {
            case ADSCMD_READ_DEVICE_INFO:
                // Response: Result (4 bytes), MajorVersion (1 byte), MinorVersion (1 byte), VersionBuild (2 bytes), DeviceName (16 bytes)
                responseData = new byte[24];
                BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0); // OK
                responseData[4] = 3;  // Major
                responseData[5] = 1;  // Minor
                BinaryPrimitives.WriteUInt16LittleEndian(responseData.AsSpan(6, 2), 4026); // Build
                byte[] nameBytes = Encoding.ASCII.GetBytes("TwinCAT 3.1 PLC\0");
                Array.Copy(nameBytes, 0, responseData, 8, Math.Min(nameBytes.Length, 16));
                break;

            case ADSCMD_READ_STATE:
                // Response: Result (4 bytes), AdsState (2 bytes), DeviceState (2 bytes)
                responseData = new byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0);
                BinaryPrimitives.WriteUInt16LittleEndian(responseData.AsSpan(4, 2), CurrentAdsState);
                BinaryPrimitives.WriteUInt16LittleEndian(responseData.AsSpan(6, 2), CurrentDeviceState);
                break;

            case ADSCMD_WRITE_CONTROL:
                if (requestData.Length >= 4)
                {
                    ushort newAdsState = BinaryPrimitives.ReadUInt16LittleEndian(requestData[0..2]);
                    ushort newDeviceState = BinaryPrimitives.ReadUInt16LittleEndian(requestData[2..4]);
                    CurrentAdsState = newAdsState;
                    CurrentDeviceState = newDeviceState;
                }
                // Response: Result (4 bytes)
                responseData = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0);
                break;

            case ADSCMD_READ:
                // Request: IndexGroup (4), IndexOffset (4), Length (4)
                if (requestData.Length >= 12)
                {
                    uint idxGroup = BinaryPrimitives.ReadUInt32LittleEndian(requestData[0..4]);
                    uint idxOffset = BinaryPrimitives.ReadUInt32LittleEndian(requestData[4..8]);
                    uint readLen = BinaryPrimitives.ReadUInt32LittleEndian(requestData[8..12]);

                    var payload = GenerateSimulatedReadPayload(idxGroup, idxOffset, (int)readLen);
                    responseData = new byte[4 + 4 + payload.Length];
                    BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0); // Result
                    BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(4, 4), (uint)payload.Length);
                    payload.CopyTo(responseData.AsSpan(8));
                }
                else
                {
                    responseData = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0x0701); // ADS error invalid param
                }
                break;

            case ADSCMD_READ_WRITE:
                // Request: IndexGroup (4), IndexOffset (4), ReadLength (4), WriteLength (4), Data (WriteLength)
                if (requestData.Length >= 16)
                {
                    uint idxGroup = BinaryPrimitives.ReadUInt32LittleEndian(requestData[0..4]);
                    uint idxOffset = BinaryPrimitives.ReadUInt32LittleEndian(requestData[4..8]);
                    uint readLen = BinaryPrimitives.ReadUInt32LittleEndian(requestData[8..12]);
                    uint writeLen = BinaryPrimitives.ReadUInt32LittleEndian(requestData[12..16]);

                    string symbolName = "";
                    if (writeLen > 0 && requestData.Length >= 16 + (int)writeLen)
                    {
                        symbolName = Encoding.ASCII.GetString(requestData.Slice(16, (int)writeLen)).TrimEnd('\0');
                    }

                    var payload = GenerateSymbolReadPayload(symbolName, (int)readLen);
                    responseData = new byte[4 + 4 + payload.Length];
                    BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0);
                    BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(4, 4), (uint)payload.Length);
                    payload.CopyTo(responseData.AsSpan(8));
                }
                else
                {
                    responseData = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0x0701);
                }
                break;

            default:
                responseData = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(responseData.AsSpan(0, 4), 0); // Echo OK
                break;
        }

        // Build Response AMS Header (Swap target/source NetId & Port, Set response flag 0x0001)
        var responseAms = new byte[32 + responseData.Length];
        sourceNetId.CopyTo(responseAms.AsSpan(0, 6)); // Target is request source
        BinaryPrimitives.WriteUInt16LittleEndian(responseAms.AsSpan(6, 2), sourcePort);

        targetNetId.CopyTo(responseAms.AsSpan(8, 6)); // Source is this server
        BinaryPrimitives.WriteUInt16LittleEndian(responseAms.AsSpan(14, 2), targetPort);

        BinaryPrimitives.WriteUInt16LittleEndian(responseAms.AsSpan(16, 2), commandId);
        BinaryPrimitives.WriteUInt16LittleEndian(responseAms.AsSpan(18, 2), (ushort)(stateFlags | 0x0001)); // Response flag
        BinaryPrimitives.WriteUInt32LittleEndian(responseAms.AsSpan(20, 4), (uint)responseData.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(responseAms.AsSpan(24, 4), responseErrorCode);
        BinaryPrimitives.WriteUInt32LittleEndian(responseAms.AsSpan(28, 4), invokeId);

        responseData.CopyTo(responseAms.AsSpan(32));
        return responseAms;
    }

    private byte[] GenerateSimulatedReadPayload(uint indexGroup, uint indexOffset, int requestedLength)
    {
        int len = Math.Max(requestedLength, 8);
        var buffer = new byte[len];

        // Return temperature as float64 at offset 0
        if (SimulatedVariables.TryGetValue("MAIN.TemperatureDegC", out var val) && val is double temp)
        {
            if (len >= 8) BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(0, 8), temp);
        }
        return buffer[0..Math.Min(len, requestedLength)];
    }

    private byte[] GenerateSymbolReadPayload(string symbolName, int requestedLength)
    {
        int len = Math.Max(requestedLength, 8);
        var buffer = new byte[len];

        if (SimulatedVariables.TryGetValue(symbolName, out var val))
        {
            if (val is double d && len >= 8)
                BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(0, 8), d);
            else if (val is uint u && len >= 4)
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), u);
            else if (val is bool b && len >= 1)
                buffer[0] = (byte)(b ? 1 : 0);
        }
        else
        {
            // Default 42.0
            if (len >= 8) BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(0, 8), 42.0);
        }
        return buffer[0..Math.Min(len, requestedLength)];
    }

    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken token)
    {
        int total = 0;
        while (total < count)
        {
            int r = await stream.ReadAsync(buffer.AsMemory(offset + total, count - total), token);
            if (r == 0) break;
            total += r;
        }
        return total;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _tcpListener?.Stop();
        _simulationTicker?.Dispose();
        IsListening = false;
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
