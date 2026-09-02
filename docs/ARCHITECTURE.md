# Heimdall System Architecture & Data Model Specification

This document presents the detailed architectural design, data model graph, and integration blueprints for the **Heimdall Industrial Management System**.

---

## 1. Domain Data Model (Graph-Relational Architecture)

In industrial automation environments, production stations, controllers, hardware components, and software applications do not strictly conform to a simple parent-child tree. Heimdall implements a **graph-relational network model** that supports many-to-many (`M:N`) relationships.

### 1.1 Key Data Model Concept

```
  +--------------------+         M:N          +------------------------+
  | ProductionStation  |<-------------------->| IndustrialController   |
  | (Machine/Cell/Line)|  StationController   | (IPC / PLC / Soft-PLC) |
  +--------------------+                      +------------------------+
           |                                               |
           | 1:N                                           | 1:N
           v                                               v
  +--------------------+                      +------------------------+
  |  StationHardware   |                      |   ControllerHardware   |
  | (Junction / Asset) |                      |   (Junction / Asset)   |
  +--------------------+                      +------------------------+
           |                                               |
           +-----------------------+-----------------------+
                                   |
                                   v
                        +--------------------+
                        | HardwareComponent  |
                        | (Sensors, Drives)  |
                        +--------------------+
                                   |
                                   | 1:N
                                   v
                        +--------------------+
                        | SoftwareAsset      |
                        | (PLC/Firmware/OS)  |
                        +--------------------+
```

---

### 1.2 Entity Specifications

#### `ProductionStation` (`stations`)
Represents a physical manufacturing station, assembly line cell, or process node.
- `Id` (Guid, PK)
- `CustomIdentifier` (String, e.g., `"LINE-A-OP10"`)
- `Name` (String)
- `OrganizationId` (String, Tenant FK)
- `PinnedObjectHandle` (String, CAD DXF/SVG Handle)
- `Controllers` (Collection of `StationController` junction records)

#### `IndustrialController` (`controllers` / `client_pcs`)
Represents an Industrial PC (IPC), Hardware PLC, Soft-PLC (TwinCAT), Robot Controller, or specialized autonomous controller.
- `Id` (Guid, PK)
- `Hostname` (String)
- `MacAddress` (String)
- `IpAddress` (String)
- `ControllerType` (Enum: `IPC`, `PLC`, `SoftPLC`, `RobotController`, `Dispenser`, `VisionController`, `AutonomousDevice`)
- `LastOnline` (DateTimeOffset)
- `SystemMetadata` (JSONB: OS, IP, WMI/SetupAPI telemetry)
- `ControlledStations` (Collection of `StationController` junction records)

#### `StationController` (Junction Table)
Establishes the many-to-many relationship between `ProductionStation` and `IndustrialController`.
- `StationId` (Guid, FK)
- `ControllerId` (Guid, FK)
- `ControlRole` (Enum: `Primary`, `Secondary`, `Safety`, `Motion`, `Vision`)
- `IsPrimary` (Boolean)

#### `HardwareComponent` (`hardware_components`)
Represents physical hardware assets (CPUs, RAM sticks, NICs, Servo Drives, Dispensing Heads).
- `Id` (Guid, PK)
- `SerialNumber` (String)
- `ModelNumber` (String)
- `Revision` (String)
- `Category` (Enum: `NIC`, `CPU`, `RAM`, `Storage`, `Servo`, `FieldbusCoupler`, `DispenserHead`)
- `ManufacturerId` (Guid, FK)
- `Metadata` (JSONB)

#### `SoftwareAsset` (`software_assets`)
Represents logical software assets, PLC programs, OS installations, drivers, or firmware.
- `Id` (Guid, PK)
- `Name` (String)
- `Publisher` (String)
- `Version` (String)
- `SoftwareType` (Enum: `OS`, `Driver`, `SoftPLC_Project`, `MES_Connector`, `OPC_UA_NodeSet`, `Patch`)
- `CopiaRepoUrl` (String, optional Copia Git URL)
- `CopiaCommitSha` (String, optional Git SHA)

#### `EquipmentInterconnect` (`equipment_interconnects`)
Tracks physical fieldbus and industrial ethernet connections between controllers and station equipment.
- `Id` (Guid, PK)
- `SourceControllerId` (Guid, FK)
- `TargetControllerId` (Guid, FK)
- `Protocol` (Enum: `EtherCAT`, `PROFINET`, `OPC_UA`, `ModbusTCP`, `EtherNet_IP`)
- `ChannelInfo` (String, e.g., `"Port 1 -> EtherCAT Slave 05"`)

---

## 2. Copia Automation Integration Architecture

