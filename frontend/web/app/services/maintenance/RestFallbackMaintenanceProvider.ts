import type {
  IMaintenanceService,
  MaintenanceTicket,
  CreateTicketInput,
  TicketFilter,
  TicketMetrics,
  MaintenanceEvent,
  TicketStatus,
  TicketComment
} from '~/types/maintenance'

export class RestFallbackMaintenanceProvider implements IMaintenanceService {
  private eventListeners: Set<(event: MaintenanceEvent) => void> = new Set()
  private pollingTimer: any = null
  private lastTicketCount = 0

  constructor(private pollIntervalMs: number = 10000) {
    if (typeof window !== 'undefined') {
      this.startPolling()
    }
  }

  private startPolling() {
    if (this.pollingTimer) clearInterval(this.pollingTimer)
    this.pollingTimer = setInterval(async () => {
      try {
        const tickets = await this.getTickets()
        if (tickets.length !== this.lastTicketCount) {
          this.lastTicketCount = tickets.length
          this.notifyListeners({
            type: 'TicketUpdated',
            timestamp: new Date().toISOString()
          })
        }
      } catch {
        // Silent poll error handling
      }
    }, this.pollIntervalMs)
  }

  private notifyListeners(event: MaintenanceEvent) {
    this.eventListeners.forEach(listener => {
      try {
        listener(event)
      } catch (e) {
        console.error('Error in REST event listener:', e)
      }
    })
  }

  public async getTickets(filter?: TicketFilter): Promise<MaintenanceTicket[]> {
    const params = new URLSearchParams()
    if (filter?.status && filter.status !== 'all') params.set('status', filter.status)
    if (filter?.priority && filter.priority !== 'all') params.set('priority', filter.priority)
    if (filter?.query) params.set('query', filter.query)
    if (filter?.stationId) params.set('stationId', filter.stationId)
    if (filter?.controllerId) params.set('controllerId', filter.controllerId)
    if (filter?.sortBy) params.set('sortBy', filter.sortBy)

    const queryString = params.toString() ? `?${params.toString()}` : ''
    let rawList: any[] = []

    try {
      const res = await $fetch<any>(`/api/tickets${queryString}`)
      if (res) {
        if (Array.isArray(res)) rawList = res
        else if (Array.isArray(res.tickets)) rawList = res.tickets
      }
    } catch {}

    if (!rawList || rawList.length === 0) {
      try {
        const proxyRes = await $fetch<any[]>(`/api/proxy/MaintenanceTicket${queryString}`)
        if (proxyRes && Array.isArray(proxyRes) && proxyRes.length > 0) {
          rawList = proxyRes
        }
      } catch {}
    }

    return (rawList || []).map(r => ({
      id: r.id || r.Id,
      ticketNumber: r.ticketNumber || `TKT-${(r.id || '').substring(0, 8)}`,
      stationId: r.stationId || r.machineId,
      stationName: r.stationName || r.machine?.name || 'Production Station',
      machineType: r.machineType || r.MachineType,
      groupId: r.groupId || r.GroupId,
      controllerId: r.controllerId || r.clientPcId,
      controllerName: r.controllerName || r.clientPc?.hostname,
      title: r.title || r.Title || '',
      description: r.description || r.Description || '',
      status: (r.status || r.Status || 'Open') as TicketStatus,
      priority: (r.priority || r.Priority || 'Medium') as any,
      category: r.category || r.Category,
      errorGroup: r.errorGroup || r.ErrorGroup,
      errorCode: r.errorCode || r.ErrorCode,
      tags: Array.isArray(r.tags) ? r.tags : [],
      sfc: r.sfc || r.Sfc,
      pendingReason: r.pendingReason || r.PendingReason || 'None',
      pendingDetails: r.pendingDetails || r.PendingDetails || null,
      isEscalated: Boolean(r.isEscalated ?? r.IsEscalated ?? false),
      escalationReason: r.escalationReason || r.EscalationReason || null,
      escalatedAt: r.escalatedAt || r.EscalatedAt || null,
      escalatedBy: r.escalatedBy || r.EscalatedBy || null,
      escalationClosedAt: r.escalationClosedAt || r.EscalationClosedAt || null,
      escalationClosedBy: r.escalationClosedBy || r.EscalationClosedBy || null,
      reportedByUserId: r.reportedByUserId || r.createdBy,
      reportedByUserName: r.reportedByUserName || r.createdBy || 'Operator',
      assignedTechnicianId: r.assignedTechnicianId || r.assignedTo,
      assignedTechnicianName: r.assignedTechnicianName || r.assignedTo || 'Unassigned',
      createdAt: r.createdAt || r.CreatedAt || new Date().toISOString(),
      updatedAt: r.updatedAt || r.UpdatedAt || new Date().toISOString(),
      slaDueAt: r.slaDueAt || r.sla_due_at,
      resolvedAt: r.resolvedAt || r.ResolvedAt,
      comments: r.comments || [],
      attachments: r.attachments || []
    }))
  }

