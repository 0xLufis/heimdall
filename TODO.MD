# Heimdall Development Roadmap & Action Items

> **Status:** Active Development (PoC / Architectural Refactoring Phase)

---

## Milestone 1: Core Domain Graph Model & Database Schema
- [x] Refactor domain model from rigid component tree to graph-relational architecture.
- [x] Establish `StationController` junction table for M:N relationships between Production Stations and IPCs/Controllers.
- [x] Separate `HardwareComponent` and `SoftwareAsset` entities with relational junction tables (`ControllerHardware`, `StationHardware`, `ControllerSoftware`).
- [x] Add specialized controller classification (`IPC`, `PLC`, `SoftPLC`, `RobotController`, `Dispenser`, `VisionController`, `AutonomousDevice`).
- [x] Implement `EquipmentInterconnect` entity for OT fieldbus topology (EtherCAT, PROFINET, OPC UA, Modbus TCP).
- [ ] Generate EF Core migration for schema updates (`backend` & `shared` projects).
- [ ] Update seed data scripts (`seed_data/inventory_seed.csv`, `incremental_seed.py`, `simulate_pcs.py`) to support M:N station-controller relationships.

---

## Milestone 2: Windows System Telemetry & Driver Diagnostics
- [x] Design WMI / P/Invoke telemetry contract for Beckhoff Real-Time Ethernet Drivers (`TcRTEthernet`, `TcEth`).
- [x] Implement Registry uninstall key scanner for fast installed software detection on Windows IPCs.
- [ ] Add Beckhoff TwinCAT ADS System API integration (`TcAdsDll.dll` / ADS protocol) to query TwinCAT AMS NetID, PLC state, and RT task CPU load.
- [ ] Implement Windows SetupAPI (`cfgmgr32.dll` / `setupapi.dll`) P/Invoke wrapper in `App.Agent.Daemon` for exact PCI Vendor/Device ID extraction (`VEN_8086&DEV_1539`).

---

## Milestone 3: Industrial Integration APIs (OPC UA & Copia Automation)
- [x] Architectural blueprint for Heimdall OPC UA Server & Gateway (`opc.tcp://0.0.0.0:4840/Heimdall`).
- [x] Design Copia Automation Git integration blueprint for PLC/Soft-PLC project version tracking.
- [ ] Implement Copia webhook receiver in .NET Web API (`/api/v1/integrations/copia/webhook`).
- [ ] Implement OPC UA Information Model mapping aligned with OPC 10000-100 (Devices) and OPC IA specs.

---

## Milestone 4: TISAX Security Compliance & Auditing
- [x] TISAX (VDA ISA 6.0) security assessment framework mapping.
- [x] Enforce EF Core Global Query Filters for multi-tenant isolation (`OrganizationId`).
- [x] Command signature validation for queued agent operations.
- [ ] Add automated security audit logging for configuration changes and user logins (`audit_logs`).
- [ ] Implement mTLS support for gRPC Agent telemetry channel.

---

## Milestone 5: Database Caching, Indexing & Performance Optimization
- [x] Multi-tier caching strategy (L1 `IMemoryCache` for static references, L2 Redis for telemetry state).
- [x] Data tiering specification (Instant Summary Payload vs. On-Demand Lazy Queries).
- [x] PostgreSQL GIN indexing for JSONB columns (`system_metadata`, `metadata`).
- [ ] Configure Redis L2 cache provider in `App.Backend.Api`.
- [ ] Add database ETag / Conditional GET middleware (`304 Not Modified`) in Nuxt Nitro BFF.

---

## Milestone 6: Frontend Offloading & UI Refactoring
- [x] Fix Nuxt 4 `shadcn-vue` component structure (`app/components/ui/searchable-select/`).
- [x] Configure Vitest test runner with JSDOM environment and alias resolving.
- [x] Modularize `AppSidebar.vue` and `useAppSettings.ts` composables.
- [ ] Move client-side sorting, filtering, and aggregation to Nuxt Nitro server routes (`server/api/`).
- [ ] Add spatial layout viewer component with interactive DXF handle selection.

