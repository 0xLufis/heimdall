import { H3Event, getHeader, getCookie, createError } from 'h3'
import { auth } from './auth'
import { isExplicitDevMode } from './devMode'
import { eq } from 'drizzle-orm'
import { useDb } from './db'
import * as hbSchema from '../database/drizzle/schema'

/**
 * Sessions created by the development-only Azure SSO simulation (`/api/auth/sso/simulate`) are
 * plain rows in `auth.session` and their cookie carries the bare token, so Better-Auth's
 * signed-cookie check does not recognise them. Without this lookup every route guarded by
 * `getAuthenticatedUser(event, true)` answers 401 to a user who signed in that way.
 *
 * Strict on purpose: the token must be a simulation token, the row must exist and be unexpired,
 * and the user must not be banned. Disabled in production, where the simulation endpoint is too.
 */
async function resolveSimulatedSsoUser(rawToken: string | undefined) {
  if (process.env.NODE_ENV === 'production' || !rawToken) return undefined
  const token = rawToken.trim().replace(/^Bearer\s+/i, '').split('.')[0]
  if (!token || !token.startsWith('sso_')) return undefined

  try {
    const rows = await useDb()
      .select({
        id: hbSchema.user.id,
        name: hbSchema.user.name,
        email: hbSchema.user.email,
        role: hbSchema.user.role,
        permissions: hbSchema.user.permissions,
        banned: hbSchema.user.banned,
        expiresAt: hbSchema.session.expiresAt
      })
      .from(hbSchema.session)
      .innerJoin(hbSchema.user, eq(hbSchema.session.userId, hbSchema.user.id))
      .where(eq(hbSchema.session.token, token))
      .limit(1)
    const row = rows[0]
    if (!row || row.banned === true) return undefined
    if (new Date(row.expiresAt as any).getTime() <= Date.now()) return undefined
    return { id: row.id, name: row.name, email: row.email, role: row.role, permissions: row.permissions ?? null }
  } catch {
    // Database unreachable: stay unauthenticated rather than guess.
    return undefined
  }
}

/**
 * The user behind the request's session: a Better-Auth session or a development SSO simulation
 * session. No synthetic fallback; `undefined` when there is no valid session.
 */
export async function resolveSessionUser(event: H3Event): Promise<any | undefined> {
  const existing = (event.context as any)?.auth?.user
  if (existing) return existing

  const authHeader = getHeader(event, 'authorization')
  const cookieToken = getCookie(event, 'better-auth.session_token')
  if (!authHeader && !cookieToken) return undefined

  try {
    const sessionData = await auth.api.getSession({ headers: event.headers })
    if (sessionData?.user) return sessionData.user
  } catch {
    // ignore
  }
  return await resolveSimulatedSsoUser(cookieToken || authHeader)
}

export async function getAuthenticatedUser(event: H3Event, requireAuth = false) {
  let user = await resolveSessionUser(event)

  // Synthetic admin fallback ONLY under an explicit opt-in (HEIMDALL_ENABLE_DEV=true, set by
  // .env.dev) or Vitest. Previously any NODE_ENV other than "production" (including an unset
  // NODE_ENV) silently granted anonymous callers admin rights.
  // The identity matches the seeded `usr-admin-primary` account (admin@heimdall.dev, system_admin).
  // It used to claim the legacy `admin` role, which is not an IT / engineering administrator.
  if (!user && isExplicitDevMode()) {
    user = {
      id: 'usr-admin-primary',
      name: 'System Administrator',
      email: 'admin@heimdall.dev',
      role: 'system_admin'
    }
  }

  if (!user && requireAuth) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Authentication required'
    })
  }

  return user
}
