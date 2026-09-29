import { defineEventHandler, getRouterParam, createError } from 'h3'
import { partsInventoryStore } from '../../../../utils/partsInventoryStore'

export default defineEventHandler((event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Template ID parameter is required.' })
  }

  const result = partsInventoryStore.getTemplateInstanceTree(id)
  if (!result) {
    throw createError({ statusCode: 404, message: `Asset template "${id}" not found.` })
  }

  return {
    success: true,
    ...result
  }
})
