import { defineEventHandler, readBody, createError, setResponseStatus } from 'h3'
import { verifyAutomationApiKey } from '../../../../utils/verifyApiKey'
import { createSecurityGroupMapping } from '../../../../utils/securityGroupMappingsStore'

export default defineEventHandler(async (event) => {
  // 1. Authenticate API Key with required write scope
  const keyRecord = verifyAutomationApiKey(event, 'security_groups:write')

  // 2. Parse body
  const body = await readBody(event)

  if (!body || !body.groupIdentifier || !body.displayName || !body.mappedRole) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Bad Request: "groupIdentifier", "displayName", and "mappedRole" are required fields.',
      data: {
        received: body
      }
    })
  }

  // 3. Register or update mapping with ticketing metadata
  const { mapping, auditTicket } = createSecurityGroupMapping({
    identityProvider: body.identityProvider || 'EntraID',
    groupIdentifier: body.groupIdentifier,
    displayName: body.displayName,
    mappedRole: body.mappedRole,
    organizationId: body.organizationId || null,
    isEnabled: body.isEnabled !== false,
    sourceTicketId: body.ticketId || body.sourceTicketId || null,
    sourceSystem: body.sourceSystem || keyRecord.systemType || 'Outside IT Automation',
    requestedBy: body.requestedBy || null,
    approvedBy: body.approvedBy || null,
    reason: body.reason || `Automated provisioning via external ticket ${body.ticketId || ''}`.trim(),
    autoCreateAuditTicket: body.autoCreateAuditTicket !== false,
    apiKeyName: keyRecord.name
  })

  setResponseStatus(event, 201)

  return {
    success: true,
    message: `Security group mapping '${mapping.displayName}' provisioned successfully via outside IT automation.`,
    mapping,
    auditTrail: auditTicket ? {
      heimdallTicketId: auditTicket.id,
      ticketNumber: auditTicket.ticketNumber,
      externalTicketRef: body.ticketId || null,
      status: auditTicket.status
    } : null,
    authenticatedKey: {
      keyId: keyRecord.id,
      name: keyRecord.name,
      systemType: keyRecord.systemType
    }
  }
})
