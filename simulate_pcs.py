import grpc
import time
import random
import datetime
import argparse
import sys
from google.protobuf.timestamp_pb2 import Timestamp

import system_info_pb2
import system_info_pb2_grpc

# Configuration
GRPC_HOST = 'localhost:5001'

# Sample data for simulation
CLIENTS = {
    f"ROBOT-CELL-{i:02d}": {
        "hostname": f"ROBOT-CELL-{i:02d}",
        "machine_identifier": f"UUID-ROBOT-{i:04d}",
        "mac_address": f"00:1A:2B:3C:4D:{i:02d}",
        "cpu": "Intel Core i7-12700K",
        "ram": "32 GB",
        "storage": "512 GB NVMe",
        "os": "Windows 10 IoT Enterprise",
        "packages": ["KUKA System Software", "VLC", "Chrome"]
    }
    for i in range(1, 6)
}
CLIENTS.update({
    f"ASSEMBLY-ST-{i:02d}": {
        "hostname": f"ASSEMBLY-ST-{i:02d}",
        "machine_identifier": f"UUID-ASSM-{i:04d}",
        "mac_address": f"00:1A:2B:3C:5D:{i:02d}",
        "cpu": "Intel Core i5-11400",
        "ram": "16 GB",
        "storage": "256 GB SSD",
        "os": "Ubuntu 22.04 LTS",
        "packages": ["Docker", "Python 3.10", "Node.js"]
    }
    for i in range(1, 6)
})

def simulate_single_client(client_hostname: str):
    """Simulates a single client reporting its status periodically."""
    if client_hostname not in CLIENTS:
        print(f"Error: Client with hostname '{client_hostname}' not found.", file=sys.stderr)
        sys.exit(1)

    client = CLIENTS[client_hostname]
    print(f"Simulating agent for '{client['hostname']}'. Connecting to Heimdall Backend at {GRPC_HOST}...")

    while True:
        try:
            with grpc.insecure_channel(GRPC_HOST) as channel:
                stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
                
                now = datetime.datetime.utcnow()
                ts = Timestamp()
                ts.FromDatetime(now)
                
                req = system_info_pb2.SystemInfoRequest(
                    hostname=client["hostname"],
                    machine_identifier=client["machine_identifier"],
                    mac_address=client["mac_address"],
                    last_online=ts,
                    hardware_config=system_info_pb2.HardwareConfig(
                        cpu=client["cpu"],
                        ram=client["ram"],
                        storage=client["storage"]
                    ),
                    software_config=system_info_pb2.SoftwareConfig(
                        os_version=client["os"],
                        installed_packages=client["packages"]
                    )
                )
                
                resp = stub.ReportSystemInfo(req)
                print(f"[{now.strftime('%H:%M:%S')}] Sent data for {client['hostname']} -> Success: {resp.success}")

        except Exception as e:
            print(f"Failed to send data for {client['hostname']}: {e}")
        
        # Wait for a random interval before the next report to make it more realistic
        wait_time = random.randint(10, 30)
        print(f"--- Waiting {wait_time} seconds before next report... ---")
        time.sleep(wait_time)


def simulate_all_clients():
    """Original simulation logic: cycles through all clients in a single process."""
    print(f"Connecting to Heimdall Backend at {GRPC_HOST}...")
    
    while True:
        for client_hostname in CLIENTS:
            try:
                with grpc.insecure_channel(GRPC_HOST) as channel:
                    stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
                    client = CLIENTS[client_hostname]
                    
                    now = datetime.datetime.utcnow()
                    ts = Timestamp()
                    ts.FromDatetime(now)
                    
                    req = system_info_pb2.SystemInfoRequest(
                        hostname=client["hostname"],
                        machine_identifier=client["machine_identifier"],
                        mac_address=client["mac_address"],
                        last_online=ts,
                        hardware_config=system_info_pb2.HardwareConfig(
                            cpu=client["cpu"],
                            ram=client["ram"],
                            storage=client["storage"]
                        ),
                        software_config=system_info_pb2.SoftwareConfig(
                            os_version=client["os"],
                            installed_packages=client["packages"]
                        )
                    )
                    
                    resp = stub.ReportSystemInfo(req)
                    print(f"[{now.strftime('%H:%M:%S')}] Sent data for {client['hostname']} -> Success: {resp.success}")
            except Exception as e:
                print(f"Failed to send data for {client['hostname']}: {e}")
            
            # Small delay between each client report
            time.sleep(1)
            
        # Wait a bit before next full cycle
        print("--- Cycle complete. Waiting 10 seconds... ---")
        time.sleep(10)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Simulate one or more Heimdall agents.")
    parser.add_argument(
        '--client', 
        metavar='HOSTNAME',
        type=str, 
        help=f"Hostname of a single client to simulate. Available: {', '.join(CLIENTS.keys())}"
    )
    args = parser.parse_args()

    if args.client:
        simulate_single_client(args.client)
    else:
        print("Starting simulation for ALL clients in a single process.")
        print("To simulate a single client, use the --client HOSTNAME argument.")
        simulate_all_clients()
