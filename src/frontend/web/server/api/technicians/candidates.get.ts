import { defineEventHandler, getQuery } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

/** Assignable technicians (backend: TechnicianController.GetCandidates) with role/search filtering. */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const roleFilter = query.role ? String(query.role).toLowerCase() : undefined
  const search = query.search ? String(query.search).toLowerCase() : undefined

  const data = await backendFetch<any[]>(event, 'technician/candidates')
  let result = Array.isArray(data) ? data : []

  if (roleFilter) {
    if (roleFilter === 'technician') {
      result = result.filter(c => c.role === 'technician')
    } else if (roleFilter === 'engineer_technician') {
      result = result.filter(c => c.role === 'technician' || c.role === 'engineer' || c.role === 'group_leader')
    } else {
      result = result.filter(c => c.role === roleFilter)
    }
  }
  if (search) {
    result = result.filter(c =>
      (c.name && String(c.name).toLowerCase().includes(search)) ||
      (c.email && String(c.email).toLowerCase().includes(search)) ||
      (c.department && String(c.department).toLowerCase().includes(search)) ||
      (c.specialization && String(c.specialization).toLowerCase().includes(search))
    )
  }
  return result
})
