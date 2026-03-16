UPDATE hardware_components 
SET name = REPLACE(name, 'Cobot', 'Industrial Robot')
WHERE name LIKE '%Cobot%';

UPDATE hardware_components
SET technical_specs = jsonb_set(technical_specs, '{Category}', '"Industrial Robot"')
WHERE technical_specs->>'Category' = 'Cobot';