**Copia Automation** provides Git-based version control for PLC programs (TwinCAT, Siemens TIA Portal, Rockwell Studio 5000).

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Controls Engineer
    participant Copia as Copia Automation (Git)
    participant Heimdall as Heimdall Web API
    participant Agent as Heimdall Agent (IPC)

    Dev->>Copia: Git Push PLC project commit
    Copia->>Heimdall: Webhook POST /api/v1/integrations/copia/webhook
    Heimdall->>Heimdall: Update SoftwareAsset (Commit SHA & Version)
    Heimdall->>Agent: Queue Command (UPDATE_PLC_PROJECT)
    Agent->>Agent: Verify TwinCAT / PLC deployment state
    Agent->>Heimdall: gRPC ReportSystemInfo (Status: Deployed)
```

---

## 3. Windows System API Telemetry Architecture

The Heimdall Agent running on Windows Industrial PCs queries low-level hardware and driver details using a combination of WMI, P/Invoke, and Registry API calls:

```mermaid
graph TD
    AgentWorker[Agent Worker Service] --> WMIQuery[WMI ExecQuery]
    AgentWorker --> PInvokeSetupAPI[P/Invoke setupapi.dll / cfgmgr32.dll]
    AgentWorker --> RegScanner[Registry Uninstall Key Scanner]
    AgentWorker --> AdsClient[Beckhoff TwinCAT ADS API]

    WMIQuery --> Win32_PnPSignedDriver[Win32_PnPSignedDriver]
    PInvokeSetupAPI --> DevicePnpID[PCI VEN/DEV IDs & RT NIC Driver Status]
    RegScanner --> InstalledSoftware[Fast Software Inventory List]
    AdsClient --> TwinCatState[TwinCAT AMS NetID & PLC Run State]
```

---

## 4. Dual-ORM & Multi-Tenant Data Layer Architecture

Heimdall employs a specialized dual-ORM strategy designed to isolate authentication and authorization from operational manufacturing telemetry:

```mermaid
graph TD
    subgraph AppRuntimes ["Application Runtimes"]
        DotnetAPI[".NET 9 Web API & gRPC Collector"]
        NuxtBFF["Nuxt 4 / Nitro BFF"]
    end

    subgraph DataAccess ["ORM Data Access Layer"]
        EF["Entity Framework Core 9\n(Schema: 'backend')"]
        Drizzle["Drizzle ORM\n(Schema: 'auth')"]
    end

    subgraph Database ["PostgreSQL 17.9 Container (heimdall_dev_db)"]
        subgraph BackendSchema ["'backend' Schema"]
            Inv["inventory_items (TPT Inheritance)"]
            PCs["client_pcs (GIN Indexing)"]
            Junc["StationControllers (M:N Graph)"]
            Tickets["maintenance_tickets"]
            Audit["audit_logs & malformed_quarantine"]
        end

        subgraph AuthSchema ["'auth' Schema"]
            Users["user / session / account"]
            Orgs["organization / member"]
        end
    end

    DotnetAPI -->|Role: dotnet_backend| EF
    NuxtBFF -->|Role: nuxt_frontend| Drizzle
    EF --> BackendSchema
    Drizzle --> AuthSchema
```

### 4.1 Multi-Tenancy via Global Query Filters
Multi-tenancy is enforced at the `AppDbContext` level using EF Core Global Query Filters:
```csharp
modelBuilder.Entity<ClientPc>()
    .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);
```
All queries generated by repository classes automatically append `WHERE organization_id = @CurrentOrganizationId`, guaranteeing strict data boundaries between factory plants.

---

## 5. Edge Offline Telemetry Spooler & Quota Architecture

To ensure telemetry data is never lost during network maintenance or plant WiFi interruptions, `App.Agent.Daemon` includes an offline spooling ring buffer:

```mermaid
sequenceDiagram
    autonumber
    participant Worker as Agent Worker
    participant Reporter as SystemInfoReporter
    participant Spooler as LocalTelemetrySpooler (Disk)
    participant Backend as gRPC Collector (Backend API)

    Worker->>Reporter: Report SystemInfoData
    alt Network Connected
        Reporter->>Backend: gRPC ReportSystemInfoAsync()
        Backend-->>Reporter: Response (Success)
        Reporter->>Spooler: Trigger DrainSpoolAsync() in background
    else Network Disconnected / RpcException
        Reporter-->>Spooler: SpoolPayloadAsync(json)
        Spooler->>Spooler: Check MaxSpoolDiskMb Quota (FIFO Eviction)
        Spooler->>Spooler: Write spool_timestamp_guid.json
    end
```


