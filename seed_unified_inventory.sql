-- Heimdall Unified Inventory Seed Data
-- Adheres to the refactored InventoryComponent model

-- 1. CLEANUP (Optional, use with caution)
-- DELETE FROM backend.inventory_components;
-- DELETE FROM backend.client_pc_machine;
-- DELETE FROM backend.client_pcs;
-- DELETE FROM backend.machines;
-- DELETE FROM backend.suppliers;
-- DELETE FROM backend.manufacturers;

-- 2. SEED MANUFACTURERS
INSERT INTO backend.manufacturers (id, name, website, support_contact) VALUES 
('550e8400-e29b-41d4-a716-446655443010', 'KUKA', 'https://www.kuka.com', 'support@kuka.com'),
('550e8400-e29b-41d4-a716-446655443011', 'FANUC', 'https://www.fanuc.com', 'service@fanuc.com'),
('550e8400-e29b-41d4-a716-446655443012', 'Samsung', 'https://www.samsung.com', NULL),
('550e8400-e29b-41d4-a716-446655443013', 'Universal Robots', 'https://www.universal-robots.com', 'help@universal-robots.com'),
('a1a1a1a1-1111-41d4-a716-446655440001', 'RoboCorp', 'https://robocorp.example.com', 'support@robocorp.example.com'),
('a1a1a1a1-3333-41d4-a716-446655440003', 'Cyberdyne', 'https://cyberdyne.example.com', 'contact@cyberdyne.example.com'),
('a1a1a1a1-4444-41d4-a716-446655440004', 'Omni Consumer Products', 'https://ocp.example.com', 'care@ocp.example.com'),
('550e8400-e29b-41d4-a716-446655441001', 'Dell', 'https://www.dell.com', NULL),
('550e8400-e29b-41d4-a716-446655441002', 'HP', 'https://www.hp.com', NULL),
('550e8400-e29b-41d4-a716-446655442001', 'Microsoft', 'https://www.microsoft.com', NULL),
('550e8400-e29b-41d4-a716-446655442002', 'Autodesk', 'https://www.autodesk.com', NULL)
ON CONFLICT (name) DO UPDATE SET website = EXCLUDED.website;

-- 3. SEED SUPPLIERS
INSERT INTO backend.suppliers (id, name, website, contact_person, email) VALUES
('b2b2b2b2-1111-41d4-a716-446655440001', 'Industrial Automata Direct', 'https://iadirect.example.com', 'Jane Doe', 'jane.d@iadirect.example.com'),
('b2b2b2b2-2222-41d4-a716-446655440002', 'FutureTech Solutions', 'https://fts.example.com', 'John Smith', 'j.smith@fts.example.com')
ON CONFLICT (name) DO UPDATE SET contact_person = EXCLUDED.contact_person;

-- 4. SEED MACHINES
INSERT INTO backend.machines (id, custom_identifier, organization_id, pinned_object_handle) VALUES
('m1m1m1m1-1111-41d4-a716-446655440001', 'Assembly Line 1', 'org_1', 'H-AL1'),
('m1m1m1m1-2222-41d4-a716-446655440002', 'Welding Station 5', 'org_1', 'H-WS5')
ON CONFLICT (custom_identifier) DO NOTHING;

-- 5. SEED CLIENT PCS
INSERT INTO backend.client_pcs (id, hostname, machine_identifier, mac_address, last_online, organization_id, custom_data_points, predecessors) VALUES 
(
    '550e8400-e29b-41d4-a716-446655440001', 
    'PROD-LINE-A1', 
    'UUID-9988-7766', 
    '00:1A:2B:3C:4D:01', 
    NOW(), 
    'org_1',
    '{"Environment": "Production", "Zone": "North"}'::jsonb,
    '[]'::jsonb
),
(
    '550e8400-e29b-41d4-a716-446655440002', 
    'PROD-LINE-B2', 
    'UUID-1122-3344', 
    '00:1A:2B:3C:4D:02', 
    NOW() - INTERVAL '10 minutes', 
    'org_1',
    '{"Environment": "Production", "Zone": "South"}'::jsonb,
    '[]'::jsonb
),
(
    '550e8400-e29b-41d4-a716-446655440003', 
    'OFFICE-SEC-01', 
    'UUID-5566-7788', 
    '00:1A:2B:3C:4D:03', 
    NOW() - INTERVAL '2 days', 
    'org_1',
    '{"Environment": "Office", "Zone": "Admin"}'::jsonb,
    '[]'::jsonb
)
ON CONFLICT (mac_address) DO NOTHING;

-- 6. MANY-TO-MANY RELATIONSHIPS (ClientPcMachine)
INSERT INTO backend.client_pc_machine (client_pcs_id, machines_id) VALUES
('550e8400-e29b-41d4-a716-446655440001', 'm1m1m1m1-1111-41d4-a716-446655440001'), -- A1 controls AL1
('550e8400-e29b-41d4-a716-446655440002', 'm1m1m1m1-1111-41d4-a716-446655440001'), -- B2 also controls AL1
('550e8400-e29b-41d4-a716-446655440002', 'm1m1m1m1-2222-41d4-a716-446655440002')  -- B2 also controls WS5
ON CONFLICT DO NOTHING;

-- 7. SEED INVENTORY COMPONENTS