  public async getTicketById(id: string): Promise<MaintenanceTicket | null> {
    try {
      const res = await $fetch<any>(`/api/tickets/${id}`)
      const r = res?.ticket || res
      if (r && (r.id || r.ticketNumber)) {
        return {
          id: r.id || r.Id,
          ticketNumber: r.ticketNumber || `TKT-${(r.id || '').substring(0, 8)}`,
          stationId: r.stationId || r.machineId,
          stationName: r.stationName || r.machine?.name,
          machineType: r.machineType || r.MachineType,
          groupId: r.groupId || r.GroupId,
          controllerId: r.controllerId || r.clientPcId,
          controllerName: r.controllerName || r.clientPc?.hostname,
          title: r.title,
          description: r.description,
          status: (r.status || 'Open') as TicketStatus,
          priority: (r.priority || 'Medium') as any,
          category: r.category || r.Category,
          errorGroup: r.errorGroup || r.ErrorGroup,
          errorCode: r.errorCode || r.ErrorCode,
          tags: Array.isArray(r.tags) ? r.tags : [],
          sfc: r.sfc || r.Sfc,
          pendingReason: r.pendingReason || r.PendingReason || 'None',
          pendingDetails: r.pendingDetails || r.PendingDetails || null,
          isEscalated: Boolean(r.isEscalated ?? r.IsEscalated ?? false),
          escalationReason: r.escalationReason || r.EscalationReason || null,
          escalatedAt: r.escalatedAt || r.EscalatedAt || null,
          escalatedBy: r.escalatedBy || r.EscalatedBy || null,
          escalationClosedAt: r.escalationClosedAt || r.EscalationClosedAt || null,
          escalationClosedBy: r.escalationClosedBy || r.EscalationClosedBy || null,
          reportedByUserId: r.reportedByUserId || r.createdBy,
          reportedByUserName: r.reportedByUserName || r.createdBy,
          assignedTechnicianId: r.assignedTechnicianId || r.assignedTo,
          assignedTechnicianName: r.assignedTechnicianName || r.assignedTo,
          createdAt: r.createdAt || new Date().toISOString(),
          updatedAt: r.updatedAt || new Date().toISOString(),
          slaDueAt: r.slaDueAt,
          resolvedAt: r.resolvedAt,
          comments: r.comments || [],
          attachments: r.attachments || []
        }
      }
    } catch {}

    try {
      const r = await $fetch<any>(`/api/proxy/MaintenanceTicket/${id}`)
      if (!r) return null
      return {
        id: r.id || r.Id,
        ticketNumber: r.ticketNumber || `TKT-${(r.id || '').substring(0, 8)}`,
        stationId: r.machineId,
        stationName: r.machine?.name,
        controllerId: r.clientPcId,
        controllerName: r.clientPc?.hostname,
        title: r.title,
        description: r.description,
        status: (r.status || 'Open') as TicketStatus,
        priority: (r.priority || 'Medium') as any,
        pendingReason: r.pendingReason || 'None',
        pendingDetails: r.pendingDetails || null,
        isEscalated: Boolean(r.isEscalated || false),
        escalationReason: r.escalationReason || null,
        reportedByUserName: r.createdBy,
        assignedTechnicianName: r.assignedTo,
        createdAt: r.createdAt || new Date().toISOString(),
        updatedAt: r.updatedAt || new Date().toISOString(),
        comments: r.comments || [],
        attachments: r.attachments || []
      }
    } catch {
      return null
    }
  }

  public async createTicket(input: CreateTicketInput): Promise<MaintenanceTicket> {
    const payload = {
      title: input.title,
      description: input.description,
      stationId: input.stationId,
      stationName: input.stationName,
      controllerId: input.controllerId,
      priority: input.priority,
      status: 'Open',
      reportedByUserName: input.reportedByUserName || 'Operator',
      assignedTechnicianName: input.assignedTechnicianName
    }

    let r: any = null
    try {
      const res = await $fetch<any>('/api/tickets', {
        method: 'POST',
        body: payload
      })
      if (res?.ticket || res?.id) r = res?.ticket || res
    } catch {}

    if (!r) {
      r = await $fetch<any>('/api/proxy/MaintenanceTicket', {
        method: 'POST',
        body: {
          ...payload,
          machineId: input.stationId,
          clientPcId: input.controllerId,
          createdBy: payload.reportedByUserName,
          assignedTo: payload.assignedTechnicianName
        }
      })
    }

    return {
      id: r.id,
      ticketNumber: r.ticketNumber || `TKT-${r.id.substring(0, 8)}`,
      title: r.title,
      description: r.description,
      status: 'Open',
      priority: r.priority,
      createdAt: r.createdAt || new Date().toISOString(),
      updatedAt: r.updatedAt || new Date().toISOString(),
      comments: [],
      attachments: []
    }
  }

