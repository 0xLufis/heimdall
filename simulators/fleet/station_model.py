#!/usr/bin/env python3
"""
Station process model for the Heimdall fleet simulator (dev / demo only).

Pure standard library: no MQTT, no protobuf. One `StationProcess` simulates the PLC-side
telemetry of one production station: machine dynamics (vibration, spindle speed, cycle time,
power, temperature, frequency), production counters / KPIs and an operational state machine
(RUNNING / IDLE / WARNING / FAULTED / MAINTENANCE).

The model advances in fixed simulated time steps, and every station owns a `random.Random`
seeded from (global seed, station id). Given the same seed and the same sequence of
`step(dt)` / scenario calls the output is identical, independent of wall-clock timing.
"""

from __future__ import annotations

import math
import random
import zlib
from dataclasses import dataclass, field
from typing import Dict, List, Optional

RUNNING = "RUNNING"
IDLE = "IDLE"
WARNING = "WARNING"
FAULTED = "FAULTED"
MAINTENANCE = "MAINTENANCE"
STATES = (RUNNING, IDLE, WARNING, FAULTED, MAINTENANCE)

# PLC enum codes published in the DeviceStateValue (labels are what the backend reads).
STATE_CODES = {IDLE: 0, RUNNING: 1, WARNING: 2, FAULTED: 3, MAINTENANCE: 4}

AMBIENT_C = 24.0


@dataclass(frozen=True)
class MachineProfile:
    """Nominal operating point of a machine type. `None` means the machine has no such signal."""
    cycle_time_s: float
    power_kw: float
    standby_power_kw: float
    temperature_c: float           # steady-state process temperature while producing
    temp_warn_c: float
    temp_alarm_c: float
    vibration_mms: float
    vib_warn_mms: float
    vib_alarm_mms: float
    spindle_rpm: Optional[float] = None
    frequency_hz: Optional[float] = 50.0
    scrap_rate: float = 0.012
    thermal_tau_s: float = 240.0   # first-order thermal time constant
    power_warn_low_ratio: Optional[float] = None   # WARNING when power falls below nominal * ratio
    power_alarm_low_ratio: Optional[float] = None  # FAULTED when power falls below nominal * ratio


PROFILES: Dict[str, MachineProfile] = {
    "ConveyorTransfer": MachineProfile(16.0, 2.2, 0.3, 38.0, 55.0, 65.0, 0.9, 2.8, 4.5, spindle_rpm=1450.0, scrap_rate=0.002),
    "CncMachining": MachineProfile(48.0, 11.5, 1.4, 46.0, 62.0, 72.0, 1.8, 3.5, 4.5, spindle_rpm=12000.0, frequency_hz=200.0, scrap_rate=0.015),
    "Dispenser": MachineProfile(22.0, 1.6, 0.4, 41.0, 52.0, 60.0, 0.5, 1.8, 2.8, scrap_rate=0.018),
    "RobotCell": MachineProfile(28.0, 6.4, 0.9, 44.0, 60.0, 70.0, 1.2, 3.0, 4.5, spindle_rpm=3000.0, scrap_rate=0.008),
    "LaserWelder": MachineProfile(34.0, 6.0, 0.6, 46.0, 52.0, 55.0, 0.6, 2.0, 3.2, scrap_rate=0.014,
                                  power_warn_low_ratio=0.975, power_alarm_low_ratio=0.95),
    "LaserSource": MachineProfile(34.0, 6.0, 0.6, 42.0, 50.0, 56.0, 0.3, 1.2, 2.0, scrap_rate=0.0,
                                  power_warn_low_ratio=0.975, power_alarm_low_ratio=0.95),
    "LaserMarker": MachineProfile(12.0, 0.9, 0.2, 36.0, 48.0, 55.0, 0.3, 1.2, 2.0, scrap_rate=0.004),
    "VisionInspection": MachineProfile(9.0, 0.7, 0.3, 34.0, 46.0, 54.0, 0.25, 1.0, 1.8, frequency_hz=60.0, scrap_rate=0.03),
    "FasteningStation": MachineProfile(18.0, 2.8, 0.4, 39.0, 54.0, 62.0, 1.4, 3.2, 4.8, spindle_rpm=850.0, scrap_rate=0.01),
    "ServoPress": MachineProfile(20.0, 7.5, 0.8, 43.0, 58.0, 66.0, 1.6, 3.6, 5.0, spindle_rpm=600.0, scrap_rate=0.009),
    "EOLTester": MachineProfile(42.0, 3.4, 0.7, 36.0, 50.0, 58.0, 0.7, 2.2, 3.5, spindle_rpm=4500.0, scrap_rate=0.025),
}
DEFAULT_PROFILE = MachineProfile(24.0, 3.0, 0.5, 40.0, 55.0, 65.0, 1.0, 3.0, 4.5, spindle_rpm=1500.0)


