import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { eq } from 'drizzle-orm'
import { useDb } from '../../../utils/db'
import * as hbSchema from '../../../database/drizzle/schema'
import { assertOrganizationAction, memberRoleOf, requireOrganization, requireOrganizationActor } from '../../../utils/organizationAccess'

/** Renames an organization (Heimdall administrators: any; other managers: their own). */
export default defineEventHandler(async (event) => {
  const caller = await requireOrganizationActor(event)
  const organization = await requireOrganization(getRouterParam(event, 'id'))
  assertOrganizationAction(caller, 'update', { actorMemberRole: await memberRoleOf(caller.id, organization.id) })

  const body = await readBody(event).catch(() => ({}))
  const name = typeof body?.name === 'string' ? body.name.trim() : ''
  if (!name) throw createError({ statusCode: 400, statusMessage: 'An organization name is required.' })

  // The slug is the organization's stable identifier (AD OU matching, links); a rename keeps it.
  await useDb().update(hbSchema.organization).set({ name }).where(eq(hbSchema.organization.id, organization.id))
  return { success: true, organization: { ...organization, name } }
})
