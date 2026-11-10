import { defineEventHandler, getRouterParam, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

/**
 * Component slots for a machine (backend: ComponentController.GetMachineComponents).
 * Software, license, document and service-log quadrants have no backend endpoint yet and are
 * returned empty rather than served from fabricated in-memory seed data.
 */
export default defineEventHandler(async (event) => {
  const machineId = getRouterParam(event, 'id')
  if (!machineId) throw createError({ statusCode: 400, statusMessage: 'Machine ID is required.' })

  const slots = await backendFetch<unknown[]>(event, `component/machine/${machineId}`)
  return {
    slots: Array.isArray(slots) ? slots : [],
    software: [],
    licenses: [],
    documents: [],
    serviceLogs: []
  }
})
