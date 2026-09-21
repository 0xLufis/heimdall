#!/usr/bin/env bash
# Heimdall Standalone Packaging Script
# Builds self-contained, independent deployment artifacts for Linux and Windows
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="${ROOT_DIR}/dist"
STAGING_DIR="${DIST_DIR}/staging"
TARGET="${1:-agent}"

echo "=== Heimdall Standalone Packaging Tool ==="
echo "Target: ${TARGET}"
echo "Workspace: ${ROOT_DIR}"
echo "Output Directory: ${DIST_DIR}"

mkdir -p "${DIST_DIR}"
mkdir -p "${STAGING_DIR}"

package_agent_linux() {
    echo "--> Packaging standalone Agent for linux-x64..."
    local out_dir="${STAGING_DIR}/heimdall-agent-linux-x64"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"

    dotnet publish "${ROOT_DIR}/agent/App.Agent.Daemon/App.Agent.Daemon.csproj" \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o "${out_dir}"

    # Rename single-file executable to canonical standard binary name
    if [[ -f "${out_dir}/App.Agent.Daemon" ]]; then
        mv "${out_dir}/App.Agent.Daemon" "${out_dir}/heimdall-agent"
    fi
    chmod +x "${out_dir}/heimdall-agent"

    # Bundle packaging scripts & configs
    cp -f "${ROOT_DIR}/packaging/agent/heimdall-agent.service" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/install-service.sh" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/uninstall-service.sh" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/default-config.json" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/agent.env.example" "${out_dir}/"
    chmod +x "${out_dir}/"*.sh

    # Create standalone compressed tarball
    local archive="${DIST_DIR}/heimdall-agent-linux-x64.tar.gz"
    tar -czf "${archive}" -C "${STAGING_DIR}" "heimdall-agent-linux-x64"
    echo "✓ Created: ${archive} ($(du -h "${archive}" | cut -f1))"
}

package_agent_windows() {
    echo "--> Packaging standalone Agent for win-x64..."
    local out_dir="${STAGING_DIR}/heimdall-agent-win-x64"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"

    dotnet publish "${ROOT_DIR}/agent/App.Agent.Daemon/App.Agent.Daemon.csproj" \
        -c Release \
        -r win-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o "${out_dir}"

    if [[ -f "${out_dir}/App.Agent.Daemon.exe" ]]; then
        mv "${out_dir}/App.Agent.Daemon.exe" "${out_dir}/HeimdallAgent.exe"
    fi

    # Bundle Windows service scripts & configs
    cp -f "${ROOT_DIR}/packaging/agent/install-service.ps1" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/uninstall-service.ps1" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/default-config.json" "${out_dir}/"

    local archive="${DIST_DIR}/heimdall-agent-win-x64.zip"
    if command -v zip >/dev/null 2>&1; then
        (cd "${STAGING_DIR}" && zip -rq "${archive}" "heimdall-agent-win-x64")
        echo "✓ Created: ${archive} ($(du -h "${archive}" | cut -f1))"
    else
        tar -czf "${DIST_DIR}/heimdall-agent-win-x64.tar.gz" -C "${STAGING_DIR}" "heimdall-agent-win-x64"
        echo "✓ Created: ${DIST_DIR}/heimdall-agent-win-x64.tar.gz"
    fi
}

package_backend() {
    echo "--> Packaging Backend API Release..."
    local out_dir="${STAGING_DIR}/heimdall-backend"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"

    dotnet publish "${ROOT_DIR}/backend/App.Backend.Api/App.Backend.Api.csproj" \
        -c Release \
        -o "${out_dir}" \
        /p:UseAppHost=false

    local archive="${DIST_DIR}/heimdall-backend.tar.gz"
    tar -czf "${archive}" -C "${STAGING_DIR}" "heimdall-backend"
    echo "✓ Created: ${archive} ($(du -h "${archive}" | cut -f1))"
}

package_frontend() {
    echo "--> Packaging Frontend Web App (Standalone Nitro Output)..."
    local out_dir="${STAGING_DIR}/heimdall-frontend"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"

    (cd "${ROOT_DIR}/frontend/web" && bun run build)
    cp -r "${ROOT_DIR}/frontend/web/.output" "${out_dir}/"
    
    local archive="${DIST_DIR}/heimdall-frontend.tar.gz"
    tar -czf "${archive}" -C "${STAGING_DIR}" "heimdall-frontend"
    echo "✓ Created: ${archive} ($(du -h "${archive}" | cut -f1))"
}

case "${TARGET}" in
    agent)
        package_agent_linux
        package_agent_windows
        ;;
    backend)
        package_backend
        ;;
    frontend)
        package_frontend
        ;;
    all)
        package_agent_linux
        package_agent_windows
        package_backend
        package_frontend
        ;;
    *)
        echo "Unknown target '${TARGET}'. Options: agent, backend, frontend, all" >&2
        exit 1
        ;;
esac

echo "=== Standalone Packaging Completed Successfully ==="
ls -lh "${DIST_DIR}"/*.tar.gz "${DIST_DIR}"/*.zip 2>/dev/null || true
