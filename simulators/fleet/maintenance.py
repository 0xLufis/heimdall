#!/usr/bin/env python3
"""
Maintenance desk of the fleet simulator (dev / demo only): turns simulated station faults into
maintenance work, the way a plant produces it.

    fault (scheduled, injected or reached through a drift)
      -> the controller raises a ticket                      raise
      -> a technician of the responsible crew takes it       reserve, pickup (station goes to MAINTENANCE)
      -> a spare is taken from the store when the fix needs one   consume  (or pending -> restock -> resume)
      -> long repairs are escalated                           escalate / deescalate
      -> the station runs again                               resolve, then close once it stayed healthy

This module is pure: no I/O, no wall clock. `MaintenanceDesk.step()` advances in simulated time and
returns `Action`s; `backend_client.ActionDispatcher` performs them against the backend REST API.
With the same seed and the same sequence of steps the action log is identical.
"""

from __future__ import annotations

import random
from collections import deque
from dataclasses import dataclass, field
from typing import Deque, Dict, List, Optional, Tuple

from station_model import FAULTED, PlantModel, StationProcess, stable_seed
from topology import PlantProfile


@dataclass(frozen=True)
class FaultMode:
    code: str                                   # alarm code (PLC / NET / COMM / ELEC in the code steer the backend's dispatch)
    title: str
    description: str
    priority: str = "High"                      # Critical | High | Medium | Low
    line_stop: bool = False
    drift: Optional[str] = None                 # reach the fault through this drift profile instead of faulting at once
    slot: Optional[str] = None                  # component slot the alarm points at
    resources: Tuple[Tuple[str, str], ...] = () # ("slot" | "consumable", class) in order of preference
    repair_min: Tuple[float, float] = (10.0, 25.0)
    weight: float = 1.0


def _m(code, title, description, **kw) -> FaultMode:
    return FaultMode(code=code, title=title, description=description, **kw)


