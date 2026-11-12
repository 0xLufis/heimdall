import { eq } from 'drizzle-orm'
import { useDb } from './db'
import * as hbSchema from '../database/drizzle/schema'

/**
 * Keeps the backend's view of a session in step with Better-Auth.
 *
 * The .NET backend authenticates the same `auth.session` rows but caches the resolved role and
 * active organization per session token for five minutes (BetterAuthHandler). Better-Auth writes
 * those rows here in the BFF, so the backend has to be told when they change; otherwise a role
 * change, an organization switch, a removed membership, a ban or a revoked session only takes
 * effect in the API after the cache entry expires.
 */

const ORGANIZATION_ROLE_ORDER = ['owner', 'admin', 'member']

export interface MembershipRow {
  organizationId: string
  role?: string | null
  createdAt?: Date | string | null
}

/**
 * Organization a new session starts in: the membership with the highest organization role, then
 * the oldest, then by id, so the choice is stable. Without an active organization the backend
 * issues no `OrgId` claim and a non-administrator sees no tenant-owned data at all.
 */
export function pickDefaultOrganization(memberships: MembershipRow[]): string | undefined {
  const rank = (role?: string | null) => {
    const index = ORGANIZATION_ROLE_ORDER.indexOf(String(role || '').split(',')[0]!.trim().toLowerCase())
    return index === -1 ? ORGANIZATION_ROLE_ORDER.length : index
  }
  const time = (value?: Date | string | null) => (value ? new Date(value).getTime() || 0 : 0)
  return [...memberships]
    .sort((a, b) =>
      rank(a.role) - rank(b.role) ||
      time(a.createdAt) - time(b.createdAt) ||
      a.organizationId.localeCompare(b.organizationId))[0]?.organizationId
}

export async function resolveDefaultOrganizationId(userId: string): Promise<string | undefined> {
  try {
    const memberships = await useDb()
      .select({
        organizationId: hbSchema.member.organizationId,
        role: hbSchema.member.role,
        createdAt: hbSchema.member.createdAt
      })
      .from(hbSchema.member)
      .where(eq(hbSchema.member.userId, userId))
    return pickDefaultOrganization(memberships)
  } catch {
    return undefined
  }
}

function backendBase(): string {
  return (process.env.BACKEND_API_URL || 'http://localhost:5099').replace(/\/+$/, '')
}

/**
 * Asks the backend to drop its cached entry for each session token. The token itself is the
 * credential (`POST /api/v1/auth/session/evict` only ever evicts the caller's own entry), so no
 * service secret is involved. Best effort: an unreachable backend just means the entry expires on
 * its own.
 */
export async function evictBackendSessionCache(tokens: Array<string | null | undefined>): Promise<void> {
  const unique = [...new Set(tokens.filter((t): t is string => typeof t === 'string' && t.length > 0))]
  await Promise.all(unique.map(async (token) => {
    try {
      await fetch(`${backendBase()}/api/v1/auth/session/evict`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
        signal: AbortSignal.timeout(1500)
      })
    } catch {
      // ignore: the cached entry expires by itself
    }
  }))
}

/** Evicts every session of one user (role, ban or membership changed). */
export async function evictBackendSessionCacheForUser(userId: string | null | undefined): Promise<void> {
  if (!userId) return
  try {
    const sessions = await useDb()
      .select({ token: hbSchema.session.token })
      .from(hbSchema.session)
      .where(eq(hbSchema.session.userId, userId))
    await evictBackendSessionCache(sessions.map(s => s.token))
  } catch {
    // ignore: the cached entries expire by themselves
  }
}
