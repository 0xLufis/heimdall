import { ref, computed } from 'vue'
import type { AlertRule, AlertEvent, TelemetryMetricSample, RuleOperator, AlertSeverity } from '~/types/alertRules'
import { useMaintenance } from '~/composables/useMaintenance'

// Initial standard rules catalog
const defaultRules: AlertRule[] = [
  {
    id: 'rule-jit-01',
    name: 'Soft-PLC Cyclic Task Jitter Spike',
    description: 'Triggers when real-time TwinCAT / Soft-PLC cycle jitter exceeds deterministic real-time deadline.',
    category: 'jitter',
    targetType: 'all',
    metricKey: 'cycle_jitter_us',
    metricLabel: 'PLC Cycle Jitter',
    condition: '>',
    threshold: 45.0,
    unit: 'μs',
    severity: 'Critical',
    enabled: true,
    autoCreateTicket: true,
    errorCode: 'E-PLC-JITTER',
    ticketPriority: 'Critical',
    assignedTechnicianName: 'Controls Lead Engineer',
    cooldownMinutes: 15,
    triggerCount: 3,
    lastTriggeredAt: new Date(Date.now() - 3600000).toISOString()
  },
  {
    id: 'rule-temp-02',
    name: 'Laser / Spindle Bearing Over-Temperature',
    description: 'Detects thermal runaway or coolant circuit failure across high-speed process actuators.',
    category: 'thermal',
    targetType: 'machine',
    targetId: 'm-op20',
    targetName: 'OP20-Weld Laser Cell',
    metricKey: 'motor_temp_c',
    metricLabel: 'Core Bearing Temp',
    condition: '>',
    threshold: 72.0,
    unit: '°C',
    severity: 'Critical',
    enabled: true,
    autoCreateTicket: true,
    errorCode: 'W-THERM-OVERHEAT',
    ticketPriority: 'Critical',
    assignedTechnicianName: 'Shift Maintenance Tech',
    cooldownMinutes: 20,
    triggerCount: 6,
    lastTriggeredAt: new Date(Date.now() - 1800000).toISOString()
  },
  {
    id: 'rule-vib-03',
    name: 'Robotic Axis Mechanical Vibration RMS',
    description: 'Warns when vibration harmonics indicate impending bearing fluting, looseness, or gear wear.',
    category: 'vibration',
    targetType: 'all',
    metricKey: 'vibration_rms_mms',
    metricLabel: 'Vibration RMS Velocity',
    condition: '>',
    threshold: 2.7,
    unit: 'mm/s',
    severity: 'High',
    enabled: true,
    autoCreateTicket: true,
    errorCode: 'E-VIB-WEAR',
    ticketPriority: 'High',
    assignedTechnicianName: 'Mechanical Reliability Specialist',
    cooldownMinutes: 30,
    triggerCount: 2,
    lastTriggeredAt: new Date(Date.now() - 7200000).toISOString()
  },
  {
    id: 'rule-cpu-04',
    name: 'IPC Core CPU Sustained Saturation',
    description: 'Identifies unthrottled background threads or infinite loops threatening HMI responsiveness.',
    category: 'resources',
    targetType: 'all',
    metricKey: 'cpu_usage_pct',
    metricLabel: 'Host CPU Load',
    condition: '>',
    threshold: 85.0,
    unit: '%',
    severity: 'Medium',
    enabled: true,
    autoCreateTicket: true,
    errorCode: 'E-IPC-CPU-SAT',
    ticketPriority: 'Medium',
    assignedTechnicianName: 'IT/OT System Admin',
    cooldownMinutes: 30,
    triggerCount: 1,
    lastTriggeredAt: new Date(Date.now() - 14400000).toISOString()
  },
  {
    id: 'rule-ram-05',
    name: 'IPC System Memory Leak Warning',
    description: 'Proactively flags edge runtimes with memory utilization crossing critical swap boundaries.',
    category: 'resources',
    targetType: 'all',
    metricKey: 'ram_usage_pct',
    metricLabel: 'Host RAM Load',
    condition: '>',
    threshold: 90.0,
    unit: '%',
    severity: 'High',
    enabled: true,
    autoCreateTicket: true,
    errorCode: 'E-IPC-RAM-LEAK',
    ticketPriority: 'High',
    assignedTechnicianName: 'IT/OT System Admin',
    cooldownMinutes: 45,
    triggerCount: 0
  },
  {
    id: 'rule-pneu-06',
    name: 'Pneumatic Header Line Pressure Sag',
    description: 'Protects automated pick-and-place grippers and pneumatic clamps against supply pressure collapse.',
    category: 'pneumatics',
    targetType: 'all',
    metricKey: 'pneumatic_pressure_bar',
    metricLabel: 'Pneumatic Pressure',
    condition: '<',
    threshold: 5.4,
    unit: 'bar',
    severity: 'Critical',
    enabled: true,
    autoCreateTicket: true,
    errorCode: 'E-PNEU-PRESSURE',
    ticketPriority: 'High',
    assignedTechnicianName: 'Facility Engineering Tech',
    cooldownMinutes: 15,
    triggerCount: 4,
    lastTriggeredAt: new Date(Date.now() - 900000).toISOString()
  },
  {
    id: 'rule-fb-07',
    name: 'EtherCAT Frame CRC & Packet Drop Rate',
    description: 'Detects industrial fieldbus physical layer degradation, noise coupling, or loose RJ45 connectors.',
    category: 'fieldbus',
    targetType: 'all',
    metricKey: 'fieldbus_error_rate',
    metricLabel: 'Fieldbus Packet Drops',
    condition: '>',
    threshold: 3.0,
    unit: 'err/min',
    severity: 'High',
    enabled: true,
    autoCreateTicket: true,
    errorCode: 'E-FB-CRC-DROP',
    ticketPriority: 'High',
    assignedTechnicianName: 'Fieldbus Network Engineer',
    cooldownMinutes: 20,
    triggerCount: 2
  }
]

