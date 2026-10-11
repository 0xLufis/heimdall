import { evaluateSecurityGroupOrgMapping } from '../../utils/securityGroupOrgSync'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const groupIdentifiers = Array.isArray(body?.groupIdentifiers)
    ? body.groupIdentifiers
    : typeof body?.groupIdentifiers === 'string'
      ? body.groupIdentifiers.split('\n').map((s: string) => s.trim()).filter(Boolean)
      : []

  return evaluateSecurityGroupOrgMapping(groupIdentifiers)
})
