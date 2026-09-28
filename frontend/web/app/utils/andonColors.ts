/**
 * IEC 60073 / Industrial Andon Color Coding
 *
 * Andon states aligned to IEC 60073 and common industrial practice:
 * - RED    (STOP / Alarm / Escalated)  : Line stop, safety alarm, active escalation
 * - YELLOW (CAUTION / Pending)         : Awaiting action, parts, or external input
 * - BLUE   (ACTIVE / In Progress)      : Technician actively working
 * - CYAN   (CALL / Open)               : Assistance called, new ticket logged
 * - GREEN  (NORMAL / Resolved)         : Machine cleared, ticket resolved
 * - SLATE  (CLOSED)                    : Ticket closed
 */

import type { TicketStatus, TicketPriority, PendingReason } from '~/types/maintenance'

export type AndonColorType = 'red' | 'yellow' | 'blue' | 'cyan' | 'green' | 'slate'

export interface AndonStyleConfig {
  andonType: AndonColorType
  label: string
  dotClass: string
  badgeClass: string
  borderClass: string
  stripeClass: string
  cardBgClass: string
  tableBorderClass: string
  textClass: string
}

export const ANDON_STYLES: Record<AndonColorType, AndonStyleConfig> = {
  red: {
    andonType: 'red',
    label: 'Escalated',
    dotClass: 'bg-rose-500 animate-pulse ring-2 ring-rose-500/40 shadow-[0_0_8px_rgba(244,63,94,0.6)]',
    badgeClass: 'bg-rose-500/15 text-rose-700 dark:text-rose-400 border border-rose-500/30',
    borderClass: 'border-rose-500/50 hover:border-rose-500',
    stripeClass: 'bg-gradient-to-r from-rose-600 via-rose-500 to-rose-600',
    cardBgClass: 'bg-card hover:bg-rose-500/[0.03] dark:hover:bg-rose-950/20',
    tableBorderClass: 'border-l-4 border-l-rose-500',
    textClass: 'text-rose-600 dark:text-rose-400'
  },
  yellow: {
    andonType: 'yellow',
    label: 'Pending',
    dotClass: 'bg-amber-500 ring-2 ring-amber-500/40 shadow-[0_0_8px_rgba(245,158,11,0.5)]',
    badgeClass: 'bg-amber-500/15 text-amber-800 dark:text-amber-300 border border-amber-500/30',
    borderClass: 'border-amber-500/50 hover:border-amber-500',
    stripeClass: 'bg-gradient-to-r from-amber-600 via-amber-500 to-amber-600',
    cardBgClass: 'bg-card hover:bg-amber-500/[0.03] dark:hover:bg-amber-950/20',
    tableBorderClass: 'border-l-4 border-l-amber-500',
    textClass: 'text-amber-600 dark:text-amber-400'
  },
  blue: {
    andonType: 'blue',
    label: 'In Progress',
    dotClass: 'bg-blue-600 ring-2 ring-blue-500/40 shadow-[0_0_8px_rgba(37,99,235,0.5)]',
    badgeClass: 'bg-blue-500/15 text-blue-700 dark:text-blue-300 border border-blue-500/30',
    borderClass: 'border-blue-500/50 hover:border-blue-500',
    stripeClass: 'bg-gradient-to-r from-blue-700 via-blue-600 to-blue-500',
    cardBgClass: 'bg-card hover:bg-blue-500/[0.03] dark:hover:bg-blue-950/20',
    tableBorderClass: 'border-l-4 border-l-blue-600',
    textClass: 'text-blue-600 dark:text-blue-400'
  },
  cyan: {
    andonType: 'cyan',
    label: 'Open',
    dotClass: 'bg-cyan-400 ring-2 ring-cyan-400/40 shadow-[0_0_8px_rgba(34,211,238,0.5)]',
    badgeClass: 'bg-cyan-500/15 text-cyan-700 dark:text-cyan-300 border border-cyan-500/30',
    borderClass: 'border-cyan-500/50 hover:border-cyan-500',
    stripeClass: 'bg-gradient-to-r from-cyan-600 via-cyan-500 to-cyan-400',
    cardBgClass: 'bg-card hover:bg-cyan-500/[0.03] dark:hover:bg-cyan-950/20',
    tableBorderClass: 'border-l-4 border-l-cyan-500',
    textClass: 'text-cyan-600 dark:text-cyan-400'
  },
  green: {
    andonType: 'green',
    label: 'Resolved',
    dotClass: 'bg-emerald-500 ring-2 ring-emerald-500/40 shadow-[0_0_8px_rgba(16,185,129,0.5)]',
    badgeClass: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border border-emerald-500/30',
    borderClass: 'border-emerald-500/50 hover:border-emerald-500',
    stripeClass: 'bg-gradient-to-r from-emerald-600 via-emerald-500 to-emerald-400',
    cardBgClass: 'bg-card hover:bg-emerald-500/[0.03] dark:hover:bg-emerald-950/20',
    tableBorderClass: 'border-l-4 border-l-emerald-500',
    textClass: 'text-emerald-600 dark:text-emerald-400'
  },
  slate: {
    andonType: 'slate',
    label: 'Closed',
    dotClass: 'bg-slate-400 ring-2 ring-slate-400/40',
    badgeClass: 'bg-muted text-muted-foreground border border-border',
    borderClass: 'border-border hover:border-slate-500/50',
    stripeClass: 'bg-gradient-to-r from-slate-600 via-slate-500 to-slate-400',
    cardBgClass: 'bg-card hover:bg-muted/40',
    tableBorderClass: 'border-l-4 border-l-slate-400 dark:border-l-slate-600',
    textClass: 'text-muted-foreground'
  }
}

