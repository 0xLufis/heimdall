import { defineEventHandler, createError } from 'h3'
import { count, eq, inArray } from 'drizzle-orm'
import { useDb } from '../utils/db'
import * as hbSchema from '../database/drizzle/schema'
import { requireUser } from '../utils/authz'
import { isUserAdminRole, roleSatisfies } from '../../app/utils/roles'

/**
 * User directory for the administration pages and the user pickers.
 *
 * Reads Better-Auth's own `auth.user` table (the source of truth for identities) and requires a
 * signed-in caller. It used to be anonymous, went through the backend first (whose projection
 * lacks `banned`, `username` and `createdAt`, so suspensions never showed up) and answered with a
 * hard-coded profile catalogue when both sources failed.
 *
 * System, IT and engineering administrators (and user administrators) see every user; everyone
 * else only the users they share an organization with, and themselves - the same rule as the
 * backend's `GET /api/v1/auth/users` and its search endpoint.
 */
export default defineEventHandler(async (event) => {
  const caller = await requireUser(event)
  const seesEveryone =
    isUserAdminRole(caller.role) ||
    roleSatisfies(caller.role, 'SystemAdministration') ||
    roleSatisfies(caller.role, 'ItAdministration') ||
    roleSatisfies(caller.role, 'EngineeringAdministration')

  try {
    const db = useDb()

    let visibleUserIds: Set<string> | null = null
    if (!seesEveryone) {
      visibleUserIds = new Set([caller.id])
      const mine = await db
        .select({ organizationId: hbSchema.member.organizationId })
        .from(hbSchema.member)
        .where(eq(hbSchema.member.userId, caller.id))
      const organizationIds = [...new Set(mine.map(m => m.organizationId))]
      if (organizationIds.length > 0) {
        const colleagues = await db
          .select({ userId: hbSchema.member.userId })
          .from(hbSchema.member)
          .where(inArray(hbSchema.member.organizationId, organizationIds))
        for (const c of colleagues) visibleUserIds.add(c.userId)
      }
    }
    const [dbUsers, memberships] = await Promise.all([
      db
        .select({
          id: hbSchema.user.id,
          name: hbSchema.user.name,
          email: hbSchema.user.email,
          role: hbSchema.user.role,
          username: hbSchema.user.username,
          banned: hbSchema.user.banned,
          createdAt: hbSchema.user.createdAt
        })
        .from(hbSchema.user),
      db
        .select({ userId: hbSchema.member.userId, organizations: count() })
        .from(hbSchema.member)
        .groupBy(hbSchema.member.userId)
    ])

    const organizationCounts = new Map(memberships.map(m => [m.userId, Number(m.organizations)]))
    return {
      success: true,
      users: dbUsers.filter(u => !visibleUserIds || visibleUserIds.has(u.id)).map(u => ({
        ...u,
        banned: u.banned === true,
        organizationCount: organizationCounts.get(u.id) ?? 0
      }))
    }
  } catch (err: any) {
    console.warn('[users] Failed to read the user directory:', err?.message || err)
    throw createError({ statusCode: 503, statusMessage: 'The user directory is currently unavailable.' })
  }
})
