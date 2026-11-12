#!/usr/bin/env python3
"""
Heimdall Industrial Edge Fleet Simulator (dev / demo only)

Impersonates the plant's edge IPCs towards the backend exactly like the real agent does:
binary protobuf over MQTT 5 on the backend's embedded broker.

  heimdall/devices/{machineIdentifier}/system_info   IPC heartbeat (SystemInfoRequest)
  heimdall/devices/{machineIdentifier}/plc_memory    station process data (AdsPlcMemoryBlock)

The station blocks drive the live station telemetry path (station KPIs, plant-map HUD, SignalR
StationOscillationReceived / StationTelemetryReceived, Lua TelemetryChange scripts).

Scenarios (faults, drifts, line stops, speed) are controlled at runtime through a small HTTP API
(default http://127.0.0.1:5055, see README section in docs/guide/DEV_GUIDE.md):

  GET  /api/status            fleet, transport and station overview
  GET  /api/stations          stations only
  POST /api/fault             {hostname?} | {station?, duration_s?}
  POST /api/station/fault     {station, duration_s?}
  POST /api/station/drift     {station, profile, duration_s?, magnitude?}
  POST /api/station/maintenance {station, duration_s?}
  POST /api/line/stop         {line, duration_s?}
  POST /api/recover           {station?} | {line?} | {}   (empty = everything)
  POST /api/clear-faults      {}
  POST /api/rate              {fault_rate}
  POST /api/speed             {speed}
  POST /api/scenario          {name}
  POST /api/activity          {incident_rate}   failures per station and simulated hour

With --activity (default for continuous runs) a maintenance desk turns station faults into work through
the backend REST API: machine-raised tickets, reservation, pickup, spare-part consumption, escalation,
resolution and closure (maintenance.py, backend_client.py).
"""

import argparse
import concurrent.futures
import csv
import datetime
import json
import math
import os
import random
import sys
import threading
import time
import uuid
from dataclasses import dataclass, field
from enum import Enum
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Dict, List, Optional

# Add proto and fleet directory to path
fleet_dir = os.path.dirname(os.path.abspath(__file__))
if fleet_dir not in sys.path:
    sys.path.insert(0, fleet_dir)
proto_dir = os.path.join(fleet_dir, 'proto')
if proto_dir not in sys.path:
    sys.path.insert(0, proto_dir)

from google.protobuf.timestamp_pb2 import Timestamp

import system_info_pb2
import topology
from mqtt_transport import (MqttPublisher, PLC_MEMORY_CHANNEL, SYSTEM_INFO_CHANNEL,
                            broker_from_env, parse_broker)
from backend_client import dispatcher_from_env
from maintenance import MaintenanceDesk
from plc_encoding import build_station_block
from station_model import DRIFT_PROFILES, FAULTED, MAINTENANCE, SCENARIOS, PlantModel, StationSpec, stable_seed


class DeviceProfile(Enum):
    TWINCAT_IPC = "Beckhoff TwinCAT 3 IPC"
    SIMATIC_IPC = "Siemens SIMATIC IPC477E"
    LINUX_EDGE = "Dell OptiPlex Edge Terminal"
    ROBOT_CELL = "Fanuc Robot Controller"
    VISION_SENSOR = "Cognex In-Sight 9000"


def _profile_from_label(label: str) -> DeviceProfile:
    for p in DeviceProfile:
        if p.value == label:
            return p
    return DeviceProfile.TWINCAT_IPC


