import { defineEventHandler, getRouterParam, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler((event) => {
  const componentId = getRouterParam(event, 'id')
  if (!componentId) throw createError({ statusCode: 400, statusMessage: 'Component ID is required.' })
  return backendFetch(event, `component/${componentId}/history`)
})
