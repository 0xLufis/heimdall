#!/usr/bin/env bash
# Heimdall Industrial Edge Agent - Linux Service Installer
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
   echo "Error: This installation script must be run as root (or with sudo)." >&2
   exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BINARY_SOURCE="${1:-${SCRIPT_DIR}/heimdall-agent}"
INSTALL_BIN="/usr/local/bin/heimdall-agent"
SERVICE_NAME="heimdall-agent"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
CONFIG_DIR="/etc/heimdall"
DATA_DIR="/var/lib/heimdall-agent"
LOG_DIR="/var/log/heimdall-agent"
AGENT_USER="heimdall-agent"

echo "=== Installing Heimdall Industrial Edge Agent Service ==="

# 1. Check for compiled binary
if [[ ! -f "$BINARY_SOURCE" ]]; then
    echo "Error: Binary not found at '$BINARY_SOURCE'." >&2
    echo "Usage: sudo ./install-service.sh [path_to_heimdall_agent_binary]" >&2
    exit 1
fi

# 2. Create system user and group if missing
if ! id -u "$AGENT_USER" >/dev/null 2>&1; then
    echo "Creating system user '$AGENT_USER'..."
    useradd -r -s /bin/false -d "$DATA_DIR" -M -c "Heimdall Agent Service" "$AGENT_USER"
fi

# 3. Create required runtime directories
echo "Creating runtime directories..."
mkdir -p "$CONFIG_DIR"
mkdir -p "$DATA_DIR/spool"
mkdir -p "$LOG_DIR"

chown -R "$AGENT_USER:$AGENT_USER" "$DATA_DIR" "$LOG_DIR"
chmod 750 "$DATA_DIR" "$LOG_DIR"

# 4. Install binary
echo "Installing binary to $INSTALL_BIN..."
cp -f "$BINARY_SOURCE" "$INSTALL_BIN"
chmod 755 "$INSTALL_BIN"

# 5. Install environment config template if not present
if [[ ! -f "$CONFIG_DIR/agent.env" ]]; then
    if [[ -f "$SCRIPT_DIR/agent.env.example" ]]; then
        echo "Installing default environment configuration to $CONFIG_DIR/agent.env..."
        cp "$SCRIPT_DIR/agent.env.example" "$CONFIG_DIR/agent.env"
        chmod 600 "$CONFIG_DIR/agent.env"
    fi
fi

# 6. Install default JSON config if not present
if [[ ! -f "$CONFIG_DIR/agent.json" && -f "$SCRIPT_DIR/default-config.json" ]]; then
    echo "Installing default JSON configuration to $CONFIG_DIR/agent.json..."
    cp "$SCRIPT_DIR/default-config.json" "$CONFIG_DIR/agent.json"
    chmod 644 "$CONFIG_DIR/agent.json"
fi

# 7. Install systemd service unit
echo "Installing systemd service unit..."
cp -f "$SCRIPT_DIR/heimdall-agent.service" "$SERVICE_FILE"
chmod 644 "$SERVICE_FILE"

# 8. Reload systemd daemon and start service
echo "Reloading systemd daemon..."
systemctl daemon-reload
echo "Enabling and starting $SERVICE_NAME..."
systemctl enable "$SERVICE_NAME"
systemctl restart "$SERVICE_NAME"

echo "=== Heimdall Agent Service Installed and Started Successfully ==="
systemctl status "$SERVICE_NAME" --no-pager || true
