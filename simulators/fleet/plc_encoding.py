#!/usr/bin/env python3
"""
Encodes simulated station samples as `AdsPlcMemoryBlock` protobuf messages - the same message
the real agent publishes on `heimdall/devices/{machineIdentifier}/plc_memory`.

Only PLC field names the backend's `StationTelemetryExtractor` recognises are emitted
(src/backend/App.Backend.Api/Services/StationTelemetrySnapshot.cs). The names follow IEC 61131-3
Hungarian notation as a TwinCAT project would (fVibrationRms, nPartsProduced, eMachineState);
the extractor strips the one-letter type prefix and matches case-insensitively.
"""

from __future__ import annotations

import datetime
import os
import sys
from typing import Dict, Optional

_proto_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "proto")
if _proto_dir not in sys.path:
    sys.path.insert(0, _proto_dir)

import telemetry_pb2  # noqa: E402
from google.protobuf.timestamp_pb2 import Timestamp  # noqa: E402

from station_model import STATE_CODES  # noqa: E402

PLC_TYPE_NAME = "ST_HeimdallStationTelemetry"
AMS_PORT = 851

# model key -> (struct group, PLC field name, kind)
FIELD_MAP = {
    "vibration_mms": ("Dynamics", "fVibrationRms", "double"),
    "spindle_rpm": ("Dynamics", "fSpindleSpeedRpm", "double"),
    "cycle_time_ms": ("Dynamics", "fActualCycleTimeMs", "double"),
    "power_kw": ("Dynamics", "fActivePowerKw", "double"),
    "temperature_c": ("Dynamics", "fTemperatureC", "double"),
    "frequency_hz": ("Dynamics", "fLineFrequency", "double"),
    "availability": ("Production", "fAvailability", "double"),
    "performance": ("Production", "fPerformance", "double"),
    "quality": ("Production", "fQuality", "double"),
    "parts_per_hour": ("Production", "fPartsPerHour", "double"),
    "target_parts_per_hour": ("Production", "fTargetPartsPerHour", "double"),
    "cycle_time_s": ("Production", "fCycleTimeSeconds", "double"),
    "target_cycle_time_s": ("Production", "fTargetCycleTimeSeconds", "double"),
    "parts_produced": ("Production", "nPartsProduced", "int"),
    "scrap_parts": ("Production", "nScrapParts", "int"),
}
STATE_FIELD = "eMachineState"


def ams_net_id_for(ip_address: Optional[str]) -> str:
    """TwinCAT convention: AMS Net Id = IPv4 address + '.1.1'."""
    ip = (ip_address or "").strip()
    return f"{ip}.1.1" if ip.count(".") == 3 else "127.0.0.1.1.1"


def symbol_name_for(station_id: str) -> str:
    safe = "".join(c if c.isalnum() else "_" for c in station_id)
    return f"MAIN.fb{safe}.Telemetry._data"


def build_station_block(
    station_id: str,
    sample: Dict[str, object],
    ip_address: Optional[str] = None,
    timestamp: Optional[datetime.datetime] = None,
) -> "telemetry_pb2.AdsPlcMemoryBlock":
    ts = Timestamp()
    ts.FromDatetime(timestamp or datetime.datetime.now(datetime.timezone.utc))

    block = telemetry_pb2.AdsPlcMemoryBlock(
        ams_net_id=ams_net_id_for(ip_address),
        ams_port=AMS_PORT,
        index_group=0xF005,
        index_offset=0,
        timestamp=ts,
        symbol_name=symbol_name_for(station_id),
        plc_type_name=PLC_TYPE_NAME,
        quality=telemetry_pb2.QUALITY_GOOD,
    )
    # ResolveStationAsync attributes the block to the controlled machine named here; required
    # whenever the controller runs more than one station, harmless otherwise.
    block.metadata["StationId"] = station_id
    block.metadata["Source"] = "heimdall-fleet-simulator"

    root = block.structured_value.struct_value
    for key, (group, plc_name, kind) in FIELD_MAP.items():
        if key not in sample or sample[key] is None:
            continue
        target = root.fields[group].struct_value.fields[plc_name]
        if kind == "int":
            target.int64_value = int(sample[key])
        else:
            target.double_value = float(sample[key])

    state = str(sample.get("state") or "")
    if state:
        sv = root.fields[STATE_FIELD].state_value
        sv.state_code = STATE_CODES.get(state, 0)
        sv.state_label = state
    return block
