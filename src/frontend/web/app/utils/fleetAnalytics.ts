/**
 * Fleet analytics: pure data logic for the grouped station list on /dashboard/analytics.
 *
 * Input is the backend's `FleetStationsDto` (GET /api/v1/analytics/fleet-stations, proxied by
 * `/api/analytics/fleet-stations`). Everything here only normalizes, filters, sorts, groups and
 * aggregates those rows; a value the backend did not report stays `null` and is rendered as an
 * explicit "no data" mark. Nothing in this module produces a figure of its own.
 */

// ───────────────────────────────────────────────────────────────── wire model

/**
 * RUNNING … MAINTENANCE: state reported by the PLC while telemetry is fresh. UNKNOWN: fresh
 * telemetry without a state. STALE: last telemetry older than the station's stale threshold
 * (missed samples × its learned sample interval). OFFLINE: the controller's agent disconnected
 * (MQTT last will / shutdown) and no data arrived since. NO_DATA: the station never reported.
 */
export type LiveState = 'RUNNING' | 'IDLE' | 'WARNING' | 'FAULTED' | 'MAINTENANCE' | 'UNKNOWN' | 'STALE' | 'OFFLINE' | 'NO_DATA'

export const LIVE_STATES: readonly LiveState[] = [
  'FAULTED', 'WARNING', 'OFFLINE', 'STALE', 'MAINTENANCE', 'IDLE', 'RUNNING', 'UNKNOWN', 'NO_DATA'
]

export interface FleetStation {
  stationId: string
  /** Floor-plan handle, else custom identifier, else machine name. */
  handle: string
  /** Display label of the machine. */
  name: string
  machineName: string
  customIdentifier: string | null
  pinnedObjectHandle: string | null
  organizationId: string | null
  organizationName: string | null
  machineType: string | null
  /** active | inactive | maintenance | decommissioned | dismantled (machine record). */
  lifecycleStatus: string
  lineId: string | null
  lineName: string | null
  subLineId: string | null
  subLineName: string | null
  projectId: string | null
  projectName: string | null
  businessUnitId: string | null
  businessUnitName: string | null
  liveState: LiveState
  /** Last state the PLC reported, whatever its age. */
  operationalStatus: string
  lastTelemetryAt: string | null
  /** Age of the last telemetry when the backend built the response. */
  telemetryAgeSeconds: number | null
  /** This station's stale threshold (seconds). */
  staleAfterSeconds: number | null
  /** Sample interval the backend learned for this station; null until learned. */
  sampleIntervalSeconds: number | null
  oee: number | null
  availability: number | null
  performance: number | null
  quality: number | null
  partsPerHour: number | null
  targetPartsPerHour: number | null
  cycleTimeSeconds: number | null
  targetCycleTimeSeconds: number | null
  totalPartsProduced: number | null
  scrapParts: number | null
  vibrationMmS: number | null
  temperatureC: number | null
  powerKw: number | null
  spindleRpm: number | null
  frequencyHz: number | null
  activeTicketCount: number
  criticalTicketCount: number
  hasSlaBreach: boolean
  /** Tickets created inside the look-back window. */
  incidentCount: number
  resolvedIncidentCount: number
  mttrMinutes: number | null
  downtimeMinutes: number | null
  lastIncidentAt: string | null
  oldestOpenTicketAt: string | null
  controllerCount: number
  controllersOnline: number
  controllerLastSeenAt: string | null
}

export interface FleetStationsMeta {
  generatedAt: string | null
  windowDays: number
  windowStart: string | null
  /** Fixed threshold, or the one of stations whose sample interval is not learned yet. */
  telemetryStaleAfterSeconds: number
  /** Per-station thresholds: stale after this many missed samples; null with a fixed threshold. */
  staleAfterMissedSamples: number | null
  /** Clamp of the per-station threshold (seconds); null with a fixed threshold. */
  staleFloorSeconds: number | null
  staleCeilingSeconds: number | null
  controllerOnlineWindowSeconds: number
  criticalSlaHours: number
}

export interface FleetStationsPayload {
  meta: FleetStationsMeta
  stations: FleetStation[]
}

