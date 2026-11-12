/**
 * A station ticket as the kiosk receives it from GET /api/terminal/machine/:id (read-only projection,
 * see server/utils/terminalTickets.ts).
 */
export interface TerminalTicket {
  id: string
  ticketNumber: string
  title: string
  status: string
  priority: string
  category?: string
  createdAt: string
  updatedAt?: string
  description?: string
  isLineStop?: boolean
  errorCode?: string
  reportedByUserName?: string
  assignedTechnicianName?: string
  /** When a technician checked in at the station by scanning the ticket's QR code. */
  qrScannedAt?: string
  pendingReason?: string
  comments?: { authorName: string; content: string; createdAt: string }[]
}

/** The kiosk's station, as far as a QR code needs it. */
export interface TerminalStation {
  id?: string
  name?: string
  displayName?: string
  customIdentifier?: string
  machineType?: string
  groupId?: string
}

/** Short local date and time for kiosk labels; empty for a missing or invalid timestamp. */
export function formatTerminalTime(iso?: string | null): string {
  if (!iso) return ''
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit', hour12: false })
}

export function terminalPriorityClass(priority?: string): string {
  switch (priority) {
    case 'Critical': return 'bg-red-950 text-red-300 border-red-500/40'
    case 'High': return 'bg-orange-950 text-orange-300 border-orange-500/40'
    case 'Medium': return 'bg-amber-950 text-amber-300 border-amber-500/40'
    default: return 'bg-slate-800 text-slate-300 border-slate-600'
  }
}
