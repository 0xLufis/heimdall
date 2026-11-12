import { defineEventHandler, readBody, createError } from 'h3'
import { generateAutomationApiKey } from '../../../utils/automationApiKeysStore'
import { requirePolicy } from '../../../utils/authz'

export default defineEventHandler(async (event) => {
  // Minting an automation key hands out write access to security-group mappings and organization
  // enrolment; this route used to be anonymous and let the caller pick any scope.
  const caller = await requirePolicy(event, 'ItAdministration')
  const body = await readBody(event)

  if (!body || !body.name || typeof body.name !== 'string' || !body.name.trim()) {
    throw createError({
      statusCode: 400,
      statusMessage: 'API Key name is required.'
    })
  }

  const result = generateAutomationApiKey({
    name: body.name.trim(),
    systemType: body.systemType || 'Custom',
    scopes: Array.isArray(body.scopes) && body.scopes.length > 0 ? body.scopes : undefined,
    expiresInDays: typeof body.expiresInDays === 'number' ? body.expiresInDays : null,
    createdBy: caller.email || caller.id,
    description: body.description?.trim()
  })

  return {
    success: true,
    apiKey: result.apiKey,
    rawKey: result.rawKey, // Returned only once upon creation
    message: 'API Key generated successfully. Copy the secret key now; it cannot be viewed again.'
  }
})
