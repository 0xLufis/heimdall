UPDATE backend.inventory_components 
SET name = REPLACE(name, 'Cobot', 'Industrial Robot')
WHERE name LIKE '%Cobot%';

UPDATE backend.inventory_components
SET top_level_flags = jsonb_set(top_level_flags, '{type}', '"robot"')
WHERE top_level_flags->>'type' = 'cobot';
