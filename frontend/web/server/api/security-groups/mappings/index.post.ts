import { defineEventHandler, readBody, createError } from 'h3'
import { createSecurityGroupMapping } from '../../../utils/securityGroupMappingsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)

  if (!body || !body.groupIdentifier || !body.displayName || !body.mappedRole) {
    throw createError({
      statusCode: 400,
      statusMessage: 'groupIdentifier, displayName, and mappedRole are required.'
    })
  }

  const result = createSecurityGroupMapping({
    identityProvider: body.identityProvider || 'EntraID',
    groupIdentifier: body.groupIdentifier,
    displayName: body.displayName,
    mappedRole: body.mappedRole,
    organizationId: body.organizationId || null,
    isEnabled: body.isEnabled !== false,
    sourceTicketId: body.sourceTicketId || body.ticketId || null,
    sourceSystem: body.sourceSystem || 'Heimdall Web UI',
    requestedBy: body.requestedBy || (event.context as any)?.auth?.user?.email || 'it_admin',
    approvedBy: body.approvedBy || (event.context as any)?.auth?.user?.name || 'IT Administrator',
    reason: body.reason || 'Configured via Heimdall Security Groups Dashboard'
  })

  return result.mapping
})
