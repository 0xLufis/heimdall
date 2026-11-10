import { defineEventHandler, getRouterParam, createError, setResponseStatus } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

/** Deletes a recipe via `DELETE /api/v1/delegation/recipes/{id}` (backend enforces DelegationManagement + tenant scope). */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Identifier is required.' })
  await backendFetch(event, `delegation/recipes/${encodeURIComponent(id)}`, { method: 'DELETE' })
  setResponseStatus(event, 204)
  return null
})
