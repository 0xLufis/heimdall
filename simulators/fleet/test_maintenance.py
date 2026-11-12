#!/usr/bin/env python3
"""
Plant activity: the maintenance desk (faults -> ticket lifecycle -> spare parts) and the REST
dispatcher that performs it. No backend needed: the dispatcher talks to an in-process HTTP stub.
"""

import json
import os
import sys
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "proto"))

import topology  # noqa: E402
from backend_client import ActionDispatcher, BackendApi  # noqa: E402
from maintenance import Action, MaintenanceDesk, modes_for, resource_classes  # noqa: E402
from station_model import FAULTED, IDLE, MAINTENANCE, RUNNING, PlantModel, StationProcess, StationSpec  # noqa: E402

LIFECYCLE = ["raise", "reserve", "pickup", "resolve", "close"]


def run_desk(dataset, seed, hours=6.0, rate=0.3, dt=4.0):
    _, specs = topology.load(dataset)
    plant = PlantModel(specs, seed=seed)
    desk = MaintenanceDesk(plant, topology.load_profile(dataset), seed=seed, incidents_per_station_hour=rate)
    for _ in range(int(hours * 3600 / dt)):
        plant.step(dt)
        desk.step(dt)
    return plant, desk


class StationCoherenceTests(unittest.TestCase):
    def test_faulted_station_raises_alarm_and_stopped_station_has_no_cycle(self):
        st = StationProcess(StationSpec("A", "A", "CncMachining", "HW"), seed=1)
        for _ in range(30):
            st.step(2.0)
        self.assertIsNone(st.alarm_code)
        self.assertGreater(st.sample()["cycle_time_s"], 0)
        st.inject_fault(reason="Tool changer position fault", alarm_code="ALM-CNC-120")
        for _ in range(60):
            st.step(2.0)
        s = st.sample()
        self.assertEqual((s["state"], st.alarm_code), (FAULTED, "ALM-CNC-120"))
        self.assertEqual((s["cycle_time_s"], s["spindle_rpm"], s["vibration_mms"]), (0.0, 0.0, 0.0))
        st.recover()
        st.step(2.0)
        self.assertIsNone(st.alarm_code)

    def test_sub_machine_stops_with_its_cell(self):
        specs = [StationSpec("CELL", "CELL", "LaserWelder", "HW"),
                 StationSpec("CELL-RB", "CELL-RB", "RobotCell", "HW", parent_station_id="CELL")]
        plant = PlantModel(specs, seed=3)
        plant.step(2.0)
        self.assertEqual(plant.get("CELL-RB").state, RUNNING)
        plant.get("CELL").start_maintenance()
        plant.step(2.0)
        self.assertEqual((plant.get("CELL-RB").state, plant.get("CELL-RB").state_reason), (IDLE, "cell stopped"))
        plant.get("CELL").recover()
        plant.step(2.0)
        self.assertEqual(plant.get("CELL-RB").state, RUNNING)

    def test_every_station_and_controller_of_both_datasets_is_driven(self):
        for dataset, n_stations in (("enterprise", 100), ("demo", 19)):
            nodes, specs = topology.load(dataset)
            self.assertEqual(len(specs), n_stations, dataset)
            ids = {n.machine_identifier for n in nodes}
            self.assertTrue(all(s.controller_machine_identifier in ids for s in specs), dataset)
            self.assertTrue(all(s.components for s in specs if not s.parent_station_id), dataset)


class MaintenanceDeskTests(unittest.TestCase):
    def test_same_seed_same_activity(self):
        a = [x.log_line() for x in run_desk("demo", 7)[1].log]
        b = [x.log_line() for x in run_desk("demo", 7)[1].log]
        c = [x.log_line() for x in run_desk("demo", 8)[1].log]
        self.assertTrue(a)
        self.assertEqual(a, b)
        self.assertNotEqual(a, c)

    def test_ticket_lifecycle_order_and_stock(self):
        plant, desk = run_desk("enterprise", 11, hours=6, dt=8.0)
        by_incident = {}
        for a in desk.log:
            if a.incident:
                by_incident.setdefault(a.incident, []).append(a)
        closed = [acts for acts in by_incident.values() if acts[-1].kind == "close" and acts[0].kind == "raise"]
        self.assertGreater(len(closed), 20)
        full = [acts for acts in closed if [a.kind for a in acts if a.kind in LIFECYCLE] == LIFECYCLE]
        self.assertGreater(len(full), 10, "most incidents run through the whole lifecycle")
        for acts in by_incident.values():
            kinds = [a.kind for a in acts]
            self.assertEqual(kinds[0], "raise")
            times = [a.at_s for a in acts]
            self.assertEqual(times, sorted(times))
            if "consume" in kinds:
                self.assertLess(kinds.index("pickup"), kinds.index("consume"))
                if "resolve" in kinds:
                    self.assertLess(kinds.index("consume"), kinds.index("resolve"))
        self.assertGreater(desk.counts.get("consume", 0), 0)
        self.assertTrue(all(v >= 0 for v in desk.stock_serialized.values()))
        self.assertTrue(all(v >= 0 for v in desk.stock_consumables.values()))
        # stations under repair are in maintenance, faulted stations without a ticket do not exist
        for key, st in plant.stations.items():
            if st.state == FAULTED:
                self.assertIn(key, desk.incidents)
            if st.state == MAINTENANCE:
                self.assertIn(desk.incidents[key].stage, ("in_progress", "waiting_parts", "repairing"))

    def test_resource_classes_exist_in_the_datasets(self):
        classes = resource_classes()
        stocked = set()
        for dataset in ("enterprise", "demo"):
            p = topology.load_profile(dataset)
            stocked |= set(p.serialized) | set(p.consumables)
        self.assertTrue(classes["slot"] & stocked)
        self.assertTrue(classes["consumable"] & stocked)
        for machine_type in ("LaserWelder", "CncMachining", "RobotCell"):
            self.assertTrue(modes_for(machine_type))


