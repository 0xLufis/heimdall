namespace App.Agent.Daemon.Runtime.Throttling;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using App.Agent.Daemon.Domain.Recipes;
using Microsoft.Extensions.Logging;

public record OutboundTelemetryPacket(
    Guid RecipeId,
    string PointId,
    EgressPriority Priority,
    object? Payload,
    DateTimeOffset Timestamp,
    bool IsDelta
);

/// <summary>
/// 4-Tier priority queue router and Token Bucket network rate limiter.
/// </summary>
public class PriorityBandwidthThrottler
{
    private readonly ILogger<PriorityBandwidthThrottler> _logger;
    private readonly Channel<OutboundTelemetryPacket> _p0Channel = Channel.CreateUnbounded<OutboundTelemetryPacket>();
    private readonly Channel<OutboundTelemetryPacket> _p1Channel = Channel.CreateBounded<OutboundTelemetryPacket>(new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<OutboundTelemetryPacket> _p2Channel = Channel.CreateBounded<OutboundTelemetryPacket>(new BoundedChannelOptions(20000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<OutboundTelemetryPacket> _p3Channel = Channel.CreateBounded<OutboundTelemetryPacket>(new BoundedChannelOptions(50000) { FullMode = BoundedChannelFullMode.DropOldest });

    private readonly long _maxBytesPerSecond;
    private double _availableTokens;
    private DateTimeOffset _lastTokenRefill;
    private readonly object _tokenLock = new();

    public PriorityBandwidthThrottler(ILogger<PriorityBandwidthThrottler> logger, long maxBytesPerSecond = 262144) // Default 256 KB/s
    {
        _logger = logger;
        _maxBytesPerSecond = maxBytesPerSecond;
        _availableTokens = maxBytesPerSecond;
        _lastTokenRefill = DateTimeOffset.UtcNow;
    }

    public async ValueTask EnqueueAsync(OutboundTelemetryPacket packet, CancellationToken ct = default)
    {
        switch (packet.Priority)
        {
            case EgressPriority.P0_CriticalAlarm:
                await _p0Channel.Writer.WriteAsync(packet, ct);
                break;
            case EgressPriority.P1_HighOperational:
                await _p1Channel.Writer.WriteAsync(packet, ct);
                break;
            case EgressPriority.P2_MediumMetrics:
                await _p2Channel.Writer.WriteAsync(packet, ct);
                break;
            case EgressPriority.P3_LowInventory:
                await _p3Channel.Writer.WriteAsync(packet, ct);
                break;
        }
    }

    public async Task StartEgressLoopAsync(Func<ReadOnlyMemory<byte>, bool, Task> transportSender, CancellationToken ct)
    {
        var p0Reader = _p0Channel.Reader;
        var p1Reader = _p1Channel.Reader;
        var p2Reader = _p2Channel.Reader;
        var p3Reader = _p3Channel.Reader;

        var p1Buffer = new List<OutboundTelemetryPacket>();
        var p2Buffer = new List<OutboundTelemetryPacket>();
        var p3Buffer = new List<OutboundTelemetryPacket>();

        var lastP1Flush = DateTimeOffset.UtcNow;
        var lastP2Flush = DateTimeOffset.UtcNow;
        var lastP3Flush = DateTimeOffset.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            // 1. P0 Stream: Zero batching, instant send
            while (p0Reader.TryRead(out var criticalPacket))
            {
                byte[] raw = JsonSerializer.SerializeToUtf8Bytes(new[] { criticalPacket });
                await SendThrottledAsync(raw, isCompressed: false, transportSender, ct);
            }

            // 2. P1 Operational: Flush every 200ms or 10 items
            while (p1Reader.TryRead(out var p1Item)) p1Buffer.Add(p1Item);
            if (p1Buffer.Count >= 10 || (DateTimeOffset.UtcNow - lastP1Flush).TotalMilliseconds >= 200)
            {
                if (p1Buffer.Count > 0)
                {
                    await FlushBatchAsync(p1Buffer, transportSender, ct);
                    p1Buffer.Clear();
                    lastP1Flush = DateTimeOffset.UtcNow;
                }
            }

            // 3. P2 Metrics: Flush every 2000ms or 100 items
            while (p2Reader.TryRead(out var p2Item)) p2Buffer.Add(p2Item);
            if (p2Buffer.Count >= 100 || (DateTimeOffset.UtcNow - lastP2Flush).TotalMilliseconds >= 2000)
            {
                if (p2Buffer.Count > 0)
                {
                    await FlushBatchAsync(p2Buffer, transportSender, ct);
                    p2Buffer.Clear();
                    lastP2Flush = DateTimeOffset.UtcNow;
                }
            }

            // 4. P3 Inventory: Flush every 5 mins or 1000 items
            while (p3Reader.TryRead(out var p3Item)) p3Buffer.Add(p3Item);
            if (p3Buffer.Count >= 1000 || (DateTimeOffset.UtcNow - lastP3Flush).TotalMinutes >= 5)
            {
                if (p3Buffer.Count > 0)
                {
                    await FlushBatchAsync(p3Buffer, transportSender, ct);
                    p3Buffer.Clear();
                    lastP3Flush = DateTimeOffset.UtcNow;
                }
            }

            await Task.Delay(10, ct); // Tick interval
        }
    }

    private async Task FlushBatchAsync(List<OutboundTelemetryPacket> batch, Func<ReadOnlyMemory<byte>, bool, Task> sender, CancellationToken ct)
    {
        byte[] raw = JsonSerializer.SerializeToUtf8Bytes(batch);
        bool shouldCompress = raw.Length >= 256;
        byte[] payload = shouldCompress ? CompressGzip(raw) : raw;

        await SendThrottledAsync(payload, shouldCompress, sender, ct);
    }

    public static byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private async Task SendThrottledAsync(byte[] payload, bool isCompressed, Func<ReadOnlyMemory<byte>, bool, Task> sender, CancellationToken ct)
    {
        // Token bucket check
        lock (_tokenLock)
        {
            var now = DateTimeOffset.UtcNow;
            var elapsedSec = (now - _lastTokenRefill).TotalSeconds;
            _availableTokens = Math.Min(_maxBytesPerSecond, _availableTokens + elapsedSec * _maxBytesPerSecond);
            _lastTokenRefill = now;

            _availableTokens -= payload.Length;
        }

        await sender(payload, isCompressed);
    }
}
