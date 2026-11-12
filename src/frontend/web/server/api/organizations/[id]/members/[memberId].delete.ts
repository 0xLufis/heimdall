import { defineEventHandler, getRouterParam, createError } from 'h3'
import { and, eq } from 'drizzle-orm'
import { useDb } from '../../../../utils/db'
import * as hbSchema from '../../../../database/drizzle/schema'
import { assertOrganizationAction, memberRoleOf, requireOrganization, requireOrganizationActor } from '../../../../utils/organizationAccess'
import { evictBackendSessionCacheForUser } from '../../../../utils/sessionSync'

/** Removes a member from the organization; sessions that had it active fall back to none. */
export default defineEventHandler(async (event) => {
  const caller = await requireOrganizationActor(event)
  const organization = await requireOrganization(getRouterParam(event, 'id'))
  const memberId = getRouterParam(event, 'memberId')
  if (!memberId) throw createError({ statusCode: 400, statusMessage: 'A member is required.' })

  const db = useDb()
  const rows = await db.select({ id: hbSchema.member.id, userId: hbSchema.member.userId, role: hbSchema.member.role })
    .from(hbSchema.member)
    .where(and(eq(hbSchema.member.id, memberId), eq(hbSchema.member.organizationId, organization.id)))
    .limit(1)
  const target = rows[0]
  if (!target) throw createError({ statusCode: 404, statusMessage: 'Member not found in this organization.' })

  assertOrganizationAction(caller, 'manageMembers', {
    actorMemberRole: await memberRoleOf(caller.id, organization.id),
    targetMemberRole: target.role,
    targetIsActor: target.userId === caller.id
  })

  await db.transaction(async (tx) => {
    await tx.delete(hbSchema.member).where(eq(hbSchema.member.id, target.id))
    await tx.update(hbSchema.session).set({ activeOrganizationId: null })
      .where(and(eq(hbSchema.session.userId, target.userId), eq(hbSchema.session.activeOrganizationId, organization.id)))
  })
  await evictBackendSessionCacheForUser(target.userId)
  return { success: true, removed: target.id }
})
