import subprocess
import json
import time
import grpc
import os
import platform
import datetime
from google.protobuf.timestamp_pb2 import Timestamp

import system_info_pb2
import system_info_pb2_grpc

GRPC_HOST = os.environ.get('GRPC_HOST', 'localhost:5001')

def run_command(command):
    """Utility to run a shell command and return its output."""
    try:
        # Check if we're actually on Windows before running wmic
        if platform.system() != 'Windows':
            return f"Mocked Output: {command}"
            
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, shell=True)
        return result.stdout.strip()
    except Exception as e:
        return f"Error executing {command}: {e}"

def get_wmic_data():
    """Gathers data using Windows Management Instrumentation Command-line (WMIC)."""
    data = {}
    
    # OS Information
    os_info = run_command('wmic os get Caption,Version /value')
    data['os'] = os_info
    
    # CPU Information
    cpu_info = run_command('wmic cpu get Name,NumberOfCores,MaxClockSpeed /value')
    data['cpu'] = cpu_info
    
    # RAM Information
    ram_info = run_command('wmic memorychip get Capacity /value')
    data['ram'] = ram_info
    
    # Disk Information
    disk_info = run_command('wmic logicaldisk get Caption,FreeSpace,Size /value')
    data['disk'] = disk_info
    
    return data

def simulate_windows_10_pc():
    """Simulates a Windows 10 PC by reporting to the backend."""
    hostname = "WIN10-SIM-01"
    mac_address = "00:14:22:01:23:45"
    machine_id = "UUID-WIN10-0001"

    print(f"Connecting to Heimdall Backend at {GRPC_HOST}...")
    
    with grpc.insecure_channel(GRPC_HOST) as channel:
        stub = system_info_pb2_grpc.SystemInfoCollectorStub(channel)
        
        while True:
            # Gather fresh WMIC data
            wmic_data = get_wmic_data()
            
            now = datetime.datetime.now(datetime.timezone.utc)
            ts = Timestamp()
            ts.FromDatetime(now)
            
            # Construct the request
            req = system_info_pb2.SystemInfoRequest(
                hostname=hostname,
                machine_identifier=machine_id,
                mac_address=mac_address,
                last_online=ts,
                disk_info=system_info_pb2.DiskInfo(
                    total_free_gb=500.0,
                    os_drive_free_gb=250.0,
                    drives={"C:": 250.0, "D:": 250.0}
                ),
                components=[
                    system_info_pb2.InventoryComponent(
                        name="Hardware",
                        technology="WMIC Instrumentation",
                        type="hardware",
                        data_json=json.dumps({
                            "Cpu": "Intel Core i9-13900K",
                            "Ram": "64 GB",
                            "Storage": "1 TB NVMe",
                            "WmicRaw": wmic_data['cpu']
                        })
                    ),
                    system_info_pb2.InventoryComponent(
                        name="Software",
                        technology="WMIC Instrumentation",
                        type="software",
                        data_json=json.dumps({
                            "OsVersion": "Windows 10 Pro",
                            "InstalledPackages": ["Office 365", "Visual Studio 2022", "Docker Desktop"],
                            "WmicRaw": wmic_data['os']
                        })
                    )
                ]
            )
            
            try:
                resp = stub.ReportSystemInfo(req)
                print(f"[{now.strftime('%H:%M:%S')}] Sent WMIC data for {hostname} -> Success: {resp.success}")
            except Exception as e:
                print(f"Failed to send WMIC data: {e}")
                
            time.sleep(30)

if __name__ == "__main__":
    simulate_windows_10_pc()
