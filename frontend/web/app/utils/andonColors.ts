/**
 * Samsung Andon Industrial Color Coding System & Helpers
 * Aligned with Samsung Smart Factory and display/semiconductor line visual management.
 *
 * Andon Lamp States:
 * - RED (정지 / Line Stop / Emergency / Escalated): Critical line stop, safety alarm, external escalation
 * - YELLOW / AMBER (경고 / Caution / Delay / Pending): Awaiting parts, approval, lab, SAP, or closure
 * - BLUE (진행 / Maintenance Active / In Progress): Samsung signature blue, active technician work
 * - CYAN / WHITE (호출 / Call / Open): Assistance called, new ticket logged, dispatch pending
 * - GREEN (정상 / Running / Normal / Resolved): Machine cleared, line running, sign-off complete
 * - SLATE / GRAY (종료 / Bypassed / Unresolved): Decommissioned, closed unresolved, cancelled
 */

import type { TicketStatus, TicketPriority } from '~/types/maintenance'

export type AndonColorType = 'red' | 'yellow' | 'blue' | 'cyan' | 'green' | 'slate'

export type PendingReason =
  | 'Parts'
  | 'Approval'
  | 'Lab'
  | 'SAP/Traceability'
  | 'External'
  | 'Closure'

export type ClosureAuthorityRole =
  | 'Engineer'
  | 'Group_Leader'
  | 'Manager'
  | 'Director'

export interface AndonStyleConfig {
  andonType: AndonColorType
  label: string
  koreanLabel: string
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
    label: 'Escalated / Line Down',
    koreanLabel: '정지 (ALARM)',
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
    label: 'Pending / Caution',
    koreanLabel: '경고 (CAUTION)',
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
    label: 'In Progress / Working',
    koreanLabel: '진행 (ACTIVE)',
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
    label: 'Open / Call Incoming',
    koreanLabel: '호출 (CALL)',
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
    label: 'Resolved / Normal',
    koreanLabel: '정상 (NORMAL)',
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
    label: 'Unresolved / Offline',
    koreanLabel: '종료 (CLOSED)',
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
 * Maps a ticket status string to its corresponding Samsung Andon Color
 */
export function getAndonColorForStatus(status: TicketStatus | string): AndonColorType {
  switch (status) {
    case 'Escalated':
    case 'Escalated_External':
      return 'red'

    case 'Pending':
    case 'Pending_Parts':
    case 'Closure_Pending':
    case 'Pending_Validation':
    case 'Waiting_On_Feedback':
      return 'yellow'

    case 'In_Progress':
      return 'blue'

    case 'Open':
    case 'Draft':
      return 'cyan'

    case 'Resolved':
    case 'Closed':
      return 'green'

    case 'Closed_Unresolved':
    case 'Cancelled':
    case 'Archived':
    default:
      return 'slate'
  }
}

/**
 * Maps priority to Samsung Andon alert styling
 */
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
        label: 'CRITICAL'
      }
    case 'High':
      return {
        dotClass: 'bg-amber-500',
        badgeClass: 'bg-amber-500/15 text-amber-800 dark:text-amber-400 border border-amber-500/30 font-bold',
        label: 'HIGH'
      }
    case 'Medium':
      return {
        dotClass: 'bg-blue-500',
        badgeClass: 'bg-blue-500/15 text-blue-700 dark:text-blue-400 border border-blue-500/30 font-medium',
        label: 'MEDIUM'
      }
    case 'Low':
    default:
      return {
        dotClass: 'bg-slate-400',
        badgeClass: 'bg-muted text-muted-foreground border border-border font-medium',
        label: 'LOW'
      }
  }
}

