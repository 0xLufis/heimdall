#!/bin/bash
# ==============================================================================
# HEIMDALL INDUSTRIAL OT DEVELOPMENT ENVIRONMENT MANAGER
# ==============================================================================
# Full-stack orchestrator for ASP.NET Core backend, Nuxt 4 frontend,
# Linux Edge Agent, Beckhoff TwinCAT ADS runtime, minimal OPC UA server,
# Windows 10 LTSC KVM container, and PostgreSQL / Redis databases.
# ==============================================================================

set -e

# Ensure script runs from project root directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Ensure user-installed binaries (bun, dotnet, zellij, etc.) are in PATH
export PATH="$HOME/.local/bin:$HOME/.bun/bin:$HOME/.dotnet/tools:$HOME/.dotnet:$PATH"
if [ -d "$HOME/.dotnet" ]; then
    export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
fi

# Directory & Session Configurations
SESSION_NAME="heimdall-dev"
LAYOUT_FILE="dev_layout.kdl"
LOG_DIR="/tmp/heimdall_logs"
PID_DIR="/tmp/heimdall_dev_pids"
SHARED_WIN_AGENT_DIR="$SCRIPT_DIR/infra/windows/shared/agent"

mkdir -p "$LOG_DIR" "$PID_DIR"

# ANSI Color Palette
COLOR_RESET="\033[0m"
COLOR_BOLD="\033[1m"
COLOR_DIM="\033[2m"
COLOR_GREEN="\033[32m"
COLOR_RED="\033[31m"
COLOR_YELLOW="\033[33m"
COLOR_BLUE="\033[34m"
COLOR_CYAN="\033[36m"
COLOR_MAGENTA="\033[35m"

# ------------------------------------------------------------------------------
# Utility Functions
# ------------------------------------------------------------------------------

is_running() {
    local pid_file="$1"
    if [ -f "$pid_file" ]; then
        local pid
        pid=$(cat "$pid_file" 2>/dev/null || true)
        if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
    fi
    return 1
}

check_tcp() {
    local host="$1"
    local port="$2"
    python3 -c "import socket; s = socket.socket(); s.settimeout(0.6); res = s.connect_ex(('$host', int($port))); s.close(); exit(0 if res == 0 else 1)" 2>/dev/null
}

check_http() {
    local host="$1"
    local port="$2"
    local path="${3:-/}"
    python3 -c "import urllib.request; req = urllib.request.Request('http://$host:$port$path', headers={'User-Agent': 'Heimdall/1.0'}); res = urllib.request.urlopen(req, timeout=1.0); exit(0 if res.status in (200, 301, 302) else 1)" 2>/dev/null
}

print_header() {
    echo -e "${COLOR_CYAN}${COLOR_BOLD}=========================================================================${COLOR_RESET}"
    echo -e "${COLOR_BOLD}              HEIMDALL INDUSTRIAL OT DEVELOPMENT MANAGER                ${COLOR_RESET}"
    echo -e "${COLOR_CYAN}${COLOR_BOLD}=========================================================================${COLOR_RESET}"
}

# ------------------------------------------------------------------------------
# Database Lifecycle
# ------------------------------------------------------------------------------

ensure_database() {
    if [ -d "infra/database" ]; then
        if ! check_tcp 127.0.0.1 5432 || ! check_tcp 127.0.0.1 6379; then
            echo -e "${COLOR_BLUE}>> Ensuring PostgreSQL (5432) & Redis (6379) are active via Docker...${COLOR_RESET}"
            (cd infra/database && docker compose up -d 2>/dev/null || true)
            # Short wait for database readiness
            local retries=0
            while [ $retries -lt 15 ]; do
                if check_tcp 127.0.0.1 5432 && check_tcp 127.0.0.1 6379; then
                    echo -e "${COLOR_GREEN}  ✓ PostgreSQL & Redis are healthy.${COLOR_RESET}"
                    return 0
                fi
                sleep 0.5
                retries=$((retries + 1))
            done
        fi
    fi
}