@dataclass
class IndustrialDeviceNode:
    hostname: str
    machine_identifier: str
    mac_address: str
    ip_address: str
    profile: DeviceProfile
    os_name: str
    base_cpu: float = 15.0
    base_ram: float = 35.0
    tick_count: int = 0
    crc_errors: int = 0
    is_faulty: bool = False
    extra_metadata: Optional[dict] = None
    rng: random.Random = field(default_factory=random.Random, repr=False)
    stations: list = field(default_factory=list, repr=False)   # StationProcess objects this IPC reports

    def generate_telemetry(self, fault_rate: float = 0.0):
        self.tick_count += 1
        now = datetime.datetime.now(datetime.timezone.utc)
        ts = Timestamp()
        ts.FromDatetime(now)

        # Load follows the stations this IPC runs: real-time tasks of producing stations dominate.
        rng = self.rng
        states = [st.state for st in self.stations]
        producing = sum(1 for s in states if s not in (FAULTED, MAINTENANCE) and s != "IDLE")
        faulted = sum(1 for s in states if s == FAULTED)
        cpu_load = max(4.0, min(95.0, self.base_cpu + 6.0 * producing + 3.0 * math.sin(self.tick_count * 0.1) + rng.uniform(-2.0, 2.0)))
        ram_load = max(20.0, min(90.0, self.base_ram + 1.5 * len(states) + rng.uniform(-1.0, 1.0)))

        # IPC anomaly: injected through the control API, or at random with --fault-rate (default 0)
        inject_fault = self.is_faulty or (fault_rate > 0 and rng.random() < fault_rate)
        if inject_fault:
            cpu_load = min(100.0, cpu_load + 40.0)
            self.crc_errors += rng.randint(1, 5)

        components = [
            system_info_pb2.InventoryComponent(
                name="OS Environment",
                technology="Heimdall Edge Probe",
                type="software",
                data_json=json.dumps({
                    **(self.extra_metadata or {}),
                    "OsVersion": self.os_name,
                    "IPAddress": self.ip_address,
                    "DeviceProfile": self.profile.value,
                    "TwinCAT_ADS_State": "Run" if not inject_fault else "Exception",
                    "EtherCAT_DevState": "OP" if self.crc_errors < 10 else "INIT_ERR",
                    "EtherCAT_CRCErrors": self.crc_errors
                })
            ),
            system_info_pb2.InventoryComponent(
                name="Real-Time Metrics",
                technology="Edge Telemetry",
                type="telemetry",
                data_json=json.dumps({
                    "CpuLoad": f"{cpu_load:.1f}%",
                    "RamUsage": f"{ram_load:.1f}%",
                    "CycleJitterMicroseconds": f"{rng.randint(2, 18) + (40 if inject_fault else 0)} us",
                    "ControlledStations": len(states),
                    "StationsProducing": producing,
                    "StationsFaulted": faulted,
                    "Status": "Degraded" if (inject_fault or faulted) else "Online"
                })
            )
        ]

        return system_info_pb2.SystemInfoRequest(
            hostname=self.hostname,
            machine_identifier=self.machine_identifier,
            mac_address=self.mac_address,
            last_online=ts,
            components=components,
            disk_info=system_info_pb2.DiskInfo(
                total_free_gb=120.0,
                os_drive_free_gb=45.0,
                drives={"C:": 45.0, "D:": 75.0} if "Windows" in self.os_name else {"/": 45.0, "/var": 75.0}
            )
        )