const num = (value: unknown): number | null => {
  const n = typeof value === 'string' && value.trim() !== '' ? Number(value) : value
  return typeof n === 'number' && Number.isFinite(n) ? n : null
}
const count = (value: unknown): number => Math.max(0, Math.round(num(value) ?? 0))
const text = (value: unknown): string | null => {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

const asLiveState = (value: unknown): LiveState => {
  const state = typeof value === 'string' ? value.toUpperCase() : ''
  return (LIVE_STATES as readonly string[]).includes(state) ? (state as LiveState) : 'NO_DATA'
}

/** Normalizes one backend row; null when it carries no station id. */
export function normalizeFleetStation(raw: unknown): FleetStation | null {
  if (!raw || typeof raw !== 'object') return null
  const r = raw as Record<string, unknown>
  const stationId = text(r.stationId)
  if (!stationId) return null
  const name = text(r.name) ?? text(r.machineName) ?? text(r.customIdentifier) ?? stationId
  return {
    stationId,
    handle: text(r.handle) ?? text(r.customIdentifier) ?? name,
    name,
    machineName: text(r.machineName) ?? name,
    customIdentifier: text(r.customIdentifier),
    pinnedObjectHandle: text(r.pinnedObjectHandle),
    organizationId: text(r.organizationId),
    organizationName: text(r.organizationName),
    machineType: text(r.machineType),
    lifecycleStatus: (text(r.lifecycleStatus) ?? 'active').toLowerCase(),
    lineId: text(r.lineId),
    lineName: text(r.lineName),
    subLineId: text(r.subLineId),
    subLineName: text(r.subLineName),
    projectId: text(r.projectId),
    projectName: text(r.projectName),
    businessUnitId: text(r.businessUnitId),
    businessUnitName: text(r.businessUnitName),
    liveState: asLiveState(r.liveState),
    operationalStatus: (text(r.operationalStatus) ?? 'UNKNOWN').toUpperCase(),
    lastTelemetryAt: text(r.lastTelemetryAt),
    telemetryAgeSeconds: num(r.telemetryAgeSeconds),
    staleAfterSeconds: num(r.staleAfterSeconds),
    sampleIntervalSeconds: num(r.sampleIntervalSeconds),
    oee: num(r.oee),
    availability: num(r.availability),
    performance: num(r.performance),
    quality: num(r.quality),
    partsPerHour: num(r.partsPerHour),
    targetPartsPerHour: num(r.targetPartsPerHour),
    cycleTimeSeconds: num(r.cycleTimeSeconds),
    targetCycleTimeSeconds: num(r.targetCycleTimeSeconds),
    totalPartsProduced: num(r.totalPartsProduced),
    scrapParts: num(r.scrapParts),
    vibrationMmS: num(r.vibrationMmS),
    temperatureC: num(r.temperatureC),
    powerKw: num(r.powerKw),
    spindleRpm: num(r.spindleRpm),
    frequencyHz: num(r.frequencyHz),
    activeTicketCount: count(r.activeTicketCount),
    criticalTicketCount: count(r.criticalTicketCount),
    hasSlaBreach: r.hasSlaBreach === true,
    incidentCount: count(r.incidentCount),
    resolvedIncidentCount: count(r.resolvedIncidentCount),
    mttrMinutes: num(r.mttrMinutes),
    downtimeMinutes: num(r.downtimeMinutes),
    lastIncidentAt: text(r.lastIncidentAt),
    oldestOpenTicketAt: text(r.oldestOpenTicketAt),
    controllerCount: count(r.controllerCount),
    controllersOnline: count(r.controllersOnline),
    controllerLastSeenAt: text(r.controllerLastSeenAt)
  }
}

/** Normalizes the whole response; throws on a shape that is not a fleet station list. */
export function normalizeFleetStations(raw: unknown): FleetStationsPayload {
  if (!raw || typeof raw !== 'object' || !Array.isArray((raw as Record<string, unknown>).stations)) {
    throw new Error('Unexpected fleet station response shape')
  }
  const r = raw as Record<string, unknown>
  const stations: FleetStation[] = []
  for (const item of r.stations as unknown[]) {
    const station = normalizeFleetStation(item)
    if (station) stations.push(station)
  }
  return {
    meta: {
      generatedAt: text(r.generatedAt),
      windowDays: Math.max(1, count(r.windowDays) || 30),
      windowStart: text(r.windowStart),
      telemetryStaleAfterSeconds: count(r.telemetryStaleAfterSeconds),
      staleAfterMissedSamples: num(r.staleAfterMissedSamples) !== null && count(r.staleAfterMissedSamples) > 0 ? count(r.staleAfterMissedSamples) : null,
      staleFloorSeconds: num(r.staleFloorSeconds),
      staleCeilingSeconds: num(r.staleCeilingSeconds),
      controllerOnlineWindowSeconds: count(r.controllerOnlineWindowSeconds),
      criticalSlaHours: num(r.criticalSlaHours) ?? 0
    },
    stations
  }
}

// ───────────────────────────────────────────────────────────────── live state

export interface LiveStateMeta {
  label: string
  /** Tailwind background classes of the state's mark (dot / bar segment), light and dark. */
  dot: string
  description: string
}

export const LIVE_STATE_META: Record<LiveState, LiveStateMeta> = {
  RUNNING: { label: 'Running', dot: 'bg-emerald-600 dark:bg-emerald-500', description: 'PLC reports running; telemetry is fresh.' },
  IDLE: { label: 'Idle', dot: 'bg-sky-600 dark:bg-sky-500', description: 'PLC reports idle / stopped / waiting; telemetry is fresh.' },
  WARNING: { label: 'Warning', dot: 'bg-amber-600 dark:bg-amber-500', description: 'PLC reports a warning; telemetry is fresh.' },
  FAULTED: { label: 'Faulted', dot: 'bg-rose-600 dark:bg-rose-500', description: 'PLC reports a fault; telemetry is fresh.' },
  MAINTENANCE: { label: 'Maintenance', dot: 'bg-violet-600 dark:bg-violet-500', description: 'PLC reports maintenance / setup; telemetry is fresh.' },
  UNKNOWN: { label: 'Reporting', dot: 'bg-slate-400 dark:bg-slate-300', description: 'Telemetry is fresh but the PLC sends no machine state.' },
  STALE: { label: 'Stale', dot: 'bg-zinc-600 dark:bg-zinc-500', description: 'The station reported before, but it missed several samples in a row (its stale threshold).' },
  OFFLINE: { label: 'Offline', dot: 'bg-zinc-900 dark:bg-zinc-200', description: 'The agent of the station\'s controller disconnected (MQTT last will or shutdown) and no data arrived since.' },
  NO_DATA: { label: 'No data', dot: 'bg-transparent ring-1 ring-inset ring-muted-foreground/50', description: 'The station never reported telemetry.' }
}

/** Fresh telemetry: the station is reporting right now. */
export const isReporting = (state: LiveState): boolean => state !== 'STALE' && state !== 'OFFLINE' && state !== 'NO_DATA'

/** Severity rank used for sorting by state (most severe first). */
const STATE_RANK: Record<LiveState, number> = {
  FAULTED: 0, WARNING: 1, OFFLINE: 2, STALE: 3, MAINTENANCE: 4, IDLE: 5, UNKNOWN: 6, RUNNING: 7, NO_DATA: 8
}

/** Faulted / warning stations, SLA breaches, open critical tickets, telemetry that went stale or a disconnected controller. */
export const needsAttention = (s: FleetStation): boolean =>
  s.liveState === 'FAULTED' || s.liveState === 'WARNING' || s.liveState === 'STALE' || s.liveState === 'OFFLINE' ||
  s.hasSlaBreach || s.criticalTicketCount > 0

// ───────────────────────────────────────────────────────────────── columns & metrics

export type ColumnCategory = 'identity' | 'production' | 'condition' | 'maintenance' | 'connectivity'
/** live: latest telemetry snapshot · window: tickets created in the look-back window · now: current stored state. */
export type MetricScope = 'live' | 'window' | 'now'
export type MetricAggregation = 'mean' | 'sum' | 'max' | 'ratio'

export interface FleetContext {
  /** Server time of the response (ms); ages are measured against it, not the browser clock. */
  generatedAtMs: number
}

export interface RatioParts {
  numerator: (s: FleetStation) => number | null
  denominator: (s: FleetStation) => number | null
  /** 100 for a percentage, 1 for a weighted mean. */
  scale: number
  /** Render "numerator/denominator" instead of the scaled value. */
  showAsFraction?: boolean
}

export interface FleetColumn {
  key: string
  label: string
  /** Unit shown in the header and on KPI tiles; empty for counts and text. */
  unit: string
  kind: 'state' | 'text' | 'metric'
  category: ColumnCategory
  /** Where the figure comes from; shown in the column picker and the header tooltip. */
  description: string
  width: number
  scope?: MetricScope
  decimals?: number
  aggregate?: MetricAggregation
  /** null: neither direction is "better" (a setpoint or a plain count). */
  higherIsBetter?: boolean | null
  value?: (s: FleetStation, ctx: FleetContext) => number | null
  ratio?: RatioParts
  text?: (s: FleetStation) => string | null
  format?: (value: number) => string
  /** Station cell text when it differs from the formatted value; null renders the no-data mark. */
  cellText?: (s: FleetStation) => string | null
}

const pct = (part: number | null, whole: number | null): number | null =>
  part !== null && whole !== null && whole > 0 ? (part / whole) * 100 : null

/** Minutes as "45 min" / "27.1 h" / "3.2 d". */
export function formatDurationMinutes(minutes: number | null | undefined): string {
  if (minutes === null || minutes === undefined || !Number.isFinite(minutes)) return NO_DATA_MARK
  if (minutes < 120) return `${Math.round(minutes)} min`
  const hours = minutes / 60
  if (hours < 48) return `${(Math.round(hours * 10) / 10).toFixed(1)} h`
  return `${(Math.round((hours / 24) * 10) / 10).toFixed(1)} d`
}

/** Seconds as "4 s" / "3 min" / "2 h" / "5 d". */
export function formatAge(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) return NO_DATA_MARK
  if (seconds < 90) return `${Math.max(0, Math.round(seconds))} s`
  if (seconds < 5400) return `${Math.round(seconds / 60)} min`
  if (seconds < 172800) return `${Math.round(seconds / 3600)} h`
  return `${Math.round(seconds / 86400)} d`
}

