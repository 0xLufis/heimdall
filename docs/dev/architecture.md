# Heimdall Architecture & Development Guide

## Overview
Heimdall is a web-based dashboard and system monitoring tool designed to manage industrial automation networks, similar to SolarWinds but specifically tailored for manufacturing plant layouts. 

The system provides real-time monitoring of Client PCs, interactive floor plan mapping (via DXF to SVG conversion), and a comprehensive inventory management system for industrial components (Sensors, Industrial Robots, Controllers, etc.).

## Technology Stack
- **Frontend**: Nuxt.js (Vue 3), TailwindCSS, Shadcn Vue
- **Backend**: .NET 9 Web API (C#)
- **Agent**: .NET 9 Worker Service (C#)
- **Database**: PostgreSQL 17.9 with Entity Framework Core
- **Authentication**: Better-Auth (Frontend session management verified by Backend API)
- **Communication**: REST API (Frontend to Backend) and gRPC (Agent to Backend)

## Architecture Flow
1. **Agents (Client PCs)** run as background services, gathering system information (WMI, Network interfaces) and transmitting it via gRPC to the Backend.
2. **Backend API** receives gRPC reports, validates them, and upserts the data into PostgreSQL. It also exposes a RESTful API for the Nuxt frontend.
3. **Frontend Dashboard** consumes the REST API (via a Nitro proxy server to handle authentication transparently) to display live data, manage inventory, and interact with the Plant Map.

## Data Model (Entity Framework Core)

### Core Entities
*   **Machine**: Represents a logical "Station" on the assembly line (e.g., `Line-1-Station-A`). 
    *   Has a `PinnedObjectHandle` linking it to a physical geometry on the DXF map.
*   **ClientPc**: Represents a physical computer/IPC on the network.
    *   Many-to-Many relationship with `Machine`. One IPC can control multiple stations (e.g. motion and vision), or one station can have multiple IPCs.
*   **HardwareComponent / SoftwareComponent**: Detailed inventory tracking.
    *   Uses strongly-typed `jsonb` columns (`ComponentTechnicalSpecs`) to allow flexible, component-specific attributes without massive schema migrations. For example, storing `PayloadCapacityKg` for Industrial Robots, or `SensingDistance` for Proximity Sensors.
*   **Manufacturer / Supplier**: Normalized tables linking to components.

## Development Workflow
To start the entire stack locally, execute the script at the root of the repository:
```bash
./run_dev.sh
```
This handles Docker Compose (PostgreSQL), Nuxt (Bun), the .NET API, and a mock local Agent.

### Managing Migrations
To add new database models:
1. Update `shared/App.Shared/Entities.cs` and `AppDbContext.cs`.
2. Run EF Core migrations from the root:
```bash
dotnet ef migrations add <MigrationName> --project shared/App.Shared/App.Shared.csproj
dotnet ef database update --project shared/App.Shared/App.Shared.csproj
```
