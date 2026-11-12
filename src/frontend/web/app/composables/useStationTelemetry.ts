import { ref, shallowRef, shallowReactive, computed, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import type { MaintenanceEvent, MaintenanceTicket } from '~/types/maintenance'

/**
 * Station-level production KPIs.
 *
 * Shape mirrors the backend `StationKpiDto` (GET /api/v1/analytics/station-kpis, camelCase JSON)
 * and the `StationTelemetryBroadcastDto` pushed over SignalR (`StationTelemetryReceived`).
 *
 * Data path: backend ingestion → backend API / SignalR hub → BFF proxy (`/api/telemetry/station-kpis`)
 * → this composable → UI. Nothing in here fabricates values: a station without data resolves to
 * `null` and the UI renders an explicit "no telemetry" state.
 */
export interface StationTelemetryKpi {
  /** Backend machine id. */
  stationId: string
  /** Floor-plan (DXF) handle, else the machine's custom identifier. */
  handle: string
  name: string
  lineId: string
  lineName: string
  // Production values: null until the station's PLC has reported them.
  oee: number | null
  availability: number | null
  performance: number | null
  quality: number | null
  yield: number | null
  partsPerHour: number | null
  targetPartsPerHour: number | null
  cycleTimeSeconds: number | null
  targetCycleTimeSeconds: number | null
  totalPartsProduced: number | null
  scrapParts: number | null
  // Last reported machine dynamics (REST snapshot only; live values arrive as oscillation samples).
  vibrationMmS: number | null
  spindleRpm: number | null
  cycleTimeMs: number | null
  powerKw: number | null
  temperatureC: number | null
  frequencyHz: number | null
  /** UNKNOWN: no telemetry-reported state. */
  operationalStatus: 'RUNNING' | 'IDLE' | 'WARNING' | 'FAULTED' | 'MAINTENANCE' | 'UNKNOWN'
  activeTicketCount: number
  criticalTicketCount: number
  hasSlaBreach: boolean
  /** ISO timestamp of the last ingested telemetry for this station, null if never. */
  lastTelemetryAt: string | null
}

export interface StationSlaSummary {
  totalTickets: number
  openTickets: number
  criticalTickets: number
  breachedTickets: number
  warningTickets: number
  slaHealth: 'breached' | 'warning' | 'healthy' | 'none'
  earliestDueText: string | null
  tickets: MaintenanceTicket[]
}

/**
 * Live machine dynamics for one station. Mirrors `StationOscillationBroadcastDto`
 * (SignalR `StationOscillationReceived`); `history` is a client-side ring buffer of the
 * last {@link OSCILLATION_HISTORY_LIMIT} received vibration samples.
 */
export interface StationOscillationData {
  stationId: string
  /** Floor-plan handle the sample was published for (also used as a lookup key). */
  handle: string
  vibrationMmS: number
  // Null when the PLC does not report the quantity.
  spindleRpm: number | null
  cycleTimeMs: number | null
  powerKw: number | null
  temperatureC: number | null
  frequencyHz: number | null
  /** Epoch milliseconds of the sample as reported by the backend (receive time if absent). */
  timestamp: number
  history: number[]
}

export const OSCILLATION_HISTORY_LIMIT = 24

const OPERATIONAL_STATUSES: ReadonlyArray<StationTelemetryKpi['operationalStatus']> = [
  'RUNNING', 'IDLE', 'WARNING', 'FAULTED', 'MAINTENANCE', 'UNKNOWN'
]

/** Finite number, else the fallback (null / undefined / garbage never become 0). */
function toNumber(value: unknown, fallback: number): number
function toNumber(value: unknown, fallback: number | null): number | null
function toNumber(value: unknown, fallback: number | null): number | null {
  const n = typeof value === 'string' && value.trim() !== '' ? Number(value) : value
  return typeof n === 'number' && Number.isFinite(n) ? n : fallback
}

const toText = (value: unknown, fallback: string): string =>
  typeof value === 'string' && value.length > 0 ? value : fallback

const toEpochMs = (value: unknown): number => {
  if (typeof value === 'number' && Number.isFinite(value) && value > 0) return value
  if (typeof value === 'string' && value) {
    const parsed = Date.parse(value)
    if (Number.isFinite(parsed)) return parsed
  }
  return Date.now()
}

const stationKey = (handleOrId: string | null | undefined) => (handleOrId || '').trim().toUpperCase()

/**
 * Normalizes a backend KPI payload (REST `StationKpiDto` or partial SignalR
 * `StationTelemetryBroadcastDto`) onto the TS shape. Missing / null fields keep the previous
 * value (partial live updates) or stay null. Returns null when the payload has no identity.
 */
export function normalizeStationKpi(raw: unknown, previous?: StationTelemetryKpi | null): StationTelemetryKpi | null {
  if (!raw || typeof raw !== 'object') return null
  const r = raw as Record<string, unknown>
  const handle = toText(r.handle, previous?.handle || toText(r.stationId, ''))
  const stationId = toText(r.stationId, previous?.stationId || handle)
  if (!handle && !stationId) return null

  const statusRaw = typeof r.operationalStatus === 'string' ? r.operationalStatus.toUpperCase() : ''
  const operationalStatus = (OPERATIONAL_STATUSES as readonly string[]).includes(statusRaw)
    ? (statusRaw as StationTelemetryKpi['operationalStatus'])
    : (previous?.operationalStatus ?? 'UNKNOWN')

  const p = previous
  return {
    stationId: stationId || handle,
    handle: handle || stationId,
    name: toText(r.name, p?.name || handle || stationId),
    lineId: toText(r.lineId, p?.lineId || ''),
    lineName: toText(r.lineName, p?.lineName || ''),
    oee: toNumber(r.oee, p?.oee ?? null),
    availability: toNumber(r.availability, p?.availability ?? null),
    performance: toNumber(r.performance, p?.performance ?? null),
    quality: toNumber(r.quality, p?.quality ?? null),
    yield: toNumber(r.yield, p?.yield ?? null),
    partsPerHour: toNumber(r.partsPerHour, p?.partsPerHour ?? null),
    targetPartsPerHour: toNumber(r.targetPartsPerHour, p?.targetPartsPerHour ?? null),
    cycleTimeSeconds: toNumber(r.cycleTimeSeconds, p?.cycleTimeSeconds ?? null),
    targetCycleTimeSeconds: toNumber(r.targetCycleTimeSeconds, p?.targetCycleTimeSeconds ?? null),
    totalPartsProduced: toNumber(r.totalPartsProduced, p?.totalPartsProduced ?? null),
    scrapParts: toNumber(r.scrapParts, p?.scrapParts ?? null),
    vibrationMmS: toNumber(r.vibrationMmS, p?.vibrationMmS ?? null),
    spindleRpm: toNumber(r.spindleRpm, p?.spindleRpm ?? null),
    cycleTimeMs: toNumber(r.cycleTimeMs, p?.cycleTimeMs ?? null),
    powerKw: toNumber(r.powerKw, p?.powerKw ?? null),
    temperatureC: toNumber(r.temperatureC, p?.temperatureC ?? null),
    frequencyHz: toNumber(r.frequencyHz, p?.frequencyHz ?? null),
    operationalStatus,
    activeTicketCount: toNumber(r.activeTicketCount, p?.activeTicketCount ?? 0),
    criticalTicketCount: toNumber(r.criticalTicketCount, p?.criticalTicketCount ?? 0),
    hasSlaBreach: typeof r.hasSlaBreach === 'boolean' ? r.hasSlaBreach : (p?.hasSlaBreach ?? false),
    lastTelemetryAt: toText(r.lastTelemetryAt, p?.lastTelemetryAt ?? '') || null
  }
}

/**
 * Normalizes an oscillation sample and appends its vibration value to the bounded history.
 */
export function normalizeStationOscillation(
  raw: unknown,
  fallbackStationId: string,
  previous?: StationOscillationData | null
): StationOscillationData | null {
  if (!raw || typeof raw !== 'object') return null
  const r = raw as Record<string, unknown>
  const vibration = toNumber(r.vibrationMmS, Number.NaN)
  const p = previous
  const vibrationMmS = Number.isFinite(vibration) ? vibration : (p?.vibrationMmS ?? Number.NaN)
  // A sample without a vibration reading and no prior state carries nothing to display.
  if (!Number.isFinite(vibrationMmS)) return null

  const history = Number.isFinite(vibration)
    ? [...(p?.history ?? []), vibration].slice(-OSCILLATION_HISTORY_LIMIT)
    : (p?.history ?? [])

  return {
    stationId: toText(r.stationId, p?.stationId || fallbackStationId),
    handle: toText(r.handle, p?.handle || ''),
    vibrationMmS,
    spindleRpm: toNumber(r.spindleRpm, p?.spindleRpm ?? null),
    cycleTimeMs: toNumber(r.cycleTimeMs, p?.cycleTimeMs ?? null),
    powerKw: toNumber(r.powerKw, p?.powerKw ?? null),
    temperatureC: toNumber(r.temperatureC, p?.temperatureC ?? null),
    frequencyHz: toNumber(r.frequencyHz, p?.frequencyHz ?? null),
    timestamp: toEpochMs(r.timestamp),
    history
  }
}

type StationKpiIndex = Record<string, StationTelemetryKpi>
type StationOscillationIndex = Record<string, StationOscillationData>

/**
 * Hub updates reach the reactive store at most this often. Messages arriving in between are
 * merged per station and committed together, so a burst costs one render instead of one per
 * message. The first update after a quiet period, and any change of a station's operational status
 * (a fault, a recovery), are committed at once.
 */
export const LIVE_COMMIT_INTERVAL_MS = 250

/** false: nothing to apply; 'staged': wait for the next commit; 'urgent': commit now. */
type StageResult = false | 'staged' | 'urgent'

// The maps are reactive per key (shallowReactive inside a shallowRef): a live update replaces one
// station's entry, so only the views that show that station re-render, and a full fetch swaps the
// whole map. Replacing the map on every message re-rendered every station card for each of the
// ~60 hub messages a second that 100 simulated stations produce, which froze the machines page.
const globalStationKpis = shallowRef<StationKpiIndex>(shallowReactive<StationKpiIndex>({}))
const globalStationOscillations = shallowRef<StationOscillationIndex>(shallowReactive<StationOscillationIndex>({}))
const isStationKpisLoading = ref(false)
const stationKpisError = ref<string | null>(null)
const isLiveStreamSubscribed = ref(false)
const lastRefreshedAt = ref<Date | null>(null)
let unsubscribeMaintenanceEvents: (() => void) | null = null
// Incremented on every stop so a subscription that resolves after stop is discarded.
let liveStreamGeneration = 0
let liveStreamStarting = false
let activeSubscribers = 0

// Live updates received but not yet committed (plain objects: staging triggers nothing).
let stagedKpis: StationKpiIndex = {}
let stagedOscillations: StationOscillationIndex = {}
let hasStagedUpdates = false
let commitTimer: ReturnType<typeof setTimeout> | null = null

const indexKpi = (map: StationKpiIndex, kpi: StationTelemetryKpi) => {
  map[stationKey(kpi.handle)] = kpi
  if (kpi.stationId) map[stationKey(kpi.stationId)] = kpi
}

const stageLiveKpi = (rawKpi: unknown, fallbackKey: string): StageResult => {
  const raw = (rawKpi && typeof rawKpi === 'object') ? rawKpi as Record<string, unknown> : null
  if (!raw) return false
  const key = stationKey(toText(raw.handle, '') || toText(raw.stationId, '') || fallbackKey)
  if (!key) return false
  const committed = globalStationKpis.value[key]
  const previous = stagedKpis[key] ?? committed ?? null
  const merged = normalizeStationKpi({ stationId: fallbackKey, ...raw }, previous)
  if (!merged) return false
  indexKpi(stagedKpis, merged)
  // A station whose state changed (or that is new) is not left waiting for the next commit.
  return committed && committed.operationalStatus === merged.operationalStatus ? 'staged' : 'urgent'
}

const stageLiveOscillation = (rawOsc: unknown, fallbackKey: string) => {
  const raw = (rawOsc && typeof rawOsc === 'object') ? rawOsc as Record<string, unknown> : null
  if (!raw) return false
  const key = stationKey(toText(raw.stationId, '') || fallbackKey)
  if (!key) return false
  const handleKey = stationKey(toText(raw.handle, ''))
  const latest = (k: string) => stagedOscillations[k] ?? globalStationOscillations.value[k]
  const previous = latest(key) ?? (handleKey ? latest(handleKey) : undefined) ?? null
  const next = normalizeStationOscillation(raw, key, previous)
  if (!next) return false
  // Index by machine id (3D twin) and floor-plan handle (plant map).
  stagedOscillations[key] = next
  const nextHandleKey = stationKey(next.handle)
  if (nextHandleKey) stagedOscillations[nextHandleKey] = next
  return true
}

const stageEvent = (ev: MaintenanceEvent): StageResult => {
  let staged: StageResult = false
  if (ev.type === 'StationTelemetryReceived') {
    staged = stageLiveKpi(ev.summary, ev.stationName || '')
  } else if (ev.type === 'StationOscillationReceived') {
    staged = stageLiveOscillation(ev.summary, ev.stationName || '') ? 'staged' : false
  }
  if (staged) hasStagedUpdates = true
  return staged
}

/** Writes staged updates into the reactive maps, one key at a time. Returns whether there were any. */
const commitStagedUpdates = () => {
  if (!hasStagedUpdates) return false
  const kpis = stagedKpis
  const oscillations = stagedOscillations
  stagedKpis = {}
  stagedOscillations = {}
  hasStagedUpdates = false
  const kpiMap = globalStationKpis.value
  let kpiCount = 0
  for (const key in kpis) {
    kpiMap[key] = kpis[key]!
    kpiCount++
  }
  const oscillationMap = globalStationOscillations.value
  for (const key in oscillations) oscillationMap[key] = oscillations[key]!
  if (kpiCount > 0) lastRefreshedAt.value = new Date()
  return true
}

const scheduleCommit = () => {
  commitTimer = setTimeout(() => {
    commitTimer = null
    // Messages kept arriving during the interval: commit them and keep throttling.
    if (commitStagedUpdates()) scheduleCommit()
  }, LIVE_COMMIT_INTERVAL_MS)
}

/**
 * Hub listener: stages the update and commits at most once per {@link LIVE_COMMIT_INTERVAL_MS},
 * except for status changes, which are committed at once (with everything staged before them).
 */
const queueStationTelemetryEvent = (ev: MaintenanceEvent) => {
  const staged = stageEvent(ev)
  if (!staged || (commitTimer && staged !== 'urgent')) return
  commitStagedUpdates()
  if (!commitTimer) scheduleCommit()
}

const cancelScheduledCommit = () => {
  if (commitTimer) clearTimeout(commitTimer)
  commitTimer = null
}

// getStationSla matches a title when the key is bounded by characters outside [A-Z0-9-].
const TITLE_TOKEN = /[A-Z0-9-]+/g
const SINGLE_TOKEN = /^[A-Z0-9-]+$/

/**
 * Groups tickets by every station key they can match in `getStationSla` (station id, tags, whole
 * tokens of the title), so callers that need the SLA of many stations (the plant map's pins) scan
 * the ticket list once instead of once per station. The lookup returns a superset of a station's
 * tickets; `getStationSla` still applies the exact rules to it.
 */
export function indexTicketsByStation(tickets: readonly MaintenanceTicket[]) {
  const byKey = new Map<string, MaintenanceTicket[]>()
  const add = (key: string, ticket: MaintenanceTicket) => {
    if (!key) return
    const list = byKey.get(key)
    if (!list) byKey.set(key, [ticket])
    else if (list[list.length - 1] !== ticket) list.push(ticket)
  }
  for (const ticket of tickets) {
    if (ticket.stationId) add(ticket.stationId.toUpperCase(), ticket)
    for (const tag of ticket.tags || []) add(tag.toUpperCase(), ticket)
    for (const token of ticket.title?.toUpperCase().match(TITLE_TOKEN) || []) add(token, ticket)
  }
  return (handleOrId: string): readonly MaintenanceTicket[] => {
    const key = stationKey(handleOrId)
    if (!key) return []
    // A key with characters outside the token alphabet can match across token boundaries: scan all.
    return SINGLE_TOKEN.test(key) ? (byKey.get(key) ?? []) : tickets
  }
}

/** Applies one maintenance-hub event to the telemetry store immediately. Exported for tests. */
export const handleStationTelemetryEvent = (ev: MaintenanceEvent) => {
  if (stageEvent(ev)) commitStagedUpdates()
}

export const useStationTelemetry = () => {
  const stationKpis = globalStationKpis
  const isLoading = isStationKpisLoading
  const lastUpdated = lastRefreshedAt

  /** Unique KPI records (the map is indexed by both handle and stationId). */
  const stationKpiList = computed(() => Array.from(new Set(Object.values(globalStationKpis.value))))
  const hasStationKpiData = computed(() => stationKpiList.value.length > 0)

  const fetchStationKpis = async (lineId?: string) => {
    isLoading.value = true
    try {
      const url = lineId ? `/api/telemetry/station-kpis?lineId=${encodeURIComponent(lineId)}` : '/api/telemetry/station-kpis'
      const data = await $fetch<unknown>(url)
      if (!Array.isArray(data)) {
        throw new Error('Unexpected station KPI response shape')
      }
      // A full fetch is authoritative; a line-filtered fetch only refreshes that line. Live updates
      // still waiting for their commit are newer than either and are applied on top.
      const map: StationKpiIndex = lineId ? { ...stationKpis.value } : {}
      if (lineId) {
        for (const [k, v] of Object.entries(map)) {
          if (v.lineId.toUpperCase() === lineId.toUpperCase()) delete map[k]
        }
      }
      for (const item of data) {
        const kpi = normalizeStationKpi(item)
        if (kpi) indexKpi(map, kpi)
      }
      stationKpis.value = shallowReactive(map)
      stationKpisError.value = null
      lastRefreshedAt.value = new Date()
    } catch (err: any) {
      // Keep the last known-good data; surface the failure instead of inventing values.
      stationKpisError.value = err?.data?.message || err?.message || 'Station telemetry unavailable'
    } finally {
      isLoading.value = false
    }
  }

  /** Returns the station's last reported KPIs, or null when no telemetry exists for it. */
  const getStationKpi = (handleOrId: string): StationTelemetryKpi | null => {
    return stationKpis.value[stationKey(handleOrId)] ?? null
  }

  // Correlate tickets to compute real-time SLA metrics for any station
  const getStationSla = (handleOrId: string, allTickets: readonly MaintenanceTicket[] = []): StationSlaSummary => {
    const key = stationKey(handleOrId)
    const now = Date.now()

    // Exact identity matches only (a substring match made 'L01-OP01' claim 'L01-OP010' tickets);
    // titles must contain the handle as a whole token.
    const titleToken = key ? new RegExp(`(^|[^A-Z0-9-])${key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}($|[^A-Z0-9-])`) : null
    const matching = key
      ? (allTickets || []).filter(t => {
        const matchStation = t.stationId && t.stationId.toUpperCase() === key
        const matchTag = t.tags && t.tags.some(tag => tag.toUpperCase() === key)
        const matchTitle = !!t.title && !!titleToken && titleToken.test(t.title.toUpperCase())
        return matchStation || matchTag || matchTitle
      })
      : []

    const open = matching.filter(t => t.status !== 'Resolved' && t.status !== 'Closed')
    const critical = open.filter(t => t.priority === 'Critical')

    let breached = 0
    let warning = 0
    let earliestDueTime: number | null = null

    for (const t of open) {
      if (t.slaDueAt) {
        const due = new Date(t.slaDueAt).getTime()
        if (!Number.isFinite(due)) continue
        if (due < now) {
          breached++
        } else {
          const diff = due - now
          if (diff < 7200000) { // < 2 hours
            warning++
          }
          if (earliestDueTime === null || due < earliestDueTime) {
            earliestDueTime = due
          }
        }
      }
    }

    let slaHealth: StationSlaSummary['slaHealth'] = 'none'
    if (open.length > 0) {
      if (breached > 0) slaHealth = 'breached'
      else if (warning > 0 || critical.length > 0) slaHealth = 'warning'
      else slaHealth = 'healthy'
    }

    let earliestDueText: string | null = null
    if (breached > 0) {
      earliestDueText = `${breached} SLA Breached`
    } else if (earliestDueTime !== null) {
      const diffMin = Math.round((earliestDueTime - now) / 60000)
      if (diffMin <= 0) {
        earliestDueText = 'Due now'
      } else if (diffMin < 60) {
        earliestDueText = `Due in ${diffMin}m`
      } else {
        const hours = Math.floor(diffMin / 60)
        const mins = diffMin % 60
        earliestDueText = `Due in ${hours}h ${mins}m`
      }
    }

    return {
      totalTickets: matching.length,
      openTickets: open.length,
      criticalTickets: critical.length,
      breachedTickets: breached,
      warningTickets: warning,
      slaHealth,
      earliestDueText,
      tickets: matching
    }
  }

  /** Returns the last received oscillation sample for a station, or null when none was received. */
  const getStationOscillation = (handleOrId: string): StationOscillationData | null => {
    return globalStationOscillations.value[stationKey(handleOrId)] ?? null
  }

  /** Applies a (partial) oscillation sample now (with any updates still waiting for their commit). */
  const updateStationOscillation = (stationId: string, partial: Partial<StationOscillationData>) => {
    if (!stageLiveOscillation({ ...partial, stationId: partial.stationId || stationId }, stationId)) return
    hasStagedUpdates = true
    commitStagedUpdates()
  }

  /**
   * Subscribes to live `StationTelemetryReceived` / `StationOscillationReceived` hub events.
   * Values only ever come from the backend; there is no client-side signal generator.
   */
  const startOscillationStream = () => {
    if (typeof window === 'undefined') return
    if (unsubscribeMaintenanceEvents || liveStreamStarting) return
    liveStreamStarting = true
    const generation = liveStreamGeneration

    import('~/services/maintenance').then(({ getMaintenanceService }) => {
      liveStreamStarting = false
      // Stopped (or restarted) while the import was pending: do not leak a listener.
      if (generation !== liveStreamGeneration || unsubscribeMaintenanceEvents) return
      const svc = getMaintenanceService()
      if (svc && typeof svc.subscribeToEvents === 'function') {
        unsubscribeMaintenanceEvents = svc.subscribeToEvents(queueStationTelemetryEvent)
        isLiveStreamSubscribed.value = true
      }
    }).catch(() => {
      liveStreamStarting = false
    })
  }

  const stopOscillationStream = () => {
    liveStreamGeneration++
    liveStreamStarting = false
    if (unsubscribeMaintenanceEvents) {
      try {
        unsubscribeMaintenanceEvents()
      } catch { /* listener already gone */ }
      unsubscribeMaintenanceEvents = null
    }
    // Keep what already arrived (the store outlives its subscribers), but stop the timer.
    cancelScheduledCommit()
    commitStagedUpdates()
    isLiveStreamSubscribed.value = false
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      activeSubscribers++
      if (!hasStationKpiData.value && !isLoading.value) {
        fetchStationKpis()
      }
      startOscillationStream()
    })

    onUnmounted(() => {
      activeSubscribers = Math.max(0, activeSubscribers - 1)
      if (activeSubscribers === 0) {
        stopOscillationStream()
      }
    })
  }

  return {
    stationKpis,
    stationKpiList,
    hasStationKpiData,
    stationKpisError,
    stationOscillations: globalStationOscillations,
    isLoading,
    isLiveStreamSubscribed,
    lastUpdated,
    fetchStationKpis,
    getStationKpi,
    getStationSla,
    getStationOscillation,
    updateStationOscillation,
    startOscillationStream,
    stopOscillationStream
  }
}
