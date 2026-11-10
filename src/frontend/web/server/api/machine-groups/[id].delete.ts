import { defineEventHandler, getRouterParam, createError, setResponseStatus } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Missing group id' })

  await backendFetch(event, `machinegroup/${id}`, { method: 'DELETE' })
  setResponseStatus(event, 204)
  return null
})
