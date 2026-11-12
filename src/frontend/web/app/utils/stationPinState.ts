import type { StationSlaSummary, StationTelemetryKpi } from '~/composables/useStationTelemetry'

/**
 * Colour-coded state of a station pin on the plant map, most urgent first. A pin shows one tone;
 * the reason and the open-ticket count are in its tooltip, the numbers in the station card.
 */
export type StationPinTone = 'critical' | 'warning' | 'maintenance' | 'idle' | 'running' | 'unknown'

export interface StationPinState {
  tone: StationPinTone
  /** Why the pin has this tone (tooltip / accessible name), e.g. "Faulted", "Due in 45m". */
  reason: string
  /** Last reported operational status (UNKNOWN without telemetry); live events are compared with it. */
  status: string
  /** Open tickets on the station. */
  openTickets: number
  /** Shown by the "Alerts & SLA" filter: critical or warning tone, or open tickets. */
  alert: boolean
}

/** Running stations below this OEE are shown as warnings (the station card's red OEE band). */
export const PIN_LOW_OEE_PERCENT = 70

export const STATION_PIN_TONES: Record<StationPinTone, { label: string; color: string }> = {
  critical: { label: 'Faulted / SLA breached', color: '#f43f5e' },
  warning: { label: 'Warning / SLA at risk', color: '#f59e0b' },
  maintenance: { label: 'Maintenance', color: '#a855f7' },
  idle: { label: 'Idle', color: '#38bdf8' },
  running: { label: 'Running', color: '#10b981' },
  unknown: { label: 'No data', color: '#64748b' }
}

/** Legend order. */
export const STATION_PIN_TONE_ORDER: StationPinTone[] = ['critical', 'warning', 'maintenance', 'idle', 'running', 'unknown']

export function stationPinState(
  kpi: StationTelemetryKpi | null | undefined,
  sla: StationSlaSummary | null | undefined
): StationPinState {
  const status = kpi?.operationalStatus ?? 'UNKNOWN'
  const openTickets = sla?.openTickets ?? 0
  const slaText = sla?.earliestDueText || null

  let tone: StationPinTone
  let reason: string
  if (status === 'FAULTED') {
    tone = 'critical'
    reason = 'Faulted'
  } else if (sla?.slaHealth === 'breached') {
    tone = 'critical'
    reason = slaText || 'SLA breached'
  } else if (status === 'WARNING') {
    tone = 'warning'
    reason = 'Warning'
  } else if (sla?.slaHealth === 'warning') {
    // Due within two hours, or a critical ticket is open.
    tone = 'warning'
    reason = sla.criticalTickets > 0
      ? `${sla.criticalTickets} critical ticket${sla.criticalTickets === 1 ? '' : 's'}`
      : (slaText ? `SLA ${slaText.charAt(0).toLowerCase()}${slaText.slice(1)}` : 'SLA at risk')
  } else if (status === 'RUNNING' && kpi?.oee !== null && kpi?.oee !== undefined && kpi.oee < PIN_LOW_OEE_PERCENT) {
    tone = 'warning'
    reason = `OEE below ${PIN_LOW_OEE_PERCENT} %`
  } else if (status === 'MAINTENANCE') {
    tone = 'maintenance'
    reason = 'Maintenance'
  } else if (status === 'IDLE') {
    tone = 'idle'
    reason = 'Idle'
  } else if (status === 'RUNNING') {
    tone = 'running'
    reason = 'Running'
  } else {
    tone = 'unknown'
    reason = kpi ? 'State not reported' : 'No telemetry'
  }

  return { tone, reason, status, openTickets, alert: tone === 'critical' || tone === 'warning' || openTickets > 0 }
}

export const sameStationPinState = (a: StationPinState, b: StationPinState) =>
  a.tone === b.tone && a.reason === b.reason && a.status === b.status && a.openTickets === b.openTickets && a.alert === b.alert

/** "L01-OP100 · SLA due in 45m · 2 open tickets" */
export function describeStationPin(handle: string, state: StationPinState | null | undefined): string {
  if (!state) return handle
  const tickets = state.openTickets > 0 ? ` · ${state.openTickets} open ticket${state.openTickets === 1 ? '' : 's'}` : ''
  return `${handle} · ${state.reason}${tickets}`
}