class SimulatorHttpHandler(BaseHTTPRequestHandler):
    simulator: "IndustrialFleetSimulator" = None
    cors_origin: Optional[str] = None

    def log_message(self, format, *args):
        pass

    # ------------------------------------------------------------------ helpers
    def _send(self, code: int, data: dict):
        body = json.dumps(data).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        if self.cors_origin:
            self.send_header("Access-Control-Allow-Origin", self.cors_origin)
        self.end_headers()
        self.wfile.write(body)

    def _ok(self, message: str, **extra):
        self._send(200, {"success": True, "message": message, **extra})

    def _fail(self, code: int, message: str):
        self._send(code, {"success": False, "message": message})

    def _payload(self) -> dict:
        try:
            length = min(int(self.headers.get("Content-Length", 0) or 0), 64 * 1024)
        except ValueError:
            length = 0
        if length <= 0:
            return {}
        try:
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            return payload if isinstance(payload, dict) else {}
        except Exception:
            return {}

    @staticmethod
    def _duration(payload: dict) -> Optional[float]:
        v = payload.get("duration_s")
        try:
            return float(v) if v not in (None, "", 0) and float(v) > 0 else None
        except (TypeError, ValueError):
            return None

    # ------------------------------------------------------------------ routes
    def do_GET(self):
        path = self.path.split("?", 1)[0].rstrip("/")
        if path in ("", "/api/status", "/metrics"):
            self._send(200, self.simulator.status())
        elif path == "/api/stations":
            self._send(200, {"stations": self.simulator.station_status()})
        else:
            self._fail(404, "unknown endpoint")

    def do_POST(self):
        sim = self.simulator
        path = self.path.split("?", 1)[0].rstrip("/")
        payload = self._payload()
        plant = sim.plant

        try:
            if path == "/api/fault":
                station = plant.get(payload.get("station"))
                if station:
                    with sim.lock:
                        station.inject_fault(self._duration(payload))
                    sim.publish_station_now(station)
                    return self._ok(f"Fault injected into station {station.spec.station_id}", target=station.spec.station_id)
                matched = sim.inject_node_fault(payload.get("hostname"))
                return self._send(200, {
                    "success": True,
                    "target": matched.hostname if matched else None,
                    "message": f"Fault injected into {matched.hostname if matched else 'none'}"
                })

            if path == "/api/clear-faults":
                for n in sim.nodes:
                    n.is_faulty = False
                    n.crc_errors = 0
                with sim.lock:
                    plant.recover()
                return self._ok("All faults cleared")

            if path == "/api/rate":
                new_rate = payload.get("fault_rate")
                if new_rate is not None:
                    sim.fault_rate = max(0.0, min(1.0, float(new_rate)))
                return self._send(200, {"success": True, "fault_rate": sim.fault_rate})

            if path == "/api/speed":
                with sim.lock:
                    speed = plant.set_speed(float(payload.get("speed", 1.0)))
                return self._ok(f"Simulation speed set to {speed:g}x", speed=speed)

            if path == "/api/activity":
                with sim.lock:
                    rate = sim.desk.set_rate(float(payload.get("incident_rate", sim.desk.rate)))
                return self._ok(f"Failure rate set to {rate:g} per station and simulated hour"
                                + ("" if sim.dispatcher else " (activity disabled: start with --activity)"),
                                incident_rate=rate, enabled=sim.dispatcher is not None)

            if path == "/api/scenario":
                with sim.lock:
                    applied = plant.apply_scenario(str(payload.get("name") or ""))
                return self._ok("; ".join(applied) if applied else "Scenario had no matching stations", applied=applied)

            if path == "/api/recover":
                with sim.lock:
                    targets = plant.recover(station=payload.get("station"), line=payload.get("line"))
                if not targets and (payload.get("station") or payload.get("line")):
                    return self._fail(404, "No matching station or line")
                if not payload.get("station") and not payload.get("line"):
                    for n in sim.nodes:
                        n.is_faulty = False
                        n.crc_errors = 0
                return self._ok(f"Recovered {len(targets)} station(s)", targets=targets)

            if path == "/api/line/stop":
                with sim.lock:
                    targets = plant.line_stations(payload.get("line"))
                    for st in targets:
                        st.line_stop(self._duration(payload))
                if not targets:
                    return self._fail(404, f"Unknown line '{payload.get('line')}'")
                return self._ok(f"Line {payload.get('line')} stopped ({len(targets)} stations)",
                                targets=[st.spec.station_id for st in targets])

            if path in ("/api/station/fault", "/api/station/drift", "/api/station/maintenance"):
                station = plant.get(payload.get("station"))
                if not station:
                    return self._fail(404, f"Unknown station '{payload.get('station')}'")
                with sim.lock:
                    if path.endswith("/fault"):
                        station.inject_fault(self._duration(payload))
                        msg = f"Fault injected into station {station.spec.station_id}"
                    elif path.endswith("/maintenance"):
                        station.start_maintenance(self._duration(payload))
                        msg = f"Station {station.spec.station_id} set to maintenance"
                    else:
                        profile = str(payload.get("profile") or "")
                        station.start_drift(profile, duration_s=self._duration(payload) or 600.0,
                                            magnitude=float(payload.get("magnitude") or 1.0))
                        msg = f"Drift '{profile}' started on station {station.spec.station_id}"
                sim.publish_station_now(station)
                return self._ok(msg, target=station.spec.station_id)

            return self._fail(404, "unknown endpoint")
        except (ValueError, TypeError) as e:
            return self._fail(400, str(e))

    def do_OPTIONS(self):
        self.send_response(204)
        if self.cors_origin:
            self.send_header("Access-Control-Allow-Origin", self.cors_origin)
            self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()


