import { defineEventHandler, getQuery } from 'h3'

const BACKEND_BASE = process.env.BACKEND_API_URL || 'http://localhost:5001'

const PRIORITY_WEIGHT: Record<string, number> = {
  Critical: 4,
  High: 3,
  Medium: 2,
  Low: 1
}

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const statusFilter = (query.status as string || 'all').trim()
  const priorityFilter = (query.priority as string || 'all').trim()
  const searchQuery = (query.query as string || '').toLowerCase().trim()
  const stationFilter = (query.stationId as string || '').trim()
  const technicianFilter = (query.technicianId as string || '').trim()
  const sortBy = (query.sortBy as string || 'created_at').trim()
  const sortOrder = (query.sortOrder as string || 'desc').trim()

  const rawList = await $fetch<any[]>(`${BACKEND_BASE}/api/v1/MaintenanceTicket`, {
    headers: event.headers as any
  })

  const allTickets = (rawList ?? []).map((t: any) => ({
    id: t.id || t.Id,
    ticketNumber: t.ticketNumber || `TKT-${(t.id || '').substring(0, 8)}`,
    stationId: t.machineId || t.stationId,
    stationName: t.machine?.name || t.machine?.customIdentifier || t.stationName || '',
    controllerId: t.clientPcId || t.controllerId,
    controllerName: t.clientPc?.hostname || t.controllerName,
    title: t.title || t.Title || '',
    description: t.description || t.Description || '',
    status: t.status || t.Status || 'Open',
    priority: t.priority || t.Priority || 'Medium',
    pendingReason: t.pendingReason ?? null,
    pendingDetails: t.pendingDetails ?? null,
    isEscalated: t.isEscalated ?? false,
    escalationReason: t.escalationReason ?? null,
    reportedByUserName: t.createdBy || '',
    assignedTechnicianName: t.assignedTo || '',
    createdAt: t.createdAt || t.CreatedAt,
    updatedAt: t.updatedAt || t.UpdatedAt,
    slaDueAt: t.slaDueAt ?? null,
    comments: t.comments ?? [],
    attachments: t.attachments ?? []
  }))

  const now = new Date()
  const overdueCount = allTickets.filter(
    t => t.slaDueAt && new Date(t.slaDueAt) < now && t.status !== 'Closed' && t.status !== 'Resolved'
  ).length
  const slaCompliancePercent = allTickets.length > 0
    ? Math.round(((allTickets.length - overdueCount) / allTickets.length) * 100)
    : 100

  let filtered = [...allTickets]

  if (statusFilter !== 'all') {
    filtered = filtered.filter(t => t.status === statusFilter)
  }
  if (priorityFilter !== 'all') {
    filtered = filtered.filter(t => t.priority === priorityFilter)
  }
  if (stationFilter) {
    filtered = filtered.filter(t =>
      (t.stationId || '').toLowerCase().includes(stationFilter.toLowerCase()) ||
      (t.stationName || '').toLowerCase().includes(stationFilter.toLowerCase())
    )
  }
  if (technicianFilter) {
    filtered = filtered.filter(t =>
      t.assignedTechnicianId === technicianFilter || t.assignedTechnicianName === technicianFilter
    )
  }
  if (searchQuery) {
    filtered = filtered.filter(t =>
      (t.ticketNumber || '').toLowerCase().includes(searchQuery) ||
      (t.title || '').toLowerCase().includes(searchQuery) ||
      (t.description || '').toLowerCase().includes(searchQuery) ||
      (t.stationName || '').toLowerCase().includes(searchQuery) ||
      (t.assignedTechnicianName || '').toLowerCase().includes(searchQuery)
    )
  }

  filtered.sort((a, b) => {
    let valA: any = a.createdAt
    let valB: any = b.createdAt

    if (sortBy === 'priority') {
      valA = PRIORITY_WEIGHT[a.priority] || 0
      valB = PRIORITY_WEIGHT[b.priority] || 0
    } else if (sortBy === 'sla_due_at') {
      valA = new Date(a.slaDueAt || 0).getTime()
      valB = new Date(b.slaDueAt || 0).getTime()
    } else if (sortBy === 'title') {
      valA = (a.title || '').toLowerCase()
      valB = (b.title || '').toLowerCase()
    } else if (sortBy === 'status') {
      valA = a.status
      valB = b.status
    } else if (sortBy === 'created_at') {
      valA = new Date(a.createdAt).getTime()
      valB = new Date(b.createdAt).getTime()
    }

    if (valA < valB) return sortOrder === 'asc' ? -1 : 1
    if (valA > valB) return sortOrder === 'asc' ? 1 : -1
    return 0
  })

  return {
    tickets: filtered,
    metrics: {
      totalTickets: allTickets.length,
      filteredCount: filtered.length,
      openCount: allTickets.filter(t => t.status === 'Open').length,
      inProgressCount: allTickets.filter(t => t.status === 'InProgress').length,
      pendingCount: allTickets.filter(t => t.status === 'Pending').length,
      resolvedCount: allTickets.filter(t => t.status === 'Resolved').length,
      closedCount: allTickets.filter(t => t.status === 'Closed').length,
      escalatedCount: allTickets.filter(t => t.isEscalated).length,
      criticalCount: allTickets.filter(
        t => t.priority === 'Critical' && t.status !== 'Closed' && t.status !== 'Resolved'
      ).length,
      overdueCount,
      slaCompliancePercent
    }
  }
})
