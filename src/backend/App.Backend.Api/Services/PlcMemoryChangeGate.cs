using System.Security.Cryptography;
using Google.Protobuf;
using App.Shared.Protos;
using App.Shared.Protos.Telemetry;

namespace App.Backend.Api.Services;

/// <summary>
/// Last forwarded state of one PLC memory block, kept in the two-level cache (process memory +
/// Redis) so the comparison survives backend restarts and is shared between instances.
/// </summary>
public sealed class PlcMemoryGateState
{
    public string ContentHash { get; set; } = string.Empty;
    public string MetaSignature { get; set; } = string.Empty;
    public DateTimeOffset ForwardedAt { get; set; }
    public DateTimeOffset PersistedAt { get; set; }
}

/// <summary>Fingerprints used to decide whether an incoming PLC memory block is new information.</summary>
public static class PlcMemoryChangeGate
{
    public static readonly TimeSpan StateRetention = TimeSpan.FromHours(1);

    /// <summary>Identity of a block: one PLC symbol/address on one machine.</summary>
    public static string Key(string machineIdentifier, AdsPlcMemoryBlock block) =>
        $"plc-memory-gate:{machineIdentifier}:{block.AmsNetId}:{block.AmsPort}:{block.IndexGroup}:{block.IndexOffset}:{block.SymbolName}";

    /// <summary>
    /// Hash of everything in the block that carries information: raw memory, the structured value,
    /// descriptor and metadata. Only the sample timestamp is excluded, since it moves on every read.
    /// </summary>
    public static string ContentHash(AdsPlcMemoryBlock block)
    {
        var copy = block.Clone();
        copy.Timestamp = null;
        return Convert.ToHexString(SHA256.HashData(copy.ToByteArray()));
    }

    /// <summary>The descriptor fields that are persisted on the ClientPc and sent with PlcMemoryReceived.</summary>
    public static string MetaSignature(AdsPlcMemoryBlock block) =>
        $"{block.AmsNetId}|{block.AmsPort}|{block.IndexGroup}|{block.IndexOffset}|{block.SymbolName}|{block.PlcTypeName}|{block.Quality}|{block.RawMemory.Length}";
}
