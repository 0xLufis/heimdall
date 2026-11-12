#!/usr/bin/env python3
"""
Unit tests for the fleet simulator's station process model, PLC block encoding, dataset
topology, MQTT framing and the HTTP control API.

Run:  python -m unittest test_station_simulation -v      (needs protobuf + paho-mqtt)
No broker or backend is required; the MQTT test talks to a minimal in-process MQTT 5 stub.
"""

import json
import os
import socket
import sys
import threading
import unittest
import urllib.error
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "proto"))

import telemetry_pb2
import topology
from mqtt_transport import MqttPublisher, parse_broker, sanitize, topic
from plc_encoding import FIELD_MAP, STATE_FIELD, build_station_block
from station_model import (DRIFT_PROFILES, FAULTED, IDLE, MAINTENANCE, RUNNING, STATES, WARNING,
                           PlantModel, StationProcess, StationSpec)

# Mirror of StationTelemetryExtractor.BuildAliases()
# (src/backend/App.Backend.Api/Services/StationTelemetrySnapshot.cs). If the backend alias list
# changes, update this copy - the simulator must only emit names the extractor recognises.
EXTRACTOR_ALIASES = {
    "VibrationMmS": ["vibrationmms", "vibrationrms", "vibrationrmsmms", "vibration", "vibrms", "vibrationvelocity"],
    "SpindleRpm": ["spindlerpm", "spindlespeed", "spindlespeedrpm", "rpm"],
    "CycleTimeMs": ["cycletimems", "actualcycletimems", "actualcycletime"],
    "PowerKw": ["powerkw", "activepowerkw", "power"],
    "TemperatureC": ["temperaturec", "temperature", "motortemp", "motortempc", "temp"],
    "FrequencyHz": ["frequencyhz", "frequency", "linefrequency"],
    "Availability": ["availability", "availabilitypct", "availabilitypercent"],
    "Performance": ["performance", "performancepct", "performancepercent"],
    "Quality": ["quality", "qualitypct", "qualitypercent"],
    "Oee": ["oee", "oeepct", "oeepercent", "oeepercentage"],
    "PartsPerHour": ["partsperhour", "throughput", "throughputpartsperhour"],
    "TargetPartsPerHour": ["targetpartsperhour", "plannedpartsperhour"],
    "CycleTimeSeconds": ["cycletimeseconds", "cycletimes", "cycletimesec", "cycletime"],
    "TargetCycleTimeSeconds": ["targetcycletimeseconds", "targetcycletime", "tacttime", "tacttimeseconds"],
    "TotalPartsProduced": ["totalpartsproduced", "partsproduced", "partcount", "totalparts", "partcounter"],
    "ScrapParts": ["scrapparts", "scrapcount", "rejectedparts", "rejects", "nokparts"],
    "OperationalStatus": ["operationalstatus", "machinestate", "stationstate", "state", "status"],
}
ALIAS_TO_FIELD = {alias: f for f, names in EXTRACTOR_ALIASES.items() for alias in names}

# Mirror of StationTelemetryExtractor.NormalizeStatus()
STATUS_LABELS = {
    "RUNNING": {"running", "run", "production", "producing", "auto", "automatic"},
    "IDLE": {"idle", "stopped", "stop", "standby", "ready", "waiting", "starved", "blocked"},
    "WARNING": {"warning", "warn", "degraded"},
    "FAULTED": {"fault", "faulted", "error", "alarm", "emergencystop", "estop"},
    "MAINTENANCE": {"maintenance", "service", "setup", "changeover"},
}


def extractor_normalize(name: str) -> str:
    """Mirror of StationTelemetryExtractor.Normalize()."""
    if not name:
        return ""
    start = 1 if len(name) > 2 and name[0] in "fnbirlsuwdeq" and name[1].isupper() else 0
    return "".join(c.lower() for c in name[start:] if c.isalnum())


def extractor_status(label: str):
    s = (label or "").strip().lower()
    return next((k for k, v in STATUS_LABELS.items() if s in v), None)


