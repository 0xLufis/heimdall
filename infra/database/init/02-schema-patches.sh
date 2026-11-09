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

    -- Relational Tags Indexing Tables (avoiding slow JSONB scans)
    CREATE TABLE IF NOT EXISTS backend.known_tags (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        key character varying(128) NOT NULL,
        value character varying(255) NOT NULL,
        category character varying(64) DEFAULT 'custom',
        usage_count integer DEFAULT 0,
        created_at timestamp with time zone DEFAULT now(),
        CONSTRAINT uq_known_tag UNIQUE (key, value)
    );

    CREATE TABLE IF NOT EXISTS backend.inventory_item_tags (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        item_id uuid NOT NULL,
        tag_id uuid NOT NULL,
        tag_key character varying(128) NOT NULL,
        tag_value character varying(255) NOT NULL,
        created_at timestamp with time zone DEFAULT now()
    );

    CREATE INDEX IF NOT EXISTS idx_item_tags_key_val ON backend.inventory_item_tags (tag_key, tag_value);
    CREATE INDEX IF NOT EXISTS idx_item_tags_item_id ON backend.inventory_item_tags (item_id);

    -- Machine Hierarchy, SPOC, Owner & Sub-Machine Enhancements
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS spoc character varying(255);
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS spoc_user_id character varying(255);
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS owner character varying(255);
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS owner_user_id character varying(255);
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS sub_line_group_id uuid REFERENCES backend.machine_groups(id) ON DELETE SET NULL;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS line_group_id uuid REFERENCES backend.machine_groups(id) ON DELETE SET NULL;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS project_group_id uuid REFERENCES backend.machine_groups(id) ON DELETE SET NULL;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS business_unit_group_id uuid REFERENCES backend.machine_groups(id) ON DELETE SET NULL;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS parent_machine_id uuid REFERENCES backend.stations(id) ON DELETE CASCADE;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS sub_machine_index integer;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS cell_type character varying(64);

    CREATE INDEX IF NOT EXISTS idx_stations_parent_machine_id ON backend.stations (parent_machine_id);
    CREATE INDEX IF NOT EXISTS idx_stations_sub_line_group_id ON backend.stations (sub_line_group_id);
    CREATE INDEX IF NOT EXISTS idx_stations_line_group_id ON backend.stations (line_group_id);
    CREATE INDEX IF NOT EXISTS idx_stations_project_group_id ON backend.stations (project_group_id);
    CREATE INDEX IF NOT EXISTS idx_stations_bu_group_id ON backend.stations (business_unit_group_id);

    -- Decoupled Delegation Recipes & Multi-Group Junctions
    ALTER TABLE IF EXISTS backend.delegation_recipes ADD COLUMN IF NOT EXISTS technology character varying(128);
    ALTER TABLE IF EXISTS backend.delegation_recipes ADD COLUMN IF NOT EXISTS is_technology_primary boolean DEFAULT true;
    ALTER TABLE IF EXISTS backend.delegation_recipes ADD COLUMN IF NOT EXISTS is_complete_machine_dedication boolean DEFAULT false;
    ALTER TABLE IF EXISTS backend.delegation_recipes ADD COLUMN IF NOT EXISTS maintenance_group_ids_json text DEFAULT '[]';
    ALTER TABLE IF EXISTS backend.delegation_recipes ADD COLUMN IF NOT EXISTS machine_group_ids_json text DEFAULT '[]';

    CREATE TABLE IF NOT EXISTS backend.delegation_maintenance_groups (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        delegation_recipe_id uuid NOT NULL REFERENCES backend.delegation_recipes(id) ON DELETE CASCADE,
        maintenance_group_id uuid NOT NULL REFERENCES backend.personnel_groups(id) ON DELETE CASCADE,
        created_at timestamp with time zone DEFAULT now()
    );
    CREATE INDEX IF NOT EXISTS idx_dlg_maint_recipe_id ON backend.delegation_maintenance_groups (delegation_recipe_id);
    CREATE INDEX IF NOT EXISTS idx_dlg_maint_group_id ON backend.delegation_maintenance_groups (maintenance_group_id);

    CREATE TABLE IF NOT EXISTS backend.delegation_machine_groups (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        delegation_recipe_id uuid NOT NULL REFERENCES backend.delegation_recipes(id) ON DELETE CASCADE,
        machine_group_id uuid NOT NULL REFERENCES backend.machine_groups(id) ON DELETE CASCADE,
        created_at timestamp with time zone DEFAULT now()
    );
    CREATE INDEX IF NOT EXISTS idx_dlg_mach_recipe_id ON backend.delegation_machine_groups (delegation_recipe_id);
    CREATE INDEX IF NOT EXISTS idx_dlg_mach_group_id ON backend.delegation_machine_groups (machine_group_id);

    -- Universal Generic Templates & Recipes
    CREATE TABLE IF NOT EXISTS backend.generic_templates (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        template_type character varying(64) NOT NULL,
        code character varying(100) NOT NULL UNIQUE,
        name character varying(255) NOT NULL,
        description text,
        category character varying(100) NOT NULL DEFAULT 'General',
        icon character varying(64) DEFAULT 'Layers',
        version character varying(32) NOT NULL DEFAULT '1.0.0',
        extends_template_id uuid REFERENCES backend.generic_templates(id) ON DELETE SET NULL,
        tags text[] NOT NULL DEFAULT '{}',
        schema_json text NOT NULL DEFAULT '[]',
        default_values_json text NOT NULL DEFAULT '{}',
        ui_config_json text,
        is_active boolean NOT NULL DEFAULT true,
        is_builtin boolean NOT NULL DEFAULT false,
        organization_id character varying(128),
        created_at timestamp with time zone DEFAULT now(),
        updated_at timestamp with time zone DEFAULT now()
    );
    CREATE INDEX IF NOT EXISTS idx_generic_templates_type ON backend.generic_templates (template_type);
    CREATE INDEX IF NOT EXISTS idx_generic_templates_category ON backend.generic_templates (category);
    CREATE INDEX IF NOT EXISTS idx_generic_templates_extends ON backend.generic_templates (extends_template_id);
    CREATE INDEX IF NOT EXISTS idx_generic_templates_org ON backend.generic_templates (organization_id);

EOSQL
