#!/usr/bin/env python3
"""
Heimdall Industrial Edge Fleet Simulator (V1)
Simulates heterogeneous industrial IPCs, Soft-PLCs (Beckhoff TwinCAT ADS),
Siemens SIMATIC controllers, robot cells, and vision sensors.
Supports live streaming gRPC telemetry, dynamic fault injection, and terminal monitoring.
Includes an interactive embedded HTTP server on port 5055 for real-time control.
"""

import sys
import os
import time
import math
import random
import datetime
import json
import csv
import uuid
import argparse
import concurrent.futures
from dataclasses import dataclass, field
from enum import Enum
from http.server import HTTPServer, BaseHTTPRequestHandler
import threading

# Add proto directory to path
proto_dir = os.path.join(os.path.dirname(__file__), 'proto')
if proto_dir not in sys.path:
    sys.path.insert(0, proto_dir)

import grpc
from google.protobuf.timestamp_pb2 import Timestamp

try:
    import system_info_pb2
    import system_info_pb2_grpc
except ImportError:
    import system_info_pb2
    import system_info_pb2_grpc

class DeviceProfile(Enum):
    TWINCAT_IPC = "Beckhoff TwinCAT 3 IPC"
    SIMATIC_IPC = "Siemens SIMATIC IPC477E"
    LINUX_EDGE = "Dell OptiPlex Edge Terminal"
    ROBOT_CELL = "Fanuc Robot Controller"
    VISION_SENSOR = "Cognex In-Sight 9000"

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

    def generate_telemetry(self, fault_rate: float = 0.0):
        self.tick_count += 1
        now = datetime.datetime.now(datetime.timezone.utc)
        ts = Timestamp()
        ts.FromDatetime(now)

        # Sine wave load with random jitter
        jitter = random.uniform(-3.0, 3.0)
        cpu_load = max(5.0, min(95.0, self.base_cpu + 15.0 * math.sin(self.tick_count * 0.1) + jitter))
        ram_load = max(20.0, min(90.0, self.base_ram + 5.0 * math.cos(self.tick_count * 0.05) + random.uniform(-1.0, 1.0)))
        spindle_temp = 42.0 + 12.0 * math.sin(self.tick_count * 0.08) + random.uniform(0.0, 2.0)

        # Dynamic fault injection
        inject_fault = random.random() < fault_rate or self.is_faulty
        if inject_fault:
            cpu_load = min(100.0, cpu_load + 40.0)
            spindle_temp += 30.0
            self.crc_errors += random.randint(1, 5)

        components = [
            system_info_pb2.InventoryComponent(
                name="OS & Driver Telemetry",
                technology="Heimdall Edge Probe",
                type="software",
                data_json=json.dumps({
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
                    "SpindleTemperature": f"{spindle_temp:.1f} C",
                    "CycleJitterMicroseconds": f"{random.randint(2, 18)} us",
                    "Status": "Degraded" if inject_fault else "Online"
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
    simulator = None

    def log_message(self, format, *args):
        pass

    def do_GET(self):
        if self.path == "/api/status" or self.path == "/":
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            data = {
                "active_fleet": len(self.simulator.nodes),
                "total_dispatched": self.simulator.total_dispatched,
                "total_errors": self.simulator.total_errors,
                "fault_rate": self.simulator.fault_rate,
                "nodes": [
                    {
                        "hostname": n.hostname,
                        "profile": n.profile.value,
                        "ip": n.ip_address,
                        "faulty": n.is_faulty,
                        "crc_errors": n.crc_errors,
                        "ticks": n.tick_count
                    }
                    for n in self.simulator.nodes[:20]
                ]
            }
            self.wfile.write(json.dumps(data).encode("utf-8"))
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(length).decode("utf-8") if length > 0 else "{}"
        try:
            payload = json.loads(body)
        except Exception:
            payload = {}

        if self.path == "/api/fault":
            target_hostname = payload.get("hostname")
            matched = None
            if target_hostname:
                for n in self.simulator.nodes:
                    if n.hostname.upper() == target_hostname.upper():
                        matched = n
                        break
            if not matched and self.simulator.nodes:
                matched = random.choice(self.simulator.nodes)

            if matched:
                matched.is_faulty = True
                matched.crc_errors += 25
                try:
                    with grpc.insecure_channel(self.simulator.grpc_host) as channel:
                        stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
                        self.simulator.send_node_heartbeat(stub, matched)
                except Exception:
                    pass

            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(json.dumps({
                "success": True,
                "target": matched.hostname if matched else None,
                "message": f"Fault injected into {matched.hostname if matched else 'none'}"
            }).encode("utf-8"))

        elif self.path == "/api/clear-faults":
            for n in self.simulator.nodes:
                n.is_faulty = False
                n.crc_errors = 0
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(json.dumps({"success": True, "message": "All faults cleared"}).encode("utf-8"))

        elif self.path == "/api/rate":
            new_rate = payload.get("fault_rate")
            if new_rate is not None:
                self.simulator.fault_rate = float(new_rate)
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(json.dumps({"success": True, "fault_rate": self.simulator.fault_rate}).encode("utf-8"))
        else:
            self.send_response(404)
            self.end_headers()

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

def start_control_server(simulator, port: int = 5055):
    SimulatorHttpHandler.simulator = simulator
    server = HTTPServer(("0.0.0.0", port), SimulatorHttpHandler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    print(f"  Interactive Simulator HTTP API listening on port {port} (endpoints: /api/status, /api/fault, /api/clear-faults)")

class IndustrialFleetSimulator:
    def __init__(self, grpc_host: str = "localhost:5001", csv_path: str = None, fault_rate: float = 0.0, client: str = None):
        self.grpc_host = grpc_host
        self.fault_rate = fault_rate
        self.client = client
        self.csv_path = csv_path or os.path.join(os.path.dirname(__file__), '../../seed_data/inventory_seed.csv')
        self.nodes = self.load_nodes()
        if self.client:
            matched = [n for n in self.nodes if n.hostname.upper() == self.client.upper()]
            if matched:
                self.nodes = matched
            else:
                profile = DeviceProfile.ROBOT_CELL if "ROBOT" in self.client.upper() else \
                          DeviceProfile.SIMATIC_IPC if "ASSEMBLY" in self.client.upper() else \
                          DeviceProfile.TWINCAT_IPC
                self.nodes = [
                    IndustrialDeviceNode(
                        hostname=self.client,
                        machine_identifier=f"ID-{self.client}",
                        mac_address="02:AA:BB:CC:DD:01",
                        ip_address="10.0.1.50",
                        profile=profile,
                        os_name="Windows 10 IoT"
                    )
                ]
        self.total_dispatched = 0
        self.total_errors = 0

    def load_nodes(self):
        nodes = []
        try:
            from dataset_loader import get_all_client_pcs
            plant_pcs = get_all_client_pcs()
            for pc in plant_pcs:
                mt = pc.get('machineType', '')
                profile = DeviceProfile.ROBOT_CELL if mt == 'Manipulator' else \
                          DeviceProfile.VISION_SENSOR if mt == 'Automatic Optical Inspection' else \
                          DeviceProfile.TWINCAT_IPC if 'Beckhoff' in str(pc) else \
                          DeviceProfile.SIMATIC_IPC
                nodes.append(IndustrialDeviceNode(
                    hostname=pc.get('hostname'),
                    machine_identifier=pc.get('machineIdentifier'),
                    mac_address=pc.get('macAddress'),
                    ip_address=pc.get('ipAddress'),
                    profile=profile,
                    os_name=pc.get('osVersion', 'Windows 10 IoT Enterprise')
                ))
            if nodes:
                return nodes
        except Exception as e:
            pass

        if os.path.exists(self.csv_path):
            with open(self.csv_path, mode='r', encoding='utf-8') as f:
                reader = csv.DictReader(f)
                for row in reader:
                    if row['Type'] == 'ClientPc':
                        item_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name']))
                        mac = f"02:{item_id[0:2]}:{item_id[2:4]}:{item_id[4:6]}:{item_id[6:8]}:{item_id[9:11]}".upper()
                        meta = json.loads(row['Metadata']) if row['Metadata'] else {}
                        os_name = meta.get("OperatingSystem", "Windows 10 IoT")
                        
                        profile = DeviceProfile.TWINCAT_IPC if "Beckhoff" in row['Manufacturer'] else \
                                  DeviceProfile.SIMATIC_IPC if "Siemens" in row['Manufacturer'] else \
                                  DeviceProfile.LINUX_EDGE

                        nodes.append(IndustrialDeviceNode(
                            hostname=row['ClientPcHostname'] or row['Name'],
                            machine_identifier=f"ID-{item_id[:8]}",
                            mac_address=mac,
                            ip_address=meta.get("IPAddress", "10.0.1.10"),
                            profile=profile,
                            os_name=os_name
                        ))
        if not nodes:
            # Fallback synthetic nodes
            for i in range(1, 51):
                nodes.append(IndustrialDeviceNode(
                    hostname=f"CPC-{i:03d}",
                    machine_identifier=f"ID-CPC{i:03d}",
                    mac_address=f"02:AA:BB:{i//256:02X}:{i%256:02X}:01",
                    ip_address=f"10.0.1.{i}",
                    profile=DeviceProfile.TWINCAT_IPC if i % 2 == 0 else DeviceProfile.SIMATIC_IPC,
                    os_name="Windows 10 IoT"
                ))
        return nodes

    def send_node_heartbeat(self, stub, node: IndustrialDeviceNode):
        try:
            req = node.generate_telemetry(self.fault_rate)
            resp = stub.ReportSystemInfo(req, timeout=5.0)
            self.total_dispatched += 1
            return resp.success
        except Exception:
            self.total_errors += 1
            return False

    def run_smoke_test(self, count: int = 10):
        print(f"Running simulator smoke test against {self.grpc_host} (Count: {count})...")
        sample_nodes = self.nodes[:count]
        success_count = 0
        with grpc.insecure_channel(self.grpc_host) as channel:
            stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
            for node in sample_nodes:
                ok = self.send_node_heartbeat(stub, node)
                if ok:
                    success_count += 1
        print(f"Smoke Test Result: {success_count}/{len(sample_nodes)} heartbeats acknowledged.")
        return success_count == len(sample_nodes)

    def run_continuous(self, max_workers: int = 50):
        start_control_server(self, port=int(os.environ.get("HTTP_PORT", "5055")))
        print("==================================================================")
        print(f"  Heimdall Edge Fleet Simulator - Active Fleet: {len(self.nodes)} Nodes")
        print(f"  Target gRPC Endpoint: {self.grpc_host} | Fault Rate: {self.fault_rate*100:.1f}%")
        print("==================================================================")

        def worker_loop(node):
            with grpc.insecure_channel(self.grpc_host) as channel:
                stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
                while True:
                    self.send_node_heartbeat(stub, node)
                    time.sleep(random.uniform(6.0, 15.0))

        with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as executor:
            for node in self.nodes:
                executor.submit(worker_loop, node)

            start_time = time.time()
            try:
                while True:
                    time.sleep(2)
                    elapsed = time.time() - start_time
                    rate = self.total_dispatched / max(1.0, elapsed)
                    sys.stdout.write(f"\r[Fleet Live Monitor] Dispatched: {self.total_dispatched} msgs | Rate: {rate:.1f} msg/s | Errors: {self.total_errors} | Active Fleet: {len(self.nodes)} nodes")
                    sys.stdout.flush()
            except KeyboardInterrupt:
                print("\nStopping Edge Fleet Simulator...")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Heimdall Edge Fleet Simulator")
    parser.add_argument("--grpc-host", default=os.environ.get("GRPC_HOST", "localhost:5001"), help="Target gRPC host:port")
    parser.add_argument("--client", type=str, default=None, help="Target specific client node to simulate")
    parser.add_argument("--fault-rate", type=float, default=0.02, help="Simulated anomaly/fault rate (0.0 to 1.0)")
    parser.add_argument("--smoke-test", action="store_true", help="Run a quick smoke test and exit")
    parser.add_argument("--count", type=int, default=10, help="Number of nodes for smoke test")
    args = parser.parse_args()

    simulator = IndustrialFleetSimulator(grpc_host=args.grpc_host, fault_rate=args.fault_rate, client=args.client)
    if args.smoke_test:
        success = simulator.run_smoke_test(count=args.count)
        sys.exit(0 if success else 1)
    else:
        simulator.run_continuous()
