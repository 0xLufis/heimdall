import { defineEventHandler, getRouterParam, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Missing absence id' })
  await backendFetch(event, `technician/absences/${id}`, { method: 'DELETE' })
  return { success: true }
})
