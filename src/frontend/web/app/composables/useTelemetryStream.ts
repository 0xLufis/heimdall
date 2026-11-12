import { ref, shallowRef, computed, watch, toValue, onMounted, onUnmounted, getCurrentInstance, type MaybeRefOrGetter } from 'vue'
import { CircularBuffer } from '~/utils/CircularBuffer'
import type { MaintenanceEvent } from '~/types/maintenance'

/**
 * Live telemetry stream of ONE station.
 *
 * Data path: PLC block → backend ingestion → SignalR `StationOscillationReceived` (machine
 * dynamics) / `StationTelemetryReceived` (production KPIs + state) on the shared maintenance hub
 * connection → this composable. The target station is reactive: switching it clears the buffers
 * and from then on only that station's samples are accepted.
 *
 * Every value is `null` until the station reports it; nothing is synthesized and a missing
 * reading is never shown as 0.
 */

export type StreamConnectionState = 'connected' | 'connecting' | 'reconnecting' | 'disconnected'

/** How fresh the selected station's data is. */
export type StreamSignalState = 'live' | 'stale' | 'waiting' | 'idle'

export interface TelemetryStreamTarget {
  /** Backend machine id. */
  stationId?: string | null
  /** Floor-plan handle / custom identifier the backend also publishes samples under. */
  handle?: string | null
}

export interface TelemetryStreamOptions {
  bufferCapacity?: number
  /** Station to follow; may be a ref/getter. A plain string is treated as the station id. */
  target?: MaybeRefOrGetter<TelemetryStreamTarget | string | null | undefined>
  /** Shorthand for a fixed `target.stationId`. */
  targetMachineId?: string
  /** A sample older than this is reported as `stale` (default 15 s). */
  staleAfterMs?: number
}

export interface TelemetryStreamValues {
  cycleTimeMs: number | null
  vibrationMmS: number | null
  /** Station temperature (the backend's `temperatureC`). */
  spindleTempC: number | null
  spindleRpm: number | null
  powerKw: number | null
  frequencyHz: number | null
  /** RUNNING | IDLE | WARNING | FAULTED | MAINTENANCE, or OFFLINE while nothing was reported. */
  plcState: string
}

export const STREAM_METRICS = ['cycleTimeMs', 'vibrationMmS', 'spindleTempC', 'spindleRpm', 'powerKw', 'frequencyHz'] as const
export type StreamMetric = typeof STREAM_METRICS[number]

/** Backend DTO field for each stream metric (`StationOscillationBroadcastDto`, camelCase JSON). */
const SOURCE_FIELD: Record<StreamMetric, string> = {
  cycleTimeMs: 'cycleTimeMs',
  vibrationMmS: 'vibrationMmS',
  spindleTempC: 'temperatureC',
  spindleRpm: 'spindleRpm',
  powerKw: 'powerKw',
  frequencyHz: 'frequencyHz'
}

const emptyValues = (): TelemetryStreamValues => ({
  cycleTimeMs: null,
  vibrationMmS: null,
  spindleTempC: null,
  spindleRpm: null,
  powerKw: null,
  frequencyHz: null,
  plcState: 'OFFLINE'
})

const key = (value: unknown) => (typeof value === 'string' ? value.trim().toUpperCase() : '')

const finite = (value: unknown): number | null => {
  const n = typeof value === 'string' && value.trim() !== '' ? Number(value) : value
  return typeof n === 'number' && Number.isFinite(n) ? n : null
}

const sampleTime = (value: unknown): number => {
  if (typeof value === 'number' && Number.isFinite(value) && value > 0) return value
  if (typeof value === 'string' && value) {
    const parsed = Date.parse(value)
    if (Number.isFinite(parsed)) return parsed
  }
  return Date.now()
}

