import { defineEventHandler, getRouterParam } from 'h3'
import { eq } from 'drizzle-orm'
import { useDb } from '../../../utils/db'
import * as hbSchema from '../../../database/drizzle/schema'
import { assertOrganizationAction, memberRoleOf, requireOrganization, requireOrganizationActor } from '../../../utils/organizationAccess'
import { evictBackendSessionCacheForUser } from '../../../utils/sessionSync'

/** Deletes an organization (Heimdall administrators: any; other managers: only one they own). */
export default defineEventHandler(async (event) => {
  const caller = await requireOrganizationActor(event)
  const organization = await requireOrganization(getRouterParam(event, 'id'))
  assertOrganizationAction(caller, 'delete', { actorMemberRole: await memberRoleOf(caller.id, organization.id) })

  const db = useDb()
  const members = await db.select({ userId: hbSchema.member.userId }).from(hbSchema.member).where(eq(hbSchema.member.organizationId, organization.id))
  // Members and invitations go with it (ON DELETE CASCADE); sessions pointing at it are cleared.
  await db.transaction(async (tx) => {
    await tx.update(hbSchema.session).set({ activeOrganizationId: null }).where(eq(hbSchema.session.activeOrganizationId, organization.id))
    await tx.delete(hbSchema.organization).where(eq(hbSchema.organization.id, organization.id))
  })
  for (const m of members) await evictBackendSessionCacheForUser(m.userId)

  return { success: true, deleted: organization.id }
})
