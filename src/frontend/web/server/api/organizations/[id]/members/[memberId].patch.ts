import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { and, eq } from 'drizzle-orm'
import { useDb } from '../../../../utils/db'
import * as hbSchema from '../../../../database/drizzle/schema'
import { assertOrganizationAction, memberRoleOf, requireOrganization, requireOrganizationActor } from '../../../../utils/organizationAccess'
import { evictBackendSessionCacheForUser } from '../../../../utils/sessionSync'

/** Changes a member's role in the organization (owner / admin / member). */
export default defineEventHandler(async (event) => {
  const caller = await requireOrganizationActor(event)
  const organization = await requireOrganization(getRouterParam(event, 'id'))
  const memberId = getRouterParam(event, 'memberId')
  const body = await readBody(event).catch(() => ({}))
  const role = typeof body?.role === 'string' ? body.role : ''
  if (!memberId || !role) throw createError({ statusCode: 400, statusMessage: 'A member and a role are required.' })

  const db = useDb()
  const rows = await db.select({ id: hbSchema.member.id, userId: hbSchema.member.userId, role: hbSchema.member.role })
    .from(hbSchema.member)
    .where(and(eq(hbSchema.member.id, memberId), eq(hbSchema.member.organizationId, organization.id)))
    .limit(1)
  const target = rows[0]
  if (!target) throw createError({ statusCode: 404, statusMessage: 'Member not found in this organization.' })

  assertOrganizationAction(caller, 'manageMembers', {
    actorMemberRole: await memberRoleOf(caller.id, organization.id),
    grantedRole: role,
    targetMemberRole: target.role,
    targetIsActor: target.userId === caller.id
  })

  await db.update(hbSchema.member).set({ role }).where(eq(hbSchema.member.id, target.id))
  await evictBackendSessionCacheForUser(target.userId)
  return { success: true, member: { ...target, role } }
})
