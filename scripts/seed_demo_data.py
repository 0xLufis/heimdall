#!/usr/bin/env python3
"""
seed_demo_data.py - Heimdall demo dataset loader (Nordvik NV1, a FICTIONAL plant)
================================================================================

Single entry point for the story-driven demo dataset in ``seed_data/demo/``.
(The enterprise-scale dataset stays in ``seed_data/seed_pipeline.py``.)

What it does
------------
* Reads the JSON dataset (plant, people, tickets, templates, telemetry, Lua).
* Resolves symbolic keys to deterministic demo UUIDs (prefix ``de30de30-``),
  ``@key`` references, and ``~now-3h`` style relative timestamps.
* Validates referential integrity, duplicate ids, enum values and value ranges.
* Upserts everything in ONE transaction (``INSERT ... ON CONFLICT DO UPDATE``),
  so running it twice yields the same database state (idempotent).
* SQL text is static: the whole dataset travels as a single JSON document
  (COPY into a temp table, or a bound parameter with psycopg) and is expanded
  server-side with ``jsonb_populate_recordset``. No value is ever concatenated
  into SQL; identifiers come from a fixed allow-list that is checked against
  the live catalog (schema-drift detection).
* Optional ``--push-runtime`` registers the demo Lua scripts and replays the
  telemetry history into the backend's in-memory telemetry buffer via the API.

Safety
------
Refuses to run unless the environment is explicitly a dev/demo environment:
``HEIMDALL_ENABLE_DEV=true`` (from the environment or ``.env.dev``), no
Production/Staging ``ASPNETCORE_ENVIRONMENT``, and a target database whose
name does not look like prod/staging. Defaults to the dev database from
``.env.dev`` via the ``postgres_heimdall`` docker container.

Usage
-----
    python3 scripts/seed_demo_data.py --dry-run          # validate + print SQL plan, no DB
    python3 scripts/seed_demo_data.py --check            # run against DB, then ROLLBACK
    python3 scripts/seed_demo_data.py                    # load / refresh demo data
    python3 scripts/seed_demo_data.py --reset            # delete demo rows first, then load
    python3 scripts/seed_demo_data.py --with-logins      # also create demo-only credentials
    python3 scripts/seed_demo_data.py --push-runtime --skip-db   # Lua scripts + telemetry replay
    python3 scripts/seed_demo_data.py --write-telemetry  # regenerate seed_data/demo/telemetry.json
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import random
import re
import secrets
import shutil
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parent.parent
DEMO_DIR = REPO_ROOT / "seed_data" / "demo"
LUA_DIR = DEMO_DIR / "lua"

DEMO_NAMESPACE = uuid.UUID("6f1c2d8e-4b7a-5e3f-9c1d-0a2b3c4d5e6f")
DEMO_ID_PREFIX = "de30de30"           # every demo UUID starts with this -> safe --reset
DEMO_TEXT_ID_PREFIX = "demo-"         # auth.* text ids (users, org, members, accounts)
DEMO_ORG_ID = "demo-org-nordvik"

# Demo-only password for --with-logins. Clearly NOT a secret: it is printed in
# the README and only ever written to a dev database. Override with
# HEIMDALL_DEMO_PASSWORD if you want something else.
DEFAULT_DEMO_PASSWORD = "Demo-Only-NV1!"

SAFE_IDENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
REF_RE = re.compile(r"^@([a-z0-9-]+:[A-Za-z0-9:_.-]+)$")
TIME_RE = re.compile(r"^~now(?:([+-])((?:\d+[dhms])+))?$")
TIME_PART_RE = re.compile(r"(\d+)([dhms])")

# --------------------------------------------------------------------------------------
# Static schema allow-list (mirrors the live dev DB, Nov 2026). Used offline (--dry-run)
# and as the identifier allow-list; with a DB connection it is diffed against the live
# catalog and any drift is reported.
# --------------------------------------------------------------------------------------
TABLE_COLUMNS: dict[str, list[str]] = {
    "auth.organization": "id name slug logo created_at metadata".split(),
    "auth.user": "id name email email_verified image created_at updated_at role username banned ban_reason ban_expires_at".split(),
    "auth.account": "id account_id provider_id user_id access_token refresh_token id_token access_token_expires_at refresh_token_expires_at scope password created_at updated_at issuer".split(),
    "auth.member": "id organization_id user_id role created_at".split(),
    "backend.manufacturers": "id name website support_contact".split(),
    "backend.suppliers": "id name website contact_person email phone_number lead_time_days address notes created_at".split(),
    "backend.machine_groups": "id name description parent_id color icon lead_engineer_id lead_engineer_name machine_ids_json machine_types_json created_at updated_at group_type interface_contract technology_key".split(),
    "backend.client_pcs": "id name mac_address ip_address machine_identifier hostname last_online pinned_object_handle free_disk_space monitoring_config resource_averages alerting_limits system_metadata organization_id vlan_id vlan_name subnet ad_ou_path ou_tags certificate_thumbprint certificate_profile_name".split(),
    "backend.inventory_items": "id name display_name organization_id cost_in_huf purchase_date serial_number manufacturer_id supplier_id client_pc_id parent_id metadata equipment_status is_stock_item machine_id min_stock_threshold stock_quantity storage_location technology".split(),
    "backend.stations": "id custom_identifier pinned_object_handle machine_type group_id preferred_technician_id preferred_technician_name status decommission_reason decommission_date decommission_disposition line_group_id technologies spoc spoc_user_id owner owner_user_id sub_line_group_id project_group_id business_unit_group_id parent_machine_id sub_machine_index cell_type".split(),
    "backend.hardware_assets": "id revision model_number".split(),
    "backend.StationControllers": "client_pc_id machine_id id created_at metadata role".split(),
    "backend.machine_components": "id machine_id name functional_slot_identifier category component_recipe_id current_part_id tags attributes_json eplan_cad_ref_json installed_at updated_at".split(),
    "backend.machine_group_memberships": "id group_id machine_id assigned_at assigned_by".split(),
    "backend.skill_definitions": "id code name description category issuer validity_months created_at".split(),
    "backend.personnel_skills": "id user_id skill_id certified_at expires_at certificate_number verified_by".split(),
    "backend.personnel_groups": "id name description discipline shift_pattern organization_id created_at".split(),
    "backend.personnel_group_members": "id personnel_group_id user_id user_name joined_at".split(),
    "backend.delegation_recipes": "id name description discipline trigger_event_category allowed_schedules required_skill_ids_json required_skill_codes_json primary_personnel_group_id primary_user_id primary_user_name on_call_fallback_user_id on_call_fallback_user_name escalation_group_id escalation_timeout_minutes is_active created_at updated_at technology is_technology_primary is_complete_machine_dedication maintenance_group_ids_json machine_group_ids_json".split(),
    "backend.delegation_maintenance_groups": "id delegation_recipe_id maintenance_group_id created_at".split(),
    "backend.delegation_machine_groups": "id delegation_recipe_id machine_group_id created_at".split(),
    "backend.machine_delegation_bindings": "id machine_id delegation_recipe_id discipline is_active bound_at".split(),
    "backend.technician_rules": "id name technician_id technician_name technician_email scope_type target_id category_filter backup_technician_id backup_technician_name assigned_by_role created_at".split(),
    "backend.shift_absences": "id technician_id technician_name reason start_date end_date marked_by backup_technician_id backup_technician_name active".split(),
    "backend.generic_templates": "id template_type code name description category icon version extends_template_id tags schema_json default_values_json ui_config_json is_active is_builtin organization_id created_at updated_at".split(),
    "backend.maintenance_tickets": "id title description status priority equipment_id client_pc_id machine_id assigned_to created_by created_at resolved_at metadata organization_id pending_reason pending_details is_escalated escalation_reason escalated_at escalated_by escalation_closed_at escalation_closed_by change_history escalation_handover_state escalation_target external_operator_id external_operator_name is_line_stop issue_type line_stop_duration_minutes originator_type qr_scanned_at reaction_time_minutes reserved_by responsible_department started_at tags telemetry_snapshot affected_component_ids_json alarm_code controller_source plc_error_id target_component_bmk target_component_id telemetry_probes_json".split(),
    "backend.ticket_comments": "id maintenance_ticket_id author content created_at".split(),
    "backend.machine_service_logs": "id machine_id timestamp author entry_type summary details linked_ticket_id".split(),
    # Added by migration 20261109211514_AddLuaScripts. Optional: loaded only when the table exists.
    "backend.lua_scripts": "id name description trigger_type code is_active created_by created_at updated_at last_executed_at last_execution_success last_execution_error metadata_json".split(),
}
# Tables that may not exist yet on an older dev DB; skipped (with a notice) when absent.
OPTIONAL_TABLES = {"backend.lua_scripts"}

# Text columns that hold serialized JSON (the entity keeps them as string): the
# dataset writes them as JSON objects/arrays, the loader serializes them.
JSON_TEXT_COLUMNS = {
    "auth.organization": {"metadata"},
    "backend.machine_groups": {"machine_ids_json", "machine_types_json"},
    "backend.machine_components": {"attributes_json", "eplan_cad_ref_json"},
    "backend.delegation_recipes": {"required_skill_ids_json", "required_skill_codes_json", "maintenance_group_ids_json", "machine_group_ids_json"},
    "backend.generic_templates": {"schema_json", "default_values_json", "ui_config_json"},
    "backend.maintenance_tickets": {"change_history", "telemetry_snapshot", "telemetry_probes_json", "affected_component_ids_json"},
    "backend.lua_scripts": {"metadata_json"},
}
# Text columns that hold a comma separated list (MaintenanceTicket.Tags).
CSV_TEXT_COLUMNS = {"backend.maintenance_tickets": {"tags"}}

# Payload sections in FK-safe load order: (payload key, table, conflict columns)
LOAD_ORDER: list[tuple[str, str, list[str]]] = [
    ("organization", "auth.organization", ["id"]),
    ("users", "auth.user", ["id"]),
    ("accounts", "auth.account", ["id"]),
    ("members", "auth.member", ["id"]),
    ("manufacturers", "backend.manufacturers", ["id"]),
    ("suppliers", "backend.suppliers", ["id"]),
    ("machine_groups", "backend.machine_groups", ["id"]),
    ("controllers", "backend.client_pcs", ["id"]),
    ("machine_items", "backend.inventory_items", ["id"]),
    ("stations", "backend.stations", ["id"]),
    ("part_items", "backend.inventory_items", ["id"]),
    ("hardware_assets", "backend.hardware_assets", ["id"]),
    ("station_controllers", "backend.StationControllers", ["id"]),
    ("machine_components", "backend.machine_components", ["id"]),
    ("machine_group_memberships", "backend.machine_group_memberships", ["id"]),
    ("skill_definitions", "backend.skill_definitions", ["id"]),
    ("personnel_skills", "backend.personnel_skills", ["id"]),
    ("personnel_groups", "backend.personnel_groups", ["id"]),
    ("personnel_group_members", "backend.personnel_group_members", ["id"]),
    ("delegation_recipes", "backend.delegation_recipes", ["id"]),
    ("delegation_maintenance_groups", "backend.delegation_maintenance_groups", ["id"]),
    ("delegation_machine_groups", "backend.delegation_machine_groups", ["id"]),
    ("machine_delegation_bindings", "backend.machine_delegation_bindings", ["id"]),
    ("technician_rules", "backend.technician_rules", ["id"]),
    ("shift_absences", "backend.shift_absences", ["id"]),
    ("generic_templates", "backend.generic_templates", ["id"]),
    ("maintenance_tickets", "backend.maintenance_tickets", ["id"]),
    ("ticket_comments", "backend.ticket_comments", ["id"]),
    ("machine_service_logs", "backend.machine_service_logs", ["id"]),
    ("lua_scripts", "backend.lua_scripts", ["id"]),
]

# Values filled in when a row omits them (keeps reloads a true "reset to story").
ROW_DEFAULTS: dict[str, dict[str, Any]] = {
    "machine_items": {"organization_id": DEMO_ORG_ID, "equipment_status": "InMachine", "is_stock_item": False, "metadata": {}},
    "part_items": {"organization_id": DEMO_ORG_ID, "equipment_status": "InStorage", "is_stock_item": False, "metadata": {}},
    "stations": {"status": "active"},
    "machine_groups": {"machine_ids_json": [], "group_type": "Manual"},
    "maintenance_tickets": {
        "pending_reason": "None", "is_escalated": False, "escalation_handover_state": "None",
        "is_line_stop": False, "originator_type": "ManualUser", "issue_type": "Maintenance",
        "organization_id": DEMO_ORG_ID, "metadata": {},
    },
    "delegation_recipes": {"allowed_schedules": 7, "required_skill_ids_json": [], "required_skill_codes_json": [],
                           "escalation_timeout_minutes": 30, "is_active": True,
                           "maintenance_group_ids_json": [], "machine_group_ids_json": []},
    "generic_templates": {"is_active": True, "is_builtin": False, "tags": []},
}
AUTO_NOW_COLUMNS = ("created_at", "updated_at", "assigned_at", "joined_at", "certified_at", "installed_at", "bound_at")

# Enum-like string columns (EF HasConversion<string> or documented value sets).
ENUMS: dict[tuple[str, str], set[str]] = {
    ("maintenance_tickets", "status"): {"Open", "InProgress", "Pending", "Resolved", "Closed"},
    ("maintenance_tickets", "priority"): {"Low", "Medium", "High", "Critical"},
    ("maintenance_tickets", "pending_reason"): {"None", "Parts", "ExternalOk", "Action", "SignOff", "Custom"},
    ("maintenance_tickets", "escalation_handover_state"): {"None", "Notification", "HandOff", "ParallelWork", "HandedOffToField"},
    ("maintenance_tickets", "originator_type"): {"ManualUser", "MachineAutomatic", "ScheduledMaintenance"},
    ("maintenance_tickets", "issue_type"): {"Transient", "Maintenance", "Improvement", "Other"},
    ("machine_groups", "group_type"): {"BusinessUnit", "Project", "Line", "SubLine", "Machine", "Technological", "Customer", "Manual"},
    ("skill_definitions", "category"): {"Certification", "TechnologyKnowHow", "MachineAuthorization", "SafetyClearance"},
    ("personnel_groups", "discipline"): {"Mechanical", "Controls", "Electrical", "Safety", "Quality", "Process", "Tooling", "General"},
    ("delegation_recipes", "discipline"): {"Mechanical", "Controls", "Electrical", "Safety", "Quality", "Process", "Tooling", "General"},
    ("machine_delegation_bindings", "discipline"): {"Mechanical", "Controls", "Electrical", "Safety", "Quality", "Process", "Tooling", "General"},
    ("machine_components", "category"): {"Hardware", "SoftwareBinary", "SoftwareRuntime", "SoftwareConfig", "SoftwareProgram", "SoftwareRecipe", "License", "Document"},
    ("machine_items", "equipment_status"): {"InStorage", "InMachine", "UnderRepair", "Decommissioned", "Scrapped"},
    ("part_items", "equipment_status"): {"InStorage", "InMachine", "UnderRepair", "Decommissioned", "Scrapped"},
    ("stations", "status"): {"active", "decommissioned", "dismantled"},
    ("technician_rules", "scope_type"): {"technology", "group", "machine"},
    ("shift_absences", "reason"): {"Sick", "Emergency", "Vacation", "Training", "Unplanned"},
    ("generic_templates", "template_type"): {"machine", "component", "delegation", "telemetry", "ticket"},
    ("users", "role"): {"system_admin", "heimdall_admin", "admin", "plant_director", "it_admin", "it_site_admin", "engineering_admin",
                        "plant_engineering_manager", "senior_engineering_manager", "group_leader", "shift_leader", "lead_engineer",
                        "controls_engineer", "engineer", "technician", "operative_planner", "operator", "manager", "user"},
    ("machine_service_logs", "entry_type"): {"RoutineService", "ShiftHandover", "PartReplacement", "Calibration", "EmergencyRepair", "Note"},
}
SLA_HOURS = {"Critical": 2, "High": 4, "Medium": 8, "Low": 24}

STATION_COLUMNS = set(TABLE_COLUMNS["backend.stations"])
USER_ID_FIELDS = {"user_id", "technician_id", "backup_technician_id", "primary_user_id", "on_call_fallback_user_id",
                  "spoc_user_id", "owner_user_id", "lead_engineer_id", "preferred_technician_id", "assigned_by",
                  "external_operator_id"}


class DemoError(Exception):
    pass


# ======================================================================================
# Small helpers
# ======================================================================================
def info(msg: str) -> None:
    print(f"[demo-seed] {msg}")


def warn(msg: str) -> None:
    print(f"[demo-seed] WARNING: {msg}", file=sys.stderr)


def demo_uuid(key: str) -> str:
    """Deterministic UUID for a symbolic key; first group is the demo marker."""
    h = uuid.uuid5(DEMO_NAMESPACE, key).hex
    return f"{DEMO_ID_PREFIX}-{h[8:12]}-{h[12:16]}-{h[16:20]}-{h[20:32]}"


def iso(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"


def parse_rel_time(token: str, now: datetime) -> str:
    m = TIME_RE.match(token)
    if not m:
        raise DemoError(f"bad relative time token {token!r}")
    sign, parts = m.group(1), m.group(2)
    delta = timedelta()
    if parts:
        for amount, unit in TIME_PART_RE.findall(parts):
            n = int(amount)
            delta += {"d": timedelta(days=n), "h": timedelta(hours=n), "m": timedelta(minutes=n), "s": timedelta(seconds=n)}[unit]
    return iso(now - delta if sign == "-" else now + delta)


def quote_ident(name: str) -> str:
    if not SAFE_IDENT.match(name):
        raise DemoError(f"refusing unsafe identifier {name!r}")
    return '"' + name + '"'


def quote_table(qualified: str) -> str:
    schema, table = qualified.split(".", 1)
    return f"{quote_ident(schema)}.{quote_ident(table)}"


def read_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.exists():
        return values
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, val = line.partition("=")
        val = val.strip()
        if len(val) >= 2 and val[0] == val[-1] and val[0] in "'\"":
            val = val[1:-1]
        values[key.strip()] = val
    return values


def truthy(val: str | None) -> bool:
    return str(val or "").strip().lower() in {"1", "true", "yes", "on"}


# ======================================================================================
# Telemetry generator (deterministic; writes seed_data/demo/telemetry.json)
# ======================================================================================
def _series(points: range, fn, seed: str, noise: float, digits: int = 3) -> list[list[float]]:
    rng = random.Random(seed)
    out = []
    for t in points:
        v = fn(t)
        if v is None:
            continue  # gap (e.g. line stop -> no samples)
        out.append([t, round(v + rng.uniform(-noise, noise), digits)])
    return out


def _lerp(t: float, t0: float, v0: float, t1: float, v1: float) -> float:
    return v0 + (v1 - v0) * (t - t0) / (t1 - t0)


def build_telemetry() -> dict[str, Any]:
    step = 5
    window = range(-355, 1, step)  # 72 samples, last 6 hours

    def laser_power(t):  # NV-L2-OP060: drift -> alarm -> line stop -> cleaning -> drifts again
        if t < -240: return 6.00
        if t <= -175: return _lerp(t, -240, 6.00, -175, 5.66)
        if t < -130: return None                    # 45 min line stop
        if t <= -80: return _lerp(t, -130, 5.92, -80, 5.75)
        return _lerp(t, -80, 5.75, 0, 5.62)          # reduced speed, still drifting

    def glass_temp(t):
        if t < -240: return 33.0
        if t <= -175: return _lerp(t, -240, 33.0, -175, 52.0)
        if t < -130: return None
        if t <= -80: return _lerp(t, -130, 41.0, -80, 48.5)
        return _lerp(t, -80, 48.5, 0, 53.8)          # approaching the 55 C alarm

    def penetration(t):
        if t < -240: return 2.42
        if t <= -175: return _lerp(t, -240, 2.42, -175, 1.95)
        if t < -130: return None
        if t <= -80: return _lerp(t, -130, 2.30, -80, 2.18)
        return _lerp(t, -80, 2.18, 0, 2.06)

    def resin_flow(t):  # NV-L1-OP030: partially clogged mixer nozzle
        if t < -120: return 14.0
        if t <= -60: return _lerp(t, -120, 14.0, -60, 12.0)
        return _lerp(t, -60, 11.8, 0, 11.55)

    def spindle_vib(t):  # NV-L3-OP020: slow bearing wear, toward the 4.5 mm/s alarm
        return _lerp(t, -355, 3.18, 0, 3.42)

    stations = {
        "NV-L2-OP060": {"handle": "L02-OP060", "metrics": {
            "laser_power_kw": {"unit": "kW", "nominalLow": 5.85, "nominalHigh": 6.15, "alarmLow": 5.7, "points": _series(window, laser_power, "l2-60-p", 0.015)},
            "protective_glass_temp_c": {"unit": "C", "nominalLow": 25.0, "nominalHigh": 50.0, "alarmHigh": 55.0, "points": _series(window, glass_temp, "l2-60-t", 0.4, 1)},
            "weld_penetration_mm": {"unit": "mm", "nominalLow": 2.1, "nominalHigh": 2.6, "alarmLow": 2.1, "points": _series(window, penetration, "l2-60-w", 0.02)},
        }},
        "NV-L1-OP060": {"handle": "L01-OP060", "metrics": {
            "laser_power_kw": {"unit": "kW", "nominalLow": 5.85, "nominalHigh": 6.15, "alarmLow": 5.7, "points": _series(window, lambda t: 6.0, "l1-60-p", 0.02)},
            "protective_glass_temp_c": {"unit": "C", "nominalLow": 25.0, "nominalHigh": 50.0, "alarmHigh": 55.0, "points": _series(window, lambda t: 33.5, "l1-60-t", 0.5, 1)},
            "weld_penetration_mm": {"unit": "mm", "nominalLow": 2.1, "nominalHigh": 2.6, "alarmLow": 2.1, "points": _series(window, lambda t: 2.41, "l1-60-w", 0.02)},
        }},
        "NV-L1-OP030": {"handle": "L01-OP030", "metrics": {
            "resin_flow_ml_min": {"unit": "ml/min", "nominalLow": 12.5, "nominalHigh": 15.5, "alarmLow": 12.0, "points": _series(window, resin_flow, "l1-30-f", 0.08, 2)},
        }},
        "NV-L3-OP020": {"handle": "L03-OP020", "metrics": {
            "spindle_vibration_mm_s": {"unit": "mm/s", "nominalLow": 0.0, "nominalHigh": 4.0, "alarmHigh": 4.5, "points": _series(window, spindle_vib, "l3-20-v", 0.05, 2)},
            "spindle_temp_c": {"unit": "C", "nominalLow": 20.0, "nominalHigh": 55.0, "alarmHigh": 65.0, "points": _series(window, lambda t: 38.0 + 1.5 * math.sin(t / 40.0), "l3-20-t", 0.3, 1)},
            "runtime_hours": {"unit": "h", "nominalLow": 0.0, "nominalHigh": 1500.0, "alarmHigh": 1500.0, "points": [[0, 1460.0]]},
        }},
        "NV-L2-OP070": {"handle": "L02-OP070", "metrics": {
            "cycle_time_s": {"unit": "s", "nominalLow": 40.0, "nominalHigh": 44.0, "alarmHigh": 46.0, "points": _series(window, lambda t: 42.0 + 0.6 * math.sin(t / 25.0), "l2-70-c", 0.4, 1)},
        }},
    }
    return {
        "$comment": "GENERATED by `scripts/seed_demo_data.py --write-telemetry` (deterministic). Points are [offset_minutes_from_load_time, value]; gaps mean no samples (line stop). Persisted into tickets (telemetry_probes_json / telemetry_snapshot) and replayed into the backend's in-memory Lua telemetry buffer with --push-runtime.",
        "intervalMinutes": step,
        "stations": stations,
    }


# ======================================================================================
# Dataset loading, resolution & validation
# ======================================================================================
class Dataset:
    def __init__(self, now: datetime, with_logins: bool, demo_password: str | None,
                 live_columns: dict[str, set[str]] | None = None):
        # live_columns (from information_schema) wins over the static allow-list, so that
        # auto-filled timestamp columns follow the real table (e.g. created_at vs assigned_at).
        self.live_columns = live_columns
        self.now = now
        self.with_logins = with_logins
        self.demo_password = demo_password
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.keys: dict[str, str] = {}          # symbolic key -> uuid
        self.payload: dict[str, list[dict[str, Any]]] = {}
        self.template_extends: list[dict[str, str]] = []
        self.telemetry: dict[str, Any] = {}
        self.lua: dict[str, Any] = {}

    # ---------- loading ----------
    @staticmethod
    def _load(name: str) -> dict[str, Any]:
        path = DEMO_DIR / name
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except FileNotFoundError:
            raise DemoError(f"missing dataset file {path}")
        except json.JSONDecodeError as exc:
            raise DemoError(f"{path}: invalid JSON ({exc})")

    def build(self) -> "Dataset":
        plant = self._load("plant.json")
        people = self._load("people.json")
        tickets = self._load("tickets.json")
        templates = self._load("templates.json")
        self.telemetry = self._load("telemetry.json")
        self.lua = self._load("lua/manifest.json")

        sections: dict[str, list[dict[str, Any]]] = {}
        for src in (plant, people, tickets, templates):
            for k, v in src.items():
                if k.startswith("$"):
                    continue
                if k in sections:
                    self.errors.append(f"section {k!r} defined twice")
                sections[k] = copy.deepcopy(v)

        # 1) register keys -> ids (duplicate detection)
        for name, rows in sections.items():
            for row in rows:
                key = row.get("_key")
                if key is None:
                    continue
                if key in self.keys:
                    self.errors.append(f"duplicate _key {key!r}")
                self.keys[key] = demo_uuid(key)
        for script in self.lua.get("scripts", []):
            self.keys[script["_key"]] = demo_uuid(script["_key"])

        # 2) resolve refs/time tokens, assign ids
        for name, rows in sections.items():
            for row in rows:
                key = row.pop("_key", None)
                if key is not None:
                    row["id"] = self.keys[key]
                for col in list(row.keys()):
                    row[col] = self._resolve(row[col], f"{name}.{col}")
        self._expand_telemetry(sections.get("maintenance_tickets", []))

        # 3) shape sections into table payloads
        self._shape(sections)
        self._validate(sections)
        return self

    def table_columns(self, table: str) -> list[str]:
        if self.live_columns and table in self.live_columns:
            return sorted(self.live_columns[table])
        return TABLE_COLUMNS[table]

    def _resolve(self, value: Any, where: str) -> Any:
        if isinstance(value, str):
            if value.startswith("@"):
                m = REF_RE.match(value)
                if not m or m.group(1) not in self.keys:
                    self.errors.append(f"{where}: unresolved reference {value!r}")
                    return value
                return self.keys[m.group(1)]
            if value.startswith("~now"):
                try:
                    return parse_rel_time(value, self.now)
                except DemoError as exc:
                    self.errors.append(f"{where}: {exc}")
                    return value
            return value
        if isinstance(value, list):
            return [self._resolve(v, where) for v in value]
        if isinstance(value, dict):
            return {k: self._resolve(v, f"{where}.{k}") for k, v in value.items()}
        return value

    def _series_points(self, station: str, metric: str) -> tuple[dict[str, Any], list[tuple[str, float]]]:
        st = self.telemetry.get("stations", {}).get(station)
        if not st or metric not in st.get("metrics", {}):
            self.errors.append(f"telemetry for {station}/{metric} not found in telemetry.json")
            return {}, []
        spec = st["metrics"][metric]
        pts = [(iso(self.now + timedelta(minutes=off)), float(val)) for off, val in spec.get("points", [])]
        return spec, pts

    def _expand_telemetry(self, tickets: list[dict[str, Any]]) -> None:
        for t in tickets:
            snap = t.get("telemetry_snapshot")
            if isinstance(snap, dict) and "$latestTelemetry" in snap:
                station = snap["$latestTelemetry"]["station"]
                st = self.telemetry.get("stations", {}).get(station, {})
                metrics = {}
                last_ts = None
                for metric in st.get("metrics", {}):
                    _, pts = self._series_points(station, metric)
                    if pts:
                        # latest sample at or before the ticket's creation moment
                        created = t.get("created_at", iso(self.now))
                        before = [p for p in pts if p[0] <= created] or pts[:1]
                        metrics[metric] = before[-1][1]
                        last_ts = before[-1][0]
                t["telemetry_snapshot"] = {"timestamp": last_ts or iso(self.now), "source": station, "metrics": metrics}
            probes = t.get("telemetry_probes_json")
            if isinstance(probes, dict) and "$fromTelemetry" in probes:
                spec_in = probes["$fromTelemetry"]
                station, n = spec_in["station"], int(spec_in.get("bufferPoints", 24))
                out = []
                for i, metric in enumerate(spec_in["metrics"], start=1):
                    spec, pts = self._series_points(station, metric)
                    if not pts:
                        continue
                    last_ts, last_val = pts[-1]
                    out.append({  # App.Contracts.Telemetry.TicketTelemetryProbe (System.Text.Json default casing)
                        "ProbeId": f"{station}:{metric}",
                        "DatapointKey": f"{station}.{metric}",
                        "MetricName": metric,
                        "NumericValue": last_val,
                        "StringValue": None,
                        "Unit": spec.get("unit"),
                        "NominalLow": spec.get("nominalLow"),
                        "NominalHigh": spec.get("nominalHigh"),
                        "TriggerStatus": "ThresholdExceeded" if self._breaches(spec, last_val) else "WithinLimits",
                        "SampledAt": last_ts,
                        "ProbeBuffer": [{"Timestamp": ts, "Value": v} for ts, v in pts[-n:]],
                    })
                t["telemetry_probes_json"] = out

    @staticmethod
    def _breaches(spec: dict[str, Any], v: float) -> bool:
        lo, hi = spec.get("alarmLow"), spec.get("alarmHigh")
        return (lo is not None and v < lo) or (hi is not None and v > hi)

    def _shape(self, s: dict[str, list[dict[str, Any]]]) -> None:
        p = self.payload
        p["organization"] = s.get("organization", [])
        users, members, accounts = [], [], []
        for u in s.get("users", []):
            org_role = u.pop("org_role", "member")
            u.pop("title", None)
            u.setdefault("email_verified", True)
            users.append(u)
            members.append({"id": f"{DEMO_TEXT_ID_PREFIX}mem-{u['id']}", "organization_id": DEMO_ORG_ID, "user_id": u["id"], "role": org_role})
            if self.with_logins:
                accounts.append({"id": f"{DEMO_TEXT_ID_PREFIX}acc-{u['id']}", "account_id": u["id"], "provider_id": "credential",
                                 "user_id": u["id"], "password": better_auth_scrypt_hash(self.demo_password or DEFAULT_DEMO_PASSWORD),
                                 "issuer": "local:credential"})
        p["users"], p["members"], p["accounts"] = users, members, accounts

        for name in ("manufacturers", "suppliers", "machine_groups", "controllers", "station_controllers", "machine_components",
                     "machine_group_memberships", "skill_definitions", "personnel_skills", "personnel_groups",
                     "personnel_group_members", "delegation_recipes", "delegation_maintenance_groups",
                     "delegation_machine_groups", "machine_delegation_bindings", "technician_rules", "shift_absences",
                     "maintenance_tickets", "ticket_comments", "machine_service_logs"):
            p[name] = s.get(name, [])

        machine_items, stations = [], []
        for m in s.get("machines", []):
            item = {k: v for k, v in m.items() if k not in STATION_COLUMNS or k == "id"}
            st = {k: v for k, v in m.items() if k in STATION_COLUMNS}
            machine_items.append(item)
            stations.append(st)
        p["machine_items"], p["stations"] = machine_items, stations
        p["part_items"] = s.get("parts", [])
        p["hardware_assets"] = [{"id": part["id"]} for part in p["part_items"]]

        tmpls = []
        for t in s.get("generic_templates", []):
            ext = t.pop("extends_code", None)
            if ext:
                self.template_extends.append({"code": t["code"], "extends_code": ext})
            tmpls.append(t)
        p["generic_templates"] = tmpls

        # Lua automation scripts (persisted in backend.lua_scripts since the AddLuaScripts migration)
        p["lua_scripts"] = [{
            "id": self.keys[sc["_key"]], "name": sc["name"], "description": sc["description"],
            "trigger_type": int(sc["triggerType"]), "code": (LUA_DIR / sc["file"]).read_text(encoding="utf-8"),
            "is_active": bool(sc["isActive"]), "created_by": "demo-seed", "metadata_json": sc.get("metadata") or None,
        } for sc in self.lua.get("scripts", []) if (LUA_DIR / sc["file"]).exists()]

        for t in p["maintenance_tickets"]:
            meta = t.setdefault("metadata", {})
            hours = meta.get("slaHours", SLA_HOURS.get(t.get("priority", ""), 8))
            meta["slaHours"] = hours
            if "created_at" in t:
                created = datetime.fromisoformat(t["created_at"].replace("Z", "+00:00"))
                meta["slaDueAt"] = iso(created + timedelta(hours=hours))

        # defaults, auto timestamps, JSON/CSV serialization, uniform key sets
        for key, table, _ in LOAD_ORDER:
            rows = p.get(key, [])
            cols = self.table_columns(table)
            for row in rows:
                for col, default in ROW_DEFAULTS.get(key, {}).items():
                    row.setdefault(col, copy.deepcopy(default))
                for col in AUTO_NOW_COLUMNS:
                    if col in cols and col not in row:
                        row[col] = iso(self.now)
                for col in JSON_TEXT_COLUMNS.get(table, ()):
                    if col in row and row[col] is not None and not isinstance(row[col], str):
                        row[col] = json.dumps(row[col], separators=(",", ":"))
                for col in CSV_TEXT_COLUMNS.get(table, ()):
                    if isinstance(row.get(col), list):
                        row[col] = ",".join(row[col])
            union = sorted({c for r in rows for c in r})
            for row in rows:
                for c in union:
                    row.setdefault(c, None)

    def _validate(self, s: dict[str, list[dict[str, Any]]]) -> None:
        p = self.payload
        user_ids = {u["id"] for u in p["users"]}
        ids_by_table: dict[str, set[str]] = {}
        for key, table, _ in LOAD_ORDER:
            if self.live_columns is not None and table not in self.live_columns:
                continue  # optional table absent in the target DB
            cols = set(self.table_columns(table))
            seen: set[str] = ids_by_table.setdefault(f"{table}:{key}", set())
            for i, row in enumerate(p.get(key, [])):
                unknown = set(row) - cols
                if unknown:
                    self.errors.append(f"{key}[{i}] ({table}): unknown column(s) {sorted(unknown)}")
                rid = row.get("id")
                if rid in seen:
                    self.errors.append(f"{key}: duplicate id {rid}")
                seen.add(rid)
                if table.startswith("backend.") and rid and not str(rid).startswith(DEMO_ID_PREFIX):
                    self.errors.append(f"{key}[{i}]: id {rid} lacks demo prefix")
                if table.startswith("auth.") and rid and not str(rid).startswith(DEMO_TEXT_ID_PREFIX):
                    self.errors.append(f"{key}[{i}]: auth id {rid} lacks '{DEMO_TEXT_ID_PREFIX}' prefix")
                for (sect, col), allowed in ENUMS.items():
                    if sect == key and row.get(col) is not None and row[col] not in allowed:
                        self.errors.append(f"{key}[{i}].{col}={row[col]!r} not in {sorted(allowed)}")
                for col in USER_ID_FIELDS & set(row):
                    v = row[col]
                    if v and isinstance(v, str) and v.startswith("demo-usr-") and v not in user_ids:
                        self.errors.append(f"{key}[{i}].{col}: unknown demo user {v}")
                for col, v in row.items():
                    if isinstance(v, str) and (v.startswith("@") and REF_RE.match(v) or v.startswith("~now")):
                        self.errors.append(f"{key}[{i}].{col}: unresolved token {v}")
                    if isinstance(v, (int, float)) and not isinstance(v, bool) and col in {"stock_quantity", "min_stock_threshold", "cost_in_huf", "line_stop_duration_minutes", "reaction_time_minutes", "escalation_timeout_minutes", "lead_time_days"} and v < 0:
                        self.errors.append(f"{key}[{i}].{col}: negative value {v}")

        # emails & PII guard: only reserved example domains
        for u in p["users"]:
            if not re.search(r"@[a-z0-9.-]+\.(example\.com|example\.org|example\.net|test)$", u["email"]):
                self.errors.append(f"user {u['id']}: email {u['email']} must use an example.com/.test domain")
        for sup in p["suppliers"]:
            if sup.get("email") and not sup["email"].endswith(".example.com"):
                self.errors.append(f"supplier {sup['name']}: email must be *.example.com")

        # FK-ish integrity for uuid references
        all_ids = set(self.keys.values())
        uuid_cols = {"parent_id", "machine_id", "client_pc_id", "group_id", "line_group_id", "sub_line_group_id", "project_group_id",
                     "business_unit_group_id", "parent_machine_id", "manufacturer_id", "supplier_id", "current_part_id",
                     "skill_id", "personnel_group_id", "primary_personnel_group_id", "escalation_group_id", "delegation_recipe_id",
                     "maintenance_group_id", "machine_group_id", "maintenance_ticket_id", "linked_ticket_id", "target_component_id"}
        for key, table, _ in LOAD_ORDER:
            for i, row in enumerate(p.get(key, [])):
                for col in uuid_cols & set(row):
                    v = row[col]
                    if v and v not in all_ids:
                        self.errors.append(f"{key}[{i}].{col}: {v} does not reference a demo row")

        # semantic checks: time ordering, ticket state consistency, pinned handles
        now_s = iso(self.now)
        for t in p["maintenance_tickets"]:
            tag = f"ticket {t['title'][:40]!r}"
            if t["created_at"] > now_s:
                self.errors.append(f"{tag}: created_at in the future")
            for col in ("started_at", "resolved_at", "escalated_at", "escalation_closed_at", "qr_scanned_at"):
                if t.get(col) and t[col] < t["created_at"]:
                    self.errors.append(f"{tag}: {col} before created_at")
                if t.get(col) and t[col] > now_s:
                    self.errors.append(f"{tag}: {col} in the future")
            st = t["status"]
            if st in ("Resolved", "Closed") and not t.get("resolved_at"):
                self.errors.append(f"{tag}: {st} without resolved_at")
            if st not in ("Resolved", "Closed") and t.get("resolved_at"):
                self.errors.append(f"{tag}: unresolved ticket has resolved_at")
            if st == "Pending" and t.get("pending_reason") in (None, "None"):
                self.errors.append(f"{tag}: Pending without pending_reason")
            if st != "Pending" and t.get("pending_reason") not in (None, "None"):
                self.errors.append(f"{tag}: pending_reason on non-Pending ticket")
            if t.get("is_escalated") and not (t.get("escalated_at") and t.get("escalation_reason")):
                self.errors.append(f"{tag}: escalated without escalated_at/escalation_reason")
        statuses = {t["status"] for t in p["maintenance_tickets"]}
        missing = ENUMS[("maintenance_tickets", "status")] - statuses
        if missing:
            self.errors.append(f"tickets do not cover states {sorted(missing)}")
        handles = [st["pinned_object_handle"] for st in p["stations"] if st.get("pinned_object_handle")]
        dup = {h for h in handles if handles.count(h) > 1}
        if dup:
            self.errors.append(f"duplicate pinned_object_handle(s) {sorted(dup)}")
        for c in p["ticket_comments"]:
            parent = next((t for t in p["maintenance_tickets"] if t["id"] == c["maintenance_ticket_id"]), None)
            if parent and c["created_at"] < parent["created_at"]:
                self.errors.append(f"comment {c['id']} predates its ticket")
        for ps in p["personnel_skills"]:
            if ps.get("expires_at") and ps["expires_at"] < ps["certified_at"]:
                self.errors.append(f"personnel skill {ps['id']}: expires before certified")
        for a in p["shift_absences"]:
            if a["end_date"] < a["start_date"]:
                self.errors.append(f"absence {a['id']}: end before start")
        for m in p["machine_items"] + p["part_items"]:
            if m.get("is_stock_item") and m.get("stock_quantity") is None:
                self.errors.append(f"stock item {m['name']} without stock_quantity")
        for sc in p["station_controllers"]:
            if sc["machine_id"] not in {st["id"] for st in p["stations"]}:
                self.errors.append(f"station controller {sc['id']} references unknown station")

        # lua files exist
        for script in self.lua.get("scripts", []):
            if not (LUA_DIR / script["file"]).exists():
                self.errors.append(f"lua script file missing: {script['file']}")
        if not (LUA_DIR / self.lua.get("replay", {}).get("file", "replay_telemetry.lua")).exists():
            self.errors.append("lua replay helper missing")

    def counts(self) -> dict[str, int]:
        return {k: len(v) for k, v in self.payload.items() if v}


def better_auth_scrypt_hash(password: str) -> str:
    """Better-Auth's default credential hash: scrypt(N=16384,r=16,p=1,dkLen=64), 'saltHex:keyHex'."""
    salt = secrets.token_hex(16)
    key = hashlib.scrypt(password.encode("utf-8"), salt=salt.encode("utf-8"), n=16384, r=16, p=1, dklen=64,
                         maxmem=128 * 16384 * 16 * 2)
    return f"{salt}:{key.hex()}"


