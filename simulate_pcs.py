import grpc
import time
import random
import datetime
import argparse
import sys
import json
import os
import csv
import uuid
import threading
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
        print(f"Error: {CSV_PATH} not found.")
        return clients

    with open(CSV_PATH, mode='r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row['Type'] == 'ClientPc':
                # Replicate the UUID logic from incremental_seed.py
                item_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name']))
                mac = f"02:{item_id[0:2]}:{item_id[2:4]}:{item_id[4:6]}:{item_id[6:8]}:{item_id[9:11]}".upper()
                
                metadata = json.loads(row['Metadata']) if row['Metadata'] else {}
                
                clients.append({
                    "hostname": row['ClientPcHostname'] or row['Name'],
                    "machine_identifier": f"ID-{item_id[:8]}",
                    "mac_address": mac,
                    "os": metadata.get("OS", "Windows 10 IoT"),
                    "ip": metadata.get("IP", "127.0.0.1"),
                    "security_level": metadata.get("SecurityLevel", "Standard")
                })
    return clients

def simulate_client_loop(client):
    """Loop for a single client in its own thread."""
    print(f"Starting simulator for {client['hostname']} ({client['mac_address']})")
    
    # We use a persistent channel per thread for better performance
    with grpc.insecure_channel(GRPC_HOST) as channel:
        stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
        
        while True:
            try:
                now = datetime.datetime.now(datetime.timezone.utc)
                ts = Timestamp()
                ts.FromDatetime(now)
                
                # Dynamic performance data
                cpu_load = random.uniform(5.0, 85.0)
                ram_load = random.uniform(20.0, 90.0)
                
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
                                "Uptime": f"{random.randint(10, 1000)}h"
                            })
                        )
                    ],
                    disk_info=system_info_pb2.DiskInfo(
                        total_free_gb=random.uniform(50.0, 200.0),
                        os_drive_free_gb=random.uniform(10.0, 50.0),
                        drives={"C:": 45.2, "D:": 75.3} if "Windows" in client["os"] else {"/": 45.2, "/var": 75.3}
                    )
                )
                
                resp = stub.ReportSystemInfo(req)
                # Success output is a bit noisy for 20+ clients, so we only print failures or periodic heartbeats
            except Exception as e:
                print(f"[{client['hostname']}] Error: {e}")
            
            # Randomized interval 15-45 seconds
            time.sleep(random.randint(15, 45))

def start_multi_simulator():
    clients = load_clients_from_csv()
    if not clients:
        print("No clients found to simulate.")
        return

    print(f"--- Launching simulation for {len(clients)} PCs ---")
    threads = []
    for client in clients:
        t = threading.Thread(target=simulate_client_loop, args=(client,), daemon=True)
        t.start()
        threads.append(t)
        # Stagger start times slightly
        time.sleep(0.5)

    print("All simulators running in background. Press Ctrl+C to exit.")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\nShutting down simulator...")

if __name__ == "__main__":
    start_multi_simulator()
