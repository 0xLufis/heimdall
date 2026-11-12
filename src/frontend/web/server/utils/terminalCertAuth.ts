import type { H3Event } from 'h3'
import { createError, getHeader } from 'h3'
import { X509Certificate } from 'node:crypto'
import { isIP } from 'node:net'
import { backendBaseUrl } from './backendProxy'

/**
 * Kiosk terminal production path: a TLS-terminating reverse proxy verifies the workstation's client
 * certificate (mTLS) and forwards it, URL-encoded PEM, in `X-Client-Cert` (name configurable with
 * `HEIMDALL_CLIENT_CERT_HEADER`; nginx: `proxy_set_header X-Client-Cert $ssl_client_escaped_cert;`).
 *
 * The header is trusted only when `HEIMDALL_TRUST_CLIENT_CERT_HEADER=true` AND the TCP peer is listed
 * in `HEIMDALL_TRUSTED_PROXIES` (comma-separated IPs or IPv4 CIDRs). The certificate is then
 * validated by the backend PKI (chain to a trusted CA, validity, Heimdall revocation) and mapped to a
 * ClientPc; the terminal is allowed only for stations bound to that controller (StationControllers).
 */

export interface WorkstationCertificateAuth {
  method: 'client-certificate'
  principal: string
  clientPcId: string
  /** Station GUIDs and custom identifiers bound to the workstation's controller, lower case. */
  allowedStations: string[]
}

interface WorkstationIdentityResponse {
  valid: boolean
  status: string
  reason?: string | null
  clientPcId?: string | null
  hostname?: string | null
  mappedBy?: string | null
  stationKeys?: string[]
}

const CACHE_TTL_MS = 60_000
const CACHE_MAX = 256
const identityCache = new Map<string, { at: number; value: WorkstationIdentityResponse }>()

export function clientCertHeaderName(): string {
  return (process.env.HEIMDALL_CLIENT_CERT_HEADER || 'x-client-cert').toLowerCase()
}

export function trustedProxies(): string[] {
  return (process.env.HEIMDALL_TRUSTED_PROXIES || '').split(',').map(p => p.trim()).filter(Boolean)
}

function normalizeAddress(address: string): string {
  const a = address.trim().toLowerCase()
  return a.startsWith('::ffff:') && isIP(a.slice(7)) === 4 ? a.slice(7) : a
}

function ipv4ToInt(ip: string): number {
  return ip.split('.').reduce((acc, part) => (acc << 8) + Number(part), 0) >>> 0
}

/** True when `remoteAddress` matches an entry of HEIMDALL_TRUSTED_PROXIES (exact IP or IPv4 CIDR). */
export function isTrustedProxyAddress(remoteAddress: string | undefined | null): boolean {
  if (!remoteAddress) return false
  const remote = normalizeAddress(remoteAddress)
  return trustedProxies().some(entry => {
    const [base, bits] = entry.split('/')
    const normalizedBase = normalizeAddress(base!)
    if (bits === undefined) return normalizedBase === remote
    const prefix = Number(bits)
    if (isIP(normalizedBase) !== 4 || isIP(remote) !== 4 || !(prefix >= 0 && prefix <= 32)) return false
    const mask = prefix === 0 ? 0 : (~0 << (32 - prefix)) >>> 0
    return (ipv4ToInt(normalizedBase) & mask) === (ipv4ToInt(remote) & mask)
  })
}

/**
 * Parses the forwarded certificate: URL-encoded PEM (nginx `$ssl_client_escaped_cert`), PEM with
 * spaces instead of line breaks, or bare base64 DER (Traefik). Returns a canonical PEM, or null.
 */
export function parseForwardedCertificate(raw: string | undefined | null): { pem: string; x509: X509Certificate } | null {
  if (!raw) return null
  let value = raw.trim()
  try {
    if (value.includes('%')) value = decodeURIComponent(value)
  } catch {
    return null
  }
  const body = value
    .replace(/-----BEGIN CERTIFICATE-----/g, '')
    .replace(/-----END CERTIFICATE-----/g, '')
    .replace(/\s+/g, '')
  if (!body || !/^[A-Za-z0-9+/=]+$/.test(body)) return null
  const pem = `-----BEGIN CERTIFICATE-----\n${body.match(/.{1,64}/g)!.join('\n')}\n-----END CERTIFICATE-----\n`
  try {
    return { pem, x509: new X509Certificate(pem) }
  } catch {
    return null
  }
}

async function resolveIdentity(pem: string, fingerprint: string): Promise<WorkstationIdentityResponse> {
  const cached = identityCache.get(fingerprint)
  if (cached && Date.now() - cached.at < CACHE_TTL_MS) return cached.value

  const fetcher = (globalThis as any).$fetch
  const value: WorkstationIdentityResponse = await fetcher(`${backendBaseUrl()}/api/v1/pki/workstation-identity`, {
    method: 'POST',
    body: { certificatePem: pem },
    timeout: 8000
  })
  if (identityCache.size >= CACHE_MAX) identityCache.delete(identityCache.keys().next().value!)
  identityCache.set(fingerprint, { at: Date.now(), value })
  return value
}

/** Test hook. */
export function clearWorkstationIdentityCache() {
  identityCache.clear()
}

/**
 * Resolves the forwarded workstation certificate. `null` when no trusted certificate header is
 * present (other methods may still apply); throws 401 when a certificate was forwarded by a trusted
 * proxy but does not validate or map to a registered controller.
 */
export async function resolveWorkstationCertificate(event: H3Event): Promise<WorkstationCertificateAuth | null> {
  if (process.env.HEIMDALL_TRUST_CLIENT_CERT_HEADER !== 'true') return null
  const header = getHeader(event, clientCertHeaderName())
  if (!header) return null

  const remote = event.node?.req?.socket?.remoteAddress
  if (!isTrustedProxyAddress(remote)) {
    console.warn(`[terminal-auth] ignoring ${clientCertHeaderName()} from untrusted peer ${remote ?? 'unknown'} (HEIMDALL_TRUSTED_PROXIES)`)
    return null
  }

  const parsed = parseForwardedCertificate(header)
  if (!parsed) {
    throw createError({ statusCode: 401, statusMessage: 'Workstation client certificate is malformed.' })
  }
  const now = Date.now()
  if (Date.parse(parsed.x509.validTo) < now || Date.parse(parsed.x509.validFrom) > now) {
    throw createError({ statusCode: 401, statusMessage: 'Workstation client certificate is outside its validity period.' })
  }

  let identity: WorkstationIdentityResponse
  try {
    identity = await resolveIdentity(parsed.pem, parsed.x509.fingerprint256)
  } catch {
    throw createError({ statusCode: 503, statusMessage: 'Workstation certificate could not be verified (backend PKI unavailable).' })
  }
  if (!identity?.valid || !identity.clientPcId) {
    throw createError({
      statusCode: 401,
      statusMessage: `Workstation client certificate rejected: ${identity?.status ?? 'Invalid'}${identity?.reason ? ` - ${identity.reason}` : ''}`
    })
  }

  return {
    method: 'client-certificate',
    principal: `clientpc:${identity.clientPcId}`,
    clientPcId: identity.clientPcId,
    allowedStations: (identity.stationKeys || []).map(k => k.toLowerCase())
  }
}
