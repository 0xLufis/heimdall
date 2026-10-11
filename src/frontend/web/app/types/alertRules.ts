import type { TicketPriority } from '~/types/maintenance'

export type RuleCategory =
  | 'thermal'
  | 'vibration'
  | 'jitter'
  | 'resources'
  | 'fieldbus'
  | 'pneumatics'
  | 'quality'

export type RuleOperator = '>' | '>=' | '<' | '<=' | '==' | '!='

export type AlertSeverity = 'Low' | 'Medium' | 'High' | 'Critical'

export type AlertStatus = 'active' | 'acknowledged' | 'auto_resolved'

export interface AlertRule {
  id: string
  name: string
  description: string
  category: RuleCategory
  targetType: 'all' | 'machine' | 'controller'
  targetId?: string
  targetName?: string
  metricKey: string
  metricLabel: string
  condition: RuleOperator
  threshold: number
  unit: string
  severity: AlertSeverity
  enabled: boolean
  autoCreateTicket: boolean
  errorCode?: string
  ticketPriority: TicketPriority
  assignedTechnicianId?: string
  assignedTechnicianName?: string
  cooldownMinutes: number
  lastTriggeredAt?: string
  triggerCount: number
}

export interface AlertEvent {
  id: string
  ruleId: string
  ruleName: string
  category: RuleCategory
  severity: AlertSeverity
  targetId: string
  targetName: string
  metricKey: string
  metricLabel: string
  measuredValue: number
  threshold: number
  unit: string
  condition: RuleOperator
  triggeredAt: string
  status: AlertStatus
  acknowledgedAt?: string
  acknowledgedBy?: string
  ticketId?: string
  ticketNumber?: string
}

export interface TelemetryMetricSample {
  targetId: string
  targetName: string
  targetType: 'machine' | 'controller'
  metrics: Record<string, number>
  timestamp?: string
}
