import { defineEventHandler, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)

  if (!body || !body.template || !body.template.name) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Invalid template payload. Template name is required.'
    })
  }

  try {
    const actor = body.actor || { id: 'usr-admin', name: 'Inventory Manager' }
    const created = partsInventoryStore.createTemplate(body.template, actor)
    return {
      success: true,
      template: created
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      statusMessage: err.message || 'Failed to create asset template.'
    })
  }
})
