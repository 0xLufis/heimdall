import type { H3Event } from 'h3'
import { createError, getHeader } from 'h3'
import { timingSafeEqual } from 'node:crypto'
import { resolveSessionUser } from './authHelper'
import { isExplicitDevMode, isTerminalAuthBypassEnabled } from './devMode'
import { resolveWorkstationCertificate } from './terminalCertAuth'

export { isExplicitDevMode, isTerminalAuthBypassEnabled }

/**
 * Authentication for shop-floor kiosk terminals (`/api/terminal/*`).
 *
 * A request is accepted when ONE of the following holds:
 *  1. It presents a workstation token listed in `HEIMDALL_TERMINAL_TOKENS` (comma-separated,
 *     min. 16 characters, compared in constant time) — the documented fallback.
 *  2. It carries a valid session (Better-Auth, or a development SSO simulation session; the same
 *     resolution `getAuthenticatedUser` uses).
 *  3. Production path: the workstation's computer certificate, forwarded by a TLS-terminating
 *     reverse proxy (`HEIMDALL_TRUST_CLIENT_CERT_HEADER=true` + `HEIMDALL_TRUSTED_PROXIES`),
 *     validated by the backend PKI and mapped to a ClientPc. Only the stations bound to that
 *     controller are allowed (`assertTerminalStationAccess`), others get 403.
 *  4. Explicit dev mode (`HEIMDALL_ENABLE_DEV=true`) or Vitest: the documented demo tokens
 *     `kiosk-token-op10` / `test-token`.
 *  5. Dev / demo bypass: `HEIMDALL_ENABLE_DEV=true` and `NODE_ENV` is not `production` — no
 *     credentials at all (logged once at startup by server/plugins/terminalAuthNotice.ts).
 */

export type TerminalAuthMethod = 'session' | 'workstation-token' | 'client-certificate' | 'dev-token' | 'dev-bypass'

export interface TerminalAuthResult {
  method: TerminalAuthMethod
  principal: string
  /** Client-certificate auth only: the workstation's ClientPc. */
  clientPcId?: string
  /** Client-certificate auth only: station GUIDs / custom identifiers (lower case) this workstation may open. */
  allowedStations?: string[]
}

const DEV_TOKENS = ['kiosk-token-op10', 'test-token']

function safeEqual(a: string, b: string): boolean {
  const ab = Buffer.from(a)
  const bb = Buffer.from(b)
  return ab.length === bb.length && timingSafeEqual(ab, bb)
}

export function configuredTerminalTokens(): string[] {
  return (process.env.HEIMDALL_TERMINAL_TOKENS || '')
    .split(',')
    .map(t => t.trim())
    .filter(t => t.length >= 16)
}

export function matchTerminalToken(token: string | undefined | null): TerminalAuthResult | null {
  if (!token) return null
  const candidate = token.trim()
  if (configuredTerminalTokens().some(t => safeEqual(t, candidate))) {
    return { method: 'workstation-token', principal: `workstation:${candidate.slice(0, 4)}…` }
  }
  if (isExplicitDevMode() && process.env.NODE_ENV !== 'production' && DEV_TOKENS.some(t => safeEqual(t, candidate))) {
    return { method: 'dev-token', principal: `dev:${candidate}` }
  }
  return null
}

/**
 * Resolves terminal credentials or throws 401. `bodyToken` lets POST handlers accept the token
 * from the JSON body (the kiosk UI sends it there).
 */
export async function requireTerminalAuth(event: H3Event, bodyToken?: string): Promise<TerminalAuthResult> {
  const authHeader = getHeader(event, 'authorization')
  const bearer = authHeader?.startsWith('Bearer ') ? authHeader.substring(7) : undefined
  const queryToken = new URL(event.path || '/', 'http://local').searchParams.get('token') || undefined
  const headerToken = getHeader(event, 'x-terminal-token') || getHeader(event, 'x-workstation-token')

  for (const token of [headerToken, bearer, bodyToken, queryToken]) {
    const match = matchTerminalToken(token)
    if (match) return match
  }

  // Sessions: Better-Auth's getSession alone does not recognise development SSO simulation
  // sessions (bare `sso_` token cookie), which made the kiosk answer 401 to a signed-in user.
  const user = await resolveSessionUser(event)
  if (user?.id) return { method: 'session', principal: `user:${user.id}` }

  const certificate = await resolveWorkstationCertificate(event)
  if (certificate) return certificate

  if (isTerminalAuthBypassEnabled()) {
    return { method: 'dev-bypass', principal: 'dev:unauthenticated-kiosk' }
  }

  throw createError({
    statusCode: 401,
    statusMessage: 'Workstation terminal authentication required. Provide a registered workstation token or client certificate.'
  })
}

/**
 * Station scoping for certificate-authenticated workstations: 403 unless one of `stationKeys`
 * (requested id, station GUID, custom identifier) is bound to the workstation's controller.
 * Other methods (session, workstation token, dev) are not station-scoped.
 */
export function assertTerminalStationAccess(auth: TerminalAuthResult, ...stationKeys: Array<string | null | undefined>) {
  if (auth.method !== 'client-certificate') return
  const allowed = new Set(auth.allowedStations || [])
  if (stationKeys.some(k => k && allowed.has(String(k).toLowerCase()))) return
  throw createError({
    statusCode: 403,
    statusMessage: 'This workstation is not bound to the requested station.'
  })
}
