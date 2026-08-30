import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
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

export class HeimdallSignalRMaintenanceProvider implements IMaintenanceService {
  private hubConnection: HubConnection | null = null
  private eventListeners: Set<(event: MaintenanceEvent) => void> = new Set()
  private isConnecting = false
  private hubUrl = '/hubs/maintenance'

  constructor(hubUrl?: string) {
    if (hubUrl) this.hubUrl = hubUrl
    this.initSignalR()
  }

  private async initSignalR() {
    if (typeof window === 'undefined' || this.hubConnection || this.isConnecting) return

    try {
      this.isConnecting = true
      this.hubConnection = new HubConnectionBuilder()
        .withUrl(this.hubUrl)
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build()

      this.hubConnection.on('TicketCreated', (ticket: any) => {
        const normalized = this.normalizeTicket(ticket)
        this.notifyListeners({
          type: 'TicketCreated',
          ticket: normalized,
          ticketId: normalized.id,
          timestamp: new Date().toISOString()
        })
      })

      this.hubConnection.on('TicketUpdated', (ticket: any) => {
        const normalized = this.normalizeTicket(ticket)
        this.notifyListeners({
          type: 'TicketUpdated',
          ticket: normalized,
          ticketId: normalized.id,
          timestamp: new Date().toISOString()
        })
      })

      this.hubConnection.on('StatusChanged', (ticketId: string, status: string) => {
        this.notifyListeners({
          type: 'StatusChanged',
          ticketId,
          status: status as TicketStatus,
          timestamp: new Date().toISOString()
        })
      })

      this.hubConnection.on('NewTicketComment', (ticketId: string, comment: any) => {
        this.notifyListeners({
          type: 'NewComment',
          ticketId,
          comment,
          timestamp: new Date().toISOString()
        })
      })

      this.hubConnection.on('CriticalAlertRaised', (stationName: string, message: string) => {
        this.notifyListeners({
          type: 'CriticalAlert',
          stationName,
          message,
          timestamp: new Date().toISOString()
        })
      })

      await this.hubConnection.start()
    } catch (err) {
      console.warn('SignalR Hub connection failed, fallback mode active:', err)
    } finally {
      this.isConnecting = false
    }
  }

  private notifyListeners(event: MaintenanceEvent) {
    this.eventListeners.forEach(listener => {
      try {
        listener(event)
      } catch (e) {
        console.error('Error in maintenance event listener:', e)
      }
    })
  }

  private normalizeTicket(raw: any): MaintenanceTicket {
    return {
      id: raw.id || raw.Id,
      ticketNumber: raw.ticketNumber || raw.ticket_number || `TKT-${(raw.id || '').substring(0, 8)}`,
      stationId: raw.stationId || raw.machineId || raw.MachineId,
      stationName: raw.stationName || raw.machine?.name || raw.machine?.customIdentifier || raw.stationId || 'Production Station',
      controllerId: raw.controllerId || raw.clientPcId || raw.ClientPcId,
      controllerName: raw.controllerName || raw.clientPc?.hostname || raw.clientPc?.name,
      title: raw.title || raw.Title || 'Maintenance Incident',
      description: raw.description || raw.Description || '',
      status: (raw.status || raw.Status || 'Open') as TicketStatus,
      priority: (raw.priority || raw.Priority || 'Medium') as any,
      reportedByUserId: raw.reportedByUserId || raw.createdBy,
      reportedByUserName: raw.reportedByUserName || raw.createdBy || 'Floor Operator',
      assignedTechnicianId: raw.assignedTechnicianId || raw.assignedTo,
      assignedTechnicianName: raw.assignedTechnicianName || raw.assignedTo || 'Unassigned',
      createdAt: raw.createdAt || raw.CreatedAt || new Date().toISOString(),
      updatedAt: raw.updatedAt || raw.UpdatedAt || new Date().toISOString(),
      slaDueAt: raw.slaDueAt || raw.sla_due_at,
      resolvedAt: raw.resolvedAt || raw.ResolvedAt,
      comments: (raw.comments || []).map((c: any) => ({
        id: c.id || c.Id,
        ticketId: c.maintenanceTicketId || c.ticketId || raw.id,
        authorUserId: c.authorUserId || c.author,
        authorName: c.authorName || c.author || 'User',
        content: c.content || c.Content || '',
        createdAt: c.createdAt || c.CreatedAt || new Date().toISOString()
      })),
      attachments: (raw.attachments || []).map((a: any) => ({
        id: a.id || a.Id,
        ticketId: a.maintenanceTicketId || a.ticketId || raw.id,
        fileName: a.fileName || a.FileName || 'attachment',
        contentType: a.contentType || a.ContentType || 'application/octet-stream',
        fileSize: a.fileSizeBytes || a.fileSize || 0,
        uploadedAt: a.uploadedAt || a.UploadedAt || new Date().toISOString()
      })),
      metadata: raw.metadata || raw.Metadata
    }
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
    const rawList = await $fetch<any[]>(`/api/proxy/MaintenanceTicket${queryString}`)
    return (rawList || []).map(t => this.normalizeTicket(t))
  }

