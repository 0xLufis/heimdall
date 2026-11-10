import { defineEventHandler, getRouterParam, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Missing rule id' })
  await backendFetch(event, `technician/rules/${id}`, { method: 'DELETE' })
  return { success: true }
})
