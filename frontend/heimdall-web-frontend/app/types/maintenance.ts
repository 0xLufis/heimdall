export type TicketStatus = 'Open' | 'In_Progress' | 'Pending_Parts' | 'Resolved' | 'Closed'
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'

export interface TicketComment {
  id: string
  ticketId: string
  authorUserId?: string
  authorName: string
  content: string
  createdAt: string
}

export interface TicketAttachment {
  id: string
  ticketId: string
  fileName: string
  contentType: string
  fileSize: number
  uploadedAt: string
  url?: string
}

export interface MaintenanceTicket {
  id: string
  ticketNumber: string
  stationId?: string
  stationName?: string
  controllerId?: string
  controllerName?: string
  title: string
  description?: string
  status: TicketStatus
  priority: TicketPriority
  reportedByUserId?: string
  reportedByUserName?: string
  assignedTechnicianId?: string
  assignedTechnicianName?: string
  createdAt: string
  updatedAt: string
  slaDueAt?: string
  resolvedAt?: string
  comments: TicketComment[]
  attachments: TicketAttachment[]
  metadata?: Record<string, any>
  isOfflinePending?: boolean
}

export interface CreateTicketInput {
  title: string
  description?: string
  stationId?: string
  stationName?: string
  controllerId?: string
  controllerName?: string
  priority: TicketPriority
  reportedByUserName?: string
  assignedTechnicianName?: string
  metadata?: Record<string, any>
}

export interface TicketFilter {
  status?: TicketStatus | 'all'
  priority?: TicketPriority | 'all'
  query?: string
  stationId?: string
  controllerId?: string
  assignedTo?: string
  sortBy?: string
  sortOrder?: 'asc' | 'desc'
  page?: number
  pageSize?: number
}

export interface TicketMetrics {
  totalTickets: number
  openCount: number
  inProgressCount: number
  pendingPartsCount: number
  resolvedCount: number
  closedCount: number
  criticalCount: number
  overdueCount: number
  slaCompliancePercent: number
}

export interface MaintenanceEvent {
  type: 'TicketCreated' | 'TicketUpdated' | 'StatusChanged' | 'NewComment' | 'CriticalAlert'
  ticketId?: string
  ticket?: MaintenanceTicket
  status?: TicketStatus
  comment?: TicketComment
  stationName?: string
  message?: string
  timestamp: string
}

export interface IMaintenanceService {
  getTickets(filter?: TicketFilter): Promise<MaintenanceTicket[]>
  getTicketById(id: string): Promise<MaintenanceTicket | null>
  createTicket(ticket: CreateTicketInput): Promise<MaintenanceTicket>
  updateTicket(id: string, updates: Partial<MaintenanceTicket>): Promise<MaintenanceTicket>
  updateTicketStatus(id: string, status: TicketStatus, technicianName?: string): Promise<MaintenanceTicket>
  addComment(ticketId: string, authorName: string, content: string): Promise<TicketComment>
  uploadAttachment?(ticketId: string, file: File): Promise<TicketAttachment>
  getMetrics(): Promise<TicketMetrics>
  subscribeToEvents(listener: (event: MaintenanceEvent) => void): () => void
}
