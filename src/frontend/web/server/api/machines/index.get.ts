import { defineEventHandler, getQuery } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

/**
 * Machines visible to the caller's active organization (backend: MachineController.GetMachines).
 * The backend has no server-side filters for these, so the legacy query options are applied here.
 */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const status = typeof query.status === 'string' ? query.status : undefined
  const includeDecommissioned = query.includeDecommissioned === 'true' || query.includeDecommissioned === '1' || query.includeDecommissioned === true
  const groupId = typeof query.groupId === 'string' ? query.groupId : undefined

  const machines = await backendFetch<any[]>(event, 'machine')
  let result = Array.isArray(machines) ? machines : []

  if (status) {
    result = result.filter(m => (m.status || 'active') === status)
  } else if (!includeDecommissioned) {
    result = result.filter(m => !['decommissioned', 'dismantled'].includes(m.status || 'active'))
  }
  if (groupId) {
    result = result.filter(m =>
      [m.groupId, m.subLineGroupId, m.lineGroupId, m.projectGroupId, m.businessUnitGroupId].includes(groupId)
    )
  }
  return result
})
