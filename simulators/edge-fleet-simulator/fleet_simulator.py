#!/usr/bin/env python3
"""
Heimdall Industrial Edge Fleet Simulator (V1)
Simulates heterogeneous industrial IPCs, Soft-PLCs (Beckhoff TwinCAT ADS),
Siemens SIMATIC controllers, robot cells, and vision sensors.
Supports live streaming gRPC telemetry, dynamic fault injection, and terminal monitoring.
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
    # If not found in proto_dir, fallback to current dir
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

class IndustrialFleetSimulator:
    def __init__(self, grpc_host: str = "localhost:5001", csv_path: str = None, fault_rate: float = 0.0):
        self.grpc_host = grpc_host
        self.fault_rate = fault_rate
        self.csv_path = csv_path or os.path.join(os.path.dirname(__file__), '../../seed_data/inventory_seed.csv')
        self.nodes = self.load_nodes()
        self.total_dispatched = 0
        self.total_errors = 0

    def load_nodes(self):
        nodes = []
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
    parser.add_argument("--fault-rate", type=float, default=0.02, help="Simulated anomaly/fault rate (0.0 to 1.0)")
    parser.add_argument("--smoke-test", action="store_true", help="Run a quick smoke test and exit")
    parser.add_argument("--count", type=int, default=10, help="Number of nodes for smoke test")
    args = parser.parse_args()

    simulator = IndustrialFleetSimulator(grpc_host=args.grpc_host, fault_rate=args.fault_rate)
    if args.smoke_test:
        success = simulator.run_smoke_test(count=args.count)
        sys.exit(0 if success else 1)
    else:
        simulator.run_continuous()
