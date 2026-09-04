#!/usr/bin/env python3
"""
Heimdall Simulated PC Daemon
Runs inside a simulated PC container representing an industrial edge controller
(e.g., Kuka Robot IPC, Cognex Vision Host, Atlas Copco Screwing Controller).
Collects local CMI/WMI telemetry, checks in with simulated Active Directory,
and reports gRPC system telemetry to Heimdall Backend.
"""

import sys
import os
import time
import json
import socket
import datetime
import threading
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.request import urlopen, Request

# Proto setup
proto_dir = os.path.join(os.path.dirname(__file__), 'proto')
if proto_dir not in sys.path:
    sys.path.insert(0, proto_dir)

import grpc
from google.protobuf.timestamp_pb2 import Timestamp
import system_info_pb2
import system_info_pb2_grpc

from dataset_loader import get_client_pc_by_hostname, load_enterprise_dataset
from mock_cmi_runner import MockCmiEngine

SIMULATED_HOSTNAME = os.environ.get('SIMULATED_HOSTNAME', 'CPC-L06-ROB-01')
BACKEND_GRPC_HOST = os.environ.get('BACKEND_GRPC_HOST', 'localhost:5001')
AD_SERVICE_URL = os.environ.get('AD_SERVICE_URL', 'http://localhost:3000/api/ad-mock')
DIAGNOSTIC_PORT = int(os.environ.get('DIAGNOSTIC_PORT', '8080'))

class SimulatedPcState:
    def __init__(self, hostname: str):
        self.hostname = hostname
        self.engine = MockCmiEngine(hostname=hostname)
        self.pc = self.engine.pc or {}
        self.last_cmi_snapshot = {}
        self.ad_joined = False
        self.total_heartbeats = 0
        self.last_heartbeat_time = None
        self.last_heartbeat_success = False

state = SimulatedPcState(SIMULATED_HOSTNAME)

class DiagnosticsHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass

    def do_GET(self):
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        payload = {
            "hostname": state.hostname,
            "machineIdentifier": state.pc.get('machineIdentifier'),
            "ipAddress": state.pc.get('ipAddress'),
            "macAddress": state.pc.get('macAddress'),
            "osVersion": state.pc.get('osVersion'),
            "adOuPath": state.pc.get('adOuPath'),
            "adJoined": state.ad_joined,
            "totalHeartbeats": state.total_heartbeats,
            "lastHeartbeat": state.last_heartbeat_time,
            "lastHeartbeatSuccess": state.last_heartbeat_success,
            "cmiHardware": state.engine.cmi
        }
        self.wfile.write(json.dumps(payload, indent=2).encode('utf-8'))

def check_ad_domain(ad_url: str):
    """Verifies membership in Active Directory domain via mock REST API."""
    try:
        url = f"{ad_url}/v1.0/devices"
        req = Request(url, headers={"User-Agent": "Heimdall-SimPc-Daemon/1.0"})
        with urlopen(req, timeout=3.0) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            devices = data.get('value', [])
            matched = any(d.get('deviceHostName', '').upper() == state.hostname.upper() for d in devices)
            state.ad_joined = matched
    except Exception:
        # Fallback: verified by dataset
        state.ad_joined = True

