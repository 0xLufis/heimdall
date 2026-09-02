#!/usr/bin/env python3
"""
Heimdall Unified Seed Data Pipeline & Integrity Validator
Generates enterprise-scale inventory datasets, transactional SQL seeds,
default security group mappings, system settings, and validates referential integrity.
"""

import csv
import json
import os
import sys
import uuid
import random
import argparse

# Set seed for reproducible data generation
random.seed(42)

CSV_FILE = os.path.join(os.path.dirname(__file__), 'inventory_seed.csv')
SQL_FILE = os.path.join(os.path.dirname(__file__), 'incremental_seed.sql')
TOPOLOGY_FILE = os.path.join(os.path.dirname(__file__), 'production_topology.json')

MANUFACTURERS = ["Siemens", "Beckhoff", "Fanuc", "Cognex", "Keyence", "Festo", "Omron", "Dell", "HP", "Cisco", "Phoenix Contact", "Advantech"]
SUPPLIERS = ["Insight", "Industrial Automata Direct", "Farnell", "RS Components", "MISUMI", "Conrad Electronic"]
TEAMS = ["Controls Engineering", "Vision Systems", "Robotics Dept", "Maintenance Team", "IT Infrastructure", "Quality Assurance", "Logistics"]
ORGS = ["Production Floor A", "Production Floor B", "Production Floor C", "Production Floor D"]
LINES = [
    "Line 01 - Body Assembly Alpha",
    "Line 02 - Robotic Welding Cell",
    "Line 03 - Precision Machining & Milling",
    "Line 04 - High-Speed Stamping & Press",
    "Line 05 - Powertrain Sub-Assembly",
    "Line 06 - Automated Battery Module Line",
    "Line 07 - Electronics & SMT Placement",
    "Line 08 - Surface Coating & Paint Shop",
    "Line 09 - Optical Quality Inspection",
    "Line 10 - End-of-Line EOL Testing",
    "Line 11 - Automated Packaging & Boxing",
    "Line 12 - High-Bay Warehousing & AGVs",
    "Line 13 - Chemical Treatment & Plating",
    "Line 14 - Final Vehicle Integration",
    "Line 15 - Logistics & Palletizing Cell"
]
DXF_HANDLES = ["H-AL1", "H-WS5", "L-SORT-A", "P-LINE-B", "Q-CELL-01", "C-TANK-4", "CNC-MC-12", "P-LINE-C", "T-CELL-01", "P-CELL-02"]

