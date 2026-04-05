INSERT INTO backend.client_pcs (
    id, hostname, machine_identifier, mac_address, last_online, organization_id,
    custom_data_points, predecessors
) VALUES 
(
    '550e8400-e29b-41d4-a716-446655440001', 
    'PROD-LINE-A1', 
    'UUID-9988-7766', 
    '00:1A:2B:3C:4D:01', 
    NOW(), 
    'org_1',
    '{"Zone": "A"}'::jsonb,
    '[]'::jsonb
),
(
    '550e8400-e29b-41d4-a716-446655440002', 
    'PROD-LINE-B2', 
    'UUID-1122-3344', 
    '00:1A:2B:3C:4D:02', 
    NOW() - INTERVAL '10 minutes', 
    'org_1',
    '{"Zone": "B"}'::jsonb,
    '[]'::jsonb
)
ON CONFLICT (mac_address) DO NOTHING;