export const NO_DATA_MARK = '—'

/** Seconds of a sample interval / threshold: "2.5 s", "12 s", "5 min". */
const formatSeconds = (seconds: number): string =>
  seconds < 10 ? `${Math.round(seconds * 10) / 10} s` : formatAge(seconds)

/** Explanation of the per-station rule (footer tooltip); undefined with a fixed threshold. */
export function staleRuleExplanation(meta: FleetStationsMeta | null | undefined): string | undefined {
  if (!meta?.staleAfterMissedSamples) return undefined
  const bounds = meta.staleFloorSeconds !== null && meta.staleCeilingSeconds !== null
    ? ` (at least ${formatSeconds(meta.staleFloorSeconds)}, at most ${formatSeconds(meta.staleCeilingSeconds)})`
    : ''
  return `A station is stale after ${meta.staleAfterMissedSamples} × its own sample interval, learned from the PLC blocks that arrive${bounds}. ` +
    `Stations whose interval is not learned yet: ${meta.telemetryStaleAfterSeconds} s. Offline: the controller's agent disconnected.`
}

/** The footer's stale rule: per-station missed samples, or the fixed threshold. */
export function staleRuleText(meta: FleetStationsMeta | null | undefined): string | null {
  if (!meta) return null
  if (meta.staleAfterMissedSamples) return `Stale after ${meta.staleAfterMissedSamples} missed samples (per station)`
  return `Stale after ${meta.telemetryStaleAfterSeconds} s without telemetry`
}

/** "Samples every 4 s · stale after 12 s." for a station's tooltip; empty when the backend sent neither. */
export function freshnessText(s: FleetStation): string {
  const parts: string[] = []
  if (s.sampleIntervalSeconds !== null && s.sampleIntervalSeconds > 0) parts.push(`Samples every ${formatSeconds(s.sampleIntervalSeconds)}`)
  if (s.staleAfterSeconds !== null && s.staleAfterSeconds > 0) parts.push(`${parts.length ? 'stale' : 'Stale'} after ${formatSeconds(s.staleAfterSeconds)} without data`)
  return parts.length ? `${parts.join(' · ')}.` : ''
}

const hoursSince = (iso: string | null, ctx: FleetContext): number | null => {
  if (!iso) return null
  const at = Date.parse(iso)
  return Number.isFinite(at) ? Math.max(0, (ctx.generatedAtMs - at) / 3_600_000) : null
}