def profile_for(machine_type: Optional[str]) -> MachineProfile:
    return PROFILES.get(machine_type or "", DEFAULT_PROFILE)


# Drift profiles: fraction of full effect is `progress` in [0, 1], ramped over the drift duration.
#   power_ratio     relative change of active power at full drift (negative = power loss)
#   temp_c          additional process temperature at full drift
#   vibration_ratio relative vibration increase at full drift
#   cycle_ratio     relative cycle time increase at full drift
#   scrap           additional scrap probability at full drift
DRIFT_PROFILES: Dict[str, Dict[str, float]] = {
    # The Nordvik demo story: protective glass contamination - laser power sags, glass heats up.
    "laser_drift": {"power_ratio": -0.07, "temp_c": 11.0, "scrap": 0.06},
    "bearing_wear": {"vibration_ratio": 3.2, "temp_c": 9.0, "cycle_ratio": 0.06, "scrap": 0.02},
    "thermal": {"temp_c": 26.0, "cycle_ratio": 0.04},
    "cycle_slowdown": {"cycle_ratio": 0.35},
}


# Alarm a station raises when it faults, by fault reason. Machine types refine the generic codes.
ALARM_CODES: Dict[str, str] = {
    "power below minimum": "ALM-LSR-217",
    "temperature alarm": "ALM-TMP-301",
    "vibration alarm": "ALM-VIB-045",
    "injected fault": "ALM-GEN-001",
    "spontaneous fault": "ALM-GEN-002",
}
ALARM_CODES_BY_TYPE: Dict[str, Dict[str, str]] = {
    "LaserWelder": {"temperature alarm": "ALM-LSR-218"},
    "LaserSource": {"temperature alarm": "ALM-LSR-218"},
    "CncMachining": {"vibration alarm": "ALM-SPN-045"},
}
STANDSTILL_LOAD = 0.02   # below this the drives are off: no process noise, constant readings


def alarm_code_for(machine_type: Optional[str], reason: str) -> str:
    return ALARM_CODES_BY_TYPE.get(machine_type or "", {}).get(reason) or ALARM_CODES.get(reason) or "ALM-GEN-001"


@dataclass
class Drift:
    profile: str
    duration_s: float
    magnitude: float = 1.0
    elapsed_s: float = 0.0

    @property
    def progress(self) -> float:
        if self.duration_s <= 0:
            return 1.0
        return max(0.0, min(1.0, self.elapsed_s / self.duration_s))

    def effect(self, key: str) -> float:
        return DRIFT_PROFILES[self.profile].get(key, 0.0) * self.magnitude * self.progress


@dataclass
class StationSpec:
    """Identity of one simulated station, taken from the loaded dataset."""
    station_id: str                      # value sent as block metadata StationId (custom identifier / name)
    name: str
    machine_type: Optional[str]
    controller_machine_identifier: str   # ClientPc.machine_identifier whose plc_memory topic carries the block
    handle: Optional[str] = None         # floor-plan handle, informational
    line: Optional[str] = None
    technology: Optional[str] = None
    cycle_time_target_s: Optional[float] = None
    nominal_power_kw: Optional[float] = None
    temp_alarm_c: Optional[float] = None
    power_alarm_low_kw: Optional[float] = None
    parent_station_id: Optional[str] = None      # sub-machine of a cell: stops when the cell stops
    custom_identifier: Optional[str] = None      # stations.custom_identifier when it differs from station_id
    components: List[Dict[str, str]] = field(default_factory=list)   # [{slot, partClass, name}]


def stable_seed(global_seed: int, key: str) -> int:
    """Seed that does not depend on PYTHONHASHSEED."""
    return (int(global_seed) * 1_000_003 + zlib.crc32(key.encode("utf-8"))) & 0xFFFFFFFF


