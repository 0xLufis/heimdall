import { defineEventHandler, readBody, createError } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

/** Assign an inventory part to a machine component slot (backend: ComponentController.AssignPart). */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.machineId || !body?.partId) {
    throw createError({ statusCode: 400, statusMessage: 'machineId and partId are required.' })
  }
  return backendFetch(event, 'component/assign-part', { method: 'POST', body })
})
