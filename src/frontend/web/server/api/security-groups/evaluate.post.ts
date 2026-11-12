import { defineEventHandler, readBody } from 'h3'
import { evaluateSecurityGroupOrgMapping } from '../../utils/securityGroupOrgSync'
import { requirePolicy } from '../../utils/authz'

export default defineEventHandler(async (event) => {
  await requirePolicy(event, 'ItAdministration')
  const body = await readBody(event)
  const groupIdentifiers = Array.isArray(body?.groupIdentifiers)
    ? body.groupIdentifiers
    : typeof body?.groupIdentifiers === 'string'
      ? body.groupIdentifiers.split('\n').map((s: string) => s.trim()).filter(Boolean)
      : []

  return evaluateSecurityGroupOrgMapping(groupIdentifiers)
})
