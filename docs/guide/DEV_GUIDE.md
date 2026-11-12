# Heimdall Developer & Operations Guide

This guide provides practical instructions for setting up, developing, testing, and deploying Heimdall.

---

## 1. Monorepo Structure

```
heimdall/
├── src/
│   ├── agent/                   # Industrial Edge Agent daemon
│   │   └── App.Agent.Daemon/    # Worker service (MQTT telemetry, spooler, protocol drivers)
│   ├── backend/                 # Backend API and services
│   │   ├── App.Backend.Api/     # ASP.NET Core API controllers, MQTT ingestion, SignalR hubs
│   │   └── App.Infrastructure/  # Repositories, Redis/Memory caching, external integrations
│   ├── frontend/
│   │   └── web/                 # Nuxt 4 web dashboard & Nitro BFF proxy
│   └── shared/                  # Shared libraries
│       ├── App.Contracts/       # Protobuf definitions, DTOs, PLC type sanitizer (No DB dependency)
│       └── App.Shared/          # Domain entities, AppDbContext, EF Core migrations
├── fixtures/                # Canonical enterprise plant dataset (JSON)
├── simulators/
│   ├── active_directory/    # Standalone mock Active Directory / Graph server (port 5088)
│   └── fleet/               # Edge fleet simulator, mock CMI runner & simulated PC Docker containers
├── infra/
│   └── database/            # PostgreSQL 18, Redis 7.4, SSL certs, and seed data
├── tests/
│   ├── backend/             # xUnit backend integration tests (229 tests)
│   ├── frontend/unit/       # Vitest unit test suites (58 suites, 565 tests)
│   └── e2e/                 # Playwright browser end-to-end tests
├── docker-compose.yml       # Full stack local development compose file
└── justfile                 # Modern command runner for dev, staging, testing, and production
```

---

## 2. Environment Prerequisites

Ensure the following tools are installed on your workstation:
* **.NET SDK** (version `10.0+`)
* **Bun** (version `1.2+`)
* **Python 3** (version `3.12+`) with `protobuf`
* **just** command runner and **OpenSSL**
* **Docker** and **Docker Compose** (v2 plugin)

---

## 3. Local Development Setup

### 3.1 Starting Infrastructure Services
The development PostgreSQL and Redis containers are defined in `infra/database/docker-compose.yml`. On first run, create the secrets and TLS certificate (see the README Quickstart), then start them:
```bash
just db-up
```
`just db-up` starts both containers and waits until ports `5432` and `6379` accept connections. Alternatively, `just dev-setup` checks prerequisites, starts the containers, applies migrations, and seeds the enterprise dataset in one step.

### 3.2 Applying Database Migrations
Migrations are stored in `src/shared/App.Shared/Migrations`:
```bash
dotnet ef database update \
  --project src/shared/App.Shared \
  --startup-project src/backend/App.Backend.Api
```

To create a new migration after modifying domain models:
```bash
dotnet ef migrations add <MigrationName> \
  --project src/shared/App.Shared \
  --startup-project src/backend/App.Backend.Api \
  --output-dir Migrations
```

### 3.3 Running the Backend API
```bash
just start backend      # background: dotnet watch, rebuilds and restarts the backend on every saved C# file
just restart backend    # stop + start
just stop backend       # stops the watcher, the app and their build processes
just logs backend       # console output incl. build errors (.logs/backend.log)

# or in the foreground, without file watching
cd src/backend/App.Backend.Api && dotnet run
```
`just start backend` runs `dotnet watch --no-hot-reload`: every save rebuilds and restarts the app (about
13-18 s until Swagger and the MQTT broker answer again). Hot reload is off on purpose: with SDK 10.0.113 C# edits
crashed the watcher in Roslyn's edit-and-continue, after which nothing restarted. The watcher waits for the old
instance to exit before it starts the new one (`DOTNET_WATCH_PROCESS_CLEANUP_TIMEOUT_MS` in the justfile), and in
Development the backend shuts down within 5 s and waits up to 10 s at startup for 5099/5001/1883 to be released.
A failed build leaves the watcher waiting for the next save.

