#!/usr/bin/env bash
# Signs a Heimdall plugin package with YOUR user certificate (AD- or Heimdall-issued) and writes the
# detached signature envelope (heimdall-plugin-signature/v1) to stdout. No CMS: JSON with the
# algorithm, your certificate chain (PEM), the signature over SHA-256 of the package file, signed-at.
#
#   scripts/sign-plugin.sh <package.json> <private-key.pem> <certificate-chain.pem> > package.sig.json
#   scripts/sign-plugin.sh <package.json> <credentials.pfx|.p12>                   > package.sig.json
#     (PFX password from $PFX_PASSWORD, otherwise prompted)
#
# package.json = { "manifest": { "pluginId", "name", "version", "entrypoint", "runtimeType" }, "files": { "<path>": "<content>" } }
# Upload (the backend verifies chain + that the certificate maps to you, then countersigns):
#   curl -X POST https://<heimdall>/api/v1/plugin/signed -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
#        -d "{\"packageBase64\":\"$(base64 -w0 package.json)\",\"signature\":$(cat package.sig.json)}"
set -euo pipefail

die() { echo "sign-plugin: $*" >&2; exit 1; }
[ $# -ge 2 ] || die "usage: $0 <package.json> <key.pem|cert.pfx> [certificate-chain.pem]"
package=$1; credentials=$2; chain=${3:-}
[ -f "$package" ] || die "package not found: $package"
[ -f "$credentials" ] || die "key / PFX not found: $credentials"
command -v openssl >/dev/null || die "openssl is required"

work=$(mktemp -d "${TMPDIR:-/var/tmp}/heimdall-sign.XXXXXX")
trap 'rm -rf "$work"' EXIT
chmod 700 "$work"

case "$credentials" in
  *.pfx|*.p12|*.PFX|*.P12)
    if [ -z "${PFX_PASSWORD+x}" ]; then read -rsp "PFX password: " PFX_PASSWORD; echo >&2; fi
    export PFX_PASSWORD
    p12() { openssl pkcs12 -in "$credentials" -passin env:PFX_PASSWORD "$@" 2>/dev/null || openssl pkcs12 -legacy -in "$credentials" -passin env:PFX_PASSWORD "$@"; }
    p12 -nocerts -nodes -out "$work/key.pem"
    p12 -nokeys -clcerts -out "$work/leaf.pem"
    p12 -nokeys -cacerts -out "$work/cas.pem" || : > "$work/cas.pem"
    key="$work/key.pem"
    # Leaf first, then the CA certificates contained in the PFX.
    sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' "$work/leaf.pem" "$work/cas.pem" > "$work/chain.pem"
    ;;
  *)
    [ -n "$chain" ] && [ -f "$chain" ] || die "a PEM key needs the certificate (chain) PEM as third argument"
    key=$credentials
    sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' "$chain" > "$work/chain.pem"
    ;;
esac
[ -s "$work/chain.pem" ] || die "no certificate found"

# The first certificate must carry the signing key.
openssl x509 -in "$work/chain.pem" -noout -pubkey > "$work/cert.pub"
openssl pkey -in "$key" -pubout > "$work/key.pub"
cmp -s "$work/cert.pub" "$work/key.pub" || die "the private key does not belong to the (first) certificate"

# (`openssl ec -noout` exits 0 even for RSA keys on OpenSSL 3, so look at the key text instead.)
key_text=$(openssl pkey -in "$key" -noout -text)
if grep -q '^modulus:' <<<"$key_text"; then algorithm="RSA-PKCS1-SHA256"
elif grep -qE '^(ASN1 OID|NIST CURVE):' <<<"$key_text"; then algorithm="ECDSA-SHA256"
else die "unsupported key type (RSA or ECDSA required)"; fi

sha256=$(openssl dgst -sha256 -r "$package" | cut -d' ' -f1)
# RSA keys: PKCS#1 v1.5 over SHA-256; EC keys: ECDSA over SHA-256, DER-encoded.
openssl dgst -sha256 -sign "$key" -out "$work/sig.bin" "$package"
signature=$(openssl base64 -A -in "$work/sig.bin")
chain_json=$(awk '{ sub(/\r$/, ""); printf "%s\\n", $0 }' "$work/chain.pem")

printf '{\n  "format": "heimdall-plugin-signature/v1",\n  "algorithm": "%s",\n  "packageSha256": "%s",\n  "signedAt": "%s",\n  "certificateChainPem": "%s",\n  "signature": "%s"\n}\n' \
  "$algorithm" "$sha256" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$chain_json" "$signature"
