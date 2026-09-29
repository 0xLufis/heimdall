import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Template ID is required.'
    })
  }

  const body = await readBody(event)
  if (!body || !body.updates) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Update payload is required.'
    })
  }

  try {
    const actor = body.actor || { id: 'usr-admin', name: 'Inventory Manager' }
    const updated = partsInventoryStore.updateTemplate(id, body.updates, actor)
    return {
      success: true,
      template: updated
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      statusMessage: err.message || `Failed to update asset template "${id}".`
    })
  }
})
