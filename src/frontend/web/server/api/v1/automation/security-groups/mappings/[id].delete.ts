import { defineEventHandler, createError, getQuery } from 'h3'
import { verifyAutomationApiKey } from '../../../../../utils/verifyApiKey'
import { deleteSecurityGroupMapping, getSecurityGroupMappingById } from '../../../../../utils/securityGroupMappingsStore'

export default defineEventHandler((event) => {
  const keyRecord = verifyAutomationApiKey(event, 'security_groups:write')
  const id = event.context.params?.id

  if (!id) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Mapping ID parameter is required.'
    })
  }

  const existing = getSecurityGroupMappingById(id)
  if (!existing) {
    throw createError({
      statusCode: 404,
      statusMessage: `Security group mapping '${id}' not found.`
    })
  }

  const query = getQuery(event)
  const ticketRef = query.ticketId as string | undefined

  const success = deleteSecurityGroupMapping(id)

  return {
    success,
    message: `Security group mapping '${existing.displayName}' (${id}) removed via IT automation.`,
    ticketReference: ticketRef || null,
    authenticatedKey: {
      keyId: keyRecord.id,
      name: keyRecord.name
    }
  }
})
