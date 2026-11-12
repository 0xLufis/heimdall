import type { MaintenanceTicket } from './ticketsStore'

/** How many of its station's tickets a kiosk lists. */
export const TERMINAL_TICKET_LIMIT = 5
/** How many of a ticket's latest comments the kiosk's read-only ticket view shows. */
export const TERMINAL_TICKET_COMMENT_LIMIT = 3

export interface TerminalTicketComment {
  authorName: string
  content: string
  createdAt: string
}

/**
 * What a station kiosk may show of a ticket. The kiosk is a workstation, not a user, and only reads:
 * no user ids, attachments or escalation internals leave the BFF.
 */
export interface TerminalTicketView {
  id: string
  ticketNumber: string
  title: string
  status: string
  priority: string
  category?: string
  createdAt: string
  updatedAt?: string
  description: string
  isLineStop: boolean
  errorCode?: string
  reportedByUserName?: string
  assignedTechnicianName?: string
  /** When a technician checked in at the station by scanning the ticket's QR code (QR pickup). */
  qrScannedAt?: string
  /** Only while the ticket is Pending. */
  pendingReason?: string
  /** Newest first, at most TERMINAL_TICKET_COMMENT_LIMIT. */
  comments: TerminalTicketComment[]
}

export function toTerminalTicketView(ticket: MaintenanceTicket): TerminalTicketView {
  const comments = [...(ticket.comments ?? [])]
    .filter(c => typeof c.content === 'string' && c.content.trim().length > 0)
    .sort((a, b) => (Date.parse(b.createdAt) || 0) - (Date.parse(a.createdAt) || 0))
    .slice(0, TERMINAL_TICKET_COMMENT_LIMIT)
    .map(c => ({ authorName: c.authorName, content: c.content, createdAt: c.createdAt }))

  return {
    id: ticket.id,
    ticketNumber: ticket.ticketNumber,
    title: ticket.title,
    status: ticket.status,
    priority: ticket.priority,
    category: ticket.category,
    createdAt: ticket.createdAt,
    updatedAt: ticket.updatedAt,
    description: ticket.description ?? '',
    isLineStop: ticket.isLineStop === true,
    errorCode: ticket.errorCode || undefined,
    reportedByUserName: ticket.reportedByUserName || undefined,
    assignedTechnicianName: ticket.assignedTechnicianName || undefined,
    qrScannedAt: ticket.qrScannedAt || undefined,
    pendingReason: ticket.status === 'Pending' && ticket.pendingReason && ticket.pendingReason !== 'None'
      ? ticket.pendingReason
      : undefined,
    comments
  }
}

/**
 * The tickets a kiosk lists for its station, in store order. A ticket belongs to the station by id,
 * custom identifier or name; empty values never match, so a ticket without a station is not shown on
 * a kiosk whose machine lacks a custom identifier.
 */
export function terminalTicketsForStation(
  tickets: MaintenanceTicket[],
  machine: { id?: string; name?: string; customIdentifier?: string }
): TerminalTicketView[] {
  const stationKeys = [machine.id, machine.customIdentifier].filter((k): k is string => !!k)
  return tickets
    .filter(t =>
      (!!t.stationId && stationKeys.includes(t.stationId)) ||
      (!!machine.name && t.stationName === machine.name))
    .slice(0, TERMINAL_TICKET_LIMIT)
    .map(toTerminalTicketView)
}
