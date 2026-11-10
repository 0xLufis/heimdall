import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { backendFetch } from '../../utils/backendFetch'
import { fromBackendGroup, toBackendGroup } from '../../utils/machineGroupBackend'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Missing group id' })
  const body = await readBody(event)

  // Backend PUT replaces every field (missing machineIdsJson resets membership to "[]"), so merge the
  // partial PATCH body onto the current entity first.
  const current = fromBackendGroup(await backendFetch(event, `machinegroup/${id}`))
  const updated = await backendFetch(event, `machinegroup/${id}`, {
    method: 'PUT',
    body: toBackendGroup({ ...current, ...(body || {}) })
  })
  return fromBackendGroup(updated)
})