`just stop <service>` stops exactly what `just start <service>` launched: every process of the session that
`setsid` created for it (PID file in `/tmp/heimdall_dev_pids/`), then whatever still listens on the service's
ports (for the backend 5099, 5001, 1883), one process at a time (`tools/devproc.py`). It no longer matches
command lines, so shells, builds and tests that mention the project are left alone. `just start backend` refuses
to start while another process holds one of the ports and names it.

* **REST API & Swagger UI**: `http://localhost:5099/swagger` (also redirects from `/api-docs` and `/`). Served only when dev features are on (`ASPNETCORE_ENVIRONMENT=Development` or `HEIMDALL_ENABLE_DEV=true`). In Development an unauthenticated request runs as the dev administrator, so "Try it out" works without a token; otherwise paste a Better-Auth session token under **Authorize** (no `Bearer ` prefix).
* **Secondary cleartext HTTP port**: `http://localhost:5001` (same API; there is no gRPC service, agents and simulators report over MQTT)
* **Embedded MQTT Broker**: `localhost:1883` (device topics `heimdall/devices/{machineIdentifier}/system_info|plc_memory|telemetry|events`)
* **SignalR WebSocket Hub**: `http://localhost:5099/hubs/maintenance`

### 3.4 Running the Frontend Dashboard
```bash
cd src/frontend/web
bun install
bun run dev
```
Access the application at `http://localhost:3000`.

`just start frontend` (and `just dev`) additionally exports the dev feature flags from the repository's
`.env.dev`: `HEIMDALL_ENABLE_DEV`, `HEIMDALL_ENABLE_DEBUG`, `HEIMDALL_DEBUG`, `NUXT_PUBLIC_ENABLE_DEV_FEATURES`,
`NUXT_PUBLIC_ENABLE_DEBUG_FEATURES` and `ENABLE_DEV_HTTP_SEED`. These enable the dev conveniences (the BFF's
anonymous "System Administrator" fallback, the demo kiosk tokens and the kiosk terminal's dev auth bypass, the
debug features). Only these keys are loaded. The rest of `.env.dev` is for the backend and docker: its
`DATABASE_URL` is the backend's database role, and its `BETTER_AUTH_SECRET` differs from the frontend's dev
default, so it would invalidate existing sessions and MFA secrets. A variable that is already set wins, so
`HEIMDALL_ENABLE_DEV=false just start frontend` starts without the dev shortcuts. The dev server listens on
`0.0.0.0:3000` and the dev backend on `0.0.0.0:5099`, so on a shared network these shortcuts are reachable by
other hosts. Use the switch above or a firewall there. A plain `bun run dev` does not load `.env.dev`.

> [!NOTE]
> On startup, the Nitro server plugin (`server/plugins/ensureAdminUser.ts`) checks if a platform administrator exists. If not, it automatically seeds `admin@heimdall.dev` (password: `AdminPassword123!`). You can also manually trigger this via `GET /api/dev/seed-admin`.

### 3.5 Running the Edge Fleet Simulator
The fleet simulator (`simulators/fleet/fleet_simulator.py`) impersonates the plant's edge IPCs the same way the
real agent talks to the backend: binary protobuf over **MQTT 5** on the backend's embedded broker (`:1883`,
credentials `heimdall_agent` / `HEIMDALL_AGENT_KEY`, user property `x-agent-key`). There is no gRPC ingestion.

| Topic | Message | Effect |
|---|---|---|
| `heimdall/devices/{machineIdentifier}/system_info` | `SystemInfoRequest` | IPC heartbeat: online state, CPU/RAM, disks |
| `heimdall/devices/{machineIdentifier}/plc_memory` | `AdsPlcMemoryBlock` | Station process data: live dynamics + production KPIs (station KPIs, plant-map HUD, `StationOscillationReceived` / `StationTelemetryReceived`, Lua `TelemetryChange`) |