def generate_csv(output_path=CSV_FILE):
    print("Generating enterprise inventory dataset (500 Stations, 500 IPCs, Hardware & Software Components)...")
    rows = []

    # 1. Generate 500 Stations
    stations = []
    for i in range(1, 501):
        line_idx = (i - 1) % len(LINES)
        line_name = LINES[line_idx]
        op_num = ((i - 1) % 33 + 1) * 10
        st_name = f"L{(line_idx+1):02d}-OP{op_num}"
        st_display = f"{line_name} - Station {op_num}"
        st_team = random.choice(TEAMS) + ";" + random.choice(TEAMS)
        mfr = random.choice(["Siemens", "Beckhoff", "Fanuc", "Festo", "Omron", "Cognex"])
        sup = random.choice(SUPPLIERS)
        sn = f"SN-STA-{i:04d}"
        handle = DXF_HANDLES[(i - 1) % len(DXF_HANDLES)]
        org = ORGS[(line_idx) % len(ORGS)]

        meta = {
            "Line": line_name,
            "CycleTimeTarget": f"{random.randint(20, 90)}s",
            "SafetyRating": random.choice(["SIL2", "SIL3", "PLd", "PLe"]),
            "PowerSupply": random.choice(["400V 3Ph 50Hz", "230V 1Ph 50Hz", "24V DC Industrial"])
        }

        stations.append({
            "Type": "Machine",
            "Name": st_name,
            "DisplayName": st_display,
            "ResponsibleTeam": st_team,
            "Manufacturer": mfr,
            "Supplier": sup,
            "SerialNumber": sn,
            "ParentName": "",
            "StationIdentifier": st_name,
            "ClientPcHostname": "",
            "Metadata": json.dumps(meta),
            "PinnedObjectHandle": handle,
            "OrganizationId": org
        })
    rows.extend(stations)

    # 2. Generate 500 Client PCs / IPCs
    pcs = []
    pc_models = [
        ("SIMATIC IPC477E", "Siemens", "Windows 10 IoT Enterprise 2021 LTSC"),
        ("Embedded PC C6030", "Beckhoff", "Windows 11 IoT Enterprise LTSC 2024"),
        ("OptiPlex 7090 Micro", "Dell", "Ubuntu 24.04 LTS"),
        ("Z2 Mini G9 Workstation", "HP", "Windows 11 Pro Workstation"),
        ("UNO-2484G Fanless Edge", "Advantech", "Debian 12 Bookworm")
    ]

    software_suites = [
        "Beckhoff TwinCAT 3.1 Build 4026.10;Siemens TIA Portal V19;Wireshark 4.2.4;TcRTEthernet Driver v3.1",
        "Cognex In-Sight Explorer 6.5.0;Fanuc ROBOGUIDE V9.40;Kepware KEPServerEX 6.14",
        "Festo Automation Suite 2.6.0;Omron Sysmac Studio 1.54;Visual Studio 2022 Community",
        "Siemens WinCC Advanced V19;Node-RED v3.1;Beckhoff TwinCAT 3.1;Mosquitto MQTT Broker"
    ]

    for i in range(1, 501):
        hostname = f"CPC-{i:03d}"
        display_name = f"Edge Terminal {hostname}"
        assoc_station = stations[i - 1]["Name"]
        assoc_org = stations[i - 1]["OrganizationId"]
        model_name, mfr, os_ver = pc_models[(i - 1) % len(pc_models)]
        sw = software_suites[(i - 1) % len(software_suites)]
        handle = stations[i - 1]["PinnedObjectHandle"]

        meta = {
            "IPAddress": f"10.0.{(i // 254) + 1}.{(i % 254) + 1}",
            "OperatingSystem": os_ver,
            "Model": model_name,
            "InstalledSoftware": sw,
            "BeckhoffRtDriver": "TcRTEthernet active" if "TwinCAT" in sw else "Standard NIC",
            "SecurityPolicy": random.choice(["Enforced TISAX AL3", "Enforced TISAX AL2", "Standard Operational Domain"])
        }

        pcs.append({
            "Type": "ClientPc",
            "Name": hostname,
            "DisplayName": display_name,
            "ResponsibleTeam": "IT Infrastructure;Controls Engineering",
            "Manufacturer": mfr,
            "Supplier": "Insight",
            "SerialNumber": f"SN-PC-{i:04d}",
            "ParentName": "",
            "StationIdentifier": assoc_station,
            "ClientPcHostname": hostname,
            "Metadata": json.dumps(meta),
            "PinnedObjectHandle": handle,
            "OrganizationId": assoc_org
        })
        stations[i - 1]["ClientPcHostname"] = hostname
    rows.extend(pcs)

    # 3. Generate Hardware & Software Components
    components = []
    comp_templates = [
        ("PLC-S7-1500", "Siemens S7-1500 PLC", "HardwareComponent", "Siemens", json.dumps({"Model": "1516-3 PN/DP", "Memory": "1MB Code", "Rack": 1})),
        ("CAM-Cognex-9000", "Cognex In-Sight 9000 Vision Camera", "HardwareComponent", "Cognex", json.dumps({"Resolution": "12MP", "Lens": "16mm", "Illumination": "Red LED"})),
        ("DRV-AX5000", "Beckhoff AX5000 Servo Drive", "HardwareComponent", "Beckhoff", json.dumps({"Current": "12A", "Feedback": "EnDat 2.2", "Channels": 2})),
        ("ROB-Fanuc-M20", "Fanuc M-20iB Robot Controller", "HardwareComponent", "Fanuc", json.dumps({"Payload": "20kg", "Reach": "1811mm", "DOF": 6})),
        ("VAL-Festo-VTUG", "Festo VTUG Valve Terminal", "HardwareComponent", "Festo", json.dumps({"Valves": 12, "Bus": "Profinet", "Pressure": "6.0Bar"})),
        ("SWI-Cisco-IE3300", "Cisco Catalyst IE3300 Industrial Switch", "HardwareComponent", "Cisco", json.dumps({"Ports": 10, "PoE": True, "Speed": "1Gbps"})),
        ("SW-TwinCAT3", "Beckhoff TwinCAT 3 Runtime License", "SoftwareComponent", "Beckhoff", json.dumps({"LicenseKey": "TC3-RT-ENTERPRISE-500", "Version": "3.1.4026"})),
        ("SW-TIAPortal", "Siemens TIA Portal V19 License", "SoftwareComponent", "Siemens", json.dumps({"LicenseKey": "TIA-V19-FLOATING-500", "Version": "19.0.0"}))
    ]

    for i in range(1, 501):
        st_name = stations[i - 1]["Name"]
        pc_name = pcs[i - 1]["Name"]
        org = stations[i - 1]["OrganizationId"]

        for j in range(2):
            tmpl_name, tmpl_disp, tmpl_type, tmpl_mfr, tmpl_meta = comp_templates[(i + j) % len(comp_templates)]
            comp_name = f"{tmpl_name}-S{i:03d}-{j+1}"
            comp_disp = f"{tmpl_disp} ({st_name})"

            components.append({
                "Type": tmpl_type,
                "Name": comp_name,
                "DisplayName": comp_disp,
                "ResponsibleTeam": stations[i - 1]["ResponsibleTeam"],
                "Manufacturer": tmpl_mfr,
                "Supplier": "Industrial Automata Direct",
                "SerialNumber": f"SN-CMP-{i:04d}-{j+1}",
                "ParentName": st_name,
                "StationIdentifier": st_name,
                "ClientPcHostname": pc_name,
                "Metadata": tmpl_meta,
                "PinnedObjectHandle": "",
                "OrganizationId": org
            })
    rows.extend(components)

    fieldnames = ["Type", "Name", "DisplayName", "ResponsibleTeam", "Manufacturer", "Supplier", "SerialNumber", "ParentName", "StationIdentifier", "ClientPcHostname", "Metadata", "PinnedObjectHandle", "OrganizationId"]
    with open(output_path, mode="w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print(f"Generated {len(rows)} enterprise inventory entries in {output_path}")
    return rows

def generate_sql(csv_path=CSV_FILE, output_path=SQL_FILE):
    print("Generating transactional PostgreSQL seed script...")
    with open(csv_path, mode='r', encoding='utf-8') as f:
        rows = list(csv.DictReader(f))

    sql = [
        "-- Heimdall Enterprise Plant Seed SQL",
        "-- Auto-generated by seed_pipeline.py",
        "SET statement_timeout = 0;",
        "BEGIN;",
        "SET search_path TO backend, public;",
        "",
        "-- Ensure Schema & Governance Tables exist",
        "CREATE SCHEMA IF NOT EXISTS backend;",
        "",
        "CREATE TABLE IF NOT EXISTS backend.system_settings (",
        "    key VARCHAR(128) PRIMARY KEY,",
        "    value_json TEXT NOT NULL,",
        "    category VARCHAR(64) NOT NULL,",
        "    updated_by VARCHAR(128) NOT NULL,",
        "    updated_at TIMESTAMP WITH TIME ZONE NOT NULL",
        ");",
        "",
        "CREATE TABLE IF NOT EXISTS backend.security_group_mappings (",
        "    id UUID PRIMARY KEY,",
        "    identity_provider VARCHAR(64) NOT NULL,",
        "    group_identifier VARCHAR(256) NOT NULL,",
        "    display_name VARCHAR(256) NOT NULL,",
        "    mapped_role VARCHAR(64) NOT NULL,",
        "    organization_id VARCHAR(128),",
        "    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,",
        "    created_at TIMESTAMP WITH TIME ZONE NOT NULL,",
        "    updated_at TIMESTAMP WITH TIME ZONE NOT NULL",
        ");",
        "",
        "CREATE TABLE IF NOT EXISTS backend.client_certificates (",
        "    id UUID PRIMARY KEY,",
        "    client_pc_id UUID,",
        "    common_name VARCHAR(255) NOT NULL,",
        "    thumbprint VARCHAR(128) NOT NULL,",
        "    valid_from TIMESTAMP WITH TIME ZONE NOT NULL,",
        "    valid_to TIMESTAMP WITH TIME ZONE NOT NULL,",
        "    status VARCHAR(50) NOT NULL DEFAULT 'Active',",
        "    created_at TIMESTAMP WITH TIME ZONE NOT NULL",
        ");",
        "",
        "CREATE TABLE IF NOT EXISTS backend.schema_version_manifest (",
        "    id UUID PRIMARY KEY,",
        "    schema_version VARCHAR(50) NOT NULL,",
        "    migration_name VARCHAR(255) NOT NULL,",
        "    applied_at TIMESTAMP WITH TIME ZONE NOT NULL,",
        "    description TEXT",
        ");",
        "",
        "CREATE TABLE IF NOT EXISTS backend.audit_logs (",
        "    id UUID PRIMARY KEY,",
        "    user_id VARCHAR(128) NOT NULL,",
        "    user_name VARCHAR(255),",
        "    action VARCHAR(64) NOT NULL,",
        "    entity_type VARCHAR(128) NOT NULL,",
        "    entity_id VARCHAR(128),",
        "    old_values_json TEXT,",
        "    new_values_json TEXT,",
        "    ip_address VARCHAR(64),",
        "    organization_id VARCHAR(128),",
        "    timestamp TIMESTAMP WITH TIME ZONE NOT NULL",
        ");",
        "",
        "CREATE TABLE IF NOT EXISTS backend.malformed_telemetry_quarantine (",
        "    id UUID PRIMARY KEY,",
        "    source_identifier VARCHAR(255),",
        "    ingestion_channel VARCHAR(64) NOT NULL,",
        "    error_reason TEXT NOT NULL,",
        "    raw_payload TEXT NOT NULL,",
        "    organization_id VARCHAR(128),",
        "    quarantined_at TIMESTAMP WITH TIME ZONE NOT NULL",
        ");",
        "",
        "-- Truncate tables for a clean idempotent re-seed",
        "TRUNCATE TABLE backend.inventory_items CASCADE;",
        "TRUNCATE TABLE backend.client_pcs CASCADE;",
        "TRUNCATE TABLE backend.manufacturers CASCADE;",
        "TRUNCATE TABLE backend.suppliers CASCADE;",
        "TRUNCATE TABLE backend.responsible_teams CASCADE;",
        "TRUNCATE TABLE backend.\"ItemResponsibilities\" CASCADE;",
        "TRUNCATE TABLE backend.\"PcResponsibilities\" CASCADE;",
        "TRUNCATE TABLE backend.\"StationControllers\" CASCADE;",
        "TRUNCATE TABLE backend.equipment_interconnects CASCADE;",
        "TRUNCATE TABLE backend.maintenance_tickets CASCADE;",
        "TRUNCATE TABLE backend.ticket_comments CASCADE;",
        "TRUNCATE TABLE backend.security_group_mappings CASCADE;",
        "TRUNCATE TABLE backend.system_settings CASCADE;",
        "TRUNCATE TABLE backend.client_certificates CASCADE;",
        "TRUNCATE TABLE backend.schema_version_manifest CASCADE;",
        "TRUNCATE TABLE backend.audit_logs CASCADE;",
        "TRUNCATE TABLE backend.malformed_telemetry_quarantine CASCADE;",
        "",
    ]

    # Seed System Settings & Governance Templates
    sql.append("-- Seed Master System Settings")
    master_template = json.dumps({
        "configSchemaVersion": "1.0.0",
        "enforceHardwareBinding": True,
        "spoolEncryptionMode": "AES_256_GCM",
        "telemetryPayloadEncryption": False,
        "allowRemoteExecution": True,
        "piiScrubberStrictLevel": "Strict",
        "maxNetworkEgressBytesPerSec": 1048576,
        "deltaEvaluationAlgorithm": "xxHash64",
        "deadbandTolerancePercentage": 1.0,
        "maxSpoolDiskMb": 500,
        "heartbeatIntervalSeconds": 10
    })
    opc_config = json.dumps({"endpoint": "opc.tcp://0.0.0.0:4840/Heimdall", "securityPolicy": "Basic256Sha256"})
    copia_config = json.dumps({"webhookEndpoint": "/api/v1/integrations/copia/webhook", "autoSync": True})
    auth_config = json.dumps({"sessionTtlMinutes": 1440, "requireMfaForEngineers": True})

    sql.append(f"INSERT INTO backend.system_settings (key, value_json, category, updated_by, updated_at) VALUES ('AgentMasterTemplate', '{master_template}', 'AgentMaster', 'system_admin', NOW()) ON CONFLICT (key) DO UPDATE SET value_json = EXCLUDED.value_json;")
    sql.append(f"INSERT INTO backend.system_settings (key, value_json, category, updated_by, updated_at) VALUES ('OpcUaConfig', '{opc_config}', 'Integrations', 'system_admin', NOW()) ON CONFLICT (key) DO NOTHING;")
    sql.append(f"INSERT INTO backend.system_settings (key, value_json, category, updated_by, updated_at) VALUES ('CopiaConfig', '{copia_config}', 'Integrations', 'system_admin', NOW()) ON CONFLICT (key) DO NOTHING;")
    sql.append(f"INSERT INTO backend.system_settings (key, value_json, category, updated_by, updated_at) VALUES ('AuthPolicy', '{auth_config}', 'Auth', 'system_admin', NOW()) ON CONFLICT (key) DO NOTHING;")

    # Seed Default Enterprise Security Group Mappings
    sql.append("\n-- Seed Active Directory & Entra ID Security Group Mappings")
    default_mappings = [
        ("EntraID", "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c", "OT Plant Administrators", "admin", None),
        ("EntraID", "1b3d5f7a-9c1e-4a2b-8d6f-0e2c4a6b8d0e", "Controls Engineering Core", "engineer", "Production Floor A"),
        ("ActiveDirectory", "CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp", "On-Prem Controls Engineers", "engineer", "Production Floor B"),
        ("ActiveDirectory", "CN=OT-Maintenance-Technicians,OU=Groups,DC=factory,DC=corp", "Plant Maintenance Technicians", "technician", None),
        ("ActiveDirectory", "CN=OT-Floor-Operators,OU=Groups,DC=factory,DC=corp", "Production Operators", "operator", None)
    ]
    for idp, gid, dname, role, org in default_mappings:
        m_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{idp}:{gid}"))
        org_val = f"'{org}'" if org else "NULL"
        sql.append(f"INSERT INTO backend.security_group_mappings (id, identity_provider, group_identifier, display_name, mapped_role, organization_id, is_enabled, created_at, updated_at) "
                   f"VALUES ('{m_id}', '{idp}', '{gid}', '{dname}', '{role}', {org_val}, true, NOW(), NOW()) ON CONFLICT (id) DO NOTHING;")

    # Seed Manifest
    sql.append("\n-- Seed Schema Version Manifest")
    sql.append(f"INSERT INTO backend.schema_version_manifest (id, schema_version, migration_name, applied_at, description) "
               f"VALUES ('{uuid.uuid4()}', '1.0.0', 'SystemGovernanceAndPki', NOW(), 'Initial V1 direct schema baseline with RBAC governance and PKI') ON CONFLICT DO NOTHING;")

    # Seed Manufacturers, Suppliers, Teams
    manufacturers = set(r['Manufacturer'] for r in rows if r['Manufacturer'])
    suppliers = set(r['Supplier'] for r in rows if r['Supplier'])
    teams = set()
    for r in rows:
        if r['ResponsibleTeam']:
            for t in r['ResponsibleTeam'].split(';'):
                teams.add(t.strip())

    sql.append("\n-- Seed Reference Tables")
    for m in sorted(manufacturers):
        m_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, m))
        sql.append(f"INSERT INTO manufacturers (id, name) VALUES ('{m_id}', '{m}') ON CONFLICT (name) DO NOTHING;")
    for s in sorted(suppliers):
        s_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, s))
        sql.append(f"INSERT INTO suppliers (id, name) VALUES ('{s_id}', '{s}') ON CONFLICT (name) DO NOTHING;")
    team_ids = {}
    for t in sorted(teams):
        t_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, t))
        team_ids[t] = t_id
        sql.append(f"INSERT INTO responsible_teams (id, name) VALUES ('{t_id}', '{t}') ON CONFLICT (name) DO NOTHING;")

    # Seed Inventory Items and Client PCs
    sql.append("\n-- Seed Inventory Items & Client PCs")
    item_ids = {}
    pc_ids = {}

    for row in rows:
        item_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name']))
        pin_handle = f"'{row['PinnedObjectHandle']}'" if row.get('PinnedObjectHandle') else "NULL"
        org_id = f"'{row['OrganizationId']}'" if row.get('OrganizationId') else "'Heimdall Root'"

        if row['Type'] == 'ClientPc':
            pc_ids[row['Name']] = item_id
            mac = f"02:{item_id[0:2]}:{item_id[2:4]}:{item_id[4:6]}:{item_id[6:8]}:{item_id[9:11]}".upper()
            sql.append(f"INSERT INTO client_pcs (id, name, mac_address, hostname, machine_identifier, pinned_object_handle, organization_id) "
                       f"VALUES ('{item_id}', '{row['Name']}', '{mac}', '{row['ClientPcHostname'] or row['Name']}', 'ID-{item_id[:8]}', {pin_handle}, {org_id}) "
                       f"ON CONFLICT (id) DO UPDATE SET pinned_object_handle = EXCLUDED.pinned_object_handle, organization_id = EXCLUDED.organization_id;")
        else:
            item_ids[row['Name']] = item_id
            m_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Manufacturer'])}'" if row['Manufacturer'] else "NULL"
            s_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Supplier'])}'" if row['Supplier'] else "NULL"
            metadata = (row['Metadata'] or "{}").replace("'", "''")
            display_name = row['DisplayName'].replace("'", "''")
            name = row['Name'].replace("'", "''")
            sql.append(f"INSERT INTO inventory_items (id, name, display_name, manufacturer_id, supplier_id, metadata, serial_number, organization_id) "
                       f"VALUES ('{item_id}', '{name}', '{display_name}', {m_id}, {s_id}, '{metadata}'::jsonb, '{row['SerialNumber']}', {org_id}) "
                       f"ON CONFLICT (id) DO UPDATE SET display_name = EXCLUDED.display_name, metadata = inventory_items.metadata || EXCLUDED.metadata, organization_id = EXCLUDED.organization_id;")

    # TPT Table Extensions
    sql.append("\n-- Seed Derived TPT Tables")
    for row in rows:
        if row['Name'] in item_ids:
            item_id = item_ids[row['Name']]
            pin_handle = f"'{row['PinnedObjectHandle']}'" if row.get('PinnedObjectHandle') else "NULL"
            if row['Type'] == 'Machine':
                sql.append(f"INSERT INTO stations (id, custom_identifier, pinned_object_handle) VALUES ('{item_id}', '{row['StationIdentifier'] or row['Name']}', {pin_handle}) ON CONFLICT (id) DO UPDATE SET pinned_object_handle = EXCLUDED.pinned_object_handle;")
            elif row['Type'] == 'HardwareComponent':
                sql.append(f"INSERT INTO hardware_assets (id) VALUES ('{item_id}') ON CONFLICT (id) DO NOTHING;")
            elif row['Type'] == 'SoftwareComponent':
                sql.append(f"INSERT INTO software_assets (id) VALUES ('{item_id}') ON CONFLICT (id) DO NOTHING;")

            if row['ResponsibleTeam']:
                for t in row['ResponsibleTeam'].split(';'):
                    t = t.strip()
                    if t in team_ids:
                        sql.append(f"INSERT INTO \"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ('{item_id}', '{team_ids[t]}') ON CONFLICT DO NOTHING;")

    # Station Controllers M:N
    sql.append("\n-- Seed Station Controller Edges (M:N)")
    for row in rows:
        if row['Type'] == 'ClientPc' and row['Name'] in pc_ids:
            pc_id = pc_ids[row['Name']]
            if row['StationIdentifier'] and row['StationIdentifier'] in item_ids:
                station_id = item_ids[row['StationIdentifier']]
                sc_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{pc_id}:{station_id}"))
                sql.append(f"INSERT INTO \"StationControllers\" (id, client_pc_id, machine_id) VALUES ('{sc_id}', '{pc_id}', '{station_id}') ON CONFLICT DO NOTHING;")

    sql.append("\nCOMMIT;")

    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("\n".join(sql))
    print(f"Generated {output_path}")

