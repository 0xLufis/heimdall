namespace App.Agent.Daemon.Infrastructure.Modbus;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

public enum EndiannessFormat
{
    ABCD_BigEndian,           // Standard Big Endian
    CDAB_MidBigEndian_WordSwap,// De facto standard Float32 in Industrial PLCs (Schneider, ABB)
    BADC_MidLittle_ByteSwap,
    DCBA_LittleEndian
}

public record ModbusTagRequest(string TagName, ushort Address, ushort Length);
public record RegisterBlock(ushort StartAddress, ushort TotalLength, List<ModbusTagRequest> Tags);

/// <summary>
/// Endianness byte/word swapper and contiguous register batch optimizer for Modbus TCP.
/// </summary>
public static class ModbusEndiannessConverter
{
    public static float RegistersToFloat32(ushort reg0, ushort reg1, EndiannessFormat format = EndiannessFormat.CDAB_MidBigEndian_WordSwap)
    {
        Span<byte> bytes = stackalloc byte[4];
        
        switch (format)
        {
            case EndiannessFormat.ABCD_BigEndian:
                BinaryPrimitives.WriteUInt16BigEndian(bytes[0..2], reg0);
                BinaryPrimitives.WriteUInt16BigEndian(bytes[2..4], reg1);
                break;
            case EndiannessFormat.CDAB_MidBigEndian_WordSwap:
                BinaryPrimitives.WriteUInt16BigEndian(bytes[0..2], reg1);
                BinaryPrimitives.WriteUInt16BigEndian(bytes[2..4], reg0);
                break;
            case EndiannessFormat.BADC_MidLittle_ByteSwap:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes[0..2], reg0);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes[2..4], reg1);
                break;
            case EndiannessFormat.DCBA_LittleEndian:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes[0..2], reg1);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes[2..4], reg0);
                break;
        }

        return BinaryPrimitives.ReadSingleBigEndian(bytes);
    }

    public static int RegistersToInt32(ushort reg0, ushort reg1, EndiannessFormat format = EndiannessFormat.CDAB_MidBigEndian_WordSwap)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (format == EndiannessFormat.CDAB_MidBigEndian_WordSwap)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes[0..2], reg1);
            BinaryPrimitives.WriteUInt16BigEndian(bytes[2..4], reg0);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes[0..2], reg0);
            BinaryPrimitives.WriteUInt16BigEndian(bytes[2..4], reg1);
        }
        return BinaryPrimitives.ReadInt32BigEndian(bytes);
    }

    /// <summary>
    /// Groups adjacent register requests into contiguous chunks to reduce roundtrips.
    /// </summary>
    public static List<RegisterBlock> OptimizeRegisterBlocks(
        IEnumerable<ModbusTagRequest> tags, 
        ushort maxBlockSize = 120, 
        ushort maxGapAllowance = 5)
    {
        var sortedTags = tags.OrderBy(t => t.Address).ToList();
        var blocks = new List<RegisterBlock>();
        if (sortedTags.Count == 0) return blocks;

        ushort currentStart = sortedTags[0].Address;
        ushort currentEnd = (ushort)(currentStart + sortedTags[0].Length);
        var currentBlockTags = new List<ModbusTagRequest> { sortedTags[0] };

        for (int i = 1; i < sortedTags.Count; i++)
        {
            var tag = sortedTags[i];
            ushort tagEnd = (ushort)(tag.Address + tag.Length);

            if (tagEnd - currentStart <= maxBlockSize && (tag.Address - currentEnd) <= maxGapAllowance)
            {
                currentEnd = Math.Max(currentEnd, tagEnd);
                currentBlockTags.Add(tag);
            }
            else
            {
                blocks.Add(new RegisterBlock(currentStart, (ushort)(currentEnd - currentStart), currentBlockTags));
                currentStart = tag.Address;
                currentEnd = tagEnd;
                currentBlockTags = new List<ModbusTagRequest> { tag };
            }
        }

        blocks.Add(new RegisterBlock(currentStart, (ushort)(currentEnd - currentStart), currentBlockTags));
        return blocks;
    }
}