# ======================================================================================
# SQL generation (static statements; data comes from the _demo_payload document)
# ======================================================================================
def build_statements(ds: Dataset, live_columns: dict[str, set[str]] | None, reset: bool,
                     unpin_conflicts: bool) -> list[str]:
    stmts: list[str] = []
    if reset:
        stmts.extend(reset_statements(live_columns))
    for key, table, conflict in LOAD_ORDER:
        rows = ds.payload.get(key) or []
        if not rows:
            continue
        if live_columns is not None and table not in live_columns:
            if table in OPTIONAL_TABLES:
                continue
            raise DemoError(f"{table}: table missing in the target database")
        cols = sorted(rows[0].keys())
        allowed = live_columns.get(table) if live_columns else set(TABLE_COLUMNS[table])
        bad = [c for c in cols if c not in allowed]
        if bad:
            raise DemoError(f"{table}: column(s) {bad} not present in the target schema (schema drift)")
        col_list = ", ".join(quote_ident(c) for c in cols)
        updates = ", ".join(f"{quote_ident(c)} = EXCLUDED.{quote_ident(c)}" for c in cols if c not in conflict)
        on_conflict = f"DO UPDATE SET {updates}" if updates else "DO NOTHING"
        if not SAFE_IDENT.match(key):
            raise DemoError(f"unsafe payload key {key!r}")
        stmts.append(
            f"INSERT INTO {quote_table(table)} ({col_list})\n"
            f"SELECT {col_list} FROM jsonb_populate_recordset(NULL::{quote_table(table)}, (SELECT doc -> '{key}' FROM _demo_payload))\n"
            f"ON CONFLICT ({', '.join(quote_ident(c) for c in conflict)}) {on_conflict};"
        )
    # template inheritance by code (builtin templates are seeded by the backend)
    stmts.append(
        "UPDATE backend.generic_templates AS t SET extends_template_id = b.id\n"
        "FROM jsonb_to_recordset((SELECT doc -> 'template_extends' FROM _demo_payload)) AS x(code text, extends_code text)\n"
        "JOIN backend.generic_templates AS b ON b.code = x.extends_code\n"
        "WHERE t.code = x.code;"
    )
    if unpin_conflicts:
        stmts.append(
            "UPDATE backend.stations AS s SET pinned_object_handle = NULL\n"
            "WHERE s.id::text NOT LIKE 'de30de30-%' AND s.pinned_object_handle IN (\n"
            "  SELECT x ->> 'pinned_object_handle' FROM jsonb_array_elements((SELECT doc -> 'stations' FROM _demo_payload)) AS x\n"
            "  WHERE x ->> 'pinned_object_handle' IS NOT NULL);"
        )
    return stmts


