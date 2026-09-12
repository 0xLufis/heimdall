#!/usr/bin/env python3
"""
Generates fixtures/enterprise_plant_dataset.json and frontend/web/fixtures/enterprise_plant_dataset.json
matching the 100 diverse machines, 56 IPC controllers, 60 fake AI users, and 16 organizations.
"""

import json
import os
import uuid
import random

from seed_pipeline import ORGANIZATIONS, FAKE_USERS, LINE_CONFIGS

random.seed(42)

def generate_fixtures():
    # 1. Metadata
    metadata = {
        "version": "2.0.0",
        "plantName": "Smart Factory Giga-01 (AI Synthetic Facility)",
        "plantCode": "SF-GIGA-01",
        "domain": "fake-factory.internal",
        "entraTenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47",
        "createdAt": "2026-09-12T00:00:00Z",
        "description": "Canonical enterprise dataset for 100 automated stations across 8 lines, 56 IPC controllers with TwinCAT & MES, and 60 fake AI-generated users."
    }

    # 2. Organizations (16 orgs)
    orgs = []
    for o in ORGANIZATIONS:
        orgs.append({
            "id": o["id"],
            "name": o["name"],
            "slug": o["slug"],
            "description": o["desc"]
        })

    # 3. Security Groups
    sec_groups = [
        {
            "id": "sg-entra-admins",
            "groupIdentifier": "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c",
            "displayName": "OT Plant Administrators (Synthetic Superusers)",
            "identityProvider": "EntraID",
            "mappedRole": "system_admin",
            "mappedOrganizationName": "Platform Operations (Synthetic AI Guild)",
            "mappedOrgRole": "owner",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-03", "usr-synth-60"]
        },
        {
            "id": "sg-line01-controls",
            "groupIdentifier": "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 01 Controls Engineers",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "controls_engineer",
            "mappedOrganizationName": "Line 01 – Synthetic Audi E-Tron Module Line",
            "mappedOrgRole": "admin",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-01", "usr-synth-02"]
        },
        {
            "id": "sg-line02-assembly",
            "groupIdentifier": "CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 02 Assembly Techs",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "technician",
            "mappedOrganizationName": "Line 02 – Synthetic Audi Battery Pack Line",
            "mappedOrgRole": "member",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-11", "usr-synth-28", "usr-synth-39"]
        },
        {
            "id": "sg-line03-powertrain",
            "groupIdentifier": "CN=SG-Line03-Powertrain,OU=AudiLine03,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 03 Powertrain Specialists",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "engineer",
            "mappedOrganizationName": "Line 03 – Synthetic Audi Powertrain Line",
            "mappedOrgRole": "admin",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-07", "usr-synth-23"]
        },
        {
            "id": "sg-line04-smt",
            "groupIdentifier": "CN=SG-Line04-SMT,OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 04 SMT Placement Leads",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "lead_engineer",
            "mappedOrganizationName": "Line 04 – Synthetic Electronics SMT Placement",
            "mappedOrgRole": "admin",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-04", "usr-synth-17", "usr-synth-21"]
        },
        {
            "id": "sg-line05-welding",
            "groupIdentifier": "CN=SG-Line05-Welding,OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 05 Robotic Welders",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "engineer",
            "mappedOrganizationName": "Line 05 – Synthetic Body-in-White Robotic Welding",
            "mappedOrgRole": "admin",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-09", "usr-synth-10", "usr-synth-22", "usr-synth-33"]
        },
        {
            "id": "sg-line06-pressfit",
            "groupIdentifier": "CN=SG-Line06-PressFit,OU=AudiLine06,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 06 Press Fit Engineers",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "engineer",
            "mappedOrganizationName": "Line 06 – Synthetic Precision Press Fit",
            "mappedOrgRole": "admin",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-15", "usr-synth-25"]
        },
        {
            "id": "sg-line07-metrology",
            "groupIdentifier": "CN=SG-Line07-Metrology,OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 07 Quality Metrologists",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "lead_engineer",
            "mappedOrganizationName": "Line 07 – Synthetic Optical Quality Metrology",
            "mappedOrgRole": "admin",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-05", "usr-synth-13", "usr-synth-30", "usr-synth-53"]
        },
        {
            "id": "sg-line08-eol",
            "groupIdentifier": "CN=SG-Line08-Integration,OU=AudiLine08,OU=ProductionLines,DC=factory,DC=corp",
            "displayName": "Line 08 Vehicle Integration Leads",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "lead_engineer",
            "mappedOrganizationName": "Line 08 – Synthetic End-of-Line Vehicle Integration",
            "mappedOrgRole": "admin",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-14", "usr-synth-31"]
        },
        {
            "id": "sg-plant-maint",
            "groupIdentifier": "CN=SG-Plant-Maintenance,OU=Maintenance,OU=EngineeringDisciplines,DC=factory,DC=corp",
            "displayName": "Plant Maintenance Specialists",
            "identityProvider": "ActiveDirectory",
            "mappedRole": "technician",
            "mappedOrganizationName": "Plant Maintenance (Synthetic AI Guild)",
            "mappedOrgRole": "member",
            "autoCreateOrg": True,
            "isEnabled": True,
            "memberUserIds": ["usr-synth-15", "usr-synth-27", "usr-synth-32", "usr-synth-57"]
        }
    ]

    ORG_TO_SG = {
        "org-line-01": "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp",
        "org-line-02": "CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp",
        "org-line-03": "CN=SG-Line03-Powertrain,OU=AudiLine03,OU=ProductionLines,DC=factory,DC=corp",
        "org-line-04": "CN=SG-Line04-SMT,OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp",
        "org-line-05": "CN=SG-Line05-Welding,OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp",
        "org-line-06": "CN=SG-Line06-PressFit,OU=AudiLine06,OU=ProductionLines,DC=factory,DC=corp",
        "org-line-07": "CN=SG-Line07-Metrology,OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp",
        "org-line-08": "CN=SG-Line08-Integration,OU=AudiLine08,OU=ProductionLines,DC=factory,DC=corp",
        "org-controls": "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp",
        "org-robotics": "CN=SG-Line05-Welding,OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp",
        "org-vision": "CN=SG-Line07-Metrology,OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp",
        "org-smt": "CN=SG-Line04-SMT,OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp",
        "org-assembly": "CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp",
        "org-maintenance": "CN=SG-Plant-Maintenance,OU=Maintenance,OU=EngineeringDisciplines,DC=factory,DC=corp",
        "org-platform": "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c",
        "org-audi-proj": "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp"
    }

    # 4. 60 Users
    users = []
    for uid, uname, uemail, urole, utitle, udept, uorgs in FAKE_USERS:
        # Assign matching security groups
        sg_ids = []
        for org in uorgs:
            if org in ORG_TO_SG:
                sg_ids.append(ORG_TO_SG[org])
        if urole in ("system_admin", "plant_director"):
            sg_ids.append("9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c")
        if not sg_ids:
            sg_ids.append("CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp")

        users.append({
            "id": uid,
            "name": uname,
            "email": uemail,
            "username": uemail.split("@")[0],
            "primaryRole": urole,
            "jobTitle": utitle,
            "department": udept,
            "securityGroupIds": list(set(sg_ids)),
            "presence": {
                "availability": "Available" if uid not in ("usr-synth-08", "usr-synth-18") else "Busy",
                "isOutOfOffice": False
            },
            "mfaPolicy": "always" if urole in ("system_admin", "plant_director") else "weekly"
        })

    # 5. Client PCs (Edge IPCs across 8 lines)
    client_pcs = []
    all_hostnames = []

    for l_cfg in LINE_CONFIGS:
        l_num = l_cfg["line_num"]
        l_name = l_cfg["name"]
        org_id = l_cfg["org_id"]
        vlan = 100 + l_num

        line_ipcs = [
            (f"IPC-L{l_num:02d}-OP030-DEDICATED", "Beckhoff C6030 Fanless Edge", "1-1 Dedicated Station Controller", "Windows 10 IoT Enterprise 2021 LTSC"),
            (f"IPC-L{l_num:02d}-CONVEYOR-MAIN", "Siemens SIMATIC IPC477E Industrial", "1-n Master Pallet Conveyor PLC", "Windows 11 IoT Enterprise LTSC 2024"),
            (f"IPC-L{l_num:02d}-ROB-ALPHA", "Advantech UNO-2484G Industrial PC", "m-n Distributed Robotics Controller Alpha", "Windows 10 IoT Enterprise"),
            (f"IPC-L{l_num:02d}-ROB-BETA", "Advantech UNO-2484G Industrial PC", "m-n Distributed Robotics Controller Beta", "Windows 10 IoT Enterprise"),
            (f"IPC-L{l_num:02d}-OP060-PLC", "Beckhoff C6032 High-Performance IPC", "n-1 Motion & Safety PLC Core", "Windows 11 IoT Enterprise"),
            (f"IPC-L{l_num:02d}-OP060-VISION", "Dell OptiPlex 7090 Micro Edge", "n-1 Real-Time Vision Inspector", "Windows 10 IoT Enterprise"),
            (f"IPC-L{l_num:02d}-OP060-MES-GATE", "Advantech UNO-2271G Edge Gateway", "n-1 Plant MES Gateway Connector", "Debian 12 Bookworm Industrial")
        ]

        for idx, (hn, model, purpose, os_ver) in enumerate(line_ipcs, start=1):
            all_hostnames.append(hn)
            ip = f"192.168.{vlan}.{10 + idx}"
            mac = f"02:65:54:{l_num:02X}:{vlan:02X}:{idx:02X}"

            client_pcs.append({
                "hostname": hn,
                "name": f"{hn} ({purpose})",
                "machineIdentifier": f"HW-{hn}",
                "ipAddress": ip,
                "macAddress": mac,
                "vlanId": vlan,
                "adOuPath": f"OU=AudiLine{l_num:02d},OU=ProductionLines,DC=factory,DC=corp",
                "machineType": "IndustrialController",
                "groupId": l_name,
                "osVersion": os_ver,
                "cmiHardware": {
                    "cpu": {
                        "Name": "Intel(R) Core(TM) i7-1185GRE @ 2.80GHz",
                        "NumberOfCores": 8,
                        "NumberOfLogicalProcessors": 16,
                        "MaxClockSpeed": 2800
                    },
                    "memory": {
                        "Capacity": 34359738368,
                        "Speed": 3200,
                        "Manufacturer": "Innodisk Industrial",
                        "PartNumber": "M4D0-BGS2QC0K"
                    },
                    "disks": [
                        {"Caption": "C:", "Size": 274877906944, "FreeSpace": 150323855360, "FileSystem": "NTFS"},
                        {"Caption": "D:", "Size": 1099511627776, "FreeSpace": 730144440320, "FileSystem": "NTFS"}
                    ],
                    "bios": {
                        "Manufacturer": "Beckhoff Automation GmbH",
                        "SMBIOSBIOSVersion": f"CB3063-{l_num:02d}-V1.4",
                        "SerialNumber": f"BIOS-{hn}"
                    },
                    "computerSystem": {
                        "Name": hn,
                        "Manufacturer": model.split()[0],
                        "Model": model,
                        "TotalPhysicalMemory": 34359738368,
                        "Domain": "fake-factory.internal"
                    },
                    "network": {
                        "Description": "Intel(R) I210 Gigabit Network Connection",
                        "IPAddress": ip,
                        "MACAddress": mac,
                        "DefaultIPGateway": f"192.168.{vlan}.1",
                        "DNSServerSearchOrder": [f"192.168.{vlan}.1", "10.0.0.53"]
                    },
                    "os": {
                        "Caption": f"Microsoft {os_ver}",
                        "Version": "10.0.19045",
                        "BuildNumber": "19045",
                        "OSArchitecture": "64-bit",
                        "InstallDate": "2026-09-01T00:00:00Z"
                    }
                },
                "installedPackages": [
                    f"Beckhoff TwinCAT 3.1 Build 4026.10 (Port 851)",
                    "TcRTEthernet Real-Time Driver v3.1",
                    "Audi MES Production Client v3.1",
                    "Siemens Opcenter MES Connector v4.2",
                    "Cognex In-Sight Explorer 6.5.0",
                    "Wireshark 4.2.4",
                    "Node-RED Industrial Telemetry Daemon"
                ]
            })

    # 6. Active Directory OUs (8 Lines + Tech OUs)
    ad_ous = []
    for l_cfg in LINE_CONFIGS:
        l_num = l_cfg["line_num"]
        vlan = 100 + l_num
        line_hosts = [p["hostname"] for p in client_pcs if p["adOuPath"].startswith(f"OU=AudiLine{l_num:02d}")]
        ad_ous.append({
            "ouPath": f"OU=AudiLine{l_num:02d},OU=ProductionLines,DC=factory,DC=corp",
            "name": f"Audi Production Line {l_num:02d} (OT Subnet)",
            "vlanId": vlan,
            "vlanName": f"VLAN_{vlan}_LINE_{l_num:02d}",
            "subnet": f"192.168.{vlan}.0/24",
            "location": f"Production Corridor Line {l_num:02d}",
            "purpose": "Industrial Automation Pallet Conveyors & Cells",
            "machineType": "IndustrialController",
            "hostCount": len(line_hosts),
            "candidateHostnames": line_hosts
        })

    discipline_ous = [
        {
            "ouPath": "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp",
            "name": "Robotics",
            "vlanId": 10,
            "vlanName": "VLAN 10 - Production Line",
            "subnet": "10.10.10.0/24",
            "location": "Line 06 - Hall A",
            "purpose": "Robotic Pick & Place / Handling",
            "machineType": "Manipulator",
            "hostCount": 2,
            "candidateHostnames": ["IPC-L06-ROB-ALPHA", "IPC-L06-ROB-BETA"]
        },
        {
            "ouPath": "OU=VisionInspection,OU=VLAN20-Quality,DC=factory,DC=corp",
            "name": "VisionInspection",
            "vlanId": 20,
            "vlanName": "VLAN 20 - Optical Quality Inspection",
            "subnet": "10.10.20.0/24",
            "location": "Line 07 - Hall C",
            "purpose": "AOI Defect Classification & Inspection",
            "machineType": "Automatic Optical Inspection",
            "hostCount": 2,
            "candidateHostnames": ["IPC-L07-OP060-VISION", "IPC-L04-OP060-VISION"]
        },
        {
            "ouPath": "OU=MillingMachining,OU=VLAN30-Machining,DC=factory,DC=corp",
            "name": "MillingMachining",
            "vlanId": 30,
            "vlanName": "VLAN 30 - CNC Milling & Machining",
            "subnet": "10.10.30.0/24",
            "location": "Line 03 - Hall A",
            "purpose": "Precision CNC Milling & Spindle Control",
            "machineType": "CNC Machine",
            "hostCount": 1,
            "candidateHostnames": ["IPC-L03-OP030-DEDICATED"]
        },
        {
            "ouPath": "OU=TestingValidation,OU=VLAN40-Testing,DC=factory,DC=corp",
            "name": "TestingValidation",
            "vlanId": 40,
            "vlanName": "VLAN 40 - End of Line Electrical Testing",
            "subnet": "10.10.40.0/24",
            "location": "Line 08 - Hall D",
            "purpose": "High-Voltage Isolation & Leak Testing",
            "machineType": "EOL Tester",
            "hostCount": 1,
            "candidateHostnames": ["IPC-L08-OP030-DEDICATED"]
        },
        {
            "ouPath": "OU=JoiningWelding,OU=VLAN50-Welding,DC=factory,DC=corp",
            "name": "Joining",
            "vlanId": 50,
            "vlanName": "VLAN 50 - Joining & Laser Seam",
            "subnet": "10.10.50.0/24",
            "location": "Line 05 - Hall B",
            "purpose": "Laser & Ultrasonic Joining",
            "machineType": "LaserWelder",
            "hostCount": 2,
            "candidateHostnames": ["IPC-L05-OP060-PLC", "IPC-L05-OP030-DEDICATED"]
        },
        {
            "ouPath": "OU=ConveyorTransport,OU=VLAN60-Conveyors,DC=factory,DC=corp",
            "name": "ConveyorTransport",
            "vlanId": 60,
            "vlanName": "VLAN 60 - Pallet Transfer Conveyors",
            "subnet": "10.10.60.0/24",
            "location": "Central Logistics Spine",
            "purpose": "Main Loop Conveyor Transport & Pallet RFID",
            "machineType": "ConveyorPLC",
            "hostCount": 2,
            "candidateHostnames": ["IPC-L01-CONVEYOR-MAIN", "IPC-L02-CONVEYOR-MAIN"]
        }
    ]
    ad_ous.extend(discipline_ous)

    # 7. Machine Groups (8 groups for the 8 lines)
    machine_groups = []
    for l_cfg in LINE_CONFIGS:
        l_num = l_cfg["line_num"]
        st_ids = [f"L{l_num:02d}-{op}" for op, disp, mt, tech, mfr, handle in l_cfg["stations"]]
        m_types = list(set(mt for op, disp, mt, tech, mfr, handle in l_cfg["stations"]))
        machine_groups.append({
            "id": f"grp-line-{l_num:02d}",
            "name": l_cfg["name"],
            "description": f"Automated 100-station assembly loop for Line {l_num:02d}",
            "parentId": None,
            "machineIds": st_ids,
            "machineTypes": m_types,
            "color": "emerald" if l_num == 1 else "indigo" if l_num == 2 else "cyan" if l_num == 4 else "amber"
        })

    # 8. Technician Rules
    tech_rules = [
        {
            "id": "rule-controls-l1",
            "name": "Line 01 Controls Specialist Rule",
            "technicianId": "usr-synth-01",
            "technicianName": "Synthetica Botman (AI Model v4)",
            "technicianEmail": "synthetica.botman.ai@fake-factory.internal",
            "scopeType": "group",
            "targetId": "grp-line-01",
            "categoryFilter": "PLC",
            "backupTechnicianId": "usr-synth-02",
            "backupTechnicianName": "Robo McControlsFace",
            "assignedByRole": "manager"
        },
        {
            "id": "rule-vision-l4",
            "name": "Line 04 SMT AOI Vision Rule",
            "technicianId": "usr-synth-04",
            "technicianName": "Tensor Flowski",
            "technicianEmail": "tensor.flowski.ai@fake-factory.internal",
            "scopeType": "technology",
            "targetId": "SMT",
            "categoryFilter": "Camera",
            "backupTechnicianId": "usr-synth-16",
            "backupTechnicianName": "Deeplearnington Smyth",
            "assignedByRole": "group_leader"
        }
    ]

    dataset = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "metadata": metadata,
        "organizations": orgs,
        "securityGroups": sec_groups,
        "users": users,
        "activeDirectoryOUs": ad_ous,
        "clientPcs": client_pcs,
        "machineGroups": machine_groups,
        "technicianRules": tech_rules,
        "shiftAbsences": [],
        "mfaPolicies": {
            "system_admin": {"threshold": "always"},
            "engineer": {"threshold": "weekly"},
            "technician": {"threshold": "monthly"}
        }
    }

    # Save to both locations
    p1 = os.path.abspath('fixtures/enterprise_plant_dataset.json')
    p2 = os.path.abspath('frontend/web/fixtures/enterprise_plant_dataset.json')

    for p in [p1, p2]:
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, 'w', encoding='utf-8') as f:
            json.dump(dataset, f, indent=2)
        print(f"✓ Wrote {p} (Users: {len(users)}, Orgs: {len(orgs)}, IPCs: {len(client_pcs)}, OUs: {len(ad_ous)})")

if __name__ == '__main__':
    generate_fixtures()