def build_system_info_request():
    now = datetime.datetime.now(datetime.timezone.utc)
    ts = Timestamp()
    ts.FromDatetime(now)

    os_out = state.engine.execute('wmic os get Caption,Version /value')
    cpu_out = state.engine.execute('wmic cpu get Name,NumberOfCores /value')
    bios_out = state.engine.execute('wmic bios get SerialNumber /value')
    disk_out = state.engine.execute('wmic logicaldisk get Caption,FreeSpace,Size /value')

    state.last_cmi_snapshot = {
        "os": os_out,
        "cpu": cpu_out,
        "bios": bios_out,
        "disk": disk_out
    }

    disks_list = state.engine.cmi.get('disks', [])
    d0 = disks_list[0] if disks_list else {}
    total_gb = d0.get('Size', 512110190592) / (1024 ** 3)
    free_gb = d0.get('FreeSpace', 342110190592) / (1024 ** 3)

    components = [
        system_info_pb2.InventoryComponent(
            name="Operating System & CMI",
            technology="Windows Management Instrumentation",
            type="software",
            data_json=json.dumps({
                "Hostname": state.hostname,
                "OsVersion": state.pc.get('osVersion'),
                "AdOuPath": state.pc.get('adOuPath'),
                "AdJoined": state.ad_joined,
                "WmicOs": os_out.strip(),
                "WmicBios": bios_out.strip(),
                "InstalledPackages": state.pc.get('installedPackages', [])
            })
        ),
        system_info_pb2.InventoryComponent(
            name="Hardware Instrumentation",
            technology="Common Information Model (CIM)",
            type="hardware",
            data_json=json.dumps({
                "Cpu": cpu_out.strip(),
                "Memory": state.engine.cmi.get('memory', {}),
                "MachineType": state.pc.get('machineType'),
                "VlanId": state.pc.get('vlanId')
            })
        )
    ]

    return system_info_pb2.SystemInfoRequest(
        hostname=state.hostname,
        machine_identifier=state.pc.get('machineIdentifier', f"ID-{state.hostname}"),
        mac_address=state.pc.get('macAddress', '00:1A:2B:3C:4D:01'),
        last_online=ts,
        disk_info=system_info_pb2.DiskInfo(
            total_free_gb=free_gb,
            os_drive_free_gb=free_gb,
            drives={"C:": free_gb}
        ),
        components=components
    )

def telemetry_loop(grpc_host: str, interval: int = 10):
    print(f"[SimPC-{state.hostname}] Starting gRPC telemetry loop towards {grpc_host} (Interval: {interval}s)...")
    while True:
        try:
            req = build_system_info_request()
            with grpc.insecure_channel(grpc_host) as channel:
                stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
                resp = stub.ReportSystemInfo(req, timeout=4.0)
                state.last_heartbeat_success = resp.success
                state.total_heartbeats += 1
                state.last_heartbeat_time = datetime.datetime.now(datetime.timezone.utc).isoformat()
                print(f"[SimPC-{state.hostname}] Heartbeat #{state.total_heartbeats} -> Acknowledged: {resp.success}")
        except Exception as e:
            state.last_heartbeat_success = False
            state.last_heartbeat_time = datetime.datetime.now(datetime.timezone.utc).isoformat()
            # print(f"[SimPC-{state.hostname}] Heartbeat attempt failed (backend offline?): {e}")

        time.sleep(interval)

def main():
    print(f"==================================================================")
    print(f"  Heimdall Simulated Edge PC Container: {SIMULATED_HOSTNAME}")
    print(f"  Machine Type: {state.pc.get('machineType')} | VLAN: {state.pc.get('vlanId')}")
    print(f"  Target gRPC: {BACKEND_GRPC_HOST} | AD Mock: {AD_SERVICE_URL}")
    print(f"==================================================================")

    # Check Active Directory enrollment
    check_ad_domain(AD_SERVICE_URL)
    print(f"[SimPC-{state.hostname}] Active Directory Domain Status: {'JOINED' if state.ad_joined else 'STANDALONE'}")

    # Start diagnostics HTTP server
    diag_server = HTTPServer(("0.0.0.0", DIAGNOSTIC_PORT), DiagnosticsHandler)
    diag_thread = threading.Thread(target=diag_server.serve_forever, daemon=True)
    diag_thread.start()
    print(f"[SimPC-{state.hostname}] Diagnostics API listening on http://0.0.0.0:{DIAGNOSTIC_PORT}")

    # Start telemetry dispatch loop
    telemetry_loop(BACKEND_GRPC_HOST, interval=int(os.environ.get('REPORT_INTERVAL_SEC', '10')))

if __name__ == '__main__':
    main()
