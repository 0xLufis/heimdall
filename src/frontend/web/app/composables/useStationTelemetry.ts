import { ref, computed, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import type { MaintenanceTicket } from '~/types/maintenance'

export interface StationTelemetryKpi {
  stationId: string
  handle: string
  name: string
  lineId: string
  lineName: string
  oee: number
  availability: number
  performance: number
  quality: number
  yield: number
  partsPerHour: number
  targetPartsPerHour: number
  cycleTimeSeconds: number
  targetCycleTimeSeconds: number
  totalPartsProduced: number
  scrapParts: number
  operationalStatus: 'RUNNING' | 'IDLE' | 'WARNING' | 'FAULTED' | 'MAINTENANCE'
  activeTicketCount: number
  criticalTicketCount: number
  hasSlaBreach: boolean
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

export interface StationOscillationData {
  stationId: string
  vibrationMmS: number
  spindleRpm: number
  cycleTimeMs: number
  powerKw: number
  temperatureC: number
  frequencyHz: number
  timestamp: number
  history: number[] // Rolling vibration RMS values (20-30 points) for sparklines
}

const globalStationKpis = ref<Record<string, StationTelemetryKpi>>({})
const globalStationOscillations = ref<Record<string, StationOscillationData>>({})
const isStationKpisLoading = ref(false)
const isSignalRConnected = ref(false)
const lastRefreshedAt = ref<Date>(new Date())
let simTimer: any = null
let oscillationTimer: any = null
let unsubscribeMaintenanceEvents: (() => void) | null = null
let activeSubscribers = 0

export const useStationTelemetry = () => {
  const stationKpis = globalStationKpis
  const isLoading = isStationKpisLoading
  const lastUpdated = lastRefreshedAt

  const fetchStationKpis = async (lineId?: string) => {
    isLoading.value = true
    try {
      const url = lineId ? `/api/telemetry/station-kpis?lineId=${encodeURIComponent(lineId)}` : '/api/telemetry/station-kpis'
      const data = await $fetch<StationTelemetryKpi[]>(url)
      if (Array.isArray(data)) {
        const map: Record<string, StationTelemetryKpi> = { ...stationKpis.value }
        for (const item of data) {
          map[item.handle.toUpperCase()] = item
          if (item.stationId) map[item.stationId.toUpperCase()] = item
        }
        stationKpis.value = map
        lastRefreshedAt.value = new Date()
      }
    } catch {
      // Fallback: generate local fallback if offline
    } finally {
      isLoading.value = false
    }
  }

  // Fast synchronous getter with deterministic fallback
  const getStationKpi = (handleOrId: string): StationTelemetryKpi => {
    const key = (handleOrId || '').toUpperCase()
    if (stationKpis.value[key]) {
      return stationKpis.value[key]
    }

    // Deterministic algorithmic fallback if not yet fetched
    let hash = 0
    for (let i = 0; i < key.length; i++) {
      hash = (hash << 5) - hash + key.charCodeAt(i)
      hash |= 0
    }
    hash = Math.abs(hash)

    const avail = Math.min(99.5, Math.max(80.0, 96.0 + ((hash % 11) - 5) * 0.4))
    const perf = Math.min(99.0, Math.max(75.0, 94.0 + (((Math.floor(hash / 11)) % 11) - 5) * 0.5))
    const qual = Math.min(99.9, Math.max(95.0, 99.2 + (((Math.floor(hash / 121)) % 7) - 3) * 0.1))
    const oee = Number(((avail * perf * qual) / 10000).toFixed(1))
    const yieldPct = Math.min(99.9, Math.max(94.0, Number((qual - 0.2 + (hash % 5) * 0.08).toFixed(1))))
    const partsPerHour = Math.round(142.0 * (perf / 94.0))
    const cycleTimeSec = partsPerHour > 0 ? Number((3600.0 / partsPerHour).toFixed(1)) : 0.0

    const fallback: StationTelemetryKpi = {
      stationId: handleOrId,
      handle: handleOrId,
      name: `${handleOrId} Machine Cell`,
      lineId: handleOrId.startsWith('L0') ? handleOrId.substring(0, 3) : 'L01',
      lineName: 'Production Line',
      oee,
      availability: Number(avail.toFixed(1)),
      performance: Number(perf.toFixed(1)),
      quality: Number(qual.toFixed(1)),
      yield: yieldPct,
      partsPerHour,
      targetPartsPerHour: 150.0,
      cycleTimeSeconds: cycleTimeSec,
      targetCycleTimeSeconds: 24.0,
      totalPartsProduced: 1100 + (hash % 400),
      scrapParts: Math.max(0, Math.round(1100 * (1.0 - yieldPct / 100.0))),
      operationalStatus: oee < 80 ? 'WARNING' : 'RUNNING',
      activeTicketCount: 0,
      criticalTicketCount: 0,
      hasSlaBreach: false
    }

    // Cache fallback into map
    stationKpis.value = { ...stationKpis.value, [key]: fallback }
    return fallback
  }

  // Correlate tickets to compute real-time SLA metrics for any station
  const getStationSla = (handleOrId: string, allTickets: MaintenanceTicket[] = []): StationSlaSummary => {
    const key = (handleOrId || '').toUpperCase()
    const now = Date.now()

    const matching = (allTickets || []).filter(t => {
      const matchStation = t.stationId && t.stationId.toUpperCase().includes(key)
      const matchTag = t.tags && t.tags.some(tag => tag.toUpperCase().includes(key))
      const matchTitle = t.title && t.title.toUpperCase().includes(key)
      return matchStation || matchTag || matchTitle
    })

    const open = matching.filter(t => t.status !== 'Resolved' && t.status !== 'Closed')
    const critical = open.filter(t => t.priority === 'Critical')

    let breached = 0
    let warning = 0
    let earliestDueTime: number | null = null

    for (const t of open) {
      if (t.slaDueAt) {
        const due = new Date(t.slaDueAt).getTime()
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

  // Fast synchronous getter for sub-second oscillation data
  const getStationOscillation = (handleOrId: string): StationOscillationData => {
    const key = (handleOrId || '').toUpperCase()
    if (globalStationOscillations.value[key]) {
      return globalStationOscillations.value[key]
    }

    // Deterministic algorithmic baseline based on key hash
    let hash = 0
    for (let i = 0; i < key.length; i++) {
      hash = (hash << 5) - hash + key.charCodeAt(i)
      hash |= 0
    }
    hash = Math.abs(hash)

    const baseVib = 0.85 + (hash % 12) * 0.11 // 0.85 to ~2.06 mm/s
    const baseRpm = 2200 + (hash % 15) * 80  // 2200 to 3400 RPM
    const basePower = 3.2 + (hash % 10) * 0.4 // 3.2 to 6.8 kW
    const baseTemp = 42.0 + (hash % 8) * 1.5 // 42.0 to 52.5 C

    // Seed 20 historical points with minor harmonic fluctuation
    const history: number[] = []
    for (let i = 0; i < 20; i++) {
      const harmonic = 0.12 * Math.sin((i / 20) * Math.PI * 4) + 0.04 * Math.cos((i / 20) * Math.PI * 8)
      history.push(Number((baseVib + harmonic).toFixed(3)))
    }

    const fallback: StationOscillationData = {
      stationId: handleOrId,
      vibrationMmS: Number(baseVib.toFixed(2)),
      spindleRpm: baseRpm,
      cycleTimeMs: 24000,
      powerKw: Number(basePower.toFixed(1)),
      temperatureC: Number(baseTemp.toFixed(1)),
      frequencyHz: 50.0 + ((hash % 5) - 2) * 0.05,
      timestamp: Date.now(),
      history
    }

    globalStationOscillations.value = { ...globalStationOscillations.value, [key]: fallback }
    return fallback
  }

  const updateStationOscillation = (stationId: string, partial: Partial<StationOscillationData>) => {
    const key = (stationId || '').toUpperCase()
    const current = getStationOscillation(key)
    const newVib = partial.vibrationMmS !== undefined ? partial.vibrationMmS : current.vibrationMmS
    const updatedHistory = [...(current.history || []), newVib].slice(-24)

    globalStationOscillations.value = {
      ...globalStationOscillations.value,
      [key]: {
        ...current,
        ...partial,
        vibrationMmS: newVib,
        history: updatedHistory,
        timestamp: Date.now()
      }
    }
  }

  const startOscillationStream = () => {
    if (typeof window === 'undefined') return
    if (oscillationTimer) return

    // 1. Connect or listen to Maintenance Service SignalR events if available
    try {
      if (!unsubscribeMaintenanceEvents) {
        import('~/services/maintenance').then(({ getMaintenanceService }) => {
          const svc = getMaintenanceService()
          if (svc && typeof svc.subscribeToEvents === 'function') {
            unsubscribeMaintenanceEvents = svc.subscribeToEvents((ev: any) => {
              if (ev.type === 'StationTelemetryReceived' && ev.summary) {
                const kpi = ev.summary
                const key = (kpi.handle || ev.stationName || '').toUpperCase()
                if (key) {
                  globalStationKpis.value = { ...globalStationKpis.value, [key]: { ...getStationKpi(key), ...kpi } }
                }
              } else if (ev.type === 'StationOscillationReceived' && ev.summary) {
                const osc = ev.summary
                const key = (osc.stationId || ev.stationName || '').toUpperCase()
                if (key) {
                  updateStationOscillation(key, osc)
                }
              }
            })
            isSignalRConnected.value = true
          }
        }).catch(() => {})
      }
    } catch {
      // Fallback in headless/isolated test environments
    }

    // 2. High performance sub-second oscillation generator (300ms)
    let tickCount = 0
    oscillationTimer = setInterval(() => {
      tickCount++
      const currentMap = { ...globalStationOscillations.value }
      const keys = Object.keys(currentMap)
      if (keys.length === 0) return

      for (const k of keys) {
        const item = currentMap[k]
        if (!item) continue
        const t = tickCount * 0.3
        // Harmonic synthesis: f1 = 0.7Hz, f2 = 1.9Hz + slight noise
        const harmonic = 0.14 * Math.sin(2 * Math.PI * 0.7 * t) + 0.05 * Math.sin(2 * Math.PI * 1.9 * t) + (Math.random() - 0.5) * 0.03
        const newVib = Math.max(0.2, Number((item.vibrationMmS + harmonic * 0.25).toFixed(3)))
        const rpmJitter = Math.round((Math.random() - 0.5) * 16)

        const hist = [...(item.history || []), newVib].slice(-24)
        currentMap[k] = {
          ...item,
          vibrationMmS: newVib,
          spindleRpm: Math.max(800, item.spindleRpm + rpmJitter),
          history: hist,
          timestamp: Date.now()
        }
      }
      globalStationOscillations.value = currentMap
    }, 300)
  }

  const stopOscillationStream = () => {
    if (oscillationTimer) {
      clearInterval(oscillationTimer)
      oscillationTimer = null
    }
    if (unsubscribeMaintenanceEvents) {
      try {
        unsubscribeMaintenanceEvents()
      } catch {}
      unsubscribeMaintenanceEvents = null
    }
    isSignalRConnected.value = false
  }

  const startLiveSimulation = () => {
    if (typeof window === 'undefined') return
    if (simTimer) return

    simTimer = setInterval(() => {
      const current = { ...stationKpis.value }
      let changed = false

      // Slightly perturb parts count / rate for stations with simulation
      for (const [key, item] of Object.entries(current)) {
        if (item.operationalStatus === 'RUNNING' && Math.random() < 0.25) {
          const jitter = (Math.random() - 0.48) * 1.5
          const newParts = item.totalPartsProduced + 1
          const newRate = Math.min(180, Math.max(90, Math.round(item.partsPerHour + jitter)))
          current[key] = {
            ...item,
            totalPartsProduced: newParts,
            partsPerHour: newRate,
            cycleTimeSeconds: Number((3600.0 / newRate).toFixed(1))
          }
          changed = true
        }
      }

      if (changed) {
        stationKpis.value = current
        lastRefreshedAt.value = new Date()
      }
    }, 4000)
  }

  const stopLiveSimulation = () => {
    if (simTimer) {
      clearInterval(simTimer)
      simTimer = null
    }
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      activeSubscribers++
      if (Object.keys(stationKpis.value).length === 0) {
        fetchStationKpis()
      }
      startLiveSimulation()
      startOscillationStream()
    })

    onUnmounted(() => {
      activeSubscribers = Math.max(0, activeSubscribers - 1)
      if (activeSubscribers === 0) {
        stopLiveSimulation()
        stopOscillationStream()
      }
    })
  }

  return {
    stationKpis,
    stationOscillations: globalStationOscillations,
    isLoading,
    isSignalRConnected,
    lastUpdated,
    fetchStationKpis,
    getStationKpi,
    getStationSla,
    getStationOscillation,
    updateStationOscillation,
    startLiveSimulation,
    stopLiveSimulation,
    startOscillationStream,
    stopOscillationStream
  }
}
