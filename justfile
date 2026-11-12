# ==============================================================================
# HEIMDALL INDUSTRIAL OT PLATFORM - JUST COMMAND RUNNER
# ==============================================================================
# Modern command orchestration for development, staging, testing, and production.
# Usage:
#   just --list               Show all available recipes
#   just dev                  Start development environment with interactive TUI
#   just tui                  Launch interactive TUI dashboard
#   just test                 Run verification test suites
#   just staging-up           Start staging environment (no dev features, only debug)
#   just test-staging         Run verification tests against staging environment
#   just prod-up              Start production container stack
# ==============================================================================

set shell := ["bash", "-c"]

# Dev logs live on disk (git-ignored), never in /tmp: /tmp is RAM-backed on dev machines. Every service's
# console output goes through tools/logpipe.py (size + age bounded: HEIMDALL_LOG_MAX_MB, HEIMDALL_LOG_BACKUPS,
# HEIMDALL_LOG_MAX_AGE_DAYS); the backend also writes its own rotating log to .logs/backend/ (Logging:File).
LOG_DIR := justfile_directory() + "/.logs"
LOGPIPE := "python3 tools/logpipe.py"
PID_DIR := "/tmp/heimdall_dev_pids"
# Stops services by the session that `setsid` gives each one (PID file) plus the owner of its ports; never by
# command-line text (pkill -f also killed tool shells and running builds). See tools/devproc.py.
DEVPROC := "python3 tools/devproc.py"
# dotnet watch for the .NET services: rebuild + restart on every save. No hot reload: on SDK 10.0.113 C# edits
# crashed the watcher in Roslyn's edit-and-continue (ArgumentException), after which nothing restarted. The
# cleanup timeout makes the watcher wait for the old instance to exit before relaunching (default: SIGKILL right
# after SIGTERM, which orphaned the app behind `dotnet run` with its ports); no MSBuild node reuse, so no idle
# nodes outlive a build or get shared with other builds.
DOTNET_WATCH_ENV := "DOTNET_WATCH_PROCESS_CLEANUP_TIMEOUT_MS=15000 MSBUILDDISABLENODEREUSE=1 DOTNET_WATCH_SUPPRESS_EMOJIS=1"
SHARED_WIN_AGENT_DIR := "infra/windows/shared/agent"
# Python for the simulators: the project venv (paho-mqtt + protobuf from simulators/fleet/requirements.txt) when present
SIM_PYTHON := if path_exists("venv/bin/python") == "true" { "venv/bin/python" } else { "python3" }

# Display available commands
default:
    @just --list

# ------------------------------------------------------------------------------
# Development Environment
# ------------------------------------------------------------------------------

# One-command dev setup: verify prerequisites, check ports, initialize DB and seed enterprise dataset
dev-setup *ARGS:
    @python3 tools/dev_setup.py {{ARGS}}

# Launch optional pgAdmin 4 portal for the local PostgreSQL development instance (actions: up, down, restart, logs, reset)
pgadmin ACTION="up":
    #!/usr/bin/env bash
    set -euo pipefail
    # pgAdmin must join the compose project (= docker network) of the PostgreSQL container that is
    # actually running: `just db-up` uses infra/database, the all-in-docker stack uses the root file.
    if docker ps --format '{{ '{{' }}.Names{{ '}}' }}' 2>/dev/null | grep -qx heimdall_postgres; then
        COMPOSE_DIR="."
    else
        COMPOSE_DIR="infra/database"
    fi
    pg_compose() { (cd "$COMPOSE_DIR" && docker compose --profile pgadmin "$@"); }
    wait_ready() {
        for _ in $(seq 1 60); do
            if curl -fsS -o /dev/null http://127.0.0.1:5050/misc/ping 2>/dev/null; then return 0; fi
            sleep 1
        done
        return 1
    }
    start() {
        if [ "$COMPOSE_DIR" = "infra/database" ]; then just db-up; fi
        pg_compose up -d pgadmin
        if wait_ready; then
            echo "✓ pgAdmin 4 online at http://localhost:5050 (desktop mode: no pgAdmin login)."
            echo "  Server 'Heimdall Development PostgreSQL' is pre-registered (host postgres, user from"
            echo "  infra/database/secrets/pg_user.txt); enter the password from infra/database/secrets/pg_pw.txt."
        else
            echo "✗ pgAdmin did not become ready on http://localhost:5050. Last log lines:" >&2
            pg_compose logs --tail=30 pgadmin >&2 || true
            exit 1
        fi
    }
    case "{{ACTION}}" in
        up|start)
            start
            ;;
        down|stop)
            pg_compose stop pgadmin
            echo "✓ pgAdmin 4 portal stopped."
            ;;
        restart)
            pg_compose stop pgadmin
            start
            ;;
        logs)
            pg_compose logs -f --tail=50 pgadmin
            ;;
        reset)
            # Drops pgAdmin's own state (saved passwords, preferences); servers.json is re-imported on next start.
            pg_compose rm -sf pgadmin
            (cd "$COMPOSE_DIR" && docker volume rm "$(basename "$(pwd)")_pgadmin_data" 2>/dev/null) || true
            echo "✓ pgAdmin state removed. Run 'just pgadmin' to start fresh."
            ;;
        *)
            echo "Available pgadmin actions: up, down, restart, logs, reset"
            ;;
    esac

