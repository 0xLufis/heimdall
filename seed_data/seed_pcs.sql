INSERT INTO client_pcs (
    id, hostname, machine_identifier, mac_address, last_online, 
    hardware_config, software_config, predecessors
) VALUES 
(
    '550e8400-e29b-41d4-a716-446655440001', 
    'PROD-LINE-A1', 
    'UUID-9988-7766', 
    '00:1A:2B:3C:4D:01', 
    NOW(), 
    '{"Cpu": "Intel Core i7-12700K", "Ram": "32 GB", "Storage": "1 TB SSD"}'::jsonb, 
    '{"OsVersion": "Windows 10 Pro 22H2", "InstalledPackages": ["Office 365", "VLC", "Chrome"]}'::jsonb,
    '[]'::jsonb
),
(
    '550e8400-e29b-41d4-a716-446655440002', 
    'PROD-LINE-B2', 
    'UUID-1122-3344', 
    '00:1A:2B:3C:4D:02', 
    NOW() - INTERVAL '10 minutes', 
    '{"Cpu": "AMD Ryzen 9 5900X", "Ram": "64 GB", "Storage": "2 TB NVMe"}'::jsonb, 
    '{"OsVersion": "Ubuntu 22.04 LTS", "InstalledPackages": ["Docker", "Nginx", "Python 3.10"]}'::jsonb,
    '[]'::jsonb
),
(
    '550e8400-e29b-41d4-a716-446655440003', 
    'OFFICE-SEC-01', 
    'UUID-5566-7788', 
    '00:1A:2B:3C:4D:03', 
    NOW() - INTERVAL '2 days', 
    '{"Cpu": "Intel i5-11400", "Ram": "16 GB", "Storage": "500 GB HDD"}'::jsonb, 
    '{"OsVersion": "Windows 11 Enterprise", "InstalledPackages": ["SentinelOne", "Chrome"]}'::jsonb,
    '[]'::jsonb
)
ON CONFLICT (mac_address) DO UPDATE SET
    hostname = EXCLUDED.hostname,
    last_online = EXCLUDED.last_online,
    hardware_config = EXCLUDED.hardware_config,
    software_config = EXCLUDED.software_config;
