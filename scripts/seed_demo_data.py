#!/usr/bin/env python3
"""
seed_demo_data.py
=================
Seeds realistic industrial demo machine groups, technician skills, and tags
via the Heimdall HTTP API, and verifies their proper integration with tickets.

Usage:
    python3 scripts/seed_demo_data.py [--base-url http://localhost:3000] [--verbose]
"""

import sys
import json
import argparse
from typing import Dict, Any, List
import requests

def parse_args():
    parser = argparse.ArgumentParser(description="Seed Heimdall demo machine groups, skills, tags, and tickets.")
    parser.add_argument("--base-url", default="http://localhost:3000", help="Nuxt BFF base URL (default: http://localhost:3000)")
    parser.add_argument("--backend-url", default="http://localhost:5099", help=".NET Backend URL (default: http://localhost:5099)")
    parser.add_argument("--verbose", "-v", action="store_true", help="Enable verbose response logging")
    return parser.parse_args()

class SeedClient:
    def __init__(self, base_url: str, backend_url: str, verbose: bool = False):
        self.base_url = base_url.rstrip("/")
        self.backend_url = backend_url.rstrip("/")
        self.verbose = verbose
        self.session = requests.Session()
        self.session.headers.update({
            "Content-Type": "application/json",
            "Accept": "application/json"
        })

    def log(self, title: str, msg: str = ""):
        print(f"\033[1;34m[INFO]\033[0m \033[1m{title}\033[0m: {msg}")

    def success(self, title: str, msg: str = ""):
        print(f"\033[1;32m[SUCCESS]\033[0m \033[1m{title}\033[0m {msg}")

    def error(self, title: str, msg: str = ""):
        print(f"\033[1;31m[ERROR]\033[0m \033[1m{title}\033[0m {msg}", file=sys.stderr)

    # ──────────────────────────────────────────────────────────────────────────
    # 1. MACHINE GROUPS
    # ──────────────────────────────────────────────────────────────────────────
    def create_machine_group(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        url = f"{self.base_url}/api/machine-groups"
        resp = self.session.post(url, json=payload, timeout=10)
        if resp.status_code not in (200, 201):
            self.error("Failed to create machine group", f"Status: {resp.status_code}, Body: {resp.text}")
            resp.raise_for_status()
        data = resp.json()
        if self.verbose:
            print(f"Created Group: {json.dumps(data, indent=2)}")
        return data

    def get_machine_groups(self) -> List[Dict[str, Any]]:
        url = f"{self.base_url}/api/machine-groups"
        resp = self.session.get(url, timeout=10)
        resp.raise_for_status()
        return resp.json()

    # ──────────────────────────────────────────────────────────────────────────
    # 2. SKILLS CATALOG & ASSIGNMENTS
    # ──────────────────────────────────────────────────────────────────────────
    def create_skill(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        url = f"{self.base_url}/api/skills/catalog"
        resp = self.session.post(url, json=payload, timeout=10)
        if resp.status_code not in (200, 201):
            self.error("Failed to create skill", f"Status: {resp.status_code}, Body: {resp.text}")
            resp.raise_for_status()
        data = resp.json()
        if self.verbose:
            print(f"Created Skill: {json.dumps(data, indent=2)}")
        return data

    def get_skills_catalog(self) -> List[Dict[str, Any]]:
        url = f"{self.base_url}/api/skills/catalog"
        resp = self.session.get(url, timeout=10)
        resp.raise_for_status()
        return resp.json()

    def assign_skill_to_user(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        url = f"{self.base_url}/api/skills/user"
        resp = self.session.post(url, json=payload, timeout=10)
        if resp.status_code not in (200, 201):
            self.error("Failed to assign skill", f"Status: {resp.status_code}, Body: {resp.text}")
            resp.raise_for_status()
        data = resp.json()
        if self.verbose:
            print(f"Assigned Skill: {json.dumps(data, indent=2)}")
        return data

    def get_user_skills(self, user_id: str = None) -> List[Dict[str, Any]]:
        url = f"{self.base_url}/api/skills/user"
        params = {"userId": user_id} if user_id else {}
        resp = self.session.get(url, params=params, timeout=10)
        resp.raise_for_status()
        return resp.json()

    # ──────────────────────────────────────────────────────────────────────────
    # 3. TICKETS
    # ──────────────────────────────────────────────────────────────────────────
    def create_ticket(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        url = f"{self.base_url}/api/tickets"
        resp = self.session.post(url, json=payload, timeout=10)
        if resp.status_code not in (200, 201):
            self.error("Failed to create ticket", f"Status: {resp.status_code}, Body: {resp.text}")
            resp.raise_for_status()
        data = resp.json()
        if self.verbose:
            print(f"Created Ticket: {json.dumps(data, indent=2)}")
        return data

    def get_tickets(self, params: Dict[str, Any] = None) -> List[Dict[str, Any]]:
        url = f"{self.base_url}/api/tickets"
        resp = self.session.get(url, params=params or {}, timeout=10)
        resp.raise_for_status()
        data = resp.json()
        if isinstance(data, dict) and "tickets" in data:
            return data["tickets"]
        if isinstance(data, list):
            return data
        return []

    def get_technician_candidates(self, role: str = None) -> List[Dict[str, Any]]:
        url = f"{self.base_url}/api/technicians/candidates"
        params = {"role": role} if role else {}
        resp = self.session.get(url, params=params, timeout=10)
        resp.raise_for_status()
        return resp.json()

def main():
    args = parse_args()
    client = SeedClient(base_url=args.base_url, backend_url=args.backend_url, verbose=args.verbose)

    print("================================================================================")
    print("   HEIMDALL INDUSTRIAL DEMO SEEDER: MACHINE GROUPS, SKILLS, TAGS & TICKETS    ")
    print("================================================================================")

    # ──────────────────────────────────────────────────────────────────────────
    # STEP 1: CREATE DEMO MACHINE GROUPS
    # ──────────────────────────────────────────────────────────────────────────
    client.log("Step 1", "Creating hierarchical demo machine groups...")

    # Root Group 1: Battery Pack Assembly Line 01
    root_group_1_payload = {
        "name": "Battery Pack Assembly Line 01",
        "description": "High-throughput pouch cell module stacking and busbar welding line",
        "parentId": None,
        "color": "emerald",
        "machineTypes": ["Laser Welding", "Robotics Manipulator", "Screwing Station"],
        "machineIds": ["ROBOT-CELL-01", "STATION-OP10-01"]
    }
    g1 = client.create_machine_group(root_group_1_payload)
    g1_id = g1.get("id")
    client.success("Created Root Machine Group", f"'{g1.get('name')}' (ID: {g1_id})")

    # Child Group 1A: Module Fastening & Dispensing Cell (under Line 01)
    child_group_1a_payload = {
        "name": "Module Fastening & Dispensing Cell",
        "description": "Automated thermal paste dispensing and structural fastening cell",
        "parentId": g1_id,
        "color": "purple",
        "machineTypes": ["Screwing Station", "Gap Filler"],
        "machineIds": ["L06-OP150"]
    }
    g1a = client.create_machine_group(child_group_1a_payload)
    g1a_id = g1a.get("id")
    client.success("Created Child Machine Group", f"'{g1a.get('name')}' -> Parent: {g1a.get('parentId')}")

    # Child Group 1B: Vision & AOI Quality Station (under Line 01)
    child_group_1b_payload = {
        "name": "Vision & AOI Quality Station",
        "description": "High-resolution 3D weld bead inspection and surface flaw detection",
        "parentId": g1_id,
        "color": "blue",
        "machineTypes": ["Automatic Optical Inspection"],
        "machineIds": ["L09-OP270"]
    }
    g1b = client.create_machine_group(child_group_1b_payload)
    g1b_id = g1b.get("id")
    client.success("Created Child Machine Group", f"'{g1b.get('name')}' -> Parent: {g1b.get('parentId')}")

    # Root Group 2: Powertrain E-Motor Assembly Line 02
    root_group_2_payload = {
        "name": "Powertrain E-Motor Assembly Line 02",
        "description": "Stator hairpin winding, impregnation, and rotor balancing line",
        "parentId": None,
        "color": "amber",
        "machineTypes": ["CNC Milling", "Stator Insertion", "End of Line Tester"],
        "machineIds": ["STATION-CNC-02", "L02-EOL-01"]
    }
    g2 = client.create_machine_group(root_group_2_payload)
    g2_id = g2.get("id")
    client.success("Created Root Machine Group", f"'{g2.get('name')}' (ID: {g2_id})")

    # Verify machine groups in list
    all_groups = client.get_machine_groups()
    group_ids = [g.get("id") for g in all_groups]
    assert g1_id in group_ids, f"Expected {g1_id} in machine groups list"
    assert g1a_id in group_ids, f"Expected {g1a_id} in machine groups list"
    assert g1b_id in group_ids, f"Expected {g1b_id} in machine groups list"
    assert g2_id in group_ids, f"Expected {g2_id} in machine groups list"
    client.success("Verified Machine Groups Hierarchy", f"Total groups in system: {len(all_groups)}")

    # ──────────────────────────────────────────────────────────────────────────
    # STEP 2: CREATE TECHNICIAN / ENGINEERING SKILLS CATALOG
    # ──────────────────────────────────────────────────────────────────────────
    print("\n--------------------------------------------------------------------------------")
    client.log("Step 2", "Creating technician and engineering skills in catalog...")

    skills_to_seed = [
        {
            "code": "KUKA-ROB-04",
            "name": "KUKA KRC4 / KRC5 Robotics Specialist",
            "category": "TechnologyKnowHow",
            "issuer": "KUKA Robotics College",
            "description": "Master certification for industrial 6-axis manipulator trajectory tuning and safety zones",
            "validityMonths": 36
        },
        {
            "code": "BECKHOFF-TC3",
            "name": "Beckhoff TwinCAT 3 PLC & Motion Specialist",
            "category": "TechnologyKnowHow",
            "issuer": "Beckhoff Automation Academy",
            "description": "Deterministic EtherCAT master configuration and NC PTP axis control",
            "validityMonths": 36
        },
        {
            "code": "COGNEX-AOI",
            "name": "Cognex In-Sight Vision AI Specialist",
            "category": "Certification",
            "issuer": "Cognex Vision Learning",
            "description": "Deep learning pattern recognition and surface defect classification inspection",
            "validityMonths": 24
        },
        {
            "code": "EV-HV-SAFETY",
            "name": "High Voltage Battery Safety Qualified",
            "category": "SafetyClearance",
            "issuer": "DGUV-I 209-093",
            "description": "Authorized electrical qualification for EV battery pack module energization",
            "validityMonths": 12
        },
        {
            "code": "TRUMPF-LASER",
            "name": "Trumpf TruDisk 6001 Laser Certified Engineer",
            "category": "MachineAuthorization",
            "issuer": "Trumpf Lasertechnik",
            "description": "Fiber optical path alignment and protective glass window replacement clearance",
            "validityMonths": 24
        }
    ]

    existing_catalog = client.get_skills_catalog()
    existing_codes = {s.get("code"): s for s in existing_catalog}
    created_skills: Dict[str, Dict[str, Any]] = {}

    for s in skills_to_seed:
        code = s["code"]
        if code in existing_codes:
            created_skills[code] = existing_codes[code]
            client.log("Skill already present", f"{code} - {s['name']}")
        else:
            entry = client.create_skill(s)
            created_skills[code] = entry
            client.success("Created Skill", f"[{entry.get('code')}] {entry.get('name')}")

    # ──────────────────────────────────────────────────────────────────────────
    # STEP 3: ASSIGN SKILLS TO TECHNICIANS & ENGINEERS
    # ──────────────────────────────────────────────────────────────────────────
    print("\n--------------------------------------------------------------------------------")
    client.log("Step 3", "Assigning certified skills to plant technicians and engineers...")

    skill_assignments = [
        {
            "userId": "usr-nemeth",
            "userName": "Zoltán Németh",
            "skillCode": "KUKA-ROB-04",
            "certificateNumber": "KUKA-CERT-HU-2026-081",
            "verifiedBy": "Plant Engineering Director",
            "expiresAt": "2029-10-01T00:00:00Z"
        },
        {
            "userId": "usr-varga",
            "userName": "Gábor Varga",
            "skillCode": "BECKHOFF-TC3",
            "certificateNumber": "BECK-TC3-HU-8910",
            "verifiedBy": "Controls Lead",
            "expiresAt": "2029-11-15T00:00:00Z"
        },
        {
            "userId": "usr-varga",
            "userName": "Gábor Varga",
            "skillCode": "COGNEX-AOI",
            "certificateNumber": "COG-VISION-HU-4412",
            "verifiedBy": "Quality Manager",
            "expiresAt": "2028-09-30T00:00:00Z"
        },
        {
            "userId": "usr-sally",
            "userName": "Engineer Sally",
            "skillCode": "COGNEX-AOI",
            "certificateNumber": "COG-VISION-US-9910",
            "verifiedBy": "Quality Director",
            "expiresAt": "2028-10-01T00:00:00Z"
        },
        {
            "userId": "usr-orwell",
            "userName": "Engineer Orwell",
            "skillCode": "EV-HV-SAFETY",
            "certificateNumber": "DGUV-HV-2026-441",
            "verifiedBy": "EHS Lead Officer",
            "expiresAt": "2027-10-01T00:00:00Z"
        },
        {
            "userId": "usr-orwell",
            "userName": "Engineer Orwell",
            "skillCode": "TRUMPF-LASER",
            "certificateNumber": "TRU-LAS-2026-104",
            "verifiedBy": "Plant Maintenance Manager",
            "expiresAt": "2028-10-01T00:00:00Z"
        }
    ]

    for assign in skill_assignments:
        code = assign["skillCode"]
        skill_entry = created_skills.get(code)
        if not skill_entry:
            continue
        payload = {
            "userId": assign["userId"],
            "userName": assign["userName"],
            "skillId": skill_entry["id"],
            "certificateNumber": assign["certificateNumber"],
            "verifiedBy": assign["verifiedBy"],
            "expiresAt": assign["expiresAt"]
        }
        res = client.assign_skill_to_user(payload)
        client.success("Assigned Skill", f"{assign['userName']} <- {code} ({res.get('certificateNumber')})")

    # Verify user assignments
    all_user_skills = client.get_user_skills()
    client.success("Verified Skill Assignments", f"Total assigned user skills in database: {len(all_user_skills)}")

    # ──────────────────────────────────────────────────────────────────────────
    # STEP 4: INTEGRATE DEMO TICKETS WITH GROUPS, SKILLS, AND TAGS
    # ──────────────────────────────────────────────────────────────────────────
    print("\n--------------------------------------------------------------------------------")
    client.log("Step 4", "Creating realistic industrial tickets linking Machine Groups, Skills, and Tags...")

    demo_tickets = [
        {
            "title": "KUKA KR Quantec Axis 4 Resolver Commutation Loss",
            "description": "Robot arm halted mid-weld sequence at Station OP10. Fieldbus safe-stop triggered with hardware commutation error on axis 4.",
            "groupId": g1_id,
            "machineType": "Robotics Manipulator",
            "stationId": "ROBOT-CELL-01",
            "stationName": "Battery Pack Assembly Line 01 - Robot Cell 01",
            "priority": "Critical",
            "status": "Open",
            "category": "Error",
            "issueType": "Maintenance",
            "isLineStop": True,
            "lineStopDurationMinutes": 15,
            "responsibleDepartment": "Assy",
            "tags": ["#Robotics", "#KUKA", "#Line01", "#AxisFault"],
            "assignedTechnicianId": "usr-nemeth",
            "assignedTechnicianName": "Zoltán Németh"
        },
        {
            "title": "Cognex AOI 3D Bead Thickness False Rejection Spike",
            "description": "Inspection camera reporting false voids on busbar weld seam #4 under ambient plant illumination shifts.",
            "groupId": g1b_id,
            "machineType": "Automatic Optical Inspection",
            "stationId": "L09-OP270",
            "stationName": "Vision & AOI Quality Station - Cell 270",
            "priority": "High",
            "status": "InProgress",
            "category": "Improvement",
            "issueType": "Quality",
            "isLineStop": False,
            "responsibleDepartment": "Quality",
            "tags": ["#VisionAI", "#Cognex", "#QualityGate", "#FalseReject"],
            "assignedTechnicianId": "usr-sally",
            "assignedTechnicianName": "Engineer Sally"
        },
        {
            "title": "Thermal Paste Dispenser Nozzle Clog & Flow Rate Warning",
            "description": "Two-part polyurethane gap filler flow meter dropped below minimum 12 ml/s specification during module tray dispensing.",
            "groupId": g1a_id,
            "machineType": "Gap Filler",
            "stationId": "L06-OP150",
            "stationName": "Module Fastening & Dispensing Cell - OP150",
            "priority": "Medium",
            "status": "Open",
            "category": "Prevention",
            "issueType": "Maintenance",
            "isLineStop": False,
            "responsibleDepartment": "Assy",
            "tags": ["#Dispensing", "#ThermalPaste", "#Preventive"],
            "assignedTechnicianId": "usr-varga",
            "assignedTechnicianName": "Gábor Varga"
        },
        {
            "title": "TwinCAT 3 EtherCAT Master Distributed Clock Jitter on CNC Spindle",
            "description": "Distributed clock sync jitter exceeds 25 microseconds during high-speed milling passes on stator housing.",
            "groupId": g2_id,
            "machineType": "CNC Milling",
            "stationId": "STATION-CNC-02",
            "stationName": "Powertrain Line 02 - CNC Milling Station",
            "priority": "High",
            "status": "Open",
            "category": "Error",
            "issueType": "Maintenance",
            "isLineStop": False,
            "responsibleDepartment": "Machining",
            "tags": ["#Controls", "#TwinCAT", "#Beckhoff", "#CNC"],
            "assignedTechnicianId": "usr-varga",
            "assignedTechnicianName": "Gábor Varga"
        }
    ]

    created_tickets = []
    for tkt in demo_tickets:
        res = client.create_ticket(tkt)
        ticket_obj = res.get("ticket") or res
        created_tickets.append(ticket_obj)
        client.success("Created Ticket", f"[{ticket_obj.get('ticketNumber')}] {ticket_obj.get('title')}")
        print(f"      Group ID: {ticket_obj.get('groupId')} | Station: {ticket_obj.get('stationId')} | Assigned: {ticket_obj.get('assignedTechnicianName')}")
        print(f"      Tags: {', '.join(ticket_obj.get('tags', []))}")

    # ──────────────────────────────────────────────────────────────────────────
    # STEP 5: VERIFY INTEGRATION VIA API QUERIES
    # ──────────────────────────────────────────────────────────────────────────
    print("\n--------------------------------------------------------------------------------")
    client.log("Step 5", "Verifying ticket integration with Machine Groups, Tags, and Technician Candidates...")

    # Verification A: Query by Machine Group ID
    group_1_tickets = client.get_tickets(params={"groupId": g1_id})
    client.log("Query by Group ID", f"Found {len(group_1_tickets)} tickets for groupId={g1_id}")
    assert any(t.get("title") == "KUKA KR Quantec Axis 4 Resolver Commutation Loss" for t in group_1_tickets), \
        "Expected KUKA ticket in group 1 query"
    client.success("Group Filter Integration Verified", f"Ticket properly associated with Machine Group {g1_id}")

    # Verification B: Query by Tag
    robotics_tickets = client.get_tickets(params={"tag": "#Robotics"})
    client.log("Query by Tag '#Robotics'", f"Found {len(robotics_tickets)} tickets")
    assert any(t.get("title") == "KUKA KR Quantec Axis 4 Resolver Commutation Loss" for t in robotics_tickets), \
        "Expected KUKA ticket in #Robotics tag filter"
    client.success("Tag Filter Integration Verified", "Tag #Robotics returned matching ticket")

    vision_tickets = client.get_tickets(params={"tag": "#VisionAI"})
    client.log("Query by Tag '#VisionAI'", f"Found {len(vision_tickets)} tickets")
    assert any(t.get("title") == "Cognex AOI 3D Bead Thickness False Rejection Spike" for t in vision_tickets), \
        "Expected Cognex AOI ticket in #VisionAI tag filter"
    client.success("Tag Filter Integration Verified", "Tag #VisionAI returned matching ticket")

    # Verification C: Full-text Search across Tags and Fields
    search_results = client.get_tickets(params={"query": "Beckhoff"})
    client.log("Search Query 'Beckhoff'", f"Found {len(search_results)} tickets")
    assert any(t.get("title") == "TwinCAT 3 EtherCAT Master Distributed Clock Jitter on CNC Spindle" for t in search_results), \
        "Expected TwinCAT ticket in 'Beckhoff' search"
    client.success("Search Query Integration Verified", "Full text query matched tags and content")

    # Verification D: Technician Candidate Matching
    candidates = client.get_technician_candidates(role="technician")
    client.log("Technician Candidates", f"Found {len(candidates)} available technicians")
    assert len(candidates) > 0, "Expected at least 1 technician candidate"
    tech_names = [c.get("name") for c in candidates]
    assert any("Zoltán Németh" in n for n in tech_names), "Expected Zoltán Németh in candidates"
    client.success("Technician Integration Verified", f"Technician candidates active: {', '.join(tech_names[:4])}...")

    print("\n================================================================================")
    print("   ALL DEMO SEEDING & INTEGRATION VERIFICATIONS COMPLETED SUCCESSFULLY!        ")
    print("================================================================================")

if __name__ == "__main__":
    main()
