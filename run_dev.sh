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

# Function to stop all services
stop_services() {
    echo "========================================"
    echo " Stopping Heimdall Services..."
    echo "========================================"
    
    # 1. Kill Zellij session if it exists
    if zellij list-sessions 2>/dev/null | grep -q "$SESSION_NAME"; then
        echo "Killing Zellij session: $SESSION_NAME"
        zellij kill-session "$SESSION_NAME"
    fi

    # 2. Kill background jobs and any lingering dev processes
    echo "Cleaning up lingering development processes..."
    # Kill by pattern to catch bun, dotnet, and python processes
    # We use pkill -f for broader matching of the command lines
    pkill -f "dotnet run --project backend/App.Backend.Api" 2>/dev/null
    pkill -f "dotnet run --project agent/App.Agent.Daemon" 2>/dev/null
    pkill -f "bun run dev" 2>/dev/null
    pkill -f "nuxt" 2>/dev/null # Bun often spawns a 'nuxt' child
    pkill -f "python simulate_pcs.py" 2>/dev/null

    # Give them a moment to stop gracefully, then force if necessary
    sleep 1
    pids=$(pgrep -f "dotnet run|bun run dev|nuxt|python simulate_pcs.py")
    if [ -n "$pids" ]; then
        echo "Forcing remaining processes to stop..."
        kill -9 $pids 2>/dev/null
    fi

    # 3. Stop Database via Docker Compose
    echo "Stopping Database via Docker Compose..."
    if [ -d "infra/database" ]; then
        cd infra/database
        docker compose down 2>/dev/null || true
        cd "$SCRIPT_DIR"
    fi
    
    echo "All services stopped."
}

# Function to start all services
start_services() {
    echo "========================================"
    echo " Starting Heimdall Development Environment"
    echo "========================================"

    # Ensure Database is running
    echo "Starting Database via Docker Compose..."
    if [ -d "infra/database" ]; then
        cd infra/database
        docker compose up -d 2>/dev/null || true
        cd "$SCRIPT_DIR"
    fi

    # Start Backend API if not running
    if ! pgrep -f "App.Backend.Api" >/dev/null 2>&1; then
        echo "Starting Backend API..."
        (export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && cd backend/App.Backend.Api && nohup dotnet run > /tmp/heimdall-backend.log 2>&1) &
    fi

    # Start Nuxt Frontend if not running
    if ! pgrep -f "nuxt" >/dev/null 2>&1; then
        echo "Starting Nuxt Frontend server on http://localhost:3000..."
        (export PATH="$HOME/.bun/bin:$PATH" && cd frontend/nuxt-app && nohup bun run dev > /tmp/heimdall-nuxt.log 2>&1) &
    fi

    # Start Agent Daemon if not running
    if ! pgrep -f "App.Agent.Daemon" >/dev/null 2>&1; then
        echo "Starting Agent Daemon..."
        (export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && cd agent/App.Agent.Daemon && nohup dotnet run > /tmp/heimdall-agent.log 2>&1) &
    fi

    # Start PC Telemetry Simulator if not running
    if ! pgrep -f "simulate_pcs.py" >/dev/null 2>&1; then
        echo "Starting PC Simulator..."
        (nohup ./venv/bin/python simulate_pcs.py > /tmp/heimdall-simulator.log 2>&1) &
    fi

    echo "Services started successfully."
}

# Main logic
case "$1" in
    stop)
        stop_services
        ;;
    *)
        start_services
        ;;
esac
