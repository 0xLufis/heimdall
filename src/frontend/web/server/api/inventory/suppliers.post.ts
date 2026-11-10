import { defineEventHandler, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body || !body.name) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Supplier name is required.'
    })
  }

  return partsInventoryStore.createSupplier(body)
})