def reset_statements(live_columns: dict[str, set[str]] | None = None) -> list[str]:
    """Delete only demo-owned rows (id prefix), children first."""
    out = []
    demo_like = f"'{DEMO_ID_PREFIX}-%'"
    out.append(f"DELETE FROM backend.maintenance_tickets WHERE id::text LIKE {demo_like} OR machine_id::text LIKE {demo_like};")
    for _, table, _ in reversed(LOAD_ORDER):
        if live_columns is not None and table not in live_columns:
            continue
        if table in ("backend.maintenance_tickets", "backend.hardware_assets", "backend.stations") or table.startswith("auth."):
            continue  # tickets above; TPT children cascade from inventory_items
        out.append(f"DELETE FROM {quote_table(table)} WHERE id::text LIKE {demo_like};")
    out.append(f"DELETE FROM auth.\"user\" WHERE id LIKE '{DEMO_TEXT_ID_PREFIX}usr-%';")
    out.append(f"DELETE FROM auth.organization WHERE id = '{DEMO_ORG_ID}';")
    # dedupe repeated statements while keeping order
    seen, uniq = set(), []
    for s in out:
        if s not in seen:
            seen.add(s)
            uniq.append(s)
    return uniq


def payload_document(ds: Dataset) -> str:
    doc = dict(ds.payload)
    doc["template_extends"] = ds.template_extends
    return json.dumps(doc, ensure_ascii=True, separators=(",", ":"), sort_keys=True)


