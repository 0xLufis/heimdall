#!/usr/bin/env python3
"""
Loads the plant the simulator impersonates: IPC nodes (heartbeats) and the controller -> station
links (station process telemetry). Two datasets are supported:

  enterprise  fixtures/enterprise_plant_dataset.json (clientPcs) +
              seed_data/production_topology.json (stations and their controlling PCs)
  demo        seed_data/demo/plant.json (Nordvik NV1: controllers, station_controllers, machines)

Identifiers must match what the seed scripts wrote to the database, otherwise the backend cannot
attribute the telemetry: ClientPc.machine_identifier for the topic, and the station's custom
identifier / name / floor-plan handle for the block's StationId.
"""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Tuple

from station_model import StationSpec

FLEET_DIR = os.path.dirname(os.path.abspath(__file__))


@dataclass
class ControllerNode:
    hostname: str
    machine_identifier: str
    mac_address: str
    ip_address: str
    os_name: str
    profile: str = "Beckhoff TwinCAT 3 IPC"
    # Seeded system_metadata, echoed in the heartbeat's OS component: the backend replaces the
    # stored document with what the "agent" reports, so dataset fields would otherwise vanish.
    metadata: Optional[Dict[str, Any]] = None


@dataclass
class PlantProfile:
    """Who maintains the plant and what the spare-part store holds (read by the maintenance desk)."""
    crews: Dict[str, List[Dict[str, str]]] = field(default_factory=dict)   # technology -> [{id, name}]
    default_crew: List[Dict[str, str]] = field(default_factory=list)
    escalation: List[Dict[str, str]] = field(default_factory=list)
    serialized: Dict[str, int] = field(default_factory=dict)               # part class -> spares in storage
    consumables: Dict[str, int] = field(default_factory=dict)              # consumable class -> stock quantity
    consumable_minimum: Dict[str, int] = field(default_factory=dict)

    def crew_for(self, technology: Optional[str]) -> List[Dict[str, str]]:
        return self.crews.get(technology or "") or self.default_crew


def _first_existing(candidates: List[Optional[str]]) -> Optional[str]:
    for c in candidates:
        if c and os.path.exists(c):
            return os.path.abspath(c)
    return None


def _repo_path(*parts: str) -> List[str]:
    return [
        os.path.join(FLEET_DIR, "..", "..", *parts),
        os.path.join("/workspace", *parts),
        os.path.join(os.getcwd(), *parts),
    ]


def _parse_seconds(value: Any) -> Optional[float]:
    """Accepts 24, '24', '24s'."""
    if value is None:
        return None
    try:
        v = float(str(value).strip().rstrip("sS").strip())
        return v if v > 0 else None
    except ValueError:
        return None


def _profile_for(text: str, machine_type: str = "") -> str:
    t = text.upper()
    if "ROB" in t or machine_type == "Manipulator":
        return "Fanuc Robot Controller"
    if "VISION" in t:
        return "Cognex In-Sight 9000"
    if "MES" in t or "DEBIAN" in t or "LINUX" in t:
        return "Dell OptiPlex Edge Terminal"
    if "CONVEYOR" in t or "SIEMENS" in t or "SIMATIC" in t:
        return "Siemens SIMATIC IPC477E"
    return "Beckhoff TwinCAT 3 IPC"