export const FLEET_COLUMNS: readonly FleetColumn[] = [
  // identity
  { key: 'state', label: 'State', unit: '', kind: 'state', category: 'identity', width: 132,
    description: 'Machine state reported by the PLC while its telemetry is fresh; otherwise stale or no data.' },
  { key: 'line', label: 'Line', unit: '', kind: 'text', category: 'identity', width: 190,
    description: 'Line machine group of the station, else the legacy line text on the machine record.',
    text: s => s.lineName },
  { key: 'machineType', label: 'Machine type', unit: '', kind: 'text', category: 'identity', width: 150,
    description: 'Machine type on the machine record.', text: s => s.machineType },
  { key: 'organization', label: 'Organization', unit: '', kind: 'text', category: 'identity', width: 190,
    description: 'Organization that owns the machine record.', text: s => s.organizationName ?? s.organizationId },
  { key: 'lifecycle', label: 'Lifecycle', unit: '', kind: 'text', category: 'identity', width: 116,
    description: 'Lifecycle status of the machine record (not a live state).', text: s => lifecycleLabel(s.lifecycleStatus) },
  { key: 'telemetryAge', label: 'Last telemetry', unit: '', kind: 'metric', category: 'identity', width: 116, scope: 'live',
    decimals: 0, aggregate: 'max', higherIsBetter: false,
    description: 'Age of the last telemetry sample when the data was loaded. Groups show the oldest one.',
    value: s => s.telemetryAgeSeconds, format: v => formatAge(v) },

  // production (latest telemetry snapshot)
  { key: 'oee', label: 'OEE', unit: '%', kind: 'metric', category: 'production', width: 104, scope: 'live', decimals: 1,
    aggregate: 'mean', higherIsBetter: true,
    description: 'OEE as reported by the PLC, else availability × performance × quality. Groups show the mean of the stations reporting it.',
    value: s => s.oee },
  { key: 'availability', label: 'Availability', unit: '%', kind: 'metric', category: 'production', width: 104, scope: 'live', decimals: 1,
    aggregate: 'mean', higherIsBetter: true,
    description: 'Availability reported by the PLC. Groups show the mean of the stations reporting it.',
    value: s => s.availability },
  { key: 'performance', label: 'Performance', unit: '%', kind: 'metric', category: 'production', width: 104, scope: 'live', decimals: 1,
    aggregate: 'mean', higherIsBetter: true,
    description: 'Performance reported by the PLC. Groups show the mean of the stations reporting it.',
    value: s => s.performance },
  { key: 'quality', label: 'Quality', unit: '%', kind: 'metric', category: 'production', width: 104, scope: 'live', decimals: 1,
    aggregate: 'mean', higherIsBetter: true,
    description: 'Quality reported by the PLC, else first-pass yield from its part counters. Groups show the mean of the stations reporting it.',
    value: s => s.quality },
  { key: 'partsPerHour', label: 'Throughput', unit: 'parts/h', kind: 'metric', category: 'production', width: 112, scope: 'live', decimals: 0,
    aggregate: 'sum', higherIsBetter: true,
    description: 'Parts per hour reported by the PLC. Groups show the sum over their stations (not a line output rate).',
    value: s => s.partsPerHour },
  { key: 'targetPartsPerHour', label: 'Target throughput', unit: 'parts/h', kind: 'metric', category: 'production', width: 126, scope: 'live', decimals: 0,
    aggregate: 'sum', higherIsBetter: null,
    description: 'Target parts per hour reported by the PLC. Groups show the sum over their stations.',
    value: s => s.targetPartsPerHour },
  { key: 'attainment', label: 'Attainment', unit: '%', kind: 'metric', category: 'production', width: 108, scope: 'live', decimals: 0,
    aggregate: 'ratio', higherIsBetter: true,
    description: 'Throughput as a share of the target throughput, for stations reporting both. Groups divide the summed throughput by the summed target.',
    value: s => pct(s.partsPerHour, s.targetPartsPerHour),
    ratio: { numerator: s => (s.targetPartsPerHour !== null && s.targetPartsPerHour > 0 ? s.partsPerHour : null), denominator: s => (s.partsPerHour !== null ? s.targetPartsPerHour : null), scale: 100 } },
  { key: 'cycleTime', label: 'Cycle time', unit: 's', kind: 'metric', category: 'production', width: 104, scope: 'live', decimals: 1,
    aggregate: 'mean', higherIsBetter: false,
    description: 'Cycle time reported by the PLC. Groups show the mean of the stations reporting it.',
    value: s => s.cycleTimeSeconds },
  { key: 'targetCycleTime', label: 'Target cycle', unit: 's', kind: 'metric', category: 'production', width: 108, scope: 'live', decimals: 1,
    aggregate: 'mean', higherIsBetter: null,
    description: 'Target cycle time reported by the PLC. Groups show the mean of the stations reporting it.',
    value: s => s.targetCycleTimeSeconds },
  { key: 'cycleDeviation', label: 'Cycle vs target', unit: '%', kind: 'metric', category: 'production', width: 116, scope: 'live', decimals: 1,
    aggregate: 'mean', higherIsBetter: false,
    description: 'How much slower (+) or faster (−) the cycle is than its target, for stations reporting both. Groups show the mean.',
    value: s => (s.cycleTimeSeconds !== null && s.targetCycleTimeSeconds !== null && s.targetCycleTimeSeconds > 0
      ? ((s.cycleTimeSeconds - s.targetCycleTimeSeconds) / s.targetCycleTimeSeconds) * 100
      : null),
    format: v => `${v > 0 ? '+' : ''}${v.toFixed(1)}` },
  { key: 'partsProduced', label: 'Parts counter', unit: 'parts', kind: 'metric', category: 'production', width: 116, scope: 'live', decimals: 0,
    aggregate: 'sum', higherIsBetter: null,
    description: 'Total-parts counter of the PLC (since its last reset). Groups show the sum.',
    value: s => s.totalPartsProduced },
  { key: 'scrapRate', label: 'Scrap rate', unit: '%', kind: 'metric', category: 'production', width: 104, scope: 'live', decimals: 2,
    aggregate: 'ratio', higherIsBetter: false,
    description: 'Scrap counter as a share of the parts counter. Groups divide the summed scrap by the summed parts.',
    value: s => pct(s.scrapParts, s.totalPartsProduced),
    ratio: { numerator: s => (s.totalPartsProduced !== null && s.totalPartsProduced > 0 ? s.scrapParts : null), denominator: s => (s.scrapParts !== null ? s.totalPartsProduced : null), scale: 100 } },

  // condition (latest telemetry snapshot)
  { key: 'vibration', label: 'Vibration', unit: 'mm/s', kind: 'metric', category: 'condition', width: 104, scope: 'live', decimals: 2,
    aggregate: 'max', higherIsBetter: false,
    description: 'Vibration velocity reported by the PLC. Groups show the highest value.',
    value: s => s.vibrationMmS },
  { key: 'temperature', label: 'Temperature', unit: '°C', kind: 'metric', category: 'condition', width: 108, scope: 'live', decimals: 1,
    aggregate: 'max', higherIsBetter: false,
    description: 'Temperature reported by the PLC. Groups show the highest value.',
    value: s => s.temperatureC },
  { key: 'power', label: 'Power', unit: 'kW', kind: 'metric', category: 'condition', width: 96, scope: 'live', decimals: 2,
    aggregate: 'sum', higherIsBetter: null,
    description: 'Active power reported by the PLC. Groups show the sum.',
    value: s => s.powerKw },
  { key: 'spindleRpm', label: 'Spindle', unit: 'rpm', kind: 'metric', category: 'condition', width: 96, scope: 'live', decimals: 0,
    aggregate: 'mean', higherIsBetter: null,
    description: 'Spindle speed reported by the PLC. Groups show the mean of the stations reporting it.',
    value: s => s.spindleRpm },

  // maintenance (stored tickets)
  { key: 'openTickets', label: 'Open tickets', unit: '', kind: 'metric', category: 'maintenance', width: 108, scope: 'now', decimals: 0,
    aggregate: 'sum', higherIsBetter: false,
    description: 'Tickets of the station that are not resolved or closed. Groups show the sum.',
    value: s => s.activeTicketCount },
  { key: 'criticalTickets', label: 'Critical open', unit: '', kind: 'metric', category: 'maintenance', width: 108, scope: 'now', decimals: 0,
    aggregate: 'sum', higherIsBetter: false,
    description: 'Open tickets with priority Critical. Groups show the sum.',
    value: s => s.criticalTicketCount },
  { key: 'slaBreaches', label: 'SLA breached', unit: '', kind: 'metric', category: 'maintenance', width: 112, scope: 'now', decimals: 0,
    aggregate: 'sum', higherIsBetter: false,
    description: 'Stations with an open Critical ticket older than the SLA limit. Groups count such stations.',
    value: s => (s.hasSlaBreach ? 1 : 0) },
  { key: 'oldestOpenTicket', label: 'Oldest open', unit: '', kind: 'metric', category: 'maintenance', width: 108, scope: 'now', decimals: 0,
    aggregate: 'max', higherIsBetter: false,
    description: 'Age of the oldest open ticket. Groups show the oldest one.',
    value: (s, ctx) => hoursSince(s.oldestOpenTicketAt, ctx), format: v => formatDurationMinutes(v * 60) },
  { key: 'incidents', label: 'Incidents', unit: '', kind: 'metric', category: 'maintenance', width: 100, scope: 'window', decimals: 0,
    aggregate: 'sum', higherIsBetter: false,
    description: 'Tickets created for the station inside the look-back window (any status). Groups show the sum.',
    value: s => s.incidentCount },
  { key: 'mttr', label: 'MTTR', unit: '', kind: 'metric', category: 'maintenance', width: 100, scope: 'window', decimals: 0,
    aggregate: 'ratio', higherIsBetter: false,
    description: 'Mean time from ticket creation to resolution, for tickets created inside the look-back window. Groups weight each station by its resolved tickets.',
    value: s => s.mttrMinutes, format: v => formatDurationMinutes(v),
    ratio: { numerator: s => (s.mttrMinutes !== null && s.resolvedIncidentCount > 0 ? s.mttrMinutes * s.resolvedIncidentCount : null), denominator: s => (s.mttrMinutes !== null && s.resolvedIncidentCount > 0 ? s.resolvedIncidentCount : null), scale: 1 } },
  { key: 'downtime', label: 'Reported downtime', unit: '', kind: 'metric', category: 'maintenance', width: 132, scope: 'window', decimals: 0,
    aggregate: 'sum', higherIsBetter: false,
    description: 'Line-stop minutes recorded on the tickets created inside the look-back window; no data when no ticket recorded any. Groups show the sum.',
    value: s => s.downtimeMinutes, format: v => formatDurationMinutes(v) },

  // connectivity (client PCs bound to the station)
  { key: 'controllers', label: 'Controllers online', unit: '', kind: 'metric', category: 'connectivity', width: 130, scope: 'now', decimals: 0,
    aggregate: 'ratio', higherIsBetter: true,
    description: 'Station rows: client PCs bound to the station whose agent reported recently, out of all bound PCs (no data when none is bound). Groups: stations whose bound PCs are all online, out of the stations that have one — a PC shared by several stations is not counted twice as a PC.',
    value: s => pct(s.controllersOnline, s.controllerCount),
    cellText: s => (s.controllerCount > 0 ? `${s.controllersOnline}/${s.controllerCount}` : null),
    ratio: { numerator: s => (s.controllerCount > 0 ? (s.controllersOnline >= s.controllerCount ? 1 : 0) : null), denominator: s => (s.controllerCount > 0 ? 1 : null), scale: 100, showAsFraction: true } }
]