def copy_escape(text: str) -> str:
    """Escape for PostgreSQL COPY text format (json.dumps output has no raw newlines/tabs)."""
    return text.replace("\\", "\\\\").replace("\r", "\\r").replace("\n", "\\n").replace("\t", "\\t")


def psql_script(ds: Dataset, statements: list[str], commit: bool) -> str:
    parts = [
        "\\set ON_ERROR_STOP on",
        "SET client_encoding = 'UTF8';",
        "BEGIN;",
        "CREATE TEMP TABLE _demo_payload (doc jsonb) ON COMMIT DROP;",
        "COPY _demo_payload (doc) FROM STDIN;",
        copy_escape(payload_document(ds)),
        "\\.",
    ]
    parts.extend(statements)
    parts.append("SELECT 'demo-seed-ok';")
    parts.append("COMMIT;" if commit else "ROLLBACK;")
    return "\n".join(parts) + "\n"


# ======================================================================================
# Database transports
# ======================================================================================
INTROSPECT_SQL = (
    "SELECT table_schema || '.' || table_name, column_name FROM information_schema.columns "
    "WHERE table_schema IN ('backend', 'auth') ORDER BY 1, ordinal_position"
)
CONFLICT_SQL = (
    "SELECT s.custom_identifier, s.pinned_object_handle FROM backend.stations s "
    "WHERE s.id::text NOT LIKE 'de30de30-%' AND s.pinned_object_handle = ANY(string_to_array(:'handles', ','))"
)