// Initial seed alerts
const initialAlerts: AlertEvent[] = [
  {
    id: 'alt-001',
    ruleId: 'rule-temp-02',
    ruleName: 'Laser / Spindle Bearing Over-Temperature',
    category: 'thermal',
    severity: 'Critical',
    targetId: 'm-op20',
    targetName: 'OP20-Weld Laser Cell',
    metricKey: 'motor_temp_c',
    metricLabel: 'Core Bearing Temp',
    measuredValue: 78.4,
    threshold: 72.0,
    unit: '°C',
    condition: '>',
    triggeredAt: new Date(Date.now() - 1800000).toISOString(),
    status: 'active',
    ticketId: 'tkt-auto-01',
    ticketNumber: 'TKT-2026-0084'
  },
  {
    id: 'alt-002',
    ruleId: 'rule-pneu-06',
    ruleName: 'Pneumatic Header Line Pressure Sag',
    category: 'pneumatics',
    severity: 'Critical',
    targetId: 'm-op10',
    targetName: 'OP10-Dispense Bonding Cell',
    metricKey: 'pneumatic_pressure_bar',
    metricLabel: 'Pneumatic Pressure',
    measuredValue: 4.8,
    threshold: 5.4,
    unit: 'bar',
    condition: '<',
    triggeredAt: new Date(Date.now() - 900000).toISOString(),
    status: 'active',
    ticketId: 'tkt-auto-02',
    ticketNumber: 'TKT-2026-0085'
  },
  {
    id: 'alt-003',
    ruleId: 'rule-vib-03',
    ruleName: 'Robotic Axis Mechanical Vibration RMS',
    category: 'vibration',
    severity: 'High',
    targetId: 'm-op50',
    targetName: 'OP50-Fasten Screwing Station',
    metricKey: 'vibration_rms_mms',
    metricLabel: 'Vibration RMS Velocity',
    measuredValue: 3.12,
    threshold: 2.7,
    unit: 'mm/s',
    condition: '>',
    triggeredAt: new Date(Date.now() - 7200000).toISOString(),
    status: 'acknowledged',
    acknowledgedAt: new Date(Date.now() - 3600000).toISOString(),
    acknowledgedBy: 'Shift Supervisor',
    ticketId: 'tkt-auto-03',
    ticketNumber: 'TKT-2026-0082'
  }
]

