-- Advanced Seed Data for Heimdall Unified Inventory

-- Seed Manufacturers
INSERT INTO backend.manufacturers (id, name, website, support_contact) VALUES
('a1a1a1a1-1111-41d4-a716-446655440001', 'RoboCorp', 'https://robocorp.example.com', 'support@robocorp.example.com'),
('a1a1a1a1-3333-41d4-a716-446655440003', 'Cyberdyne', 'https://cyberdyne.example.com', 'contact@cyberdyne.example.com')
ON CONFLICT (name) DO NOTHING;

-- Seed Inventory Components (with Hierarchy)
-- Top Level
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, data, manufacturer_id) VALUES
('c3c3c3c3-0001-41d4-a716-446655440001', 'Assembly Robot Arm', 'Automation', '{"type": "robot", "owner": "outsourced"}'::jsonb, '{"model": "RC-ARM-X5"}'::jsonb, 'a1a1a1a1-1111-41d4-a716-446655440001');

-- Sub-component
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, data, parent_id, manufacturer_id) VALUES
('c3c3c3c3-1003-41d4-a716-446655440003', 'Vision Sensor', 'Vision', '{"type": "sensor"}'::jsonb, '{"resolution": "4K"}'::jsonb, 'c3c3c3c3-0001-41d4-a716-446655440001', 'a1a1a1a1-3333-41d4-a716-446655440003');

-- Control Systems
INSERT INTO backend.inventory_components (id, name, technology, top_level_flags, data, manufacturer_id) VALUES
('d4d4d4d4-0001-41d4-a716-446655440001', 'RoboOS', 'IT', '{"type": "software", "owner": "in-house"}'::jsonb, '{"version": "5.2.1"}'::jsonb, 'a1a1a1a1-1111-41d4-a716-446655440001');