class DockerPsql:
    def __init__(self, container: str, user: str, db: str):
        self.container, self.user, self.db = container, user, db

    def describe(self) -> str:
        return f"docker exec {self.container} psql -U {self.user} -d {self.db}"

    def _run(self, args: list[str], stdin: str | None = None) -> str:
        cmd = ["docker", "exec", "-i", self.container, "psql", "-X", "-v", "ON_ERROR_STOP=1", "-U", self.user, "-d", self.db] + args
        res = subprocess.run(cmd, input=stdin, capture_output=True, text=True)
        if res.returncode != 0:
            raise DemoError(f"psql failed (exit {res.returncode}):\n{res.stderr.strip() or res.stdout.strip()}")
        return res.stdout

    def columns(self) -> dict[str, set[str]]:
        out: dict[str, set[str]] = {}
        for line in self._run(["-At", "-F", "\t", "-c", INTROSPECT_SQL]).splitlines():
            if "\t" in line:
                t, c = line.split("\t", 1)
                out.setdefault(t, set()).add(c)
        return out

    def handle_conflicts(self, handles: list[str]) -> list[tuple[str, str]]:
        # psql variable interpolation (:'handles') is client-side literal quoting via PQescapeLiteral
        out = self._run(["-At", "-F", "\t", "-v", "handles=" + ",".join(handles)], stdin=CONFLICT_SQL + ";\n")
        return [tuple(line.split("\t", 1)) for line in out.splitlines() if "\t" in line]  # type: ignore[misc]

    def execute(self, ds: Dataset, statements: list[str], commit: bool) -> None:
        out = self._run(["-q", "-At"], stdin=psql_script(ds, statements, commit))
        if "demo-seed-ok" not in out:
            raise DemoError(f"unexpected psql output:\n{out}")


