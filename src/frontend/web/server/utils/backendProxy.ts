import type { H3Event } from 'h3'
import { getCookie, getHeader, getMethod, getRequestURL, proxyRequest, setResponseStatus } from 'h3'
import { auth } from './auth'

/**
 * Shared BFF → .NET backend reverse proxy.
 *
 * Security properties (all callers inherit these):
 * - The forwarded path is validated segment-by-segment; `.`/`..`, backslashes, NUL and
 *   percent-encoded separators are rejected so a caller cannot escape the mounted prefix
 *   (e.g. `/api/v1/lua/..%2F..%2Fadmin`).
 * - `X-Organization-Id` is derived from the authenticated Better-Auth session only. A
 *   client-supplied header is never forwarded, so users cannot pivot into another tenant.
 * - Only idempotent methods (GET/HEAD/OPTIONS) are retried; request bodies are single-use
 *   streams and replaying a POST could duplicate side effects.
 * - The 502 response does not disclose the internal backend address.
 */

const RETRYABLE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS'])
const SAFE_SEGMENT = /^[A-Za-z0-9._~@:+-]+$/

export function backendBaseUrl(): string {
  return (process.env.BACKEND_API_URL || 'http://localhost:5099').replace(/\/+$/, '')
}

/**
 * Returns the normalized relative path, or `null` when the path is unsafe.
 * Exported for unit tests.
 */
export function sanitizeProxyPath(rawPath: string | undefined | null): string | null {
  if (!rawPath) return ''
  let decoded: string
  try {
    decoded = decodeURIComponent(rawPath)
  } catch {
    return null
  }
  if (decoded.includes('\\') || decoded.includes('\0') || /%2f|%5c/i.test(decoded)) return null

  const segments = decoded.split('/').filter(s => s.length > 0)
  for (const segment of segments) {
    if (segment === '.' || segment === '..' || !SAFE_SEGMENT.test(segment)) return null
  }
  return segments.map(encodeURIComponent).join('/')
}

async function resolveAuthContext(event: H3Event): Promise<{ token?: string; orgId?: string }> {
  const rawToken =
    getCookie(event, 'better-auth.session_token') ||
    getHeader(event, 'authorization')?.replace(/^Bearer\s+/i, '')
  if (!rawToken) return {}

  // Better-Auth cookies are `<token>.<signature>`; the backend validates the bare token.
  let token: string | undefined = rawToken.trim().split('.')[0]
  let orgId: string | undefined
  try {
    const sessionData = await auth.api.getSession({ headers: event.headers })
    orgId = (sessionData?.session as { activeOrganizationId?: string } | undefined)?.activeOrganizationId ?? undefined
    if (sessionData?.session?.token) token = sessionData.session.token
  } catch {
    // No valid session: forward the token as-is and let the backend reject it.
  }
  return { token, orgId }
}

/**
 * Proxies the current request to `${BACKEND_API_URL}/api/${prefix}/${subPath}` preserving the
 * original query string (including repeated keys).
 */
export async function proxyToBackend(event: H3Event, prefix: string, subPath: string | undefined, label = 'Proxy') {
  const safePath = sanitizeProxyPath(subPath)
  if (safePath === null) {
    setResponseStatus(event, 400)
    return { statusCode: 400, error: 'Bad Request', message: 'Invalid proxy path.' }
  }

  const search = getRequestURL(event).search
  const target = `${backendBaseUrl()}/api/${prefix}${safePath ? `/${safePath}` : ''}${search}`
  const { token, orgId } = await resolveAuthContext(event)
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`
  if (orgId) headers['X-Organization-Id'] = orgId

  // Never forward a client-chosen tenant header or the raw cookie jar upstream.
  const opts = { headers, fetchOptions: { redirect: 'manual' as const } }
  delete event.node.req.headers['x-organization-id']

  try {
    return await proxyRequest(event, target, opts)
  } catch (err: unknown) {
    if (RETRYABLE_METHODS.has(getMethod(event))) {
      // Single retry for transient backend restarts / hot reloads.
      await new Promise(resolve => setTimeout(resolve, 250))
      try {
        return await proxyRequest(event, target, opts)
      } catch (retryErr: unknown) {
        err = retryErr
      }
    }
    console.warn(`[${label}] backend unreachable for /api/${prefix}/${safePath}:`, (err as Error)?.message || err)
    setResponseStatus(event, 502)
    return {
      statusCode: 502,
      error: 'Bad Gateway',
      message: 'The Heimdall backend is currently unavailable.',
      timestamp: new Date().toISOString()
    }
  }
}