def leaves(struct, depth=0):
    """Yields (name, TelemetryValue) for every non-struct field, like StationTelemetryExtractor.Walk."""
    assert depth <= 4, "extractor stops at depth 4"
    for name, child in struct.fields.items():
        if child.WhichOneof("value") == "struct_value":
            yield from leaves(child.struct_value, depth + 1)
        else:
            yield name, child


def spec(station_id="ST-1", machine_type="CncMachining", **kw):
    return StationSpec(station_id=station_id, name=station_id, machine_type=machine_type,
                       controller_machine_identifier="HW-IPC-1", line="L01", **kw)


def run(station, seconds, dt=2.0):
    for _ in range(int(seconds / dt)):
        station.step(dt)


class StationModelTests(unittest.TestCase):
    def test_same_seed_is_deterministic_and_seeds_differ(self):
        def trace(seed):
            st = StationProcess(spec(), seed=seed)
            st.start_drift("bearing_wear", duration_s=300)
            out = []
            for _ in range(400):
                st.step(2.0)
                out.append(json.dumps(st.sample(), sort_keys=True))
            return out
        self.assertEqual(trace(42), trace(42))
        self.assertNotEqual(trace(42), trace(43))

    def test_stations_with_same_seed_are_decorrelated(self):
        a = StationProcess(spec("ST-A"), seed=1)
        b = StationProcess(spec("ST-B"), seed=1)
        run(a, 60), run(b, 60)
        self.assertNotEqual(a.sample()["vibration_mms"], b.sample()["vibration_mms"])

    def test_counters_are_monotonic_and_kpis_bounded(self):
        st = StationProcess(spec(), seed=3, random_fault_rate=20.0)
        last_parts, last_scrap = st.parts_produced, st.scrap_parts
        seen_states = set()
        for i in range(9000):
            if i == 500:
                st.start_drift("bearing_wear", duration_s=600)
            if i == 1500:
                st.recover()
            st.step(2.0)
            s = st.sample()
            seen_states.add(s["state"])
            self.assertIn(s["state"], STATES)
            self.assertGreaterEqual(s["parts_produced"], last_parts)
            self.assertGreaterEqual(s["scrap_parts"], last_scrap)
            self.assertLessEqual(s["scrap_parts"], s["parts_produced"])
            for k in ("availability", "performance", "quality"):
                self.assertGreaterEqual(s[k], 0.0)
                self.assertLessEqual(s[k], 100.0)
            for k in ("vibration_mms", "power_kw", "cycle_time_ms", "parts_per_hour", "spindle_rpm"):
                self.assertGreaterEqual(s[k], 0.0)
            last_parts, last_scrap = s["parts_produced"], s["scrap_parts"]
        self.assertTrue({RUNNING, IDLE, FAULTED, WARNING} <= seen_states, seen_states)

    def test_healthy_station_produces_near_target(self):
        st = StationProcess(spec(cycle_time_target_s=20.0), seed=5)
        before = st.parts_produced
        run(st, 3600)
        produced = st.parts_produced - before
        self.assertGreater(produced, 140)   # 180 at 100 %, minus micro-stops
        self.assertLessEqual(produced, 180)
        self.assertEqual(st.sample()["target_parts_per_hour"], 180.0)
        self.assertEqual(st.sample()["target_cycle_time_s"], 20.0)
        self.assertIn(st.state, (RUNNING, IDLE))

    def test_injected_fault_stops_production_until_recovered(self):
        st = StationProcess(spec(), seed=9)
        st.inject_fault()
        run(st, 120)
        parts = st.parts_produced
        run(st, 600)
        self.assertEqual(st.state, FAULTED)
        self.assertEqual(st.parts_produced, parts)
        self.assertLess(st.sample()["spindle_rpm"], 1.0)
        self.assertLess(st.sample()["power_kw"], 2.0)
        st.recover()
        run(st, 120)
        self.assertIn(st.state, (RUNNING, IDLE))
        self.assertGreater(st.parts_produced, parts)

    def test_timed_fault_and_line_stop_auto_restart(self):
        st = StationProcess(spec(), seed=9)
        st.inject_fault(duration_s=60)
        run(st, 30)
        self.assertEqual(st.state, FAULTED)
        run(st, 40)
        self.assertNotEqual(st.state, FAULTED)

        st.line_stop(duration_s=60)
        run(st, 30)
        self.assertEqual((st.state, st.scenario), (IDLE, "line_stop"))
        run(st, 40)
        self.assertIsNone(st.scenario)

    def test_line_stop_does_not_mask_fault_or_maintenance(self):
        st = StationProcess(spec(), seed=9)
        st.start_maintenance()
        st.line_stop()
        run(st, 60)
        self.assertEqual(st.state, MAINTENANCE)

    def test_laser_drift_follows_the_demo_story(self):
        st = StationProcess(spec("NV-L2-OP060", "LaserWelder", nominal_power_kw=6.0,
                                 temp_alarm_c=55, power_alarm_low_kw=5.7), seed=11)
        run(st, 60)
        p0, t0 = st.sample()["power_kw"], st.sample()["temperature_c"]
        self.assertAlmostEqual(p0, 6.0, delta=0.15)
        st.start_drift("laser_drift", duration_s=900)

        states, min_running_power, max_temp = [], p0, t0
        for _ in range(600):
            st.step(2.0)
            s = st.sample()
            if not states or states[-1] != s["state"]:
                states.append(s["state"])
            if s["state"] in (RUNNING, WARNING):
                min_running_power = min(min_running_power, s["power_kw"])
                max_temp = max(max_temp, s["temperature_c"])
        ordered = [s for s in states if s in (WARNING, FAULTED)]
        self.assertIn(WARNING, ordered)
        self.assertEqual(ordered[-1], FAULTED, states)
        self.assertLess(ordered.index(WARNING), ordered.index(FAULTED))
        self.assertLess(min_running_power, 5.85)   # power sagged below the warning band
        self.assertGreater(max_temp, t0 + 2.0)     # protective glass heated up
        self.assertEqual(st.state, FAULTED)        # alarm is latched

        st.recover()
        run(st, 900)
        self.assertIn(st.state, (RUNNING, IDLE))
        self.assertIsNone(st.drift)

    def test_unknown_drift_profile_is_rejected(self):
        with self.assertRaises(ValueError):
            StationProcess(spec(), seed=1).start_drift("nope")

    def test_plant_scenarios_speed_and_lookup(self):
        specs = [spec("NV-L2-OP060", "LaserWelder", handle="L02-OP060"), spec("NV-L3-OP020"), spec("NV-L1-OP030", "Dispenser")]
        specs[0].line, specs[1].line, specs[2].line = "L02", "L03", "L01"
        plant = PlantModel(specs, seed=2)
        self.assertIs(plant.get("l02-op060"), plant.get("NV-L2-OP060"))
        self.assertEqual(plant.lines(), ["L01", "L02", "L03"])
        applied = plant.apply_scenario("demo_story")
        self.assertEqual(len(applied), 3)
        self.assertEqual(plant.get("NV-L2-OP060").scenario, "drift:laser_drift")
        self.assertEqual(plant.set_speed(1000), 120.0)
        plant.set_speed(10)
        plant.step(2.0)
        self.assertAlmostEqual(plant.get("NV-L3-OP020").sim_time_s, 20.0)
        self.assertEqual(plant.recover(line="L02"), ["NV-L2-OP060"])
        self.assertIsNone(plant.get("NV-L2-OP060").scenario)
        self.assertIsNotNone(plant.get("NV-L3-OP020").scenario)
        with self.assertRaises(ValueError):
            plant.apply_scenario("nope")


