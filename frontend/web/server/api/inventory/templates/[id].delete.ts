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

  let actor = { id: 'usr-admin', name: 'Inventory Manager' }
  try {
    const body = await readBody(event)
    if (body?.actor) actor = body.actor
  } catch {
    // Body optional on DELETE
  }

  try {
    const success = partsInventoryStore.deleteTemplate(id, actor)
    return {
      success,
      deletedId: id
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      statusMessage: err.message || `Failed to delete asset template "${id}".`
    })
  }
})