It needs `paho-mqtt` and `protobuf` (`pip install -r simulators/fleet/requirements.txt` into `./venv`; the `just`
recipes use `venv/bin/python` when it exists).

```bash
just sim-reset                       # ONE COMMAND: reseed the enterprise dataset + restart the simulator with plant activity
just sim-reset demo                  # same for the Nordvik NV1 demo plant (demo-seed --unpin-conflicts, simulator on "all")
just sim-rate 0.3 10                 # 0.3 failures per station-hour, 10x speed (defaults: 0.05, 1x)
just start simulator                 # background, enterprise dataset (SIM_DATASET=enterprise|demo|all, SIM_STATION_INTERVAL=4)
just sim-demo                        # foreground, Nordvik NV1 demo dataset (after `just demo-seed`)
just sim-demo --scenario demo_story --speed 10 --seed 7
just test-smoke                      # one round of heartbeats + station blocks, needs the backend running
just test-simulator                  # unit tests, no backend needed

# Direct invocation
venv/bin/python simulators/fleet/fleet_simulator.py --dataset demo --broker 127.0.0.1:1883 --station-interval 2
```

Only simulate a dataset that is seeded (`just db-seed` → `enterprise`, `just demo-seed` → `demo`): the backend
auto-registers unknown controllers that report in, and station blocks are only attributed to stations that are
linked to the reporting controller in the database.

**Station model.** Every controller → station link of the dataset gets a process model
(`station_model.py`) with machine-type specific operating points (CNC, laser welder, robot cell, conveyor, …):
vibration, spindle speed, cycle time, power, temperature, frequency, availability / performance / quality,
parts per hour, part and scrap counters and a state machine (`RUNNING`, `IDLE`, `WARNING`, `FAULTED`,
`MAINTENANCE`). WARNING / FAULTED follow from thresholds on the simulated signals; a FAULTED station carries an
alarm code, a stopped station reports no cycle time, zero speed and constant readings (identical PLC blocks, which the
backend forwards only as 10 s keep-alives), sub-machines stop with their cell. The model advances in fixed steps, so
`--seed N` reproduces the same run. `plc_encoding.py` emits only PLC field names the backend's
`StationTelemetryExtractor` recognises. Every seeded station is simulated and reported by its *Primary* controller;
every seeded IPC sends heartbeats whose load and status follow the stations it runs.

**Plant activity** (`--activity`, default for continuous runs; `--no-activity` / `SIM_ACTIVITY=0` turns it off).
`maintenance.py` turns faults into maintenance work in simulated time (deterministic per seed): failures arrive at
`--incident-rate` per station-hour (some through drifts, e.g. laser power, bearing wear), and so do faults injected via the
control API. `backend_client.py` performs the work through the real REST API:

| Step | Endpoint |
|---|---|
| alarm raises a ticket (assignee from the delegation rules) | `POST /api/v1/tickets/machine-raised` (anonymous machine endpoint) |
| crew technician takes it / arrives | `POST /api/v1/tickets/{id}/reserve`, `POST …/qr-pickup` (station goes to MAINTENANCE) |
| part not in the store | `PATCH …/pending` (Parts), later `PATCH …/status "InProgress"` |
| serialized spare into the component slot | `POST /api/v1/component/assign-part` (part history + service log) |
| consumable used / goods receipt | `PUT /api/v1/inventory/{id}` `{stockQuantity}` |
| long repair | `POST …/escalate`, `POST …/deescalate` |
| repaired, then verified | `PATCH …/status "Resolved"`, `POST …/auto-close` |

Lifecycle calls need a maintenance role: a Development backend accepts unauthenticated calls as the dev admin,
otherwise pass a session token (`--api-token` / `HEIMDALL_SIM_API_TOKEN`). The REST worker queues and retries while the
backend restarts; MQTT reconnects on its own. Progress: `GET /api/status` → `maintenance`.