# Failure modes per machine type. Resource classes are the partClass / consumableClass values of the datasets;
# a class the loaded dataset does not stock is skipped, so the same catalogue serves both plants.
FAULT_MODES: Dict[str, Tuple[FaultMode, ...]] = {
    "ConveyorTransfer": (
        _m("ALM-CNV-101", "Pallet jam at stopper", "Pallet did not leave the stopper position within the monitoring time.",
           priority="Medium", slot="CONVEYOR-01", resources=(("consumable", "PROX-SENSOR"), ("consumable", "PNEU-FITTING")), repair_min=(6, 15), weight=2),
        _m("ALM-ELEC-CNV-012", "Conveyor drive inverter tripped", "Drive inverter reports overcurrent; the conveyor section is stopped.",
           line_stop=True, slot="CONVEYOR-01", resources=(("slot", "DRIVE-INVERTER"),), repair_min=(20, 40)),
    ),
    "Dispenser": (
        _m("ALM-DSP-112", "Dispense flow below limit", "Flow meter below the lower limit for 10 cycles; static mixer nozzle suspected.",
           slot="TOOL-HEAD-01", resources=(("consumable", "DISP-NOZZLE"),), repair_min=(10, 20), weight=2),
        _m("ALM-DSP-140", "Dosing valve does not close", "Valve feedback missing after the dosing shot; material drips.",
           slot="TOOL-HEAD-01", resources=(("slot", "VALVE-TERMINAL"), ("consumable", "PNEU-VALVE")), repair_min=(25, 45)),
    ),
    "FasteningStation": (
        _m("ALM-FST-208", "Torque outside window", "Final torque below the lower window on consecutive cycles.",
           slot="TOOL-HEAD-01", resources=(("slot", "TIGHTENING-SPINDLE"),), repair_min=(20, 40)),
        _m("ALM-FST-031", "Screw feed jam", "No screw present at the bit after two feed attempts.",
           priority="Medium", slot="TOOL-HEAD-01", resources=(("consumable", "FASTENER"),), repair_min=(5, 12), weight=2),
    ),
    "LaserWelder": (
        _m("ALM-LSR-217", "Laser power drift", "Laser power at the workpiece is falling while the optics heat up.",
           priority="Critical", line_stop=True, drift="laser_drift", slot="TOOL-HEAD-01",
           resources=(("consumable", "PROTECTIVE-GLASS"), ("slot", "LASER-OPTIC")), repair_min=(20, 45), weight=2),
        _m("ALM-LSR-230", "Seam tracking axis following error", "Following error of the seam tracking axis above the limit.",
           slot="GANTRY-X-01", resources=(("slot", "SERVO-DRIVE"), ("consumable", "SERVO-MOTOR")), repair_min=(30, 50)),
    ),
    "LaserSource": (
        _m("ALM-LSR-240", "Laser source chiller flow low", "Cooling water flow below the minimum; the source is interlocked.", repair_min=(15, 30)),
    ),
    "LaserMarker": (
        _m("ALM-LSR-260", "Marking laser not ready", "Marking laser did not report ready within the cycle.", priority="Medium", repair_min=(8, 20)),
    ),
    "EOLTester": (
        _m("ALM-NET-077", "Test bench communication timeout", "The test sequencer lost the fieldbus connection to the bench I/O.",
           slot="PANEL-STATUS-01", resources=(("slot", "NET-SWITCH"),), repair_min=(15, 30)),
        _m("ALM-EOL-052", "Dyno drive fault", "Load machine drive reports a fault during the run-up.",
           slot="GANTRY-X-01", resources=(("slot", "SERVO-DRIVE"),), repair_min=(30, 50)),
    ),
    "RobotCell": (
        _m("ALM-ROB-330", "Safety scanner protective field fault", "Protective field infringed without a person in the cell; scanner contaminated or defective.",
           slot="VISION-CAM-01", resources=(("slot", "SAFETY-SCANNER"),), repair_min=(20, 35)),
        _m("ALM-ROB-410", "Robot axis vibration", "Axis vibration is rising; gearbox or bearing wear suspected.",
           drift="bearing_wear", slot="GANTRY-X-01", resources=(("slot", "ROBOT-CTRL"), ("slot", "SERVO-DRIVE")), repair_min=(30, 55)),
    ),
    "VisionInspection": (
        _m("ALM-VIS-064", "Camera image acquisition timeout", "No image received within the trigger window.",
           slot="VISION-CAM-01", resources=(("slot", "VISION-CAM"),), repair_min=(15, 30)),
        _m("ALM-VIS-011", "Illumination intensity low", "Auto-exposure reached its limit; lighting degraded.", priority="Low", slot="VISION-CAM-01", repair_min=(10, 20), weight=2),
    ),
    "ServoPress": (
        _m("ALM-PRS-031", "Press force curve outside envelope", "Force-displacement curve left the envelope on consecutive parts.",
           slot="TOOL-HEAD-01", resources=(("slot", "PRESS-DRIVE"),), repair_min=(25, 45)),
        _m("ALM-PRS-090", "Press axis vibration", "Vibration of the press axis is rising.", drift="bearing_wear", slot="GANTRY-Z-01",
           resources=(("slot", "SERVO-DRIVE"), ("consumable", "SERVO-MOTOR")), repair_min=(25, 40)),
    ),
    "CncMachining": (
        _m("ALM-SPN-045", "Spindle vibration rising", "Spindle vibration envelope is rising towards the alarm limit; bearing wear suspected.",
           drift="bearing_wear", slot="TOOL-HEAD-01", resources=(("consumable", "SPINDLE-BEARING"),), repair_min=(45, 90)),
        _m("ALM-CNC-120", "Tool changer position fault", "Tool changer did not reach its end position.", priority="Medium",
           resources=(("consumable", "PROX-SENSOR"),), repair_min=(15, 30), weight=2),
    ),
}
DEFAULT_MODES: Tuple[FaultMode, ...] = (
    _m("ALM-GEN-010", "Station fault", "The station stopped with a general fault.", priority="Medium", repair_min=(10, 25)),
)
# Faults that were not planned by the desk (control API, drift scenarios, spontaneous faults).
REASON_TITLES = {
    "power below minimum": ("Laser power below minimum", "Critical", True),
    "temperature alarm": ("Process temperature above alarm limit", "High", False),
    "vibration alarm": ("Vibration above alarm limit", "High", False),
}
SLA_HOURS = {"Critical": 2, "High": 4, "Medium": 8, "Low": 24}


def modes_for(machine_type: Optional[str]) -> Tuple[FaultMode, ...]:
    return FAULT_MODES.get(machine_type or "", DEFAULT_MODES)


def resource_classes() -> Dict[str, set]:
    out: Dict[str, set] = {"slot": set(), "consumable": set()}
    for modes in FAULT_MODES.values():
        for m in modes:
            for kind, cls in m.resources:
                out[kind].add(cls)
    return out