class PlcEncodingTests(unittest.TestCase):
    def roundtrip(self, station):
        block = build_station_block(station.spec.station_id, station.sample(), ip_address="10.42.2.16")
        decoded = telemetry_pb2.AdsPlcMemoryBlock()
        decoded.ParseFromString(block.SerializeToString())
        return decoded

    def test_every_emitted_field_is_recognised_by_the_extractor(self):
        st = StationProcess(spec(), seed=1)
        run(st, 30)
        block = self.roundtrip(st)
        self.assertEqual(block.structured_value.WhichOneof("value"), "struct_value")

        mapped = {}
        for name, value in leaves(block.structured_value.struct_value):
            norm = extractor_normalize(name)
            self.assertIn(norm, ALIAS_TO_FIELD, f"'{name}' ({norm}) is not an extractor alias")
            field = ALIAS_TO_FIELD[norm]
            self.assertNotIn(field, mapped, f"{field} emitted twice")
            mapped[field] = value
        # CncMachining reports every signal; Oee is intentionally left to the backend (A x P x Q).
        self.assertEqual(set(mapped), set(EXTRACTOR_ALIASES) - {"Oee"})

        for field, value in mapped.items():
            kind = value.WhichOneof("value")
            if field == "OperationalStatus":
                self.assertEqual(kind, "state_value")
                self.assertEqual(extractor_status(value.state_value.state_label), st.state)
            else:
                self.assertIn(kind, ("double_value", "int64_value"), field)   # ToDouble-compatible

    def test_values_survive_the_roundtrip(self):
        st = StationProcess(spec(), seed=1)
        run(st, 30)
        sample = st.sample()
        root = self.roundtrip(st).structured_value.struct_value
        dyn, prod = root.fields["Dynamics"].struct_value, root.fields["Production"].struct_value
        self.assertAlmostEqual(dyn.fields["fVibrationRms"].double_value, sample["vibration_mms"])
        self.assertAlmostEqual(dyn.fields["fActualCycleTimeMs"].double_value, sample["cycle_time_ms"])
        self.assertEqual(prod.fields["nPartsProduced"].int64_value, sample["parts_produced"])
        self.assertEqual(prod.fields["nScrapParts"].int64_value, sample["scrap_parts"])

    def test_block_envelope(self):
        st = StationProcess(spec("NV-L2-OP060", "LaserWelder"), seed=1)
        block = self.roundtrip(st)
        self.assertEqual(block.metadata["StationId"], "NV-L2-OP060")
        self.assertEqual(block.ams_net_id, "10.42.2.16.1.1")
        self.assertEqual(block.ams_port, 851)
        self.assertEqual(block.quality, telemetry_pb2.QUALITY_GOOD)
        self.assertTrue(block.symbol_name.endswith("._data"))
        self.assertGreater(block.timestamp.seconds, 0)

    def test_signals_a_machine_does_not_have_are_omitted(self):
        st = StationProcess(spec("NV-L2-OP060", "LaserWelder"), seed=1)   # no spindle
        names = {extractor_normalize(n) for n, _ in leaves(self.roundtrip(st).structured_value.struct_value)}
        self.assertNotIn("spindlespeedrpm", names)
        self.assertIn("activepowerkw", names)

    def test_all_states_map_to_backend_status_vocabulary(self):
        for state in STATES:
            self.assertEqual(extractor_status(state), state)

    def test_field_map_covers_only_known_aliases(self):
        for _, (_, plc_name, _) in FIELD_MAP.items():
            self.assertIn(extractor_normalize(plc_name), ALIAS_TO_FIELD)
        self.assertEqual(ALIAS_TO_FIELD[extractor_normalize(STATE_FIELD)], "OperationalStatus")