const COLUMN_BY_KEY: Record<string, FleetColumn> = Object.fromEntries(FLEET_COLUMNS.map(c => [c.key, c]))

export const getColumn = (key: string): FleetColumn | undefined => COLUMN_BY_KEY[key]

/** Columns that carry a number and can be aggregated (KPI tiles, roll-ups, KPI widgets). */
export const FLEET_METRICS: readonly FleetColumn[] = FLEET_COLUMNS.filter(c => c.kind === 'metric')

export const CATEGORY_LABELS: Record<ColumnCategory, string> = {
  identity: 'Station',
  production: 'Production (latest telemetry)',
  condition: 'Condition (latest telemetry)',
  maintenance: 'Maintenance (tickets)',
  connectivity: 'Connectivity'
}

export const SCOPE_LABELS: Record<MetricScope, string> = {
  live: 'latest telemetry',
  window: 'look-back window',
  now: 'current'
}

export function lifecycleLabel(status: string): string {
  switch (status) {
    case 'active': return 'Active'
    case 'inactive': return 'Inactive'
    case 'maintenance': return 'In maintenance'
    case 'decommissioned': return 'Decommissioned'
    case 'dismantled': return 'Dismantled'
    default: return status ? status.charAt(0).toUpperCase() + status.slice(1) : 'Unknown'
  }
}

/** A number in the column's own format; the no-data mark for null. */
export function formatColumnValue(value: number | null | undefined, column: FleetColumn): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return NO_DATA_MARK
  if (column.format) return column.format(value)
  const decimals = column.decimals ?? 0
  return value.toLocaleString('en-US', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
}

/** Cell text of a station for a column (numbers formatted, missing values as the no-data mark). */
export function stationCellText(s: FleetStation, column: FleetColumn, ctx: FleetContext): string {
  if (column.kind === 'state') return LIVE_STATE_META[s.liveState].label
  if (column.kind === 'text') return column.text?.(s) ?? NO_DATA_MARK
  if (column.cellText) return column.cellText(s) ?? NO_DATA_MARK
  return formatColumnValue(column.value?.(s, ctx) ?? null, column)
}

// ───────────────────────────────────────────────────────────────── roll-ups

export interface MetricRollup {
  /** Aggregated value; null when no station contributes. */
  value: number | null
  /** Stations that contributed a value. */
  n: number
  numerator?: number
  denominator?: number
}

export interface FleetRollup {
  stationCount: number
  states: Record<LiveState, number>
  /** Stations with fresh telemetry. */
  reporting: number
  attention: number
  metrics: Record<string, MetricRollup>
}

const emptyStates = (): Record<LiveState, number> => ({
  RUNNING: 0, IDLE: 0, WARNING: 0, FAULTED: 0, MAINTENANCE: 0, UNKNOWN: 0, STALE: 0, OFFLINE: 0, NO_DATA: 0
})

/** Aggregates one metric over stations. Stations without a value are left out, never counted as 0. */
export function rollUpMetric(rows: readonly FleetStation[], column: FleetColumn, ctx: FleetContext): MetricRollup {
  if (column.aggregate === 'ratio' && column.ratio) {
    let numerator = 0
    let denominator = 0
    let n = 0
    for (const s of rows) {
      const a = column.ratio.numerator(s)
      const b = column.ratio.denominator(s)
      if (a === null || b === null) continue
      numerator += a
      denominator += b
      n++
    }
    return { value: n > 0 && denominator > 0 ? (numerator / denominator) * column.ratio.scale : null, n, numerator, denominator }
  }

  let n = 0
  let sum = 0
  let max = -Infinity
  for (const s of rows) {
    const v = column.value?.(s, ctx) ?? null
    if (v === null || !Number.isFinite(v)) continue
    n++
    sum += v
    if (v > max) max = v
  }
  if (n === 0) return { value: null, n: 0 }
  if (column.aggregate === 'sum') return { value: sum, n }
  if (column.aggregate === 'max') return { value: max, n }
  return { value: sum / n, n }
}

export function rollUp(rows: readonly FleetStation[], ctx: FleetContext, columns: readonly FleetColumn[] = FLEET_METRICS): FleetRollup {
  const states = emptyStates()
  let attention = 0
  for (const s of rows) {
    states[s.liveState]++
    if (needsAttention(s)) attention++
  }
  const metrics: Record<string, MetricRollup> = {}
  for (const column of columns) {
    if (column.kind === 'metric') metrics[column.key] = rollUpMetric(rows, column, ctx)
  }
  return {
    stationCount: rows.length,
    states,
    reporting: rows.length - states.STALE - states.OFFLINE - states.NO_DATA,
    attention,
    metrics
  }
}

/** Text of a roll-up cell: the aggregate in the column's format, or the no-data mark. */
export function rollupCellText(rollup: MetricRollup | undefined, column: FleetColumn): string {
  if (!rollup || rollup.value === null) return NO_DATA_MARK
  if (column.ratio?.showAsFraction && rollup.numerator !== undefined && rollup.denominator !== undefined) {
    return `${rollup.numerator}/${rollup.denominator}`
  }
  return formatColumnValue(rollup.value, column)
}

