import { defineEventHandler, getQuery } from 'h3'
import { getTeamsOooStatuses, getAllRules, SEEDED_CANDIDATES } from '../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const roleFilter = query.role ? String(query.role).toLowerCase() : undefined
  const search = query.search ? String(query.search).toLowerCase() : undefined

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const backendCandidates = await $fetch<any[]>(`${backendBase}/api/v1/technician/candidates`, {
      headers: event.headers as any
    })
    if (backendCandidates && Array.isArray(backendCandidates) && backendCandidates.length > 0) {
      let result = backendCandidates
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
          (c.name && c.name.toLowerCase().includes(search)) ||
          (c.email && c.email.toLowerCase().includes(search)) ||
          (c.department && c.department.toLowerCase().includes(search)) ||
          (c.specialization && c.specialization.toLowerCase().includes(search))
        )
      }
      return result
    }
  } catch {}

  const oooStatuses = getTeamsOooStatuses()
  const rules = getAllRules()

  let candidates = SEEDED_CANDIDATES.map(c => {
    const ooo = oooStatuses.find(s => s.userId === c.id || s.displayName.toLowerCase() === c.name.toLowerCase())
    const userRules = rules.filter(r => r.technicianId === c.id || r.technicianName.toLowerCase() === c.name.toLowerCase())
    return {
      ...c,
      isOutOfOffice: ooo ? ooo.isOutOfOffice : c.isOutOfOffice,
      assignedRulesCount: userRules.length
    }
  })

  if (roleFilter) {
    if (roleFilter === 'technician') {
      candidates = candidates.filter(c => c.role === 'technician')
    } else if (roleFilter === 'engineer_technician') {
      candidates = candidates.filter(c => c.role === 'technician' || c.role === 'engineer' || c.role === 'group_leader')
    } else {
      candidates = candidates.filter(c => c.role === roleFilter)
    }
  }

  if (search) {
    candidates = candidates.filter(c =>
      c.name.toLowerCase().includes(search) ||
      c.email.toLowerCase().includes(search) ||
      c.department.toLowerCase().includes(search) ||
      (c.specialization && c.specialization.toLowerCase().includes(search))
    )
  }

  return candidates
})
