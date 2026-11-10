import { defineEventHandler, readBody, createError, setResponseStatus } from 'h3'
import { backendFetch } from '../../utils/backendFetch'
import { fromBackendGroup, toBackendGroup } from '../../utils/machineGroupBackend'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.name) {
    throw createError({ statusCode: 400, statusMessage: 'Group name is required' })
  }

  const created = await backendFetch(event, 'machinegroup', {
    method: 'POST',
    body: toBackendGroup(body, { machineIdsJson: '[]', machineTypesJson: null })
  })
  setResponseStatus(event, 201)
  return fromBackendGroup(created)
})