**Control API** (`http://127.0.0.1:5055`, loopback only unless `HTTP_HOST` is set; no authentication, dev only).
The web UI reaches it through the BFF proxy `/api/simulator/*` (`SIMULATOR_HOST`, simulation feature flag).

| Method / path | Body | Purpose |
|---|---|---|
| `GET /api/status` | – | fleet counters, MQTT link state, lines, nodes, stations (state, scenario, current values) |
| `GET /api/stations` | – | stations only |
| `POST /api/station/fault` | `{station, duration_s?}` | fault a station (latched until recover when no duration) |
| `POST /api/station/drift` | `{station, profile, duration_s?, magnitude?}` | `laser_drift`, `bearing_wear`, `thermal`, `cycle_slowdown` |
| `POST /api/station/maintenance` | `{station, duration_s?}` | put a station into maintenance |
| `POST /api/line/stop` | `{line, duration_s?}` | stop every station of a line (`L01`, …) |
| `POST /api/recover` | `{station?}` / `{line?}` / `{}` | clear scenarios for a station, a line or everything |
| `POST /api/speed` | `{speed}` | simulated seconds per second (0.1 – 120) |
| `POST /api/scenario` | `{name}` | `demo_story` (Nordvik laser drift story), `clear` |
| `POST /api/activity` | `{incident_rate}` | failures per station and simulated hour (plant activity) |
| `POST /api/fault` | `{hostname?}` or `{station?, duration_s?}` | IPC heartbeat fault (random node when empty) or station fault |
| `POST /api/clear-faults` | `{}` | clear all IPC and station faults |
| `POST /api/rate` | `{fault_rate}` | random IPC heartbeat anomaly rate (0 – 1) |

`station` accepts the station id, name or floor-plan handle (`NV-L2-OP060`, `L02-OP060`).

### 3.6 Frontend Architecture & Key Utilities
* **HeimdallSearch Engine (`useHeimdallSearch.ts`)**:
  * Composable exported as both `useHeimdallSearch` and `useFmfd`.
  * Provides parameterized search indexing across clients, machines, tickets, and inventory with tag directives (`@`, `#`, `!`), fuzzy matching, and GDPR data diagnostics. UI components include `HeimdallSearchBar.vue` and `GlobalHeimdallSearchModal.vue`.
* **Canonical 5-State Ticketing & Orthogonal Escalation (`types/maintenance.ts`)**:
  * Statuses strictly adhere to 5 canonical states: `Open`, `InProgress`, `Pending`, `Resolved`, and `Closed`.
  * `PendingReason`: Strongly typed to `None | Parts | ExternalOk | Action`, mapping directly to the backend enum.
  * Orthogonal Escalation: Escalation is decoupled from ticket status (`isEscalated`, `escalationReason`, `escalatedAt`, `escalatedBy`, `escalationClosedAt`, `escalationClosedBy`). A ticket can be escalated or resolved in any status.
* **Modular Settings Architecture**:
  * Tabbed subcomponents located in `src/frontend/web/app/components/settings/` (`SettingsProfileTab`, `SettingsRolesTab`, `SettingsSecurityTab`, `SettingsAppearanceTab`, `SettingsPersonasTab`).
* **Drag-and-Drop Reordering (`reorderList.ts`)**:
  * `reorderAndPrioritize<T>(items, fromIndex, toIndex, options)`: Moves items between indices and recalculates sequential 1-based priority fields (`priority: 1, 2, 3...`).
  * `setDragImageAtClickPoint(event, element)`: Ensures HTML5 drag ghost images stay anchored to the exact cursor click point. Incorporates global `pointerdown` tracking to work around the Linux Chromium bug where `DragEvent.clientX/Y` reports `0` during `dragstart`, rendering temporary clones to force Blink to honor custom offsets.
