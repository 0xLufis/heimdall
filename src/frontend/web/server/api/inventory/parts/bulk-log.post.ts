import { defineEventHandler, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body) {
    throw createError({ statusCode: 400, message: 'Request body is required.' })
  }

  const items = Array.isArray(body) ? body : (body.items || body.parts)
  if (!Array.isArray(items) || items.length === 0) {
    throw createError({ statusCode: 400, message: 'An array of parts is required for bulk logging.' })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Technician' }
  const created = partsInventoryStore.bulkLogParts(items, actor)

  return {
    success: true,
    count: created.length,
    parts: created
  }
})
