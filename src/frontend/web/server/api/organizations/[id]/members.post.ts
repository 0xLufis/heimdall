import { defineEventHandler, getRouterParam, readBody, createError, setResponseStatus } from 'h3'
import { randomUUID } from 'node:crypto'
import { eq, sql } from 'drizzle-orm'
import { useDb } from '../../../utils/db'
import * as hbSchema from '../../../database/drizzle/schema'
import { assertOrganizationAction, memberRoleOf, requireOrganization, requireOrganizationActor } from '../../../utils/organizationAccess'
import { evictBackendSessionCacheForUser } from '../../../utils/sessionSync'

/**
 * Adds an existing user (by `userId` or `email`) to the organization with a member role
 * (owner / admin / member, default member). 404 when no such user exists - invite them instead
 * (POST /api/organizations/:id/invitations).
 */
export default defineEventHandler(async (event) => {
  const caller = await requireOrganizationActor(event)
  const organization = await requireOrganization(getRouterParam(event, 'id'))
  const body = await readBody(event).catch(() => ({}))
  const role = typeof body?.role === 'string' && body.role ? body.role : 'member'
  assertOrganizationAction(caller, 'manageMembers', { actorMemberRole: await memberRoleOf(caller.id, organization.id), grantedRole: role })

  const db = useDb()
  const userId = typeof body?.userId === 'string' ? body.userId.trim() : ''
  const email = typeof body?.email === 'string' ? body.email.trim().toLowerCase() : ''
  if (!userId && !email) throw createError({ statusCode: 400, statusMessage: 'A userId or an email is required.' })
  const users = await db
    .select({ id: hbSchema.user.id, name: hbSchema.user.name, email: hbSchema.user.email, username: hbSchema.user.username })
    .from(hbSchema.user)
    .where(userId ? eq(hbSchema.user.id, userId) : sql`lower(${hbSchema.user.email}) = ${email}`)
    .limit(1)
  const target = users[0]
  if (!target) throw createError({ statusCode: 404, statusMessage: 'No user with that id or e-mail exists. Send an invitation instead.' })

  if (await memberRoleOf(target.id, organization.id)) {
    throw createError({ statusCode: 409, statusMessage: `${target.name} is already a member of ${organization.name}.` })
  }

  const createdAt = new Date()
  const member = { id: randomUUID(), organizationId: organization.id, userId: target.id, role, createdAt }
  await db.insert(hbSchema.member).values(member)
  await evictBackendSessionCacheForUser(target.id)

  setResponseStatus(event, 201)
  return { success: true, member: { id: member.id, role, createdAt, user: target } }
})
