# Heimdall Developer & Operations Guide

This guide provides practical instructions for setting up, developing, testing, and deploying Heimdall.

---

## 1. Monorepo Structure

```
heimdall/
├── agent/                   # Industrial Edge Agent daemon
│   └── App.Agent.Daemon/    # Worker service (gRPC telemetry, spooler, protocol drivers)
├── backend/                 # Backend API and services
│   ├── App.Backend.Api/     # ASP.NET Core API controllers, gRPC collector, SignalR hubs
│   └── App.Infrastructure/  # Repositories, Redis/Memory caching, external integrations
├── fixtures/                # Canonical enterprise plant dataset (JSON)
├── shared/                  # Shared libraries
│   ├── App.Contracts/       # Protobuf definitions, DTOs, PLC type sanitizer (No DB dependency)
│   └── App.Shared/          # Domain entities, AppDbContext, EF Core migrations
├── frontend/
│   └── web/                 # Nuxt 4 web dashboard & Nitro BFF proxy
├── simulators/
│   ├── active_directory/    # Standalone mock Active Directory / Graph server (port 5088)
│   └── fleet/               # Edge fleet simulator, mock CMI runner & simulated PC Docker containers
├── infra/
│   └── database/            # PostgreSQL 18, Redis 7.4, SSL certs, and seed data
├── tests/
│   ├── backend/             # xUnit backend integration tests (67 tests)
│   ├── frontend/unit/       # Vitest unit test suites (18 suites, 126 tests)
│   └── e2e/                 # Playwright browser end-to-end tests
├── docker-compose.yml       # Full stack local development compose file
├── run_dev.sh               # Local development launch script
└── run_simulators.sh        # Simulator runner script
```

---

## 2. Environment Prerequisites

Ensure the following tools are installed on your workstation:
* **.NET SDK** (version `10.0+`)
* **Bun** (version `1.2+`)
* **Python 3** (version `3.11+`) with `grpcio` and `protobuf`
* **Docker** and **Docker Compose** (v2 plugin)

---

## 3. Local Development Setup

### 3.1 Starting Infrastructure Services
Start the PostgreSQL database and Redis cache:
```bash
docker compose up -d postgres redis
```
Wait for both services to report `healthy`:
```bash
docker compose ps
```

### 3.2 Applying Database Migrations
Migrations are stored in `shared/App.Shared/Migrations`:
```bash
dotnet ef database update \
  --project shared/App.Shared \
  --startup-project backend/App.Backend.Api
```

To create a new migration after modifying domain models:
```bash
dotnet ef migrations add <MigrationName> \
  --project shared/App.Shared \
  --startup-project backend/App.Backend.Api \
  --output-dir Migrations
```

### 3.3 Running the Backend API
```bash
cd backend/App.Backend.Api
dotnet run
```
* **REST API & Swagger UI**: `http://localhost:5099/swagger` (also redirects from `/api-docs` and `/`)
* **Cleartext gRPC Endpoint**: `http://localhost:5001`
* **SignalR WebSocket Hub**: `http://localhost:5099/hubs/maintenance`

### 3.4 Running the Frontend Dashboard
```bash
cd frontend/web
bun install
bun run dev
```
Access the application at `http://localhost:3000`.

### 3.5 Running the Edge Fleet Simulator
To simulate edge controllers generating real-time telemetry:
```bash
# Smoke test (5 heartbeats)
python3 simulators/fleet/fleet_simulator.py --client ROBOT-CELL-01 --smoke-test --count 5

# Continuous background simulation
python3 simulators/fleet/fleet_simulator.py --client ROBOT-CELL-01 --count 100
```

---

## 4. Running With Docker Compose

The complete 7-service ecosystem can be executed inside container networks:
```bash
# Build all container images
docker compose build

# Start the full stack in background
docker compose up -d

# Inspect service statuses
docker compose ps

# View service logs
docker compose logs -f backend simulator agent

# Tear down the stack
docker compose down
```

### Service Port Allocations:
| Service | Container Name | Port Mapping | Purpose |
| :--- | :--- | :--- | :--- |
| **Frontend** | `heimdall_frontend` | `3000:3000` | Nuxt web dashboard & Nitro BFF |
| **Backend API** | `heimdall_backend` | `5099:5099` | REST API, Swagger, SignalR |
| **Backend gRPC**| `heimdall_backend` | `5001:5001` | Edge telemetry collector |
| **PostgreSQL** | `heimdall_postgres`| `5432:5432` | Primary database with SSL |
| **Redis** | `heimdall_redis` | `6379:6379` | L2 distributed cache |
| **pgAdmin** | `heimdall_pgadmin` | `5050:80` | Web database management |
| **Agent** | `heimdall_agent` | `5998` (internal)| Local edge daemon |
| **Simulator** | `heimdall_simulator`| *(Internal)* | Simulated factory node |

---

## 5. Automated Test Verification

### 5.1 Backend & Agent Unit Tests (xUnit)
```bash
dotnet test ./tests/backend/App.Backend.Tests/App.Backend.Tests.csproj
```
Executes 67 tests covering multi-tenancy global query filters, MFA policy rules, Active Directory OU synchronization, entity inheritance, AES-256-GCM encryption roundtrips, PII exclusion rules, and gRPC endpoints.

### 5.2 Frontend Unit Tests (Vitest)
```bash
cd frontend/web
bun run test:unit
```
Executes 18 test suites (126 tests) covering the 8-stage Kanban lifecycle, error template catalog, technician delegation inheritance, zero-dependency SVG QR generation, Better-Auth security group org mapping, and mock Active Directory endpoints.

### 5.3 Python Fleet Simulator & Mock CMI Tests
```bash
./venv/bin/python3 -m unittest discover -s simulators/fleet -p "test_*.py"
```
Executes 9 unit tests verifying `MockCmiEngine` queries (`wmic`, `Get-CimInstance`) and `SimulatedPcDaemon` edge diagnostics and telemetry generation.

### 5.4 End-to-End Browser Tests (Playwright)
```bash
cd frontend/web
bun x playwright test
```
Executes end-to-end browser automation validating MFA policy enforcement, Active Directory host discovery, and security group organization provisioning.
