import { defineEventHandler, readBody, createError, setResponseStatus } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.technicianName || !body?.targetId) {
    throw createError({ statusCode: 400, statusMessage: 'technicianName and targetId are required' })
  }
  const created = await backendFetch(event, 'technician/rules', { method: 'POST', body })
  setResponseStatus(event, 201)
  return created
})
