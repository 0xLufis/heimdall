INSERT INTO backend.manufacturers (id, name) VALUES 
('550e8400-e29b-41d4-a716-446655441001', 'Dell'),
('550e8400-e29b-41d4-a716-446655441002', 'HP')
ON CONFLICT (name) DO NOTHING;

INSERT INTO backend.inventory_components (id, manufacturer_id, name, technology, top_level_flags, data)
VALUES 
('550e8400-e29b-41d4-a716-446655441001', '550e8400-e29b-41d4-a716-446655441001', 'Optiplex 7000 Micro', 'IT', '{"type": "hardware"}'::jsonb, '{"specs": "Core i7, 16GB RAM, 512GB SSD"}'::jsonb),
('550e8400-e29b-41d4-a716-446655441002', '550e8400-e29b-41d4-a716-446655441002', 'ZBook Power G10', 'IT', '{"type": "hardware"}'::jsonb, '{"specs": "Ryzen 7, 32GB RAM, 1TB SSD"}'::jsonb);
