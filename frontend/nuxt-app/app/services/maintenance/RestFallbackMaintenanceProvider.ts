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

    const queryString = params.toString() ? `?${params.toString()}` : ''
    const rawList = await $fetch<any[]>(`/api/proxy/MaintenanceTicket${queryString}`)
    return (rawList || []).map(r => ({
      id: r.id || r.Id,
      ticketNumber: r.ticketNumber || `TKT-${(r.id || '').substring(0, 8)}`,
      stationId: r.machineId || r.stationId,
      stationName: r.machine?.name || r.stationName || 'Production Station',
      controllerId: r.clientPcId || r.controllerId,
      controllerName: r.clientPc?.hostname || r.controllerName,
      title: r.title || r.Title || '',
      description: r.description || r.Description || '',
      status: (r.status || r.Status || 'Open') as TicketStatus,
      priority: (r.priority || r.Priority || 'Medium') as any,
      reportedByUserName: r.createdBy || r.reportedByUserName || 'Operator',
      assignedTechnicianName: r.assignedTo || r.assignedTechnicianName || 'Unassigned',
      createdAt: r.createdAt || r.CreatedAt || new Date().toISOString(),
      updatedAt: r.updatedAt || r.UpdatedAt || new Date().toISOString(),
      comments: r.comments || [],
      attachments: r.attachments || []
    }))
  }

  public async getTicketById(id: string): Promise<MaintenanceTicket | null> {
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
      machineId: input.stationId,
      clientPcId: input.controllerId,
      priority: input.priority,
      status: 'Open',
      createdBy: input.reportedByUserName || 'Operator',
      assignedTo: input.assignedTechnicianName
    }

    const r = await $fetch<any>('/api/proxy/MaintenanceTicket', {
      method: 'POST',
      body: payload
    })

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
    await $fetch(`/api/proxy/MaintenanceTicket/${id}`, {
      method: 'PUT',
      body: { id, ...updates }
    })
    return (await this.getTicketById(id))!
  }

  public async updateTicketStatus(id: string, status: TicketStatus, technicianName?: string): Promise<MaintenanceTicket> {
    await $fetch(`/api/proxy/MaintenanceTicket/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify(status)
    })
    return (await this.getTicketById(id)) || ({ id, status } as any)
  }

  public async addComment(ticketId: string, authorName: string, content: string): Promise<TicketComment> {
    return {
      id: `c-${Date.now()}`,
      ticketId,
      authorName,
      content,
      createdAt: new Date().toISOString()
    }
  }

  public async getMetrics(): Promise<TicketMetrics> {
    const tickets = await this.getTickets()
    return {
      totalTickets: tickets.length,
      openCount: tickets.filter(t => t.status === 'Open').length,
      inProgressCount: tickets.filter(t => t.status === 'In_Progress').length,
      pendingPartsCount: tickets.filter(t => t.status === 'Pending_Parts').length,
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
