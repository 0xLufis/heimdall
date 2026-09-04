#compdef run_dev.sh run_simulators.sh dev_manager.py seed_pipeline.py fleet_simulator.py

# Ensure compinit is loaded
if ! type compdef >/dev/null 2>&1; then
    autoload -Uz compinit && compinit -i
fi

_heimdall_clients() {
    local -a clients
    local csv_file="seed_data/inventory_seed.csv"
    if [[ -f "$csv_file" ]]; then
        clients=(${(f)"$(awk -F',' '$1=="ClientPc"{print $2}' "$csv_file" 2>/dev/null | head -n 50)"})
    else
        clients=(
            "ROBOT-CELL-01" "ROBOT-CELL-02" "ROBOT-CELL-03" "ROBOT-CELL-04" "ROBOT-CELL-05"
            "ASSEMBLY-ST-01" "ASSEMBLY-ST-02" "ASSEMBLY-ST-03" "ASSEMBLY-ST-04" "ASSEMBLY-ST-05"
        )
    fi
    _describe -t clients 'client PC' clients
}

_heimdall_run_dev() {
    local -a commands services
    commands=(
        'start:Start all background services (PostgreSQL, Backend API, Nuxt Frontend, Agent Daemon, Fleet Simulator)'
        'stop:Stop all running Heimdall background services and processes'
        'clean:Force kill all lingering host processes and free all network ports'
        'restart:Restart all services or a specific subsystem'
        'status:Display real-time status matrix for all services (supports -w for live monitor)'
        'monitor:Launch continuous, live-updating service health dashboard'
        'watch:Launch continuous, live-updating service health dashboard'
        'docker:Manage containerized development stack (up, down, build, logs, ps)'
        'daemon:Start development services in background daemon mode'
        'logs:Tail log streams for a specific subsystem'
        'zellij:Launch interactive multi-pane Zellij terminal workspace'
        'completion:Output shell completion script for bash or zsh'
        'help:Show help message'
    )
    services=(
        'backend:Backend ASP.NET Core REST API & gRPC Service'
        'frontend:Nuxt 4 Web Frontend & Nitro BFF'
        'agent:Heimdall Industrial Edge Daemon'
        'simulator:Industrial Edge Fleet Simulator'
        'db:PostgreSQL Database Docker Service'
        'all:All subsystems'
    )

    if (( CURRENT == 2 )); then
        _describe -t commands 'command' commands
    elif (( CURRENT == 3 )); then
        case "$words[2]" in
            logs|restart)
                _describe -t services 'service' services
                ;;
            completion)
                _values 'shell' 'bash' 'zsh'
                ;;
        esac
    fi
}

_heimdall_run_simulators() {
    local -a commands
    commands=(
        'start:Start simulator processes for edge nodes'
        'stop:Stop active simulator processes'
        'restart:Restart all active simulators'
        'status:Check status of simulated client nodes'
        'logs:Tail simulator logs for a client node'
        'completion:Output shell completion script'
        'help:Show help message'
    )

    if (( CURRENT == 2 )); then
        _describe -t commands 'command' commands
    elif (( CURRENT == 3 )); then
        case "$words[2]" in
            logs|start|stop)
                _heimdall_clients
                ;;
            completion)
                _values 'shell' 'bash' 'zsh'
                ;;
        esac
    fi
}

_heimdall_dev_manager() {
    local -a commands
    commands=(
        'status:Display formatted service status and port matrix'
        'check-health:Run automated HTTP/TCP health checks and exit'
        'test:Execute full end-to-end test suite (Seed, xUnit, Vitest, Smoke)'
    )

    if (( CURRENT == 2 )); then
        _describe -t commands 'command' commands
    fi
}

_heimdall_seed_pipeline() {
    _arguments \
        '--generate-all[Generate enterprise inventory CSV and transactional SQL seed]' \
        '--validate[Validate referential integrity across seed data]' \
        '(-h --help)'{-h,--help}'[Show help message]'
}

_heimdall_fleet_simulator() {
    _arguments \
        '--grpc-host[Target gRPC host and port (default: localhost:5001)]:_hosts' \
        '--fault-rate[Simulated anomaly injection rate (0.0 to 1.0)]' \
        '--smoke-test[Run non-interactive smoke test and exit]' \
        '--count[Number of node heartbeats for smoke test]' \
        '(-h --help)'{-h,--help}'[Show help message]'
}

# Bind to script names
compdef _heimdall_run_dev run_dev.sh ./run_dev.sh
compdef _heimdall_run_simulators run_simulators.sh ./run_simulators.sh
compdef _heimdall_dev_manager dev_manager.py ./tools/dev_manager.py
compdef _heimdall_seed_pipeline seed_pipeline.py ./seed_data/seed_pipeline.py
compdef _heimdall_fleet_simulator fleet_simulator.py ./simulators/fleet/fleet_simulator.py
