#!/usr/bin/env python3
"""
Heimdall Agent Extension Plugin Reference Script
Demonstrates how external software, scripts, and sidecars securely interface
with the local Heimdall Agent to submit custom sensor inventory, live metrics,
and operational events with component tree parent linkage.
"""

import os
import sys
import json
import time
import urllib.request
import urllib.error

AGENT_BASE_URL = os.environ.get("HEIMDALL_AGENT_URL", "http://127.0.0.1:5998")
EXTENSION_KEY = os.environ.get("HEIMDALL_EXTENSION_KEY", "heimdall-dev-extension-key")


def make_request(endpoint: str, method: str = "GET", data: dict = None) -> dict:
    url = f"{AGENT_BASE_URL}/api/v1{endpoint}"
    headers = {
        "User-Agent": "Heimdall-Custom-Sensor-Plugin/1.0",
        "Content-Type": "application/json",
        "X-Extension-Key": EXTENSION_KEY,
    }

    body = json.dumps(data).encode("utf-8") if data else None
    req = urllib.request.Request(url, data=body, headers=headers, method=method)

    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            if response.status in (200, 201, 202):
                content = response.read().decode("utf-8")
                return json.loads(content) if content else {}
            return {"status": response.status}
    except urllib.error.HTTPError as e:
        error_content = e.read().decode("utf-8")
        print(f"HTTP Error {e.code}: {error_content}", file=sys.stderr)
        raise
    except Exception as e:
        print(f"Connection Error: {e}", file=sys.stderr)
        raise


def main():
    print("[1] Checking Heimdall Agent status...")
    status = make_request("/agent/status")
    print(f"    Connected to Hostname: {status.get('hostname')}")
    print(f"    Machine Identifier:   {status.get('machineIdentifier')}")
    print(f"    Environment:          {status.get('environment')}")
    print(f"    Active Extensions:    {status.get('activeExtensionsCount')}")

    print("\n[2] Submitting custom sensor component with Component Tree parent linkage...")
    component_payload = {
        "componentName": "SpindleTriaxialVibrationSensor",
        "technology": "Python",
        "componentType": "sensor",
        "parentComponentName": "Spindle Motor Assembly 15kW",  # Automatically links under spindle in tree
        "data": {
            "sensorModel": "IFM VSA001",
            "bus": "IO-Link",
            "samplingRateHz": 10000,
            "axisX_RmsVelocity": 1.42,
            "axisY_RmsVelocity": 0.88,
            "axisZ_RmsVelocity": 2.05,
            "temperatureCelsius": 41.6,
            "bearingHealthIndex": 98.4
        },
        "ttlSeconds": 1800,  # 30 minute TTL
        "immediateSync": True  # Trigger immediate report to backend
    }
    comp_res = make_request("/extensions/components", method="POST", data=component_payload)
    print(f"    Component submission: {comp_res.get('message')}")

    print("\n[3] Submitting live telemetry readings...")
    telemetry_payload = {
        "source": "VibrationPlugin",
        "metrics": [
            {
                "key": "spindle.vibration.rms_velocity_x",
                "value": 1.42,
                "unit": "mm/s",
                "tags": {"axis": "X", "machine": "OP10"}
            },
            {
                "key": "spindle.vibration.rms_velocity_z",
                "value": 2.05,
                "unit": "mm/s",
                "tags": {"axis": "Z", "machine": "OP10"}
            }
        ]
    }
    telem_res = make_request("/extensions/telemetry", method="POST", data=telemetry_payload)
    print(f"    Telemetry submission: Enqueued {telem_res.get('enqueued')} metric points")

    print("\n[4] Submitting operational event...")
    event_payload = {
        "source": "SpindleVibrationMonitor",
        "level": "Info",
        "message": "Spindle vibration baselines calibrated within nominal limits.",
        "payload": {
            "calibrationTimestamp": time.time(),
            "status": "Healthy"
        }
    }
    event_res = make_request("/extensions/events", method="POST", data=event_payload)
    print(f"    Event submission: {event_res.get('status')}")

    print("\n[✓] All custom reporting tasks completed successfully.")


if __name__ == "__main__":
    main()
