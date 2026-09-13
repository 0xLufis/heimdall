import { randomUUID } from 'node:crypto'
import { initialTickets } from './initialTickets'

export interface StateTransitionMeta {
  fromStatus: string
  toStatus: string
  reason?: string
  actor?: string
}

export interface TicketComment {
  id: string
  ticketId: string
  authorUserId: string
  authorName: string
  content: string
  createdAt: string
  transition?: StateTransitionMeta
  attachments?: TicketAttachment[]
}

export interface TicketAttachment {
  id: string
  ticketId: string
  commentId?: string
  fileName: string
  contentType: string
  fileSize: number
  uploadedAt: string
  url?: string
}

export type TicketStatus =
  | 'Open'
  | 'In_Progress'
  | 'Pending_Parts'
  | 'Escalated'
  | 'Escalated_External'
  | 'Closure_Pending'
  | 'Resolved'
  | 'Closed_Unresolved'
  | 'Closed'
  | 'Draft'

export interface FunctionBlockState {
  blockName: string
  state: string
  subState?: string
  errorCode?: string
}

export interface TelemetrySnapshot {
  timestamp: string
  metrics: Record<string, string | number>
}

export interface MaintenanceTicket {
  id: string
  ticketNumber: string
  stationId: string
  stationName: string
  machineType?: string
  controllerId?: string
  title: string
  description: string
  status: TicketStatus
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  category?: 'Prevention' | 'Error' | 'Improvement' | 'ETC'
  errorGroup?: string
  errorCode?: string
  tags?: string[]
  fbState?: FunctionBlockState
  sfc?: string
  telemetrySnapshot?: TelemetrySnapshot
  externalEscalationTarget?: string
  reportedByUserId: string
  reportedByUserName: string
  assignedTechnicianId?: string
  assignedTechnicianName?: string
  createdAt: string
  updatedAt: string
  slaDueAt: string
  resolvedAt?: string
  comments: TicketComment[]
  attachments: TicketAttachment[]
  metadata?: Record<string, any>
}

// ─── Store ────────────────────────────────────────────────────────────────────

let ticketsStore: MaintenanceTicket[] = [...initialTickets]

// ─── Settings ─────────────────────────────────────────────────────────────────

export interface TicketSettings {
  autoAssignTickets: boolean
  devTicketGenEnabled: boolean
}

let ticketSettings: TicketSettings = {
  autoAssignTickets: false,
  devTicketGenEnabled: true
}

export function getTicketSettings(): TicketSettings {
  return ticketSettings
}

export function updateTicketSettings(updates: Partial<TicketSettings>): TicketSettings {
  ticketSettings = { ...ticketSettings, ...updates }
  return ticketSettings
}

// ─── Store Functions ──────────────────────────────────────────────────────────

export function getTicketsStore(): MaintenanceTicket[] {
  return ticketsStore
}

export const getTickets = getTicketsStore

export function addTicketToStore(ticket: MaintenanceTicket): MaintenanceTicket {
  ticketsStore.unshift(ticket)
  return ticket
}

export function findTicketById(id: string): MaintenanceTicket | undefined {
  return ticketsStore.find(t => t.id === id || t.ticketNumber === id)
}

export function updateTicketInStore(
  id: string,
  updates: Partial<MaintenanceTicket>
): MaintenanceTicket | undefined {
  const ticket = findTicketById(id)
  if (!ticket) return undefined

  const oldStatus = ticket.status
  Object.assign(ticket, updates, { updatedAt: new Date().toISOString() })

  if (updates.status === 'Resolved' && !ticket.resolvedAt) {
    ticket.resolvedAt = new Date().toISOString()
  }

  // Auto-append system comment on status change
  if (updates.status && updates.status !== oldStatus) {
    const sysComment: TicketComment = {
      id: randomUUID(),
      ticketId: ticket.id,
      authorName: 'System',
      content: '',
      transition: {
        fromStatus: oldStatus,
        toStatus: updates.status,
        actor: 'System'
      },
      createdAt: new Date().toISOString(),
      attachments: []
    }
    ticket.comments.push(sysComment)
  }

  return ticket
}

/** Update ticket status with an explicit actor label, auto-appending a system comment. */
export function updateTicketStatus(
  id: string,
  newStatus: TicketStatus,
  actor: string = 'System'
): MaintenanceTicket | undefined {
  const ticket = findTicketById(id)
  if (!ticket) return undefined

  const oldStatus = ticket.status
  if (oldStatus === newStatus) return ticket

  ticket.status = newStatus
  ticket.updatedAt = new Date().toISOString()

  if (newStatus === 'Resolved' && !ticket.resolvedAt) {
    ticket.resolvedAt = new Date().toISOString()
  }

  const sysComment: TicketComment = {
    id: crypto.randomUUID(),
    ticketId: ticket.id,
    authorName: 'System',
    content: '',
    transition: { fromStatus: oldStatus, toStatus: newStatus, actor },
    createdAt: new Date().toISOString(),
    attachments: []
  }
  ticket.comments.push(sysComment)

  return ticket
}

export function addCommentToTicket(
  ticketId: string,
  comment: TicketComment
): TicketComment | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined
  ticket.comments.push(comment)
  ticket.updatedAt = new Date().toISOString()
  return comment
}

export function addAttachmentToTicket(
  ticketId: string,
  attachment: Omit<TicketAttachment, 'id' | 'ticketId' | 'uploadedAt'> & { id?: string },
  commentId?: string
): TicketAttachment | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined

  const fullAttachment: TicketAttachment = {
    id: attachment.id || (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `att-${Date.now()}`),
    ticketId,
    commentId,
    fileName: attachment.fileName,
    contentType: attachment.contentType,
    fileSize: attachment.fileSize,
    uploadedAt: new Date().toISOString(),
    url: attachment.url || ''
  }

  ticket.attachments.push(fullAttachment)

  if (commentId) {
    const comment = ticket.comments.find(c => c.id === commentId)
    if (comment) {
      if (!comment.attachments) comment.attachments = []
      comment.attachments.push(fullAttachment)
    }
  }

  ticket.updatedAt = new Date().toISOString()
  return fullAttachment
}