@dataclass
class Action:
    """One thing that happens on a ticket / in the store. `at_s` is simulated time."""
    kind: str                  # raise | reserve | pickup | pending | restock | resume | consume | escalate | deescalate | resolve | close
    incident: str              # stable incident key, "" for store bookings
    station: str               # station id (custom identifier / name)
    at_s: float
    data: Dict[str, object] = field(default_factory=dict)

    def log_line(self) -> str:
        return f"{self.at_s:10.1f} {self.kind:10s} {self.station:16s} {self.incident:22s} {sorted(self.data.items())}"


@dataclass
class Incident:
    key: str
    station: str
    mode: FaultMode
    alarm_code: str
    priority: str
    stage: str = "open"        # open -> reserved -> in_progress -> (waiting_parts) -> repairing -> verifying
    due_s: float = 0.0
    technician: Optional[Dict[str, str]] = None
    resource: Optional[Tuple[str, str]] = None
    repair_s: float = 0.0
    escalate_at_s: Optional[float] = None
    escalated: bool = False


class MaintenanceDesk:
    def __init__(self, plant: PlantModel, profile: PlantProfile, seed: int = 0, incidents_per_station_hour: float = 0.05):
        self.plant = plant
        self.profile = profile
        self.rate = max(0.0, float(incidents_per_station_hour))
        self.rng = random.Random(stable_seed(seed, "maintenance-desk"))
        self._fault_rng = {k: random.Random(stable_seed(seed, "faults:" + k)) for k in plant.stations}
        self.now_s = 0.0
        self._next_fault_s: Dict[str, float] = {k: self._draw_gap(k) for k in plant.stations}
        self._planned: Dict[str, FaultMode] = {}
        self._planned_at: Dict[str, float] = {}
        self._seq: Dict[str, int] = {}
        self.incidents: Dict[str, Incident] = {}            # by station key
        self._busy: Dict[str, str] = {}                     # technician id -> station key
        self.stock_serialized = dict(profile.serialized)
        self.stock_consumables = dict(profile.consumables)
        self._restocks: List[Tuple[float, str, int]] = []   # (due, consumable class, quantity)
        self._history: Dict[str, Deque[Tuple[float, Dict[str, float]]]] = {k: deque(maxlen=12) for k in plant.stations}
        self.log: List[Action] = []
        self.counts: Dict[str, int] = {}

    # ------------------------------------------------------------------ helpers
    def _draw_gap(self, key: str) -> float:
        if self.rate <= 0:
            return float("inf")
        return self._fault_rng[key].expovariate(self.rate / 3600.0)

    def set_rate(self, incidents_per_station_hour: float) -> float:
        self.rate = max(0.0, float(incidents_per_station_hour))
        for k in self._next_fault_s:
            self._next_fault_s[k] = self.now_s + self._draw_gap(k)
        return self.rate

    def _emit(self, out: List[Action], kind: str, inc: Optional[Incident], station: str, **data) -> None:
        action = Action(kind=kind, incident=inc.key if inc else "", station=station, at_s=round(self.now_s, 1), data=data)
        out.append(action)
        self.log.append(action)
        self.counts[kind] = self.counts.get(kind, 0) + 1

    def _crew(self, st: StationProcess, alarm_code: str) -> List[Dict[str, str]]:
        code = alarm_code.upper()
        if any(t in code for t in ("PLC", "NET", "COMM")) and self.profile.crews.get("Controls"):
            return self.profile.crews["Controls"]
        if "ELEC" in code and self.profile.crews.get("Electrical"):
            return self.profile.crews["Electrical"]
        return self.profile.crew_for(st.spec.technology)

    def _pick_resource(self, mode: FaultMode) -> Optional[Tuple[str, str]]:
        for kind, cls in mode.resources:
            if cls in (self.stock_serialized if kind == "slot" else self.stock_consumables):
                return kind, cls
        return None

    def _probes(self, key: str, alarm_code: str) -> List[Dict[str, object]]:
        metrics = ("vibration_mms",) if any(t in alarm_code for t in ("VIB", "SPN", "ROB-410", "PRS-090")) else ("power_kw", "temperature_c")
        units = {"vibration_mms": "mm/s", "power_kw": "kW", "temperature_c": "C"}
        out = []
        for metric in metrics:
            points = [(round(self.now_s - t, 1), v[metric]) for t, v in self._history[key] if metric in v]
            if points:
                out.append({"metric": metric, "unit": units[metric], "value": points[-1][1],
                            "buffer": [{"age_s": age, "value": val} for age, val in points]})
        return out

    # ------------------------------------------------------------------ simulation
    def step(self, dt_s: float) -> List[Action]:
        """Advances the desk by `dt_s` simulated seconds (call right after PlantModel.step)."""
        out: List[Action] = []
        self.now_s += dt_s
        for key, st in self.plant.stations.items():
            sample = st.sample()
            self._history[key].append((self.now_s, {m: float(sample[m]) for m in ("power_kw", "temperature_c", "vibration_mms")}))

        # 1. goods receipts booked by the store
        for due, cls, qty in [r for r in self._restocks if r[0] <= self.now_s]:
            self._restocks.remove((due, cls, qty))
            self.stock_consumables[cls] = self.stock_consumables.get(cls, 0) + qty
            self._emit(out, "restock", None, "", consumable_class=cls, quantity=qty)

        for key, st in self.plant.stations.items():
            inc = self.incidents.get(key)
            # 2. new failures (not on sub-machines whose cell is down, not while one is being handled)
            if inc is None and key not in self._planned and st.producing and self.now_s >= self._next_fault_s[key]:
                rng = self._fault_rng[key]
                modes = modes_for(st.spec.machine_type)
                mode = rng.choices(modes, weights=[m.weight for m in modes])[0]
                self._planned[key] = mode
                self._planned_at[key] = self.now_s
                if mode.drift:
                    st.start_drift(mode.drift, duration_s=rng.uniform(420.0, 780.0))
                else:
                    st.inject_fault(reason=mode.title, alarm_code=mode.code)
            elif inc is None and key in self._planned and st.state != FAULTED and self.now_s - self._planned_at[key] > 2400.0:
                # the drift never reached its alarm limit (or somebody recovered the station): forget it
                self._planned.pop(key)
                st.drift = None
                self._next_fault_s[key] = self.now_s + self._draw_gap(key)
            # 3. a faulted station without a ticket raises one
            if inc is None and st.state == FAULTED:
                inc = self._open(out, key, st)
            if inc is not None:
                self._advance(out, key, st, inc)
        return out

    def _open(self, out: List[Action], key: str, st: StationProcess) -> Incident:
        mode = self._planned.pop(key, None)
        alarm = mode.code if mode else (st.alarm_code or "ALM-GEN-001")
        st.alarm_code = alarm                       # the station reports the alarm its ticket carries
        if mode is None:
            title, priority, line_stop = REASON_TITLES.get(st.state_reason, (st.state_reason.capitalize(), "High", False))
            mode = next((m for m in modes_for(st.spec.machine_type) if m.code == alarm), None) or \
                FaultMode(code=alarm, title=title, description=f"The station stopped: {st.state_reason}.", priority=priority, line_stop=line_stop)
        self._seq[key] = self._seq.get(key, 0) + 1
        inc = Incident(key=f"{st.spec.station_id}#{self._seq[key]}", station=key, mode=mode, alarm_code=alarm, priority=mode.priority)
        react = self.rng.uniform(60.0, 240.0) if mode.priority == "Critical" else self.rng.uniform(120.0, 480.0)
        inc.due_s = self.now_s + react
        inc.resource = self._pick_resource(mode)
        inc.repair_s = self.rng.uniform(*mode.repair_min) * 60.0
        self.incidents[key] = inc
        self._emit(out, "raise", inc, st.spec.station_id, alarm_code=alarm, title=f"{mode.title} - {st.spec.station_id}",
                   description=mode.description, priority=mode.priority, is_line_stop=mode.line_stop, slot=mode.slot,
                   controller=st.spec.controller_machine_identifier, probes=self._probes(key, alarm))
        return inc

    def _advance(self, out: List[Action], key: str, st: StationProcess, inc: Incident) -> None:
        sid = st.spec.station_id
        if inc.escalate_at_s is not None and not inc.escalated and self.now_s >= inc.escalate_at_s and inc.stage in ("repairing", "waiting_parts"):
            inc.escalated = True
            by = (self.profile.escalation or [{"id": "shift-leader", "name": "Shift Leader"}])[0]
            self._emit(out, "escalate", inc, sid, reason="Repair exceeds 30 minutes", by=by["name"], target="DedicatedEngineer")
        if self.now_s < inc.due_s:
            return

        if inc.stage == "open":
            if st.state != FAULTED and st.scenario != "fault":
                # the condition cleared by itself before anybody arrived
                self._emit(out, "close", inc, sid, cleared_reason="Alarm condition cleared before pickup", transient=True)
                self._finish(key, inc)
                return
            crew = self._crew(st, inc.alarm_code)
            free = next((p for p in crew if p["id"] not in self._busy), None)
            if free is None:
                inc.due_s = self.now_s + 120.0          # everybody is on another job
                return
            inc.technician = free
            self._busy[free["id"]] = key
            inc.stage, inc.due_s = "reserved", self.now_s + self.rng.uniform(60.0, 240.0)
            self._emit(out, "reserve", inc, sid, technician=free["name"], technician_id=free["id"])

        elif inc.stage == "reserved":
            st.start_maintenance()
            inc.stage, inc.due_s = "in_progress", self.now_s + self.rng.uniform(180.0, 360.0)
            self._emit(out, "pickup", inc, sid, technician=inc.technician["name"])

        elif inc.stage == "in_progress":       # diagnosis done: is the part there?
            inc.escalate_at_s = self.now_s + 1800.0 if inc.repair_s > 1800.0 else None
            if inc.resource and self._stock(inc.resource) <= 0:
                kind, cls = inc.resource
                lead = self.rng.uniform(1200.0, 2400.0)
                inc.stage, inc.due_s = "waiting_parts", self.now_s + lead
                self._emit(out, "pending", inc, sid, details=f"Waiting for spare part {cls}; none in the store. Expected in {int(lead // 60)} min.")
            else:
                self._start_repair(out, st, inc)

        elif inc.stage == "waiting_parts":
            kind, cls = inc.resource
            if kind == "consumable":
                self.stock_consumables[cls] = self.stock_consumables.get(cls, 0) + 5
                self._emit(out, "restock", None, "", consumable_class=cls, quantity=5)
                self._emit(out, "resume", inc, sid)
                self._start_repair(out, st, inc)
            else:                                # no goods receipt for serialized parts: repair in place
                inc.resource = None
                self._emit(out, "resume", inc, sid, note=f"No spare {cls} available: installed part repaired in place")
                self._start_repair(out, st, inc)

        elif inc.stage == "repairing":
            st.recover()
            if inc.escalated:
                self._emit(out, "deescalate", inc, sid, by=inc.technician["name"])
            self._emit(out, "resolve", inc, sid, technician=inc.technician["name"])
            self._busy.pop(inc.technician["id"], None)
            inc.stage, inc.due_s = "verifying", self.now_s + self.rng.uniform(300.0, 600.0)

        elif inc.stage == "verifying":
            # (a station that faulted again in the meantime opens a new incident on the next step)
            self._emit(out, "close", inc, sid, cleared_reason="Station producing again; alarm cleared", transient=False)
            self._finish(key, inc)

    def _stock(self, resource: Tuple[str, str]) -> int:
        kind, cls = resource
        return (self.stock_serialized if kind == "slot" else self.stock_consumables).get(cls, 0)

    def _start_repair(self, out: List[Action], st: StationProcess, inc: Incident) -> None:
        if inc.resource:
            kind, cls = inc.resource
            store = self.stock_serialized if kind == "slot" else self.stock_consumables
            store[cls] = store.get(cls, 0) - 1
            slot = next((c["slot"] for c in st.spec.components if c.get("partClass") == cls), None) if kind == "slot" else None
            if kind == "slot" and slot is None:
                store[cls] += 1                 # this station has no slot for the part: nothing is swapped
            else:
                self._emit(out, "consume", inc, st.spec.station_id, resource=kind, part_class=cls, slot=slot,
                           technician=inc.technician["name"], technician_id=inc.technician["id"])
                minimum = self.profile.consumable_minimum.get(cls, 0)
                if kind == "consumable" and store[cls] < minimum and not any(r[1] == cls for r in self._restocks):
                    self._restocks.append((self.now_s + self.rng.uniform(1800.0, 3600.0), cls, max(5, minimum)))
        inc.stage, inc.due_s = "repairing", self.now_s + inc.repair_s

    def _finish(self, key: str, inc: Incident) -> None:
        if inc.technician:
            self._busy.pop(inc.technician["id"], None)
        self.incidents.pop(key, None)
        self._next_fault_s[key] = self.now_s + self._draw_gap(key)

    # ------------------------------------------------------------------ status
    def describe(self) -> Dict[str, object]:
        return {
            "incidents_per_station_hour": self.rate,
            "open_incidents": [{"incident": i.key, "alarm": i.alarm_code, "stage": i.stage, "priority": i.priority,
                                "technician": i.technician["name"] if i.technician else None} for i in self.incidents.values()],
            "actions": dict(sorted(self.counts.items())),
            "stock": {"serialized": dict(sorted(self.stock_serialized.items())), "consumables": dict(sorted(self.stock_consumables.items()))},
        }