class TopologyTests(unittest.TestCase):
    def test_demo_dataset_links(self):
        nodes, stations = topology.load_demo()
        self.assertEqual(len(nodes), 6)
        by_id = {s.station_id: s for s in stations}
        self.assertEqual(len(by_id), len(stations))
        self.assertEqual(len(stations), 19)         # 15 line stations + 4 sub-machines of the two cells
        hero = by_id["NV-L2-OP060"]
        self.assertEqual(hero.controller_machine_identifier, "HW-NV-IPC-L2-LASER")   # Primary beats Secondary
        self.assertEqual((hero.handle, hero.line, hero.machine_type), ("L02-OP060", "L02", "LaserWelder"))
        self.assertEqual((hero.nominal_power_kw, hero.power_alarm_low_kw, hero.temp_alarm_c), (6.0, 5.7, 55))
        self.assertEqual(by_id["NV-L2-OP070"].cycle_time_target_s, 42.0)
        self.assertNotIn("NV-L3-OP100", by_id)          # decommissioned / no controller
        identifiers = {n.machine_identifier for n in nodes}
        self.assertTrue(all(s.controller_machine_identifier in identifiers for s in stations))

    def test_enterprise_dataset_links(self):
        nodes, stations = topology.load_enterprise()
        self.assertEqual(len(nodes), 56)
        self.assertGreaterEqual(len(stations), 50)
        identifiers = {n.machine_identifier for n in nodes}
        self.assertTrue(all(s.controller_machine_identifier in identifiers for s in stations))
        st = next(s for s in stations if s.station_id == "L01-OP030")
        self.assertEqual(st.controller_machine_identifier, "HW-IPC-L01-OP030-DEDICATED")
        self.assertEqual((st.line, st.machine_type), ("L01", "Dispenser"))
        self.assertEqual(len({s.station_id for s in stations}), len(stations))

    def test_all_dataset_keeps_lines_apart(self):
        _, stations = topology.load("all")
        lines = {s.line for s in stations}
        self.assertIn("NV-L02", lines)
        self.assertIn("L02", lines)


