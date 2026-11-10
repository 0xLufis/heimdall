import { defineEventHandler, getRouterParam, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

/** Delegation recipes bound to a machine (backend: DelegationController.GetRecipesForMachine). */
export default defineEventHandler((event) => {
  const machineId = getRouterParam(event, 'id')
  if (!machineId) throw createError({ statusCode: 400, statusMessage: 'Machine ID is required.' })
  return backendFetch(event, `delegation/machine/${machineId}`)
})