# ------------------------------------------------------------------------------
# Windows Edge Agent Management
# ------------------------------------------------------------------------------

windows_check_kvm() {
    if [ -e "/dev/kvm" ] && [ -r "/dev/kvm" ] && [ -w "/dev/kvm" ]; then
        return 0
    fi
    return 1
}

windows_build_agent() {
    echo -e "${COLOR_BLUE}>> Publishing Windows Agent binary (win-x64, self-contained)...${COLOR_RESET}"
    mkdir -p "$SHARED_WIN_AGENT_DIR"
    dotnet publish agent/App.Agent.Daemon/App.Agent.Daemon.csproj \
        -r win-x64 \
        -c Release \
        --self-contained true \
        -o "$SHARED_WIN_AGENT_DIR"
    
    local exe_path="$SHARED_WIN_AGENT_DIR/App.Agent.Daemon.exe"
    if [ -f "$exe_path" ]; then
        local size_mb
        size_mb=$(du -m "$exe_path" | cut -f1)
        echo -e "${COLOR_GREEN}  ✓ Windows Agent binary ready: $exe_path (~${size_mb} MB)${COLOR_RESET}"
    fi
}

windows_start() {
    echo -e "${COLOR_BLUE}>> Starting Windows 10 LTSC Edge Agent Container (KVM)...${COLOR_RESET}"
    if ! windows_check_kvm; then
        echo -e "${COLOR_YELLOW}  ⚠ Warning: /dev/kvm not accessible. Container will run in emulation mode.${COLOR_RESET}"
    fi

    # Compile win-x64 binary if not already published
    if [ ! -f "$SHARED_WIN_AGENT_DIR/App.Agent.Daemon.exe" ]; then
        windows_build_agent
    fi

    docker compose --profile windows up -d windows-agent
    echo -e "${COLOR_GREEN}  ✓ Windows Agent container initialized.${COLOR_RESET}"
    echo -e "  • Web VNC:          ${COLOR_CYAN}http://localhost:8006${COLOR_RESET}"
    echo -e "  • Agent API:        ${COLOR_CYAN}http://localhost:5998${COLOR_RESET}"
    echo -e "  • TwinCAT ADS:      ${COLOR_CYAN}localhost:48898${COLOR_RESET} (AMS NetId 5.80.201.44.1.1:851)"
    echo -e "  • Minimal OPC UA:   ${COLOR_CYAN}opc.tcp://localhost:4840${COLOR_RESET}"
    echo -e "  • WinRM Management: ${COLOR_CYAN}http://localhost:5985${COLOR_RESET}"
    echo -e "  • RDP Remote:       ${COLOR_CYAN}localhost:3389${COLOR_RESET}"
}

windows_stop() {
    echo -e "${COLOR_BLUE}>> Stopping Windows Edge Agent container...${COLOR_RESET}"
    docker compose --profile windows stop windows-agent 2>/dev/null || true
    echo -e "${COLOR_GREEN}  ✓ Windows Edge Agent stopped.${COLOR_RESET}"
}

windows_restart() {
    windows_stop
    sleep 1
    windows_start
}

