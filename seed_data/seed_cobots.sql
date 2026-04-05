-- Insert Collaborative Robot Manufacturers
INSERT INTO backend.manufacturers (id, name, website) VALUES 
('550e8400-e29b-41d4-a716-446655443010', 'KUKA', 'https://www.kuka.com') ON CONFLICT (name) DO NOTHING;
INSERT INTO backend.manufacturers (id, name, website) VALUES 
('550e8400-e29b-41d4-a716-446655443011', 'FANUC', 'https://www.fanuc.com') ON CONFLICT (name) DO NOTHING;

-- Seed Collaborative Robots as InventoryComponents
INSERT INTO backend.inventory_components (id, manufacturer_id, name, technology, top_level_flags, data) VALUES 
(
    '550e8400-e29b-41d4-a716-446655445010', 
    '550e8400-e29b-41d4-a716-446655443010', 
    'LBR iiwa Industrial Robot', 
    'Automation',
    '{"type": "robot", "owner": "in-house"}'::jsonb, 
    '{"PayloadCapacityKg": 7, "ReachMm": 800, "InterfaceType": "EtherCAT"}'::jsonb
),
(
    '550e8400-e29b-41d4-a716-446655445011', 
    '550e8400-e29b-41d4-a716-446655443011', 
    'CRX Industrial Robot', 
    'Automation',
    '{"type": "robot", "owner": "outsourced"}'::jsonb, 
    '{"PayloadCapacityKg": 10, "ReachMm": 1418, "InterfaceType": "Profinet"}'::jsonb
);
