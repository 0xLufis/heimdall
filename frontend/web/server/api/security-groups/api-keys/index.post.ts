import { defineEventHandler, readBody, createError } from 'h3'
import { generateAutomationApiKey } from '../../../utils/automationApiKeysStore'

export default defineEventHandler(async (event) => {
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
    createdBy: body.createdBy || 'it_admin',
    description: body.description?.trim()
  })

  return {
    success: true,
    apiKey: result.apiKey,
    rawKey: result.rawKey, // Returned only once upon creation
    message: 'API Key generated successfully. Copy the secret key now; it cannot be viewed again.'
  }
})
