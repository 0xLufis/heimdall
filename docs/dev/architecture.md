# Heimdall Architecture & Development Guide

## Overview
Heimdall is a web-based dashboard and system monitoring tool designed to manage industrial automation networks, similar to SolarWinds but specifically tailored for manufacturing plant layouts. 

The system provides real-time monitoring of Client PCs, interactive floor plan mapping (via DXF to SVG conversion), and a comprehensive inventory management system for industrial components (Sensors, Industrial Robots, Controllers, etc.).

## Technology Stack
- **Frontend**: Nuxt.js (Vue 3), Shadcn-Vue, Tailwind CSS 4, Better-Auth (Nuxt Module)
- **Backend**: .NET 9 Web API (C#)
- **Agent**: .NET 9 Worker Service (C#)
- **Database**: PostgreSQL 17.9 with Entity Framework Core
- **Authentication**: Better-Auth (Frontend session management verified by Backend API using JWT/Cookie)
- **Communication**: REST API (Frontend to Backend) and gRPC (Agent to Backend, Backend to Frontend for real-time updates)

## Architecture Flow
1. **Agents (Client PCs)** run as background services, gathering system information (WMI, Network interfaces) and transmitting it via gRPC to the Backend.
2. **Backend API** receives gRPC reports, validates them, and upserts the data into PostgreSQL. It also exposes a RESTful API for the Nuxt frontend, secured by Better-Auth.
3. **Frontend Dashboard** consumes the REST API (via a Nitro proxy server to handle authentication transparently) to display live data, manage inventory, and interact with the Plant Map. Real-time updates may be pushed via gRPC from Backend to Frontend.

## Data Model (Entity Framework Core)

### Core Entities
*   **Machine**: Represents a logical "Station" on the assembly line (e.g., `Line-1-Station-A`). 
    *   Has a `PinnedObjectHandle` linking it to a physical geometry on the DXF map.
*   **ClientPc**: Represents a physical computer/IPC on the network.
    *   Many-to-Many relationship with `Machine`. One IPC can control multiple stations (e.g. motion and vision), or one station can have multiple IPCs.
*   **HardwareComponent / SoftwareComponent**: Detailed inventory tracking.
    *   Uses strongly-typed `jsonb` columns (`ComponentTechnicalSpecs`) to allow flexible, component-specific attributes without massive schema migrations. For example, storing `PayloadCapacityKg` for Industrial Robots, or `SensingDistance` for Proximity Sensors.
*   **Manufacturer / Supplier**: Normalized tables linking to components.
*   **AuthUser / AuthSession**: Entities managed by the Better-Auth library, representing user accounts and active sessions.

## Development Workflow
To start the entire stack locally, execute the script at the root of the repository:
```bash
./run_dev.sh
```
This handles Docker Compose (PostgreSQL), Nuxt (Bun), the .NET API, and a mock local Agent.

### Managing Database Migrations
To add new database models or modify existing ones:
1. Update `shared/App.Shared/Entities.cs` and `AppDbContext.cs`.
2. Run EF Core migrations from the root:
```bash
dotnet ef migrations add <MigrationName> --project shared/App.Shared/App.Shared.csproj
dotnet ef database update --project shared/App.Shared/App.Shared.csproj
```

### Backend Testing (xUnit)
Backend tests are implemented using xUnit and `Microsoft.AspNetCore.Mvc.Testing` for integration tests.

*   **Project Location:** `tests/backend/App.Backend.Tests/`
*   **Running Tests:** From the project root, execute `dotnet test tests/backend/App.Backend.Tests/App.Backend.Tests.csproj`
*   **In-Memory Database:** For integration tests, the `CustomWebApplicationFactory` (defined in `GrpcCommsTests.cs`) is used to override `AppDbContext` and configure an in-memory database. This ensures tests are isolated and fast, without affecting the real database.
*   **gRPC Service Testing:** gRPC services are tested by creating a `GrpcChannel` that targets the test server's base address, allowing direct interaction with the gRPC endpoints within the test environment.
*   **Resolving gRPC Types:** Protobuf definitions are centralized in `shared/App.Shared/App.Shared.csproj` using `GrpcServices="Both"`. Other projects (`App.Backend.Api`, `App.Backend.Tests`) then reference `App.Shared` to access the generated gRPC types, avoiding type conflicts (`CS0436`).

### Frontend Testing (Vitest - *currently deferred*)
Frontend tests are intended to be implemented using Vitest. Due to ongoing environmental configuration challenges, this task is currently deferred.

*   **Framework**: Vitest with `jsdom` environment.
*   **Test Files**: Located in `tests/frontend/tests/`.
*   **Running Tests**: (To be re-evaluated)

### Generating Documentation
*   **JSDoc (Frontend)**: Generated automatically from JSDoc comments within `.ts` and `.vue` files.
*   **XML Documentation (Backend)**: Generated automatically from XML documentation comments (`///`) within C# files.
