#!/bin/bash

echo "========================================"
echo " Starting Heimdall Development Server"
echo "========================================"

# Function to clean up background processes on exit
cleanup() {
    echo "Shutting down services..."
    kill $(jobs -p) 2>/dev/null
    exit
}

# Trap termination signals to clean up child processes
trap cleanup SIGINT SIGTERM EXIT

# 1. Ensure Database is running
echo "[1/4] Starting Database via Docker Compose..."
cd infra/database
docker compose up -d
cd ../../

# 2. Start Backend API
echo "[2/4] Starting .NET Backend API (Port 5099)..."
cd backend/App.Backend.Api
dotnet run &
cd ../../

# Wait a few seconds to let the backend initialize
sleep 3

# 3. Start Nuxt Frontend
echo "[3/4] Starting Nuxt Frontend (Port 3000)..."
cd frontend/nuxt-app
bun run dev &
cd ../../

# 4. Start the local Agent Daemon
echo "[4/5] Starting Local Agent Daemon..."
cd agent/App.Agent.Daemon
dotnet run &
cd ../../

# 5. Start PC Simulator
echo "[5/5] Starting Client PC Simulator..."
source venv/bin/activate
python simulate_pcs.py &
deactivate
cd ../../

echo "========================================"
echo " All services are running!"
echo " Frontend:  http://localhost:3000"
echo " Backend:   http://localhost:5099"
echo " Press Ctrl+C to stop all services."
echo "========================================"

# Wait for all background jobs to finish
wait