# Start development stack and launch TUI dashboard (pass --daemon for background)
dev *ARGS:
    #!/usr/bin/env bash
    set -euo pipefail
    mkdir -p {{LOG_DIR}} {{PID_DIR}}
    echo ">> Starting Heimdall Development Environment..."
    just start all
    if [[ " {{ARGS}} " =~ " --daemon " ]] || [[ " {{ARGS}} " =~ " -d " ]]; then
        echo "✓ Heimdall services running in background daemon mode."
        echo "Run 'just tui' to connect interactive monitor or 'just status' for health."
    else
        just tui
    fi

# Launch the interactive full-screen curses TUI dashboard
tui:
    @python3 tools/tui.py

# Display service topology status and latency matrix
status *ARGS:
    @python3 tools/dev_manager.py status {{ARGS}}

# Continuously watch service status in terminal
watch:
    @python3 tools/dev_manager.py watch

# Clean lingering processes, release allocated ports, and clear PID files
clean:
    #!/usr/bin/env bash
    set -euo pipefail
    echo ">> Cleaning Heimdall development processes and ports..."
    # Same targeting as `just stop` (session from the PID file + port owners), with a 2 s grace before SIGKILL.
    {{DEVPROC}} stop "Backend API" --pidfile {{PID_DIR}}/backend.pid --ports 5099,5001,1883 --grace 2
    {{DEVPROC}} stop "Agent Daemon" --pidfile {{PID_DIR}}/agent.pid --ports 5998 --grace 2
    {{DEVPROC}} stop "Nuxt Frontend" --pidfile {{PID_DIR}}/frontend.pid --ports 3000 --grace 2
    {{DEVPROC}} stop "Fleet Simulator" --pidfile {{PID_DIR}}/simulator.pid --ports 5055 --grace 2
    rm -f {{PID_DIR}}/*.pid
    echo "✓ Environment cleared. Ports 5099, 5001, 1883, 3000, 5055, 5998 released (except ones held by containers)."

# Stream service logs (backend, backend-file, frontend, agent, simulator, windows, db)
logs SERVICE="all":
    #!/usr/bin/env bash
    # -F follows the file name across rotations (logpipe / the backend file logger rename the full file).
    case "{{SERVICE}}" in
        backend)
            tail -n 60 -F {{LOG_DIR}}/backend.log
            ;;
        backend-file)
            tail -n 60 -F {{LOG_DIR}}/backend/backend.log
            ;;
        frontend)
            tail -n 60 -F {{LOG_DIR}}/frontend.log
            ;;
        agent)
            tail -n 60 -F {{LOG_DIR}}/agent.log
            ;;
        simulator)
            tail -n 60 -F {{LOG_DIR}}/simulator.log
            ;;
        windows|winagent)
            docker compose --profile windows logs -f --tail=60 windows-agent
            ;;
        db|database|postgres|redis)
            docker compose -f infra/database/docker-compose.yml logs -f --tail=60
            ;;
        all|*)
            echo "Available log targets: backend, backend-file, frontend, agent, simulator, windows, db"
            echo "  • Backend API:     {{LOG_DIR}}/backend.log (console output, incl. build errors)"
            echo "  • Backend file log:{{LOG_DIR}}/backend/backend.log (rotating, see Logging:File)"
            echo "  • Web Frontend:    {{LOG_DIR}}/frontend.log"
            echo "  • Edge Agent:      {{LOG_DIR}}/agent.log"
            echo "  • Simulator:       {{LOG_DIR}}/simulator.log"
            echo "  Rotated copies: <name>.log.1 (newest) .. .log.N; previous run = .log.1"
            ;;
    esac

# ------------------------------------------------------------------------------
# Service Lifecycle Control (start, stop, restart)
# ------------------------------------------------------------------------------

# Start all dev services or a specific service
start SERVICE="all":
    @just start-service {{SERVICE}}

# Stop all dev services or a specific service (NEVER kills active TUI)
stop SERVICE="all":
    @just stop-service {{SERVICE}}

# Restart all dev services or a specific service (NEVER kills active TUI)
restart SERVICE="all":
    @just restart-service {{SERVICE}}

# Internal helper: Start individual target
start-service TARGET:
    #!/usr/bin/env bash
    set -euo pipefail
    mkdir -p {{LOG_DIR}} {{PID_DIR}}
    case "{{TARGET}}" in
        postgres|db|database)
            just db-up
            ;;
        redis)
            (cd infra/database && docker compose up -d redis 2>/dev/null || true)
            ;;
        backend|grpc)
            just db-up
            if {{DEVPROC}} running --pidfile {{PID_DIR}}/backend.pid; then
                echo "Backend already running (session $(cat {{PID_DIR}}/backend.pid)); 'just restart backend' restarts it."
            else
                {{DEVPROC}} ports-free 5099,5001,1883 --service backend
                echo ">> Starting Backend REST API & embedded MQTT broker (dotnet watch: rebuild + restart on save)..."
                # Console output -> .logs/backend.log through the rotating log pipe (a detached sibling: it
                # ends at EOF and survives Ctrl-C); the backend's own rotating file log -> .logs/backend/.
                {{DOTNET_WATCH_ENV}} Logging__File__Path="{{LOG_DIR}}/backend" setsid dotnet watch --no-hot-reload --project src/backend/App.Backend.Api run </dev/null > >(exec setsid -f {{LOGPIPE}} "{{LOG_DIR}}/backend.log" --pidfile {{PID_DIR}}/backend.pid >/dev/null 2>&1) 2>&1 &
                echo $! > {{PID_DIR}}/backend.pid
            fi
            ;;
        frontend)
            if ! {{DEVPROC}} running --pidfile {{PID_DIR}}/frontend.pid; then
                {{DEVPROC}} ports-free 3000 --service frontend
                # Dev feature flags from the committed .env.dev (HEIMDALL_ENABLE_DEV & co.: anonymous dev admin in
                # the BFF, kiosk demo tokens and dev auth bypass, debug features). Only these keys: the rest of the
                # file is meant for the backend/docker (its DATABASE_URL is the backend's DB role, its
                # BETTER_AUTH_SECRET differs from the frontend's dev default and would void sessions and MFA
                # secrets). Variables already set win: HEIMDALL_ENABLE_DEV=false just start frontend
                while IFS='=' read -r key value; do
                    case "$key" in
                        HEIMDALL_ENABLE_DEV|HEIMDALL_ENABLE_DEBUG|HEIMDALL_DEBUG|NUXT_PUBLIC_ENABLE_DEV_FEATURES|NUXT_PUBLIC_ENABLE_DEBUG_FEATURES|ENABLE_DEV_HTTP_SEED)
                            if [ -z "${!key+x}" ]; then export "$key=$value"; fi
                            ;;
                    esac
                done < <(grep -E '^[A-Z_]+=' .env.dev 2>/dev/null || true)
                echo ">> Starting Nuxt 4 Web Frontend on http://localhost:3000 (HEIMDALL_ENABLE_DEV=${HEIMDALL_ENABLE_DEV:-unset})..."
                setsid bun run --cwd src/frontend/web dev </dev/null > >(exec setsid -f {{LOGPIPE}} "{{LOG_DIR}}/frontend.log" --pidfile {{PID_DIR}}/frontend.pid >/dev/null 2>&1) 2>&1 &
                echo $! > {{PID_DIR}}/frontend.pid
            fi
            ;;
        agent)
            if ! {{DEVPROC}} running --pidfile {{PID_DIR}}/agent.pid; then
                echo ">> Starting Host Linux Edge Agent Daemon..."
                {{DOTNET_WATCH_ENV}} setsid dotnet watch --no-hot-reload --project src/agent/App.Agent.Daemon run </dev/null > >(exec setsid -f {{LOGPIPE}} "{{LOG_DIR}}/agent.log" --pidfile {{PID_DIR}}/agent.pid >/dev/null 2>&1) 2>&1 &
                echo $! > {{PID_DIR}}/agent.pid
            fi
            ;;
        simulator)
            if ! {{DEVPROC}} running --pidfile {{PID_DIR}}/simulator.pid; then
                {{DEVPROC}} ports-free 5055 --service simulator
                # SIM_DATASET=enterprise (just db-seed) | demo (just demo-seed) | all. Only pick a dataset that is seeded:
                # the backend auto-registers unknown controllers that report in.
                echo ">> Starting Edge Fleet Simulator (MQTT -> :1883, control API 127.0.0.1:5055, dataset ${SIM_DATASET:-enterprise})..."
                setsid {{SIM_PYTHON}} -u simulators/fleet/fleet_simulator.py --dataset "${SIM_DATASET:-enterprise}" --station-interval "${SIM_STATION_INTERVAL:-4}" </dev/null > >(exec setsid -f {{LOGPIPE}} "{{LOG_DIR}}/simulator.log" --pidfile {{PID_DIR}}/simulator.pid >/dev/null 2>&1) 2>&1 &
                echo $! > {{PID_DIR}}/simulator.pid
            fi
            ;;
        windows|windows_agent|windows_vnc|windows_ads|windows_opc|winagent)
            just windows-start
            ;;
        all)
            just db-up
            # A service whose port is taken (e.g. a backend run by hand) is reported and skipped, not fatal.
            just start-service backend || true
            just start-service frontend || true
            just start-service simulator || true
            just windows-start
            echo "✓ All core development services successfully launched."
            ;;
        *)
            echo "Unknown service target: {{TARGET}}"
            exit 1
            ;;
    esac

# Internal helper: Stop individual target (Preserves caller TUI)
stop-service TARGET:
    #!/usr/bin/env bash
    set -euo pipefail
    case "{{TARGET}}" in
        postgres)
            (cd infra/database && docker compose stop postgres 2>/dev/null || true)
            echo "PostgreSQL stopped."
            ;;
        redis)
            (cd infra/database && docker compose stop redis 2>/dev/null || true)
            echo "Redis stopped."
            ;;
        db|database)
            just db-down
            ;;
        # Each service: every process of its setsid session (PID file), then whoever still holds its ports.
        backend|grpc)
            {{DEVPROC}} stop "Backend API" --pidfile {{PID_DIR}}/backend.pid --ports 5099,5001,1883
            ;;
        frontend)
            {{DEVPROC}} stop "Nuxt Frontend" --pidfile {{PID_DIR}}/frontend.pid --ports 3000
            ;;
        agent)
            {{DEVPROC}} stop "Agent Daemon" --pidfile {{PID_DIR}}/agent.pid --ports 5998
            ;;
        simulator)
            {{DEVPROC}} stop "Fleet Simulator" --pidfile {{PID_DIR}}/simulator.pid --ports 5055
            ;;
        windows|windows_agent|windows_vnc|windows_ads|windows_opc|winagent)
            just windows-stop
            ;;
        all)
            echo ">> Stopping all development background services..."
            for s in backend frontend agent simulator; do
                just stop-service "$s" >/dev/null 2>&1 || true
            done
            just windows-stop >/dev/null 2>&1 || true
            just db-down >/dev/null 2>&1 || true
            echo "✓ All development services stopped (TUI preserved)."
            ;;
        *)
            echo "Unknown service target: {{TARGET}}"
            exit 1
            ;;
    esac

# Internal helper: Restart individual target
restart-service TARGET:
    #!/usr/bin/env bash
    set -euo pipefail
    case "{{TARGET}}" in
        windows|windows_agent|windows_vnc|windows_ads|windows_opc|winagent)
            just windows-restart
            ;;
        all)
            echo ">> Restarting all development services..."
            just stop-service all
            sleep 1.2
            just start-service all
            echo "✓ All development services successfully restarted."
            ;;
        *)
            just stop-service "{{TARGET}}"
            sleep 1
            just start-service "{{TARGET}}"
            echo "✓ Restarted {{TARGET}}."
            ;;
    esac

# ------------------------------------------------------------------------------
# Windows 10 LTSC Edge Agent Container (KVM)
# ------------------------------------------------------------------------------

# Windows agent dispatcher
windows ACTION="status":
    #!/usr/bin/env bash
    case "{{ACTION}}" in
        start|up)       just windows-start ;;
        stop|down)      just windows-stop ;;
        restart)        just windows-restart ;;
        status)         just windows-status ;;
        logs)           just windows-logs ;;
        build)          just windows-build ;;
        launch)         python3 tools/launch_agent_win.py ;;
        test)           just windows-test ;;
        vnc)            echo "http://localhost:8006" ;;
        api)            echo "http://localhost:5998" ;;
        *)              just windows-status ;;
    esac

# Start Windows Agent KVM container
windows-start:
    #!/usr/bin/env bash
    set -euo pipefail
    if [ ! -f "{{SHARED_WIN_AGENT_DIR}}/heimdall-agent.exe" ]; then
        just windows-build
    fi
    docker compose --profile windows up -d windows-agent 2>/dev/null || true
    echo "✓ Windows Agent container initialized (:8006 VNC, :5998 API, :48898 ADS, :4840 OPC)."

# Stop Windows Agent container
windows-stop:
    @docker compose --profile windows stop windows-agent 2>/dev/null || true
    @echo "✓ Windows Edge Agent container stopped."

# Restart Windows Agent container
windows-restart:
    @just windows-stop
    @sleep 1
    @just windows-start

# Display Windows Agent ports & service connectivity
windows-status:
    @python3 tools/test_windows_agent.py --status

# Live container logs for Windows Agent
windows-logs:
    @docker compose --profile windows logs -f --tail=50 windows-agent

# Build win-x64 self-contained agent binary
windows-build:
    #!/usr/bin/env bash
    set -euo pipefail
    echo ">> Publishing Windows Agent binary (win-x64, self-contained)..."
    mkdir -p {{SHARED_WIN_AGENT_DIR}}
    dotnet publish src/agent/App.Agent.Daemon/App.Agent.Daemon.csproj \
        -r win-x64 \
        -c Release \
        --self-contained true \
        -o {{SHARED_WIN_AGENT_DIR}}
    echo "✓ Windows Agent binary ready in {{SHARED_WIN_AGENT_DIR}}."

# Run Windows integration tests
windows-test:
    @python3 tools/test_windows_agent.py --test

# ------------------------------------------------------------------------------
# Database Lifecycle & Seed Data Management
# ------------------------------------------------------------------------------

# Ensure PostgreSQL (5432) and Redis (6379) are active
db-up:
    #!/usr/bin/env bash
    set -euo pipefail
    if [ -d "infra/database" ]; then
        python3 -c "import socket; s = socket.socket(); s.settimeout(0.5); res = s.connect_ex(('127.0.0.1', 5432)); s.close(); exit(0 if res == 0 else 1)" 2>/dev/null && \
        python3 -c "import socket; s = socket.socket(); s.settimeout(0.5); res = s.connect_ex(('127.0.0.1', 6379)); s.close(); exit(0 if res == 0 else 1)" 2>/dev/null || {
            echo ">> Booting PostgreSQL & Redis databases via Docker..."
            (cd infra/database && docker compose up -d 2>/dev/null || true)
            for i in {1..20}; do
                if python3 -c "import socket; s = socket.socket(); s.settimeout(0.5); res = s.connect_ex(('127.0.0.1', 5432)); s.close(); exit(0 if res == 0 else 1)" 2>/dev/null && \
                   python3 -c "import socket; s = socket.socket(); s.settimeout(0.5); res = s.connect_ex(('127.0.0.1', 6379)); s.close(); exit(0 if res == 0 else 1)" 2>/dev/null; then
                    echo "✓ Database containers healthy."
                    break
                fi
                sleep 0.5
            done
        }
    fi

# Stop PostgreSQL & Redis containers
db-down:
    @if [ -d "infra/database" ]; then (cd infra/database && docker compose down 2>/dev/null || true); fi
    @echo "✓ Database containers stopped."

# Seed development database with full enterprise dataset
db-seed:
    #!/usr/bin/env bash
    set -euo pipefail
    just db-up
    echo ">> Validating & applying seed data pipeline..."
    python3 seed_data/seed_pipeline.py --generate-all --validate
    if docker ps | grep -q "postgres_heimdall"; then
        docker exec -i postgres_heimdall psql -v ON_ERROR_STOP=1 -U postgres -d heimdall_dev_db < seed_data/incremental_seed.sql >/dev/null
        echo "✓ Enterprise reference data (100 machines, 56 IPCs, 26 machine groups, 16 orgs, 107 users, 550 spares) applied to heimdall_dev_db."
        echo "  No tickets / telemetry are seeded: run the simulator (just sim-reset) to produce plant activity."
    fi

# ARGS go to scripts/seed_demo_data.py, e.g. `just demo-seed --reset --unpin-conflicts` (see seed_data/demo/README.md)
# Load the story-driven Nordvik NV1 demo dataset into the dev DB (idempotent, dev-only, demo logins)
demo-seed *ARGS:
    #!/usr/bin/env bash
    set -euo pipefail
    just db-up
    python3 scripts/seed_demo_data.py --with-logins {{ARGS}}

# Validate the demo dataset offline (JSON, references, enums, SQL plan) and against the dev DB (rolled back)
demo-check:
    #!/usr/bin/env bash
    set -euo pipefail
    python3 scripts/seed_demo_data.py --dry-run >/dev/null
    if docker ps --format '{{{{.Names}}' | grep -qx "postgres_heimdall"; then
        python3 scripts/seed_demo_data.py --check
    else
        echo "postgres_heimdall not running - offline validation only."
    fi

# Register the demo Lua scripts and replay demo telemetry into the running backend (repeat after each backend restart)
demo-runtime:
    @python3 scripts/seed_demo_data.py --skip-db --push-runtime

# ARGS go to fleet_simulator.py, e.g. `just sim-demo --scenario demo_story --speed 10 --seed 7`
# Run the fleet simulator in the foreground against the Nordvik NV1 demo dataset (live station telemetry for the demo plant)
sim-demo *ARGS:
    #!/usr/bin/env bash
    set -euo pipefail
    just stop-service simulator >/dev/null 2>&1 || true
    exec {{SIM_PYTHON}} -u simulators/fleet/fleet_simulator.py --dataset demo {{ARGS}}

# One command: reseed DATASET (enterprise | demo) and restart the background simulator on it with plant activity
# (telemetry, heartbeats, tickets and spare-part use through the backend API). Needs the backend running.
sim-reset DATASET="enterprise":
    #!/usr/bin/env bash
    set -euo pipefail
    case "{{DATASET}}" in
        enterprise) just db-seed; export SIM_DATASET=enterprise ;;
        demo)       just demo-seed --unpin-conflicts; export SIM_DATASET=all ;;
        *)          echo "DATASET must be enterprise or demo"; exit 1 ;;
    esac
    just stop-service simulator >/dev/null 2>&1 || true
    just start-service simulator
    echo "✓ Simulator restarted on dataset ${SIM_DATASET}. Status: curl -s 127.0.0.1:5055/api/status | jq .maintenance"

# Set the simulated failure rate (failures per station and simulated hour) and optionally the speed, e.g. `just sim-rate 0.3 10`
sim-rate RATE="0.05" SPEED="1":
    @curl -fsS -X POST -H 'Content-Type: application/json' -d '{"incident_rate": {{RATE}}}' http://127.0.0.1:5055/api/activity && echo
    @curl -fsS -X POST -H 'Content-Type: application/json' -d '{"speed": {{SPEED}}}' http://127.0.0.1:5055/api/speed && echo

# Trigger a simulator scenario on the running simulator, e.g. `just sim-scenario demo_story` or `just sim-scenario clear`
sim-scenario NAME="demo_story":
    @curl -fsS -X POST -H 'Content-Type: application/json' -d '{"name":"{{NAME}}"}' http://127.0.0.1:5055/api/scenario && echo

# Remove all demo rows (ids de30de30-* / demo-*) from the dev database and reload them
demo-reset *ARGS:
    @python3 scripts/seed_demo_data.py --reset --with-logins {{ARGS}}

# Seed staging database with the exact same enterprise seed data
db-staging-seed:
    #!/usr/bin/env bash
    set -euo pipefail
    just db-up
    echo ">> Seeding staging database (heimdall_staging_db) with enterprise seed data..."
    python3 seed_data/seed_pipeline.py --generate-all --validate
    if docker ps | grep -q "postgres_heimdall"; then
        docker exec postgres_heimdall psql -U postgres -c "CREATE DATABASE heimdall_staging_db TEMPLATE heimdall_dev_db OWNER ef_admin;" 2>/dev/null || true
        docker exec -i postgres_heimdall psql -U postgres -d heimdall_staging_db < seed_data/incremental_seed.sql >/dev/null
        docker exec postgres_heimdall psql -U postgres -d heimdall_staging_db -c "
            GRANT ALL PRIVILEGES ON DATABASE heimdall_staging_db TO ef_admin, drizzle_admin, dotnet_backend, nuxt_frontend;
            GRANT USAGE, CREATE ON SCHEMA backend TO ef_admin, drizzle_admin, dotnet_backend, nuxt_frontend;
            GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA backend TO ef_admin, drizzle_admin, dotnet_backend, nuxt_frontend;
            GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA backend TO ef_admin, drizzle_admin, dotnet_backend, nuxt_frontend;
            GRANT USAGE, CREATE ON SCHEMA auth TO ef_admin, drizzle_admin, dotnet_backend, nuxt_frontend;
            GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA auth TO ef_admin, drizzle_admin, dotnet_backend, nuxt_frontend;
            GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA auth TO ef_admin, drizzle_admin, dotnet_backend, nuxt_frontend;
        " >/dev/null 2>&1 || true
        echo "✓ Enterprise dataset successfully seeded into heimdall_staging_db."
    fi

# ------------------------------------------------------------------------------
# Staging Environment (No Dev Features, Only Debug)
# ------------------------------------------------------------------------------

# Start staging environment stack with staging configuration
staging-up:
    #!/usr/bin/env bash
    set -euo pipefail
    echo ">> Booting Heimdall Staging Environment (no dev features, only debug)..."
    just db-staging-seed
    docker compose -f docker-compose.staging.yml up -d
    echo "✓ Staging environment online."
    echo "  • Web Frontend: http://localhost:3000 (NODE_ENV=production, DEV=false, DEBUG=true)"
    echo "  • Backend API:  http://localhost:5099 (ASPNETCORE_ENVIRONMENT=Staging, DEV=false, DEBUG=true)"
    echo "  • Staging DB:   heimdall_staging_db (PostgreSQL 18)"

# Stop staging environment stack
staging-down:
    @docker compose -f docker-compose.staging.yml down
    @echo "✓ Staging environment stopped."

# Restart staging environment stack
staging-restart:
    @just staging-down
    @sleep 1
    @just staging-up

# Check staging environment service status
staging-status:
    @docker compose -f docker-compose.staging.yml ps

# View staging logs
staging-logs SERVICE="all":
    #!/usr/bin/env bash
    if [ "{{SERVICE}}" = "all" ]; then
        docker compose -f docker-compose.staging.yml logs -f --tail=60
    else
        docker compose -f docker-compose.staging.yml logs -f --tail=60 "{{SERVICE}}"
    fi

# Run comprehensive test suite against the staging environment
test-staging:
    #!/usr/bin/env bash
    set -euo pipefail
    echo "========================================================================="
    echo "          RUNNING HEIMDALL STAGING ENVIRONMENT VERIFICATION SUITE         "
    echo "========================================================================="
    python3 tools/test_staging.py

# ------------------------------------------------------------------------------
# Production Environment
# ------------------------------------------------------------------------------

# Build production container images
prod-build:
    @docker compose -f docker-compose.prod.yml build
    @echo "✓ Production container images built."

# Start production container stack
prod-up:
    @docker compose -f docker-compose.prod.yml up -d
    @echo "✓ Production stack running with docker-compose.prod.yml."

# Stop production container stack
prod-down:
    @docker compose -f docker-compose.prod.yml down
    @echo "✓ Production stack stopped."

# Restart production container stack
prod-restart:
    @just prod-down
    @sleep 1
    @just prod-up

# Check production service status
prod-status:
    @docker compose -f docker-compose.prod.yml ps

# View production logs
prod-logs SERVICE="all":
    #!/usr/bin/env bash
    if [ "{{SERVICE}}" = "all" ]; then
        docker compose -f docker-compose.prod.yml logs -f --tail=60
    else
        docker compose -f docker-compose.prod.yml logs -f --tail=60 "{{SERVICE}}"
    fi

# ------------------------------------------------------------------------------
# Verification & Testing Suites
# ------------------------------------------------------------------------------

# Run verification test suites (all, backend, frontend, windows, seed, smoke, staging)
test SUBSYSTEM="all":
    #!/usr/bin/env bash
    set -euo pipefail
    case "{{SUBSYSTEM}}" in
        backend)
            just test-backend
            ;;
        frontend)
            just test-frontend
            ;;
        windows)
            just windows-test
            ;;
        seed)
            just test-seed
            ;;
        smoke)
            just test-smoke
            ;;
        staging)
            just test-staging
            ;;
        all|*)
            just test-seed
            just test-backend
            just test-frontend
            echo "✅ All verification suites passed successfully."
            ;;
    esac

# Run .NET backend unit and integration tests (xUnit)
test-backend:
    @echo ">> Running Backend Test Suite (xUnit)..."
    @dotnet test Heimdall.sln

# Run frontend unit tests (Vitest)
test-frontend:
    @echo ">> Running Frontend Test Suite (Vitest)..."
    @(cd src/frontend/web && bun run test)

# Validate seed data pipeline and referential integrity
test-seed:
    @echo ">> Validating Seed Data Pipeline & Referential Integrity..."
    @python3 seed_data/seed_pipeline.py --validate
    @python3 scripts/seed_demo_data.py --dry-run >/dev/null && echo "✓ Demo dataset (seed_data/demo) valid, references resolve"
    @python3 -m unittest discover -s seed_data -p "test_*.py" 2>&1 | tail -1

# Run edge fleet simulator smoke test (publishes 5 heartbeats + 5 station blocks to the backend's MQTT broker)
test-smoke:
    @echo ">> Running Fleet Simulator Smoke Test..."
    @{{SIM_PYTHON}} simulators/fleet/fleet_simulator.py --smoke-test --count 5 || echo "Note: Smoke test requires the backend's embedded MQTT broker on port 1883 (just start backend) and paho-mqtt/protobuf (pip install -r simulators/fleet/requirements.txt)."

# Run the fleet simulator unit tests (station model, PLC block encoding, MQTT framing, control API; no backend needed)
test-simulator:
    @cd simulators/fleet && ../../{{SIM_PYTHON}} -m unittest discover -p "test_*.py"

# Build mobile PWA production assets
mobile-build:
    @echo ">> Building Mobile PWA Production Assets..."
    @(cd src/frontend/web && npm run build)

# Package release Android APK via containerized build environment
mobile-apk:
    @echo ">> Packaging Android APK via Containerized Build..."
    @docker build -t heimdall-android-builder -f deployment/android/Dockerfile.android-build .

