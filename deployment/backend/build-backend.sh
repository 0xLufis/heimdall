#!/usr/bin/env bash
# Heimdall Backend Cross-Platform Standalone Build & Signing Pipeline
# Standard configuration search paths:
#   Linux:   /etc/heimdall/appsettings.json
#   Windows: %ProgramData%\Heimdall\appsettings.json
set -euo pipefail

DIST_DIR="$(pwd)/dist/backend"
mkdir -p "${DIST_DIR}"

echo ">> Publishing Standalone Linux Binary (linux-x64)..."
dotnet publish backend/App.Backend.Api/App.Backend.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o "${DIST_DIR}/linux-x64"

echo ">> Publishing Standalone Windows Binary (win-x64)..."
dotnet publish backend/App.Backend.Api/App.Backend.Api.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o "${DIST_DIR}/win-x64"

# Optional Authenticode Code Signing (Windows) if SIGNING_CERT is provided
if [ -n "${SIGNING_CERT:-}" ] && command -v osslsigncode &>/dev/null; then
    echo ">> Applying Authenticode Signature to heimdall-backend.exe..."
    osslsigncode sign \
        -pkcs12 "${SIGNING_CERT}" \
        -pass "${SIGNING_PASSWORD:-}" \
        -n "Heimdall Backend Server" \
        -i "https://github.com/heimdall-iot" \
        -in "${DIST_DIR}/win-x64/heimdall-backend.exe" \
        -out "${DIST_DIR}/win-x64/heimdall-backend-signed.exe"
    mv "${DIST_DIR}/win-x64/heimdall-backend-signed.exe" "${DIST_DIR}/win-x64/heimdall-backend.exe"
fi

echo ">> Backend build artifacts successfully written to: ${DIST_DIR}"
