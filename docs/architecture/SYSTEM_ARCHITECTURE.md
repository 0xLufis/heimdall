# Heimdall System Architecture & Data Model

This document defines the system architecture, domain data model, database schema, and service layers of the Heimdall platform.

---

## 1. Domain Topology & The Graph-Relational Model

In manufacturing automation environments, production lines do not follow rigid hierarchical trees. A single manufacturing station (such as a robotic welding cell or an automated packaging station) often involves multiple controllers:
* A primary **Industrial PC (IPC)** running human-machine interface (HMI) software and telemetry agents.
* A **Hardware PLC** or **Soft-PLC** (e.g., Beckhoff TwinCAT) executing real-time cyclic control logic.
* Specialized controllers for safety gates, vision inspection, motion control, and dispensing heads.

Conversely, a high-performance multi-core IPC may control multiple physical stations or process zones simultaneously.

To model this reality without artificial constraints, Heimdall implements a **graph-relational data model** with explicit many-to-many ($M:N$) junctions and typed directional interconnects between equipment.

```
┌─────────────────────────┐               M:N               ┌───────────────────────────┐
│    ProductionStation    │◄───────────────────────────────►│   IndustrialController    │
│  (e.g., LINE-A-OP10)    │       StationController         │   (IPC / PLC / Soft-PLC)  │
└────────────┬────────────┘       (Role & Primary Flag)     └─────────────┬─────────────┘
             │                                                            │
             │ 1:N                                                        │ 1:N
             ▼                                                            ▼
┌─────────────────────────┐                                 ┌───────────────────────────┐
│     StationHardware     │                                 │    ControllerHardware     │
│    (Junction Entity)    │                                 │     (Junction Entity)     │
└────────────┬────────────┘                                 └─────────────┬─────────────┘
             │                                                            │
             └──────────────────────────────┬─────────────────────────────┘
                                            │
                                            ▼
                                ┌───────────────────────┐
                                │   HardwareComponent   │
                                │   (Sensors, Drives)   │
                                └───────────┬───────────┘
                                            │
                                            │ 1:N
                                            ▼
                                ┌───────────────────────┐
                                │     SoftwareAsset     │
                                │   (Firmware, Logic)   │
                                └───────────────────────┘
```

---

## 2. Core Entity Specifications

The platform uses Entity Framework Core with Table-per-Type (TPT) inheritance for asset tracking, combined with explicit graph edge tables and JSONB document columns for dynamic metadata.

### 2.1 Base Inventory Asset (`inventory_items`)
The abstract foundation for all physical and logical assets:
* `id` (`uuid`, PK): Immutable unique identifier.
* `name` (`varchar(255)`): Internal asset name (e.g., `L01-OP10`).
* `display_name` (`varchar(255)`): Human-readable descriptive name (e.g., `Line 01 - Body Assembly Alpha - Station 10`).
* `organization_id` (`varchar(128)`): Multi-tenant ownership identifier. Filtered globally across queries.
* `manufacturer_id` (`uuid`, FK): Reference to the equipment manufacturer (`manufacturers` table).
* `supplier_id` (`uuid`, FK): Reference to the procurement vendor (`suppliers` table).
* `serial_number` (`varchar(255)`): Vendor hardware serial number.
* `cost_in_huf` (`numeric(18,2)`): Procurement or replacement valuation in Hungarian Forints.
* `purchase_date` (`timestamptz`): Acquisition date.
* `parent_id` (`uuid`, FK, nullable): Self-referencing link supporting recursive assembly trees.
* `metadata` (`jsonb`): Semi-structured attributes indexed via PostgreSQL GIN (`jsonb_path_ops`). Stores cycle time targets, electrical ratings, pneumatic limits, and bus parameters.

### 2.2 Production Stations (`stations`)
Extends `BaseInventoryItem` for physical stations and assembly lines:
* `custom_identifier` (`varchar(100)`): Factory floor station code (e.g., `LINE-01-OP10`). Indexed with `organization_id`.
* `pinned_object_handle` (`varchar(64)`): AutoCAD DXF block entity handle or SVG element ID linking the station to factory CAD drawings.
* Relationships:
  * `Controllers`: Navigation collection to `StationController` junctions.
  * `Tickets`: Incident tickets associated with this station.

