#!/usr/bin/env bash
# Heimdall Industrial Edge Agent - Linux Service Uninstaller
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
   echo "Error: This uninstallation script must be run as root (or with sudo)." >&2
   exit 1
fi

SERVICE_NAME="heimdall-agent"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
INSTALL_BIN="/usr/local/bin/heimdall-agent"

echo "=== Uninstalling Heimdall Industrial Edge Agent Service ==="

if systemctl is-active --quiet "$SERVICE_NAME"; then
    echo "Stopping $SERVICE_NAME..."
    systemctl stop "$SERVICE_NAME"
fi

if systemctl is-enabled --quiet "$SERVICE_NAME" 2>/dev/null; then
    echo "Disabling $SERVICE_NAME..."
    systemctl disable "$SERVICE_NAME"
fi

if [[ -f "$SERVICE_FILE" ]]; then
    echo "Removing $SERVICE_FILE..."
    rm -f "$SERVICE_FILE"
    systemctl daemon-reload
fi

if [[ -f "$INSTALL_BIN" ]]; then
    echo "Removing binary $INSTALL_BIN..."
    rm -f "$INSTALL_BIN"
fi

echo "Note: Configuration (/etc/heimdall) and spool data (/var/lib/heimdall-agent) were retained."
echo "=== Heimdall Agent Service Uninstalled Successfully ==="