/** "Mean of 12 of 15 stations" style explanation of a roll-up, for tooltips and screen readers. */
export function rollupExplanation(rollup: MetricRollup | undefined, column: FleetColumn, stationCount: number): string {
  if (!rollup || rollup.n === 0) return `${column.label}: no station in this group reports it.`
  const how = column.aggregate === 'sum' ? 'Sum'
    : column.aggregate === 'max' ? 'Highest'
      : column.aggregate === 'ratio' ? (column.ratio?.scale === 1 ? 'Weighted mean' : 'Ratio of sums')
        : 'Mean'
  return `${column.label}: ${how.toLowerCase()} over ${rollup.n} of ${stationCount} station${stationCount === 1 ? '' : 's'}.`
}

export const AGGREGATION_PREFIX: Record<MetricAggregation, string> = {
  mean: '⌀',
  sum: 'Σ',
  max: 'max',
  ratio: ''
}

// ───────────────────────────────────────────────────────────────── grouping

export type GroupDimension =
  | 'none' | 'line' | 'subLine' | 'project' | 'businessUnit' | 'machineType' | 'organization' | 'liveState' | 'lifecycle'

export const UNASSIGNED_KEY = '__unassigned__'

export interface DimensionDef {
  key: GroupDimension
  label: string
  /** Label of the group for stations without a value in this dimension. */
  unassignedLabel: string
  /** Stable key of the station's group, null when the station has none. */
  groupKey: (s: FleetStation) => string | null
  groupLabel: (s: FleetStation) => string | null
}

const keyOf = (id: string | null, name: string | null): string | null =>
  id ? `id:${id.toLowerCase()}` : name ? `name:${name.toLowerCase()}` : null

export const GROUP_DIMENSIONS: readonly DimensionDef[] = [
  { key: 'none', label: 'No grouping', unassignedLabel: 'All stations', groupKey: () => null, groupLabel: () => null },
  { key: 'line', label: 'Line', unassignedLabel: 'No line assigned', groupKey: s => keyOf(s.lineId, s.lineName), groupLabel: s => s.lineName },
  { key: 'subLine', label: 'Sub-line', unassignedLabel: 'No sub-line assigned', groupKey: s => keyOf(s.subLineId, s.subLineName), groupLabel: s => s.subLineName },
  { key: 'project', label: 'Project', unassignedLabel: 'No project assigned', groupKey: s => keyOf(s.projectId, s.projectName), groupLabel: s => s.projectName },
  { key: 'businessUnit', label: 'Business unit', unassignedLabel: 'No business unit assigned', groupKey: s => keyOf(s.businessUnitId, s.businessUnitName), groupLabel: s => s.businessUnitName },
  { key: 'machineType', label: 'Machine type', unassignedLabel: 'Type not set', groupKey: s => keyOf(null, s.machineType), groupLabel: s => s.machineType },
  { key: 'organization', label: 'Organization', unassignedLabel: 'No organization', groupKey: s => keyOf(s.organizationId, null), groupLabel: s => s.organizationName ?? s.organizationId },
  { key: 'liveState', label: 'Live state', unassignedLabel: 'No data', groupKey: s => `state:${s.liveState}`, groupLabel: s => LIVE_STATE_META[s.liveState].label },
  { key: 'lifecycle', label: 'Lifecycle status', unassignedLabel: 'Unknown', groupKey: s => keyOf(null, s.lifecycleStatus), groupLabel: s => lifecycleLabel(s.lifecycleStatus) }
]

const DIMENSION_BY_KEY: Record<string, DimensionDef> = Object.fromEntries(GROUP_DIMENSIONS.map(d => [d.key, d]))

export const getDimension = (key: string): DimensionDef => DIMENSION_BY_KEY[key] ?? DIMENSION_BY_KEY.none!

export interface FleetGroup {
  key: string
  label: string
  isUnassigned: boolean
  rows: FleetStation[]
  rollup: FleetRollup
}

const natural = (a: string, b: string): number => a.localeCompare(b, 'en', { numeric: true, sensitivity: 'base' })

/**
 * Splits stations into the groups of a dimension (input order is kept inside each group), with a
 * roll-up per group. Stations without a value form one "unassigned" group, listed last.
 */
export function groupStations(
  rows: readonly FleetStation[],
  dimension: GroupDimension,
  ctx: FleetContext,
  columns: readonly FleetColumn[] = FLEET_METRICS
): FleetGroup[] {
  const def = getDimension(dimension)
  if (def.key === 'none') {
    return [{ key: UNASSIGNED_KEY, label: def.unassignedLabel, isUnassigned: false, rows: [...rows], rollup: rollUp(rows, ctx, columns) }]
  }

  const buckets = new Map<string, { label: string; rows: FleetStation[] }>()
  for (const s of rows) {
    const key = def.groupKey(s) ?? UNASSIGNED_KEY
    let bucket = buckets.get(key)
    if (!bucket) {
      bucket = { label: key === UNASSIGNED_KEY ? def.unassignedLabel : (def.groupLabel(s) ?? def.unassignedLabel), rows: [] }
      buckets.set(key, bucket)
    }
    bucket.rows.push(s)
  }

  const groups = Array.from(buckets, ([key, bucket]) => ({
    key,
    label: bucket.label,
    isUnassigned: key === UNASSIGNED_KEY,
    rows: bucket.rows,
    rollup: rollUp(bucket.rows, ctx, columns)
  }))

  if (def.key === 'liveState') {
    const rank = (g: FleetGroup) => STATE_RANK[g.key.slice('state:'.length) as LiveState] ?? 99
    return groups.sort((a, b) => rank(a) - rank(b))
  }
  return groups.sort((a, b) => Number(a.isUnassigned) - Number(b.isUnassigned) || natural(a.label, b.label))
}

// ───────────────────────────────────────────────────────────────── sorting

export interface FleetSort {
  /** 'name' or a column key. */
  key: string
  direction: 'asc' | 'desc'
}

type SortValue = number | string | null

const sortValue = (s: FleetStation, key: string, ctx: FleetContext): SortValue => {
  if (key === 'name') return s.name
  const column = getColumn(key)
  if (!column) return null
  if (column.kind === 'state') return STATE_RANK[s.liveState]
  if (column.kind === 'text') return column.text?.(s) ?? null
  return column.value?.(s, ctx) ?? null
}

const compareValues = (a: SortValue, b: SortValue, direction: 'asc' | 'desc'): number => {
  // Stations without a value always go last, whatever the direction.
  if (a === null && b === null) return 0
  if (a === null) return 1
  if (b === null) return -1
  const result = typeof a === 'number' && typeof b === 'number' ? a - b : natural(String(a), String(b))
  return direction === 'asc' ? result : -result
}

/** Sorted copy; ties keep the station name order so the list does not jump between refreshes. */
export function sortStations(rows: readonly FleetStation[], sort: FleetSort, ctx: FleetContext): FleetStation[] {
  return [...rows].sort((a, b) =>
    compareValues(sortValue(a, sort.key, ctx), sortValue(b, sort.key, ctx), sort.direction) ||
    natural(a.name, b.name) ||
    a.stationId.localeCompare(b.stationId)
  )
}