-- --- COMPONENTS FOR PROD-LINE-A1 (PC) ---
-- Top-level Nodes
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, client_pc_id) VALUES
('c1c1c1c1-0001-41d4-a716-000000000001', 'Hardware', 'IT', '{"type": "hardware", "owner": "in-house"}'::jsonb, '550e8400-e29b-41d4-a716-446655440001'),
('c1c1c1c1-0001-41d4-a716-000000000002', 'Software', 'IT', '{"type": "software", "owner": "in-house"}'::jsonb, '550e8400-e29b-41d4-a716-446655440001'),
('c1c1c1c1-0001-41d4-a716-000000000003', 'Peripherals', 'Automation', '{"type": "peripherals", "owner": "mixed"}'::jsonb, '550e8400-e29b-41d4-a716-446655440001');

-- PC 1 Hardware Data
INSERT INTO backend.inventory_components (id, name, technology, data, parent_id, manufacturer_id) VALUES
('c1c1c1c1-1001-41d4-a716-000000000001', 'Mainboard/CPU', 'IT', '{"Cpu": "Intel Core i7-12700K", "Ram": "32 GB", "Storage": "1 TB SSD"}'::jsonb, 'c1c1c1c1-0001-41d4-a716-000000000001', '550e8400-e29b-41d4-a716-446655441001');

-- PC 1 Software Data
INSERT INTO backend.inventory_components (id, name, technology, data, parent_id, manufacturer_id) VALUES
('c1c1c1c1-2001-41d4-a716-000000000001', 'Operating System', 'IT', '{"OsVersion": "Windows 10 Pro 22H2"}'::jsonb, 'c1c1c1c1-0001-41d4-a716-000000000002', '550e8400-e29b-41d4-a716-446655442001'),
('c1c1c1c1-2001-41d4-a716-000000000002', 'Installed Packages', 'IT', '{"Packages": ["Office 365", "VLC", "Chrome"]}'::jsonb, 'c1c1c1c1-0001-41d4-a716-000000000002', NULL);

-- PC 1 Peripherals
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, data, parent_id, manufacturer_id) VALUES
('c1c1c1c1-3001-41d4-a716-000000000001', 'Vision Sensor A', 'Vision', '{"type": "sensor"}'::jsonb, '{"Resolution": "4K", "Interface": "EtherNet/IP"}'::jsonb, 'c1c1c1c1-0001-41d4-a716-000000000003', 'a1a1a1a1-3333-41d4-a716-446655440003');


-- --- COMPONENTS FOR ASSEMBLY LINE 1 (Machine) ---
-- Top-level Hardware Node
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, machine_id) VALUES
('m1c1c1c1-0001-41d4-a716-000000000001', 'Machine Hardware', 'Maintenance', '{"type": "hardware", "owner": "outsourced"}'::jsonb, 'm1m1m1m1-1111-41d4-a716-446655440001');

-- Machine Components (Recursive)
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, data, parent_id, manufacturer_id, supplier_id) VALUES
('m1c1c1c1-1001-41d4-a716-000000000001', 'Assembly Robot Arm', 'Automation', '{"type": "robot"}'::jsonb, '{"model": "RC-ARM-X5", "payload": "5kg"}'::jsonb, 'm1c1c1c1-0001-41d4-a716-000000000001', 'a1a1a1a1-1111-41d4-a716-446655440001', 'b2b2b2b2-1111-41d4-a716-446655440001'),
('m1c1c1c1-1001-41d4-a716-000000000002', 'Conveyor Belt Section', 'Mechanical', '{"type": "conveyor"}'::jsonb, '{"length": "10m"}'::jsonb, 'm1c1c1c1-0001-41d4-a716-000000000001', 'a1a1a1a1-4444-41d4-a716-446655440004', 'b2b2b2b2-1111-41d4-a716-446655440001');

-- Sub-component
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, data, parent_id, manufacturer_id) VALUES
('m1c1c1c1-2001-41d4-a716-000000000001', 'Pneumatic Gripper', 'Automation', '{"type": "effector"}'::jsonb, '{"pressure": "6 bar"}'::jsonb, 'm1c1c1c1-1001-41d4-a716-000000000001', 'a1a1a1a1-1111-41d4-a716-446655440001');


-- --- COMPONENTS FOR WELDING STATION 5 (Machine) ---
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, machine_id) VALUES
('m2c1c1c1-0001-41d4-a716-000000000001', 'Welding Hardware', 'Maintenance', '{"type": "hardware", "owner": "in-house"}'::jsonb, 'm1m1m1m1-2222-41d4-a716-446655440002');

INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, data, parent_id, manufacturer_id) VALUES
('m2c1c1c1-1001-41d4-a716-000000000001', 'Industrial Robot', 'Automation', '{"type": "robot"}'::jsonb, '{"Category": "Industrial Robot", "PayloadCapacityKg": 7, "ReachMm": 800, "InterfaceType": "EtherCAT"}'::jsonb, 'm2c1c1c1-0001-41d4-a716-000000000001', '550e8400-e29b-41d4-a716-446655443010');

-- 8. LATERAL LINKS EXAMPLE
-- Link "Vision Sensor A" (PC Peripheral) to "Assembly Robot Arm" (Machine component)
UPDATE backend.inventory_components 
SET lateral_link_id = 'm1c1c1c1-1001-41d4-a716-000000000001' 
WHERE id = 'c1c1c1c1-3001-41d4-a716-000000000001';