# ----------------------------------------------------------------------------- MQTT framing
def _read_exact(conn, n):
    buf = b""
    while len(buf) < n:
        chunk = conn.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("closed")
        buf += chunk
    return buf


def _read_packet(conn):
    first = _read_exact(conn, 1)[0]
    length, shift = 0, 0
    while True:
        b = _read_exact(conn, 1)[0]
        length |= (b & 0x7F) << shift
        shift += 7
        if not b & 0x80:
            break
    return first, _read_exact(conn, length)


def _varint(data, pos):
    value, shift = 0, 0
    while True:
        b = data[pos]
        pos += 1
        value |= (b & 0x7F) << shift
        shift += 7
        if not b & 0x80:
            return value, pos


def _string(data, pos):
    n = int.from_bytes(data[pos:pos + 2], "big")
    return data[pos + 2:pos + 2 + n].decode("utf-8"), pos + 2 + n


class Mqtt5Stub:
    """Just enough of an MQTT 5 broker to capture one client's CONNECT and PUBLISH packets."""

    def __init__(self):
        self.sock = socket.socket()
        self.sock.bind(("127.0.0.1", 0))
        self.sock.listen(1)
        self.port = self.sock.getsockname()[1]
        self.connect = None
        self.publishes = []
        self.got_publish = threading.Event()
        threading.Thread(target=self._serve, daemon=True).start()

    def _serve(self):
        conn = None
        try:
            conn, _ = self.sock.accept()
            while True:
                first, body = _read_packet(conn)
                ptype = first >> 4
                if ptype == 1:      # CONNECT
                    name, pos = _string(body, 0)
                    level, flags = body[pos], body[pos + 1]
                    pos += 4        # level, flags, keepalive
                    plen, pos = _varint(body, pos)
                    pos += plen
                    client_id, pos = _string(body, pos)
                    user = password = None
                    if flags & 0x80:
                        user, pos = _string(body, pos)
                    if flags & 0x40:
                        password, pos = _string(body, pos)
                    self.connect = {"protocol": name, "level": level, "client_id": client_id, "user": user, "password": password}
                    conn.sendall(bytes([0x20, 3, 0, 0, 0]))
                elif ptype == 3:    # PUBLISH
                    qos = (first >> 1) & 3
                    topic_name, pos = _string(body, 0)
                    pid = None
                    if qos:
                        pid = body[pos:pos + 2]
                        pos += 2
                    plen, pos = _varint(body, pos)
                    props, end, user_props = body[pos:pos + plen], pos + plen, {}
                    i = 0
                    while i < len(props):
                        if props[i] != 0x26:
                            raise AssertionError(f"unexpected PUBLISH property 0x{props[i]:02x}")
                        k, j = _string(props, i + 1)
                        v, i = _string(props, j)
                        user_props[k] = v
                    self.publishes.append({"topic": topic_name, "qos": qos, "user_props": user_props, "payload": body[end:]})
                    if pid:
                        conn.sendall(bytes([0x40, 2]) + pid)
                    self.got_publish.set()
                elif ptype == 12:   # PINGREQ
                    conn.sendall(bytes([0xD0, 0]))
                elif ptype == 14:   # DISCONNECT
                    break
        except (ConnectionError, OSError):
            pass
        finally:
            if conn is not None:
                conn.close()

    def close(self):
        self.sock.close()


