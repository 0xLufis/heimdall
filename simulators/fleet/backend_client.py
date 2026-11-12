#!/usr/bin/env python3
"""
Performs the maintenance desk's actions against the Heimdall backend REST API (dev / demo only).

Every action uses an existing endpoint, the same ones the web UI and a machine controller use:

  raise      POST  /api/v1/tickets/machine-raised        (anonymous machine endpoint; assignee from the delegation rules)
  reserve    POST  /api/v1/tickets/{id}/reserve          {technician}
  pickup     POST  /api/v1/tickets/{id}/qr-pickup        {technician}        -> InProgress, reaction time
  pending    PATCH /api/v1/tickets/{id}/pending          {pendingReason: 1 (Parts), pendingDetails}
  resume     PATCH /api/v1/tickets/{id}/status           "InProgress"
  consume    POST  /api/v1/component/assign-part         serialized spare into a component slot (history + service log)
             PUT   /api/v1/inventory/{id}                {stockQuantity: n-1} for bulk consumables
  restock    PUT   /api/v1/inventory/{id}                {stockQuantity: n+q}
  escalate   POST  /api/v1/tickets/{id}/escalate         / deescalate
  resolve    PATCH /api/v1/tickets/{id}/status           "Resolved"
  close      POST  /api/v1/tickets/{id}/auto-close

Authentication: the lifecycle endpoints need a session with a maintenance role. A Development backend
accepts unauthenticated calls as the dev administrator; otherwise pass a Better-Auth session token
(HEIMDALL_SIM_API_TOKEN / --api-token). The agent key is an MQTT credential and is not accepted by REST.

The dispatcher is a FIFO worker: when the backend is unreachable it keeps the action and retries, so
a backend restart only delays the simulated work.
"""

from __future__ import annotations

import datetime
import json
import os
import queue
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Dict, List, Optional

from maintenance import Action

PENDING_PARTS = 1            # App.Contracts.Enums.PendingReason.Parts
HANDOVER_NOTIFICATION = 1    # EscalationHandoverState.Notification
SWAP_FAILURE = 1             # PartSwapReason.FailureBreakdown
DISPOSITION_SENT_FOR_REPAIR = 5


class BackendUnavailable(Exception):
    """Connection problem or 5xx: worth retrying."""


class BackendRejected(Exception):
    """4xx: the request is wrong for this database (e.g. dataset not seeded)."""


class BackendApi:
    def __init__(self, base_url: str = "http://127.0.0.1:5099", token: Optional[str] = None, allow_remote: bool = False, timeout: float = 15.0):
        parsed = urllib.parse.urlparse(base_url)
        if parsed.scheme not in ("http", "https") or not parsed.hostname:
            raise ValueError(f"bad backend URL {base_url!r}")
        if parsed.hostname not in ("localhost", "127.0.0.1", "::1", "backend", "heimdall_backend") and not allow_remote:
            raise ValueError(f"refusing to simulate plant activity against non-local backend {parsed.hostname!r} (set SIM_ALLOW_REMOTE_BACKEND=1)")
        self.base = base_url.rstrip("/")
        self.token = token
        self.timeout = timeout

    def call(self, method: str, path: str, body: Any = None) -> Any:
        data = json.dumps(body).encode("utf-8") if body is not None else None
        req = urllib.request.Request(self.base + path, data=data, method=method)
        req.add_header("Accept", "application/json")
        req.add_header("User-Agent", "heimdall-fleet-simulator")
        if data is not None:
            req.add_header("Content-Type", "application/json")
        if self.token:
            req.add_header("Authorization", f"Bearer {self.token}")
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                raw = resp.read().decode("utf-8")
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", "replace")[:200]
            if exc.code >= 500 or exc.code == 429:
                raise BackendUnavailable(f"{method} {path} -> HTTP {exc.code}: {detail}")
            raise BackendRejected(f"{method} {path} -> HTTP {exc.code}: {detail}")
        except (urllib.error.URLError, OSError, ValueError) as exc:
            raise BackendUnavailable(f"{method} {path} -> {exc}")


