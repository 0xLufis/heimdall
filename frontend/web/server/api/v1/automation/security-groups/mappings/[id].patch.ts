import { defineEventHandler, readBody, createError } from 'h3'
import { verifyAutomationApiKey } from '../../../../../utils/verifyApiKey'
import { updateSecurityGroupMapping } from '../../../../../utils/securityGroupMappingsStore'

export default defineEventHandler(async (event) => {
  const keyRecord = verifyAutomationApiKey(event, 'security_groups:write')
  const id = event.context.params?.id

  if (!id) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Mapping ID parameter is required.'
    })
  }

  const body = await readBody(event)

  const updated = updateSecurityGroupMapping(id, {
    ...(body?.displayName && { displayName: body.displayName }),
    ...(body?.mappedRole && { mappedRole: body.mappedRole }),
    ...(body?.organizationId !== undefined && { organizationId: body.organizationId }),
    ...(body?.isEnabled !== undefined && { isEnabled: Boolean(body.isEnabled) }),
    ...(body?.ticketId && { sourceTicketId: body.ticketId }),
    ...(body?.reason && { reason: body.reason }),
    sourceSystem: body?.sourceSystem || keyRecord.systemType || 'Outside IT Automation'
  })

  if (!updated) {
    throw createError({
      statusCode: 404,
      statusMessage: `Security group mapping '${id}' not found.`
    })
  }

  return {
    success: true,
    message: `Security group mapping '${id}' updated successfully.`,
    mapping: updated
  }
})
