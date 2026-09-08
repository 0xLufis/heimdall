#!/usr/bin/env bash
set -e

DOTNET_BACKEND_PW="${DOTNET_BACKEND_PASSWORD:-your_backend_pw}"
EF_ADMIN_PW="${EF_ADMIN_PASSWORD:-migrate}"
NUXT_FRONTEND_PW="${NUXT_FRONTEND_PASSWORD:-your_frontend_pw}"
DRIZZLE_ADMIN_PW="${DRIZZLE_ADMIN_PASSWORD:-migrate}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- 1. Create Roles (Users) with parameterized passwords
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'dotnet_backend') THEN
            EXECUTE format('CREATE ROLE dotnet_backend WITH LOGIN PASSWORD %L', '$DOTNET_BACKEND_PW');
        ELSE
            EXECUTE format('ALTER ROLE dotnet_backend WITH PASSWORD %L', '$DOTNET_BACKEND_PW');
        END IF;

        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'ef_admin') THEN
            EXECUTE format('CREATE ROLE ef_admin WITH LOGIN PASSWORD %L', '$EF_ADMIN_PW');
        ELSE
            EXECUTE format('ALTER ROLE ef_admin WITH PASSWORD %L', '$EF_ADMIN_PW');
        END IF;

        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'nuxt_frontend') THEN
            EXECUTE format('CREATE ROLE nuxt_frontend WITH LOGIN PASSWORD %L', '$NUXT_FRONTEND_PW');
        ELSE
            EXECUTE format('ALTER ROLE nuxt_frontend WITH PASSWORD %L', '$NUXT_FRONTEND_PW');
        END IF;

        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'drizzle_admin') THEN
            EXECUTE format('CREATE ROLE drizzle_admin WITH LOGIN PASSWORD %L', '$DRIZZLE_ADMIN_PW');
        ELSE
            EXECUTE format('ALTER ROLE drizzle_admin WITH PASSWORD %L', '$DRIZZLE_ADMIN_PW');
        END IF;
    END
    \$\$;

    -- 2. Create Schemas
    CREATE SCHEMA IF NOT EXISTS backend;
    CREATE SCHEMA IF NOT EXISTS auth;

    -- 3. Public Schema Permissions
    GRANT USAGE ON SCHEMA public TO dotnet_backend, nuxt_frontend, ef_admin, drizzle_admin;
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO dotnet_backend, nuxt_frontend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO dotnet_backend, nuxt_frontend;
    GRANT ALL PRIVILEGES ON SCHEMA public TO ef_admin, drizzle_admin;

    -- 4. backend Schema Permissions
    GRANT ALL PRIVILEGES ON DATABASE heimdall_dev_db TO ef_admin, drizzle_admin;
    GRANT USAGE, CREATE ON SCHEMA backend TO ef_admin, drizzle_admin;
    GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA backend TO ef_admin, drizzle_admin;
    GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA backend TO ef_admin, drizzle_admin;

    GRANT USAGE, CREATE ON SCHEMA backend TO dotnet_backend, nuxt_frontend;
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA backend TO dotnet_backend, nuxt_frontend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA backend TO dotnet_backend, nuxt_frontend;

    -- 4b. Auth Schema Permissions
    GRANT ALL PRIVILEGES ON SCHEMA auth TO ef_admin, drizzle_admin;
    GRANT USAGE, CREATE ON SCHEMA auth TO dotnet_backend, nuxt_frontend;
    GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA auth TO ef_admin, drizzle_admin;
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA auth TO dotnet_backend, nuxt_frontend;
    GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA auth TO ef_admin, drizzle_admin;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA auth TO dotnet_backend, nuxt_frontend;

    -- 5. Default Privileges
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dotnet_backend, nuxt_frontend;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO dotnet_backend, nuxt_frontend;

    ALTER DEFAULT PRIVILEGES IN SCHEMA backend GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dotnet_backend, nuxt_frontend;
    ALTER DEFAULT PRIVILEGES IN SCHEMA backend GRANT ALL PRIVILEGES ON TABLES TO ef_admin, drizzle_admin;
    ALTER DEFAULT PRIVILEGES IN SCHEMA backend GRANT USAGE, SELECT ON SEQUENCES TO dotnet_backend, nuxt_frontend, ef_admin, drizzle_admin;

    ALTER DEFAULT PRIVILEGES IN SCHEMA auth GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dotnet_backend, nuxt_frontend;
    ALTER DEFAULT PRIVILEGES IN SCHEMA auth GRANT USAGE, SELECT ON SEQUENCES TO dotnet_backend, nuxt_frontend;
EOSQL
chmod +x /home/lufis/Projects/Heimdall/heimdall/infra/database/init/01-init-roles.sh
