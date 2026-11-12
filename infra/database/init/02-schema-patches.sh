#!/usr/bin/env bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE SCHEMA IF NOT EXISTS backend;

    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS status character varying(64) DEFAULT 'active';
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS decommission_reason text;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS decommission_date timestamp with time zone;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS decommission_disposition character varying(64);

    -- Suppliers Enhancements
    ALTER TABLE IF EXISTS backend.suppliers ADD COLUMN IF NOT EXISTS phone_number character varying(64);
    ALTER TABLE IF EXISTS backend.suppliers ADD COLUMN IF NOT EXISTS lead_time_days integer DEFAULT 7;
    ALTER TABLE IF EXISTS backend.suppliers ADD COLUMN IF NOT EXISTS address text;
    ALTER TABLE IF EXISTS backend.suppliers ADD COLUMN IF NOT EXISTS notes text;
    ALTER TABLE IF EXISTS backend.suppliers ADD COLUMN IF NOT EXISTS created_at timestamp with time zone DEFAULT now();

    -- NOTE: backend.known_tags and backend.inventory_item_tags are owned by the EF Core migration
    -- 20261011224836_AddMissingTables. They used to be created here as well, which made
    -- `dotnet ef database update` fail on a fresh database with 42P07 "relation already exists".

    -- NOTE: Machine hierarchy (stations.*_group_id, parent_machine_id, spoc/owner, technologies),
    -- delegation multi-group junctions and generic_templates are owned by the EF Core migration
    -- 20261109210719_MachineHierarchyDelegationAndTemplates. Do not duplicate that DDL here: this
    -- script runs on an empty database (docker-entrypoint-initdb.d) before any EF migration, so
    -- FKs to EF-managed tables cannot resolve and duplicated CREATE TABLEs break `ef database update`.

EOSQL
