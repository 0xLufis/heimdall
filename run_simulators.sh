#!/bin/bash

# Script to run (and stop) multiple instances of the Heimdall PC simulator.

# --- Configuration ---
# Activate virtual environment if it exists
if [ -d "venv" ]; then
    source "venv/bin/activate"
    echo "Python virtual environment activated."
fi
PYTHON_CMD="python"
SIMULATOR_SCRIPT="simulate_pcs.py"
PID_FILE_DIR="/tmp/heimdall_sims"
CLIENTS=(
    "ROBOT-CELL-01"
    "ROBOT-CELL-02"
    "ROBOT-CELL-03"
    "ROBOT-CELL-04"
    "ROBOT-CELL-05"
    "ASSEMBLY-ST-01"
    "ASSEMBLY-ST-02"
    "ASSEMBLY-ST-03"
    "ASSEMBLY-ST-04"
    "ASSEMBLY-ST-05"
)

# --- Functions ---
start() {
    if [ ! -f "$SIMULATOR_SCRIPT" ]; then
        echo "Simulator script '$SIMULATOR_SCRIPT' not found!"
        exit 1
    fi
    
    mkdir -p "$PID_FILE_DIR"
    
    echo "Starting simulators for ${#CLIENTS[@]} clients..."
    
    for client in "${CLIENTS[@]}"; do
        log_file="$PID_FILE_DIR/${client}.log"
        pid_file="$PID_FILE_DIR/${client}.pid"
        
        if [ -f "$pid_file" ]; then
            if ps -p $(cat "$pid_file") > /dev/null; then
                echo "Simulator for $client is already running (PID $(cat "$pid_file"))."
                continue
            fi
        fi

        # Run the python script in the background
        # Redirect stdout and stderr to a log file
        nohup $PYTHON_CMD "$SIMULATOR_SCRIPT" --client "$client" > "$log_file" 2>&1 &
        
        # Write the PID of the background process to its pid file
        echo $! > "$pid_file"
        
        echo "  -> Started simulator for $client (PID $!, Log: $log_file)"
        sleep 0.2 # Stagger the starts slightly
    done
    
    echo "All simulators started. You can monitor their logs in '$PID_FILE_DIR'."
    echo "Use './run_simulators.sh status' to check their status."
}

stop() {
    echo "Stopping all simulators..."
    
    if [ ! -d "$PID_FILE_DIR" ]; then
        echo "PID directory not found. Nothing to stop."
        exit 0
    fi

    for pid_file in "$PID_FILE_DIR"/*.pid; do
        if [ -f "$pid_file" ]; then
            pid=$(cat "$pid_file")
            client_name=$(basename "$pid_file" .pid)
            
            if ps -p "$pid" > /dev/null; then
                echo "  -> Stopping simulator for $client_name (PID $pid)..."
                kill "$pid"
                # Wait a moment for the process to terminate
                sleep 0.5 
                if ps -p "$pid" > /dev/null; then
                    echo "  -> Process $pid did not stop gracefully, sending SIGKILL."
                    kill -9 "$pid"
                fi
            else
                echo "  -> Simulator for $client_name is not running."
            fi
            rm -f "$pid_file"
        fi
    done
    
    echo "All simulator processes have been stopped."
    echo "You may need to manually clean up log files in '$PID_FILE_DIR'."
}

status() {
    echo "Checking status of simulators..."
    
    if [ ! -d "$PID_FILE_DIR" ]; then
        echo "PID directory not found. No simulators appear to be running."
        exit 0
    fi
    
    running_count=0
    for client in "${CLIENTS[@]}"; do
        pid_file="$PID_FILE_DIR/${client}.pid"
        
        if [ -f "$pid_file" ]; then
            pid=$(cat "$pid_file")
            if ps -p "$pid" > /dev/null; then
                echo "  [RUNNING]   $client (PID $pid)"
                running_count=$((running_count+1))
            else
                echo "  [STOPPED]   $client (stale PID file found)"
            fi
        else
            echo "  [STOPPED]   $client"
        fi
    done
    echo "---"
    echo "$running_count of ${#CLIENTS[@]} simulators are running."
}

# --- Main Logic ---
case "$1" in
    start)
        start
        ;;
    stop)
        stop
        ;;
    status)
        status
        ;;
    *)
        echo "Usage: $0 {start|stop|status}"
        exit 1
        ;;
esac

exit 0
