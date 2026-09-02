# bash completion for Heimdall scripts (run_dev.sh, run_simulators.sh, dev_manager.py, seed_pipeline.py, fleet_simulator.py)

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
    _init_completion || return

    local commands="start stop clean restart status monitor watch docker logs zellij daemon completion help"
    local services="backend frontend agent simulator db all"

    if [ "$cword" -eq 1 ]; then
        if [[ "$cur" == -* ]]; then
            COMPREPLY=( $(compgen -W "--help -h --zellij --quiet -q" -- "$cur") )
        else
            COMPREPLY=( $(compgen -W "$commands" -- "$cur") )
        fi
        return 0
    fi

    local subcmd="${words[1]}"
    case "$subcmd" in
        logs|restart)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "$services" -- "$cur") )
            fi
            ;;
        completion)
            if [ "$cword" -eq 2 ]; then
                COMPREPLY=( $(compgen -W "bash zsh" -- "$cur") )
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
    _init_completion || return

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
    _init_completion || return

    local commands="status watch check-health test"
    if [ "$cword" -eq 1 ] || [ "${words[1]}" = "python" -a "$cword" -eq 2 ] || [ "${words[1]}" = "python3" -a "$cword" -eq 2 ]; then
        COMPREPLY=( $(compgen -W "$commands --help -h" -- "$cur") )
        return 0
    fi

    if [[ "$cur" == -* ]]; then
        COMPREPLY=( $(compgen -W "--help -h" -- "$cur") )
    fi
}

_heimdall_seed_pipeline_completion() {
    local cur prev words cword
    _init_completion || return

    local flags="--generate-all --validate --help -h"
    COMPREPLY=( $(compgen -W "$flags" -- "$cur") )
}

_heimdall_fleet_simulator_completion() {
    local cur prev words cword
    _init_completion || return

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

# Register completion handlers for scripts
complete -F _heimdall_run_dev_completion ./run_dev.sh run_dev.sh run_dev
complete -F _heimdall_run_simulators_completion ./run_simulators.sh run_simulators.sh run_simulators
complete -F _heimdall_dev_manager_completion ./tools/dev_manager.py tools/dev_manager.py dev_manager.py
complete -F _heimdall_seed_pipeline_completion ./seed_data/seed_pipeline.py seed_data/seed_pipeline.py seed_pipeline.py
complete -F _heimdall_fleet_simulator_completion ./simulators/edge-fleet-simulator/fleet_simulator.py fleet_simulator.py
