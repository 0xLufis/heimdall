import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Part ID parameter is required.' })
  }

  const body = (await readBody(event).catch(() => null)) || (event as any)._body || (event.node?.req as any)?.body
  if (!body) {
    throw createError({ statusCode: 400, message: 'Request body is required.' })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Technician' }

  try {
    const updatedPart = partsInventoryStore.editPart(id, body, actor)
    return {
      success: true,
      part: updatedPart
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      message: err.message || 'Failed to edit part.'
    })
  }
})
