# Data Models (Entities)

Heimdall utilizes an Object-Oriented schema modeled with Entity Framework Core's Table-per-Type (TPT) inheritance.

## Core Abstractions

### BaseInventoryItem
The root class for all physical and logical assets.
* **Id**: Unique identifier.
* **Name / DisplayName**: Primary identification.
* **OrganizationId**: Ownership mapping.
* **Financials**: `CostInHUF`, `PurchaseDate`.
* **Hierarchy**: `ParentId` and `Children` (Tree structure).
* **Metadata**: Flexible JSONB for domain-specific attributes.

## Primary Entities

### ClientPc
Represents a physical PC/Terminal on the factory floor.
* **MacAddress / IPAddress**: Network identifiers.
* **Hostname**: Device name.
* **LastOnline**: Heartbeat timestamp.
* **ControlledMachines**: Stations managed by this PC.
* **InventoryItems**: Internal hardware/software components.
* **Telemetry**: `FreeDiskSpace`, `ResourceAverages`.

### Machine
Represents a Production Station or Process Node.
* **CustomIdentifier**: Operational label (e.g., OP10).
* **PinnedObjectHandle**: Spatial mapping to CAD layouts.
* **Controllers**: List of ClientPcs managing this station.

## Components & Activity

### HardwareComponent
Station-level equipment (Valves, Sensors, Motors).
* **Revision / ModelNumber**.

### SoftwareComponent
Logical assets (PLC Programs, Licenses).
* **Version / LicenseKey**.

### AgentEvent
Security or system events reported by an Agent.
* **Source / Message / Level / Timestamp**.

### QueuedAgentCommand
Remote commands awaiting execution.
* **Type / Payload / Signature**.