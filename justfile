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

LOG_DIR := "/tmp/heimdall_logs"
PID_DIR := "/tmp/heimdall_dev_pids"
SHARED_WIN_AGENT_DIR := "infra/windows/shared/agent"

# Display available commands
default:
    @just --list

# ------------------------------------------------------------------------------
# Development Environment
# ------------------------------------------------------------------------------

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
    for pid_file in {{PID_DIR}}/*.pid; do
        if [ -f "$pid_file" ]; then
            pid=$(cat "$pid_file" 2>/dev/null || true)
            if [ -n "$pid" ]; then
                kill -9 "$pid" 2>/dev/null || true
            fi
            rm -f "$pid_file"
        fi
    done
    pkill -9 -f "App.Backend.Api" 2>/dev/null || true
    pkill -9 -f "App.Agent.Daemon" 2>/dev/null || true
    pkill -9 -f "dotnet watch.*App.Backend.Api" 2>/dev/null || true
    pkill -9 -f "dotnet watch.*App.Agent.Daemon" 2>/dev/null || true
    pkill -9 -f "bun.*run.*dev" 2>/dev/null || true
    pkill -9 -f "nuxt dev" 2>/dev/null || true
    pkill -9 -f "fleet_simulator.py" 2>/dev/null || true
    if command -v fuser >/dev/null 2>&1; then
        fuser -k 5099/tcp 2>/dev/null || true
        fuser -k 5001/tcp 2>/dev/null || true
        fuser -k 3000/tcp 2>/dev/null || true
        fuser -k 5055/tcp 2>/dev/null || true
        fuser -k 5998/tcp 2>/dev/null || true
    fi
    echo "✓ Environment cleanly cleared. Ports 5099, 5001, 3000, 5055, 5998 released."

# Stream service logs (backend, frontend, agent, simulator, windows, db)
logs SERVICE="all":
    #!/usr/bin/env bash
    case "{{SERVICE}}" in
        backend)
            tail -n 60 -f {{LOG_DIR}}/backend.log 2>/dev/null || tail -n 60 -f /tmp/heimdall-backend.log
            ;;
        frontend)
            tail -n 60 -f {{LOG_DIR}}/frontend.log 2>/dev/null || tail -n 60 -f /tmp/heimdall-nuxt.log
            ;;
        agent)
            tail -n 60 -f {{LOG_DIR}}/agent.log 2>/dev/null || tail -n 60 -f /tmp/heimdall-agent.log
            ;;
        simulator)
            tail -n 60 -f {{LOG_DIR}}/simulator.log 2>/dev/null || tail -n 60 -f /tmp/heimdall-simulator.log
            ;;
        windows|winagent)
            docker compose --profile windows logs -f --tail=60 windows-agent
            ;;
        db|database|postgres|redis)
            docker compose -f infra/database/docker-compose.yml logs -f --tail=60
            ;;
        all|*)
            echo "Available log targets: backend, frontend, agent, simulator, windows, db"
            echo "  • Backend API:     {{LOG_DIR}}/backend.log"
            echo "  • Web Frontend:    {{LOG_DIR}}/frontend.log"
            echo "  • Edge Agent:      {{LOG_DIR}}/agent.log"
            echo "  • Simulator:       {{LOG_DIR}}/simulator.log"
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
            if ! [ -f {{PID_DIR}}/backend.pid ] || ! kill -0 $(cat {{PID_DIR}}/backend.pid 2>/dev/null) 2>/dev/null; then
                echo ">> Starting Backend REST API & gRPC Ingestion..."
                setsid dotnet watch --project backend/App.Backend.Api run </dev/null > {{LOG_DIR}}/backend.log 2>&1 &
                echo $! > {{PID_DIR}}/backend.pid
                ln -sf {{LOG_DIR}}/backend.log /tmp/heimdall-backend.log 2>/dev/null || true
            fi
            ;;
        frontend)
            if ! [ -f {{PID_DIR}}/frontend.pid ] || ! kill -0 $(cat {{PID_DIR}}/frontend.pid 2>/dev/null) 2>/dev/null; then
                echo ">> Starting Nuxt 4 Web Frontend on http://localhost:3000..."
                setsid bun run --cwd frontend/web dev </dev/null > {{LOG_DIR}}/frontend.log 2>&1 &
                echo $! > {{PID_DIR}}/frontend.pid
                ln -sf {{LOG_DIR}}/frontend.log /tmp/heimdall-nuxt.log 2>/dev/null || true
            fi
            ;;
        agent)
            if ! [ -f {{PID_DIR}}/agent.pid ] || ! kill -0 $(cat {{PID_DIR}}/agent.pid 2>/dev/null) 2>/dev/null; then
                echo ">> Starting Host Linux Edge Agent Daemon..."
                setsid dotnet watch --project agent/App.Agent.Daemon run </dev/null > {{LOG_DIR}}/agent.log 2>&1 &
                echo $! > {{PID_DIR}}/agent.pid
                ln -sf {{LOG_DIR}}/agent.log /tmp/heimdall-agent.log 2>/dev/null || true
            fi
            ;;
        simulator)
            if ! [ -f {{PID_DIR}}/simulator.pid ] || ! kill -0 $(cat {{PID_DIR}}/simulator.pid 2>/dev/null) 2>/dev/null; then
                echo ">> Starting Edge Fleet Simulator (:5055)..."
                setsid python3 -u simulators/fleet/fleet_simulator.py </dev/null > {{LOG_DIR}}/simulator.log 2>&1 &
                echo $! > {{PID_DIR}}/simulator.pid
                ln -sf {{LOG_DIR}}/simulator.log /tmp/heimdall-simulator.log 2>/dev/null || true
            fi
            ;;
        windows|windows_agent|windows_vnc|windows_ads|windows_opc|winagent)
            just windows-start
            ;;
        all)
            just db-up
            just start-service backend
            just start-service frontend
            just start-service simulator
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
        backend|grpc)
            if [ -f {{PID_DIR}}/backend.pid ]; then
                kill $(cat {{PID_DIR}}/backend.pid 2>/dev/null) 2>/dev/null || true
                rm -f {{PID_DIR}}/backend.pid
            fi
            pkill -f "dotnet watch.*backend/App.Backend.Api" 2>/dev/null || true
            pkill -f "App.Backend.Api" 2>/dev/null || true
            if command -v fuser >/dev/null 2>&1; then
                fuser -k 5099/tcp 2>/dev/null || true
                fuser -k 5001/tcp 2>/dev/null || true
            fi
            echo "Backend API stopped."
            ;;
        frontend)
            if [ -f {{PID_DIR}}/frontend.pid ]; then
                kill $(cat {{PID_DIR}}/frontend.pid 2>/dev/null) 2>/dev/null || true
                rm -f {{PID_DIR}}/frontend.pid
            fi
            pkill -f "bun.*run.*dev" 2>/dev/null || true
            pkill -f "nuxt dev" 2>/dev/null || true
            if command -v fuser >/dev/null 2>&1; then
                fuser -k 3000/tcp 2>/dev/null || true
            fi
            echo "Nuxt Frontend stopped."
            ;;
        agent)
            if [ -f {{PID_DIR}}/agent.pid ]; then
                kill $(cat {{PID_DIR}}/agent.pid 2>/dev/null) 2>/dev/null || true
                rm -f {{PID_DIR}}/agent.pid
            fi
            pkill -f "dotnet watch.*agent/App.Agent.Daemon" 2>/dev/null || true
            pkill -f "App.Agent.Daemon" 2>/dev/null || true
            if command -v fuser >/dev/null 2>&1; then
                fuser -k 5998/tcp 2>/dev/null || true
            fi
            echo "Agent Daemon stopped."
            ;;
        simulator)
            if [ -f {{PID_DIR}}/simulator.pid ]; then
                kill $(cat {{PID_DIR}}/simulator.pid 2>/dev/null) 2>/dev/null || true
                rm -f {{PID_DIR}}/simulator.pid
            fi
            pkill -f "fleet_simulator.py" 2>/dev/null || true
            if command -v fuser >/dev/null 2>&1; then
                fuser -k 5055/tcp 2>/dev/null || true
            fi
            echo "Fleet Simulator stopped."
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
    dotnet publish agent/App.Agent.Daemon/App.Agent.Daemon.csproj \
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
    python3 seed_data/seed_pipeline.py --validate
    if docker ps | grep -q "postgres_heimdall"; then
        docker exec -i postgres_heimdall psql -U postgres -d heimdall_dev_db < seed_data/incremental_seed.sql >/dev/null
        echo "✓ Enterprise dataset (100 machines, 16 orgs, 550 spare parts) applied to heimdall_dev_db."
    fi

# Seed staging database with the exact same enterprise seed data
db-staging-seed:
    #!/usr/bin/env bash
    set -euo pipefail
    just db-up
    echo ">> Seeding staging database (heimdall_staging_db) with enterprise seed data..."
    python3 seed_data/seed_pipeline.py --validate
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
    @(cd frontend/web && bun run test)

# Validate seed data pipeline and referential integrity
test-seed:
    @echo ">> Validating Seed Data Pipeline & Referential Integrity..."
    @python3 seed_data/seed_pipeline.py --validate

# Run edge fleet simulator smoke test
test-smoke:
    @echo ">> Running Fleet Simulator Smoke Test..."
    @python3 simulators/fleet/fleet_simulator.py --smoke-test --count 5 || echo "Note: Smoke test requires active local gRPC service on port 5001."
