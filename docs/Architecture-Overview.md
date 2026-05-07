# Architecture Overview

Heimdall is built on a distributed three-pillar architecture designed for high performance, scalability, and spatial awareness in industrial environments.

## The Three Pillars

### 1. The Edge Agent (The Probe)
A lightweight C# background service deployed on physical factory floor PCs.
* **Telemetry**: Collects real-time CPU, RAM, and Disk metrics.
* **Control**: Implements a secure command queuing system for remote configuration and orchestration.
* **Heartbeat**: Maintains a sub-5 minute synchronization with the central core.

### 2. The High-Performance Core (The Brain)
A .NET 9 Web API backed by a PostgreSQL database.
* **TPT Inheritance**: Uses Table-per-Type inheritance to model complex hierarchies of machines, PCs, and components.
* **Optimized Data Layer**: Employs LINQ projections to reduce multi-gigabyte data sets into lightweight (KB-scale) DTOs.
* **Security**: Multi-tenant isolation and role-based access control via Better-Auth.

### 3. The Lens (The Dashboard)
A Nuxt 3 / Vue frontend providing the primary user interface.
* **Spatial Engine**: Integrates DXF/CAD layouts for real-time asset topography.
* **Omni-Search**: A unified search interface for rapid infrastructure discovery across all layers.
* **Responsive Design**: Industrial-grade UI with zero focus-hijacking and high-concurrency optimizations.

## Data Flow
1. **Agent** pushes telemetry to **Core**.
2. **Core** aggregates and stores data in **PostgreSQL**.
3. **Lens** queries **Core** via optimized DTOs for visualization.
4. **Admins** issue commands via **Lens**, which are queued in **Core** and polled by **Agent**.