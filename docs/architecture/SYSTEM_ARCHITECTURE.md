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
* `serial_number` (`varchar(255)`): Vendor hardware serial number or batch lot code.
* `cost_in_huf` (`numeric(18,2)`): Procurement or replacement valuation in Hungarian Forints.
* `purchase_date` (`timestamptz`): Acquisition date.
* `parent_id` (`uuid`, FK, nullable): Self-referencing link supporting recursive assembly trees.
* `metadata` (`jsonb`): Semi-structured attributes indexed via PostgreSQL GIN (`jsonb_path_ops`). Stores cycle time targets, electrical ratings, pneumatic limits, and bus parameters.
* `storage_location` (`varchar(255)`): Shelf, cabinet, bin, or factory cell location (e.g. `Shelf B3`, `Bin 42-B`, `L01-OP10 Mount Bay 1`).
* `equipment_status` (`varchar(50)`): Lifecycle operational status:
  * `InStorage`: Spare unit or stock kept in warehouse storage awaiting deployment.
  * `InMachine`: Installed component actively operating on a physical production station.
  * `UnderRepair`: Component pulled from line undergoing diagnostic service or repair depot maintenance.
  * `Decommissioned`: Obsolete asset retired from service.
* `machine_id` (`uuid`, FK, nullable): Navigation link to the parent `Machine` where this component is installed (when `equipment_status == InMachine`).
* `stock_quantity` (`int`, nullable): Current quantity on hand for bulk consumable/spare stock items (e.g. 9 bolts, 24 quick couplers).
* `min_stock_threshold` (`int`, nullable): Safety reorder threshold for automated inventory alerts.
* `is_stock_item` (`boolean`): Discriminator flag distinguishing bulk quantity-tracked stock (`true`) from discrete, high-value one-of serialized capital assets (`false`).
* `technology` (`varchar(100)`): Manufacturing technology domain (e.g. `Assembly`, `Test`, `SMT`, `Welding`, `Fastening`, `Dispensing`, `Robotics`).

### 2.2 Production Stations & Lines (`stations`)
Extends `BaseInventoryItem` (TPT child) representing a process node or machine cell on the factory floor:
* `custom_identifier` (`varchar(255)`): Factory floor station code (e.g., `L01-OP10`, `STATION-OP20-02`).
* `machine_type` (`varchar(100)`): Functional automation classification (e.g. `Automatic Optical Inspection`, `Gap Filler`, `Screwing Station`, `Soldering`, `Milling`, `Fitting`, `Pressing`, `Manipulator`, `Tester Cell`).
* `group_id` (`varchar(100)`): Production line envelope identifier (e.g. `Line 01 - Body Assembly Alpha`, `Line 06 - Automated Battery Module Line`).
* `pinned_object_handle` (`varchar(64)`): AutoCAD DXF block entity handle or SVG element ID linking the station to factory CAD drawings.
* `preferred_technician_id` / `preferred_technician_name`: Designated primary technician for dedicated station servicing.
* Relationships:
  * `Controllers`: Navigation collection to `StationController` junctions.
  * `Tickets`: Incident tickets associated with this station.
  * `InstalledComponents`: Components where `machine_id == station.id`.

### 2.3 Discrete Parts vs. Bulk Stock Inventory Model
Heimdall enforces a strict distinction between two categories of inventory assets:
1. **Discrete High-Value Parts (`is_stock_item = false`)**:
   * Individual one-of serialized pieces of equipment (e.g., Siemens S7-1500 PLC, Cognex In-Sight 9000 camera, KUKA servo drive, Fanuc robot controller).
   * Tracked by unique `serial_number`.
   * Exactly located: either installed on a machine (`InMachine`, linked to `machine_id`), resting in central storage (`InStorage`, linked to `storage_location`), or in repair depot (`UnderRepair`).
   * Tree visualization: Station component trees are rendered on-demand via modal dialog (`StationComponentTreeModal`) rather than cluttering the primary inventory view with an inflexible tree page.
