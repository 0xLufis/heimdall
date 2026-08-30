import csv
import json
import uuid
import os

def generate_sql(csv_path, output_path):
    with open(csv_path, mode='r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        rows = list(reader)

    sql_statements = [
        "--- Heimdall Incremental Seed SQL ---",
        "SET search_path TO backend, public;",
        "BEGIN;", # Use a transaction
        "",
        "-- Truncate existing data for a clean re-seed",
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
        "TRUNCATE TABLE backend.ticket_attachments CASCADE;",
        "",
    ]

    # 1. Collect unique entities
    manufacturers = set()
    suppliers = set()
    teams = set()
    
    for row in rows:
        if row['Manufacturer']: manufacturers.add(row['Manufacturer'])
        if row['Supplier']: suppliers.add(row['Supplier'])
        if row['ResponsibleTeam']:
            for t in row['ResponsibleTeam'].split(';'):
                teams.add(t.strip())

    # 2. Seed Manufacturers
    sql_statements.append("-- Seed Manufacturers")
    for m in manufacturers:
        m_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, m))
        sql_statements.append(f"INSERT INTO manufacturers (id, name) VALUES ('{m_id}', '{m}') ON CONFLICT (name) DO NOTHING;")
    
    # 3. Seed Suppliers
    sql_statements.append("\n-- Seed Suppliers")
    for s in suppliers:
        s_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, s))
        sql_statements.append(f"INSERT INTO suppliers (id, name) VALUES ('{s_id}', '{s}') ON CONFLICT (name) DO NOTHING;")

    # 4. Seed Teams
    sql_statements.append("\n-- Seed Teams")
    team_ids = {}
    for t in teams:
        t_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, t))
        team_ids[t] = t_id
        sql_statements.append(f"INSERT INTO responsible_teams (id, name) VALUES ('{t_id}', '{t}') ON CONFLICT (name) DO NOTHING;")

    # 5. Seed Items
    sql_statements.append("\n-- Seed Inventory Items")
    item_ids = {}
    pc_ids = {}
    
    # First pass: Create base items and PCs
    for row in rows:
        item_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name']))
        pin_handle = f"'{row['PinnedObjectHandle']}'" if row.get('PinnedObjectHandle') else "NULL"
        org_id = f"'{row['OrganizationId']}'" if row.get('OrganizationId') else "'Heimdall Root'"
        
        if row['Type'] == 'ClientPc':
            pc_ids[row['Name']] = item_id
            mac = f"02:{item_id[0:2]}:{item_id[2:4]}:{item_id[4:6]}:{item_id[6:8]}:{item_id[9:11]}".upper()
            sql_statements.append(f"INSERT INTO client_pcs (id, name, mac_address, hostname, machine_identifier, pinned_object_handle) "
                                 f"VALUES ('{item_id}', '{row['Name']}', '{mac}', '{row['ClientPcHostname'] or row['Name']}', 'ID-{item_id[:8]}', {pin_handle}) "
                                 f"ON CONFLICT (id) DO UPDATE SET pinned_object_handle = EXCLUDED.pinned_object_handle;")
        else:
            item_ids[row['Name']] = item_id
            m_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Manufacturer'])}'" if row['Manufacturer'] else "NULL"
            s_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Supplier'])}'" if row['Supplier'] else "NULL"
            
            # Escape single quotes for SQL
            metadata_raw = row['Metadata'] if row['Metadata'] else "{}"
            metadata = metadata_raw.replace("'", "''")
            display_name = row['DisplayName'].replace("'", "''")
            name = row['Name'].replace("'", "''")
            
            # Base item
            sql_statements.append(f"INSERT INTO inventory_items (id, name, display_name, manufacturer_id, supplier_id, metadata, serial_number, organization_id) "
                                 f"VALUES ('{item_id}', '{name}', '{display_name}', {m_id}, {s_id}, '{metadata}'::jsonb, '{row['SerialNumber']}', {org_id}) "
                                 f"ON CONFLICT (id) DO UPDATE SET display_name = EXCLUDED.display_name, metadata = inventory_items.metadata || EXCLUDED.metadata, organization_id = EXCLUDED.organization_id;")

    # Second pass: Specific TPT tables
    sql_statements.append("\n-- Seed Derived Tables")
    for row in rows:
        if row['Name'] in item_ids:
            item_id = item_ids[row['Name']]
            pin_handle = f"'{row['PinnedObjectHandle']}'" if row.get('PinnedObjectHandle') else "NULL"
            if row['Type'] == 'Machine':
                sql_statements.append(f"INSERT INTO stations (id, custom_identifier, pinned_object_handle) VALUES ('{item_id}', '{row['StationIdentifier'] or row['Name']}', {pin_handle}) "
                                     f"ON CONFLICT (id) DO UPDATE SET pinned_object_handle = EXCLUDED.pinned_object_handle;")
            elif row['Type'] == 'HardwareComponent':
                sql_statements.append(f"INSERT INTO hardware_assets (id) VALUES ('{item_id}') ON CONFLICT (id) DO NOTHING;")
            elif row['Type'] == 'SoftwareComponent':
                sql_statements.append(f"INSERT INTO software_assets (id) VALUES ('{item_id}') ON CONFLICT (id) DO NOTHING;")
            elif row['Type'] == 'PcHardware':
                sql_statements.append(f"INSERT INTO pc_hardware (id) VALUES ('{item_id}') ON CONFLICT (id) DO NOTHING;")

            # Item Responsibilities
            if row['ResponsibleTeam']:
                for t in row['ResponsibleTeam'].split(';'):
                    t = t.strip()
                    sql_statements.append(f"INSERT INTO \"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ('{item_id}', '{team_ids[t]}') ON CONFLICT DO NOTHING;")

    # Third pass: Update relationships
    sql_statements.append("\n-- Update Relationships")
    for row in rows:
        if row['Name'] in item_ids:
            item_id = item_ids[row['Name']]
            
            # Parent-Child (Tree)
            if row['ParentName'] and row['ParentName'] in item_ids:
                parent_id = item_ids[row['ParentName']]
                sql_statements.append(f"UPDATE inventory_items SET parent_id = '{parent_id}' WHERE id = '{item_id}';")
            
            # Link to PC
            if row['ClientPcHostname'] and row['ClientPcHostname'] in pc_ids:
                pc_id = pc_ids[row['ClientPcHostname']]
                sql_statements.append(f"UPDATE inventory_items SET client_pc_id = '{pc_id}' WHERE id = '{item_id}';")

        # Many-to-Many Station Controllers
        if row['Type'] == 'ClientPc' and row['Name'] in pc_ids:
            pc_id = pc_ids[row['Name']]
            if row['StationIdentifier'] and row['StationIdentifier'] in item_ids:
                station_id = item_ids[row['StationIdentifier']]
                sc_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{pc_id}:{station_id}"))
                sql_statements.append(f"INSERT INTO \"StationControllers\" (id, client_pc_id, machine_id) VALUES ('{sc_id}', '{pc_id}', '{station_id}') ON CONFLICT DO NOTHING;")

    # Fourth pass: Equipment Interconnects (Graph Links between Inventory Items)
    sql_statements.append("\n-- Seed Equipment Interconnects")
    interconnects = [
        ("PLC-01", "SEN-01", "PROFINET", "192.168.1.101:502", "PROFINET IO", "Active"),
        ("PLC-01", "CAM-01", "EtherNet/IP", "192.168.1.102:44818", "EtherNet/IP", "Active"),
        ("PLC-02", "ROB-01", "OPC UA", "opc.tcp://192.168.1.120:4840", "OPC UA", "Active"),
        ("PLC-03", "VAL-01", "Modbus TCP", "192.168.2.50:502", "Modbus TCP", "Active"),
        ("PLC-01", "DRV-01", "EtherCAT", "EtherCAT Master Port 1", "EtherCAT", "Active"),
        ("PLC-04", "CAM-02", "EtherNet/IP", "192.168.3.10:44818", "EtherNet/IP", "Active")
    ]
    for src, tgt, itype, addr, proto, status in interconnects:
        src_id = item_ids.get(src)
        tgt_id = item_ids.get(tgt)
        if src_id and tgt_id:
            ic_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{src}->{tgt}"))
            sql_statements.append(
                f"INSERT INTO equipment_interconnects (id, source_equipment_id, target_equipment_id, interconnect_type, port_or_address, protocol, status, created_at) "
                f"VALUES ('{ic_id}', '{src_id}', '{tgt_id}', '{itype}', '{addr}', '{proto}', '{status}', NOW()) ON CONFLICT DO NOTHING;"
            )

    # Fifth pass: Maintenance Tickets & Comments
    sql_statements.append("\n-- Seed Maintenance Tickets")
    tickets = [
        {
            "title": "PROFINET Bus Failure on Station 10 Loading",
            "desc": "Intermittent communication loss between Siemens S7-1500 PLC and Barcode Reader SCN-01. Check cabling and termination resistor.",
            "status": "InProgress",
            "priority": "Critical",
            "eq": "PLC-01",
            "pc": "CPC-01",
            "station": "OP10-Load",
            "assigned": "Controls Engineering",
            "created_by": "Operator_FloorA",
            "comments": [
                ("Lead Engineer", "Inspected cable assembly at Port 2. Detected loose RJ45 connector latch."),
                ("Field Tech", "Replacement industrial M12 connector ordered from RS Components.")
            ]
        },
        {
            "title": "Vision System Camera 01 Lens Calibration Required",
            "desc": "Cognex In-Sight 9000 lens blur detected after station OP20 cleaning. Re-calibrate focal distance.",
            "status": "Open",
            "priority": "High",
            "eq": "CAM-01",
            "pc": "CPC-02",
            "station": "OP20-Vision",
            "assigned": "Vision Systems",
            "created_by": "QA_Inspector",
            "comments": [
                ("QA Supervisor", "Quality inspection pass rate dropped from 99.8% to 94.1%. High priority.")
            ]
        },
        {
            "title": "Drill Tool Spindle Wear Warning on OP30",
            "desc": "Spindle motor temperature exceeds 75C baseline. Inspect lubrication and bearings.",
            "status": "InProgress",
            "priority": "Medium",
            "eq": "DRV-01",
            "pc": "CPC-03",
            "station": "OP30-Drill",
            "assigned": "Maintenance Team",
            "created_by": "Telemetry_Alert",
            "comments": [
                ("Maintenance Tech", "Grease levels checked. Ordering replacement bearing kit.")
            ]
        },
        {
            "title": "Fanuc Robot Arm Weld Joint 3 Overheat",
            "desc": "Axis 3 torque current spiking above 85%. Perform servo drive thermal check.",
            "status": "Open",
            "priority": "Critical",
            "eq": "ROB-01",
            "pc": "CPC-03",
            "station": "OP40-Weld",
            "assigned": "Robotics Dept",
            "created_by": "Safety_Auditor",
            "comments": []
        },
        {
            "title": "Pneumatic Valve Terminal VTUG Pressure Drop",
            "desc": "Air line supply pressure dropped to 3.8 Bar on Station 60 Packaging line.",
            "status": "Resolved",
            "priority": "Low",
            "eq": "VAL-01",
            "pc": "CPC-05",
            "station": "OP60-Pack",
            "assigned": "Maintenance Team",
            "created_by": "Shift_Supervisor",
            "comments": [
                ("Pneumatics Specialist", "Fixed O-ring seal leak on manifold inlet port. Pressure restored to 6.0 Bar.")
            ]
        }
    ]

    for t in tickets:
        t_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, t["title"]))
        eq_id = f"'{item_ids[t['eq']]}'" if t.get("eq") and t["eq"] in item_ids else "NULL"
        pc_id = f"'{pc_ids[t['pc']]}'" if t.get("pc") and t["pc"] in pc_ids else "NULL"
        st_id = f"'{item_ids[t['station']]}'" if t.get("station") and t["station"] in item_ids else "NULL"
        
        safe_title = t['title'].replace("'", "''")
        safe_desc = t['desc'].replace("'", "''")

        sql_statements.append(
            f"INSERT INTO maintenance_tickets (id, title, description, status, priority, equipment_id, client_pc_id, machine_id, assigned_to, created_by, created_at) "
            f"VALUES ('{t_id}', '{safe_title}', '{safe_desc}', '{t['status']}', '{t['priority']}', {eq_id}, {pc_id}, {st_id}, '{t['assigned']}', '{t['created_by']}', NOW()) "
            f"ON CONFLICT DO NOTHING;"
        )

        for author, c_content in t.get("comments", []):
            c_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{t_id}:{author}:{c_content}"))
            safe_author = author.replace("'", "''")
            safe_c_content = c_content.replace("'", "''")
            sql_statements.append(
                f"INSERT INTO ticket_comments (id, maintenance_ticket_id, author, content, created_at) "
                f"VALUES ('{c_id}', '{t_id}', '{safe_author}', '{safe_c_content}', NOW()) "
                f"ON CONFLICT DO NOTHING;"
            )

    sql_statements.append("\nCOMMIT;")

    with open(output_path, 'w') as f:
        f.write("\n".join(sql_statements))
    print(f"Generated {output_path}")

if __name__ == "__main__":
    generate_sql('seed_data/inventory_seed.csv', 'seed_data/incremental_seed.sql')
