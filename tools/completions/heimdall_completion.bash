# bash completion for Heimdall scripts (run_dev.sh, run_simulators.sh, dev_manager.py, seed_pipeline.py, fleet_simulator.py)

# Safe fallback for _init_completion in bare bash environments
_heimdall_init_completion() {
    if declare -F _init_completion >/dev/null 2>&1; then
        _init_completion || return 1
    else
        COMPREPLY=()
        cur="${COMP_WORDS[COMP_CWORD]}"
        prev="${COMP_WORDS[COMP_CWORD-1]}"
        words=("${COMP_WORDS[@]}")
        cword=$COMP_CWORD
    fi
    return 0
}

_heimdall_get_clients() {
    local csv_file="seed_data/inventory_seed.csv"
    if [ -f "$csv_file" ]; then
        awk -F',' '$1=="ClientPc"{print $2}' "$csv_file" 2>/dev/null | head -n 50
    else
        echo "ROBOT-CELL-01 ROBOT-CELL-02 ROBOT-CELL-03 ROBOT-CELL-04 ROBOT-CELL-05 ASSEMBLY-ST-01 ASSEMBLY-ST-02 ASSEMBLY-ST-03 ASSEMBLY-ST-04 ASSEMBLY-ST-05"
    fi
}

_heimdall_run_dev_completion() {
    local cur prev words cword
    _heimdall_init_completion || return

    local commands="start stop clean restart status monitor watch tui windows docker logs test build zellij daemon completion install-completions help"
    local services="backend frontend agent simulator windows windows-agent db all"
    local win_actions="start stop restart status logs build launch test vnc api"
    local docker_actions="up down restart logs ps build start stop"
    local test_subsystems="all backend frontend windows seed smoke"
    local build_targets="agent-win windows frontend backend all"

    if [ "$cword" -eq 1 ]; then
        if [[ "$cur" == -* ]]; then
            COMPREPLY=( $(compgen -W "--help -h --daemon -d --windows -w --zellij --tui --no-tui" -- "$cur") )
        else
            COMPREPLY=( $(compgen -W "$commands" -- "$cur") )
        fi
        return 0
    fi

    local subcmd="${words[1]}"
    case "$subcmd" in
        start)
            COMPREPLY=( $(compgen -W "--daemon -d --windows -w --with-windows --zellij --tui --no-tui --help -h" -- "$cur") )
            ;;
        logs|restart|stop)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "$services" -- "$cur") )
            fi
            ;;
        windows|winagent)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "$win_actions" -- "$cur") )
            fi
            ;;
        docker|compose)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "$docker_actions" -- "$cur") )
            fi
            ;;
        test)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "$test_subsystems" -- "$cur") )
            fi
            ;;
        build)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "$build_targets" -- "$cur") )
            fi
            ;;
        status)
            COMPREPLY=( $(compgen -W "-w --watch --json --no-windows --help -h" -- "$cur") )
            ;;
        completion)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "bash zsh install" -- "$cur") )
            fi
            ;;
        *)
            if [[ "$cur" == -* ]]; then
                COMPREPLY=( $(compgen -W "--help -h" -- "$cur") )
            fi
            ;;
    esac
}

_heimdall_run_simulators_completion() {
    local cur prev words cword
    _heimdall_init_completion || return

    local commands="start stop restart status logs completion help"

    if [ "$cword" -eq 1 ]; then
        if [[ "$cur" == -* ]]; then
            COMPREPLY=( $(compgen -W "--client --help -h" -- "$cur") )
        else
            COMPREPLY=( $(compgen -W "$commands" -- "$cur") )
        fi
        return 0
    fi

    case "$prev" in
        --client|-c|logs)
            local clients=$(_heimdall_get_clients)
            COMPREPLY=( $(compgen -W "$clients" -- "$cur") )
            return 0
            ;;
        completion)
            COMPREPLY=( $(compgen -W "bash zsh" -- "$cur") )
            return 0
            ;;
    esac

    if [[ "$cur" == -* ]]; then
        COMPREPLY=( $(compgen -W "--client -c --help -h" -- "$cur") )
    fi
}

_heimdall_dev_manager_completion() {
    local cur prev words cword
    _heimdall_init_completion || return

    local commands="status watch monitor check-health test test-windows tui windows"
    local win_actions="start stop restart status logs build launch test"

    if [ "$cword" -eq 1 ] || [ "${words[1]}" = "python" -a "$cword" -eq 2 ] || [ "${words[1]}" = "python3" -a "$cword" -eq 2 ]; then
        COMPREPLY=( $(compgen -W "$commands --help -h" -- "$cur") )
        return 0
    fi

    if [ "${words[1]}" = "windows" ] && [ "$cword" -eq 2 ]; then
        COMPREPLY=( $(compgen -W "$win_actions" -- "$cur") )
        return 0
    fi

    if [[ "$cur" == -* ]]; then
        COMPREPLY=( $(compgen -W "--interval --json --no-windows --help -h" -- "$cur") )
    fi
}

_heimdall_seed_pipeline_completion() {
    local cur prev words cword
    _heimdall_init_completion || return

    local flags="--generate-all --validate --help -h"
    COMPREPLY=( $(compgen -W "$flags" -- "$cur") )
}

_heimdall_fleet_simulator_completion() {
    local cur prev words cword
    _heimdall_init_completion || return

    case "$prev" in
        --grpc-host)
            COMPREPLY=( $(compgen -W "localhost:5001 127.0.0.1:5001" -- "$cur") )
            return 0
            ;;
        --fault-rate)
            COMPREPLY=( $(compgen -W "0.0 0.02 0.05 0.10 0.20" -- "$cur") )
            return 0
            ;;
        --count)
            COMPREPLY=( $(compgen -W "5 10 25 50 100 500" -- "$cur") )
            return 0
            ;;
    esac

    local flags="--grpc-host --fault-rate --smoke-test --count --help -h"
    COMPREPLY=( $(compgen -W "$flags" -- "$cur") )
}

# Register completion handlers for scripts and aliases
complete -F _heimdall_run_dev_completion ./run_dev.sh run_dev.sh run_dev heimdall
complete -F _heimdall_run_simulators_completion ./run_simulators.sh run_simulators.sh run_simulators
complete -F _heimdall_dev_manager_completion ./tools/dev_manager.py tools/dev_manager.py dev_manager.py
complete -F _heimdall_seed_pipeline_completion ./seed_data/seed_pipeline.py seed_data/seed_pipeline.py seed_pipeline.py
complete -F _heimdall_fleet_simulator_completion ./simulators/fleet/fleet_simulator.py fleet_simulator.py
