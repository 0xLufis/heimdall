import { defineEventHandler, readBody, createError } from 'h3'
import { createSecurityGroupMapping } from '../../../utils/securityGroupMappingsStore'
import { requirePolicy } from '../../../utils/authz'

export default defineEventHandler(async (event) => {
  // Mapping a directory group to a role grants that role, so only IT administrators may do it.
  const caller = await requirePolicy(event, 'ItAdministration')
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
    // The audit trail names the signed-in administrator; it is never taken from the request body.
    requestedBy: caller.email || caller.id,
    approvedBy: caller.email || caller.id,
    reason: body.reason || 'Configured via Heimdall Security Groups Dashboard'
  })

  return result.mapping
})
