#!/usr/bin/env bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE SCHEMA IF NOT EXISTS backend;

    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS status character varying(64) DEFAULT 'active';
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS decommission_reason text;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS decommission_date timestamp with time zone;
    ALTER TABLE IF EXISTS backend.stations ADD COLUMN IF NOT EXISTS decommission_disposition character varying(64);
EOSQL