class Psycopg:
    def __init__(self, dsn: str):
        import psycopg  # type: ignore  # optional dependency
        self.psycopg = psycopg
        self.dsn = dsn

    def describe(self) -> str:
        return "psycopg " + re.sub(r"(password=)[^ ;]+|(:)[^:@/]+(@)", r"\1\2***\3", self.dsn)

    def columns(self) -> dict[str, set[str]]:
        out: dict[str, set[str]] = {}
        with self.psycopg.connect(self.dsn) as conn:
            for t, c in conn.execute(INTROSPECT_SQL).fetchall():
                out.setdefault(t, set()).add(c)
        return out

    def handle_conflicts(self, handles: list[str]) -> list[tuple[str, str]]:
        with self.psycopg.connect(self.dsn) as conn:
            return conn.execute(
                "SELECT custom_identifier, pinned_object_handle FROM backend.stations "
                "WHERE id::text NOT LIKE 'de30de30-%%' AND pinned_object_handle = ANY(%s)", (handles,)).fetchall()

    def execute(self, ds: Dataset, statements: list[str], commit: bool) -> None:
        with self.psycopg.connect(self.dsn) as conn:
            with conn.transaction(force_rollback=not commit):
                conn.execute("CREATE TEMP TABLE _demo_payload (doc jsonb) ON COMMIT DROP")
                conn.execute("INSERT INTO _demo_payload (doc) VALUES (%s::jsonb)", (payload_document(ds),))
                for stmt in statements:
                    conn.execute(stmt)  # static SQL, no client-side parameters