def start_control_server(simulator, port: int = 5055, host: Optional[str] = None):
    """
    The control API has no authentication, so it listens on loopback only unless HTTP_HOST is set
    (the docker compose service sets 0.0.0.0 inside its private network). Browsers reach it through
    the Nuxt BFF proxy (/api/simulator/*), so no CORS headers are sent unless SIM_CORS_ORIGIN is set.
    """
    host = host or os.environ.get("HTTP_HOST") or "127.0.0.1"
    SimulatorHttpHandler.simulator = simulator
    SimulatorHttpHandler.cors_origin = os.environ.get("SIM_CORS_ORIGIN") or None
    server = ThreadingHTTPServer((host, port), SimulatorHttpHandler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    print(f"  Simulator control API listening on http://{host}:{server.server_address[1]} (GET /api/status)")
    return server


class IndustrialFleetSimulator:
    def __init__(self, broker: Optional[str] = None, csv_path: str = None, fault_rate: float = 0.0,
                 client: str = None, dataset: str = "enterprise", seed: Optional[int] = None,
                 station_interval: float = 2.0, heartbeat_interval: float = 10.0,
                 stations_enabled: bool = True, speed: float = 1.0, station_fault_rate: float = 0.0,
                 activity: bool = False, incident_rate: float = 0.05, backend_url: Optional[str] = None,
                 api_token: Optional[str] = None):
        self.broker_host, self.broker_port = parse_broker(broker) if broker else broker_from_env()
        self.fault_rate = fault_rate
        self.client = client
        self.dataset = dataset
        self.seed = seed if seed is not None else random.SystemRandom().randrange(1, 2**31)
        self.station_interval = max(0.2, station_interval)
        self.heartbeat_interval = max(1.0, heartbeat_interval)
        self.lock = threading.Lock()
        candidate_paths = [
            os.environ.get("CSV_PATH"),
            csv_path,
            os.path.join(os.path.dirname(__file__), '../../seed_data/inventory_seed.csv'),
            '/workspace/seed_data/inventory_seed.csv',
            'seed_data/inventory_seed.csv'
        ]
        self.csv_path = next((p for p in candidate_paths if p and os.path.exists(p)), None)

        self.nodes, station_specs = self.load_nodes()
        if self.client:
            matched = [n for n in self.nodes if n.hostname.upper() == self.client.upper()]
            if matched:
                self.nodes = matched
            else:
                profile = DeviceProfile.ROBOT_CELL if "ROBOT" in self.client.upper() else \
                          DeviceProfile.SIMATIC_IPC if "ASSEMBLY" in self.client.upper() else \
                          DeviceProfile.TWINCAT_IPC
                client_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, self.client))
                mac = f"02:{client_id[0:2]}:{client_id[2:4]}:{client_id[4:6]}:{client_id[6:8]}:{client_id[9:11]}".upper()
                self.nodes = [
                    IndustrialDeviceNode(
                        hostname=self.client,
                        machine_identifier=f"HW-{self.client}",
                        mac_address=mac,
                        ip_address="192.168.100.50",
                        profile=profile,
                        os_name="Windows 10 IoT"
                    )
                ]
            # Only stations whose reporting controller is still simulated.
            ids = {n.machine_identifier for n in self.nodes}
            station_specs = [s for s in station_specs if s.controller_machine_identifier in ids]
        for n in self.nodes:
            n.rng = random.Random(stable_seed(self.seed, n.hostname))

        self._ip_by_identifier: Dict[str, str] = {n.machine_identifier: n.ip_address for n in self.nodes}
        self.plant = PlantModel(station_specs if stations_enabled else [], seed=self.seed,
                                random_fault_rate=station_fault_rate)
        self.plant.set_speed(speed)
        for n in self.nodes:
            n.stations = [st for st in self.plant.stations.values() if st.spec.controller_machine_identifier == n.machine_identifier]

        try:
            profile = topology.load_profile(self.dataset)
        except Exception as e:
            print(f"  No crew / store profile for dataset '{self.dataset}' ({e}); tickets stay unassigned by the simulator.")
            profile = topology.PlantProfile()
        self.desk = MaintenanceDesk(self.plant, profile, seed=self.seed, incidents_per_station_hour=incident_rate)
        self.dispatcher = dispatcher_from_env(lambda: self.plant.speed, base_url=backend_url, token=api_token) if activity else None

        self.mqtt: Optional[MqttPublisher] = None
        self.total_dispatched = 0
        self.total_errors = 0
        self.station_blocks_dispatched = 0
        self.started_at = time.time()

    # ------------------------------------------------------------------ dataset
    def load_nodes(self):
        nodes: List[IndustrialDeviceNode] = []
        stations: List[StationSpec] = []
        try:
            ctrl, stations = topology.load(self.dataset)
            for c in ctrl:
                nodes.append(IndustrialDeviceNode(
                    hostname=c.hostname,
                    machine_identifier=c.machine_identifier,
                    mac_address=c.mac_address,
                    ip_address=c.ip_address,
                    profile=_profile_from_label(c.profile),
                    os_name=c.os_name,
                    extra_metadata=c.metadata
                ))
            if nodes:
                return nodes, stations
        except Exception as e:
            print(f"  Dataset '{self.dataset}' could not be loaded ({e}); falling back to CSV / canonical fleet.")
            stations = []

        if self.csv_path and os.path.exists(self.csv_path):
            with open(self.csv_path, mode='r', encoding='utf-8') as f:
                reader = csv.DictReader(f)
                for row in reader:
                    if row['Type'] == 'ClientPc':
                        item_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name']))
                        meta = json.loads(row['Metadata']) if row['Metadata'] else {}
                        mac = meta.get("MACAddress") or \
                            f"02:{item_id[0:2]}:{item_id[2:4]}:{item_id[4:6]}:{item_id[6:8]}:{item_id[9:11]}".upper()
                        os_name = meta.get("OperatingSystem", "Windows 10 IoT")
                        hostname = row['ClientPcHostname'] or row['Name']

                        profile = DeviceProfile.TWINCAT_IPC if "Beckhoff" in row['Manufacturer'] else \
                                  DeviceProfile.SIMATIC_IPC if "Siemens" in row['Manufacturer'] else \
                                  DeviceProfile.LINUX_EDGE

                        nodes.append(IndustrialDeviceNode(
                            hostname=hostname,
                            machine_identifier=meta.get("MachineIdentifier") or f"HW-{hostname}",
                            mac_address=mac,
                            ip_address=meta.get("IPAddress", "10.0.1.10"),
                            profile=profile,
                            os_name=os_name
                        ))
        if not nodes:
            # Canonical 56 plant IPCs across 8 lines (7 per line)
            for l_num in range(1, 9):
                vlan = 100 + l_num
                line_specs = [
                    (f"IPC-L{l_num:02d}-OP030-DEDICATED", DeviceProfile.TWINCAT_IPC, "Windows 10 IoT Enterprise 2021 LTSC"),
                    (f"IPC-L{l_num:02d}-CONVEYOR-MAIN", DeviceProfile.SIMATIC_IPC, "Windows 11 IoT Enterprise LTSC 2024"),
                    (f"IPC-L{l_num:02d}-ROB-ALPHA", DeviceProfile.ROBOT_CELL, "Windows 10 IoT Enterprise"),
                    (f"IPC-L{l_num:02d}-ROB-BETA", DeviceProfile.ROBOT_CELL, "Windows 10 IoT Enterprise"),
                    (f"IPC-L{l_num:02d}-OP060-PLC", DeviceProfile.TWINCAT_IPC, "Windows 11 IoT Enterprise"),
                    (f"IPC-L{l_num:02d}-OP060-VISION", DeviceProfile.VISION_SENSOR, "Windows 10 IoT Enterprise"),
                    (f"IPC-L{l_num:02d}-OP060-MES-GATE", DeviceProfile.LINUX_EDGE, "Debian 12 Bookworm Industrial")
                ]
                for idx, (hn, prof, os_n) in enumerate(line_specs, start=1):
                    nodes.append(IndustrialDeviceNode(
                        hostname=hn,
                        machine_identifier=f"HW-{hn}",
                        mac_address=f"02:65:54:{l_num:02X}:{vlan:02X}:{idx:02X}",
                        ip_address=f"192.168.{vlan}.{10 + idx}",
                        profile=prof,
                        os_name=os_n
                    ))
        return nodes, stations

    # ------------------------------------------------------------------ transport
    def connect(self, timeout: float = 5.0) -> bool:
        if self.mqtt is None:
            self.mqtt = MqttPublisher(self.broker_host, self.broker_port)
            self.mqtt.start()
        return self.mqtt.wait_connected(timeout)

    def send_node_heartbeat(self, node: IndustrialDeviceNode, wait: float = 0.0) -> bool:
        try:
            req = node.generate_telemetry(self.fault_rate)
            ok = bool(self.mqtt and self.mqtt.publish(node.machine_identifier, SYSTEM_INFO_CHANNEL,
                                                       req.SerializeToString(), qos=1, wait=wait))
        except Exception:
            ok = False
        if ok:
            self.total_dispatched += 1
        else:
            self.total_errors += 1
        return ok

    def send_station_block(self, station, wait: float = 0.0) -> bool:
        try:
            spec = station.spec
            block = build_station_block(spec.station_id, station.sample(),
                                        ip_address=self._ip_by_identifier.get(spec.controller_machine_identifier))
            ok = bool(self.mqtt and self.mqtt.publish(spec.controller_machine_identifier, PLC_MEMORY_CHANNEL,
                                                       block.SerializeToString(), qos=1, wait=wait))
        except Exception:
            ok = False
        if ok:
            self.total_dispatched += 1
            self.station_blocks_dispatched += 1
        else:
            self.total_errors += 1
        return ok

    def publish_station_now(self, station) -> None:
        """Pushes a scenario change immediately instead of waiting for the next station tick."""
        with self.lock:
            self.send_station_block(station)

    def inject_node_fault(self, hostname: Optional[str]) -> Optional[IndustrialDeviceNode]:
        matched = None
        if hostname:
            matched = next((n for n in self.nodes if n.hostname.upper() == str(hostname).upper()), None)
        if not matched and self.nodes:
            matched = random.choice(self.nodes)
        if matched:
            matched.is_faulty = True
            matched.crc_errors += 25
            self.send_node_heartbeat(matched)
        return matched

    # ------------------------------------------------------------------ status
    def station_status(self) -> List[dict]:
        with self.lock:
            return [st.describe() for st in self.plant.stations.values()]

    def status(self) -> dict:
        mqtt_connected = bool(self.mqtt and self.mqtt.connected.is_set())
        stations = self.station_status()
        return {
            "active_fleet": len(self.nodes),
            "total_dispatched": self.total_dispatched,
            "total_errors": self.total_errors,
            "fault_rate": self.fault_rate,
            "dataset": self.dataset,
            "seed": self.seed,
            "speed": self.plant.speed,
            "uptime_s": round(time.time() - self.started_at, 1),
            "transport": {
                "protocol": "mqtt5",
                "broker": f"{self.broker_host}:{self.broker_port}",
                "connected": mqtt_connected,
                "error": None if mqtt_connected else (self.mqtt.last_error if self.mqtt else "not started"),
            },
            "station_interval_s": self.station_interval,
            "station_blocks_dispatched": self.station_blocks_dispatched,
            "active_stations": len(stations),
            "lines": self.plant.lines(),
            "drift_profiles": sorted(DRIFT_PROFILES),
            "scenarios": list(SCENARIOS),
            "nodes": [
                {
                    "hostname": n.hostname,
                    "profile": n.profile.value,
                    "ip": n.ip_address,
                    "faulty": n.is_faulty,
                    "crc_errors": n.crc_errors,
                    "ticks": n.tick_count
                }
                for n in self.nodes
            ],
            "stations": stations,
            "maintenance": self._maintenance_status(),
        }

    def _maintenance_status(self) -> dict:
        with self.lock:
            desk = self.desk.describe()
        return {"enabled": self.dispatcher is not None, **desk,
                "backend_api": self.dispatcher.describe() if self.dispatcher else None}

    # ------------------------------------------------------------------ run modes
    def run_smoke_test(self, count: int = 10):
        print(f"Running simulator smoke test against MQTT broker {self.broker_host}:{self.broker_port} (Count: {count})...")
        if not self.connect(timeout=5.0):
            reason = self.mqtt.last_error if self.mqtt and self.mqtt.last_error else "no broker reachable"
            print(f"Smoke Test Result: could not connect to the MQTT broker ({reason}). Is the backend running?")
            return False
        sample_nodes = self.nodes[:count]
        ok_nodes = sum(1 for node in sample_nodes if self.send_node_heartbeat(node, wait=5.0))
        sample_stations = list(self.plant.stations.values())[:count]
        self.plant.step(self.station_interval)
        ok_stations = sum(1 for st in sample_stations if self.send_station_block(st, wait=5.0))
        self.mqtt.stop()
        print(f"Smoke Test Result: {ok_nodes}/{len(sample_nodes)} heartbeats and "
              f"{ok_stations}/{len(sample_stations)} station blocks acknowledged by the broker.")
        print("Note: a broker acknowledgement (QoS 1) confirms delivery to the broker, not that the backend ingested the message.")
        return ok_nodes == len(sample_nodes) and ok_stations == len(sample_stations)

    def _station_loop(self):
        """Fixed-step station simulation: every tick advances station_interval * speed simulated seconds."""
        next_tick = time.monotonic()
        while True:
            next_tick += self.station_interval
            with self.lock:
                self.plant.step(self.station_interval)
                if self.dispatcher is not None:
                    self.dispatcher.submit(self.desk.step(self.station_interval * self.plant.speed))
                if self.mqtt and self.mqtt.connected.is_set():
                    for st in self.plant.stations.values():
                        self.send_station_block(st)
            delay = next_tick - time.monotonic()
            if delay > 0:
                time.sleep(delay)
            else:
                next_tick = time.monotonic()

    def run_continuous(self, max_workers: int = None, scenario: Optional[str] = None):
        if max_workers is None:
            max_workers = max(8, min(64, len(self.nodes)))
        start_control_server(self, port=int(os.environ.get("HTTP_PORT", "5055")))
        print("==================================================================")
        print(f"  Heimdall Edge Fleet Simulator - Dataset: {self.dataset} | Seed: {self.seed}")
        print(f"  IPC nodes: {len(self.nodes)} | Stations: {len(self.plant.stations)} "
              f"(every {self.station_interval:g}s, speed {self.plant.speed:g}x)")
        print(f"  MQTT broker: {self.broker_host}:{self.broker_port} | IPC fault rate: {self.fault_rate*100:.1f}%")
        print("==================================================================")

        if scenario:
            print(f"  Scenario '{scenario}': " + "; ".join(self.plant.apply_scenario(scenario)))

        if not self.connect(timeout=5.0):
            print("  MQTT broker not reachable yet - retrying in the background (start the backend).")

        if self.dispatcher is not None:
            self.dispatcher.start()
            print(f"  Plant activity: {self.desk.rate:g} failures per station-hour -> tickets / spare parts via {self.dispatcher.api.base}")
        if self.plant.stations:
            threading.Thread(target=self._station_loop, daemon=True).start()

        def worker_loop(node):
            time.sleep(node.rng.uniform(0.0, self.heartbeat_interval))
            while True:
                if self.mqtt.connected.is_set():
                    self.send_node_heartbeat(node)
                time.sleep(self.heartbeat_interval * node.rng.uniform(0.6, 1.5))

        with concurrent.futures.ThreadPoolExecutor(max_workers=max(max_workers, len(self.nodes))) as executor:
            for node in self.nodes:
                executor.submit(worker_loop, node)

            start_time = time.time()
            last_log_time = 0.0
            try:
                while True:
                    time.sleep(2)
                    elapsed = time.time() - start_time
                    rate = self.total_dispatched / max(1.0, elapsed)
                    link = "connected" if self.mqtt.connected.is_set() else f"OFFLINE ({self.mqtt.last_error or 'connecting'})"
                    msg = (f"[Fleet Live Monitor] MQTT: {link} | Dispatched: {self.total_dispatched} msgs "
                           f"({self.station_blocks_dispatched} station blocks) | Rate: {rate:.1f} msg/s | "
                           f"Errors: {self.total_errors} | Nodes: {len(self.nodes)} | Stations: {len(self.plant.stations)}")
                    if self.dispatcher is not None:
                        done = self.dispatcher.stats
                        msg += (f" | Tickets: {done.get('raise', 0)} raised, {done.get('close', 0)} closed, "
                                f"{done.get('consume', 0)} parts used, queue {self.dispatcher.queue.qsize()}"
                                + (f" (API: {self.dispatcher.last_error[:60]})" if self.dispatcher.last_error else ""))
                    if sys.stdout.isatty():
                        sys.stdout.write(f"\r{msg}")
                        sys.stdout.flush()
                    else:
                        now = time.time()
                        if now - last_log_time >= 5.0:
                            sys.stdout.write(f"{msg}\n")
                            sys.stdout.flush()
                            last_log_time = now
            except KeyboardInterrupt:
                print("\nStopping Edge Fleet Simulator...")
                os._exit(0)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Heimdall Edge Fleet Simulator (MQTT)")
    parser.add_argument("--broker", default=os.environ.get("MQTT_BROKER") or None,
                        help="MQTT broker host[:port] (default: MQTT_BROKER_HOST/MQTT_BROKER_PORT or 127.0.0.1:1883)")
    parser.add_argument("--grpc-host", default=None,
                        help="Deprecated and ignored: the backend no longer exposes gRPC ingestion; use --broker")
    parser.add_argument("--dataset", choices=("enterprise", "demo", "all"),
                        default=os.environ.get("SIM_DATASET", "enterprise"),
                        help="Plant to impersonate: enterprise (db-seed), demo (Nordvik NV1, demo-seed) or all")
    parser.add_argument("--client", type=str, default=None, help="Target specific client node to simulate")
    parser.add_argument("--fault-rate", type=float, default=0.0, help="Random IPC heartbeat anomaly rate (0.0 to 1.0; default 0: only via the control API)")
    parser.add_argument("--activity", dest="activity", action="store_true", default=os.environ.get("SIM_ACTIVITY", "1") not in ("0", "false", "no"),
                        help="Turn station faults into tickets / spare-part use through the backend REST API (default; SIM_ACTIVITY=0 disables)")
    parser.add_argument("--no-activity", dest="activity", action="store_false", help="Telemetry and heartbeats only")
    parser.add_argument("--incident-rate", type=float, default=float(os.environ.get("SIM_INCIDENT_RATE", "0.05")),
                        help="Failures per station and simulated hour (default 0.05)")
    parser.add_argument("--backend-url", default=os.environ.get("BACKEND_API_URL") or "http://127.0.0.1:5099", help="Backend REST API base URL")
    parser.add_argument("--api-token", default=os.environ.get("HEIMDALL_SIM_API_TOKEN") or None,
                        help="Better-Auth session token for the REST calls (a Development backend accepts none)")
    parser.add_argument("--station-fault-rate", type=float, default=0.0,
                        help="Spontaneous station faults per simulated hour (default 0: faults only via the control API)")
    parser.add_argument("--station-interval", type=float, default=float(os.environ.get("SIM_STATION_INTERVAL", "2.0")),
                        help="Seconds between station telemetry blocks")
    parser.add_argument("--heartbeat-interval", type=float, default=10.0, help="Mean seconds between IPC heartbeats")
    parser.add_argument("--speed", type=float, default=1.0, help="Simulated seconds per wall-clock second")
    parser.add_argument("--seed", type=int, default=None, help="Seed for reproducible station behaviour")
    parser.add_argument("--scenario", choices=SCENARIOS, default=None, help="Scenario to apply at start-up")
    parser.add_argument("--no-stations", action="store_true", help="Only send IPC heartbeats")
    parser.add_argument("--smoke-test", action="store_true", help="Publish one round to the broker and exit")
    parser.add_argument("--count", type=int, default=10, help="Number of nodes / stations for the smoke test")
    return parser


if __name__ == "__main__":
    args = build_arg_parser().parse_args()
    if args.grpc_host or os.environ.get("GRPC_HOST"):
        print("Note: --grpc-host / GRPC_HOST is ignored. Heimdall ingests over MQTT; "
              "set --broker or MQTT_BROKER_HOST / MQTT_BROKER_PORT.")

    simulator = IndustrialFleetSimulator(
        broker=args.broker, fault_rate=args.fault_rate, client=args.client, dataset=args.dataset,
        seed=args.seed, station_interval=args.station_interval, heartbeat_interval=args.heartbeat_interval,
        stations_enabled=not args.no_stations, speed=args.speed, station_fault_rate=args.station_fault_rate,
        activity=args.activity and not args.smoke_test, incident_rate=args.incident_rate,
        backend_url=args.backend_url, api_token=args.api_token)
    if args.smoke_test:
        success = simulator.run_smoke_test(count=args.count)
        sys.exit(0 if success else 1)
    else:
        simulator.run_continuous(scenario=args.scenario)