windows_status() {
    echo -e "${COLOR_BOLD}=== Windows Edge Agent Node Status ===${COLOR_RESET}"
    check_tcp 127.0.0.1 8006 && echo -e "  Web VNC Viewer:      ${COLOR_GREEN}● ONLINE (:8006)${COLOR_RESET}" || echo -e "  Web VNC Viewer:      ${COLOR_RED}○ OFFLINE (:8006)${COLOR_RESET}"
    check_tcp 127.0.0.1 5998 && echo -e "  Agent Config API:    ${COLOR_GREEN}● ONLINE (:5998)${COLOR_RESET}" || echo -e "  Agent Config API:    ${COLOR_RED}○ OFFLINE (:5998)${COLOR_RESET}"
    check_tcp 127.0.0.1 48898 && echo -e "  TwinCAT ADS Server:  ${COLOR_GREEN}● ONLINE (:48898)${COLOR_RESET}" || echo -e "  TwinCAT ADS Server:  ${COLOR_RED}○ OFFLINE (:48898)${COLOR_RESET}"
    check_tcp 127.0.0.1 4840 && echo -e "  Minimal OPC UA:      ${COLOR_GREEN}● ONLINE (:4840)${COLOR_RESET}" || echo -e "  Minimal OPC UA:      ${COLOR_RED}○ OFFLINE (:4840)${COLOR_RESET}"
    check_tcp 127.0.0.1 5985 && echo -e "  WinRM HTTP Service:  ${COLOR_GREEN}● ONLINE (:5985)${COLOR_RESET}" || echo -e "  WinRM HTTP Service:  ${COLOR_RED}○ OFFLINE (:5985)${COLOR_RESET}"
    check_tcp 127.0.0.1 3389 && echo -e "  RDP Remote Desktop:  ${COLOR_GREEN}● ONLINE (:3389)${COLOR_RESET}" || echo -e "  RDP Remote Desktop:  ${COLOR_RED}○ OFFLINE (:3389)${COLOR_RESET}"
}

windows_logs() {
    echo -e "${COLOR_BLUE}>> Streaming Windows Agent container logs (Ctrl+C to stop)...${COLOR_RESET}"
    docker compose --profile windows logs -f --tail=50 windows-agent
}

windows_launch() {
    echo -e "${COLOR_BLUE}>> Executing Windows Agent daemon launcher via WinRM...${COLOR_RESET}"
    python3 tools/launch_agent_win.py
}

windows_test() {
    echo -e "${COLOR_BLUE}>> Executing Windows Endpoint Integration Tests...${COLOR_RESET}"
    python3 tools/test_windows_agent.py --test
}

# ------------------------------------------------------------------------------
# Core Services Lifecycle
# ------------------------------------------------------------------------------

start_services() {
    local with_windows="${1:-true}"
    print_header
    echo -e "${COLOR_BOLD}Starting Heimdall Development Services...${COLOR_RESET}"

    # 1. Database
    ensure_database

    # 2. Backend API (.NET 10)
    if ! check_tcp 127.0.0.1 5099; then
        echo -e "${COLOR_BLUE}>> Starting Backend REST API & gRPC Ingestion with hot-reload...${COLOR_RESET}"
        (cd backend/App.Backend.Api && nohup dotnet watch run </dev/null > "$LOG_DIR/backend.log" 2>&1 & echo $! > "$PID_DIR/backend.pid")
        ln -sf "$LOG_DIR/backend.log" /tmp/heimdall-backend.log 2>/dev/null || true
    fi

    # 3. Nuxt 4 Web Frontend
    if ! check_tcp 127.0.0.1 3000; then
        echo -e "${COLOR_BLUE}>> Starting Nuxt 4 Web Frontend on http://localhost:3000 (Vite HMR)...${COLOR_RESET}"
        (cd "$SCRIPT_DIR/frontend/web" && nohup bun run dev </dev/null > "$LOG_DIR/frontend.log" 2>&1 & echo $! > "$PID_DIR/frontend.pid")
        ln -sf "$LOG_DIR/frontend.log" /tmp/heimdall-nuxt.log 2>/dev/null || true
    fi

    # 4. Industrial Edge Agent: Windows Docker Agent by default
    if [ "$with_windows" = "true" ]; then
        echo -e "${COLOR_BLUE}>> Using Windows 10 LTSC Docker Edge Agent (TwinCAT ADS & OPC UA)...${COLOR_RESET}"
        windows_start
    else
        # Host Linux Edge Agent Daemon fallback
        if ! is_running "$PID_DIR/agent.pid"; then
            echo -e "${COLOR_BLUE}>> Starting Host Linux Edge Agent Daemon with hot-reload...${COLOR_RESET}"
            (cd agent/App.Agent.Daemon && nohup dotnet watch run </dev/null > "$LOG_DIR/agent.log" 2>&1 & echo $! > "$PID_DIR/agent.pid")
            ln -sf "$LOG_DIR/agent.log" /tmp/heimdall-agent.log 2>/dev/null || true
        fi
    fi

    # 5. Industrial Edge Fleet Simulator
    if ! is_running "$PID_DIR/simulator.pid"; then
        echo -e "${COLOR_BLUE}>> Starting Edge Fleet Simulator (:5055)...${COLOR_RESET}"
        local python_bin="./venv/bin/python"
        [ ! -f "$python_bin" ] && python_bin="python3"
        (nohup $python_bin simulators/fleet/fleet_simulator.py </dev/null > "$LOG_DIR/simulator.log" 2>&1 & echo $! > "$PID_DIR/simulator.pid")
        ln -sf "$LOG_DIR/simulator.log" /tmp/heimdall-simulator.log 2>/dev/null || true
    fi

    echo -e "${COLOR_GREEN}${COLOR_BOLD}✓ Core development services successfully launched.${COLOR_RESET}"
}

