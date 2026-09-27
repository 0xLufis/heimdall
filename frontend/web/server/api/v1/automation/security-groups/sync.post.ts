import { defineEventHandler, readBody, createError } from 'h3'
import { verifyAutomationApiKey } from '../../../../utils/verifyApiKey'
import { syncUserSecurityGroupsToOrganizations } from '../../../../utils/securityGroupOrgSync'

export default defineEventHandler(async (event) => {
  const keyRecord = verifyAutomationApiKey(event, 'security_groups:write')
  const body = await readBody(event)

  if (!body?.userId || !Array.isArray(body?.groupIdentifiers)) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Bad Request: "userId" and "groupIdentifiers" (array) are required.'
    })
  }

  const result = await syncUserSecurityGroupsToOrganizations(body.userId, body.groupIdentifiers)

  return {
    success: true,
    message: `Synchronized ${result.enrolledOrganizations.length} organization(s) for user '${body.userId}'.`,
    syncResult: result,
    ticketReference: body.ticketId || null,
    authenticatedKey: {
      keyId: keyRecord.id,
      name: keyRecord.name
    }
  }
})
