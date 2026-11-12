#!/usr/bin/env python3
"""
Readers for the artifacts the seed datasets must stay consistent with. Shared by the enterprise
pipeline (seed_data/seed_pipeline.py) and the Nordvik demo loader (scripts/seed_demo_data.py).
Standard library only.

* floor plans     DXF block handles the stations and controllers are pinned to
* digital twin    mesh (node) names a machine component slot binds to
* role constants  HeimdallRoles in src/shared/App.Contracts/Security/SecurityConstants.cs
"""

import json
import os
import re

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SAMPLE_DIR = os.path.join(REPO_ROOT, 'src', 'frontend', 'web', 'public', 'sample')
# The drawing the plant map opens by default (map.vue: 'production_hall').
FLOOR_PLAN_DXF = os.path.join(SAMPLE_DIR, 'production_hall.dxf')
DIGITAL_TWIN_GLB = os.path.join(SAMPLE_DIR, 'station_digital_twin.glb')
DIGITAL_TWIN_URL = "/sample/station_digital_twin.glb"
SECURITY_CONSTANTS_CS = os.path.join(REPO_ROOT, 'src', 'shared', 'App.Contracts', 'Security', 'SecurityConstants.cs')

# Used when SecurityConstants.cs is not available (e.g. a container that only ships seed_data/).
FALLBACK_ROLES = frozenset({
    "system_admin", "heimdall_admin", "admin", "plant_director", "it_admin", "it_site_admin", "engineering_admin",
    "plant_engineering_manager", "senior_engineering_manager", "group_leader", "shift_leader", "lead_engineer",
    "controls_engineer", "engineer", "technician", "operative_planner", "operator", "manager", "user",
})

# Node names of the sample digital twin: a machine component whose functional slot equals one of
# them is bound to that mesh in the 3D view.
TWIN_SLOTS = ("FRAME-BASE-01", "CONVEYOR-01", "GANTRY-X-01", "GANTRY-Z-01", "TOOL-HEAD-01", "VISION-CAM-01", "PANEL-STATUS-01")


def read_dxf_insert_handles(path=FLOOR_PLAN_DXF):
    """Handles of the INSERT entities (block references) in a DXF's ENTITIES section.

    The floor plans carry the station identifier as the entity handle (group code 5). That is not a
    valid hexadecimal DXF handle, so ezdxf refuses to load the file; the web map reads it as text,
    and so does this reader.
    """
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()
    handles, section, current, expect_section_name = [], None, None, False
    for i in range(0, len(lines) - 1, 2):
        code, value = lines[i].strip(), lines[i + 1].strip()
        if code == "0":
            current = value
            expect_section_name = value == "SECTION"
            if value == "ENDSEC":
                section = None
        elif code == "2" and expect_section_name:
            section, expect_section_name = value, False
        elif code == "5" and section == "ENTITIES" and current == "INSERT":
            handles.append(value)
    return handles


def read_glb_node_names(path=DIGITAL_TWIN_GLB):
    """Scene-graph node names of a binary glTF file (JSON chunk only)."""
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"glTF":
        raise ValueError(f"{path}: not a GLB file")
    chunk_len = int.from_bytes(data[12:16], "little")
    doc = json.loads(data[20:20 + chunk_len])
    return [n.get("name") for n in doc.get("nodes", []) if n.get("name")]


def read_heimdall_roles(path=SECURITY_CONSTANTS_CS):
    """(roles, from_source): the HeimdallRoles constants, or the built-in list when the source is absent."""
    try:
        with open(path, encoding="utf-8") as f:
            text = f.read()
    except OSError:
        return set(FALLBACK_ROLES), False
    start = text.find("class HeimdallRoles")
    end = text.find("}", start)
    roles = set(re.findall(r'public const string \w+ = "([a-z_]+)";', text[start:end])) if start >= 0 else set()
    return (roles, True) if roles else (set(FALLBACK_ROLES), False)
