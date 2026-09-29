import { defineEventHandler, getRouterParam, getQuery, createError } from 'h3'
import { partsInventoryStore } from '../../../../utils/partsInventoryStore'

export default defineEventHandler((event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Part ID parameter is required.' })
  }

  const query = getQuery(event)
  const currency = (query.currency as string) || 'EUR'

  const part = partsInventoryStore.getPartByIdOrIdentifier(id, currency)
  if (!part) {
    throw createError({ statusCode: 404, message: `Part "${id}" not found.` })
  }

  return {
    success: true,
    part
  }
})
