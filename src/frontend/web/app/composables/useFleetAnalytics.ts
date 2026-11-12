import { ref, computed } from 'vue'
import {
  normalizeFleetStations,
  type FleetContext,
  type FleetStation,
  type FleetStationsMeta
} from '~/utils/fleetAnalytics'

/**
 * Fleet analytics station rows (GET /api/analytics/fleet-stations → backend
 * /api/v1/analytics/fleet-stations). Shared by the analytics tabs; holds the last good response
 * while a refresh is running or failed, and never fills gaps with made-up values.
 */
const stations = ref<FleetStation[]>([])
const meta = ref<FleetStationsMeta | null>(null)
const isLoading = ref(false)
const error = ref<string | null>(null)
const hasLoaded = ref(false)
// Only the newest request may write its result (window changes can overlap).
let requestSequence = 0

export function useFleetAnalytics() {
  const refresh = async (windowDays: number) => {
    const sequence = ++requestSequence
    isLoading.value = true
    try {
      const raw = await $fetch<unknown>('/api/analytics/fleet-stations', { query: { windowDays } })
      if (sequence !== requestSequence) return
      const payload = normalizeFleetStations(raw)
      stations.value = payload.stations
      meta.value = payload.meta
      error.value = null
      hasLoaded.value = true
    } catch (err: any) {
      if (sequence !== requestSequence) return
      error.value = err?.data?.message || err?.statusMessage || err?.message || 'Fleet data is unavailable'
    } finally {
      if (sequence === requestSequence) isLoading.value = false
    }
  }

  /** Ages are measured against the server time of the response, not the browser clock. */
  const context = computed<FleetContext>(() => {
    const generatedAt = meta.value?.generatedAt ? Date.parse(meta.value.generatedAt) : NaN
    return { generatedAtMs: Number.isFinite(generatedAt) ? generatedAt : Date.now() }
  })

  return { stations, meta, isLoading, error, hasLoaded, context, refresh }
}
