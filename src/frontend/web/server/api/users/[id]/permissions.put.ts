import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { eq } from 'drizzle-orm'
import { useDb } from '../../../utils/db'
import * as hbSchema from '../../../database/drizzle/schema'
import { requireUser } from '../../../utils/authz'
import { evictBackendSessionCacheForUser } from '../../../utils/sessionSync'
import { GRANTABLE_PERMISSIONS, roleSatisfies, splitPermissions } from '../../../../app/utils/roles'

/**
 * Grants or revokes explicit per-user permissions (today only `organizations.manage`).
 *
 * Body: `{ permissions: string[] }` (the complete new set) or `{ grant?: string[], revoke?: string[] }`.
 * Only callers who hold the OrganizationManagement right by role (Heimdall administrators, middle
 * managers and up) may change grants - a granted user cannot pass the grant on.
 */
export default defineEventHandler(async (event) => {
  const caller = await requireUser(event)
  if (!roleSatisfies(caller.role, 'OrganizationManagement')) {
    throw createError({ statusCode: 403, statusMessage: 'Forbidden: only Heimdall administrators and managers may grant or revoke permissions.' })
  }

  const userId = getRouterParam(event, 'id')
  if (!userId) throw createError({ statusCode: 400, statusMessage: 'User ID is required.' })
  const body = await readBody(event).catch(() => ({}))

  const db = useDb()
  const rows = await db.select({ id: hbSchema.user.id, permissions: hbSchema.user.permissions }).from(hbSchema.user).where(eq(hbSchema.user.id, userId)).limit(1)
  const target = rows[0]
  if (!target) throw createError({ statusCode: 404, statusMessage: 'User not found.' })

  const requested = Array.isArray(body?.permissions)
    ? splitPermissions(body.permissions)
    : [...new Set([...splitPermissions(target.permissions), ...splitPermissions(body?.grant)])]
        .filter(p => !splitPermissions(body?.revoke).includes(p))
  const unknown = requested.filter(p => !Object.prototype.hasOwnProperty.call(GRANTABLE_PERMISSIONS, p))
  if (unknown.length > 0) {
    throw createError({ statusCode: 400, statusMessage: `Unknown permission(s): ${unknown.join(', ')}. Grantable: ${Object.keys(GRANTABLE_PERMISSIONS).join(', ')}.` })
  }

  const permissions = requested.length > 0 ? requested.join(',') : null
  await db.update(hbSchema.user).set({ permissions }).where(eq(hbSchema.user.id, userId))
  // The backend turns grants into claims and caches them per session.
  await evictBackendSessionCacheForUser(userId)
  return { success: true, userId, permissions: requested }
})