export const PENDING_REASONS: {
  id: PendingReason
  label: string
  shortLabel: string
  iconName: string
  description: string
  badgeColor: string
}[] = [
  {
    id: 'Parts',
    label: 'Parts (Spare Parts)',
    shortLabel: 'Parts',
    iconName: 'Package',
    description: 'Awaiting replacement components, servos, or warehouse stock',
    badgeColor: 'bg-amber-500/15 text-amber-800 dark:text-amber-300 border-amber-500/30'
  },
  {
    id: 'Approval',
    label: 'Approval (Engineering/QA)',
    shortLabel: 'Approval',
    iconName: 'FileCheck',
    description: 'Awaiting engineering change order, tech sign-off, or QA release',
    badgeColor: 'bg-blue-500/15 text-blue-800 dark:text-blue-300 border-blue-500/30'
  },
  {
    id: 'Lab',
    label: 'Lab (Testing / Analysis)',
    shortLabel: 'Lab',
    iconName: 'FlaskConical',
    description: 'Awaiting metallurgical, optical inspection, chemical sample, or micro-sectioning',
    badgeColor: 'bg-purple-500/15 text-purple-800 dark:text-purple-300 border-purple-500/30'
  },
  {
    id: 'SAP/Traceability',
    label: 'SAP / Traceability (ERP/MES)',
    shortLabel: 'SAP / Trace',
    iconName: 'Database',
    description: 'Awaiting SAP material booking, serial trace unblocking, or scrap sign-off',
    badgeColor: 'bg-teal-500/15 text-teal-800 dark:text-teal-300 border-teal-500/30'
  },
  {
    id: 'External',
    label: 'External (OEM / Vendor)',
    shortLabel: 'External',
    iconName: 'Globe',
    description: 'Awaiting external field specialist dispatch (e.g. Beckhoff, KUKA, Siemens)',
    badgeColor: 'bg-rose-500/15 text-rose-800 dark:text-rose-300 border-rose-500/30'
  },
  {
    id: 'Closure',
    label: 'Closure (Higher Authority)',
    shortLabel: 'Closure',
    iconName: 'Lock',
    description: 'Awaiting final closure verification from an engineer, group leader, or manager',
    badgeColor: 'bg-cyan-500/15 text-cyan-800 dark:text-cyan-300 border-cyan-500/30'
  }
]

export const CLOSURE_AUTHORITIES: {
  id: ClosureAuthorityRole
  label: string
  roleKey: string
}[] = [
  { id: 'Engineer', label: 'Controls Engineer', roleKey: 'controls_engineer' },
  { id: 'Group_Leader', label: 'Group Leader', roleKey: 'group_leader' },
  { id: 'Manager', label: 'Maintenance Manager', roleKey: 'manager' },
  { id: 'Director', label: 'Plant Director', roleKey: 'plant_director' }
]

/**
 * Normalizes ticket status into one of the 6 canonical columns:
 * 'Open' | 'In_Progress' | 'Pending' | 'Escalated' | 'Resolved' | 'Closed_Unresolved'
 */
export function getCanonicalColumn(status: TicketStatus | string): 'Open' | 'In_Progress' | 'Pending' | 'Escalated' | 'Resolved' | 'Closed_Unresolved' {
  switch (status) {
    case 'Open':
    case 'Draft':
      return 'Open'

    case 'In_Progress':
      return 'In_Progress'

    case 'Pending':
    case 'Pending_Parts':
    case 'Closure_Pending':
    case 'Pending_Validation':
    case 'Waiting_On_Feedback':
      return 'Pending'

    case 'Escalated':
    case 'Escalated_External':
      return 'Escalated'

    case 'Resolved':
    case 'Closed':
      return 'Resolved'

    case 'Closed_Unresolved':
    case 'Cancelled':
    case 'Archived':
    default:
      return 'Closed_Unresolved'
  }
}

/**
 * Extracts or derives the pending reason from ticket properties
 */
export function getTicketPendingReason(ticket: { status: string; pendingReason?: string; tags?: string[] }): PendingReason {
  if (ticket.pendingReason) {
    const matched = PENDING_REASONS.find(r => r.id.toLowerCase() === ticket.pendingReason?.toLowerCase())
    if (matched) return matched.id
  }

  if (ticket.status === 'Closure_Pending') return 'Closure'
  if (ticket.status === 'Pending_Parts') return 'Parts'

  if (ticket.tags) {
    if (ticket.tags.some(t => t.toLowerCase().includes('lab'))) return 'Lab'
    if (ticket.tags.some(t => t.toLowerCase().includes('approval'))) return 'Approval'
    if (ticket.tags.some(t => t.toLowerCase().includes('sap') || t.toLowerCase().includes('trace'))) return 'SAP/Traceability'
    if (ticket.tags.some(t => t.toLowerCase().includes('external'))) return 'External'
  }

  return 'Parts'
}