stop_services() {
    local target="${1:-all}"
    echo -e "${COLOR_YELLOW}${COLOR_BOLD}========================================${COLOR_RESET}"
    echo -e "${COLOR_YELLOW}${COLOR_BOLD} Stopping Heimdall Services: $target    ${COLOR_RESET}"
    echo -e "${COLOR_YELLOW}${COLOR_BOLD}========================================${COLOR_RESET}"

    case "$target" in
        backend)
            [ -f "$PID_DIR/backend.pid" ] && kill "$(cat "$PID_DIR/backend.pid" 2>/dev/null)" 2>/dev/null || true
            pkill -f "dotnet watch.*backend/App.Backend.Api" 2>/dev/null || true
            pkill -f "App.Backend.Api" 2>/dev/null || true
            rm -f "$PID_DIR/backend.pid"
            echo "Backend API stopped."
            ;;
        frontend)
            [ -f "$PID_DIR/frontend.pid" ] && kill "$(cat "$PID_DIR/frontend.pid" 2>/dev/null)" 2>/dev/null || true
            pkill -f "bun.*frontend/web" 2>/dev/null || true
            pkill -f "nuxt/bin/nuxt" 2>/dev/null || true
            rm -f "$PID_DIR/frontend.pid"
            echo "Nuxt Frontend stopped."
            ;;
        agent)
            [ -f "$PID_DIR/agent.pid" ] && kill "$(cat "$PID_DIR/agent.pid" 2>/dev/null)" 2>/dev/null || true
            pkill -f "dotnet watch.*agent/App.Agent.Daemon" 2>/dev/null || true
            pkill -f "App.Agent.Daemon" 2>/dev/null || true
            rm -f "$PID_DIR/agent.pid"
            echo "Agent Daemon stopped."
            ;;
        simulator)
            [ -f "$PID_DIR/simulator.pid" ] && kill "$(cat "$PID_DIR/simulator.pid" 2>/dev/null)" 2>/dev/null || true
            pkill -f "fleet_simulator.py" 2>/dev/null || true
            rm -f "$PID_DIR/simulator.pid"
            echo "Fleet Simulator stopped."
            ;;
        windows|windows-agent|winagent)
            windows_stop
            ;;
        db|database)
            if [ -d "infra/database" ]; then
                (cd infra/database && docker compose down 2>/dev/null || true)
            fi
            echo "Database containers stopped."
            ;;
        all|*)
            # Kill Zellij session if active
            if command -v zellij >/dev/null 2>&1 && zellij list-sessions 2>/dev/null | grep -q "$SESSION_NAME"; then
                zellij kill-session "$SESSION_NAME" 2>/dev/null || true
            fi

            # Stop tracked PID processes
            for pid_file in "$PID_DIR"/*.pid; do
                if [ -f "$pid_file" ]; then
                    local pid
                    pid=$(cat "$pid_file" 2>/dev/null || true)
                    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
                        kill "$pid" 2>/dev/null || true
                    fi
                    rm -f "$pid_file"
                fi
            done

            # Stop dev processes
            pkill -f "dotnet watch.*backend/App.Backend.Api" 2>/dev/null || true
            pkill -f "dotnet watch.*agent/App.Agent.Daemon" 2>/dev/null || true
            pkill -f "dotnet run --project backend/App.Backend.Api" 2>/dev/null || true
            pkill -f "dotnet run --project agent/App.Agent.Daemon" 2>/dev/null || true
            pkill -f "bun.*frontend/web" 2>/dev/null || true
            pkill -f "nuxt/bin/nuxt" 2>/dev/null || true
            pkill -f "fleet_simulator.py" 2>/dev/null || true
            pkill -f "dev_manager.py" 2>/dev/null || true
            pkill -f "tools/tui.py" 2>/dev/null || true

            # Stop Windows agent container if running
            docker compose --profile windows stop windows-agent 2>/dev/null || true

            # Stop Database
            if [ -d "infra/database" ]; then
                (cd infra/database && docker compose down 2>/dev/null || true)
            fi
            echo -e "${COLOR_GREEN}All Heimdall services cleanly stopped.${COLOR_RESET}"
            ;;
    esac
}

clean_environment() {
    echo -e "${COLOR_RED}${COLOR_BOLD}=========================================================================${COLOR_RESET}"
    echo -e "${COLOR_RED}${COLOR_BOLD} Force cleaning all Heimdall processes, containers, and ports           ${COLOR_RESET}"
    echo -e "${COLOR_RED}${COLOR_BOLD}=========================================================================${COLOR_RESET}"
    stop_services all
    pkill -9 -f "App.Backend.Api" 2>/dev/null || true
    pkill -9 -f "App.Agent.Daemon" 2>/dev/null || true
    pkill -9 -f "dotnet exec" 2>/dev/null || true
    pkill -9 -f "dotnet run" 2>/dev/null || true
    pkill -9 -f "dotnet watch" 2>/dev/null || true
    pkill -9 -f "fleet_simulator.py" 2>/dev/null || true
    if command -v zellij >/dev/null 2>&1; then
        zellij delete-session "$SESSION_NAME" 2>/dev/null || true
    fi
    rm -rf "$PID_DIR"/*
    echo -e "${COLOR_GREEN}Environment cleaned. Ports 5099, 5001, 3000, 5998, 5055, 5432, 6379 released.${COLOR_RESET}"
}

restart_service() {
    local target="${1:-all}"
    case "$target" in
        windows|windows-agent|winagent)
            windows_restart
            ;;
        all)
            stop_services all
            sleep 1
            start_services false
            ;;
        *)
            stop_services "$target"
            sleep 1
            case "$target" in
                backend)
                    (cd backend/App.Backend.Api && nohup dotnet watch run </dev/null > "$LOG_DIR/backend.log" 2>&1 & echo $! > "$PID_DIR/backend.pid")
                    ;;
                frontend)
                    (cd "$SCRIPT_DIR/frontend/web" && nohup bun run dev </dev/null > "$LOG_DIR/frontend.log" 2>&1 & echo $! > "$PID_DIR/frontend.pid")
                    ;;
                agent)
                    (cd agent/App.Agent.Daemon && nohup dotnet watch run </dev/null > "$LOG_DIR/agent.log" 2>&1 & echo $! > "$PID_DIR/agent.pid")
                    ;;
                simulator)
                    local python_bin="./venv/bin/python"
                    [ ! -f "$python_bin" ] && python_bin="python3"
                    (nohup $python_bin simulators/fleet/fleet_simulator.py </dev/null > "$LOG_DIR/simulator.log" 2>&1 & echo $! > "$PID_DIR/simulator.pid")
                    ;;
                db|database)
                    ensure_database
                    ;;
            esac
            echo -e "${COLOR_GREEN}Restarted $target.${COLOR_RESET}"
            ;;
    esac
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
        windows|windows-agent|winagent)
            windows_logs
            ;;
        db|database)
            if [ -d "infra/database" ]; then
                (cd infra/database && docker compose logs -f --tail=50)
            fi
            ;;
        *)
            echo "Displaying latest log pointers:"
            echo "  • Backend API:      $LOG_DIR/backend.log"
            echo "  • Web Frontend:     $LOG_DIR/frontend.log"
            echo "  • Linux Agent:      $LOG_DIR/agent.log"
            echo "  • Fleet Simulator:  $LOG_DIR/simulator.log"
            echo "  • Windows Agent:    docker compose --profile windows logs windows-agent"
            echo ""
            echo "Usage: ./run_dev.sh logs <backend|frontend|agent|simulator|windows|db>"
            ;;
    esac
}

check_status() {
    python3 tools/dev_manager.py status "$@"
}

watch_status() {
    python3 tools/dev_manager.py watch "$@"
}

launch_tui() {
    python3 tools/tui.py
}

start_zellij() {
    if ! command -v zellij >/dev/null 2>&1; then
        echo -e "${COLOR_YELLOW}Zellij is not installed. Falling back to default TUI mode.${COLOR_RESET}"
        launch_tui
        return
    fi

    ensure_database

    if zellij list-sessions 2>/dev/null | grep -q "^$SESSION_NAME"; then
        echo -e "${COLOR_BLUE}Attaching to existing Zellij session: $SESSION_NAME...${COLOR_RESET}"
        exec zellij attach "$SESSION_NAME"
    else
        echo -e "${COLOR_BLUE}Launching Zellij workspace: $SESSION_NAME...${COLOR_RESET}"
        exec zellij --session "$SESSION_NAME" --layout "$LAYOUT_FILE"
    fi
}

# ------------------------------------------------------------------------------
# Main Dispatcher / Startup Routine
# ------------------------------------------------------------------------------

start_dev() {
    local mode="tui"
    local with_windows="true"

    while [ $# -gt 0 ]; do
        case "$1" in
            --daemon|-d|--background|--no-tui)
                mode="daemon"
                shift
                ;;
            --windows|--with-windows|-w)
                with_windows="true"
                shift
                ;;
            --no-windows|--without-windows|--linux-agent)
                with_windows="false"
                shift
                ;;
            --zellij)
                mode="zellij"
                shift
                ;;
            --tui)
                mode="tui"
                shift
                ;;
            *)
                shift
                ;;
        esac
    done

    # If stdin/stdout is not a TTY (CI, pipes, background jobs), force daemon mode
    if ! [ -t 0 ] || ! [ -t 1 ]; then
        mode="daemon"
    fi

    # 1. Start core development services
    start_services "$with_windows"

    # 2. Launch Interface Mode
    case "$mode" in
        zellij)
            start_zellij
            ;;
        daemon)
            echo ""
            echo -e "${COLOR_GREEN}${COLOR_BOLD}Heimdall is running in background daemon mode.${COLOR_RESET}"
            echo -e "Use ${COLOR_CYAN}./run_dev.sh tui${COLOR_RESET} to launch interactive TUI dashboard."
            echo -e "Use ${COLOR_CYAN}./run_dev.sh status${COLOR_RESET} to view service status."
            echo -e "Use ${COLOR_CYAN}./run_dev.sh stop${COLOR_RESET} to stop all services."
            ;;
        tui|*)
            echo -e "${COLOR_CYAN}Launching interactive Heimdall TUI (staying alive by default)...${COLOR_RESET}"
            sleep 0.8
            launch_tui
            ;;
    esac
}

output_completion() {
    local shell_type="${1:-bash}"
    if [ "$shell_type" = "zsh" ]; then
        cat "$SCRIPT_DIR/tools/completions/heimdall_completion.zsh"
    else
        cat "$SCRIPT_DIR/tools/completions/heimdall_completion.bash"
    fi
}

install_completions() {
    local target_shell="${1:-auto}"
    if [ "$target_shell" = "auto" ]; then
        if [ -n "$ZSH_VERSION" ] || [[ "$SHELL" =~ "zsh" ]]; then
            target_shell="zsh"
        else
            target_shell="bash"
        fi
    fi

    echo -e "${COLOR_BLUE}>> Installing Heimdall tab completions for ${target_shell}...${COLOR_RESET}"
    if [ "$target_shell" = "zsh" ]; then
        local zsh_dir="$HOME/.zsh/completions"
        mkdir -p "$zsh_dir"
        cp "$SCRIPT_DIR/tools/completions/heimdall_completion.zsh" "$zsh_dir/_run_dev.sh"
        if ! grep -q "fpath+=~/.zsh/completions" "$HOME/.zshrc" 2>/dev/null; then
            echo 'fpath=(~/.zsh/completions $fpath)' >> "$HOME/.zshrc"
            echo 'autoload -Uz compinit && compinit' >> "$HOME/.zshrc"
        fi
        echo -e "${COLOR_GREEN}  ✓ Zsh completion installed into $zsh_dir/_run_dev.sh${COLOR_RESET}"
        echo -e "  To activate, run: ${COLOR_CYAN}source ~/.zshrc${COLOR_RESET}"
    else
        local bash_file="$HOME/.bash_completion.d/heimdall"
        mkdir -p "$HOME/.bash_completion.d"
        cp "$SCRIPT_DIR/tools/completions/heimdall_completion.bash" "$bash_file"
        if ! grep -q "source ~/.bash_completion.d/heimdall" "$HOME/.bashrc" 2>/dev/null; then
            echo '[[ -f ~/.bash_completion.d/heimdall ]] && source ~/.bash_completion.d/heimdall' >> "$HOME/.bashrc"
        fi
        echo -e "${COLOR_GREEN}  ✓ Bash completion installed into $bash_file${COLOR_RESET}"
        echo -e "  To activate, run: ${COLOR_CYAN}source ~/.bashrc${COLOR_RESET}"
    fi
}

show_help() {
    print_header
    echo -e "${COLOR_BOLD}Usage:${COLOR_RESET} ./run_dev.sh [COMMAND] [OPTIONS]"
    echo ""
    echo -e "${COLOR_BOLD}Core Commands:${COLOR_RESET}"
    echo -e "  ${COLOR_CYAN}start [options]${COLOR_RESET}     Start development services and launch interactive TUI by default"
    echo -e "                      Options: ${COLOR_DIM}--daemon, -d${COLOR_RESET} (background daemon without TUI)"
    echo -e "                               ${COLOR_DIM}--windows, -w${COLOR_RESET} (also boot Windows 10 LTSC KVM container)"
    echo -e "                               ${COLOR_DIM}--zellij${COLOR_RESET} (multiplexer session)"
    echo -e "  ${COLOR_CYAN}stop [service]${COLOR_RESET}      Stop all services or a specific subsystem (backend, frontend, agent, windows, db)"
    echo -e "  ${COLOR_CYAN}clean${COLOR_RESET}               Force kill lingering host processes, release all ports, stop containers"
    echo -e "  ${COLOR_CYAN}restart [service]${COLOR_RESET}   Restart all or a specific service with hot-reload"
    echo -e "  ${COLOR_CYAN}status [-w|--json]${COLOR_RESET}  Display service health matrix (pass -w for live monitor, --json for JSON)"
    echo -e "  ${COLOR_CYAN}tui, monitor, watch${COLOR_RESET} Launch the interactive full-screen TUI dashboard"
    echo -e "  ${COLOR_CYAN}logs [service]${COLOR_RESET}      Stream logs (backend, frontend, agent, simulator, windows, db)"
    echo ""
    echo -e "${COLOR_BOLD}Windows Agent Subcommands:${COLOR_RESET}"
    echo -e "  ${COLOR_CYAN}windows start${COLOR_RESET}       Start the Windows 10 LTSC KVM Docker container"
    echo -e "  ${COLOR_CYAN}windows stop${COLOR_RESET}        Stop the Windows Agent container"
    echo -e "  ${COLOR_CYAN}windows restart${COLOR_RESET}     Restart the Windows Agent container"
    echo -e "  ${COLOR_CYAN}windows status${COLOR_RESET}      Show Windows Agent ports (8006 VNC, 5998 API, 48898 ADS, 4840 OPC)"
    echo -e "  ${COLOR_CYAN}windows logs${COLOR_RESET}        Stream live container logs"
    echo -e "  ${COLOR_CYAN}windows build${COLOR_RESET}       Publish win-x64 agent binary and build Dockerfile"
    echo -e "  ${COLOR_CYAN}windows launch${COLOR_RESET}      Trigger agent daemon startup inside Windows via WinRM"
    echo -e "  ${COLOR_CYAN}windows test${COLOR_RESET}        Run Windows endpoint integration test suite"
    echo ""
    echo -e "${COLOR_BOLD}Verification & Tools:${COLOR_RESET}"
    echo -e "  ${COLOR_CYAN}test [subsystem]${COLOR_RESET}    Run test suite (all, backend, frontend, windows, smoke, seed)"
    echo -e "  ${COLOR_CYAN}docker [action]${COLOR_RESET}     Manage containerized development stack (up, down, ps, logs)"
    echo -e "  ${COLOR_CYAN}completion [shell]${COLOR_RESET}  Output tab completion code (bash, zsh)"
    echo -e "  ${COLOR_CYAN}install-completions${COLOR_RESET} Install tab completion hooks into ~/.bashrc or ~/.zshrc"
    echo -e "  ${COLOR_CYAN}help, -h, --help${COLOR_RESET}    Show this help documentation"
}

# ------------------------------------------------------------------------------
# Command Routing
# ------------------------------------------------------------------------------

CMD="${1:-start}"
shift || true

case "$CMD" in
    start)
        start_dev "$@"
        ;;
    stop)
        stop_services "$1"
        ;;
    clean)
        clean_environment
        ;;
    restart)
        restart_service "$1"
        ;;
    status)
        check_status "$@"
        ;;
    monitor|watch|tui)
        launch_tui
        ;;
    windows|winagent)
        ACTION="${1:-status}"
        case "$ACTION" in
            start|up)       windows_start ;;
            stop|down)      windows_stop ;;
            restart)        windows_restart ;;
            status)         windows_status ;;
            logs)           windows_logs ;;
            build)          windows_build_agent ;;
            launch)         windows_launch ;;
            test)           windows_test ;;
            vnc)            echo "http://localhost:8006" ;;
            api)            echo "http://localhost:5998" ;;
            *)              windows_status ;;
        esac
        ;;
    logs)
        show_logs "$1"
        ;;
    docker|compose)
        docker compose "$@"
        ;;
    zellij)
        start_zellij
        ;;
    daemon)
        start_dev --daemon "$@"
        ;;
    test)
        SUBCMD="${1:-all}"
        case "$SUBCMD" in
            backend)
                dotnet test Heimdall.sln
                ;;
            frontend)
                (cd frontend/web && bun run test)
                ;;
            windows)
                windows_test
                ;;
            seed)
                python3 seed_data/seed_pipeline.py --validate
                ;;
            smoke)
                python3 simulators/fleet/fleet_simulator.py --smoke-test --count 5
                ;;
            all|*)
                python3 tools/dev_manager.py test
                ;;
        esac
        ;;
    build)
        TARGET="${1:-agent-win}"
        case "$TARGET" in
            agent-win|windows)
                windows_build_agent
                ;;
            frontend)
                (cd frontend/web && bun run build)
                ;;
            backend)
                dotnet build backend/App.Backend.Api/App.Backend.Api.csproj
                ;;
            *)
                windows_build_agent
                ;;
        esac
        ;;
    completion)
        if [ "$1" = "install" ]; then
            install_completions "$2"
        else
            output_completion "$1"
        fi
        ;;
    install-completions)
        install_completions "$1"
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        echo -e "${COLOR_RED}Unknown command: $CMD${COLOR_RESET}"
        echo "Run './run_dev.sh help' for usage instructions."
        exit 1
        ;;
esac
