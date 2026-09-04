import { getTeamsOooStatuses, getAllRules, SEEDED_CANDIDATES, type CandidateUser } from '../../utils/technicianRulesStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const roleFilter = query.role ? String(query.role).toLowerCase() : undefined
  const search = query.search ? String(query.search).toLowerCase() : undefined

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