# ---------------------------------------------------------------------------- enterprise
def load_enterprise() -> Tuple[List[ControllerNode], List[StationSpec]]:
    nodes: List[ControllerNode] = []
    try:
        from dataset_loader import get_all_client_pcs
        for pc in get_all_client_pcs():
            hostname = pc.get("hostname")
            if not hostname:
                continue
            nodes.append(ControllerNode(
                hostname=hostname,
                machine_identifier=pc.get("machineIdentifier") or f"HW-{hostname}",
                mac_address=pc.get("macAddress") or "",
                ip_address=pc.get("ipAddress") or "",
                os_name=pc.get("osVersion") or "Windows 10 IoT Enterprise",
                profile=_profile_for(f"{hostname} {pc.get('osVersion', '')}", pc.get("machineType", "")),
            ))
    except Exception:
        nodes = []

    # Echo the seeded system metadata in the heartbeat (the backend replaces the stored document with the report).
    csv_path = _first_existing([os.environ.get("CSV_PATH")] + _repo_path("seed_data", "inventory_seed.csv"))
    if csv_path:
        import csv
        with open(csv_path, "r", encoding="utf-8", newline="") as f:
            seeded = {r["Name"].upper(): r["Metadata"] for r in csv.DictReader(f) if r.get("Type") == "ClientPc"}
        for n in nodes:
            try:
                n.metadata = json.loads(seeded.get(n.hostname.upper()) or "null") or None
            except ValueError:
                pass
    by_host = {n.hostname.upper(): n for n in nodes}
    stations: List[StationSpec] = []
    topo_path = _first_existing([os.environ.get("TOPOLOGY_PATH")] + _repo_path("seed_data", "production_topology.json"))
    if topo_path:
        with open(topo_path, "r", encoding="utf-8") as f:
            topo = json.load(f)
        for line in topo.get("production_hall", {}).get("lines", []):
            line_key = f"L{int(line.get('line_num', 0)):02d}" if line.get("line_num") else (line.get("id") or "")
            for st in line.get("stations", []):
                pcs = st.get("pcs") or []
                if not pcs or not st.get("id"):
                    continue
                # Several PCs may serve one station (m-n / n-1); the Primary one reports its process data.
                primary = next((pc for pc in pcs if pc.get("role") == "Primary"), pcs[0])
                host = primary.get("hostname") or primary.get("id") or ""
                node = by_host.get(host.upper())
                if node is None:
                    node = ControllerNode(host, f"HW-{host}", "", primary.get("ip") or "",
                                          primary.get("os") or "Windows 10 IoT Enterprise", _profile_for(host))
                    by_host[host.upper()] = node
                    nodes.append(node)
                stations.append(StationSpec(
                    station_id=st["id"],                      # == stations.name and floor-plan handle in the seed
                    name=st.get("name") or st["id"],
                    machine_type=st.get("type"),
                    controller_machine_identifier=node.machine_identifier,
                    handle=st.get("pinnedObjectHandle"),
                    line=line_key,
                    technology=st.get("technology"),
                    cycle_time_target_s=_parse_seconds(st.get("cycleTimeTargetS")),
                    custom_identifier=st.get("customIdentifier"),
                    components=[{"slot": c.get("slot"), "partClass": c.get("partClass"), "name": c.get("name")}
                                for c in st.get("components") or []],
                ))
    return nodes, stations


def load_enterprise_profile() -> PlantProfile:
    from dataset_loader import load_enterprise_dataset
    ds = load_enterprise_dataset()
    groups = {g["id"]: [{"id": m["userId"], "name": m["name"]} for m in g.get("members", [])]
              for g in ds.get("personnelGroups", [])}
    profile = PlantProfile()
    for r in ds.get("delegationRecipes", []):
        profile.crews[r["technology"]] = groups.get(r["personnelGroupId"], [])
        profile.escalation = profile.escalation or groups.get(r.get("escalationGroupId"), [])
    profile.default_crew = profile.crews.get("Assembly") or next(iter(groups.values()), [])
    profile.serialized = {s["partClass"]: int(s["inStorage"]) for s in ds.get("spareParts", [])}
    for c in ds.get("consumables", []):
        cls = c["consumableClass"]
        profile.consumables[cls] = profile.consumables.get(cls, 0) + int(c["quantity"])
        profile.consumable_minimum[cls] = profile.consumable_minimum.get(cls, 0) + int(c["minimum"])
    return profile


# ---------------------------------------------------------------------------- demo (Nordvik NV1)
def load_demo(path: Optional[str] = None) -> Tuple[List[ControllerNode], List[StationSpec]]:
    plant_path = _first_existing([path, os.environ.get("DEMO_PLANT_PATH")] + _repo_path("seed_data", "demo", "plant.json"))
    if not plant_path:
        raise FileNotFoundError("seed_data/demo/plant.json not found (set DEMO_PLANT_PATH)")
    with open(plant_path, "r", encoding="utf-8") as f:
        plant = json.load(f)

    nodes: List[ControllerNode] = []
    by_key: Dict[str, ControllerNode] = {}
    for c in plant.get("controllers", []):
        meta = c.get("system_metadata") or {}
        node = ControllerNode(
            hostname=c.get("hostname") or c.get("name"),
            machine_identifier=c.get("machine_identifier") or f"HW-{c.get('name')}",
            mac_address=c.get("mac_address") or "",
            ip_address=c.get("ip_address") or "",
            os_name=meta.get("OperatingSystem") or "Windows 10 IoT Enterprise",
            profile=_profile_for(f"{c.get('name', '')} {meta.get('Role', '')}"),
            metadata=dict(meta) or None,
        )
        nodes.append(node)
        by_key[f"@{c['_key']}"] = node

    machines = {f"@{m['_key']}": m for m in plant.get("machines", [])}
    part_class = {f"@{x['_key']}": (x.get("metadata") or {}).get("partClass") for x in plant.get("parts", [])}
    components: Dict[str, List[Dict[str, Any]]] = {}
    for mc in plant.get("machine_components", []):
        components.setdefault(mc.get("machine_id"), []).append(
            {"slot": mc.get("functional_slot_identifier"), "partClass": part_class.get(mc.get("current_part_id")), "name": mc.get("name")})
    # One reporting controller per station: Primary wins over Secondary.
    links = sorted(plant.get("station_controllers", []), key=lambda l: 0 if l.get("role") == "Primary" else 1)
    stations: List[StationSpec] = []
    seen = set()
    for link in links:
        m = machines.get(link.get("machine_id"))
        node = by_key.get(link.get("client_pc_id"))
        if not m or not node or link["machine_id"] in seen:
            continue
        if str(m.get("status") or "").lower() in ("decommissioned", "dismantled"):
            continue
        seen.add(link["machine_id"])
        meta = m.get("metadata") or {}
        tmpl = ((meta.get("template") or {}).get("values") or {})
        stations.append(StationSpec(
            station_id=m.get("custom_identifier") or m["name"],
            name=m.get("display_name") or m["name"],
            machine_type=m.get("machine_type"),
            controller_machine_identifier=node.machine_identifier,
            handle=m.get("pinned_object_handle"),
            line=meta.get("line"),
            technology=m.get("technology"),
            cycle_time_target_s=_parse_seconds(meta.get("cycleTimeTargetS")),
            nominal_power_kw=tmpl.get("laser_power_nominal_kw") or meta.get("ratedPowerKw"),
            temp_alarm_c=tmpl.get("glass_temp_alarm_c"),
            power_alarm_low_kw=tmpl.get("laser_power_min_kw"),
            parent_station_id=(machines.get(m.get("parent_machine_id")) or {}).get("custom_identifier"),
            components=components.get(link["machine_id"], []),
        ))
    stations.sort(key=lambda s: s.station_id)
    return nodes, stations