# ----------------------------------------------------------------------------- REST dispatcher
class _Stub(BaseHTTPRequestHandler):
    calls = []
    stock = {"id": "stock-1", "stockQuantity": 4, "metadata": {"consumableClass": "DISP-NOZZLE"}, "isStockItem": True}

    def log_message(self, *a):
        pass

    def _reply(self, code, body):
        raw = json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def _handle(self):
        length = int(self.headers.get("Content-Length") or 0)
        body = json.loads(self.rfile.read(length)) if length else None
        _Stub.calls.append((self.command, self.path, body))
        if self.path == "/api/v1/machine":
            return self._reply(200, [{"id": "m-1", "name": "L01-OP030", "customIdentifier": "LINE-01-OP030", "pinnedObjectHandle": "L01-OP030"}])
        if self.path == "/api/v1/tickets":
            return self._reply(200, [])
        if self.path == "/api/v1/tickets/machine-raised":
            return self._reply(201, {"ticketId": "t-1"})
        if self.path == "/api/v1/inventory/stock":
            return self._reply(200, [_Stub.stock])
        if self.path == "/api/v1/inventory/parts":
            return self._reply(200, [{"id": "spare-1", "equipmentStatus": "InStorage", "isStockItem": False, "machineId": None,
                                       "metadata": {"partClass": "VALVE-TERMINAL"}}])
        if self.path == "/api/v1/component/machine/m-1":
            return self._reply(200, [{"id": "c-1", "functionalSlotIdentifier": "TOOL-HEAD-01"}])
        return self._reply(200, {})

    do_GET = do_POST = do_PUT = do_PATCH = _handle


class DispatcherTests(unittest.TestCase):
    def test_actions_map_onto_the_backend_ticket_and_inventory_api(self):
        _Stub.calls = []
        server = ThreadingHTTPServer(("127.0.0.1", 0), _Stub)
        threading.Thread(target=server.serve_forever, daemon=True).start()
        try:
            d = ActionDispatcher(BackendApi(f"http://127.0.0.1:{server.server_address[1]}"))
            inc, st = "L01-OP030#1", "L01-OP030"
            actions = [
                Action("raise", inc, st, 0, {"alarm_code": "ALM-DSP-112", "title": "Dispense flow below limit - L01-OP030", "description": "x",
                                             "priority": "High", "is_line_stop": False, "slot": "TOOL-HEAD-01", "controller": "HW-IPC",
                                             "probes": [{"metric": "power_kw", "unit": "kW", "value": 1.6, "buffer": [{"age_s": 4.0, "value": 1.6}]}]}),
                Action("reserve", inc, st, 1, {"technician": "Impregnator Ian", "technician_id": "usr-synth-55"}),
                Action("pickup", inc, st, 2, {"technician": "Impregnator Ian"}),
                Action("consume", inc, st, 3, {"resource": "consumable", "part_class": "DISP-NOZZLE", "slot": None,
                                               "technician": "Impregnator Ian", "technician_id": "usr-synth-55"}),
                Action("consume", inc, st, 3, {"resource": "slot", "part_class": "VALVE-TERMINAL", "slot": "TOOL-HEAD-01",
                                               "technician": "Impregnator Ian", "technician_id": "usr-synth-55"}),
                Action("resolve", inc, st, 4, {"technician": "Impregnator Ian"}),
                Action("close", inc, st, 5, {"cleared_reason": "ok", "transient": False}),
            ]
            for a in actions:
                d.handle(a)
        finally:
            server.shutdown()
            server.server_close()
        writes = [(m, p) for m, p, _ in _Stub.calls if m != "GET"]
        self.assertEqual(writes, [
            ("POST", "/api/v1/tickets/machine-raised"),
            ("POST", "/api/v1/tickets/t-1/reserve"),
            ("POST", "/api/v1/tickets/t-1/qr-pickup"),
            ("PUT", "/api/v1/inventory/stock-1"),
            ("POST", "/api/v1/component/assign-part"),
            ("PATCH", "/api/v1/tickets/t-1/status"),
            ("POST", "/api/v1/tickets/t-1/auto-close"),
        ])
        bodies = {p: b for m, p, b in _Stub.calls if m != "GET"}
        raised = bodies["/api/v1/tickets/machine-raised"]
        self.assertEqual((raised["machineId"], raised["alarmCode"], raised["targetComponentDeviceTag"]), ("m-1", "ALM-DSP-112", "TOOL-HEAD-01"))
        self.assertEqual(raised["probes"][0]["probeBuffer"][0]["value"], 1.6)
        self.assertEqual(bodies["/api/v1/inventory/stock-1"], {"stockQuantity": 3})
        swap = bodies["/api/v1/component/assign-part"]
        self.assertEqual((swap["existingComponentId"], swap["partId"], swap["associatedTicketId"]), ("c-1", "spare-1", "t-1"))
        self.assertEqual(bodies["/api/v1/tickets/t-1/status"], "Resolved")

    def test_refuses_remote_backend(self):
        with self.assertRaises(ValueError):
            BackendApi("http://prod.example.com:5099")


if __name__ == "__main__":
    unittest.main()