class StationProcess:
    def __init__(self, spec: StationSpec, seed: int = 0, random_fault_rate: float = 0.0):
        self.spec = spec
        self.profile = profile_for(spec.machine_type)
        self.rng = random.Random(stable_seed(seed, spec.station_id))
        self.random_fault_rate = random_fault_rate   # expected spontaneous faults per simulated hour

        p = self.profile
        self.target_cycle_s = float(spec.cycle_time_target_s or p.cycle_time_s)
        self.nominal_power_kw = float(spec.nominal_power_kw or p.power_kw)
        self.temp_alarm_c = float(spec.temp_alarm_c or p.temp_alarm_c)
        # Keep the warning band below a dataset-provided alarm limit.
        self.temp_warn_c = min(p.temp_warn_c, self.temp_alarm_c - 2.0)
        self.power_warn_low_kw = self.nominal_power_kw * p.power_warn_low_ratio if p.power_warn_low_ratio else None
        if spec.power_alarm_low_kw:
            self.power_alarm_low_kw: Optional[float] = float(spec.power_alarm_low_kw)
        else:
            self.power_alarm_low_kw = self.nominal_power_kw * p.power_alarm_low_ratio if p.power_alarm_low_ratio else None
        self.target_parts_per_hour = round(3600.0 / self.target_cycle_s, 1)

        # --- state machine ---
        self.state = RUNNING
        self.state_reason = "auto"
        self.alarm_code: Optional[str] = None   # set while FAULTED
        self.sim_time_s = 0.0
        self._idle_remaining_s = 0.0        # short starved/blocked pauses
        self._fault_remaining_s: Optional[float] = None   # None = latched until recover()
        self._hold: Optional[str] = None    # "fault" | "maintenance" | "line_stop"
        self._hold_remaining_s: Optional[float] = None
        self.drift: Optional[Drift] = None

        # --- dynamics (start at the steady operating point) ---
        self.temperature_c = p.temperature_c + self.rng.uniform(-1.5, 1.5)
        self.load = 1.0                     # 0..1 ramp of drives/process
        self._noise = {"vib": 0.0, "pwr": 0.0, "rpm": 0.0, "cyc": 0.0, "tmp": 0.0, "frq": 0.0}
        self.vibration_mms = p.vibration_mms
        self.power_kw = self.nominal_power_kw
        self.spindle_rpm = p.spindle_rpm
        self.frequency_hz = p.frequency_hz
        self.cycle_time_s = self.target_cycle_s

        # --- production accounting (pre-loaded with a believable shift history) ---
        hist_h = self.rng.uniform(2.0, 5.5)
        avail0 = self.rng.uniform(0.88, 0.97)
        perf0 = self.rng.uniform(0.9, 0.99)
        self._planned_s = hist_h * 3600.0
        self._run_s = self._planned_s * avail0
        parts0 = int(self._run_s / self.target_cycle_s * perf0)
        self.parts_produced = parts0
        self.scrap_parts = int(parts0 * p.scrap_rate * self.rng.uniform(0.6, 1.4))
        self._ideal_s = parts0 * self.target_cycle_s
        self._part_progress = self.rng.random()
        self._pph_ema = self.target_parts_per_hour * perf0

    # ------------------------------------------------------------------ scenarios
    def inject_fault(self, duration_s: Optional[float] = None, reason: str = "injected fault",
                     alarm_code: Optional[str] = None) -> None:
        self._hold = "fault"
        self._hold_remaining_s = float(duration_s) if duration_s else None
        self._enter(FAULTED, reason, alarm_code)

    def start_maintenance(self, duration_s: Optional[float] = None) -> None:
        self._hold = "maintenance"
        self._hold_remaining_s = float(duration_s) if duration_s else None
        self._enter(MAINTENANCE, "maintenance")

    def line_stop(self, duration_s: Optional[float] = None, reason: str = "line stop") -> None:
        if self._hold in ("fault", "maintenance"):
            return
        self._hold = "line_stop"
        self._hold_remaining_s = float(duration_s) if duration_s else None
        self._enter(IDLE, reason)

    def release_line_stop(self, reason: str = "line stop") -> None:
        """Ends a line stop that was started with the same reason (other holds are left alone)."""
        if self._hold == "line_stop" and self.state_reason == reason:
            self._hold = None
            self._hold_remaining_s = None
            self._enter(RUNNING, "auto")

    def start_drift(self, profile: str, duration_s: float = 600.0, magnitude: float = 1.0) -> None:
        if profile not in DRIFT_PROFILES:
            raise ValueError(f"unknown drift profile '{profile}' (known: {', '.join(sorted(DRIFT_PROFILES))})")
        self.drift = Drift(profile=profile, duration_s=max(1.0, float(duration_s)), magnitude=float(magnitude))

    def recover(self) -> None:
        """Clears every injected scenario and returns the station to production."""
        self._hold = None
        self._hold_remaining_s = None
        self._fault_remaining_s = None
        self._idle_remaining_s = 0.0
        self.drift = None
        self._enter(RUNNING, "recovered")

    @property
    def scenario(self) -> Optional[str]:
        if self._hold:
            return self._hold
        if self.drift:
            return f"drift:{self.drift.profile}"
        return None

    # ------------------------------------------------------------------ simulation
    def _enter(self, state: str, reason: str, alarm_code: Optional[str] = None) -> None:
        self.state = state
        self.state_reason = reason
        # A faulted station raises an alarm; any other state clears it.
        self.alarm_code = (alarm_code or alarm_code_for(self.spec.machine_type, reason)) if state == FAULTED else None

    @property
    def producing(self) -> bool:
        return self.state in (RUNNING, WARNING)

    def _ar1(self, key: str, sigma: float, alpha: float = 0.82) -> float:
        """Correlated (AR(1)) noise so consecutive samples look like a process, not white noise."""
        v = alpha * self._noise[key] + (1.0 - alpha) * self.rng.gauss(0.0, sigma) * 3.0
        self._noise[key] = v
        return v

    def step(self, dt: float) -> None:
        """Advances the station by `dt` simulated seconds."""
        if dt <= 0:
            return
        p = self.profile
        self.sim_time_s += dt

        # 1. timed holds
        if self._hold and self._hold_remaining_s is not None:
            self._hold_remaining_s -= dt
            if self._hold_remaining_s <= 0:
                self._hold = None
                self._hold_remaining_s = None
                self._enter(RUNNING, "auto restart")

        # 2. autonomous transitions (only when no scenario pins the state)
        if not self._hold:
            if self.state == FAULTED:
                if self._fault_remaining_s is not None:
                    self._fault_remaining_s -= dt
                    if self._fault_remaining_s <= 0:
                        self._fault_remaining_s = None
                        self._enter(RUNNING, "fault acknowledged")
            elif self.state == IDLE:
                self._idle_remaining_s -= dt
                if self._idle_remaining_s <= 0:
                    self._enter(RUNNING, "auto")
            elif self.producing:
                # short starved / blocked pauses: about 3 per simulated hour
                if self.rng.random() < dt * 3.0 / 3600.0:
                    self._idle_remaining_s = self.rng.uniform(15.0, 90.0)
                    self._enter(IDLE, self.rng.choice(("starved", "blocked")))
                elif self.random_fault_rate > 0 and self.rng.random() < dt * self.random_fault_rate / 3600.0:
                    self._fault_remaining_s = self.rng.uniform(90.0, 420.0)
                    self._enter(FAULTED, "spontaneous fault")

        if self.drift:
            self.drift.elapsed_s += dt
        drift = self.drift
        d = (lambda key: drift.effect(key)) if drift else (lambda key: 0.0)

        # 3. drives ramp towards the load the state asks for
        target_load = 1.0 if self.producing else 0.0
        ramp = min(1.0, dt / 6.0)
        self.load += (target_load - self.load) * ramp

        # 4. dynamics
        standstill = self.load < STANDSTILL_LOAD and not self.producing
        if standstill:
            self.load = 0.0
            self.power_kw = p.standby_power_kw * (0.5 if self.state == MAINTENANCE else 1.0)
            self.vibration_mms = 0.0
            if p.spindle_rpm is not None:
                self.spindle_rpm = 0.0
            if p.frequency_hz is not None:
                self.frequency_hz = p.frequency_hz
        else:
            self.power_kw = max(0.0, (
                p.standby_power_kw
                + (self.nominal_power_kw - p.standby_power_kw) * self.load * (1.0 + d("power_ratio"))
                + self._ar1("pwr", self.nominal_power_kw * 0.004) * self.load
            )) if self.state != MAINTENANCE else max(0.0, p.standby_power_kw * 0.5)

            fault_vib = 2.4 if (self.state == FAULTED and self.load > 0.05) else 1.0
            self.vibration_mms = max(0.02, (
                0.04 + p.vibration_mms * self.load * (1.0 + d("vibration_ratio")) * fault_vib
                + self._ar1("vib", p.vibration_mms * 0.06) * max(self.load, 0.1)
            ))

            if p.spindle_rpm is not None:
                self.spindle_rpm = max(0.0, p.spindle_rpm * self.load * (1.0 - 0.04 * d("cycle_ratio"))
                                       + self._ar1("rpm", p.spindle_rpm * 0.003) * self.load)
            if p.frequency_hz is not None:
                self.frequency_hz = p.frequency_hz + self._ar1("frq", 0.02)

        # temperature: first-order lag towards the state-dependent target
        temp_target = AMBIENT_C + (p.temperature_c - AMBIENT_C) * self.load + d("temp_c") * max(self.load, 0.25)
        if self.state == FAULTED and self._hold == "fault":
            temp_target += 6.0 * self.load
        k = 1.0 - math.exp(-dt / p.thermal_tau_s)
        self.temperature_c += (temp_target - self.temperature_c) * k
        # at standstill the sensor reads the (slowly settling) body temperature, coarsely quantised
        measured_temp = round(self.temperature_c, 1) if standstill else self.temperature_c + self._ar1("tmp", 0.12)

        # 5. production
        self._planned_s += dt if self.state != MAINTENANCE else 0.0
        if self.producing:
            self.cycle_time_s = max(0.2, self.target_cycle_s * (1.0 + d("cycle_ratio"))
                                    * (1.0 + 0.02 + self._ar1("cyc", 0.012)))
            self._run_s += dt
            self._part_progress += dt / self.cycle_time_s
            scrap_p = min(0.9, p.scrap_rate + d("scrap"))
            while self._part_progress >= 1.0:
                self._part_progress -= 1.0
                self.parts_produced += 1
                self._ideal_s += self.target_cycle_s
                if self.rng.random() < scrap_p:
                    self.scrap_parts += 1
            rate = 3600.0 / self.cycle_time_s
        else:
            rate = 0.0
            self.cycle_time_s = 0.0          # a stopped station has no cycle time
        a = min(1.0, dt / 120.0)
        self._pph_ema += (rate - self._pph_ema) * a

        # 6. condition monitoring: thresholds drive WARNING / FAULTED, not dice
        if self.producing:
            alarm = None
            warn = None
            if measured_temp >= self.temp_alarm_c:
                alarm = "temperature alarm"
            elif self.vibration_mms >= p.vib_alarm_mms:
                alarm = "vibration alarm"
            elif self.power_alarm_low_kw is not None and self.load > 0.95 and self.power_kw <= self.power_alarm_low_kw:
                alarm = "power below minimum"
            elif measured_temp >= self.temp_warn_c:
                warn = "temperature high"
            elif self.vibration_mms >= p.vib_warn_mms:
                warn = "vibration high"
            elif self.power_warn_low_kw is not None and self.load > 0.95 and self.power_kw <= self.power_warn_low_kw:
                warn = "power low"
            elif d("cycle_ratio") >= 0.12:
                warn = "cycle time high"

            if alarm:
                # latched: stays FAULTED until recover() (the drift is still there)
                self._fault_remaining_s = None
                self._enter(FAULTED, alarm)
            elif warn:
                self._enter(WARNING, warn)
            elif self.state == WARNING:
                self._enter(RUNNING, "auto")

        self._measured_temp = measured_temp

    # ------------------------------------------------------------------ outputs
    @property
    def availability(self) -> float:
        return round(100.0 * self._run_s / self._planned_s, 2) if self._planned_s > 0 else 0.0

    @property
    def performance(self) -> float:
        return round(min(100.0, 100.0 * self._ideal_s / self._run_s), 2) if self._run_s > 0 else 0.0

    @property
    def quality(self) -> float:
        if self.parts_produced <= 0:
            return 100.0
        return round(100.0 * (self.parts_produced - self.scrap_parts) / self.parts_produced, 2)

    def sample(self) -> Dict[str, object]:
        """Current PLC-side values. Keys are model names; plc_encoding maps them to PLC symbols."""
        out: Dict[str, object] = {
            "state": self.state,
            "vibration_mms": round(self.vibration_mms, 3),
            "cycle_time_ms": round(self.cycle_time_s * 1000.0, 1),
            "power_kw": round(self.power_kw, 3),
            "temperature_c": round(getattr(self, "_measured_temp", self.temperature_c), 2),
            "availability": self.availability,
            "performance": self.performance,
            "quality": self.quality,
            "parts_per_hour": round(max(0.0, self._pph_ema), 1),
            "target_parts_per_hour": self.target_parts_per_hour,
            "cycle_time_s": round(self.cycle_time_s, 3),
            "target_cycle_time_s": round(self.target_cycle_s, 3),
            "parts_produced": int(self.parts_produced),
            "scrap_parts": int(self.scrap_parts),
        }
        if self.profile.spindle_rpm is not None:
            out["spindle_rpm"] = round(float(self.spindle_rpm or 0.0), 1)
        if self.profile.frequency_hz is not None:
            out["frequency_hz"] = round(float(self.frequency_hz or 0.0), 3)
        return out

    def describe(self) -> Dict[str, object]:
        """Status entry for the control API."""
        s = self.spec
        return {
            "id": s.station_id,
            "name": s.name,
            "handle": s.handle,
            "line": s.line,
            "machineType": s.machine_type,
            "controller": s.controller_machine_identifier,
            "state": self.state,
            "reason": self.state_reason,
            "alarm": self.alarm_code,
            "scenario": self.scenario,
            "driftProgress": round(self.drift.progress, 3) if self.drift else None,
            "values": self.sample(),
        }


