-- Insert Collaborative Robot Manufacturers
INSERT INTO manufacturers (id, name, website) VALUES 
('550e8400-e29b-41d4-a716-446655443010', 'KUKA', 'https://www.kuka.com') ON CONFLICT (name) DO NOTHING;
INSERT INTO manufacturers (id, name, website) VALUES 
('550e8400-e29b-41d4-a716-446655443011', 'FANUC', 'https://www.fanuc.com') ON CONFLICT (name) DO NOTHING;
INSERT INTO manufacturers (id, name, website) VALUES 
('550e8400-e29b-41d4-a716-446655443012', 'Samsung', 'https://www.samsung.com') ON CONFLICT (name) DO NOTHING;
INSERT INTO manufacturers (id, name, website) VALUES 
('550e8400-e29b-41d4-a716-446655443013', 'Universal Robots', 'https://www.universal-robots.com') ON CONFLICT (name) DO NOTHING;

-- Seed Collaborative Robots
INSERT INTO hardware_components (id, manufacturer_id, name, model_number, technical_specs, cost_in_huf) VALUES 
(
    '550e8400-e29b-41d4-a716-446655445010', 
    (SELECT id FROM manufacturers WHERE name = 'KUKA'), 
    'LBR iiwa Cobot', 
    'LBR iiwa 7 R800', 
    '{"Category": "Cobot", "PayloadCapacityKg": 7, "ReachMm": 800, "InterfaceType": "EtherCAT"}'::jsonb, 
    25000000
),
(
    '550e8400-e29b-41d4-a716-446655445011', 
    (SELECT id FROM manufacturers WHERE name = 'FANUC'), 
    'CRX Collaborative Robot', 
    'CRX-10iA/L', 
    '{"Category": "Cobot", "PayloadCapacityKg": 10, "ReachMm": 1418, "InterfaceType": "Profinet"}'::jsonb, 
    18000000
),
(
    '550e8400-e29b-41d4-a716-446655445012', 
    (SELECT id FROM manufacturers WHERE name = 'Samsung'), 
    'FARA Robot Arm', 
    'FAR-C-05', 
    '{"Category": "Cobot", "PayloadCapacityKg": 5, "ReachMm": 900, "InterfaceType": "TCP/IP"}'::jsonb, 
    14500000
),
(
    '550e8400-e29b-41d4-a716-446655445013', 
    (SELECT id FROM manufacturers WHERE name = 'Universal Robots'), 
    'UR10e', 
    'UR10e', 
    '{"Category": "Cobot", "PayloadCapacityKg": 12.5, "ReachMm": 1300, "InterfaceType": "Profinet"}'::jsonb, 
    16000000
);