### 2.3 Industrial Controllers (`client_pcs`)
Represents edge compute nodes, industrial PCs, and soft-PLC hosts:
* `mac_address` (`varchar(17)`, unique index): Normalized primary hardware identifier (`XX:XX:XX:XX:XX:XX`).
* `hostname` (`text`): Network device hostname, indexed for lookup.
* `ip_address` (`text`): Current primary IPv4 address.
* `machine_identifier` (`text`): Persistent hardware UUID (Windows `MachineGuid` or Linux `/etc/machine-id`).
* `last_online` (`timestamptz`): Timestamp of the most recent heartbeat.
* `free_disk_space` (`jsonb`): Drive letters, total space, and free space in gigabytes.
* `monitoring_config` (`jsonb`): Node-specific telemetry collection overrides (sampling rates, enabled adapters).
* `resource_averages` (`jsonb`): Rolling window CPU load, memory utilization, and network egress metrics.
* `alerting_limits` (`jsonb`): Configurable thresholds (disk space alerts, memory warnings).
* `system_metadata` (`jsonb`, GIN indexed): Detailed OS specifications, kernel version, installed Beckhoff driver versions, and network adapter hardware info.

### 2.4 Station-to-Controller Junction (`StationControllers`)
Models the $M:N$ mapping between physical stations and managing edge computers:
* `StationId` (`uuid`, PK/FK): Production station.
* `ClientPcId` (`uuid`, PK/FK): Industrial controller.
* `ControlRole` (`varchar(32)`): Operational role (`Primary`, `Secondary`, `Safety`, `Motion`, `Vision`, `Gateway`).
* `IsPrimary` (`boolean`): Flags the authoritative controller for scheduling and high-priority alarms.

### 2.5 Equipment Interconnects (`equipment_interconnects`)
Directional fieldbus and network connections between edge nodes:
* `id` (`uuid`, PK): Interconnect identifier.
* `source_controller_id` (`uuid`, FK): Originating controller.
* `target_controller_id` (`uuid`, FK): Destination device or controller.
* `protocol` (`varchar(32)`): Protocol type (`EtherCAT`, `PROFINET`, `ModbusTCP`, `OPC_UA`, `EtherNet_IP`).
* `channel_info` (`text`): Physical interface, subnet, or slave address details (e.g., `Port 1 -> Slave 04 (EK1100)`).

### 2.6 Incident Maintenance Tickets (`maintenance_tickets`)
Tracks equipment malfunctions, maintenance requests, and repairs:
* `id` (`uuid`, PK): Unique ticket identifier.
* `ticket_number` (`varchar(32)`, unique): Human-readable sequential code (e.g., `TKT-2026-0042`).
* `title` (`varchar(255)`): Short summary of the defect or task.
* `description` (`text`): Detailed observations, error codes, or symptoms.
* `status` (`varchar(32)`): Lifecycle state (`Open`, `In_Progress`, `Pending_Parts`, `Resolved`, `Closed`).
* `priority` (`varchar(32)`): Urgency (`Low`, `Medium`, `High`, `Critical`).
* `station_id` (`uuid`, FK, nullable): Affected production station.
* `client_pc_id` (`uuid`, FK, nullable): Affected IPC/controller.
* `inventory_item_id` (`uuid`, FK, nullable): Specific failed hardware component.
* `reported_by` (`varchar(128)`): User identifier of the reporter.
* `assigned_to` (`varchar(128)`, nullable): Assigned technician identifier.
* `created_at` / `updated_at` (`timestamptz`): Audit timestamps.
* `sla_due_at` (`timestamptz`, nullable): Service level agreement resolution deadline.

---

## 3. Database Architecture & Schema Isolation

The relational store is partitioned into two schemas to enforce separation of concerns and least-privilege database access:

```
Database: heimdall_dev_db
├── Schema: backend (Managed by EF Core Migrations)
│   ├── inventory_items / stations / hardware_assets / software_assets
│   ├── client_pcs / StationControllers / equipment_interconnects
│   ├── maintenance_tickets / ticket_comments / ticket_attachments
│   ├── floor_plans / floor_plan_anchor
│   ├── agent_events / queued_agent_commands
│   ├── audit_logs / malformed_telemetry_quarantine
│   └── security_group_mappings / system_settings
│
└── Schema: auth (Managed by Better-Auth Drizzle migrations)
    ├── user (id, name, email, role, image, createdAt, updatedAt)
    ├── session (id, userId, token, expiresAt, ipAddress, userAgent)
    ├── account (id, userId, accountId, providerId, accessToken, ...)
    ├── verification (id, identifier, value, expiresAt)
    ├── organization (id, name, slug, logo, metadata, createdAt)
    └── member (id, organizationId, userId, role, createdAt)
```