class PlantModel:
    """All simulated stations plus the plant-level scenarios (line stop, demo story, speed)."""

    def __init__(self, specs: List[StationSpec], seed: int = 0, random_fault_rate: float = 0.0):
        self.seed = seed
        self.speed = 1.0
        self.stations: Dict[str, StationProcess] = {}
        for spec in specs:
            key = spec.station_id.upper()
            if key not in self.stations:
                self.stations[key] = StationProcess(spec, seed=seed, random_fault_rate=random_fault_rate)

    def get(self, station_id: Optional[str]) -> Optional[StationProcess]:
        if not station_id:
            return None
        key = str(station_id).strip().upper()
        if key in self.stations:
            return self.stations[key]
        for st in self.stations.values():
            if (st.spec.handle or "").upper() == key or st.spec.name.upper() == key:
                return st
        return None

    def lines(self) -> List[str]:
        return sorted({st.spec.line for st in self.stations.values() if st.spec.line})

    def line_stations(self, line: Optional[str]) -> List[StationProcess]:
        if not line:
            return []
        key = str(line).strip().upper()
        return [st for st in self.stations.values() if (st.spec.line or "").upper() == key]

    def set_speed(self, speed: float) -> float:
        self.speed = max(0.1, min(120.0, float(speed)))
        return self.speed

    def set_random_fault_rate(self, per_hour: float) -> None:
        for st in self.stations.values():
            st.random_fault_rate = max(0.0, float(per_hour))

    def step(self, dt_wall: float) -> None:
        dt = dt_wall * self.speed
        for st in self.stations.values():
            st.step(dt)
        # A sub-machine cannot produce while its cell is stopped (fault, maintenance, line stop).
        for st in self.stations.values():
            parent = self.stations.get((st.spec.parent_station_id or "").upper())
            if parent is None:
                continue
            if not parent.producing:
                st.line_stop(reason="cell stopped")
            else:
                st.release_line_stop("cell stopped")

    def recover(self, station: Optional[str] = None, line: Optional[str] = None) -> List[str]:
        if station:
            st = self.get(station)
            targets = [st] if st else []
        elif line:
            targets = self.line_stations(line)
        else:
            targets = list(self.stations.values())
        for st in targets:
            st.recover()
        return [st.spec.station_id for st in targets]

    def apply_scenario(self, name: str) -> List[str]:
        """Named presets. Returns a human-readable list of what was applied."""
        applied: List[str] = []
        if name == "demo_story":
            # Nordvik NV1: the rotor laser welding cell loses power while the protective glass heats up,
            # the housing CNC spindle slowly wears, resin flow on L1 slows the dispenser.
            for sid, profile, duration in (
                ("NV-L2-OP060", "laser_drift", 900.0),
                ("NV-L3-OP020", "bearing_wear", 3600.0),
                ("NV-L1-OP030", "cycle_slowdown", 600.0),
            ):
                st = self.get(sid)
                if st:
                    st.start_drift(profile, duration_s=duration)
                    applied.append(f"{sid}: {profile}")
            if not applied:
                # Dataset without the demo stations: drift the first laser welder / first station instead.
                st = next((s for s in self.stations.values() if s.spec.machine_type == "LaserWelder"), None) \
                    or next(iter(self.stations.values()), None)
                if st:
                    profile = "laser_drift" if st.spec.machine_type in ("LaserWelder", "LaserSource") else "bearing_wear"
                    st.start_drift(profile, duration_s=900.0)
                    applied.append(f"{st.spec.station_id}: {profile}")
        elif name == "clear":
            self.recover()
            applied.append("all stations recovered")
        else:
            raise ValueError(f"unknown scenario '{name}' (known: demo_story, clear)")
        return applied


SCENARIOS = ("demo_story", "clear")
