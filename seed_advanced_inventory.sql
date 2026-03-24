-- Advanced Seed Data for Heimdall Inventory
-- This script generates a more complex and hierarchical set of hardware and software components.

-- Clear existing data to avoid conflicts
-- DELETE FROM software_components;
-- DELETE FROM hardware_components;
-- DELETE FROM manufacturers;
-- DELETE FROM suppliers;

-- Seed Manufacturers
INSERT INTO manufacturers (id, name, website, support_contact) VALUES
('a1a1a1a1-1111-41d4-a716-446655440001', 'RoboCorp', 'https://robocorp.example.com', 'support@robocorp.example.com'),
('a1a1a1a1-2222-41d4-a716-446655440002', 'Synthoid Systems', 'https://synthoid.example.com', 'help@synthoid.example.com'),
('a1a1a1a1-3333-41d4-a716-446655440003', 'Cyberdyne', 'https://cyberdyne.example.com', 'contact@cyberdyne.example.com'),
('a1a1a1a1-4444-41d4-a716-446655440004', 'Omni Consumer Products', 'https://ocp.example.com', 'care@ocp.example.com');

-- Seed Suppliers
INSERT INTO suppliers (id, name, website, contact_person, email) VALUES
('b2b2b2b2-1111-41d4-a716-446655440001', 'Industrial Automata Direct', 'https://iadirect.example.com', 'Jane Doe', 'jane.d@iadirect.example.com'),
('b2b2b2b2-2222-41d4-a716-446655440002', 'FutureTech Solutions', 'https://fts.example.com', 'John Smith', 'j.smith@fts.example.com');

-- Seed Hardware Components (with Hierarchy)
-- Top Level: Robots
INSERT INTO hardware_components (id, manufacturer_id, name, model_number, description, serial_number, cost_in_huf, parent_id) VALUES
('c3c3c3c3-0001-41d4-a716-446655440001', 'a1a1a1a1-1111-41d4-a716-446655440001', 'Assembly Robot Arm', 'RC-ARM-X5', '5-axis robotic arm for assembly lines.', 'SN-RC-ARM-001', 12500000, NULL),
('c3c3c3c3-0002-41d4-a716-446655440002', 'a1a1a1a1-2222-41d4-a716-446655440002', 'Welding Bot', 'SS-WELD-9000', 'Automated spot welding robot.', 'SN-SS-WELD-001', 25000000, NULL);

-- Sub-components for Assembly Robot Arm
INSERT INTO hardware_components (id, manufacturer_id, name, model_number, description, serial_number, cost_in_huf, parent_id) VALUES
('c3c3c3c3-1001-41d4-a716-446655440001', 'a1a1a1a1-1111-41d4-a716-446655440001', 'Servo Motor - Axis 1', 'RC-SERVO-A1', 'High-torque servo for base rotation.', 'SN-RC-SERVO-001', 750000, 'c3c3c3c3-0001-41d4-a716-446655440001'),
('c3c3c3c3-1002-41d4-a716-446655440002', 'a1a1a1a1-1111-41d4-a716-446655440001', 'Pneumatic Gripper', 'RC-GRIP-P2', '2-finger pneumatic gripper attachment.', 'SN-RC-GRIP-001', 320000, 'c3c3c3c3-0001-41d4-a716-446655440001'),
('c3c3c3c3-1003-41d4-a716-446655440003', 'a1a1a1a1-3333-41d4-a716-446655440003', 'Vision Sensor', 'CY-EYE-4K', '4K resolution vision system for part identification.', 'SN-CY-EYE-001', 1200000, 'c3c3c3c3-1002-41d4-a716-446655440002'); -- Sub-component of the gripper

-- Sub-components for Welding Bot
INSERT INTO hardware_components (id, manufacturer_id, name, model_number, description, serial_number, cost_in_huf, parent_id) VALUES
('c3c3c3c3-2001-41d4-a716-446655440001', 'a1a1a1a1-2222-41d4-a716-446655440002', 'High-Current Transformer', 'SS-TR-20KVA', '20KVA transformer for welding.', 'SN-SS-TR-001', 3500000, 'c3c3c3c3-0002-41d4-a716-446655440002'),
('c3c3c3c3-2002-41d4-a716-446655440002', 'a1a1a1a1-4444-41d4-a716-446655440004', 'Cooling System', 'OCP-COOL-3', 'Liquid cooling system for transformer.', 'SN-OCP-COOL-001', 1500000, 'c3c3c3c3-0002-41d4-a716-446655440002');

-- Seed Software Components (with Hierarchy)
-- Top Level: Control Systems
INSERT INTO software_components (id, manufacturer_id, name, version, description, serial_number, cost_in_huf, parent_id) VALUES
('d4d4d4d4-0001-41d4-a716-446655440001', 'a1a1a1a1-1111-41d4-a716-446655440001', 'RoboOS', '5.2.1', 'Operating system for RoboCorp hardware.', 'LIC-RC-OS-001', 5000000, NULL),
('d4d4d4d4-0002-41d4-a716-446655440002', 'a1a1a1a1-3333-41d4-a716-446655440003', 'Skynet', '2.0', 'Global manufacturing network AI.', 'LIC-SKY-001', 999999999, NULL);

-- Sub-components for RoboOS
INSERT INTO software_components (id, manufacturer_id, name, version, description, serial_number, cost_in_huf, parent_id) VALUES
('d4d4d4d4-1001-41d4-a716-446655440001', 'a1a1a1a1-1111-41d4-a716-446655440001', 'Pathing Module', '3.5', 'Motion planning and collision avoidance.', 'MOD-PATH-001', 1200000, 'd4d4d4d4-0001-41d4-a716-446655440001'),
('d4d4d4d4-1002-41d4-a716-446655440002', 'a1a1a1a1-1111-41d4-a716-446655440001', 'Vision AI Library', '2.1', 'Library for processing data from vision sensors.', 'MOD-VISION-001', 850000, 'd4d4d4d4-0001-41d4-a716-446655440001');

-- Sub-components for Skynet
INSERT INTO software_components (id, manufacturer_id, name, version, description, serial_number, cost_in_huf, parent_id) VALUES
('d4d4d4d4-2001-41d4-a716-446655440001', 'a1a1a1a1-3333-41d4-a716-446655440003', 'Hunter-Killer AI', '4.5a', 'Drone control module.', 'MOD-HK-001', 50000000, 'd4d4d4d4-0002-41d4-a716-446655440002');

-- More standalone components
INSERT INTO hardware_components (id, manufacturer_id, name, model_number, description, serial_number, cost_in_huf, parent_id) VALUES
('c3c3c3c3-3001-41d4-a716-446655440001', 'a1a1a1a1-4444-41d4-a716-446655440004', 'Conveyor Belt Section', 'OCP-CB-10M', '10-meter modular conveyor belt.', 'SN-OCP-CB-001', 800000, NULL),
('c3c3c3c3-3002-41d4-a716-446655440002', 'a1a1a1a1-4444-41d4-a716-446655440004', 'Emergency Stop Button', 'OCP-ESTOP-BIGRED', 'A big red button.', 'SN-OCP-ESTOP-001', 25000, 'c3c3c3c3-3001-41d4-a716-446655440001');

INSERT INTO software_components (id, manufacturer_id, name, version, description, serial_number, cost_in_huf, parent_id) VALUES
('d4d4d4d4-3001-41d4-a716-446655440001', 'a1a1a1a1-2222-41d4-a716-446655440002', 'Factory Floor HMI', '2.3.1', 'Human-Machine Interface for line operators.', 'LIC-SS-HMI-001', 450000, NULL);