def npgsql_to_dsn(conn_str: str) -> str | None:
    parts = dict(p.split("=", 1) for p in conn_str.split(";") if "=" in p)
    norm = {k.strip().lower(): v.strip() for k, v in parts.items()}
    if "host" not in norm:
        return None
    return " ".join(f"{k}={norm[src]}" for k, src in (("host", "host"), ("port", "port"), ("dbname", "database"),
                                                         ("user", "username"), ("password", "password")) if src in norm)


def docker_container_running(name: str) -> bool:
    if not shutil.which("docker"):
        return False
    res = subprocess.run(["docker", "ps", "--filter", f"name=^{name}$", "--format", "{{.Names}}"], capture_output=True, text=True)
    return res.returncode == 0 and name in res.stdout.split()


# ======================================================================================
# Guards
# ======================================================================================
def enforce_dev_guard(env: dict[str, str], db_name: str) -> None:
    if not truthy(env.get("HEIMDALL_ENABLE_DEV")):
        raise DemoError("refusing to run: HEIMDALL_ENABLE_DEV is not 'true' (set it in .env.dev or the environment). "
                        "The demo dataset is for development/demo databases only.")
    aspnet = (env.get("ASPNETCORE_ENVIRONMENT") or env.get("DOTNET_ENVIRONMENT") or "").lower()
    if aspnet in {"production", "staging"}:
        raise DemoError(f"refusing to run: ASPNETCORE_ENVIRONMENT={aspnet}")
    if (env.get("NODE_ENV") or "").lower() == "production":
        raise DemoError("refusing to run: NODE_ENV=production")
    if re.search(r"prod|stag", db_name, re.IGNORECASE):
        raise DemoError(f"refusing to run against database {db_name!r} (looks like prod/staging)")


# ======================================================================================
# Runtime push (backend API): Lua scripts + telemetry replay
# ======================================================================================
class Api:
    def __init__(self, base: str, token: str | None, allow_remote: bool):
        parsed = urllib.parse.urlparse(base)
        if parsed.scheme not in ("http", "https"):
            raise DemoError(f"bad backend URL {base!r}")
        if parsed.hostname not in ("localhost", "127.0.0.1", "::1") and not allow_remote:
            raise DemoError(f"refusing to push runtime data to non-local backend {parsed.hostname!r} (use --allow-remote-backend)")
        self.base = base.rstrip("/")
        self.token = token

    def call(self, method: str, path: str, body: Any | None = None) -> Any:
        data = json.dumps(body).encode("utf-8") if body is not None else None
        req = urllib.request.Request(self.base + path, data=data, method=method)
        req.add_header("Accept", "application/json")
        req.add_header("User-Agent", "heimdall-demo-seed")
        if data is not None:
            req.add_header("Content-Type", "application/json")
        if self.token:
            req.add_header("Authorization", f"Bearer {self.token}")
        try:
            with urllib.request.urlopen(req, timeout=20) as resp:
                raw = resp.read().decode("utf-8")
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", "replace")[:300]
            raise DemoError(f"{method} {path} -> HTTP {exc.code}: {detail}")
        except urllib.error.URLError as exc:
            raise DemoError(f"{method} {path} -> backend unreachable ({exc.reason})")


def push_runtime(ds: Dataset, api: Api, dry_run: bool) -> None:
    scripts = ds.lua.get("scripts", [])
    station_ids = {row["custom_identifier"]: row["id"] for row in ds.payload["stations"]}

    series = []
    for station, st in ds.telemetry.get("stations", {}).items():
        mid = station_ids.get(station)
        if not mid:
            raise DemoError(f"telemetry station {station} is not a demo station")
        for metric, spec in st["metrics"].items():
            series.append({"machine_id": mid, "metric": metric, "values": [v for _, v in spec["points"]]})

    if dry_run:
        info(f"[dry-run] would register {len(scripts)} Lua scripts and replay {sum(len(s['values']) for s in series)} telemetry samples")
        return

    for s in scripts:
        code = (LUA_DIR / s["file"]).read_text(encoding="utf-8")
        body = {"id": ds.keys[s["_key"]], "name": s["name"], "description": s["description"], "triggerType": s["triggerType"],
                "code": code, "isActive": s["isActive"], "createdBy": "demo-seed", "metadata": s.get("metadata", {})}
        api.call("POST", "/api/v1/lua/scripts", body)  # same id -> overwrite (idempotent)
        info(f"registered Lua script {s['name']} ({body['id']})")

    replay = (LUA_DIR / ds.lua.get("replay", {}).get("file", "replay_telemetry.lua")).read_text(encoding="utf-8")
    total = 0
    for s in series:  # one call per series keeps each run well inside the sandbox time budget
        res = api.call("POST", "/api/v1/lua/execute", {"code": replay, "context": {"demo_series": [s]}})
        ok = isinstance(res, dict) and (res.get("success") or res.get("Success"))
        if not ok:
            raise DemoError(f"telemetry replay failed for {s['metric']}: {json.dumps(res)[:300]}")
        total += len(s["values"])
    info(f"replayed {total} telemetry samples into the backend Lua telemetry buffer ({len(series)} series)")

    watchdog = next((s for s in scripts if s["_key"] == "lua:telemetry-threshold-watchdog"), None)
    if watchdog:
        hero = station_ids.get("NV-L2-OP060")
        res = api.call("POST", f"/api/v1/lua/scripts/{ds.keys[watchdog['_key']]}/run", {"context_machine_id": hero})
        out = res.get("output") if isinstance(res, dict) else None
        if isinstance(out, list):
            out = "\n".join(out)
        info("watchdog run on NV-L2-OP060 (deduplicates, no new ticket expected):\n" + (out or json.dumps(res)[:500]))


