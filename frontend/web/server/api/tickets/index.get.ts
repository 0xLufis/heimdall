import { defineEventHandler, getQuery } from 'h3'
import { getTicketsStore, getStoppageStatsFromStore } from '../../utils/ticketsStore'

const BACKEND_BASE = process.env.BACKEND_API_URL || 'http://localhost:5001'

const PRIORITY_WEIGHT: Record<string, number> = {
  Critical: 4,
  High: 3,
  Medium: 2,
  Low: 1
}

const LIFECYCLE_ORDER: Record<string, number> = {
  Open: 1,
  InProgress: 2,
  Pending: 3,
  Resolved: 4,
  Closed: 5
}

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const statusFilter = (query.status as string || 'all').trim()
  const priorityFilter = (query.priority as string || 'all').trim()
  const searchQuery = (query.query as string || '').toLowerCase().trim()
  const stationFilter = (query.stationId as string || '').trim()
  const technicianFilter = (query.technicianId as string || '').trim()
  const departmentFilter = (query.department as string || '').toLowerCase().trim()
  const sortBy = (query.sortBy as string || 'lifecycle').trim()
  const sortOrder = (query.sortOrder as string || 'asc').trim()

  let rawList: any[] = []
  try {
    const res = await $fetch<any[]>(`${BACKEND_BASE}/api/v1/tickets`, {
      headers: event.headers as any
    })
    if (res && Array.isArray(res) && res.length > 0) {
      rawList = res
    }
  } catch (err: any) {
    // Fallback to local ticketsStore
  }

  let allTickets: any[] = []
  if (rawList.length > 0) {
    allTickets = rawList.map((t: any) => ({
      id: t.id || t.Id,
      ticketNumber: t.ticketNumber || `TKT-${(t.id || '').substring(0, 8)}`,
      stationId: t.machineId || t.stationId,
      stationName: t.machine?.name || t.machine?.customIdentifier || t.stationName || '',
      machineType: t.machineType || t.MachineType,
      groupId: t.groupId || t.GroupId,
      controllerId: t.clientPcId || t.controllerId,
      controllerName: t.clientPc?.hostname || t.controllerName,
      title: t.title || t.Title || '',
      description: t.description || t.Description || '',
      status: t.status || t.Status || 'Open',
      priority: t.priority || t.Priority || 'Medium',
      category: t.category || t.Category,
      issueType: t.issueType || t.IssueType || 'Maintenance',
      originatorType: t.originatorType || t.OriginatorType || 'ManualUser',
      isLineStop: Boolean(t.isLineStop ?? t.IsLineStop ?? false),
      lineStopDurationMinutes: t.lineStopDurationMinutes ?? t.LineStopDurationMinutes ?? (Boolean(t.isLineStop ?? t.IsLineStop) ? 0 : null),
      responsibleDepartment: t.responsibleDepartment || t.ResponsibleDepartment || 'Assy',
      externalOperatorId: t.externalOperatorId || t.ExternalOperatorId || null,
      externalOperatorName: t.externalOperatorName || t.ExternalOperatorName || null,
      startedAt: t.startedAt || t.StartedAt || null,
      qrScannedAt: t.qrScannedAt || t.QrScannedAt || null,
      reactionTimeMinutes: t.reactionTimeMinutes ?? t.ReactionTimeMinutes ?? null,
      reservedBy: t.reservedBy || t.ReservedBy || null,
      errorGroup: t.errorGroup || t.ErrorGroup,
      errorCode: t.errorCode || t.ErrorCode,
      tags: t.tags || [],
      sfc: t.sfc || t.Sfc,
      pendingReason: t.pendingReason || t.PendingReason || 'None',
      pendingDetails: t.pendingDetails || t.PendingDetails || null,
      isEscalated: Boolean(t.isEscalated ?? t.IsEscalated ?? false),
      escalationReason: t.escalationReason || t.EscalationReason || null,
      escalationTarget: t.escalationTarget || t.EscalationTarget || null,
      escalationHandoverState: t.escalationHandoverState || t.EscalationHandoverState || null,
      escalatedAt: t.escalatedAt || t.EscalatedAt || null,
      escalatedBy: t.escalatedBy || t.EscalatedBy || null,
      telemetrySnapshot: t.telemetrySnapshot || t.TelemetrySnapshot || null,
      reportedByUserId: t.reportedByUserId || t.createdBy || '',
      reportedByUserName: t.reportedByUserName || t.createdBy || 'Operator',
      assignedTechnicianId: t.assignedTechnicianId || t.assignedTo || '',
      assignedTechnicianName: t.assignedTechnicianName || t.assignedTo || 'Unassigned',
      createdAt: t.createdAt || t.CreatedAt || new Date().toISOString(),
      updatedAt: t.updatedAt || t.UpdatedAt || new Date().toISOString(),
      slaDueAt: t.slaDueAt ?? null,
      resolvedAt: t.resolvedAt ?? null,
      comments: t.comments ?? [],
      attachments: t.attachments ?? [],
      changeHistory: t.changeHistory ?? []
    }))
  } else {
    allTickets = [...getTicketsStore()]
  }

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
  if (departmentFilter) {
    filtered = filtered.filter(t => (t.responsibleDepartment || '').toLowerCase() === departmentFilter)
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
      (t.responsibleDepartment || '').toLowerCase().includes(searchQuery) ||
      (t.assignedTechnicianName || '').toLowerCase().includes(searchQuery)
    )
  }

  // Default sorting follows ticket lifecycle (Open -> InProgress -> Pending -> Resolved -> Closed),
  // with tickets that are equal in primary sorting secondarily sorted by time opened (createdAt).
  filtered.sort((a, b) => {
    let primaryA = 0
    let primaryB = 0

    if (sortBy === 'lifecycle') {
      primaryA = LIFECYCLE_ORDER[a.status] || 99
      primaryB = LIFECYCLE_ORDER[b.status] || 99
    } else if (sortBy === 'priority') {
      primaryA = -(PRIORITY_WEIGHT[a.priority] || 0)
      primaryB = -(PRIORITY_WEIGHT[b.priority] || 0)
    } else if (sortBy === 'sla_due_at') {
      primaryA = new Date(a.slaDueAt || 0).getTime()
      primaryB = new Date(b.slaDueAt || 0).getTime()
    } else if (sortBy === 'title') {
      primaryA = (a.title || '').localeCompare(b.title || '')
      primaryB = 0
    } else if (sortBy === 'created_at') {
      primaryA = new Date(a.createdAt).getTime()
      primaryB = new Date(b.createdAt).getTime()
    }

    if (primaryA !== primaryB) {
      if (sortOrder === 'desc') {
        return primaryA > primaryB ? -1 : 1
      }
      return primaryA < primaryB ? -1 : 1
    }

    // Secondary sort: time opened (createdAt) descending
    const timeA = new Date(a.createdAt).getTime()
    const timeB = new Date(b.createdAt).getTime()
    return timeB - timeA
  })

  const stoppageStats = getStoppageStatsFromStore()

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
      lineStopCount: allTickets.filter(t => t.isLineStop && t.status !== 'Closed' && t.status !== 'Resolved').length,
      overdueCount,
      slaCompliancePercent,
      stoppageStats
    }
  }
})
