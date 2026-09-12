#!/usr/bin/env python3
"""
Heimdall Windows Agent Orchestrator & Test Runner
Automates building the self-contained win-x64 agent binary, managing the
Windows 10 LTSC Docker container, and executing the Windows endpoint test suite.
"""

import sys
import os
import subprocess
import argparse
import socket
import time
import shutil

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHARED_AGENT_DIR = os.path.join(ROOT_DIR, "infra", "windows", "shared", "agent")
WINDOWS_HOST = "127.0.0.1"


def run_cmd(cmd, cwd=ROOT_DIR, check=True):
    print(f">> Executing: {' '.join(cmd) if isinstance(cmd, list) else cmd}")
    res = subprocess.run(cmd, cwd=cwd, shell=isinstance(cmd, str))
    if check and res.returncode != 0:
        print(f"Command failed with exit code {res.returncode}")
        sys.exit(res.returncode)
    return res.returncode == 0


def check_prerequisites():
    print("================================================================")
    print("           WINDOWS AGENT PREREQUISITES VERIFICATION            ")
    print("================================================================")
    all_ok = True

    # 1. KVM Virtualization Support
    kvm_path = "/dev/kvm"
    if os.path.exists(kvm_path) and os.access(kvm_path, os.R_OK | os.W_OK):
        print(f"  ✓ KVM Hardware Virtualization: Available ({kvm_path})")
    else:
        print(f"  ✗ KVM Hardware Virtualization: Missing or inaccessible at {kvm_path}")
        all_ok = False

    # 2. Docker & Compose
    if shutil.which("docker"):
        print("  ✓ Docker Engine: Available")
    else:
        print("  ✗ Docker Engine: Not found in PATH")
        all_ok = False

    # 3. .NET SDK
    if shutil.which("dotnet"):
        print("  ✓ .NET SDK: Available")
    else:
        print("  ✗ .NET SDK: Not found in PATH")
        all_ok = False

    print("================================================================")
    return all_ok


def build_agent_win64():
    print("\n>> Building Heimdall Agent for Windows (win-x64, self-contained)...")
    os.makedirs(SHARED_AGENT_DIR, exist_ok=True)
    cmd = [
        "dotnet", "publish",
        "agent/App.Agent.Daemon/App.Agent.Daemon.csproj",
        "-r", "win-x64",
        "-c", "Release",
        "--self-contained", "true",
        "-o", SHARED_AGENT_DIR
    ]
    ok = run_cmd(cmd)
    if ok:
        exe_path = os.path.join(SHARED_AGENT_DIR, "App.Agent.Daemon.exe")
        if os.path.exists(exe_path):
            size_mb = round(os.path.getsize(exe_path) / (1024 * 1024), 2)
            print(f"  ✓ Windows binary generated: {exe_path} ({size_mb} MB)")
            return True
    return False


def build_container_image():
    print("\n>> Building Windows Agent Docker Image...")
    cmd = ["docker", "compose", "--profile", "windows", "build", "windows-agent"]
    return run_cmd(cmd)


def start_container():
    print("\n>> Starting Windows Agent Container (Windows 10 LTSC)...")
    cmd = ["docker", "compose", "--profile", "windows", "up", "-d", "windows-agent"]
    return run_cmd(cmd)


def stop_container():
    print("\n>> Stopping Windows Agent Container...")
    cmd = ["docker", "compose", "--profile", "windows", "stop", "windows-agent"]
    return run_cmd(cmd)


def check_port(host, port, timeout=1.0):
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except (socket.timeout, ConnectionRefusedError, OSError):
        return False


def show_status():
    print("================================================================")
    print("                WINDOWS TEST NODE STATUS                        ")
    print("================================================================")
    ports = [
        ("Web VNC Viewer", 8006),
        ("RDP Remote Desktop", 3389),
        ("WinRM HTTP Service", 5985),
        ("Agent Configurator API", 5998),
        ("OpenSSH Server", 2222)
    ]
    for name, port in ports:
        alive = check_port(WINDOWS_HOST, port)
        status_str = "\033[92m● ONLINE\033[0m" if alive else "\033[91m○ OFFLINE\033[0m"
        print(f"  {name:<26} | {WINDOWS_HOST}:{port:<6} | {status_str}")
    print("================================================================")


def run_tests():
    print("\n>> Executing Windows Endpoint Integration Tests...")
    test_script = os.path.join(ROOT_DIR, "tests", "integration", "test_windows_endpoint.py")
    return run_cmd([sys.executable, test_script], check=False)


def main():
    parser = argparse.ArgumentParser(description="Heimdall Windows Agent Orchestrator")
    parser.add_argument("--check-prereqs", action="store_true", help="Check host prerequisites")
    parser.add_argument("--build-agent", action="store_true", help="Compile win-x64 agent binary into shared folder")
    parser.add_argument("--build-image", action="store_true", help="Build Windows container Dockerfile")
    parser.add_argument("--up", action="store_true", help="Start the Windows agent container")
    parser.add_argument("--down", action="store_true", help="Stop the Windows agent container")
    parser.add_argument("--status", action="store_true", help="Show Windows container port status")
    parser.add_argument("--test", action="store_true", help="Run Windows endpoint integration tests")

    args = parser.parse_args()

    # If no flags provided, run full standard workflow: prereqs -> build binary -> check/start -> test
    if not any(vars(args).values()):
        if not check_prerequisites():
            sys.exit(1)
        build_agent_win64()
        start_container()
        show_status()
        run_tests()
        return

    if args.check_prereqs:
        sys.exit(0 if check_prerequisites() else 1)

    if args.build_agent:
        build_agent_win64()

    if args.build_image:
        build_container_image()

    if args.up:
        start_container()
        show_status()

    if args.down:
        stop_container()

    if args.status:
        show_status()

    if args.test:
        run_tests()


if __name__ == "__main__":
    main()
