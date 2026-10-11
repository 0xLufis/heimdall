import { defineEventHandler, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body) {
    throw createError({ statusCode: 400, message: 'Request body is required.' })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Technician' }
  const part = partsInventoryStore.logPart(body, actor)

  return {
    success: true,
    part
  }
})
