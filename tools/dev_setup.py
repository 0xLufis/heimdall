#!/usr/bin/env python3
"""
Heimdall Dev Setup & Environment Automation
Automates prerequisite verification, diagnostic port checking, database bootstrap,
migrations, and enterprise seed data initialization.
"""

import sys
import os
import shutil
import socket
import subprocess
import argparse
import time
import json
import re

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

PREREQUISITE_PORTS = [
    {"port": 3000, "service": "Nuxt 4 Web Frontend", "role": "Frontend Web Application"},
    {"port": 5000, "service": "Legacy / Alt Backend", "role": "Diagnostic Port"},
    {"port": 5001, "service": "Backend gRPC Ingestion", "role": "Industrial Telemetry Collector"},
    {"port": 5099, "service": "Backend REST API (V1)", "role": "Core API Gateway"},
    {"port": 5432, "service": "PostgreSQL 18 Database", "role": "Relational Multi-Tenant Storage"},
    {"port": 6379, "service": "Redis Spool Cache", "role": "Telemetry & Event Spool"}
]


def print_banner():
    print("=========================================================================")
    print("           HEIMDALL INDUSTRIAL OT PLATFORM - DEV SETUP AUTOMATION       ")
    print("=========================================================================")


def check_tool_version(cmd, name, version_arg="--version", version_pattern=None, min_version=None):
    executable = shutil.which(cmd)
    if not executable:
        return False, f"Not found in PATH (executable '{cmd}' missing)"
    
    try:
        res = subprocess.run([cmd, version_arg], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=5)
        raw_out = (res.stdout + " " + res.stderr).strip()
        first_line = raw_out.splitlines()[0] if raw_out else "unknown"
        
        if version_pattern and min_version:
            match = re.search(version_pattern, raw_out)
            if match:
                version_str = match.group(1)
                # Parse numeric parts
                parts = [int(p) for p in re.findall(r"\d+", version_str)]
                min_parts = [int(p) for p in re.findall(r"\d+", min_version)]
                if parts < min_parts:
                    return False, f"Version {version_str} found, but >={min_version} is required"
                return True, f"v{version_str} ({executable})"
            return True, f"{first_line} ({executable})"
        return True, f"{first_line} ({executable})"
    except Exception as e:
        return False, str(e)


