#!/usr/bin/env python3
import grpc
import time
import random
import datetime
import sys
import json
import os
import csv
import uuid
import concurrent.futures
from google.protobuf.timestamp_pb2 import Timestamp

try:
    import system_info_pb2
    import system_info_pb2_grpc
except ImportError:
    # If not in path, try to import from the root directory
    sys.path.append(os.getcwd())
    import system_info_pb2
    import system_info_pb2_grpc

# Configuration
GRPC_HOST = os.environ.get('GRPC_HOST', 'localhost:5001')
CSV_PATH = 'seed_data/inventory_seed.csv'

def load_clients_from_csv():
    clients = []
    if not os.path.exists(CSV_PATH):
        print(f"Warning: {CSV_PATH} not found, generating virtual client fleet...")
        for i in range(1, 501):
            clients.append({
                "hostname": f"CPC-{i:03d}",
                "machine_identifier": f"ID-CPC{i:03d}",
                "mac_address": f"02:65:54:{i//256:02X}:{i%256:02X}:FC",
                "os": "Windows 10 IoT Enterprise" if i % 2 == 0 else "Ubuntu Linux 24.04 LTS",
                "ip": f"10.0.{(i // 250) + 1}.{(i % 250) + 1}",
                "security_level": "High" if i % 5 == 0 else "Standard"
            })
        return clients

    with open(CSV_PATH, mode='r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row['Type'] == 'ClientPc':
                item_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name']))
                mac = f"02:{item_id[0:2]}:{item_id[2:4]}:{item_id[4:6]}:{item_id[6:8]}:{item_id[9:11]}".upper()
                metadata = json.loads(row['Metadata']) if row['Metadata'] else {}
                
                clients.append({
                    "hostname": row['ClientPcHostname'] or row['Name'],
                    "machine_identifier": f"ID-{item_id[:8]}",
                    "mac_address": mac,
                    "os": metadata.get("OS", "Windows 10 IoT"),
                    "ip": metadata.get("IP", "10.0.1.10"),
                    "security_level": metadata.get("SecurityLevel", "Standard")
                })
    return clients

def send_client_heartbeat(stub, client):
    """Sends a single system info heartbeat via gRPC."""
    try:
        now = datetime.datetime.now(datetime.timezone.utc)
        ts = Timestamp()
        ts.FromDatetime(now)
        
        cpu_load = random.uniform(8.0, 75.0)
        ram_load = random.uniform(25.0, 80.0)
        
        req = system_info_pb2.SystemInfoRequest(
            hostname=client["hostname"],
            machine_identifier=client["machine_identifier"],
            mac_address=client["mac_address"],
            last_online=ts,
            components=[
                system_info_pb2.InventoryComponent(
                    name="OS Environment",
                    technology="Agent",
                    type="software",
                    data_json=json.dumps({
                        "OsVersion": client["os"],
                        "IPAddress": client["ip"],
                        "SecurityLevel": client["security_level"],
                        "UpdateStatus": "Current"
                    })
                ),
                system_info_pb2.InventoryComponent(
                    name="Live Telemetry",
                    technology="Agent",
                    type="telemetry",
                    data_json=json.dumps({
                        "CpuLoad": f"{cpu_load:.1f}%",
                        "RamUsage": f"{ram_load:.1f}%",
                        "Uptime": f"{random.randint(24, 720)}h",
                        "Status": "Online"
                    })
                )
            ],
            disk_info=system_info_pb2.DiskInfo(
                total_free_gb=random.uniform(60.0, 250.0),
                os_drive_free_gb=random.uniform(15.0, 60.0),
                drives={"C:": 45.2, "D:": 85.3} if "Windows" in client["os"] else {"/": 45.2, "/var": 85.3}
            )
        )
        stub.ReportSystemInfo(req)
        return True
    except Exception as e:
        return False

def client_worker_loop(client):
    """Worker loop per client node."""
    try:
        with grpc.insecure_channel(GRPC_HOST) as channel:
            stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
            # Send initial heartbeat immediately
            send_client_heartbeat(stub, client)
            
            while True:
                time.sleep(random.randint(8, 20))
                send_client_heartbeat(stub, client)
    except Exception as e:
        print(f"[{client['hostname']}] Worker exited: {e}")

def main():
    clients = load_clients_from_csv()
    print(f"=== Heimdall Edge PC Simulator Started ===")
    print(f"Target gRPC Server: {GRPC_HOST}")
    print(f"Simulating {len(clients)} Industrial PCs concurrently...")

    # Fast initial burst dispatch using thread pool
    print("Dispatching initial parallel heartbeat burst across all edge nodes...")
    with grpc.insecure_channel(GRPC_HOST) as channel:
        stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
        with concurrent.futures.ThreadPoolExecutor(max_workers=50) as executor:
            futures = [executor.submit(send_client_heartbeat, stub, c) for c in clients]
            concurrent.futures.wait(futures)
    print("Initial fleet heartbeat burst complete. Starting streaming background loops...")

    # Launch background simulator threads
    with concurrent.futures.ThreadPoolExecutor(max_workers=100) as executor:
        for client in clients:
            executor.submit(client_worker_loop, client)
        
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\nShutting down PC simulators...")

if __name__ == "__main__":
    main()