/**
 * Maps a ticket status to its Andon color.
 * Escalation is a separate overlay — callers should check isEscalated separately.
 */
export function getAndonColorForStatus(status: TicketStatus | string): AndonColorType {
  switch (status) {
    case 'Pending':  return 'yellow'
    case 'InProgress': return 'blue'
    case 'Open':     return 'cyan'
    case 'Resolved': return 'green'
    case 'Closed':   return 'slate'
    default:         return 'slate'
  }
}

export function getAndonPriorityStyle(priority: TicketPriority | string): {
  dotClass: string
  badgeClass: string
  label: string
} {
  switch (priority) {
    case 'Critical':
      return {
        dotClass: 'bg-rose-500 animate-pulse',
        badgeClass: 'bg-rose-500/15 text-rose-700 dark:text-rose-400 border border-rose-500/30 font-black',
        label: 'Critical'
      }
    case 'High':
      return {
        dotClass: 'bg-amber-500',
        badgeClass: 'bg-amber-500/15 text-amber-800 dark:text-amber-400 border border-amber-500/30 font-bold',
        label: 'High'
      }
    case 'Medium':
      return {
        dotClass: 'bg-blue-500',
        badgeClass: 'bg-blue-500/15 text-blue-700 dark:text-blue-400 border border-blue-500/30 font-medium',
        label: 'Medium'
      }
    case 'Low':
    default:
      return {
        dotClass: 'bg-slate-400',
        badgeClass: 'bg-muted text-muted-foreground border border-border font-medium',
        label: 'Low'
      }
  }
}

export interface PendingReasonConfig {
  id: PendingReason
  label: string
  description: string
  badgeClass: string
}

/**
 * Display metadata for each PendingReason value.
 * Maps to backend PendingReason enum: None / Parts / ExternalOk / Action
 */
export const PENDING_REASON_CONFIGS: Record<PendingReason, PendingReasonConfig> = {
  None: {
    id: 'None',
    label: 'No specific reason',
    description: 'Pending without a specific categorized reason',
    badgeClass: 'bg-muted text-muted-foreground border border-border'
  },
  Parts: {
    id: 'Parts',
    label: 'Awaiting parts',
    description: 'Replacement components, servos, or warehouse stock not yet received',
    badgeClass: 'bg-amber-500/15 text-amber-800 dark:text-amber-300 border border-amber-500/30'
  },
  ExternalOk: {
    id: 'ExternalOk',
    label: 'Pending external sign-off',
    description: 'Awaiting approval or confirmation from an external party (OEM, vendor, QA)',
    badgeClass: 'bg-blue-500/15 text-blue-800 dark:text-blue-300 border border-blue-500/30'
  },
  Action: {
    id: 'Action',
    label: 'Pending action',
    description: 'Awaiting a specific corrective action by the technician or engineer',
    badgeClass: 'bg-rose-500/15 text-rose-800 dark:text-rose-300 border border-rose-500/30'
  }
}
