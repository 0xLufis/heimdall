#!/bin/bash

# Ensure script runs from project root directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Ensure user-installed binaries (zellij, bun, dotnet local tools, etc.) are in PATH
export PATH="$HOME/.local/bin:$HOME/.bun/bin:$HOME/.dotnet/tools:$HOME/.dotnet:$PATH"
if [ -d "$HOME/.dotnet" ]; then
    export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
fi

SESSION_NAME="heimdall-dev"
LAYOUT_FILE="dev_layout.kdl"
LOG_DIR="/tmp/heimdall_logs"
PID_DIR="/tmp/heimdall_dev_pids"
mkdir -p "$LOG_DIR" "$PID_DIR"

is_running() {
    local pid_file="$1"
    if [ -f "$pid_file" ]; then
        local pid=$(cat "$pid_file" 2>/dev/null)
        if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
    fi
    return 1
}

check_tcp() {
    local host="$1"
    local port="$2"
    python3 -c "import socket; s = socket.socket(); s.settimeout(0.5); res = s.connect_ex(('$host', int($port))); s.close(); exit(0 if res == 0 else 1)" 2>/dev/null
}

show_logs() {
    local target="${1:-all}"
    case "$target" in
        backend)
            tail -n 50 -f "$LOG_DIR/backend.log" 2>/dev/null || tail -n 50 -f /tmp/heimdall-backend.log
            ;;
        frontend)
            tail -n 50 -f "$LOG_DIR/frontend.log" 2>/dev/null || tail -n 50 -f /tmp/heimdall-nuxt.log
            ;;
        agent)
            tail -n 50 -f "$LOG_DIR/agent.log" 2>/dev/null || tail -n 50 -f /tmp/heimdall-agent.log
            ;;
        simulator)
            tail -n 50 -f "$LOG_DIR/simulator.log" 2>/dev/null || tail -n 50 -f /tmp/heimdall-simulator.log
            ;;
        db)
            if [ -d "infra/database" ]; then
                cd infra/database && docker compose logs -f --tail=50
            fi
            ;;
        *)
            echo "Displaying latest log pointers:"
            echo "  Backend:   /tmp/heimdall-backend.log"
            echo "  Frontend:  /tmp/heimdall-nuxt.log"
            echo "  Agent:     /tmp/heimdall-agent.log"
            echo "  Simulator: /tmp/heimdall-simulator.log"
            echo ""
            echo "Use './run_dev.sh logs <backend|frontend|agent|simulator|db>' to stream a specific service."
            ;;
    esac
}

check_status() {
    if [ -f "tools/dev_manager.py" ]; then
        python3 tools/dev_manager.py status
    else
        echo "=== Heimdall Process Status ==="
        check_tcp 127.0.0.1 5099 && echo "  Backend API: ● ONLINE (:5099)" || echo "  Backend API: ○ OFFLINE"
        check_tcp 127.0.0.1 3000 && echo "  Web Frontend: ● ONLINE (:3000)" || echo "  Web Frontend: ○ OFFLINE"
        check_tcp 127.0.0.1 5001 && echo "  gRPC Collector: ● ONLINE (:5001)" || echo "  gRPC Collector: ○ OFFLINE"
    fi
}

