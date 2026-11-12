#!/usr/bin/env python3
"""
MQTT transport for the simulators. Mirrors the real edge agent
(src/agent/App.Agent.Daemon/Infrastructure/MqttAgentClient.cs):

  * MQTT 5 over TCP to the backend's embedded broker (default 127.0.0.1:1883)
  * credentials  heimdall_agent / <agent key>
  * every publish carries the user property  x-agent-key = <agent key>
  * binary protobuf payloads on  heimdall/devices/{machineIdentifier}/{channel}
"""

from __future__ import annotations

import os
import threading
import uuid
from typing import Optional, Tuple

import paho.mqtt.client as mqtt
from paho.mqtt.packettypes import PacketTypes
from paho.mqtt.properties import Properties

ROOT_PREFIX = "heimdall/devices"
SYSTEM_INFO_CHANNEL = "system_info"
PLC_MEMORY_CHANNEL = "plc_memory"
DEFAULT_AGENT_KEY = "heimdall-dev-agent-key"


def sanitize(identifier: str) -> str:
    """Same rule as MqttTopics.Sanitize."""
    if not identifier or not identifier.strip():
        return "unknown"
    return identifier.strip().replace("/", "_").replace("+", "_").replace("#", "_")


def topic(machine_identifier: str, channel: str) -> str:
    return f"{ROOT_PREFIX}/{sanitize(machine_identifier)}/{channel}"


def parse_broker(value: Optional[str], default_port: int = 1883) -> Tuple[str, int]:
    """'host', 'host:port', 'mqtt://host:port' or 'tcp://host:port'."""
    v = (value or "").strip()
    for prefix in ("mqtt://", "tcp://"):
        if v.startswith(prefix):
            v = v[len(prefix):]
    if not v:
        return "127.0.0.1", default_port
    if ":" in v:
        host, _, port = v.rpartition(":")
        try:
            return host or "127.0.0.1", int(port)
        except ValueError:
            return v, default_port
    return v, default_port


def broker_from_env() -> Tuple[str, int]:
    host = os.environ.get("MQTT_BROKER_HOST") or "127.0.0.1"
    try:
        port = int(os.environ.get("MQTT_BROKER_PORT") or 1883)
    except ValueError:
        port = 1883
    return host, port


class MqttPublisher:
    def __init__(self, host: str, port: int, agent_key: Optional[str] = None, client_name: str = "fleet_simulator"):
        self.host = host
        self.port = port
        self.agent_key = agent_key if agent_key is not None else os.environ.get("HEIMDALL_AGENT_KEY", DEFAULT_AGENT_KEY)
        self.connected = threading.Event()
        self.last_error: Optional[str] = None
        self._client = mqtt.Client(
            mqtt.CallbackAPIVersion.VERSION2,
            client_id=f"heimdall_{client_name}_{uuid.uuid4().hex[:12]}",
            protocol=mqtt.MQTTv5,
        )
        if self.agent_key:
            self._client.username_pw_set("heimdall_agent", self.agent_key)
        self._client.on_connect = self._on_connect
        self._client.on_disconnect = self._on_disconnect
        self._client.reconnect_delay_set(min_delay=1, max_delay=15)

    # paho callback API v2 signatures
    def _on_connect(self, client, userdata, flags, reason_code, properties=None):
        if getattr(reason_code, "is_failure", False):
            self.last_error = f"broker refused connection: {reason_code}"
            self.connected.clear()
        else:
            self.last_error = None
            self.connected.set()

    def _on_disconnect(self, client, userdata, flags, reason_code, properties=None):
        self.connected.clear()
        if getattr(reason_code, "is_failure", False):
            self.last_error = f"disconnected: {reason_code}"

    def start(self) -> None:
        """Non-blocking: paho's network thread keeps retrying until the broker is up."""
        self._client.connect_async(self.host, self.port, keepalive=30)
        self._client.loop_start()

    def wait_connected(self, timeout: float = 5.0) -> bool:
        return self.connected.wait(timeout)

    def stop(self) -> None:
        try:
            self._client.disconnect()
        finally:
            self._client.loop_stop()

    def publish(self, machine_identifier: str, channel: str, payload: bytes, qos: int = 1, wait: float = 0.0) -> bool:
        """Returns True when the message was handed to the broker (acknowledged if `wait` > 0)."""
        if not self.connected.is_set():
            return False
        props = Properties(PacketTypes.PUBLISH)
        if self.agent_key:
            props.UserProperty = ("x-agent-key", self.agent_key)
        info = self._client.publish(topic(machine_identifier, channel), payload, qos=qos, properties=props)
        if info.rc != mqtt.MQTT_ERR_SUCCESS:
            self.last_error = mqtt.error_string(info.rc)
            return False
        if wait > 0:
            try:
                info.wait_for_publish(timeout=wait)
            except (ValueError, RuntimeError) as e:
                self.last_error = str(e)
                return False
            return info.is_published()
        return True
