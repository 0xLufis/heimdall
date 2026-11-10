import type { H3Event } from 'h3'
import { createError, getHeader } from 'h3'
import { timingSafeEqual } from 'node:crypto'
import { auth } from './auth'
import { getAllCertificates } from './pkiStore'

/**
 * Authentication for shop-floor kiosk terminals (`/api/terminal/*`).
 *
 * A request is accepted when ONE of the following holds:
 *  1. It carries a valid Better-Auth session (an operator signed in on the kiosk).
 *  2. It presents a workstation token listed in `HEIMDALL_TERMINAL_TOKENS`
 *     (comma-separated; compared in constant time).
 *  3. `HEIMDALL_TRUST_CLIENT_CERT_HEADER=true` (only set this when a reverse proxy terminates
 *     mTLS and overwrites the header) AND the forwarded thumbprint belongs to an Active
 *     certificate in the PKI registry.
 *  4. Explicit dev mode (`HEIMDALL_ENABLE_DEV=true`) or Vitest, where the documented demo
 *     tokens `kiosk-token-op10` / `test-token` are accepted.
 *
 * Previously any token of 4+ characters, any self-asserted `x-client-cert` header, or
 * `mock_auth=true` was accepted, which made the kiosk API effectively unauthenticated.
 */

export type TerminalAuthMethod = 'session' | 'workstation-token' | 'client-certificate' | 'dev-token'

export interface TerminalAuthResult {
  method: TerminalAuthMethod
  principal: string
}

const DEV_TOKENS = ['kiosk-token-op10', 'test-token']

export function isExplicitDevMode(): boolean {
  return process.env.HEIMDALL_ENABLE_DEV === 'true' || process.env.VITEST === 'true'
}

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
  if (isExplicitDevMode() && DEV_TOKENS.some(t => safeEqual(t, candidate))) {
    return { method: 'dev-token', principal: `dev:${candidate}` }
  }
  return null
}

export function matchClientCertificate(thumbprint: string | undefined | null): TerminalAuthResult | null {
  if (!thumbprint || process.env.HEIMDALL_TRUST_CLIENT_CERT_HEADER !== 'true') return null
  const normalized = thumbprint.replace(/[^0-9a-f]/gi, '').toUpperCase()
  const now = Date.now()
  const cert = getAllCertificates().find(c =>
    c.status === 'Active' &&
    !c.isRootCa &&
    c.thumbprint.toUpperCase() === normalized &&
    Date.parse(c.validTo) > now
  )
  return cert ? { method: 'client-certificate', principal: `cert:${cert.commonName}` } : null
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

  const certMatch = matchClientCertificate(
    getHeader(event, 'x-client-cert-thumbprint') || getHeader(event, 'x-client-cert')
  )
  if (certMatch) return certMatch

  try {
    const session = await auth.api.getSession({ headers: event.headers })
    if (session?.user) return { method: 'session', principal: `user:${session.user.id}` }
  } catch {
    // fall through to 401
  }

  throw createError({
    statusCode: 401,
    statusMessage: 'Workstation terminal authentication required. Provide a registered workstation token or client certificate.'
  })
}
