# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Multi-tenancy global query filters (`HasQueryFilter`) in `AppDbContext` for `ClientPc`, `BaseInventoryItem`, `MaintenanceTicket`, `AgentEvent`, and `AuditLog`.
- `AuditLog` entity for immutable tracking of user actions, role assignments, and configuration changes (TISAX ISA 5.1 / NIS2 compliance).
- `MalformedTelemetryRecord` dead-letter quarantine table storing unparseable or rejected telemetry events (Guideline 36).
- `LocalTelemetrySpooler` in `App.Agent.Daemon` providing offline telemetry buffering with FIFO quota eviction (Guidelines 21, 22, 23).
- Continuous Integration workflow `.github/workflows/ci.yml` running backend (.NET 9) and frontend (Nuxt/Bun) test suites.
- `.editorconfig` enforcing standard formatting rules across C# and TypeScript/Vue codebases.
- EF Core migration `SystemGovernanceAndPki` for governance, PKI, and audit entities.
- Dual-Licensing model: GNU Affero General Public License v3.0 (AGPL-3.0) for open source with Commercial Enterprise Licensing for proprietary enterprise deployments.
- Unit tests for multi-tenant query filters, fail-secure signature verification, and offline telemetry spooler.

### Changed
- Switched backend runtime (`Program.cs`, `appsettings.json`) and frontend BFF (`server/utils/db.ts`) connection defaults from `ef_admin` to least-privilege DML accounts (`dotnet_backend`, `nuxt_frontend`).
- Hardened agent command signature verification to fail-secure mode when `ServerPublicKey` is absent, unless `AllowUnsignedCommands` is explicitly enabled.
- Reconciled table names and column definitions between Python seed pipeline (`seed_pipeline.py`) and EF Core entities (`client_certificates`, `schema_version_manifest`).
- Added Redis authentication (`requirepass`) and restricted PostgreSQL query logging (`log_statement=ddl`) in `infra/database/docker-compose.yml`.
- Configured max database connections to 300 in PostgreSQL container to accommodate Npgsql's connection pool size of 250.
- Deduplicated `MaintenanceTicket` entity configuration in `AppDbContext.cs`.
- Added missing B-tree indexes on `maintenance_tickets(status, priority, created_at, assigned_to, organization_id)` and `agent_events(client_pc_id, timestamp)`.

## [0.1.0-alpha] - 2026-09-02

### Added
- Initial project baseline: ASP.NET Core .NET 9 API, Nuxt 4 Web Frontend, Edge Agent Daemon.
- Hybrid Graph-Relational inventory model with Table-per-Type (TPT) inheritance.
- GIN indexed JSONB metadata for dynamic industrial asset telemetry.
- Field-level AES-256-GCM encryption for license keys and sensitive layout assets.
- Initial seed pipeline generating 500 manufacturing stations, 500 IPCs, and associated components.
