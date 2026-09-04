import { syncUserSecurityGroupsToOrganizations } from '../../utils/securityGroupOrgSync'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const userId = body?.userId || 'usr-anonymous'
  const groupIdentifiers = Array.isArray(body?.groupIdentifiers)
    ? body.groupIdentifiers
    : typeof body?.groupIdentifiers === 'string'
      ? body.groupIdentifiers.split('\n').map((s: string) => s.trim()).filter(Boolean)
      : []

  return await syncUserSecurityGroupsToOrganizations(userId, groupIdentifiers)
})