class ActionDispatcher:
    """Executes desk actions in order on a worker thread; resolves database ids through the API."""

    def __init__(self, api: BackendApi, speed_getter=lambda: 1.0, retry_s: float = 5.0):
        self.api = api
        self.speed = speed_getter
        self.retry_s = retry_s
        self.queue: "queue.Queue[Action]" = queue.Queue()
        self.tickets: Dict[str, str] = {}            # incident key -> ticket id
        self.dropped: set = set()                    # incidents the backend rejected
        self.stats: Dict[str, int] = {}
        self.last_error: Optional[str] = None
        self.recent: List[Dict[str, Any]] = []
        self._machines: Optional[Dict[str, Dict[str, Any]]] = None
        self._components: Dict[str, List[Dict[str, Any]]] = {}
        self._used_parts: set = set()
        self._open_tickets: Optional[Dict[tuple, str]] = None
        self._thread: Optional[threading.Thread] = None
        self._stop = threading.Event()

    # ------------------------------------------------------------------ lifecycle
    def start(self) -> None:
        self._thread = threading.Thread(target=self._run, name="maintenance-dispatcher", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()

    def submit(self, actions: List[Action]) -> None:
        for a in actions:
            self.queue.put(a)

    def drain(self, timeout: float = 30.0) -> bool:
        """Waits until every submitted action was handled (tests / smoke runs)."""
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if self.queue.unfinished_tasks == 0:
                return True
            time.sleep(0.05)
        return False

    def _run(self) -> None:
        while not self._stop.is_set():
            try:
                action = self.queue.get(timeout=0.5)
            except queue.Empty:
                continue
            while not self._stop.is_set():
                try:
                    self.handle(action)
                    self.last_error = None
                    break
                except BackendUnavailable as exc:        # backend down / restarting: keep the action
                    self.last_error = str(exc)
                    self.stats["retries"] = self.stats.get("retries", 0) + 1
                    self._stop.wait(self.retry_s)
                except BackendRejected as exc:
                    self.last_error = str(exc)
                    self.stats["rejected"] = self.stats.get("rejected", 0) + 1
                    if action.incident:
                        self.dropped.add(action.incident)
                    break
                except Exception as exc:                 # never let one action kill the worker
                    self.last_error = f"{action.kind}: {exc!r}"
                    self.stats["errors"] = self.stats.get("errors", 0) + 1
                    break
            self.queue.task_done()

    # ------------------------------------------------------------------ directory (ids come from the API, not from the seed files)
    def _machine(self, station: str) -> Dict[str, Any]:
        if self._machines is None or station.upper() not in self._machines:
            index: Dict[str, Dict[str, Any]] = {}
            for m in self.api.call("GET", "/api/v1/machine") or []:
                for key in (m.get("customIdentifier"), m.get("name"), m.get("pinnedObjectHandle")):
                    if key:
                        index.setdefault(str(key).upper(), m)
            self._machines = index
        if station.upper() not in self._machines:
            raise BackendRejected(f"station {station} is not in the database (is this dataset seeded?)")
        return self._machines[station.upper()]

    def _component(self, machine_id: str, slot: str) -> Optional[Dict[str, Any]]:
        if machine_id not in self._components:
            self._components[machine_id] = self.api.call("GET", f"/api/v1/component/machine/{machine_id}") or []
        return next((c for c in self._components[machine_id] if c.get("functionalSlotIdentifier") == slot), None)

    def _adopt_open_ticket(self, machine_id: str, alarm_code: str) -> Optional[str]:
        """A ticket for the same machine + alarm that is still open (e.g. from before a simulator restart)."""
        if self._open_tickets is None:
            self._open_tickets = {}
            for t in self.api.call("GET", "/api/v1/tickets") or []:
                if t.get("status") not in ("Resolved", "Closed") and t.get("machineId") and t.get("alarmCode"):
                    self._open_tickets.setdefault((t["machineId"], t["alarmCode"]), t["id"])
        return self._open_tickets.pop((machine_id, alarm_code), None)

    def _stock_item(self, consumable_class: str) -> Optional[Dict[str, Any]]:
        items = [i for i in self.api.call("GET", "/api/v1/inventory/stock") or []
                 if (i.get("metadata") or {}).get("consumableClass") == consumable_class]
        return max(items, key=lambda i: i.get("stockQuantity") or 0) if items else None

    def _spare(self, part_class: str) -> Optional[Dict[str, Any]]:
        for i in self.api.call("GET", "/api/v1/inventory/parts") or []:
            if (i.get("equipmentStatus") == "InStorage" and not i.get("isStockItem") and not i.get("machineId")
                    and (i.get("metadata") or {}).get("partClass") == part_class and i["id"] not in self._used_parts):
                return i
        return None

    # ------------------------------------------------------------------ actions
    def handle(self, a: Action) -> None:
        if a.incident and a.incident in self.dropped:
            return
        d = a.data
        if a.kind == "restock":
            item = self._stock_item(str(d["consumable_class"]))
            if item:
                self.api.call("PUT", f"/api/v1/inventory/{item['id']}", {"stockQuantity": int(item.get("stockQuantity") or 0) + int(d["quantity"])})
            return self._done(a)

        machine = self._machine(a.station)
        if a.kind == "raise":
            ticket_id = self._adopt_open_ticket(machine["id"], str(d["alarm_code"]))
            if ticket_id is None:
                now = datetime.datetime.now(datetime.timezone.utc)
                speed = max(0.1, float(self.speed()))
                probes = [{
                    "probeId": f"{a.station}:{p['metric']}", "datapointKey": f"{a.station}.{p['metric']}", "metricName": p["metric"],
                    "numericValue": p["value"], "unit": p["unit"], "triggerStatus": "AlarmBitSet", "sampledAt": now.isoformat(),
                    "probeBuffer": [{"timestamp": (now - datetime.timedelta(seconds=b["age_s"] / speed)).isoformat(), "value": b["value"]}
                                    for b in p["buffer"]],
                } for p in d.get("probes", [])]
                res = self.api.call("POST", "/api/v1/tickets/machine-raised", {
                    "machineId": machine["id"], "alarmCode": d["alarm_code"], "title": d["title"], "description": d["description"],
                    "priority": d["priority"], "isLineStop": bool(d["is_line_stop"]), "controllerSource": d.get("controller"),
                    "targetComponentDeviceTag": d.get("slot"), "probes": probes})
                ticket_id = res["ticketId"]
            self.tickets[a.incident] = ticket_id
            return self._done(a, ticket=ticket_id)

        ticket_id = self.tickets.get(a.incident)
        if ticket_id is None:
            return                                   # its ticket was never created
        path = f"/api/v1/tickets/{ticket_id}"
        if a.kind == "reserve":
            self.api.call("POST", path + "/reserve", {"technician": d["technician"]})
        elif a.kind == "pickup":
            self.api.call("POST", path + "/qr-pickup", {"technician": d["technician"]})
        elif a.kind == "pending":
            self.api.call("PATCH", path + "/pending", {"pendingReason": PENDING_PARTS, "pendingDetails": d["details"]})
        elif a.kind == "resume":
            self.api.call("PATCH", path + "/status", "InProgress")
        elif a.kind == "escalate":
            self.api.call("POST", path + "/escalate", {"reason": d["reason"], "escalatedBy": d["by"],
                                                         "handoverState": HANDOVER_NOTIFICATION, "target": d["target"]})
        elif a.kind == "deescalate":
            self.api.call("POST", path + "/deescalate", {"resolvedBy": d["by"], "resolutionNotes": "Repair completed"})
        elif a.kind == "resolve":
            self.api.call("PATCH", path + "/status", "Resolved")
        elif a.kind == "close":
            self.api.call("POST", path + "/auto-close", {"machineId": a.station, "controllerId": machine.get("name"),
                                                           "clearedReason": d["cleared_reason"]})
            self.tickets.pop(a.incident, None)
        elif a.kind == "consume":
            self._consume(a, machine, ticket_id)
        return self._done(a, ticket=ticket_id)

    def _consume(self, a: Action, machine: Dict[str, Any], ticket_id: str) -> None:
        d = a.data
        if d["resource"] == "consumable":
            item = self._stock_item(str(d["part_class"]))
            if item and (item.get("stockQuantity") or 0) > 0:
                self.api.call("PUT", f"/api/v1/inventory/{item['id']}", {"stockQuantity": int(item["stockQuantity"]) - 1})
            return
        component = self._component(machine["id"], str(d["slot"]))
        spare = self._spare(str(d["part_class"]))
        if component is None or spare is None:
            return
        self._used_parts.add(spare["id"])
        self.api.call("POST", "/api/v1/component/assign-part", {
            "machineId": machine["id"], "existingComponentId": component["id"], "partId": spare["id"],
            "swappedByUserId": d["technician_id"], "swappedByUserName": d["technician"], "reason": SWAP_FAILURE,
            "previousPartDisposition": DISPOSITION_SENT_FOR_REPAIR, "associatedTicketId": ticket_id,
            "notes": f"Replaced during {a.incident}"})
        self._components.pop(machine["id"], None)

    def _done(self, a: Action, ticket: Optional[str] = None) -> None:
        self.stats[a.kind] = self.stats.get(a.kind, 0) + 1
        self.recent.append({"action": a.kind, "station": a.station, "incident": a.incident, "ticket": ticket,
                            "at": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds")})
        del self.recent[:-25]

    def describe(self) -> Dict[str, Any]:
        return {"backend": self.api.base, "authenticated": bool(self.api.token), "queued": self.queue.qsize(),
                "done": dict(sorted(self.stats.items())), "last_error": self.last_error, "recent": self.recent[-10:]}


def dispatcher_from_env(speed_getter=lambda: 1.0, base_url: Optional[str] = None, token: Optional[str] = None) -> ActionDispatcher:
    base = base_url or os.environ.get("BACKEND_API_URL") or "http://127.0.0.1:5099"
    api = BackendApi(base, token=token or os.environ.get("HEIMDALL_SIM_API_TOKEN") or None,
                     allow_remote=os.environ.get("SIM_ALLOW_REMOTE_BACKEND", "").lower() in ("1", "true", "yes"))
    return ActionDispatcher(api, speed_getter=speed_getter)
