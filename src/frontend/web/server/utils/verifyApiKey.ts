import { H3Event, getHeader, getQuery, createError } from 'h3'
import { validateAutomationApiKey, AutomationApiKey } from './automationApiKeysStore'

export interface VerifiedAutomationContext {
  apiKey: AutomationApiKey
  isAutomation: true
}

/**
 * Extracts and verifies an external automation API key from an H3Event.
 * Checks 'X-API-Key', 'Authorization: Bearer <key>', and 'X-Extension-Key' headers.
 * Throws H3 401 (Unauthorized) or 403 (Forbidden) if invalid, expired, or missing scope.
 */
export function verifyAutomationApiKey(
  event: H3Event,
  requiredScope?: string
): AutomationApiKey {
  let rawKey: string | undefined

  const getHeaderValue = (name: string): string | undefined => {
    const lowerName = name.toLowerCase()
    try {
      const h = getHeader(event, name)
      if (h) return h
    } catch {}

    if (event?.headers) {
      if (typeof (event.headers as any).get === 'function') {
        const val = (event.headers as any).get(name) || (event.headers as any).get(lowerName)
        if (val) return val
      } else {
        const hMap = event.headers as Record<string, string | undefined>
        if (hMap[name] || hMap[lowerName]) return hMap[name] || hMap[lowerName]
      }
    }

    if (event?.node?.req?.headers) {
      const reqH = event.node.req.headers
      return (reqH[lowerName] || reqH[name]) as string | undefined
    }

    return undefined
  }

  // 1. Check X-API-Key header (standard REST API key)
  const xApiKey = getHeaderValue('x-api-key')
  if (xApiKey) {
    rawKey = xApiKey
  }

  // 2. Check Authorization Bearer header
  if (!rawKey) {
    const authHeader = getHeaderValue('authorization')
    if (authHeader && authHeader.toLowerCase().startsWith('bearer ')) {
      rawKey = authHeader.slice(7).trim()
    }
  }

  // 3. Check X-Extension-Key header
  if (!rawKey) {
    const extKey = getHeaderValue('x-extension-key')
    if (extKey) {
      rawKey = extKey
    }
  }

  // 4. Query param fallback (?api_key=...)
  if (!rawKey) {
    try {
      const query = getQuery(event)
      if (typeof query?.api_key === 'string' && query.api_key.trim()) {
        rawKey = query.api_key.trim()
      }
    } catch {}
  }

  if (!rawKey) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Missing API Key. Provide via "X-API-Key" or "Authorization: Bearer <key>" header.',
      data: {
        error: 'missing_api_key',
        hint: 'Generate an outside IT automation API key on /dashboard/security-groups.'
      }
    })
  }

  const keyRecord = validateAutomationApiKey(rawKey)
  if (!keyRecord) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Invalid, expired, or deactivated API Key.',
      data: {
        error: 'invalid_api_key'
      }
    })
  }

  // Check specific scope if needed
  if (requiredScope && !keyRecord.scopes.includes(requiredScope) && !keyRecord.scopes.includes('*') && !keyRecord.scopes.includes('admin')) {
    throw createError({
      statusCode: 403,
      statusMessage: `Forbidden: API Key lacks the required scope: '${requiredScope}'.`,
      data: {
        error: 'insufficient_scope',
        grantedScopes: keyRecord.scopes,
        requiredScope
      }
    })
  }


  // Attach to event context for downstream handlers
  if (event && event.context) {
    event.context.automationKey = keyRecord
  }

  return keyRecord
}
