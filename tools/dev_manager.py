#!/usr/bin/env python3
"""
Heimdall Dev Manager & Service Health Monitor
Orchestrates backend, agent daemon, web frontend, fleet simulator, Windows agent, and database services.
"""

import sys
import os
import socket
import urllib.request
import subprocess
import argparse
import time
import json

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

CORE_SERVICES = [
    {"name": "PostgreSQL Database", "host": "127.0.0.1", "port": 5432, "type": "tcp", "cat": "Data Storage"},
    {"name": "Redis Spool Cache", "host": "127.0.0.1", "port": 6379, "type": "tcp", "cat": "Data Storage"},
    {"name": "Backend REST API (V1)", "host": "127.0.0.1", "port": 5099, "type": "http", "path": "/swagger/v1/swagger.json", "cat": "Application"},
    {"name": "Backend gRPC Ingestion", "host": "127.0.0.1", "port": 5001, "type": "tcp", "cat": "Application"},
    {"name": "Web Frontend (Nuxt 4)", "host": "127.0.0.1", "port": 3000, "type": "http", "path": "/", "cat": "Application"},
    {"name": "Edge Agent Config API", "host": "127.0.0.1", "port": 5998, "type": "tcp", "cat": "Edge Node"},
    {"name": "Edge Fleet Simulator", "host": "127.0.0.1", "port": 5055, "type": "tcp", "cat": "Simulation"},
]

WINDOWS_SERVICES = [
    {"name": "Windows Web VNC Viewer", "host": "127.0.0.1", "port": 8006, "type": "tcp", "cat": "Windows Container"},
    {"name": "TwinCAT ADS Server", "host": "127.0.0.1", "port": 48898, "type": "tcp", "cat": "OT Protocol"},
    {"name": "Minimal OPC UA Server", "host": "127.0.0.1", "port": 4840, "type": "tcp", "cat": "OT Protocol"},
    {"name": "WinRM Management HTTP", "host": "127.0.0.1", "port": 5985, "type": "tcp", "cat": "Windows Container"},
    {"name": "RDP Remote Desktop", "host": "127.0.0.1", "port": 3389, "type": "tcp", "cat": "Windows Container"},
]


def check_tcp(host, port, timeout=1.0):
    t0 = time.time()
    try:
        with socket.create_connection((host, int(port)), timeout=timeout):
            return True, round((time.time() - t0) * 1000, 1)
    except (socket.timeout, ConnectionRefusedError, OSError):
        return False, 0.0


def check_http(host, port, path="/", timeout=1.5):
    t0 = time.time()
    url = f"http://{host}:{port}{path}"
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Heimdall-DevManager/1.0"})
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            ok = resp.status in (200, 301, 302)
            return ok, round((time.time() - t0) * 1000, 1)
    except Exception:
        return False, 0.0


def get_status(include_windows=True, output_json=False):
    services = list(CORE_SERVICES)
    if include_windows:
        services.extend(WINDOWS_SERVICES)

    results = []
    all_ok = True

    for svc in services:
        if svc["type"] == "tcp":
            alive, lat = check_tcp(svc["host"], svc["port"])
        else:
            alive, lat = check_http(svc["host"], svc["port"], svc.get("path", "/"))

        if not alive and svc not in WINDOWS_SERVICES:
            all_ok = False

        results.append({
            "name": svc["name"],
            "category": svc.get("cat", "General"),
            "host": svc["host"],
            "port": svc["port"],
            "alive": alive,
            "latency_ms": lat
        })

    if output_json:
        print(json.dumps(results, indent=2))
        return all_ok

    print("=========================================================================")
    print("              HEIMDALL INDUSTRIAL OT SERVICE TOPOLOGY MONITOR           ")
    print("=========================================================================")
    print(f"{'CATEGORY':<16} | {'SERVICE':<26} | {'TARGET':<16} | {'LATENCY':<8} | {'STATUS'}")
    print("-" * 81)

    for item in results:
        target = f"{item['host']}:{item['port']}"
        lat_str = f"{item['latency_ms']}ms" if item['alive'] else "---"
        status_str = "\033[92m● ONLINE\033[0m" if item['alive'] else "\033[91m○ OFFLINE\033[0m"
        print(f"{item['category']:<16} | {item['name']:<26} | {target:<16} | {lat_str:<8} | {status_str}")

    print("=========================================================================")
    return all_ok