export type GroupOrder = 'label' | 'metric'

/**
 * Orders groups by label (unassigned last) or by the roll-up of the sorted column, in the same
 * direction as the rows. Sorting by a column without a roll-up (name, text) falls back to the label.
 */
export function orderGroups(groups: readonly FleetGroup[], order: GroupOrder, sort: FleetSort): FleetGroup[] {
  const column = getColumn(sort.key)
  if (order !== 'metric' || !column || column.kind !== 'metric') return [...groups]
  return [...groups].sort((a, b) =>
    compareValues(a.rollup.metrics[column.key]?.value ?? null, b.rollup.metrics[column.key]?.value ?? null, sort.direction) ||
    Number(a.isUnassigned) - Number(b.isUnassigned) ||
    natural(a.label, b.label)
  )
}

// ───────────────────────────────────────────────────────────────── filters

export interface FleetFilters {
  search: string
  /** Group keys of the line dimension (UNASSIGNED_KEY for stations without a line). */
  lines: string[]
  organizations: string[]
  machineTypes: string[]
  states: LiveState[]
  attentionOnly: boolean
}

export const EMPTY_FILTERS: FleetFilters = {
  search: '', lines: [], organizations: [], machineTypes: [], states: [], attentionOnly: false
}

const dimensionKey = (s: FleetStation, dimension: GroupDimension): string =>
  getDimension(dimension).groupKey(s) ?? UNASSIGNED_KEY

export function filterStations(rows: readonly FleetStation[], filters: FleetFilters): FleetStation[] {
  const q = filters.search.trim().toLowerCase()
  const lines = new Set(filters.lines)
  const organizations = new Set(filters.organizations)
  const machineTypes = new Set(filters.machineTypes)
  const states = new Set<string>(filters.states)

  return rows.filter(s => {
    if (lines.size > 0 && !lines.has(dimensionKey(s, 'line'))) return false
    if (organizations.size > 0 && !organizations.has(dimensionKey(s, 'organization'))) return false
    if (machineTypes.size > 0 && !machineTypes.has(dimensionKey(s, 'machineType'))) return false
    if (states.size > 0 && !states.has(s.liveState)) return false
    if (filters.attentionOnly && !needsAttention(s)) return false
    if (!q) return true
    return [s.name, s.machineName, s.customIdentifier, s.handle, s.lineName, s.subLineName, s.machineType, s.organizationName]
      .some(field => !!field && field.toLowerCase().includes(q))
  })
}

export const activeFilterCount = (filters: FleetFilters): number =>
  (filters.search.trim() ? 1 : 0) + (filters.lines.length ? 1 : 0) + (filters.organizations.length ? 1 : 0) +
  (filters.machineTypes.length ? 1 : 0) + (filters.states.length ? 1 : 0) + (filters.attentionOnly ? 1 : 0)

export interface FilterOption {
  key: string
  label: string
  count: number
}

/** The values a dimension takes in the loaded stations, with station counts (unassigned last). */
export function dimensionOptions(rows: readonly FleetStation[], dimension: GroupDimension): FilterOption[] {
  const def = getDimension(dimension)
  const options = new Map<string, FilterOption>()
  for (const s of rows) {
    const key = def.groupKey(s) ?? UNASSIGNED_KEY
    const existing = options.get(key)
    if (existing) existing.count++
    else options.set(key, { key, label: key === UNASSIGNED_KEY ? def.unassignedLabel : (def.groupLabel(s) ?? def.unassignedLabel), count: 1 })
  }
  return Array.from(options.values()).sort((a, b) =>
    Number(a.key === UNASSIGNED_KEY) - Number(b.key === UNASSIGNED_KEY) || natural(a.label, b.label)
  )
}

/** Live states present in the loaded stations, most severe first. */
export function stateOptions(rows: readonly FleetStation[]): FilterOption[] {
  const counts = emptyStates()
  for (const s of rows) counts[s.liveState]++
  return LIVE_STATES.filter(state => counts[state] > 0).map(state => ({ key: state, label: LIVE_STATE_META[state].label, count: counts[state] }))
}

// ───────────────────────────────────────────────────────────────── persisted view

export const WINDOW_OPTIONS: readonly { days: number; label: string }[] = [
  { days: 1, label: '24 h' },
  { days: 7, label: '7 d' },
  { days: 30, label: '30 d' },
  { days: 90, label: '90 d' },
  { days: 365, label: '1 y' }
]

export const AUTO_REFRESH_OPTIONS: readonly { seconds: number; label: string }[] = [
  { seconds: 0, label: 'Off' },
  { seconds: 10, label: '10 s' },
  { seconds: 30, label: '30 s' },
  { seconds: 60, label: '1 min' }
]

export type Density = 'comfortable' | 'compact'

export interface FleetViewState {
  version: 1
  groupBy: GroupDimension
  groupOrder: GroupOrder
  /** Visible column keys, in display order. */
  columns: string[]
  sort: FleetSort
  filters: FleetFilters
  windowDays: number
  /** Collapsed group keys per grouping dimension. */
  collapsed: Record<string, string[]>
  density: Density
  autoRefreshSeconds: number
}

export interface ColumnPreset {
  key: string
  label: string
  columns: string[]
}

export const COLUMN_PRESETS: readonly ColumnPreset[] = [
  { key: 'overview', label: 'Overview', columns: ['state', 'oee', 'availability', 'performance', 'quality', 'attainment', 'openTickets', 'incidents', 'controllers'] },
  { key: 'production', label: 'Production', columns: ['state', 'oee', 'availability', 'performance', 'quality', 'partsPerHour', 'targetPartsPerHour', 'attainment', 'cycleTime', 'cycleDeviation', 'scrapRate'] },
  { key: 'condition', label: 'Condition', columns: ['state', 'telemetryAge', 'vibration', 'temperature', 'power', 'spindleRpm', 'controllers'] },
  { key: 'maintenance', label: 'Maintenance', columns: ['state', 'openTickets', 'criticalTickets', 'slaBreaches', 'oldestOpenTicket', 'incidents', 'mttr', 'downtime'] }
]

export const DEFAULT_VIEW: FleetViewState = {
  version: 1,
  groupBy: 'line',
  groupOrder: 'label',
  columns: [...COLUMN_PRESETS[0]!.columns],
  sort: { key: 'name', direction: 'asc' },
  filters: { ...EMPTY_FILTERS },
  windowDays: 30,
  collapsed: {},
  density: 'comfortable',
  autoRefreshSeconds: 30
}

export const VIEW_STORAGE_KEY = 'heimdall_fleet_analytics_view_v1'

const stringArray = (value: unknown, max = 500): string[] =>
  Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string' && v.length > 0 && v.length <= 300).slice(0, max) : []

