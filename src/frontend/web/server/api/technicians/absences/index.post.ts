import { defineEventHandler, readBody, createError, setResponseStatus } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.technicianName || !body?.reason) {
    throw createError({ statusCode: 400, statusMessage: 'technicianName and reason are required' })
  }
  const created = await backendFetch(event, 'technician/absences', { method: 'POST', body })
  setResponseStatus(event, 201)
  return created
})
