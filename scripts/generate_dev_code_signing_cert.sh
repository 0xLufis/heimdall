#!/usr/bin/env bash
# Generate a self-signed code signing certificate and PKCS#12 (.pfx) bundle
# for local development and staging verification of Heimdall binaries.
set -euo pipefail

CERT_DIR="${1:-./packaging/certs}"
CERT_PASS="${2:-HeimdallDev123!}"

mkdir -p "${CERT_DIR}"

echo "=== Generating Self-Signed Code Signing Certificate ==="
echo "Output Directory: ${CERT_DIR}"

# 1. Create OpenSSL configuration for Code Signing
CONFIG_FILE="${CERT_DIR}/codesign.cnf"
cat << 'EOF' > "${CONFIG_FILE}"
[ req ]
default_bits        = 2048
distinguished_name  = req_distinguished_name
prompt              = no
x509_extensions     = v3_codesign

[ req_distinguished_name ]
C                   = US
ST                  = Industrial
L                   = Plant Floor
O                   = Heimdall Project
OU                  = Edge Engineering
CN                  = Heimdall Dev Code Signing CA

[ v3_codesign ]
keyUsage            = critical, digitalSignature
extendedKeyUsage    = critical, codeSigning
basicConstraints    = critical, CA:FALSE
subjectKeyIdentifier = hash
EOF

# 2. Generate private key and self-signed certificate (valid 365 days)
KEY_FILE="${CERT_DIR}/codesign-dev.key"
CRT_FILE="${CERT_DIR}/codesign-dev.crt"
PFX_FILE="${CERT_DIR}/codesign-dev.pfx"

openssl req -x509 -nodes -days 365 \
    -newkey rsa:2048 \
    -keyout "${KEY_FILE}" \
    -out "${CRT_FILE}" \
    -config "${CONFIG_FILE}"

# 3. Export to PKCS#12 (.pfx) for Authenticode / signtool / osslsigncode
openssl pkcs12 -export \
    -out "${PFX_FILE}" \
    -inkey "${KEY_FILE}" \
    -in "${CRT_FILE}" \
    -passout "pass:${CERT_PASS}"

chmod 600 "${KEY_FILE}" "${PFX_FILE}"

echo "✓ Created Private Key: ${KEY_FILE}"
echo "✓ Created Certificate: ${CRT_FILE}"
echo "✓ Created PKCS#12 PFX: ${PFX_FILE}"
echo ""
echo "To sign binaries with this certificate, export:"
echo "  export SIGN_CERT_FILE=\"${PFX_FILE}\""
echo "  export SIGN_CERT_PASSWORD=\"${CERT_PASS}\""
echo "=========================================================="