2. **Bulk Stock Consumables (`is_stock_item = true`)**:
   * Quantity-tracked components and consumable hardware (e.g., M8 bolts, pneumatic quick couplers, dispenser nozzles, solder paste, fuses, terminal blocks).
   * Monitored via `stock_quantity` against `min_stock_threshold`.
   * Bin storage tracking (e.g., `Bin 18-A`, `Bin 42-B`).
   * Low-stock reorder warnings triggered dynamically when `stock_quantity <= min_stock_threshold`.

### 2.4 Enterprise Engineering Organization & Governance Role Hierarchy
The security and permission model reflects real-world plant organizational structures, strictly separating engineering administrative domains from operational shift management:
* **`system_admin`**: Superuser / god user with root permissions across all platform subsystems and administrative role overrides.
* **`it_admin` / `it_site_admin`**: IT infrastructure administrator. Authoritative for Active Directory and Microsoft Entra ID OU read/write approval, PKI certificates, and IT security policies.
* **`heimdall_admin`**: OT platform administrator managing overall Heimdall operational settings, telemetry pipelines, and security group mappings. May inherit pseudo-IT admin capabilities when enabled by system admin.
* **`engineering_admin`**: Engineering technical administrator managing users, functional engineering configurations, recipes, and machine profiles from the application UI (strictly segregated from production management).
* **`plant_director`**: Executive engineering director with plant-wide read access and final sign-off authority for line stops and capital investments.
* **`plant_engineering_manager` & `senior_engineering_manager`**: Senior engineering leaders managing maintenance strategy, technology disciplines, and equipment life-cycles.
* **`group_leader`**: First-line engineering supervisors organized either by industrial technology domain (`Assembly`, `Welding`, `SMT`, `Test`, `Robotics`) or by production line envelope.
* **`engineer`**: Automation, controls, vision, and process engineers with execution permissions for PLC logic, calibration, and diagnostics.
* **`technician`**: Mechatronics and maintenance technicians with work order execution, parts replacement, and field repair logging permissions.
* **`operative_planner`**: Line planning managers responsible for production scheduling, takt time monitoring, and requesting/authorizing scheduled line stops.

### 2.5 Industrial Controllers (`client_pcs`)
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

### 2.6 Station-to-Controller Junction (`StationControllers`)
Models the $M:N$ mapping between physical stations and managing edge computers:
* `StationId` (`uuid`, PK/FK): Production station.
* `ClientPcId` (`uuid`, PK/FK): Industrial controller.
* `ControlRole` (`varchar(32)`): Operational role (`Primary`, `Secondary`, `Safety`, `Motion`, `Vision`, `Gateway`).
* `IsPrimary` (`boolean`): Flags the authoritative controller for scheduling and high-priority alarms.

### 2.7 Equipment Interconnects (`equipment_interconnects`)
Directional fieldbus and network connections between edge nodes:
* `id` (`uuid`, PK): Interconnect identifier.
* `source_controller_id` (`uuid`, FK): Originating controller.
* `target_controller_id` (`uuid`, FK): Destination device or controller.
* `protocol` (`varchar(32)`): Protocol type (`EtherCAT`, `PROFINET`, `ModbusTCP`, `OPC_UA`, `EtherNet_IP`).
* `channel_info` (`text`): Physical interface, subnet, or slave address details (e.g., `Port 1 -> Slave 04 (EK1100)`).

### 2.8 Incident Maintenance Tickets (`maintenance_tickets`)
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

### 2.9 OmniSearch 2.0 Language-Server Search Pipeline
Heimdall includes an advanced industrial search engine styled after `nvim-coc` autocompletion:
* **3-Stage Search Engine**:
  1. *Prefix Tokenizer*: Identifies filter directives (`@technology`, `#status`, `!priority`, `typ:`, `org:`).
  2. *Live Tag Suggestions*: Floating completion dropdown offering matching technology tags, operational statuses, and equipment categories with keyboard navigation (`ArrowUp`, `ArrowDown`, `Enter`, `Tab`).
  3. *Fuzzy Ranking*: Exact prefix matches are scored highest ($100$), followed by token substring matches ($50$), case-insensitive matches ($25$), and fuzzy typo-tolerant matches.
* Search scopes include stations, controllers, discrete serialized parts, bulk stock items, and maintenance tickets with instantaneous sub-5ms UI filtering.

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
