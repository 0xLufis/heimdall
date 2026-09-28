#!/usr/bin/env bash
# Heimdall Standalone Packaging Script
# Builds self-contained, independent deployment artifacts for Linux and Windows
# Supports Authenticode code signing, semver archive naming, and SHA256SUMS manifests
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="${ROOT_DIR}/dist"
STAGING_DIR="${DIST_DIR}/staging"
TARGET="${1:-agent}"
VERSION="${VERSION:-1.0.0}"

echo "=== Heimdall Standalone Packaging Tool ==="
echo "Target: ${TARGET}"
echo "Version: v${VERSION}"
echo "Workspace: ${ROOT_DIR}"
echo "Output Directory: ${DIST_DIR}"

mkdir -p "${DIST_DIR}"
mkdir -p "${STAGING_DIR}"

sign_windows_binary() {
    local bin_path="$1"
    local cert_file="${SIGN_CERT_FILE:-}"
    local cert_pass="${SIGN_CERT_PASSWORD:-}"
    local timestamp_url="${SIGN_TIMESTAMP_URL:-http://timestamp.digicert.com}"

    if [[ -z "${cert_file}" || ! -f "${cert_file}" ]]; then
        echo "    [i] Code signing skipped: SIGN_CERT_FILE not specified or certificate file not found."
        return 0
    fi

    echo "--> Signing Windows binary: ${bin_path}..."
    if command -v osslsigncode >/dev/null 2>&1; then
        local signed_tmp="${bin_path}.signed"
        osslsigncode sign \
            -pkcs12 "${cert_file}" \
            -pass "${cert_pass}" \
            -h sha256 \
            -ts "${timestamp_url}" \
            -in "${bin_path}" \
            -out "${signed_tmp}"
        mv -f "${signed_tmp}" "${bin_path}"
        echo "    ✓ Successfully signed with osslsigncode (SHA256 / Authenticode)"
    elif command -v signtool >/dev/null 2>&1; then
        signtool sign /f "${cert_file}" /p "${cert_pass}" /fd SHA256 /tr "${timestamp_url}" /td SHA256 /d "Heimdall Edge Daemon" "${bin_path}"
        echo "    ✓ Successfully signed with signtool (SHA256 / Authenticode)"
    else
        echo "    [!] Warning: Neither osslsigncode nor signtool available on host. Binary left unsigned."
    fi
}

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

    # Ensure canonical binary name heimdall-agent
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

    # Create standalone versioned compressed tarball and unversioned link
    local versioned_archive="${DIST_DIR}/heimdall-agent-v${VERSION}-linux-x64.tar.gz"
    local unversioned_archive="${DIST_DIR}/heimdall-agent-linux-x64.tar.gz"
    tar -czf "${versioned_archive}" -C "${STAGING_DIR}" "heimdall-agent-linux-x64"
    cp -f "${versioned_archive}" "${unversioned_archive}"
    echo "✓ Created: ${versioned_archive} ($(du -h "${versioned_archive}" | cut -f1))"
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

    # Ensure canonical binary name heimdall-agent.exe
    if [[ -f "${out_dir}/App.Agent.Daemon.exe" ]]; then
        mv "${out_dir}/App.Agent.Daemon.exe" "${out_dir}/heimdall-agent.exe"
    fi

    # Sign Windows executable if signing certificate is configured
    sign_windows_binary "${out_dir}/heimdall-agent.exe"

    # Provide HeimdallAgent.exe alias for backward compatibility with existing service definitions
    cp -f "${out_dir}/heimdall-agent.exe" "${out_dir}/HeimdallAgent.exe"

    # Bundle Windows service scripts & configs
    cp -f "${ROOT_DIR}/packaging/agent/install-service.ps1" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/uninstall-service.ps1" "${out_dir}/"
    cp -f "${ROOT_DIR}/packaging/agent/default-config.json" "${out_dir}/"

    local versioned_archive="${DIST_DIR}/heimdall-agent-v${VERSION}-win-x64.zip"
    local unversioned_archive="${DIST_DIR}/heimdall-agent-win-x64.zip"
    if command -v zip >/dev/null 2>&1; then
        (cd "${STAGING_DIR}" && zip -rq "${versioned_archive}" "heimdall-agent-win-x64")
        cp -f "${versioned_archive}" "${unversioned_archive}"
        echo "✓ Created: ${versioned_archive} ($(du -h "${versioned_archive}" | cut -f1))"
    else
        tar -czf "${DIST_DIR}/heimdall-agent-v${VERSION}-win-x64.tar.gz" -C "${STAGING_DIR}" "heimdall-agent-win-x64"
        cp -f "${DIST_DIR}/heimdall-agent-v${VERSION}-win-x64.tar.gz" "${DIST_DIR}/heimdall-agent-win-x64.tar.gz"
        echo "✓ Created: ${DIST_DIR}/heimdall-agent-v${VERSION}-win-x64.tar.gz"
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

    local versioned_archive="${DIST_DIR}/heimdall-backend-v${VERSION}.tar.gz"
    local unversioned_archive="${DIST_DIR}/heimdall-backend.tar.gz"
    tar -czf "${versioned_archive}" -C "${STAGING_DIR}" "heimdall-backend"
    cp -f "${versioned_archive}" "${unversioned_archive}"
    echo "✓ Created: ${versioned_archive} ($(du -h "${versioned_archive}" | cut -f1))"
}

package_frontend() {
    echo "--> Packaging Frontend Web App (Standalone Nitro Output)..."
    local out_dir="${STAGING_DIR}/heimdall-frontend"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"

    (cd "${ROOT_DIR}/frontend/web" && bun run build)
    cp -r "${ROOT_DIR}/frontend/web/.output" "${out_dir}/"
    
    local versioned_archive="${DIST_DIR}/heimdall-frontend-v${VERSION}.tar.gz"
    local unversioned_archive="${DIST_DIR}/heimdall-frontend.tar.gz"
    tar -czf "${versioned_archive}" -C "${STAGING_DIR}" "heimdall-frontend"
    cp -f "${versioned_archive}" "${unversioned_archive}"
    echo "✓ Created: ${versioned_archive} ($(du -h "${versioned_archive}" | cut -f1))"
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

echo "--> Generating SHA256SUMS manifest..."
(cd "${DIST_DIR}" && sha256sum *.tar.gz *.zip > SHA256SUMS 2>/dev/null || true)

echo "=== Standalone Packaging Completed Successfully ==="
ls -lh "${DIST_DIR}"/*.tar.gz "${DIST_DIR}"/*.zip "${DIST_DIR}/SHA256SUMS" 2>/dev/null || true