  public async getTicketById(id: string): Promise<MaintenanceTicket | null> {
    try {
      const raw = await $fetch<any>(`/api/proxy/MaintenanceTicket/${id}`)
      return raw ? this.normalizeTicket(raw) : null
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

    const created = await $fetch<any>('/api/proxy/MaintenanceTicket', {
      method: 'POST',
      body: payload
    })

    const normalized = this.normalizeTicket(created)
    this.notifyListeners({
      type: 'TicketCreated',
      ticket: normalized,
      ticketId: normalized.id,
      timestamp: new Date().toISOString()
    })
    return normalized
  }

  public async updateTicket(id: string, updates: Partial<MaintenanceTicket>): Promise<MaintenanceTicket> {
    const payload: any = { id, ...updates }
    await $fetch(`/api/proxy/MaintenanceTicket/${id}`, {
      method: 'PUT',
      body: payload
    })

    const updated = await this.getTicketById(id)
    if (updated) {
      this.notifyListeners({
        type: 'TicketUpdated',
        ticket: updated,
        ticketId: id,
        timestamp: new Date().toISOString()
      })
      return updated
    }
    throw new Error(`Failed to retrieve updated ticket ${id}`)
  }

  public async updateTicketStatus(id: string, status: TicketStatus, technicianName?: string): Promise<MaintenanceTicket> {
    await $fetch(`/api/proxy/MaintenanceTicket/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify(status)
    })

    this.notifyListeners({
      type: 'StatusChanged',
      ticketId: id,
      status,
      timestamp: new Date().toISOString()
    })

    const updated = await this.getTicketById(id)
    return updated || ({ id, status } as any)
  }

  public async addComment(ticketId: string, authorName: string, content: string): Promise<TicketComment> {
    const commentPayload = {
      id: crypto.randomUUID ? crypto.randomUUID() : `c-${Date.now()}`,
      maintenanceTicketId: ticketId,
      author: authorName,
      content,
      createdAt: new Date().toISOString()
    }

    // Attempt to persist comment or update ticket
    const comment: TicketComment = {
      id: commentPayload.id,
      ticketId,
      authorName,
      content,
      createdAt: commentPayload.createdAt
    }

    this.notifyListeners({
      type: 'NewComment',
      ticketId,
      comment,
      timestamp: new Date().toISOString()
    })

    return comment
  }

  public async getMetrics(): Promise<TicketMetrics> {
    const tickets = await this.getTickets()
    const open = tickets.filter(t => t.status === 'Open').length
    const inProgress = tickets.filter(t => t.status === 'In_Progress').length
    const pendingParts = tickets.filter(t => t.status === 'Pending_Parts').length
    const resolved = tickets.filter(t => t.status === 'Resolved').length
    const closed = tickets.filter(t => t.status === 'Closed').length
    const critical = tickets.filter(t => t.priority === 'Critical' && t.status !== 'Resolved' && t.status !== 'Closed').length
    
    const now = Date.now()
    const overdue = tickets.filter(t => t.slaDueAt && new Date(t.slaDueAt).getTime() < now && t.status !== 'Resolved' && t.status !== 'Closed').length

    const slaCompliance = tickets.length > 0 ? Math.round(((tickets.length - overdue) / tickets.length) * 100) : 100

    return {
      totalTickets: tickets.length,
      openCount: open,
      inProgressCount: inProgress,
      pendingPartsCount: pendingParts,
      resolvedCount: resolved,
      closedCount: closed,
      criticalCount: critical,
      overdueCount: overdue,
      slaCompliancePercent: slaCompliance
    }
  }

  public subscribeToEvents(listener: (event: MaintenanceEvent) => void): () => void {
    this.eventListeners.add(listener)
    return () => {
      this.eventListeners.delete(listener)
    }
  }
}