def load_demo_profile(path: Optional[str] = None) -> PlantProfile:
    plant_path = _first_existing([path, os.environ.get("DEMO_PLANT_PATH")] + _repo_path("seed_data", "demo", "plant.json"))
    if not plant_path:
        raise FileNotFoundError("seed_data/demo/plant.json not found (set DEMO_PLANT_PATH)")
    with open(plant_path, "r", encoding="utf-8") as f:
        plant = json.load(f)
    with open(os.path.join(os.path.dirname(plant_path), "people.json"), "r", encoding="utf-8") as f:
        people = json.load(f)
    members: Dict[str, List[Dict[str, str]]] = {}
    for m in people.get("personnel_group_members", []):
        members.setdefault(m["personnel_group_id"], []).append({"id": m["user_id"], "name": m.get("user_name") or m["user_id"]})
    profile = PlantProfile()
    for r in people.get("delegation_recipes", []):
        if r.get("technology"):
            profile.crews[r["technology"]] = members.get(r.get("primary_personnel_group_id"), [])
        profile.escalation = profile.escalation or members.get(r.get("escalation_group_id"), [])
    mechanical = next((f"@{g['_key']}" for g in people.get("personnel_groups", []) if g.get("discipline") == "Mechanical"), None)
    profile.default_crew = members.get(mechanical) or next(iter(members.values()), [])
    for x in plant.get("parts", []):
        meta = x.get("metadata") or {}
        if x.get("is_stock_item") and meta.get("consumableClass"):
            cls = meta["consumableClass"]
            profile.consumables[cls] = profile.consumables.get(cls, 0) + int(x.get("stock_quantity") or 0)
            profile.consumable_minimum[cls] = profile.consumable_minimum.get(cls, 0) + int(x.get("min_stock_threshold") or 0)
        elif x.get("equipment_status") != "InMachine" and meta.get("partClass"):
            profile.serialized.setdefault(meta["partClass"], 0)
            if x.get("equipment_status") == "InStorage":
                profile.serialized[meta["partClass"]] += 1
    return profile


def load_profile(dataset: str) -> PlantProfile:
    """Crews and store of the dataset; 'all' merges both plants (their technologies overlap, the demo crew wins nothing)."""
    if dataset == "demo":
        return load_demo_profile()
    profile = load_enterprise_profile()
    if dataset == "all":
        try:
            demo = load_demo_profile()
        except FileNotFoundError:
            return profile
        for cls, n in demo.serialized.items():
            profile.serialized[cls] = profile.serialized.get(cls, 0) + n
        for cls, n in demo.consumables.items():
            profile.consumables[cls] = profile.consumables.get(cls, 0) + n
            profile.consumable_minimum[cls] = profile.consumable_minimum.get(cls, 0) + demo.consumable_minimum.get(cls, 0)
    return profile


def load(dataset: str) -> Tuple[List[ControllerNode], List[StationSpec]]:
    """dataset: 'enterprise' | 'demo' | 'all' (both; they use disjoint identifiers)."""
    if dataset == "demo":
        return load_demo()
    if dataset == "all":
        n1, s1 = load_enterprise()
        try:
            n2, s2 = load_demo()
        except FileNotFoundError:
            n2, s2 = [], []
        # Both plants number their lines L01..; keep line-level scenarios apart.
        for st in s2:
            if st.line:
                st.line = f"NV-{st.line}"
        return n1 + n2, s1 + s2
    return load_enterprise()