def watch_status(interval=2.0, include_windows=True):
    """Continuously refresh service status in terminal."""
    try:
        while True:
            sys.stdout.write("\033[2J\033[H")
            sys.stdout.flush()
            get_status(include_windows=include_windows)
            current_time = time.strftime("%Y-%m-%d %H:%M:%S")
            print(f"\n\033[90m[Live Updating] Refreshed at {current_time} | Press Ctrl+C to exit monitor.\033[0m")
            time.sleep(interval)
    except KeyboardInterrupt:
        print("\n\033[93mMonitor stopped.\033[0m")


def run_tests():
    python_bin = "venv/bin/python" if os.path.exists("venv/bin/python") else sys.executable

    print("\n>>> 1. Running Seed Data Integrity Pipeline...")
    res = subprocess.run([python_bin, "seed_data/seed_pipeline.py", "--validate"], cwd=ROOT_DIR)
    if res.returncode != 0:
        print("❌ Seed data validation failed.")
        return False

    print("\n>>> 2. Running .NET Backend Test Suite (xUnit, 162 tests)...")
    res = subprocess.run(["dotnet", "test", "Heimdall.sln"], cwd=ROOT_DIR)
    if res.returncode != 0:
        print("❌ Backend tests failed.")
        return False

    print("\n>>> 3. Running Frontend Test Suite (Vitest, 275 tests)...")
    res = subprocess.run(["bun", "run", "test"], cwd=os.path.join(ROOT_DIR, "frontend", "web"))
    if res.returncode != 0:
        print("❌ Frontend tests failed.")
        return False

    print("\n>>> 4. Running Fleet Simulator Smoke Test...")
    res = subprocess.run([python_bin, "simulators/fleet/fleet_simulator.py", "--smoke-test", "--count", "5"], cwd=ROOT_DIR)
    if res.returncode != 0:
        print("ℹ️ Note: Fleet simulator smoke test requires active local gRPC service on port 5001.")

    print("\n✅ Verification Suite Execution Finished.")
    return True


def run_windows_command(subcmd):
    script = os.path.join(ROOT_DIR, "tools", "test_windows_agent.py")
    if subcmd in ("start", "up"):
        subprocess.run([sys.executable, script, "--up"])
    elif subcmd in ("stop", "down"):
        subprocess.run([sys.executable, script, "--down"])
    elif subcmd == "restart":
        subprocess.run([sys.executable, script, "--down"])
        time.sleep(1)
        subprocess.run([sys.executable, script, "--up"])
    elif subcmd == "status":
        subprocess.run([sys.executable, script, "--status"])
    elif subcmd == "build":
        subprocess.run([sys.executable, script, "--build-agent"])
        subprocess.run([sys.executable, script, "--build-image"])
    elif subcmd == "test":
        subprocess.run([sys.executable, script, "--test"])
    elif subcmd == "launch":
        launch_script = os.path.join(ROOT_DIR, "tools", "launch_agent_win.py")
        subprocess.run([sys.executable, launch_script])
    else:
        print(f"Unknown windows subcommand: {subcmd}")
        print("Available: start, stop, restart, status, build, test, launch")


def main():
    parser = argparse.ArgumentParser(description="Heimdall Dev Manager & Service Health Monitor")
    parser.add_argument("command", nargs="?", default="status",
                        choices=["status", "watch", "check-health", "test", "test-windows", "tui", "windows"],
                        help="Dev manager command")
    parser.add_argument("subcommand", nargs="?", default="status", help="Subcommand for windows command")
    parser.add_argument("--interval", type=float, default=2.0, help="Refresh interval in seconds for watch mode")
    parser.add_argument("--json", action="store_true", help="Output status in JSON format")
    parser.add_argument("--no-windows", action="store_true", help="Exclude Windows container services from check")

    args = parser.parse_args()

    if args.command == "status":
        get_status(include_windows=not args.no_windows, output_json=args.json)
    elif args.command in ("watch", "monitor"):
        watch_status(interval=args.interval, include_windows=not args.no_windows)
    elif args.command == "check-health":
        ok = get_status(include_windows=False, output_json=args.json)
        sys.exit(0 if ok else 1)
    elif args.command == "test":
        run_tests()
    elif args.command == "test-windows":
        run_windows_command("test")
    elif args.command == "windows":
        run_windows_command(args.subcommand)
    elif args.command == "tui":
        from tools.tui import main as run_tui
        run_tui()


if __name__ == "__main__":
    main()