# ======================================================================================
# main
# ======================================================================================
def parse_args(argv: list[str]) -> argparse.Namespace:
    ap = argparse.ArgumentParser(description="Load the Heimdall Nordvik NV1 demo dataset (idempotent, dev-only).")
    ap.add_argument("--env-file", default=str(REPO_ROOT / ".env.dev"), help="env file providing HEIMDALL_ENABLE_DEV / POSTGRES_DB (default: .env.dev)")
    ap.add_argument("--dry-run", action="store_true", help="validate the dataset and print the SQL plan; no database access")
    ap.add_argument("--check", action="store_true", help="execute against the database inside a transaction, then ROLLBACK")
    ap.add_argument("--reset", action="store_true", help="delete existing demo rows (de30de30-* / demo-*) before loading")
    ap.add_argument("--with-logins", action="store_true", help="create Better-Auth credentials for the demo users (demo-only password)")
    ap.add_argument("--unpin-conflicts", action="store_true", help="clear floor-plan handles on NON-demo stations that collide with demo handles")
    ap.add_argument("--skip-db", action="store_true", help="do not touch the database (use with --push-runtime)")
    ap.add_argument("--push-runtime", action="store_true", help="register demo Lua scripts and replay telemetry via the backend API")
    ap.add_argument("--backend-url", default=None, help="backend base URL (default: BACKEND_API_URL from env or http://localhost:5099)")
    ap.add_argument("--allow-remote-backend", action="store_true", help="allow --push-runtime against a non-localhost backend")
    ap.add_argument("--transport", choices=("auto", "docker", "psycopg"), default="auto")
    ap.add_argument("--container", default=None, help="postgres docker container (default: HEIMDALL_PG_CONTAINER or postgres_heimdall)")
    ap.add_argument("--dsn", default=None, help="libpq DSN for --transport psycopg (default: from ConnectionStrings__DefaultConnection)")
    ap.add_argument("--write-telemetry", action="store_true", help="regenerate seed_data/demo/telemetry.json and exit")
    ap.add_argument("--print-sql", action="store_true", help="with --dry-run: print every statement")
    ap.add_argument("--now", default=None, help="pin the reference time (ISO-8601, UTC) instead of the current time; "
                                                "useful for byte-identical reloads (default: HEIMDALL_DEMO_NOW or now)")
    return ap.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)

    if args.write_telemetry:
        out = DEMO_DIR / "telemetry.json"
        out.write_text(json.dumps(build_telemetry(), indent=1) + "\n", encoding="utf-8")
        info(f"wrote {out.relative_to(REPO_ROOT)}")
        return 0

    env = {**read_env_file(Path(args.env_file)), **os.environ}
    db_name = env.get("POSTGRES_DB", "heimdall_dev_db")
    now_raw = args.now or env.get("HEIMDALL_DEMO_NOW")
    try:
        now = (datetime.fromisoformat(now_raw.replace("Z", "+00:00")).astimezone(timezone.utc) if now_raw
               else datetime.now(timezone.utc)).replace(microsecond=0)
    except ValueError:
        raise DemoError(f"bad --now value {now_raw!r}")
    password = env.get("HEIMDALL_DEMO_PASSWORD") or DEFAULT_DEMO_PASSWORD

    ds = Dataset(now, with_logins=args.with_logins, demo_password=password).build()
    for w in ds.warnings:
        warn(w)
    if ds.errors:
        for e in ds.errors:
            print(f"[demo-seed] INVALID: {e}", file=sys.stderr)
        info(f"dataset validation failed with {len(ds.errors)} error(s)")
        return 2
    info("dataset valid: " + ", ".join(f"{k}={v}" for k, v in ds.counts().items()))

    if args.dry_run:
        stmts = build_statements(ds, None, args.reset, args.unpin_conflicts)
        info(f"[dry-run] {len(stmts)} statements, payload {len(payload_document(ds)) // 1024} KiB, target would be {db_name}")
        for s in stmts:
            print(s if args.print_sql else s.splitlines()[0][:120])
        if args.push_runtime:
            push_runtime(ds, None, dry_run=True)  # type: ignore[arg-type]
        return 0

    enforce_dev_guard(env, db_name)

    if not args.skip_db:
        transport: DockerPsql | Psycopg
        container = args.container or env.get("HEIMDALL_PG_CONTAINER", "postgres_heimdall")
        if args.transport in ("auto", "docker") and docker_container_running(container):
            transport = DockerPsql(container, env.get("POSTGRES_USER", "postgres"), db_name)
        elif args.transport in ("auto", "psycopg"):
            dsn = args.dsn or env.get("HEIMDALL_DEMO_DSN") or npgsql_to_dsn(env.get("ConnectionStrings__DefaultConnection", ""))
            if not dsn:
                raise DemoError("no database transport: docker container not running and no DSN configured")
            if not re.search(r"dbname=([^ ]+)", dsn) or re.search(r"dbname=\S*(prod|stag)", dsn, re.IGNORECASE):
                raise DemoError("refusing DSN without dbname or pointing at prod/staging")
            try:
                transport = Psycopg(dsn)
            except ImportError:
                raise DemoError("psycopg is not installed and the docker container is not running")
        else:
            raise DemoError(f"docker container {container!r} is not running")
        info(f"transport: {transport.describe()}")

        live = transport.columns()
        # rebuild with the live catalog so auto-filled columns match the real tables
        ds = Dataset(now, with_logins=args.with_logins, demo_password=password, live_columns=live).build()
        if ds.errors:
            for e in ds.errors:
                print(f"[demo-seed] INVALID (live schema): {e}", file=sys.stderr)
            return 2
        drift = []
        for table, cols in TABLE_COLUMNS.items():
            have = live.get(table)
            if have is None:
                if table in OPTIONAL_TABLES:
                    info(f"{table} not present in this DB (older schema) - skipped; use --push-runtime for Lua scripts")
                else:
                    drift.append(f"{table}: table missing")
                continue
            extra = sorted(have - set(cols))
            missing = sorted(set(cols) - have)
            if extra:
                drift.append(f"{table}: new column(s) not covered by the demo allow-list {extra}")
            if missing:
                drift.append(f"{table}: column(s) gone from DB {missing}")
        for d in drift:
            warn("schema drift - " + d)

        handles = [s["pinned_object_handle"] for s in ds.payload["stations"] if s.get("pinned_object_handle")]
        conflicts = transport.handle_conflicts(handles)
        if conflicts and not args.unpin_conflicts:
            warn(f"{len(conflicts)} non-demo station(s) are pinned to the same floor-plan handles as the demo stations "
                 f"(e.g. {conflicts[0][0]} -> {conflicts[0][1]}). The plant map will show both. "
                 "Re-run with --unpin-conflicts, or load the demo into a DB without the enterprise dataset.")

        stmts = build_statements(ds, live, args.reset, args.unpin_conflicts)
        transport.execute(ds, stmts, commit=not args.check)
        info(("CHECK OK (rolled back)" if args.check else "demo dataset loaded") + f": {len(stmts)} statements into {db_name}")
        if args.with_logins and not args.check:
            info(f"demo logins enabled for {len(ds.payload['users'])} users, password: {password!r}  (DEMO ONLY - dev database)")

    if args.push_runtime:
        base = args.backend_url or env.get("BACKEND_API_URL") or "http://localhost:5099"
        push_runtime(ds, Api(base, env.get("HEIMDALL_DEMO_API_TOKEN"), args.allow_remote_backend), dry_run=args.check)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except DemoError as exc:
        print(f"[demo-seed] ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