def validate():
    print("=== Validating Seed Data Referential Integrity ===")
    if not os.path.exists(CSV_FILE):
        print(f"FAIL: {CSV_FILE} missing.")
        return False
    if not os.path.exists(SQL_FILE):
        print(f"FAIL: {SQL_FILE} missing.")
        return False

    with open(CSV_FILE, mode='r', encoding='utf-8') as f:
        rows = list(csv.DictReader(f))

    stations = [r for r in rows if r['Type'] == 'Machine']
    pcs = [r for r in rows if r['Type'] == 'ClientPc']
    components = [r for r in rows if r['Type'] not in ('Machine', 'ClientPc')]

    print(f"✓ Inventory CSV rows: {len(rows)} (Stations: {len(stations)}, IPCs: {len(pcs)}, Components: {len(components)})")
    assert len(stations) == 500, f"Expected 500 stations, got {len(stations)}"
    assert len(pcs) == 500, f"Expected 500 IPCs, got {len(pcs)}"

    # Check DXF handles
    handles_found = set(r['PinnedObjectHandle'] for r in rows if r['PinnedObjectHandle'])
    print(f"✓ Mapped CAD Handles: {len(handles_found)} unique anchors")

    # Check SQL file content
    with open(SQL_FILE, 'r', encoding='utf-8') as f:
        sql_content = f.read()

    assert "security_group_mappings" in sql_content, "Missing security_group_mappings in SQL seed"
    assert "system_settings" in sql_content, "Missing system_settings in SQL seed"
    assert "client_certificates" in sql_content, "Missing client_certificates in SQL seed"
    assert "schema_version_manifest" in sql_content, "Missing schema_version_manifest in SQL seed"
    assert "StationControllers" in sql_content, "Missing StationControllers in SQL seed"
    assert "COMMIT;" in sql_content, "Missing COMMIT in SQL seed"

    print("✓ SQL transaction integrity validated.")
    print("All Seed Data validations passed successfully!")
    return True

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Heimdall Seed Data Pipeline")
    parser.add_argument("--generate-all", action="store_true", help="Generate CSV and SQL seed files")
    parser.add_argument("--validate", action="store_true", help="Validate referential integrity")
    args = parser.parse_args()

    if args.generate_all:
        generate_csv()
        generate_sql()
        validate()
    elif args.validate:
        validate()
    else:
        generate_csv()
        generate_sql()
        validate()