export function useTelemetryStream(options: TelemetryStreamOptions = {}) {
  const capacity = options.bufferCapacity || 120
  const staleAfterMs = options.staleAfterMs ?? 15_000

  const isSubscribed = ref(false)
  const lastHeartbeat = ref<Date | null>(null)
  const sampleCount = ref(0)
  const now = ref(Date.now())
  const latestPlcState = ref<string>('OFFLINE')
  const latestValues = ref<TelemetryStreamValues>(emptyValues())

  // Ring buffers (zero allocation per sample); wrapped in shallowRef so a target switch can swap them.
  const buffers: Record<StreamMetric, ReturnType<typeof shallowRef<CircularBuffer>>> = {
    cycleTimeMs: shallowRef(new CircularBuffer(capacity)),
    vibrationMmS: shallowRef(new CircularBuffer(capacity)),
    spindleTempC: shallowRef(new CircularBuffer(capacity)),
    spindleRpm: shallowRef(new CircularBuffer(capacity)),
    powerKw: shallowRef(new CircularBuffer(capacity)),
    frequencyHz: shallowRef(new CircularBuffer(capacity))
  }

  const target = computed<TelemetryStreamTarget>(() => {
    const raw = options.target !== undefined ? toValue(options.target) : (options.targetMachineId ?? null)
    if (!raw) return {}
    return typeof raw === 'string' ? { stationId: raw } : raw
  })
  const targetKeys = computed(() => [key(target.value.stationId), key(target.value.handle)].filter(Boolean))
  const hasTarget = computed(() => targetKeys.value.length > 0)

  const reset = () => {
    for (const metric of STREAM_METRICS) buffers[metric].value = new CircularBuffer(capacity)
    latestValues.value = emptyValues()
    latestPlcState.value = 'OFFLINE'
    lastHeartbeat.value = null
    sampleCount.value = 0
  }

  const matchesTarget = (ev: MaintenanceEvent) => {
    if (!hasTarget.value) return false
    const summary = (ev.summary && typeof ev.summary === 'object' ? ev.summary : {}) as Record<string, unknown>
    const candidates = [key(ev.stationName), key(summary.stationId), key(summary.handle)]
    return candidates.some(c => c && targetKeys.value.includes(c))
  }

  const applyState = (raw: unknown) => {
    if (typeof raw !== 'string' || !raw.trim()) return
    const state = raw.trim().toUpperCase()
    latestPlcState.value = state
    latestValues.value = { ...latestValues.value, plcState: state }
  }

  /**
   * Applies a dynamics sample (`StationOscillationBroadcastDto` shape). Only reported fields are
   * pushed; an absent field keeps its previous value and adds no point to its trend.
   */
  const applyDynamics = (raw: unknown, pushToBuffers = true) => {
    if (!raw || typeof raw !== 'object') return false
    const r = raw as Record<string, unknown>
    const next = { ...latestValues.value }
    let any = false
    for (const metric of STREAM_METRICS) {
      const value = finite(r[SOURCE_FIELD[metric]])
      if (value === null) continue
      any = true
      next[metric] = value
      if (pushToBuffers) buffers[metric].value.push(value)
    }
    if (any) {
      latestValues.value = next
      // Live samples are timed by arrival (freshness must not depend on PLC clock skew);
      // a seeded snapshot carries the time it was last reported.
      lastHeartbeat.value = pushToBuffers ? new Date() : new Date(sampleTime(r.lastTelemetryAt ?? r.timestamp))
      if (pushToBuffers) sampleCount.value++
    }
    applyState(r.operationalStatus)
    return any
  }

  /** Routes a maintenance-hub event into the stream. Exported for tests. */
  const handleEvent = (ev: MaintenanceEvent) => {
    if (ev.type === 'StationOscillationReceived') {
      if (matchesTarget(ev)) applyDynamics(ev.summary)
    } else if (ev.type === 'StationTelemetryReceived') {
      if (!matchesTarget(ev)) return
      const summary = (ev.summary && typeof ev.summary === 'object' ? ev.summary : {}) as Record<string, unknown>
      applyState(summary.operationalStatus)
      lastHeartbeat.value = new Date()
    }
  }

  /**
   * Seeds the current values from the station's last known snapshot (REST station KPIs) so the
   * page is not blank until the next live sample. Does not add trend points.
   */
  const seed = (snapshot: unknown) => {
    if (!snapshot || typeof snapshot !== 'object' || sampleCount.value > 0) return
    applyDynamics(snapshot, false)
  }

  let unsubscribe: (() => void) | null = null
  let generation = 0
  let clock: ReturnType<typeof setInterval> | null = null

  const connect = async () => {
    if (typeof window === 'undefined' || unsubscribe) return
    const mine = ++generation
    try {
      const { getMaintenanceService } = await import('~/services/maintenance')
      // Disconnected (or reconnected) while the import was pending: do not leak a listener.
      if (mine !== generation || unsubscribe) return
      const svc = getMaintenanceService()
      if (svc && typeof svc.subscribeToEvents === 'function') {
        unsubscribe = svc.subscribeToEvents(handleEvent)
        isSubscribed.value = true
      }
    } catch (err) {
      console.warn('[useTelemetryStream] could not subscribe to the maintenance hub:', err)
    }
    if (!clock) clock = setInterval(() => { now.value = Date.now() }, 1000)
  }

  const disconnect = async () => {
    generation++
    if (unsubscribe) {
      try { unsubscribe() } catch { /* listener already gone */ }
      unsubscribe = null
    }
    if (clock) {
      clearInterval(clock)
      clock = null
    }
    isSubscribed.value = false
  }

  // Following another station: its predecessor's values and trends must not linger.
  watch(() => targetKeys.value.join('|'), (next, prev) => {
    if (next !== prev) reset()
  })

  const ageMs = computed<number | null>(() =>
    lastHeartbeat.value ? Math.max(0, now.value - lastHeartbeat.value.getTime()) : null
  )

  const signalState = computed<StreamSignalState>(() => {
    if (!hasTarget.value) return 'idle'
    if (ageMs.value === null) return 'waiting'
    return ageMs.value <= staleAfterMs ? 'live' : 'stale'
  })

  /** Kept for existing consumers: connected = fresh samples, connecting = subscribed but silent. */
  const connectionState = computed<StreamConnectionState>(() => {
    if (!isSubscribed.value) return 'disconnected'
    return signalState.value === 'live' ? 'connected' : 'connecting'
  })

  if (getCurrentInstance()) {
    onMounted(() => { connect() })
    onUnmounted(() => { disconnect() })
  }

  return {
    target,
    hasTarget,
    isSubscribed,
    connectionState,
    signalState,
    ageMs,
    sampleCount,
    lastHeartbeat,
    latestPlcState,
    latestValues,
    cycleTimeBuffer: buffers.cycleTimeMs,
    vibrationBuffer: buffers.vibrationMmS,
    temperatureBuffer: buffers.spindleTempC,
    spindleRpmBuffer: buffers.spindleRpm,
    powerBuffer: buffers.powerKw,
    frequencyBuffer: buffers.frequencyHz,
    handleEvent,
    seed,
    reset,
    connect,
    disconnect
  }
}
