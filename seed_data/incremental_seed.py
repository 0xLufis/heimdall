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
                sql_statements.append(f"INSERT INTO \"StationControllers\" (controllers_id, controlled_machines_id) VALUES ('{pc_id}', '{station_id}') ON CONFLICT DO NOTHING;")

    sql_statements.append("\nCOMMIT;")

    with open(output_path, 'w') as f:
        f.write("\n".join(sql_statements))
    print(f"Generated {output_path}")

if __name__ == "__main__":
    generate_sql('seed_data/inventory_seed.csv', 'seed_data/incremental_seed.sql')
