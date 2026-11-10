namespace App.Agent.Daemon.Infrastructure.Beckhoff;

using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ST_EcSlaveState
{
    public byte DeviceState; // Lower 4 bits = State, Upper 4 bits = Error Flags
    public byte LinkState;   // Link status & port connection flags
}

public record EtherCatMasterTelemetry(
    string MasterAmsNetId,
    ushort MasterState,
    string MasterStateLabel,
    ushort DevStateRaw,
    bool LinkError,
    bool MissingFrames,
    bool SlaveErrorPresent,
    bool DcNotInSync,
    ushort SlaveCount
);

public record EtherCatSlaveTelemetry(
    ushort SlaveAddress,
    string StateLabel,
    bool HasError,
    byte LinkStateRaw,
    bool LinkPresent
);

/// <summary>
/// Bitmask decoding and telemetry parser for Beckhoff EtherCAT Master and Slaves.
/// </summary>
public static class EtherCatDiagnosticsHelper
{
    public const ushort EC_DEVSTATE_LINKERROR = 0x0001;
    public const ushort EC_DEVSTATE_IOLOCKED = 0x0002;
    public const ushort EC_DEVSTATE_MISSING_FRAME = 0x0008;
    public const ushort EC_DEVSTATE_SLAVE_ERROR = 0x0800;
    public const ushort EC_DEVSTATE_DC_NOT_IN_SYNC = 0x1000;

    public static EtherCatMasterTelemetry DecodeMasterState(string netId, ushort stateRaw, ushort devStateRaw, ushort slaveCount)
    {
        string stateLabel = stateRaw switch
        {
            0x01 => "INIT",
            0x02 => "PREOP",
            0x03 => "BOOT",
            0x04 => "SAFEOP",
            0x08 => "OP",
            _ => $"UNKNOWN(0x{stateRaw:X})"
        };

        return new EtherCatMasterTelemetry(
            MasterAmsNetId: netId,
            MasterState: stateRaw,
            MasterStateLabel: stateLabel,
            DevStateRaw: devStateRaw,
            LinkError: (devStateRaw & EC_DEVSTATE_LINKERROR) != 0,
            MissingFrames: (devStateRaw & EC_DEVSTATE_MISSING_FRAME) != 0,
            SlaveErrorPresent: (devStateRaw & EC_DEVSTATE_SLAVE_ERROR) != 0,
            DcNotInSync: (devStateRaw & EC_DEVSTATE_DC_NOT_IN_SYNC) != 0,
            SlaveCount: slaveCount
        );
    }

    public static EtherCatSlaveTelemetry DecodeSlaveState(ushort address, ST_EcSlaveState raw)
    {
        byte state = (byte)(raw.DeviceState & 0x0F);
        bool hasError = (raw.DeviceState & 0x10) != 0;
        bool linkPresent = (raw.LinkState & 0x01) == 0;

        string stateStr = state switch
        {
            0x01 => "INIT",
            0x02 => "PREOP",
            0x03 => "BOOT",
            0x04 => "SAFEOP",
            0x08 => "OP",
            _ => $"UNKNOWN(0x{state:X})"
        };

        return new EtherCatSlaveTelemetry(
            SlaveAddress: address,
            StateLabel: stateStr,
            HasError: hasError,
            LinkStateRaw: raw.LinkState,
            LinkPresent: linkPresent
        );
    }
}
