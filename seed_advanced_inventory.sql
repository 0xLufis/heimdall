-- Manufacturers
INSERT INTO manufacturers (id, name, website) VALUES 
('550e8400-e29b-41d4-a716-446655443001', 'Keyence', 'https://www.keyence.com'),
('550e8400-e29b-41d4-a716-446655443002', 'Deprag', 'https://www.deprag.com'),
('550e8400-e29b-41d4-a716-446655443003', 'Sick', 'https://www.sick.com');

-- Suppliers
INSERT INTO suppliers (id, name, email) VALUES 
('550e8400-e29b-41d4-a716-446655444001', 'Automation Direct', 'sales@automationdirect.com'),
('550e8400-e29b-41d4-a716-446655444002', 'Local Industrial Supply', 'info@localsupply.hu');

-- Hardware with Technical Specs
INSERT INTO hardware_components (id, manufacturer_id, name, model_number, technical_specs, cost_in_huf, supplier_id) VALUES 
(
    '550e8400-e29b-41d4-a716-446655445001', 
    '550e8400-e29b-41d4-a716-446655443001', 
    'Vision Sensor IV3', 
    'IV3-G500MA', 
    '{"Category": "Vision Sensor", "Resolution": "640x480", "InterfaceType": "Ethernet/IP"}'::jsonb, 
    450000,
    '550e8400-e29b-41d4-a716-446655444001'
),
(
    '550e8400-e29b-41d4-a716-446655445002', 
    '550e8400-e29b-41d4-a716-446655443002', 
    'Screwdriver ESFM', 
    'ESFM-12', 
    '{"Category": "Screwdriver", "TorqueMax": 12.5, "MaxSpeed": 1200}'::jsonb, 
    850000,
    '550e8400-e29b-41d4-a716-446655444002'
),
(
    '550e8400-e29b-41d4-a716-446655445003', 
    '550e8400-e29b-41d4-a716-446655443003', 
    'Inductive Sensor', 
    'IME12-04BPSZC0K', 
    '{"Category": "Sensor", "SensingDistance": "4mm", "OutputType": "PNP"}'::jsonb, 
    15000,
    '550e8400-e29b-41d4-a716-446655444001'
);
