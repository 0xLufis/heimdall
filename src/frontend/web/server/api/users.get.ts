import { defineEventHandler, createError } from 'h3'
import { eq } from 'drizzle-orm'
import { useDb } from '../utils/db'
import * as hbSchema from '../database/drizzle/schema'
import { requireUser } from '../utils/authz'
import { splitPermissions } from '../../app/utils/roles'

/**
 * User directory for the administration pages, the user pickers and the user profile page.
 *
 * Reads Better-Auth's own `auth.user` table (the source of truth for identities) and requires a
 * signed-in caller. It used to be anonymous, went through the backend first (whose projection
 * lacks `banned`, `username` and `createdAt`, so suspensions never showed up) and answered with a
 * hard-coded profile catalogue when both sources failed.
 *
 * The directory is global (owner decision): every signed-in user sees every user - name, e-mail,
 * role, organizations and explicit permissions - across organizations, like the backend's
 * `GET /api/v1/auth/users` and its search endpoint. Changing users (role, ban, sessions,
 * permissions) keeps its own guards.
 */
export default defineEventHandler(async (event) => {
  await requireUser(event)

  try {
    const db = useDb()
    const [dbUsers, memberships] = await Promise.all([
      db
        .select({
          id: hbSchema.user.id,
          name: hbSchema.user.name,
          email: hbSchema.user.email,
          role: hbSchema.user.role,
          username: hbSchema.user.username,
          banned: hbSchema.user.banned,
          permissions: hbSchema.user.permissions,
          createdAt: hbSchema.user.createdAt
        })
        .from(hbSchema.user),
      db
        .select({
          userId: hbSchema.member.userId,
          organizationId: hbSchema.member.organizationId,
          organizationName: hbSchema.organization.name,
          memberRole: hbSchema.member.role
        })
        .from(hbSchema.member)
        .innerJoin(hbSchema.organization, eq(hbSchema.member.organizationId, hbSchema.organization.id))
    ])

    const organizationsByUser = new Map<string, Array<{ id: string; name: string; role: string }>>()
    for (const m of memberships) {
      const list = organizationsByUser.get(m.userId) ?? []
      list.push({ id: m.organizationId, name: m.organizationName, role: m.memberRole })
      organizationsByUser.set(m.userId, list)
    }

    return {
      success: true,
      users: dbUsers.map(u => {
        const organizations = (organizationsByUser.get(u.id) ?? []).sort((a, b) => String(a.name ?? '').localeCompare(String(b.name ?? '')))
        return {
          ...u,
          banned: u.banned === true,
          permissions: splitPermissions(u.permissions),
          organizations,
          organizationCount: organizations.length
        }
      })
    }
  } catch (err: any) {
    console.warn('[users] Failed to read the user directory:', err?.message || err)
    throw createError({ statusCode: 503, statusMessage: 'The user directory is currently unavailable.' })
  }
})