/** A valid view from anything that was stored: unknown keys are dropped, missing parts take the default. */
export function sanitizeViewState(raw: unknown): FleetViewState {
  const base: FleetViewState = { ...DEFAULT_VIEW, columns: [...DEFAULT_VIEW.columns], filters: { ...EMPTY_FILTERS }, collapsed: {} }
  if (!raw || typeof raw !== 'object') return base
  const r = raw as Record<string, unknown>

  if (typeof r.groupBy === 'string' && DIMENSION_BY_KEY[r.groupBy]) base.groupBy = r.groupBy as GroupDimension
  if (r.groupOrder === 'label' || r.groupOrder === 'metric') base.groupOrder = r.groupOrder

  if (Array.isArray(r.columns)) {
    const seen = new Set<string>()
    base.columns = stringArray(r.columns).filter(key => !!COLUMN_BY_KEY[key] && !seen.has(key) && !!seen.add(key))
  }

  const sort = r.sort as Record<string, unknown> | undefined
  if (sort && typeof sort === 'object' && typeof sort.key === 'string' && (sort.key === 'name' || COLUMN_BY_KEY[sort.key])) {
    base.sort = { key: sort.key, direction: sort.direction === 'desc' ? 'desc' : 'asc' }
  }

  const filters = r.filters as Record<string, unknown> | undefined
  if (filters && typeof filters === 'object') {
    base.filters = {
      search: typeof filters.search === 'string' ? filters.search.slice(0, 200) : '',
      lines: stringArray(filters.lines),
      organizations: stringArray(filters.organizations),
      machineTypes: stringArray(filters.machineTypes),
      states: stringArray(filters.states).filter((s): s is LiveState => (LIVE_STATES as readonly string[]).includes(s)),
      attentionOnly: filters.attentionOnly === true
    }
  }

  const windowDays = num(r.windowDays)
  if (windowDays !== null && WINDOW_OPTIONS.some(o => o.days === windowDays)) base.windowDays = windowDays

  if (r.collapsed && typeof r.collapsed === 'object') {
    for (const [dimension, keys] of Object.entries(r.collapsed as Record<string, unknown>)) {
      if (DIMENSION_BY_KEY[dimension]) base.collapsed[dimension] = stringArray(keys)
    }
  }

  if (r.density === 'compact' || r.density === 'comfortable') base.density = r.density
  const autoRefresh = num(r.autoRefreshSeconds)
  if (autoRefresh !== null && AUTO_REFRESH_OPTIONS.some(o => o.seconds === autoRefresh)) base.autoRefreshSeconds = autoRefresh

  return base
}

type StorageLike = Pick<Storage, 'getItem' | 'setItem'>

const browserStorage = (): StorageLike | null => {
  try {
    return typeof window !== 'undefined' && window.localStorage ? window.localStorage : null
  } catch {
    return null // storage blocked (private mode, sandboxed frame)
  }
}

/** The view stored in this browser, or the default when nothing usable is stored. */
export function loadViewState(storage: StorageLike | null = browserStorage()): FleetViewState {
  try {
    const stored = storage?.getItem(VIEW_STORAGE_KEY)
    return sanitizeViewState(stored ? JSON.parse(stored) : null)
  } catch {
    return sanitizeViewState(null)
  }
}

/** Stores the view for this browser; false when storage is unavailable or full. */
export function saveViewState(view: FleetViewState, storage: StorageLike | null = browserStorage()): boolean {
  try {
    if (!storage) return false
    storage.setItem(VIEW_STORAGE_KEY, JSON.stringify(view))
    return true
  } catch {
    return false
  }
}

// ───────────────────────────────────────────────────────────────── virtual window

export interface VirtualSectionInput {
  /** Rows the section would render when expanded. */
  rowCount: number
  collapsed: boolean
}

export interface VirtualSection {
  /** Offset of the section (its header) from the top of the list. */
  top: number
  /** Header plus all rows (0 rows when collapsed). */
  height: number
  /** First rendered row index (inclusive) and last (exclusive); equal when none is in view. */
  start: number
  end: number
  /** Space taken by the rows before `start` and after `end`. */
  padTop: number
  padBottom: number
}

export interface VirtualWindow {
  totalHeight: number
  sections: VirtualSection[]
}

export interface VirtualWindowOptions {
  scrollTop: number
  /** Height of the scroll container; unknown (0) renders a generous default window. */
  viewportHeight: number
  rowHeight: number
  headerHeight: number
  /** Extra rows rendered above and below the viewport. */
  overscan?: number
}

/**
 * Which rows of each (fixed row height) section intersect the viewport. Sections keep their full
 * height through padding, so sticky group headers and the scroll bar behave as if every row
 * were rendered.
 */
export function computeVirtualWindow(sections: readonly VirtualSectionInput[], options: VirtualWindowOptions): VirtualWindow {
  const rowHeight = Math.max(1, options.rowHeight)
  const headerHeight = Math.max(0, options.headerHeight)
  const overscan = Math.max(0, options.overscan ?? 6)
  const viewportHeight = options.viewportHeight > 0 ? options.viewportHeight : 900
  const from = Math.max(0, options.scrollTop) - overscan * rowHeight
  const to = Math.max(0, options.scrollTop) + viewportHeight + overscan * rowHeight

  const result: VirtualSection[] = []
  let top = 0
  for (const section of sections) {
    const rows = section.collapsed ? 0 : Math.max(0, section.rowCount)
    const rowsTop = top + headerHeight
    const height = headerHeight + rows * rowHeight
    let start = 0
    let end = 0
    if (rows > 0 && rowsTop < to && rowsTop + rows * rowHeight > from) {
      start = Math.min(rows, Math.max(0, Math.floor((from - rowsTop) / rowHeight)))
      end = Math.min(rows, Math.max(start, Math.ceil((to - rowsTop) / rowHeight)))
    }
    result.push({ top, height, start, end, padTop: start * rowHeight, padBottom: (rows - end) * rowHeight })
    top += height
  }
  return { totalHeight: top, sections: result }
}

// ───────────────────────────────────────────────────────────────── station links

/** Entity binding of the global right-click menu (`v-ctx`) for a station row. */
export const stationContextData = (s: FleetStation) => ({
  entityType: 'machine' as const,
  entityId: s.stationId,
  entityName: s.name,
  machineId: s.stationId,
  machineName: s.machineName,
  handle: s.pinnedObjectHandle ?? undefined
})

export const stationMachineLink = (s: FleetStation) => ({
  path: '/dashboard/machines',
  query: { search: s.customIdentifier ?? s.machineName, tree: s.stationId }
})

export const stationTelemetryLink = (s: FleetStation) => ({ path: '/dashboard/telemetry', query: { station: s.stationId } })

export const stationTicketsLink = (s: FleetStation) => ({ path: '/dashboard/tickets', query: { stationId: s.stationId } })
