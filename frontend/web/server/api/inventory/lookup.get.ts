import { defineEventHandler, getQuery, createError } from 'h3'
import { partsInventoryStore } from '../../utils/partsInventoryStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const code = (query.code as string || query.q as string || '').trim()

  if (!code) {
    throw createError({ statusCode: 400, message: 'Scan code or query string is required.' })
  }

  const currency = (query.currency as string) || 'EUR'
  const part = partsInventoryStore.getPartByIdOrIdentifier(code, currency)

  if (!part) {
    throw createError({ statusCode: 404, message: `No inventory part found matching code "${code}".` })
  }

  return {
    success: true,
    code,
    part
  }
})
