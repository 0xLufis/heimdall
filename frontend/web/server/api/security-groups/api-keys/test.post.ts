import { defineEventHandler, readBody, createError } from 'h3'
import { validateAutomationApiKey } from '../../../utils/automationApiKeysStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const rawKey = body?.rawKey || body?.apiKey

  if (!rawKey || typeof rawKey !== 'string') {
    throw createError({
      statusCode: 400,
      statusMessage: 'rawKey is required to test API key authentication.'
    })
  }

  const keyRecord = validateAutomationApiKey(rawKey, body?.requiredScope)

  if (!keyRecord) {
    return {
      valid: false,
      message: 'API Key is invalid, expired, revoked, or missing required scope.'
    }
  }

  return {
    valid: true,
    message: 'API Key is active and authorized for outside IT automation.',
    key: {
      id: keyRecord.id,
      name: keyRecord.name,
      systemType: keyRecord.systemType,
      scopes: keyRecord.scopes,
      createdAt: keyRecord.createdAt,
      expiresAt: keyRecord.expiresAt,
      lastUsedAt: keyRecord.lastUsedAt
    }
  }
})
