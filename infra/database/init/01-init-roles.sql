-- 1. Create Roles (Users)
CREATE ROLE dotnet_backend WITH LOGIN PASSWORD 'your_backend_pw';
CREATE ROLE ef_admin WITH LOGIN PASSWORD 'migrate';
CREATE ROLE nuxt_frontend WITH LOGIN PASSWORD 'your_frontend_pw';
CREATE ROLE drizzle_admin WITH LOGIN PASSWORD 'migrate';
-- 2. Create Schemas
CREATE SCHEMA IF NOT EXISTS heimdall_dev_db;
-- 3. Public Schema Permissions
-- Everyone needs USAGE to see the schema
GRANT USAGE
ON SCHEMA public TO dotnet_backend,
nuxt_frontend,
ef_admin,
drizzle_admin;
-- Apps need DML (Data Manipulation) access
GRANT SELECT, INSERT, UPDATE, DELETE 
ON ALL TABLES IN SCHEMA public TO dotnet_backend,
   nuxt_frontend;
GRANT USAGE, SELECT 
ON ALL SEQUENCES IN SCHEMA public TO dotnet_backend,
   nuxt_frontend;
-- Admins need DDL (Data Definition) access
GRANT ALL PRIVILEGES
ON SCHEMA public TO ef_admin,
drizzle_admin;
-- 4. heimdall_dev_db Schema Permissions
-- Only the admin roles get access here
GRANT USAGE, CREATE
ON SCHEMA heimdall_dev_db TO ef_admin,
drizzle_admin;
GRANT ALL PRIVILEGES
ON ALL TABLES IN SCHEMA heimdall_dev_db TO ef_admin,
drizzle_admin;
GRANT ALL PRIVILEGES
ON ALL SEQUENCES IN SCHEMA heimdall_dev_db TO ef_admin,
drizzle_admin;

-- Grant app roles access to auth tables
GRANT USAGE
ON SCHEMA heimdall_dev_db TO dotnet_backend,
nuxt_frontend;
GRANT SELECT, INSERT, UPDATE, DELETE 
ON ALL TABLES IN SCHEMA heimdall_dev_db TO dotnet_backend,
   nuxt_frontend;
GRANT USAGE, SELECT 
ON ALL SEQUENCES IN SCHEMA heimdall_dev_db TO dotnet_backend,
   nuxt_frontend;

-- 5. Set Default Privileges (Crucial for future tables)
-- This ensures that when an admin creates a table, the app roles can actually see it automatically
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE 
ON TABLES TO dotnet_backend,
   nuxt_frontend;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT 
ON SEQUENCES TO dotnet_backend,
   nuxt_frontend;

ALTER DEFAULT PRIVILEGES IN SCHEMA heimdall_dev_db GRANT SELECT, INSERT, UPDATE, DELETE 
ON TABLES TO dotnet_backend,
   nuxt_frontend;
ALTER DEFAULT PRIVILEGES IN SCHEMA heimdall_dev_db GRANT USAGE, SELECT 
ON SEQUENCES TO dotnet_backend,
   nuxt_frontend;
