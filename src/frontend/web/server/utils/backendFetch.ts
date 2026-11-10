import type { H3Event } from 'h3'
import { createError, getCookie, getHeader } from 'h3'
import { auth } from './auth'
import { backendBaseUrl, sanitizeProxyPath } from './backendProxy'

/**
 * Server-side JSON call to the .NET backend on behalf of the current user, for BFF routes
 * that need to reshape the request/response (pure pass-throughs should use `proxyToBackend`).
 *
 * Mirrors `backendProxy.ts` security properties:
 * - the bearer token and `X-Organization-Id` come from the Better-Auth session only (a
 *   client-supplied tenant header is never forwarded);
 * - the path is validated so it cannot escape `/api/v1`;
 * - backend errors (401/403/404/...) are re-thrown with their status instead of being
 *   swallowed, so the BFF never silently "succeeds" against local state.
 */
export async function backendFetch<T = unknown>(
  event: H3Event,
  path: string,
  options: { method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'; body?: unknown; query?: Record<string, unknown> } = {}
): Promise<T> {
  const safePath = sanitizeProxyPath(path)
  if (safePath === null || safePath === '') {
    throw createError({ statusCode: 400, statusMessage: 'Invalid backend path.' })
  }

  const rawToken =
    getCookie(event, 'better-auth.session_token') ||
    getHeader(event, 'authorization')?.replace(/^Bearer\s+/i, '')
  if (!rawToken) {
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized: Authentication required' })
  }

  let token = rawToken.trim().split('.')[0]
  let orgId: string | undefined
  try {
    const sessionData = await auth.api.getSession({ headers: event.headers })
    orgId = (sessionData?.session as { activeOrganizationId?: string } | undefined)?.activeOrganizationId ?? undefined
    if (sessionData?.session?.token) token = sessionData.session.token
  } catch {
    // Forward the token as-is and let the backend reject it.
  }

  const headers: Record<string, string> = { Authorization: `Bearer ${token}` }
  if (orgId) headers['X-Organization-Id'] = orgId

  try {
    return await $fetch<T>(`${backendBaseUrl()}/api/v1/${safePath}`, {
      method: options.method || 'GET',
      body: options.body as any,
      query: options.query,
      headers,
      redirect: 'manual',
      timeout: 10000
    })
  } catch (err: any) {
    const statusCode = err?.response?.status || err?.statusCode
    if (statusCode && statusCode < 500) {
      throw createError({
        statusCode,
        statusMessage: err?.data?.title || err?.data?.message || err?.statusMessage || 'Backend request rejected.',
        data: err?.data
      })
    }
    console.warn(`[BackendFetch] /api/v1/${safePath} failed:`, err?.message || err)
    throw createError({ statusCode: 502, statusMessage: 'The Heimdall backend is currently unavailable.' })
  }
}