### Database Roles & Least Privilege:
1. `ef_admin`: Owns the `backend` schema. Used strictly for schema migration execution (`CREATE TABLE`, `ALTER TABLE`, `CREATE INDEX`).
2. `dotnet_backend`: Used by the backend application runtime. Granted `SELECT`, `INSERT`, `UPDATE`, `DELETE` privileges on tables and sequences in `backend`. Has no DDL (`DROP`/`ALTER`) rights.
3. `nuxt_frontend`: Used by the Nuxt Nitro BFF runtime. Granted DML privileges on the `auth` schema and read access to necessary reporting views.

### Multi-Tenancy via Global Query Filters
Multi-tenancy is enforced at the data access level in `AppDbContext`:
```csharp
modelBuilder.Entity<ClientPc>().HasQueryFilter(e => 
    _tenantService.IsSuperAdmin || e.OrganizationId == _tenantService.CurrentOrganizationId);

modelBuilder.Entity<BaseInventoryItem>().HasQueryFilter(e => 
    _tenantService.IsSuperAdmin || e.OrganizationId == _tenantService.CurrentOrganizationId);

modelBuilder.Entity<MaintenanceTicket>().HasQueryFilter(e => 
    _tenantService.IsSuperAdmin || e.OrganizationId == _tenantService.CurrentOrganizationId);
```
Every query issued by EF Core automatically appends `AND (organization_id = @currentOrg OR @isSuperAdmin)` to the generated SQL, preventing cross-tenant data leakage.

---

## 4. Backend Service Architecture

The backend adopts clean separation between controllers, business services, and repository layers:

```
[ HTTP REST / JSON ]   [ HTTP/2 gRPC ]   [ WebSockets / SignalR ]
        │                     │                      │
        ▼                     ▼                      ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ API Controllers  │  │   gRPC Service   │  │  MaintenanceHub  │
└────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘
         │                     │                     │
         └───────────────┬─────┴─────────────────────┘
                         ▼
        ┌──────────────────────────────────┐
        │      Application Services        │
        │  (TicketService, InventorySvc)   │
        └────────────────┬─────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ▼                               ▼
┌──────────────────┐            ┌──────────────────┐
│   CacheService   │            │   Repositories   │
│  (L1/L2 Hybrid)  │            │ (EF Core Npgsql) │
└────────┬─────────┘            └────────┬─────────┘
         │                               │
         ▼                               ▼
 [ Redis / Memory ]              [ PostgreSQL 18 ]
```

### 4.1 Hybrid Multi-Tier Caching (`CacheService`)
To handle frequent telemetry queries without saturating the database, a two-level caching strategy is employed:
1. **L1 Local Memory (`IMemoryCache`)**: Sub-millisecond reads for high-frequency reads (dashboard summary KPIs, system settings). Keys are tracked in a thread-safe dictionary to allow wildcard and prefix invalidation.
2. **L2 Distributed Cache (`StackExchange.Redis`)**: Shared cache across API instances with structured keys (`heimdall:{tenant}:{entity}:{id}`).
3. **Resilient Offline Bypass**: If Redis is unreachable, `CacheService` catches connection exceptions, logs a warning, and falls back to L1 local memory without failing incoming user requests.
4. **Write-Through Pattern Invalidation**: When an entity is updated (e.g., ticket status change), the repository executes `RemoveByPatternAsync("heimdall:tickets:*")`, evicting matching L1 regex patterns and executing batch key deletions in Redis.

### 4.2 Real-Time Event Broadcasting
Modifications to maintenance tickets or incoming critical alarms trigger real-time notifications:
1. Controller or service updates entity in the database.
2. Relevant cache keys are evicted.
3. `IHubContext<MaintenanceHub, IMaintenanceClient>` broadcasts the event (`TicketStatusUpdated`, `TicketCreated`, `CriticalAlertRaised`) to subscribers in the target organization's SignalR group.
