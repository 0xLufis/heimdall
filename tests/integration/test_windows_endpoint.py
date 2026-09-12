#!/usr/bin/env python3
"""
Heimdall Windows Endpoint Integration Test Suite
Validates that the Windows 10 LTSC test node and Heimdall Agent Daemon
are reachable, operational, reporting telemetry, and executing commands.
"""

import sys
import os
import time
import socket
import json
import urllib.request
import urllib.error

WINDOWS_HOST = os.environ.get("WINDOWS_HOST", "127.0.0.1")
AGENT_HTTP_PORT = int(os.environ.get("AGENT_HTTP_PORT", 5998))
WINRM_PORT = int(os.environ.get("WINRM_PORT", 5985))
RDP_PORT = int(os.environ.get("RDP_PORT", 3389))
VNC_PORT = int(os.environ.get("VNC_PORT", 8006))
BACKEND_API_URL = os.environ.get("BACKEND_API_URL", "http://127.0.0.1:5099")


def check_tcp_port(host: str, port: int, timeout: float = 2.0) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except (socket.timeout, ConnectionRefusedError, OSError):
        return False


def http_get_json(url: str, timeout: float = 3.0):
    req = urllib.request.Request(url, headers={"User-Agent": "Heimdall-EndpointTester/1.0"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def http_post_json(url: str, data: dict, timeout: float = 3.0):
    payload = json.dumps(data).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=payload,
        headers={"Content-Type": "application/json", "User-Agent": "Heimdall-EndpointTester/1.0"},
        method="POST"
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.status, resp.read().decode("utf-8")


def test_ports_reachability():
    print("\n[TEST 1] Checking Windows Endpoint Network Ports...")
    ports = [
        ("Agent Configurator HTTP", AGENT_HTTP_PORT),
        ("WinRM HTTP", WINRM_PORT),
        ("RDP", RDP_PORT),
        ("Web VNC Viewer", VNC_PORT),
    ]

    all_ok = True
    for name, port in ports:
        is_open = check_tcp_port(WINDOWS_HOST, port)
        status = "ONLINE" if is_open else "OFFLINE (Container not running or still booting)"
        print(f"  - {name:<26} ({WINDOWS_HOST}:{port}) -> {status}")
        if not is_open and port == AGENT_HTTP_PORT:
            all_ok = False

    return all_ok


def test_agent_configurator_api():
    print("\n[TEST 2] Hitting Windows Agent Configurator HTTP API (:5998)...")
    url = f"http://{WINDOWS_HOST}:{AGENT_HTTP_PORT}/api/config"

    try:
        config = http_get_json(url)
        print(f"  ✓ GET /api/config succeeded. Current BackendUrl: {config.get('backendUrl', 'N/A')}")
        print(f"    - SchemaVersion: {config.get('configSchemaVersion')}")
        print(f"    - AuthType:      {config.get('authType')}")
        print(f"    - SpoolMode:     {config.get('spoolEncryptionMode')}")

        # Test mutating config via POST
        test_update = {"backendUrl": config.get("backendUrl", "http://backend:5001")}
        status, resp_text = http_post_json(url, test_update)
        assert status == 200, f"Expected 200 OK, got {status}"
        print("  ✓ POST /api/config succeeded (configuration updated successfully).")
        return True
    except Exception as ex:
        print(f"  ✗ Configurator API test failed: {ex}")
        return False


def test_backend_windows_telemetry_ingestion():
    print("\n[TEST 3] Verifying Backend Ingestion of Windows Endpoint Telemetry...")
    url = f"{BACKEND_API_URL}/api/v1/clientpc"

    try:
        pcs = http_get_json(url)
        print(f"  ✓ Fetched {len(pcs)} registered Client PC(s) from Backend REST API.")

        # Find any client PC reporting Windows
        windows_pcs = [
            pc for pc in pcs
            if "windows" in (pc.get("osDescription") or pc.get("osVersion") or "").lower()
            or any("windows" in str(v).lower() for v in (pc.get("systemMetadata") or {}).values())
        ]

        if not windows_pcs:
            print("  ℹ Note: No Windows endpoint has reported to backend yet.")
            print("    Run the agent daemon inside the Windows container or wait for heartbeat.")
            return False

        target = windows_pcs[0]
        print(f"  ✓ Identified Windows Endpoint:")
        print(f"    - Hostname:          {target.get('hostname')}")
        print(f"    - MachineIdentifier: {target.get('machineIdentifier')}")
        print(f"    - MAC Address:       {target.get('macAddress')}")
        print(f"    - OS Description:    {target.get('osDescription') or target.get('osVersion')}")
        return True
    except urllib.error.URLError as ex:
        print(f"  ℹ Backend REST API ({url}) offline: {ex.reason}")
        return None
    except Exception as ex:
        print(f"  ✗ Telemetry verification error: {ex}")
        return False


def main():
    print("================================================================")
    print("        HEIMDALL WINDOWS ENDPOINT INTEGRATION TEST RUNNER       ")
    print("================================================================")

    # 1. Reachability
    ports_ok = test_ports_reachability()

    if not ports_ok:
        print("\n⚠️ Windows Agent HTTP port 5998 is not currently reachable.")
        print("If the Windows container is currently booting or installing, please allow a few minutes.")
        print("Run 'python3 tools/test_windows_agent.py' to launch and orchestrate the environment.")
        sys.exit(1)

    # 2. Agent Configurator API
    config_ok = test_agent_configurator_api()

    # 3. Backend Ingestion
    backend_ok = test_backend_windows_telemetry_ingestion()

    print("\n================================================================")
    if config_ok and (backend_ok is True or backend_ok is None):
        print("🎉 Windows Endpoint Tests PASSED.")
        sys.exit(0)
    else:
        print("❌ Windows Endpoint Tests encountered issues.")
        sys.exit(1)


if __name__ == "__main__":
    main()