def check_prerequisites():
    print("\n[1/3] Verifying System Prerequisites...")
    print("-" * 73)
    results = {}
    all_passed = True

    # 1. .NET 10 SDK
    dotnet_ok, dotnet_msg = check_tool_version("dotnet", ".NET SDK", "--version", r"(\d+\.\d+\.\d+)", "10.0.0")
    print(f"  • .NET 10 SDK:          {'[PASS]' if dotnet_ok else '[FAIL]'} {dotnet_msg}")
    results["dotnet"] = dotnet_ok
    if not dotnet_ok: all_passed = False

    # 2. Bun
    bun_ok, bun_msg = check_tool_version("bun", "Bun", "--version", r"(\d+\.\d+\.\d+)", "1.0.0")
    print(f"  • Bun Runtime:          {'[PASS]' if bun_ok else '[FAIL]'} {bun_msg}")
    results["bun"] = bun_ok
    if not bun_ok: all_passed = False

    # 3. Docker & Docker Compose
    docker_ok, docker_msg = check_tool_version("docker", "Docker", "--version")
    if docker_ok:
        try:
            daemon_check = subprocess.run(["docker", "info"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=4)
            if daemon_check.returncode != 0:
                docker_ok = False
                docker_msg += " (Daemon not running or access denied)"
        except Exception:
            docker_ok = False
            docker_msg += " (Failed to communicate with Docker daemon)"
    print(f"  • Docker Daemon:        {'[PASS]' if docker_ok else '[FAIL]'} {docker_msg}")
    results["docker"] = docker_ok
    if not docker_ok: all_passed = False

    # 4. Python 3.12+
    py_ver = sys.version_info
    py_ok = (py_ver.major == 3 and py_ver.minor >= 12) or py_ver.major > 3
    py_msg = f"{sys.version.split()[0]} ({sys.executable})"
    if not py_ok:
        py_msg += " (Requires Python 3.12+)"
        all_passed = False
    print(f"  • Python 3.12+:         {'[PASS]' if py_ok else '[FAIL]'} {py_msg}")
    results["python"] = py_ok

    # 5. PostgreSQL client tools
    psql_ok, psql_msg = check_tool_version("psql", "PostgreSQL Client", "--version")
    if not psql_ok:
        # Check if Docker container postgres is available as client fallback
        try:
            d_psql = subprocess.run(["docker", "run", "--rm", "postgres:18", "psql", "--version"],
                                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=5)
            if d_psql.returncode == 0:
                psql_ok = True
                psql_msg = f"Available via containerized PostgreSQL runtime ({d_psql.stdout.strip()})"
            else:
                psql_msg = "psql not in host PATH (Fallback container will be utilized during seed)"
                psql_ok = True  # Tolerated via docker exec
        except Exception:
            psql_msg = "psql not found locally; Docker postgres container execution will be used"
            psql_ok = True

    print(f"  • PostgreSQL Client:    {'[PASS]' if psql_ok else '[WARN]'} {psql_msg}")
    results["psql"] = psql_ok

    return all_passed, results


def check_ports():
    print("\n[2/3] Performing Diagnostic Checks on Local Ports...")
    print("-" * 73)
    print(f"{'PORT':<6} | {'TARGET SERVICE':<28} | {'CURRENT STATE':<16} | {'DIAGNOSTIC DETAIL'}")
    print("-" * 73)

    port_status = {}
    for p in PREREQUISITE_PORTS:
        port = p["port"]
        service = p["service"]
        
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(0.4)
        is_bound = False
        try:
            res = sock.connect_ex(("127.0.0.1", port))
            is_bound = (res == 0)
        except OSError:
            is_bound = False
        finally:
            sock.close()

        if is_bound:
            # Discover process listening on port if lsof / fuser exists
            proc_info = "Active / In Use"
            try:
                proc = subprocess.run(["fuser", f"{port}/tcp"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
                pids = proc.stdout.strip()
                if pids:
                    proc_info = f"Bound by PID(s): {pids}"
            except Exception:
                pass
            print(f"{port:<6} | {service:<28} | \033[93m● BOUND\033[0m         | {proc_info}")
            port_status[port] = "bound"
        else:
            print(f"{port:<6} | {service:<28} | \033[92m○ AVAILABLE\033[0m     | Ready for allocation")
            port_status[port] = "available"

    return port_status


def bootstrap_databases(skip_seed=False):
    print("\n[3/3] Initializing Databases & Seeding Enterprise Data...")
    print("-" * 73)

    # 1. Start database containers
    print(">> Ensuring PostgreSQL & Redis containers are healthy...")
    db_compose_dir = os.path.join(ROOT_DIR, "infra", "database")
    try:
        subprocess.run(["docker", "compose", "up", "-d"], cwd=db_compose_dir, check=True)
    except subprocess.CalledProcessError as e:
        print(f"Error starting database containers: {e}")
        return False

    # Wait for database readiness
    ready = False
    for attempt in range(25):
        s = socket.socket()
        s.settimeout(0.5)
        try:
            if s.connect_ex(("127.0.0.1", 5432)) == 0:
                ready = True
                s.close()
                break
        except Exception:
            pass
        finally:
            s.close()
        time.sleep(0.5)

    if not ready:
        print("Failed to establish connection to PostgreSQL on port 5432.")
        return False

    print("PostgreSQL & Redis database containers are healthy.")

    if skip_seed:
        print("Skipping seed data pipeline (--skip-seed specified).")
        return True

    # 2. Run seed pipeline validation and incremental application
    print(">> Validating & executing enterprise plant seed data pipeline...")
    seed_script = os.path.join(ROOT_DIR, "seed_data", "seed_pipeline.py")
    if os.path.isfile(seed_script):
        res = subprocess.run([sys.executable, seed_script, "--validate"], cwd=ROOT_DIR)
        if res.returncode != 0:
            print("Seed pipeline validation reported issues.")
            return False

    # Apply incremental seed sql
    seed_sql = os.path.join(ROOT_DIR, "seed_data", "incremental_seed.sql")
    if os.path.isfile(seed_sql):
        # Look for postgres_heimdall or heimdall_postgres container
        find_container = subprocess.run(["docker", "ps", "--filter", "name=postgres", "--format", "{{.Names}}"],
                                        stdout=subprocess.PIPE, text=True)
        container_names = find_container.stdout.strip().splitlines()
        target_container = None
        for c in container_names:
            if "postgres" in c:
                target_container = c
                break

        if target_container:
            print(f">> Applying incremental seed dataset to container '{target_container}'...")
            with open(seed_sql, "rb") as f:
                apply_res = subprocess.run(
                    ["docker", "exec", "-i", target_container, "psql", "-U", "postgres", "-d", "heimdall_dev_db"],
                    input=f.read(),
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE
                )
            if apply_res.returncode == 0:
                print("Enterprise dataset (100 machines, 16 orgs, 550 spare parts) applied successfully.")
            else:
                err = apply_res.stderr.decode('utf-8', errors='ignore')
                print(f"Warning during seed application: {err.splitlines()[-1] if err else 'Unknown'}")
        else:
            print("Note: Running postgres container not detected directly; skipping SQL pipe execution.")

    return True


def main():
    parser = argparse.ArgumentParser(description="Heimdall Dev Setup & Environment Automation")
    parser.add_argument("--check-only", action="store_true", help="Run prerequisite and port diagnostics without modifying DB")
    parser.add_argument("--skip-seed", action="store_true", help="Initialize database containers without running enterprise seed")
    parser.add_argument("--non-interactive", action="store_true", help="Run without interactive prompts")
    args = parser.parse_args()

    print_banner()

    prereqs_ok, prereqs = check_prerequisites()
    ports = check_ports()

    if args.check_only:
        print("\nDiagnostic check complete.")
        sys.exit(0 if prereqs_ok else 1)

    if not prereqs_ok:
        print("\nMissing required prerequisites. Please install required dependencies before continuing.")
        sys.exit(1)

    success = bootstrap_databases(skip_seed=args.skip_seed)

    print("\n" + "=" * 73)
    if success:
        print("  \033[92mHEIMDALL DEVELOPMENT ENVIRONMENT SUCCESSFULLY INITIALIZED\033[0m")
        print("  • To launch full stack with interactive monitor:   just dev")
        print("  • To start individual service:                     just start <service>")
        print("  • To view pgAdmin portal (optional profile):       just pgadmin")
        print("  • To run test suites:                              just test")
    else:
        print("  \033[91mHEIMDALL DEV SETUP ENCOUNTERED ERRORS DURING INITIALIZATION\033[0m")
    print("=" * 73 + "\n")

    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
