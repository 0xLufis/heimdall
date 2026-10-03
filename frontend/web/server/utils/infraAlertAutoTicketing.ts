import { ticketsStore } from './ticketsStore'

/**
 * Infrastructure Alert Auto-Ticketing Engine.
 * Automatically generates maintenance tickets when infrastructure thresholds
 * are breached. Implements deduplication windowing to prevent duplicate tickets
 * during persistent or flapping infrastructure events.
 */

export type InfraAlertType =
  | 'agent_communication_loss'
  | 'controller_unreachable'
  | 'disk_spool_critical'
  | 'certificate_expiring'
  | 'memory_threshold_exceeded'
  | 'network_degradation'

export interface InfraAlert {
  alertType: InfraAlertType
  machineIdentifier: string
  hostname: string
  severity: 'Critical' | 'High' | 'Medium' | 'Low'
  message: string
  metricValue?: number
  thresholdValue?: number
  detectedAt: string
}

export interface InfraTicketResult {
  generated: boolean
  ticketId?: string
  ticketNumber?: string
  reason: string
  deduplicatedWith?: string
}

const ALERT_PRIORITY_MAP: Record<InfraAlertType, { priority: string; department: string; slaHours: number }> = {
  agent_communication_loss: { priority: 'Critical', department: 'IT', slaHours: 4 },
  controller_unreachable: { priority: 'Critical', department: 'Controls', slaHours: 2 },
  disk_spool_critical: { priority: 'High', department: 'IT', slaHours: 8 },
  certificate_expiring: { priority: 'Medium', department: 'IT', slaHours: 48 },
  memory_threshold_exceeded: { priority: 'High', department: 'Controls', slaHours: 8 },
  network_degradation: { priority: 'Medium', department: 'IT', slaHours: 24 }
}

const ALERT_TITLE_MAP: Record<InfraAlertType, string> = {
  agent_communication_loss: 'Edge Agent Communication Loss',
  controller_unreachable: 'Industrial Controller Unreachable',
  disk_spool_critical: 'Offline Spool Disk Space Critical',
  certificate_expiring: 'Client Certificate Approaching Expiration',
  memory_threshold_exceeded: 'Agent Process Memory Threshold Exceeded',
  network_degradation: 'Network Degradation Detected'
}

/** Deduplication window: 15 minutes */
const DEDUP_WINDOW_MS = 15 * 60 * 1000

/**
 * Tracks recent alerts to prevent duplicate ticket creation during flapping.
 * Key: `${alertType}:${machineIdentifier}`, Value: { ticketId, timestamp }
 */
const recentAlerts = new Map<string, { ticketId: string; ticketNumber: string; timestamp: number }>()

export function evaluateInfraAlert(alert: InfraAlert): InfraTicketResult {
  const dedupKey = `${alert.alertType}:${alert.machineIdentifier}`
  const now = Date.now()

  // Check deduplication window
  const existing = recentAlerts.get(dedupKey)
  if (existing && (now - existing.timestamp) < DEDUP_WINDOW_MS) {
    return {
      generated: false,
      reason: `Deduplicated: existing ticket ${existing.ticketNumber} covers this alert (within ${Math.round(DEDUP_WINDOW_MS / 60000)}m dedup window)`,
      deduplicatedWith: existing.ticketNumber
    }
  }

  const classification = ALERT_PRIORITY_MAP[alert.alertType]
  if (!classification) {
    return {
      generated: false,
      reason: `Unknown alert type: ${alert.alertType}`
    }
  }

  const titlePrefix = ALERT_TITLE_MAP[alert.alertType] || alert.alertType
  const nowDate = new Date()
  const randomSuffix = Math.floor(1000 + Math.random() * 9000)
  const dateStr = nowDate.toISOString().slice(0, 10).replace(/-/g, '')
  const ticketNumber = `TKT-${dateStr}-${randomSuffix}`
  const ticketId = `tkt-${Date.now()}-${randomSuffix}`

  const ticket = {
    id: ticketId,
    ticketNumber,
    stationId: alert.machineIdentifier,
    stationName: alert.hostname,
    title: `[INFRA] ${titlePrefix}: ${alert.hostname}`,
    description: `${alert.message}\n\nDetected: ${alert.detectedAt}\nMetric: ${alert.metricValue ?? 'N/A'} (Threshold: ${alert.thresholdValue ?? 'N/A'})`,
    status: 'Open' as const,
    priority: classification.priority,
    category: 'Infrastructure',
    issueType: 'Infrastructure',
    originatorType: 'InfraAutoTicket',
    isLineStop: classification.priority === 'Critical',
    responsibleDepartment: classification.department,
    tags: ['InfraAutoTicket', alert.alertType, classification.department],
    reportedByUserId: 'system-infra-monitor',
    reportedByUserName: 'Heimdall Infrastructure Monitor',
    createdAt: nowDate.toISOString(),
    updatedAt: nowDate.toISOString(),
    slaDueAt: new Date(nowDate.getTime() + classification.slaHours * 3600 * 1000).toISOString(),
    comments: [],
    attachments: [],
    changeHistory: [],
    telemetrySnapshot: {
      timestamp: alert.detectedAt,
      source: `InfraAlert-${alert.alertType}`,
      metrics: {
        alertType: alert.alertType,
        metricValue: alert.metricValue,
        thresholdValue: alert.thresholdValue
      }
    }
  }

  try {
    const { addTicketToStore } = require('./ticketsStore')
    addTicketToStore(ticket as any)
  } catch {
    // Ticket store may not be initialized in all contexts
  }

  // Record for deduplication
  recentAlerts.set(dedupKey, { ticketId, ticketNumber, timestamp: now })

  return {
    generated: true,
    ticketId,
    ticketNumber,
    reason: `Infrastructure ticket generated: ${classification.priority} priority, assigned to ${classification.department}`
  }
}

export function getRecentAlertCount(): number {
  return recentAlerts.size
}

export function clearDedupWindow(): void {
  recentAlerts.clear()
}

export function getAlertPriorityMap() {
  return { ...ALERT_PRIORITY_MAP }
}

export function getAlertTitleMap() {
  return { ...ALERT_TITLE_MAP }
}
