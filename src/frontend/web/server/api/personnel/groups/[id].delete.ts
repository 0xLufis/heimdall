import { defineEventHandler, getRouterParam, createError, setResponseStatus } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

/** Deletes a personnel group via `DELETE /api/v1/delegation/groups/{id}` (backend enforces DelegationManagement + tenant scope). */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Identifier is required.' })
  await backendFetch(event, `delegation/groups/${encodeURIComponent(id)}`, { method: 'DELETE' })
  setResponseStatus(event, 204)
  return null
})
