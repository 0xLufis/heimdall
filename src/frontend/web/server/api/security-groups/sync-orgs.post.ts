import { defineEventHandler, readBody, createError } from 'h3'
import { syncUserSecurityGroupsToOrganizations } from '../../utils/securityGroupOrgSync'
import { requirePolicy } from '../../utils/authz'

// Writes organization memberships (auth.member) for an arbitrary user, so it is restricted to IT
// administrators. It used to be anonymous: any caller could enrol any user - including themselves -
// into any mapped organization as owner or admin.
export default defineEventHandler(async (event) => {
  await requirePolicy(event, 'ItAdministration')
  const body = await readBody(event)
  const userId = typeof body?.userId === 'string' ? body.userId.trim() : ''
  if (!userId) {
    throw createError({ statusCode: 400, statusMessage: 'userId is required.' })
  }
  const groupIdentifiers = Array.isArray(body?.groupIdentifiers)
    ? body.groupIdentifiers
    : typeof body?.groupIdentifiers === 'string'
      ? body.groupIdentifiers.split('\n').map((s: string) => s.trim()).filter(Boolean)
      : []

  return await syncUserSecurityGroupsToOrganizations(userId, groupIdentifiers)
})
