> [!NOTE]
> **Heimdall** is an industrial management system developed as a Bachelor of Science (BSc) Thesis project at the University of Pécs, Faculty of Engineering and Information Technology (PTE MIK).

# Heimdall - Industrial Management & Telemetry System

Heimdall is a multi-tenant industrial asset management, configuration tracking, and real-time monitoring platform tailored for manufacturing environments. It bridges the gap between **Information Technology (IT)** and **Operational Technology (OT)** by tracking client PCs, Industrial PCs (IPCs), Soft-PLCs (Beckhoff TwinCAT), hardware PLCs, robot controllers, specialized autonomous equipment, software assets, and spatial factory layouts.

---

## Technical Highlights

- **Backend:** .NET 10 Web API & gRPC Collector Service (.NET 10 / C# 14).
- **Data Access & Schema Strategy:** Entity Framework Core 10 with Npgsql, PostgreSQL 18 JSONB columns, GIN indexing, and dual-schema separation (`auth` for Better-Auth, `backend` for Heimdall domain).
- **Graph-Relational Domain Model:** Flexible many-to-many relationship topology linking Production Stations with one or more controlling IPCs/PLCs and equipment interconnects.
- **Frontend:** Nuxt 4 (Nuxt 4 Directory Structure), Vue 3, Tailwind CSS v4, shadcn-vue, and Vitest.
- **Client Offloading:** Server-side aggregation, filtering, and caching via Nuxt Nitro BFF (Backend-for-Frontend) routes.
- **OT Integrations:** Copia Automation Git version control integration blueprint and native OPC UA Server API (`opc.tcp://`).
- **Security & Compliance:** TISAX (VDA ISA 6.0 / High Protection) compliance framework, multi-tenant isolation, cryptographic command signing, and mTLS support.
- **Hardware Telemetry:** Deep Windows System API / WMI / SetupAPI P/Invoke driver diagnostics (e.g., Beckhoff Real-Time Ethernet NIC driver detection and TwinCAT ADS state).

---

## Repository Structure

```
heimdall/
├── agent/                   # .NET 10 Worker Service Daemon (References App.Contracts only)
├── backend/                 # Backend API and Infrastructure layers
│   ├── App.Backend.Api/     # ASP.NET Core Web API Controllers & gRPC Endpoints
│   └── App.Infrastructure/  # Data Repositories & External Service Integrations
├── docs/                    # Structured Documentation Hub
│   ├── architecture/        # System, Agent, Encryption, Compliance, Protocols
│   ├── api/                 # API Reference, Contracts, Recipe Specification
│   ├── guide/               # Developer Guide, User Guide
│   └── plc/                 # TwinCAT 3 POU and Interface specifications
├── frontend/
│   └── web/                 # Nuxt 4 Web Dashboard & Nitro BFF
├── infra/
│   └── database/            # PostgreSQL 18 Docker Compose, SSL, Init Scripts
├── seed_data/               # Unified Seed Pipeline (CSV, SQL, Validator)
├── shared/
│   ├── App.Contracts/       # Protobuf definitions, gRPC types, PLC TypeSystem (NO DB)
│   └── App.Shared/          # Domain Entities, EF Core DbContext, Migrations
├── simulators/
│   └── fleet/               # Multi-device industrial edge telemetry simulator
├── tools/
│   ├── cad/                 # Synthetic DXF CAD floor plan generator
│   ├── completions/         # Shell completions (bash/zsh)
│   └── dev_manager.py       # Unified development service orchestrator & monitor
├── tests/                   # Unified Verification Suites
│   ├── backend/             # xUnit integration & unit tests (60 tests)
│   ├── frontend/unit/       # Vitest unit tests (13 suites, 67 tests)
│   └── e2e/                 # Playwright browser automation tests (14 tests)
├── run_dev.sh               # Local development environment launcher script
└── run_simulators.sh        # Multi-client fleet simulator orchestrator
```

---

## Domain Architecture Overview

```mermaid
graph TD
    subgraph OT_Layer ["OT Factory Floor"]
        Station["Production Station / Machine"]
        IPC["Industrial PC (IPC) / Soft-PLC"]
        PLC["Hardware PLC / Robot Controller"]
        Dispenser["Autonomous Device (Dispenser/Vision)"]
    end

    subgraph Edge_Telemetry ["Edge Telemetry Layer"]
        Agent["Industrial Edge Daemon"]
        Simulators["Edge Fleet Simulators"]
    end

    subgraph Management_Layer ["Heimdall Core Platform"]
        gRPC["gRPC Telemetry Server"]
        WebAPI["Core Web API"]
        OpcUa["OPC UA Gateway (opc.tcp)"]
        Postgres[(PostgreSQL Database)]
        NitroBFF["Nitro BFF Proxy"]
        WebDashboard["Web Dashboard"]
    end

    subgraph Integrations ["External Integrations"]
        Copia["Copia Automation (Git PLC Versioning)"]
        SCADA["SCADA / Ignition / Kepware"]
    end

    Station <-->|Control Relationship M:N| IPC
    Station <-->|Control Relationship M:N| PLC
    IPC <-->|Fieldbus EtherCAT/PROFINET| Dispenser
    
    Agent -->|gRPC / mTLS| gRPC
    Simulators -->|gRPC| gRPC
    
    gRPC --> WebAPI
    WebAPI <--> Postgres
    WebAPI <--> Copia
    OpcUa <--> WebAPI
    OpcUa -->|OPC UA Subscriptions| SCADA
    
    NitroBFF <--> WebAPI
    WebDashboard <--> NitroBFF
```

---

## Quickstart & Local Development

### Prerequisites

Ensure you have the following installed on your machine:
- **.NET 9 SDK** (`dotnet`)
- **Node.js** (v20+) & **Bun** (`bun`)
- **Python 3** (with `venv`)
- **Docker & Docker Compose**

---

### 1. Database Infrastructure Setup

Navigate to `infra/database`:

```bash
cd infra/database
mkdir -p data logs certs secrets init
echo "postgres" > secrets/pg_user.txt
echo "supersecret" > secrets/pg_pw.txt

# Generate SSL certificates for local HTTPS Postgres
openssl req -new -x509 -days 365 -nodes -text -out certs/server.crt \
  -keyout certs/server.key -subj "/CN=localhost"

# Enforce strict file permissions required by PostgreSQL
chmod 600 certs/server.key

# Start PostgreSQL 17 container
docker compose up -d
cd ../..
```

---

### 2. Database Migrations & Local Tooling

Restore local .NET tools and run database migrations:

```bash
# Restore local EF Core CLI tool
dotnet tool restore

# Run EF Core backend migrations
dotnet ef database update --project shared/App.Shared --startup-project backend/App.Backend.Api

# Prepare Nuxt 4 frontend types
cd frontend/web
bun run postinstall
cd ../..
```

---

### 3. Python Simulator Setup

Initialize the Python virtual environment and install simulator requirements:

```bash
# Create virtual environment
python3 -m venv venv

# Activate and install dependencies
source venv/bin/activate
pip install -r simulators/fleet/requirements.txt
deactivate

# Generate synthetic DXF factory layout
venv/bin/python tools/cad/generate_plant_dxf.py
```

---

### 4. Running the Development Suite

You can start and manage all services concurrently using `run_dev.sh`:

```bash
# Start all Heimdall services
./run_dev.sh start

# Stream logs for a specific service (backend, frontend, agent, simulator, db)
./run_dev.sh logs backend

# Restart a specific service
./run_dev.sh restart frontend

# Check service health and port matrix
./run_dev.sh status

# Stop all Heimdall services
./run_dev.sh stop
```

---

### 5. Shell Auto-Completion Setup

Heimdall includes comprehensive tab-completion for `run_dev.sh`, `run_simulators.sh`, `tools/dev_manager.py`, `seed_data/seed_pipeline.py`, and `simulators/fleet/fleet_simulator.py`.

```bash
# 1-Click Installation (Auto-detects Bash/Zsh and adds to ~/.bashrc or ~/.zshrc)
./tools/completions/install_completions.sh

# Or activate immediately in your current session:
source <(./run_dev.sh completion bash)   # for Bash
source <(./run_dev.sh completion zsh)    # for Zsh
```

---

## Running Verification Tests

```bash
# 1. Run .NET backend unit tests (xUnit)
dotnet test Heimdall.sln

# 2. Run Nuxt frontend unit tests (Vitest)
bun --cwd frontend/web run test
```

---

## Security & Compliance (TISAX)

Heimdall is designed in accordance with **VDA ISA 6.0 (TISAX High Protection Needs)** guidelines:
- Multi-tenant data segregation with EF Core Global Query Filters (`OrganizationId`).
- Role-Based Access Control (RBAC) powered by Better-Auth (`auth` schema).
- Immutable system and security audit event logging (`agent_events`).
- Cryptographically signed agent command payloads (`Signature` ECDSA/RSA validation).
- Secret management via file-based secrets (`infra/database/secrets`), zero plain-text passwords in code.

---

## Documentation Index

Comprehensive technical documentation is maintained in the [`docs/`](docs/Home.md) directory:

- [Documentation Index](docs/Home.md) - Master documentation directory and overview.
- [System Architecture & Data Model](docs/architecture/SYSTEM_ARCHITECTURE.md) - Graph-relational $M:N$ domain model, database schemas, and caching architecture.
- [Edge Agent & Protocols](docs/architecture/EDGE_AGENT_AND_PROTOCOLS.md) - Edge daemon lifecycle, DAG recipe merger, 4-tier bandwidth throttling, offline SQLite spooling, and industrial protocol drivers (TwinCAT ADS, EtherCAT, OPC UA, Modbus TCP).
- [Security, Encryption & Compliance](docs/architecture/SECURITY_AND_COMPLIANCE.md) - AES-256-GCM field-level encryption, signed remote command execution, PII exclusion engine, and TISAX (VDA ISA 6.0) / GDPR mappings.
- [Frontend, Real-Time PWA & Spatial UI](docs/architecture/FRONTEND_AND_PWA.md) - Nuxt architecture, Nitro BFF proxying, SignalR live Kanban ticketing, offline IndexedDB sync, AutoCAD DXF floor plan engine, and dynamic asset templating.
- [API & Interface Reference](docs/api/API_REFERENCE.md) - Complete REST API endpoints, gRPC telemetry collector protobufs, SignalR hub events, DTOs, and OPC UA address space.
- [Developer & Operations Guide](docs/guide/DEV_GUIDE.md) - Local setup, environment prerequisites, building, testing, database migrations, and Docker workflows.
- [User & Operator Guide](docs/guide/USER_GUIDE.md) - Plant floor operator guide for dashboard monitoring, spatial CAD inspection, and mobile maintenance ticketing.

---

## License & Commercial Licensing

Heimdall is dual-licensed:

- **Free & Open Source Community Edition:** [GNU Affero General Public License v3.0 (AGPL-3.0-or-later)](LICENSE). Free for individual engineers, researchers, and open-source contributors. In accordance with Section 13 of the AGPLv3, if you run a modified version of Heimdall across a network, the corresponding source code must be made available under the AGPLv3.
- **Commercial Enterprise License:** For industrial enterprises, manufacturing OEMs, and organizations that require deploying Heimdall on factory floor networks without AGPL-3.0 copyleft obligations, or who require closed-source proprietary extensions, dedicated support SLAs, and warranty indemnification, a Commercial Enterprise License is available. Inquire via the repository maintainer.