* **Database Migrations vs. SQL Dumps**:
  * `.gitignore` excludes all `*.sql` database dumps to avoid committing large seed files.
  * Schema migrations in `src/shared/App.Shared/Migrations/` (EF Core) and `src/frontend/web/server/db/migrations/` (Drizzle) are strictly whitelisted and tracked in git.

---

### 3.7 Logs, Retention & Verbosity

Dev logs are kept on disk under `<repo>/.logs/` (git-ignored). They are never written to `/tmp`, which is
RAM-backed on dev machines; an unbounded backend log once grew to 4.7 GB there overnight. Every output is
bounded by size and age:

| Output | Location | Bounded by |
|---|---|---|
| Console output of each service started by `just` (backend incl. build errors, frontend, simulator, agent) | `.logs/<service>.log`; the previous run and full files go to `.log.1` (newest) … `.log.3` | `tools/logpipe.py`: 10 MB per file, 3 backups, backups older than 7 days deleted. Override with `HEIMDALL_LOG_MAX_MB`, `HEIMDALL_LOG_BACKUPS` and `HEIMDALL_LOG_MAX_AGE_DAYS` |
| Backend file log (`Logging:File`, enabled in Development) | `.logs/backend/backend.log`; rotated files are `backend-YYYYMMDD-HHMMSS-fff.log` | 20 MB per file and a new file each day; files older than 7 days deleted; at most 200 MB in total (the oldest rotated files are deleted first) |

`just logs backend|frontend|simulator|agent` follows the console logs across rotations (`tail -F`).
`just logs backend-file` follows the backend file log. The TUI reads the same files.

**Backend file log settings** live under `Logging:File` in `appsettings*.json`. Environment variables
override them: `Logging__File__Enabled`, `__Path` (relative paths are resolved against the content root;
Development uses `../../../.logs/backend`, and the dev scripts pass an absolute path), `__MaxFileSizeMB`,
`__MaxTotalSizeMB`, `__MaxAgeDays`, `__QueueCapacity`, `__FlushIntervalSeconds` and `__ShutdownTimeoutSeconds`.
Outside Development the file log is off (containers log to the console). Log calls never wait for the disk.
Entries go into a bounded in-memory queue (20 000 entries by default), and a background thread writes them in
batches and flushes once a second (errors immediately). When the queue is full, the oldest
Trace/Debug/Information entries are dropped first, and once the queue has drained the file gets a
`N log entries dropped` warning. On shutdown the queue is written and flushed within 2 s. The console logger
uses `QueueFullMode: DropWrite`, so a stalled stdout cannot block request threads either.

**Verbosity.** Both outputs follow the standard `Logging:LogLevel` section. By default this is `Information`,
with `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore.Database.Command` (every SQL statement) at
`Warning`, in Development too. Per-output levels go in `Logging:Console:LogLevel` and `Logging:File:LogLevel`.
.NET always prefers a rule from a provider's own section over any global rule. Once you give an output its
own section, list there every category you want to keep quiet.

```bash
# Persistent (restart needed): environment variables or appsettings.Development.json
Logging__LogLevel__Default=Debug                                            # both outputs
Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information  # SQL statements, both outputs
Logging__File__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information  # SQL to the file only
```

**At runtime, no restart (in memory, reset on restart):** use the admin endpoint (`SystemAdministration`
policy; in Development an unauthenticated call runs as the dev administrator):

```bash
# Read the effective levels per output, the configured sections, the overrides and the file-log state
curl -s 'http://localhost:5099/api/v1/admin/logging?category=Microsoft.EntityFrameworkCore.Database.Command'
# Turn SQL logging on for the file only (provider: All | Console | File; level: Trace..None, or Reset)
curl -s -X PUT http://localhost:5099/api/v1/admin/logging/levels -H 'Content-Type: application/json' \
     -d '{"category":"Microsoft.EntityFrameworkCore.Database.Command","level":"Information","provider":"File"}'
# Back to the configured level (one category, or every override when no category is given)
curl -s -X DELETE 'http://localhost:5099/api/v1/admin/logging/levels?category=Microsoft.EntityFrameworkCore.Database.Command'
```

