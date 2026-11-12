import { defineEventHandler, getRouterParam, readBody, createError, setResponseStatus } from 'h3'
import { randomUUID } from 'node:crypto'
import { useDb } from '../../../utils/db'
import * as hbSchema from '../../../database/drizzle/schema'
import { assertOrganizationAction, memberRoleOf, requireOrganization, requireOrganizationActor } from '../../../utils/organizationAccess'

/** Better-Auth's default invitation lifetime (48 hours). */
const INVITATION_TTL_MS = 48 * 60 * 60 * 1000

/**
 * Invites an e-mail address into the organization. The row has the shape of Better-Auth's
 * organization plugin, so the invitee accepts through `/api/auth/organization/accept-invitation`.
 */
export default defineEventHandler(async (event) => {
  const caller = await requireOrganizationActor(event)
  const organization = await requireOrganization(getRouterParam(event, 'id'))
  const body = await readBody(event).catch(() => ({}))
  const role = typeof body?.role === 'string' && body.role ? body.role : 'member'
  assertOrganizationAction(caller, 'manageMembers', { actorMemberRole: await memberRoleOf(caller.id, organization.id), grantedRole: role })

  const email = typeof body?.email === 'string' ? body.email.trim().toLowerCase() : ''
  if (!/^[^\s@]+@[^\s@]+$/.test(email)) throw createError({ statusCode: 400, statusMessage: 'A valid e-mail address is required.' })

  const now = new Date()
  const invitation = {
    id: randomUUID(),
    organizationId: organization.id,
    email,
    role,
    status: 'pending',
    expiresAt: new Date(now.getTime() + INVITATION_TTL_MS),
    createdAt: now,
    inviterId: caller.id
  }
  await useDb().insert(hbSchema.invitation).values(invitation)

  setResponseStatus(event, 201)
  return { success: true, invitation }
})
