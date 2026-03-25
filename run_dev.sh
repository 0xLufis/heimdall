#!/bin/bash

# Configuration
SESSION_NAME="heimdall-dev"
LAYOUT_FILE="dev_resources/layouts/dev_layout.kdl"

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
    pkill -f "bun --cwd=frontend/nuxt-app run dev" 2>/dev/null
    pkill -f "nuxt" 2>/dev/null # Bun often spawns a 'nuxt' child
    pkill -f "python dev_resources/scripts/simulate_pcs.py" 2>/dev/null
    pkill -f "ngrok http 3000" 2>/dev/null

    # Give them a moment to stop gracefully, then force if necessary
    sleep 1
    pids=$(pgrep -f "dotnet run|bun run dev|nuxt|python dev_resources/scripts/simulate_pcs.py|ngrok http")
    if [ -n "$pids" ]; then
        echo "Forcing remaining processes to stop..."
        kill -9 $pids 2>/dev/null
    fi

    # 3. Stop Docker Compose
    echo "Stopping Database via Docker Compose..."
    cd infra/database
    docker compose down
    cd ../../
    
    echo "All services stopped."
}

# Function to start all services
start_services() {
    echo "========================================"
    echo " Starting Heimdall Development Environment"
    echo "========================================"

    # Ensure Database is running
    echo "Starting Database via Docker Compose..."
    cd infra/database
    docker compose up -d
    cd ../../

    # Check for Zellij
    if command -v zellij >/dev/null 2>&1; then
        echo "Starting/Attaching Zellij session: $SESSION_NAME"
        
        # Correct Zellij syntax: global flags come before the subcommand
        exec zellij --layout "$LAYOUT_FILE" attach "$SESSION_NAME" --create
    else
        echo "Zellij not found. Falling back to traditional background processes."
        # Traditional startup (original script logic)
        
        # Start Backend API
        cd backend/App.Backend.Api && dotnet run & cd ../../
        
        # Start Nuxt Frontend
        cd frontend/nuxt-app && bun run dev & cd ../../
        
        # Start Agent
        cd agent/App.Agent.Daemon && dotnet run & cd ../../
        
        # Start PC Simulator
        source venv/bin/activate && python dev_resources/scripts/simulate_pcs.py & deactivate
        
        # Start ngrok
        if command -v ngrok >/dev/null 2>&1; then
            cd frontend/nuxt-app && bunx ngrok http 3000 & cd ../../
        fi
        
        echo "Services started in background. Use '$0 stop' to kill them."
        wait
    fi
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