A runtime override is placed above every configuration source, environment variables included. `All` writes
the global key and both per-output keys, so it also wins over an output's own `Default`. Each change is
logged as a warning, including the user who made it.

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
| **Backend (alt HTTP)**| `heimdall_backend` | `5001:5001` | Secondary cleartext API port |
| **Backend MQTT**| `heimdall_backend` | `1883:1883` | Embedded MQTT broker |
| **PostgreSQL** | `heimdall_postgres`| `5432:5432` | Primary database with SSL |
| **Redis** | `heimdall_redis` | `6379:6379` | L2 distributed cache |
| **pgAdmin** | `heimdall_pgadmin` (`pgadmin_heimdall` with `just db-up`) | `127.0.0.1:5050:80` | Web database management, `just pgadmin` |
| **Agent** | `heimdall_agent` | `5998` (internal)| Local edge daemon |
| **Simulator** | `heimdall_simulator`| *(Internal)* | Simulated factory node |

---

## 5. Automated Test Verification

### 5.0 Unified Test Execution via `just`
To run all verification suites in one command:
```bash
just test
```
Executes seed validation (`just test-seed`), backend xUnit tests (`just test-backend`), and frontend Vitest suites (`just test-frontend`).

### 5.1 Backend & Agent Unit Tests (xUnit)
```bash
dotnet test Heimdall.sln
```
Executes 215 tests covering multi-tenancy global query filters, MFA policy rules, Active Directory OU synchronization, entity inheritance, AES-256-GCM encryption roundtrips, PII exclusion rules, predictive maintenance thresholds, MQTT broker communication, centralized cache key generation, and API endpoints.

### 5.2 Frontend Unit Tests (Vitest)
```bash
cd src/frontend/web
bun run test
```
Executes 58 test suites (565 tests) covering rule reordering with drag-and-drop point-of-click anchoring, HeimdallSearch keyboard shortcuts, 5-state ticket lifecycle and orthogonal escalation, error template catalog, technician delegation inheritance, zero-dependency SVG QR generation, Better-Auth security group org mapping, remote controller modal quick view, predictive maintenance metrics, feature flag gating, and all page routing.

### 5.3 Python Fleet Simulator & Mock CMI Tests
```bash
just test-simulator
# or: ./venv/bin/python3 -m unittest discover -s simulators/fleet -p "test_*.py"
python3 -m unittest tests/test_logpipe.py   # dev log pipe: size rotation at line breaks, backups, age limit, --pidfile
```
Executes 48 unit tests (plus `python3 -m unittest discover -s seed_data`, run by `just test-seed`, for the seed datasets): `MockCmiEngine` queries (`wmic`, `Get-CimInstance`), `SimulatedPcDaemon` diagnostics, and the
fleet simulator's station model (determinism, counters, state machine, drift story), PLC block encoding against a
mirror of the backend extractor's alias list, dataset topology, MQTT 5 framing (in-process broker stub) and the
HTTP control API, the maintenance desk (lifecycle order, stock, determinism) and its REST calls against an in-process
HTTP stub. No backend or broker is required.

### 5.4 End-to-End Browser Tests (Playwright)
```bash
cd src/frontend/web
bun x playwright test
```
Executes end-to-end browser automation validating MFA policy enforcement, Active Directory host discovery, and security group organization provisioning.

### 5.5 Sequence Diagram Verification
```bash
python3 tools/generate_sequence_diagrams.py --check
```
Validates that generated sequence diagrams match the C# controllers, the MQTT ingestion code, and SignalR hub code contracts. Run with `--update-docs` to synchronize diagrams across system documentation.
