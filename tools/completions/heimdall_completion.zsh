#compdef run_dev.sh run_simulators.sh dev_manager.py seed_pipeline.py fleet_simulator.py heimdall

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
    local -a commands services win_actions docker_actions test_subsystems build_targets
    commands=(
        'start:Start all services and launch interactive TUI dashboard by default'
        'stop:Stop all running services or a specific subsystem'
        'clean:Force kill lingering host processes and free all network ports'
        'restart:Restart all services or a specific subsystem with hot-reload'
        'status:Display real-time status matrix for all services (supports -w and --json)'
        'tui:Launch interactive full-screen TUI dashboard'
        'monitor:Launch continuous, live-updating service health dashboard'
        'watch:Launch continuous, live-updating service health dashboard'
        'windows:Manage Windows 10 LTSC KVM Docker container (start, stop, logs, test)'
        'docker:Manage containerized development stack (up, down, build, logs, ps)'
        'daemon:Start development services in background daemon mode'
        'logs:Tail log streams for a specific subsystem'
        'test:Execute verification test suites (all, backend, frontend, windows)'
        'build:Compile binaries or container images (agent-win, frontend, backend)'
        'zellij:Launch interactive multi-pane Zellij terminal workspace'
        'completion:Output shell completion script for bash or zsh'
        'install-completions:Install tab completion hooks into ~/.bashrc or ~/.zshrc'
        'help:Show help message'
    )
    services=(
        'backend:Backend ASP.NET Core REST API & gRPC Service'
        'frontend:Nuxt 4 Web Frontend & Nitro BFF'
        'agent:Linux Industrial Edge Agent Daemon'
        'simulator:Industrial Edge Fleet Simulator'
        'windows:Windows 10 LTSC Edge Agent Container'
        'windows-agent:Windows 10 LTSC Edge Agent Container'
        'db:PostgreSQL & Redis Database Docker Services'
        'all:All subsystems'
    )
    win_actions=(
        'start:Start the Windows 10 LTSC KVM container'
        'stop:Stop the Windows Agent container'
        'restart:Restart the Windows Agent container'
        'status:Check Windows container port matrix (VNC, ADS, OPC UA, WinRM)'
        'logs:Stream Windows container live logs'
        'build:Publish win-x64 agent binary and build Dockerfile'
        'launch:Trigger agent startup inside Windows via WinRM'
        'test:Run Windows endpoint integration test suite'
        'vnc:Print Web VNC connection URL (http://localhost:8006)'
        'api:Print Agent Web API connection URL (http://localhost:5998)'
    )
    docker_actions=(
        'up:Start all container services'
        'down:Stop and remove container services'
        'restart:Restart container services'
        'logs:Tail container log output'
        'ps:List running container processes'
        'build:Rebuild Docker container images'
    )
    test_subsystems=(
        'all:Run complete test suite (Seed, xUnit, Vitest, Smoke)'
        'backend:Run .NET xUnit backend test suite (162 tests)'
        'frontend:Run Nuxt Vitest frontend unit tests (275 tests)'
        'windows:Run Windows endpoint integration tests'
        'seed:Validate plant dataset seed pipeline'
        'smoke:Run fleet simulator gRPC smoke test'
    )
    build_targets=(
        'agent-win:Publish win-x64 self-contained agent binary'
        'windows:Publish win-x64 agent and build Docker container'
        'frontend:Build Nuxt production bundle'
        'backend:Build ASP.NET Core API project'
        'all:Build all targets'
    )

    if (( CURRENT == 2 )); then
        _describe -t commands 'command' commands
    elif (( CURRENT == 3 )); then
        case "$words[2]" in
            logs|restart|stop)
                _describe -t services 'service' services
                ;;
            windows|winagent)
                _describe -t win_actions 'windows action' win_actions
                ;;
            docker|compose)
                _describe -t docker_actions 'docker action' docker_actions
                ;;
            test)
                _describe -t test_subsystems 'test subsystem' test_subsystems
                ;;
            build)
                _describe -t build_targets 'build target' build_targets
                ;;
            completion)
                _values 'shell' 'bash' 'zsh' 'install'
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
    local -a commands win_actions
    commands=(
        'status:Display formatted service status and port matrix'
        'watch:Continuously refresh service status'
        'monitor:Continuously refresh service status'
        'check-health:Run automated HTTP/TCP health checks and exit'
        'test:Execute full end-to-end test suite (Seed, xUnit, Vitest, Smoke)'
        'test-windows:Run Windows endpoint integration tests'
        'tui:Launch interactive full-screen TUI dashboard'
        'windows:Manage Windows 10 LTSC container'
    )
    win_actions=(
        'start:Start the Windows 10 LTSC KVM container'
        'stop:Stop the Windows Agent container'
        'restart:Restart the Windows Agent container'
        'status:Check Windows container port matrix'
        'logs:Stream Windows container logs'
        'build:Publish win-x64 agent binary'
        'launch:Trigger agent startup inside Windows'
        'test:Run Windows endpoint integration test suite'
    )

    if (( CURRENT == 2 )); then
        _describe -t commands 'command' commands
    elif (( CURRENT == 3 && words[2] == "windows" )); then
        _describe -t win_actions 'windows action' win_actions
    fi
}

# Bind completion handlers
compdef _heimdall_run_dev run_dev.sh ./run_dev.sh run_dev heimdall
compdef _heimdall_run_simulators run_simulators.sh ./run_simulators.sh run_simulators
compdef _heimdall_dev_manager dev_manager.py ./tools/dev_manager.py tools/dev_manager.py