// Global singleton state
const rules = ref<AlertRule[]>(defaultRules)
const alerts = ref<AlertEvent[]>(initialAlerts)
const isEvaluating = ref(false)
const lastEvaluationTime = ref<string>(new Date().toISOString())

export const useAlertRules = () => {
  const maintenance = useMaintenance()

  const activeAlertsCount = computed(() => {
    return alerts.value.filter(a => a.status === 'active').length
  })

  const criticalAlertsCount = computed(() => {
    return alerts.value.filter(a => a.status === 'active' && a.severity === 'Critical').length
  })

  const enabledRulesCount = computed(() => {
    return rules.value.filter(r => r.enabled).length
  })

  const totalAutomatedTicketsCount = computed(() => {
    return alerts.value.filter(a => a.ticketNumber).length
  })

  // Compare metric value against rule threshold
  const checkCondition = (val: number, condition: RuleOperator, threshold: number): boolean => {
    switch (condition) {
      case '>': return val > threshold
      case '>=': return val >= threshold
      case '<': return val < threshold
      case '<=': return val <= threshold
      case '==': return Math.abs(val - threshold) < 0.0001
      case '!=': return Math.abs(val - threshold) >= 0.0001
      default: return false
    }
  }

  // Evaluate a batch of telemetry measurements against enabled rules
  const evaluateTelemetry = async (sample: TelemetryMetricSample): Promise<AlertEvent[]> => {
    const triggeredEvents: AlertEvent[] = []
    const now = Date.now()

    for (const rule of rules.value) {
      if (!rule.enabled) continue

      // Target matching: 'all' or exact target ID
      if (rule.targetType !== 'all' && rule.targetId && rule.targetId !== sample.targetId) {
        continue
      }

      // Check if metric exists in sample
      const val = sample.metrics[rule.metricKey]
      if (val === undefined || typeof val !== 'number') continue

      const isBreached = checkCondition(val, rule.condition, rule.threshold)
      if (!isBreached) continue

      // Check cooldown
      if (rule.lastTriggeredAt) {
        const lastTime = new Date(rule.lastTriggeredAt).getTime()
        const cooldownMs = (rule.cooldownMinutes || 15) * 60 * 1000
        if (now - lastTime < cooldownMs) {
          // Still in cooldown period; suppress duplicate ticket/alert
          continue
        }
      }

      // Rule breached and cooldown passed -> Generate Alert
      const alertId = `alt-${Date.now()}-${Math.floor(Math.random() * 1000)}`
      const event: AlertEvent = {
        id: alertId,
        ruleId: rule.id,
        ruleName: rule.name,
        category: rule.category,
        severity: rule.severity,
        targetId: sample.targetId,
        targetName: sample.targetName,
        metricKey: rule.metricKey,
        metricLabel: rule.metricLabel,
        measuredValue: Math.round(val * 100) / 100,
        threshold: rule.threshold,
        unit: rule.unit,
        condition: rule.condition,
        triggeredAt: new Date().toISOString(),
        status: 'active'
      }

      // Auto-Ticket Dispatch
      if (rule.autoCreateTicket) {
        try {
          const newTicket = await maintenance.createTicket({
            title: `[AUTO-ALERT] ${rule.name}: ${sample.targetName}`,
            description: `Automated rule threshold breach detected by Heimdall Rules Engine.\n\n` +
              `• Target: ${sample.targetName} (${sample.targetId})\n` +
              `• Parameter: ${rule.metricLabel} (${rule.metricKey})\n` +
              `• Measured: ${val} ${rule.unit} (${rule.condition} ${rule.threshold} ${rule.unit})\n` +
              `• Rule ID: ${rule.id} [${rule.name}]\n` +
              `• Standard Error Code: ${rule.errorCode || 'E-GEN-ANOMALY'}\n` +
              `• Diagnostic Action: Immediate technician inspection requested.`,
            stationId: sample.targetType === 'machine' ? sample.targetId : undefined,
            controllerId: sample.targetType === 'controller' ? sample.targetId : undefined,
            priority: rule.ticketPriority || 'High',
            assignedTechnicianName: rule.assignedTechnicianName,
            errorCode: rule.errorCode,
            errorGroup: rule.category.toUpperCase(),
            telemetrySnapshot: {
              timestamp: new Date().toISOString(),
              metrics: {
                [rule.metricKey]: val,
                threshold: rule.threshold,
                ...sample.metrics
              }
            }
          })

          if (newTicket) {
            event.ticketId = newTicket.id
            event.ticketNumber = newTicket.ticketNumber
          }
        } catch {
          // Fallback ticket number if creation encounters error
          event.ticketNumber = `TKT-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(1000 + Math.random() * 9000)}`
        }
      }

      rule.triggerCount += 1
      rule.lastTriggeredAt = new Date().toISOString()
      alerts.value.unshift(event)
      triggeredEvents.push(event)
    }

    lastEvaluationTime.value = new Date().toISOString()
    return triggeredEvents
  }

  // Run simulated anomaly test to demonstrate real-time auto-ticketing
  const runSimulatedEvaluation = async (preset?: 'thermal' | 'jitter' | 'pressure' | 'vibration') => {
    isEvaluating.value = true
    try {
      let sample: TelemetryMetricSample

      switch (preset) {
        case 'jitter':
          sample = {
            targetId: 'ctrl-ipc-01',
            targetName: 'IPC-01 (Line 1 Soft-PLC Master)',
            targetType: 'controller',
            metrics: {
              cycle_jitter_us: 64.8,
              cpu_usage_pct: 72.0,
              ram_usage_pct: 64.0
            }
          }
          break

        case 'pressure':
          sample = {
            targetId: 'm-op10',
            targetName: 'OP10-Dispense Bonding Cell',
            targetType: 'machine',
            metrics: {
              pneumatic_pressure_bar: 4.65,
              cycle_jitter_us: 12.0
            }
          }
          break

        case 'vibration':
          sample = {
            targetId: 'm-op50',
            targetName: 'OP50-Fasten Screwing Station',
            targetType: 'machine',
            metrics: {
              vibration_rms_mms: 3.45,
              motor_temp_c: 54.0
            }
          }
          break

        case 'thermal':
        default:
          sample = {
            targetId: 'm-op20',
            targetName: 'OP20-Weld Laser Cell',
            targetType: 'machine',
            metrics: {
              motor_temp_c: 84.5,
              cycle_jitter_us: 18.0,
              vibration_rms_mms: 1.8
            }
          }
          break
      }

      const generated = await evaluateTelemetry(sample)
      return { sample, generated }
    } finally {
      isEvaluating.value = false
    }
  }

  const acknowledgeAlert = (alertId: string, userName = 'Operator') => {
    const alert = alerts.value.find(a => a.id === alertId)
    if (alert) {
      alert.status = 'acknowledged'
      alert.acknowledgedAt = new Date().toISOString()
      alert.acknowledgedBy = userName
    }
  }

  const resolveAlert = (alertId: string) => {
    const alert = alerts.value.find(a => a.id === alertId)
    if (alert) {
      alert.status = 'auto_resolved'
    }
  }

  const toggleRule = (ruleId: string) => {
    const rule = rules.value.find(r => r.id === ruleId)
    if (rule) {
      rule.enabled = !rule.enabled
    }
  }

  const updateRule = (updated: AlertRule) => {
    const idx = rules.value.findIndex(r => r.id === updated.id)
    if (idx !== -1) {
      rules.value[idx] = { ...updated }
    }
  }

  const addRule = (newRuleData: Omit<AlertRule, 'id' | 'triggerCount'>) => {
    const newRule: AlertRule = {
      ...newRuleData,
      id: `rule-${Date.now()}`,
      triggerCount: 0
    }
    rules.value.push(newRule)
    return newRule
  }

  const deleteRule = (ruleId: string) => {
    rules.value = rules.value.filter(r => r.id !== ruleId)
  }

  return {
    rules,
    alerts,
    isEvaluating,
    lastEvaluationTime,
    activeAlertsCount,
    criticalAlertsCount,
    enabledRulesCount,
    totalAutomatedTicketsCount,
    evaluateTelemetry,
    runSimulatedEvaluation,
    acknowledgeAlert,
    resolveAlert,
    toggleRule,
    updateRule,
    addRule,
    deleteRule
  }
}