class MqttTransportTests(unittest.TestCase):
    def test_topics_match_backend_layout(self):
        self.assertEqual(topic("HW-NV-IPC-L2-LASER", "plc_memory"), "heimdall/devices/HW-NV-IPC-L2-LASER/plc_memory")
        self.assertEqual(sanitize(" a/b+c#d "), "a_b_c_d")
        self.assertEqual(sanitize(""), "unknown")

    def test_parse_broker(self):
        self.assertEqual(parse_broker("backend"), ("backend", 1883))
        self.assertEqual(parse_broker("backend:2883"), ("backend", 2883))
        self.assertEqual(parse_broker("mqtt://10.0.0.5:1884"), ("10.0.0.5", 1884))
        self.assertEqual(parse_broker(None), ("127.0.0.1", 1883))

    def test_publishes_mqtt5_with_agent_key_like_the_real_agent(self):
        stub = Mqtt5Stub()
        pub = MqttPublisher("127.0.0.1", stub.port, agent_key="heimdall-dev-agent-key")
        try:
            pub.start()
            self.assertTrue(pub.wait_connected(5.0), pub.last_error)
            st = StationProcess(spec("NV-L2-OP060", "LaserWelder"), seed=1)
            payload = build_station_block("NV-L2-OP060", st.sample()).SerializeToString()
            self.assertTrue(pub.publish("HW-NV-IPC-L2-LASER", "plc_memory", payload, qos=1, wait=5.0))
            self.assertTrue(stub.got_publish.wait(5.0))
        finally:
            pub.stop()
            stub.close()

        self.assertEqual((stub.connect["protocol"], stub.connect["level"]), ("MQTT", 5))
        self.assertEqual((stub.connect["user"], stub.connect["password"]), ("heimdall_agent", "heimdall-dev-agent-key"))
        msg = stub.publishes[0]
        self.assertEqual(msg["topic"], "heimdall/devices/HW-NV-IPC-L2-LASER/plc_memory")
        self.assertEqual(msg["qos"], 1)
        self.assertEqual(msg["user_props"], {"x-agent-key": "heimdall-dev-agent-key"})
        decoded = telemetry_pb2.AdsPlcMemoryBlock()
        decoded.ParseFromString(msg["payload"])
        self.assertEqual(decoded.metadata["StationId"], "NV-L2-OP060")

    def test_publish_without_broker_reports_failure(self):
        pub = MqttPublisher("127.0.0.1", 1, agent_key="k")
        self.assertFalse(pub.publish("HW-X", "plc_memory", b"x"))


