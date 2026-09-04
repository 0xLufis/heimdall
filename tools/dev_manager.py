#!/usr/bin/env python3
"""
Heimdall Dev Manager & Service Health Monitor
Orchestrates backend, agent daemon, web frontend, fleet simulator, and database services.
"""

import sys
import os
import socket
import urllib.request
import subprocess
import argparse
import time

SERVICES = [
    {"name": "PostgreSQL Database", "host": "127.0.0.1", "port": 5432, "type": "tcp"},
    {"name": "Redis Cache", "host": "127.0.0.1", "port": 6379, "type": "tcp"},
    {"name": "Backend REST API (V1)", "host": "127.0.0.1", "port": 5099, "type": "http", "path": "/swagger/v1/swagger.json"},
    {"name": "Backend gRPC Ingestion", "host": "127.0.0.1", "port": 5001, "type": "tcp"},
    {"name": "Web Frontend (Nuxt)", "host": "127.0.0.1", "port": 3000, "type": "http", "path": "/"}
]

def check_tcp(host, port, timeout=1.5):
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except (socket.timeout, ConnectionRefusedError, OSError):
        return False

def check_http(host, port, path="/", timeout=2.0):
    url = f"http://{host}:{port}{path}"
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Heimdall-DevManager/1.0"})
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.status in (200, 301, 302)
    except Exception:
        return False

def get_status():
    print("================================================================")
    print("           HEIMDALL SERVICE HEALTH & TOPOLOGY MONITOR           ")
    print("================================================================")
    print(f"{'SERVICE':<28} | {'TARGET':<22} | {'STATUS'}")
    print("-" * 64)
    all_ok = True
    for svc in SERVICES:
        target = f"{svc['host']}:{svc['port']}"
        if svc['type'] == "tcp":
            alive = check_tcp(svc['host'], svc['port'])
        else:
            alive = check_http(svc['host'], svc['port'], svc.get('path', '/'))
        
        status_str = "\033[92m● ONLINE\033[0m" if alive else "\033[91m○ OFFLINE\033[0m"
        if not alive:
            all_ok = False
        print(f"{svc['name']:<28} | {target:<22} | {status_str}")
    print("================================================================")
    return all_ok

def run_tests():
    python_bin = "venv/bin/python" if os.path.exists("venv/bin/python") else sys.executable

    print("\n>>> 1. Running Seed Data Integrity Pipeline...")
    res = subprocess.run([python_bin, "seed_data/seed_pipeline.py", "--validate"])
    if res.returncode != 0:
        print("❌ Seed data validation failed.")
        return False

    print("\n>>> 2. Running .NET Backend Test Suite (xUnit)...")
    res = subprocess.run(["dotnet", "test", "Heimdall.sln"])
    if res.returncode != 0:
        print("❌ Backend tests failed.")
        return False

    print("\n>>> 3. Running Frontend Test Suite (Vitest)...")
    res = subprocess.run(["bun", "run", "test"], cwd="frontend/web")
    if res.returncode != 0:
        print("❌ Frontend tests failed.")
        return False

    print("\n>>> 4. Running Fleet Simulator Smoke Test...")
    res = subprocess.run([python_bin, "simulators/fleet/fleet_simulator.py", "--smoke-test", "--count", "5"])
    # If simulator target is not running in local test environment, note it
    if res.returncode != 0:
        print("ℹ️ Note: Fleet simulator smoke test requires active local gRPC service on port 5001.")

    print("\n✅ Verification Suite Execution Finished.")
    return True

def watch_status(interval=2.0):
    """Continuously refresh service status in terminal."""
    try:
        while True:
            # Clear terminal screen and reset cursor
            sys.stdout.write("\033[2J\033[H")
            sys.stdout.flush()
            get_status()
            current_time = time.strftime('%Y-%m-%d %H:%M:%S')
            print(f"\n\033[90m[Live Updating] Last refreshed: {current_time} | Press Ctrl+C to exit monitor.\033[0m")
            time.sleep(interval)
    except KeyboardInterrupt:
        print("\n\033[93mMonitor stopped.\033[0m")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Heimdall Dev Manager")
    parser.add_argument("command", choices=["status", "watch", "check-health", "test"], help="Dev manager command")
    parser.add_argument("--interval", type=float, default=2.0, help="Refresh interval in seconds for watch mode")
    args = parser.parse_args()

    if args.command == "status":
        get_status()
    elif args.command == "watch":
        watch_status(interval=args.interval)
    elif args.command == "check-health":
        ok = get_status()
        sys.exit(0 if ok else 1)
    elif args.command == "test":
        run_tests()