  public async updateTicket(id: string, updates: Partial<MaintenanceTicket>): Promise<MaintenanceTicket> {
    try {
      await $fetch(`/api/tickets/${id}`, {
        method: 'PATCH',
        body: updates
      })
    } catch {
      await $fetch(`/api/proxy/MaintenanceTicket/${id}`, {
        method: 'PUT',
        body: { id, ...updates }
      }).catch(() => {})
    }
    return (await this.getTicketById(id))!
  }

  public async updateTicketStatus(id: string, status: TicketStatus, technicianName?: string, extra?: Record<string, any>): Promise<MaintenanceTicket> {
    const body: any = { status, ...extra }
    if (technicianName) body.assignedTechnicianName = technicianName

    try {
      await $fetch(`/api/tickets/${id}`, {
        method: 'PATCH',
        body
      })
    } catch {
      await $fetch(`/api/proxy/MaintenanceTicket/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify(status)
      }).catch(() => {})
    }
    return (await this.getTicketById(id)) || ({ id, status, assignedTechnicianName: technicianName, ...extra } as any)
  }

  public async setPending(id: string, reason: any, details?: string): Promise<MaintenanceTicket> {
    return this.updateTicketStatus(id, 'Pending', undefined, { pendingReason: reason, pendingDetails: details })
  }

  public async escalate(
    id: string,
    reason: string,
    escalatedBy: string,
    handoverState: any = 'Notification',
    target?: string
  ): Promise<MaintenanceTicket> {
    const payload = {
      isEscalated: true,
      escalationReason: reason,
      escalatedBy,
      escalationHandoverState: handoverState,
      escalationTarget: target || 'DedicatedEngineer',
      escalatedAt: new Date().toISOString()
    }
    return this.updateTicket(id, payload)
  }

  public async resolveEscalation(id: string, resolvedBy: string): Promise<MaintenanceTicket> {
    const payload = {
      isEscalated: false,
      escalationReason: null,
      escalationClosedBy: resolvedBy,
      escalationClosedAt: new Date().toISOString()
    }
    return this.updateTicket(id, payload)
  }

  public async reserveTicket(id: string, technicianName: string): Promise<MaintenanceTicket> {
    try {
      const res = await $fetch<any>(`/api/tickets/${id}/reserve`, {
        method: 'POST',
        body: { technician: technicianName }
      })
      if (res?.ticket) return res.ticket
    } catch {}
    return this.updateTicket(id, {
      reservedBy: technicianName,
      assignedTechnicianName: technicianName
    })
  }

  public async qrPickup(id: string, technicianName?: string): Promise<MaintenanceTicket> {
    try {
      const res = await $fetch<any>(`/api/tickets/${id}/qr-pickup`, {
        method: 'POST',
        body: { technician: technicianName }
      })
      if (res?.ticket) return res.ticket
    } catch {}
    const now = new Date()
    return this.updateTicket(id, {
      status: 'InProgress',
      startedAt: now.toISOString(),
      qrScannedAt: now.toISOString(),
      assignedTechnicianName: technicianName,
      reservedBy: technicianName
    })
  }

  public async editWithHistory(
    id: string,
    updates: Partial<MaintenanceTicket>,
    editorName: string,
    reason?: string
  ): Promise<MaintenanceTicket> {
    try {
      const res = await $fetch<any>(`/api/tickets/${id}`, {
        method: 'PATCH',
        body: { ...updates, editorName, changeReason: reason }
      })
      if (res?.ticket) return res.ticket
    } catch {}
    return this.updateTicket(id, updates)
  }

  public async getStoppageStats(): Promise<any> {
    return await $fetch<any>('/api/tickets/stoppage-stats')
  }

  public async addComment(ticketId: string, authorName: string, content: string): Promise<TicketComment> {
    try {
      const res = await $fetch<any>(`/api/tickets/${ticketId}/comments`, {
        method: 'POST',
        body: { authorName, content }
      })
      if (res?.comment) return res.comment
    } catch {}

    return {
      id: `c-${Date.now()}`,
      ticketId,
      authorName,
      content,
      createdAt: new Date().toISOString()
    }
  }

  public async getMetrics(): Promise<TicketMetrics> {
    try {
      const res = await $fetch<any>('/api/tickets')
      if (res?.metrics && typeof res.metrics.totalTickets === 'number') {
        return res.metrics
      }
    } catch {}

    const tickets = await this.getTickets()
    return {
      totalTickets: tickets.length,
      openCount: tickets.filter(t => t.status === 'Open').length,
      inProgressCount: tickets.filter(t => t.status === 'InProgress').length,
      pendingCount: tickets.filter(t => t.status === 'Pending').length,
      resolvedCount: tickets.filter(t => t.status === 'Resolved').length,
      closedCount: tickets.filter(t => t.status === 'Closed').length,
      criticalCount: tickets.filter(t => t.priority === 'Critical').length,
      overdueCount: 0,
      slaCompliancePercent: 100
    }
  }

  public subscribeToEvents(listener: (event: MaintenanceEvent) => void): () => void {
    this.eventListeners.add(listener)
    return () => {
      this.eventListeners.delete(listener)
    }
  }
}