# ----------------------------------------------------------------------------- control API
class ControlApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        import fleet_simulator
        cls.sim = fleet_simulator.IndustrialFleetSimulator(broker="127.0.0.1:1", dataset="demo", seed=7, fault_rate=0.0)
        cls.server = fleet_simulator.start_control_server(cls.sim, port=0, host="127.0.0.1")
        cls.base = f"http://127.0.0.1:{cls.server.server_address[1]}"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()

    def call(self, path, body=None):
        req = urllib.request.Request(self.base + path, method="POST" if body is not None else "GET",
                                     data=json.dumps(body).encode() if body is not None else None,
                                     headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=5) as res:
                return res.status, json.loads(res.read()), res.headers
        except urllib.error.HTTPError as e:
            with e:
                return e.code, json.loads(e.read()), e.headers

    def station(self, sid):
        _, data, _ = self.call("/api/stations")
        return next(s for s in data["stations"] if s["id"] == sid)

    def test_status_lists_nodes_stations_and_transport(self):
        code, data, headers = self.call("/api/status")
        self.assertEqual(code, 200)
        self.assertEqual((data["active_fleet"], data["active_stations"], data["dataset"], data["seed"]), (6, 19, "demo", 7))
        self.assertFalse(data["transport"]["connected"])
        self.assertEqual(data["lines"], ["L01", "L02", "L03"])
        self.assertEqual(sorted(data["drift_profiles"]), sorted(DRIFT_PROFILES))
        hero = next(s for s in data["stations"] if s["id"] == "NV-L2-OP060")
        self.assertEqual(hero["handle"], "L02-OP060")
        self.assertIn("power_kw", hero["values"])
        self.assertIsNone(headers.get("Access-Control-Allow-Origin"))   # no wildcard CORS by default

    def test_station_scenarios_roundtrip(self):
        code, data, _ = self.call("/api/station/fault", {"station": "NV-L1-OP040"})
        self.assertEqual((code, data["success"]), (200, True))
        self.assertEqual(self.station("NV-L1-OP040")["state"], FAULTED)

        code, _, _ = self.call("/api/station/drift", {"station": "L02-OP060", "profile": "laser_drift", "duration_s": 300})
        self.assertEqual(code, 200)
        self.assertEqual(self.station("NV-L2-OP060")["scenario"], "drift:laser_drift")

        code, _, _ = self.call("/api/station/maintenance", {"station": "NV-L3-OP070"})
        self.assertEqual(self.station("NV-L3-OP070")["state"], MAINTENANCE)

        code, data, _ = self.call("/api/line/stop", {"line": "L01"})
        self.assertEqual(code, 200)
        self.assertEqual(self.station("NV-L1-OP010")["state"], IDLE)
        self.assertEqual(self.station("NV-L1-OP040")["state"], FAULTED)   # fault not masked by the line stop

        code, data, _ = self.call("/api/recover", {"line": "L01"})
        self.assertEqual(len(data["targets"]), 6)
        self.assertEqual(self.station("NV-L1-OP040")["state"], RUNNING)
        self.assertEqual(self.station("NV-L3-OP070")["state"], MAINTENANCE)

        self.call("/api/recover", {})
        self.assertTrue(all(s["scenario"] is None for s in self.call("/api/stations")[1]["stations"]))

    def test_legacy_endpoints_still_work(self):
        host = self.sim.nodes[0].hostname
        code, data, _ = self.call("/api/fault", {"hostname": host})
        self.assertEqual((code, data["target"]), (200, host))
        self.assertTrue(self.sim.nodes[0].is_faulty)
        code, data, _ = self.call("/api/fault", {"station": "NV-L2-OP050"})
        self.assertEqual(data["target"], "NV-L2-OP050")
        code, data, _ = self.call("/api/clear-faults", {})
        self.assertTrue(data["success"])
        self.assertFalse(self.sim.nodes[0].is_faulty)
        self.assertEqual(self.station("NV-L2-OP050")["state"], RUNNING)
        code, data, _ = self.call("/api/rate", {"fault_rate": 0.25})
        self.assertEqual(data["fault_rate"], 0.25)
        self.call("/api/rate", {"fault_rate": 0.0})

    def test_speed_and_scenario(self):
        code, data, _ = self.call("/api/speed", {"speed": 30})
        self.assertEqual(data["speed"], 30.0)
        self.assertEqual(self.call("/api/status")[1]["speed"], 30.0)
        self.call("/api/speed", {"speed": 1})
        code, data, _ = self.call("/api/scenario", {"name": "demo_story"})
        self.assertEqual(len(data["applied"]), 3)
        self.call("/api/scenario", {"name": "clear"})

    def test_bad_requests(self):
        self.assertEqual(self.call("/api/station/fault", {"station": "NOPE"})[0], 404)
        self.assertEqual(self.call("/api/station/drift", {"station": "NV-L2-OP060", "profile": "nope"})[0], 400)
        self.assertEqual(self.call("/api/line/stop", {"line": "L99"})[0], 404)
        self.assertEqual(self.call("/api/scenario", {"name": "nope"})[0], 400)
        self.assertEqual(self.call("/api/speed", {"speed": "fast"})[0], 400)
        self.assertEqual(self.call("/api/nope", {})[0], 404)
        self.assertEqual(self.call("/api/nope")[0], 404)


if __name__ == "__main__":
    unittest.main()