stop_services() {
    echo "========================================"
    echo " Stopping Heimdall Services..."
    echo "========================================"
    
    # 1. Kill Zellij session if it exists
    if command -v zellij >/dev/null 2>&1 && zellij list-sessions 2>/dev/null | grep -q "$SESSION_NAME"; then
        echo "Killing Zellij session: $SESSION_NAME"
        zellij kill-session "$SESSION_NAME" 2>/dev/null || true
    fi

    # 2. Stop tracked processes from PID files
    for pid_file in "$PID_DIR"/*.pid; do
        if [ -f "$pid_file" ]; then
            pid=$(cat "$pid_file" 2>/dev/null)
            if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
                kill "$pid" 2>/dev/null || true
            fi
            rm -f "$pid_file"
        fi
    done

    # 3. Clean up lingering development processes
    pkill -f "dotnet run --project backend/App.Backend.Api" 2>/dev/null || true
    pkill -f "dotnet run --project agent/App.Agent.Daemon" 2>/dev/null || true
    pkill -f "bun.*heimdall-web-frontend" 2>/dev/null || true
    pkill -f "nuxt/bin/nuxt" 2>/dev/null || true
    pkill -f "fleet_simulator.py" 2>/dev/null || true

    sleep 1

    # 4. Stop Database via Docker Compose
    if [ -d "infra/database" ]; then
        echo "Stopping Database via Docker Compose..."
        cd infra/database
        docker compose down 2>/dev/null || true
        cd "$SCRIPT_DIR"
    fi
    
    echo "All services stopped."
}

start_services() {
    echo "========================================"
    echo " Starting Heimdall Development Environment"
    echo "========================================"

    # 1. Ensure Database is running
    if [ -d "infra/database" ]; then
        echo "Starting Database via Docker Compose..."
        cd infra/database
        docker compose up -d 2>/dev/null || true
        cd "$SCRIPT_DIR"
    fi

    # 2. Start Backend API
    if ! check_tcp 127.0.0.1 5099; then
        echo "Starting Backend API (http://localhost:5099 & gRPC :5001)..."
        (export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && cd backend/App.Backend.Api && nohup dotnet run </dev/null > /tmp/heimdall-backend.log 2>&1 & echo $! > "$PID_DIR/backend.pid")
    fi

    # 3. Start Nuxt Frontend
    if ! check_tcp 127.0.0.1 3000; then
        echo "Starting Heimdall Web Frontend on http://localhost:3000..."
        (cd "$SCRIPT_DIR/frontend/heimdall-web-frontend" && nohup node ./node_modules/nuxt/bin/nuxt.mjs dev --host 0.0.0.0 --port 3000 </dev/null > /tmp/heimdall-nuxt.log 2>&1 & echo $! > "$PID_DIR/frontend.pid")
    fi

    # 4. Start Agent Daemon
    if ! is_running "$PID_DIR/agent.pid"; then
        echo "Starting Agent Daemon..."
        (export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && cd agent/App.Agent.Daemon && nohup dotnet run </dev/null > /tmp/heimdall-agent.log 2>&1 & echo $! > "$PID_DIR/agent.pid")
    fi

    # 5. Start Industrial Edge Fleet Simulator
    if ! is_running "$PID_DIR/simulator.pid"; then
        echo "Starting Edge Fleet Simulator..."
        python_bin="./venv/bin/python"
        [ ! -f "$python_bin" ] && python_bin="python3"
        (nohup $python_bin simulators/edge-fleet-simulator/fleet_simulator.py </dev/null > /tmp/heimdall-simulator.log 2>&1 & echo $! > "$PID_DIR/simulator.pid")
    fi

    echo "Services initialized. Use './run_dev.sh status' to monitor."
}

start_zellij() {
    if ! command -v zellij >/dev/null 2>&1; then
        echo "Zellij is not installed. Falling back to background daemon mode."
        start_services
        return
    fi

    echo "Launching Zellij session: $SESSION_NAME..."
    zellij --session "$SESSION_NAME" --layout "$LAYOUT_FILE"
}

output_completion() {
    local shell_type="${1:-bash}"
    if [ "$shell_type" = "zsh" ]; then
        cat "$SCRIPT_DIR/tools/completions/heimdall_completion.zsh"
    else
        cat "$SCRIPT_DIR/tools/completions/heimdall_completion.bash"
    fi
}

show_help() {
    echo "Heimdall Development Environment Manager"
    echo ""
    echo "Usage: ./run_dev.sh [COMMAND] [OPTIONS]"
    echo ""
    echo "Commands:"
    echo "  start               Start all services in the background"
    echo "  stop                Stop all running services and cleanup processes"
    echo "  restart [service]   Restart all or a specific service (backend, frontend, agent, simulator, db)"
    echo "  status              Display live service health and port matrix"
    echo "  logs [service]      Stream logs for a service (backend, frontend, agent, simulator, db)"
    echo "  zellij              Start development environment in Zellij multiplexer"
    echo "  completion [shell]  Output shell completion code (bash, zsh)"
    echo "  help, -h, --help    Show this help message"
}

case "$1" in
    start)
        start_services
        ;;
    stop)
        stop_services
        ;;
    restart)
        stop_services
        sleep 1
        start_services
        ;;
    status)
        check_status
        ;;
    logs)
        show_logs "$2"
        ;;
    zellij)
        start_zellij
        ;;
    completion)
        output_completion "$2"
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        if [ -z "$1" ]; then
            start_services
        else
            echo "Unknown command: $1"
            echo "Run './run_dev.sh help' for usage instructions."
            exit 1
        fi
        ;;
esac
