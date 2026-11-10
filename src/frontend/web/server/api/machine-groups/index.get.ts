import { defineEventHandler } from 'h3'
import { backendFetch } from '../../utils/backendFetch'
import { fromBackendGroup } from '../../utils/machineGroupBackend'

export default defineEventHandler(async (event) => {
  const data = await backendFetch<any[]>(event, 'machinegroup')
  return Array.isArray(data) ? data.map(fromBackendGroup) : []
})
