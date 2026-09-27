import { defineEventHandler, readBody, createError } from 'h3'
import { verifyAutomationApiKey } from '../../../../utils/verifyApiKey'
import { evaluateSecurityGroupOrgMapping } from '../../../../utils/securityGroupOrgSync'

export default defineEventHandler(async (event) => {
  // 1. Authenticate API Key with evaluate or read scope
  const keyRecord = verifyAutomationApiKey(event, 'security_groups:evaluate')

  // 2. Read body
  const body = await readBody(event)

  const groupIdentifiers: string[] = Array.isArray(body?.groupIdentifiers)
    ? body.groupIdentifiers
    : typeof body?.groupIdentifiers === 'string'
      ? body.groupIdentifiers.split('\n').map((s: string) => s.trim()).filter(Boolean)
      : []

  if (groupIdentifiers.length === 0) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Bad Request: "groupIdentifiers" must be a non-empty array of group IDs or DNs.'
    })
  }

  // 3. Evaluate mapping
  const evaluation = evaluateSecurityGroupOrgMapping(groupIdentifiers)

  const resolvedRoles = Array.from(new Set(evaluation.matchedGroups.map(g => g.mappedRole)))

  return {
    success: true,
    inputCount: groupIdentifiers.length,
    matchedCount: evaluation.matchedGroups.length,
    resolvedRoles,
    matchedGroups: evaluation.matchedGroups,
    targetOrganizations: evaluation.targetOrganizations,
    suggestedActiveOrganization: evaluation.suggestedActiveOrganization || null,
    evaluationTimestamp: new Date().toISOString(),
    ticketContext: {
      ticketId: body?.ticketId || null,
      userId: body?.userId || null,
      userEmail: body?.userEmail || null
    },
    authenticatedKey: {
      keyId: keyRecord.id,
      name: keyRecord.name
    }
  }
})
