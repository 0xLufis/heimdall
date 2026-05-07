# API Contracts (DTOs)

Heimdall uses specialized Data Transfer Objects (DTOs) to optimize network traffic and simplify frontend consumption.

## Dashboard DTOs

### DashboardDto
The aggregate root for the main dashboard view.
* **Stats**: High-level metrics.
* **RecentClients**: Latest node activity.
* **SecurityEvents**: Recent system alerts.

### DashboardStatsDto
* **TotalUsers**: System-wide user count.
* **ActiveClients**: Real-time operational nodes.
* **PendingAlerts**: 24h error count.
* **AvgUptime**: Availability metric.

## Spatial & Inventory DTOs

### ClientPcDto
Flattened representation for high-concurrency spatial views.
* **PinnedObjectHandle**: CAD anchor.
* **InventoryItems**: Nested tree of components.
* **Machines**: Summary of controlled stations.

### MachineDto
Comprehensive station data.
* **CustomIdentifier**: Operational label.
* **Children**: Nested hardware tree.
* **Controllers**: Mapping to managing PCs.

### InventoryItemDto
Generic recursive DTO for the inventory tree.
* **ItemType**: Concrete class identifier.
* **Metadata**: Dynamic attributes.
* **Children**: Recursive nesting.