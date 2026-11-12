<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount } from 'vue'
import { useStationTelemetry, indexTicketsByStation, type StationTelemetryKpi, type StationSlaSummary } from '~/composables/useStationTelemetry'
import { useStaggeredRefresh } from '~/composables/useStaggeredRefresh'
import { stationPinState, sameStationPinState, type StationPinState } from '~/utils/stationPinState'
import {
  computeOverlayPlacement,
  computePinPlacement,
  projectSvgPointToScreen,
  PLANT_MAP_CARD_SIZE,
  PLANT_MAP_OVERLAY_INSETS,
  type OverlayPlacement,
  type SvgViewBox
} from '~/utils/telemetryOverlayPlacement'
import type { MaintenanceEvent, MaintenanceTicket } from '~/types/maintenance'
import StationMapPin from '~/components/map/StationMapPin.vue'
import StationTelemetryBubble from '~/components/map/StationTelemetryBubble.vue'

/** Half the station glyph in drawing units (`InteractiveMapCanvas`: 20 x 20 box around the insert point). */
const STATION_GLYPH_HALF = 10

const props = withDefaults(
  defineProps<{
    /** Station blocks (DXF INSERT entities) in drawing order. */
    entities: Array<{ handle?: string; name?: string; position: { x: number; y: number } }>
    viewBox: SvgViewBox
    containerWidth: number
    containerHeight: number
    filterMode?: 'all' | 'alerts_only' | 'hover_only'
    activePin?: string | null
    hoveredHandle?: string | null
    expandedHandles?: string[]
    stationKpis?: Record<string, StationTelemetryKpi>
    stationTickets?: MaintenanceTicket[]
    showOee?: boolean
    showYield?: boolean
    showPartsHour?: boolean
    showSla?: boolean
    showOscillation?: boolean
  }>(),
  {
    filterMode: 'alerts_only',
    activePin: null,
    hoveredHandle: null,
    expandedHandles: () => [],
    showOee: true,
    showYield: true,
    showPartsHour: true,
    showSla: true,
    showOscillation: true
  }
)

const emit = defineEmits<{
  (e: 'toggle-expand', handle: string): void
  (e: 'select-station', handle: string, name: string): void
  (e: 'create-ticket', handle: string): void
  (e: 'view-ticket', ticketId: string): void
}>()

const { getStationKpi, getStationSla } = useStationTelemetry()

const resolveKpi = (handle: string): StationTelemetryKpi | null =>
  props.stationKpis?.[handle.toUpperCase()] ?? getStationKpi(handle)

// Built once per ticket-list change; each pin then looks at its own tickets only.
const ticketsByStation = computed(() => indexTicketsByStation(props.stationTickets || []))

const resolveSla = (handle: string): StationSlaSummary => getStationSla(handle, ticketsByStation.value(handle))

const handleOf = (entity: { handle?: string; name?: string }) => entity.handle || entity.name || ''

const handles = computed(() => props.entities.map(handleOf).filter(Boolean))
const handleByKey = computed(() => new Map(handles.value.map(h => [h.toUpperCase(), h])))

// Pin colours are sampled, not live: each pin is recomputed once every 500 ms, a fifth of them
// every 100 ms, and redrawn only when its state changed. Live telemetry (~60 hub messages a second
// for 100 stations) therefore never redraws every pin at once.
const { states: pinStates, refreshNow } = useStaggeredRefresh<StationPinState>({
  keys: () => handles.value,
  compute: handle => stationPinState(resolveKpi(handle), resolveSla(handle)),
  equals: sameStationPinState
})

// Alerts, ticket / SLA changes and operational-status changes reach the pins at once instead of on
// their next turn. The refresh runs after the other listeners of the same hub message (ticket list,
// KPI store) and after Vue has passed their changes down as props.
let urgentAll = false
const urgentHandles = new Set<string>()
let urgentTimer: ReturnType<typeof setTimeout> | null = null

const flushUrgent = () => {
  urgentTimer = null
  if (urgentAll) refreshNow()
  else refreshNow([...urgentHandles])
  urgentAll = false
  urgentHandles.clear()
}

const requestUrgentRefresh = (handle?: string) => {
  if (handle) urgentHandles.add(handle)
  else urgentAll = true
  if (!urgentTimer) urgentTimer = setTimeout(flushUrgent, 0)
}

const onHubEvent = (ev: MaintenanceEvent) => {
  switch (ev.type) {
    case 'TicketCreated':
    case 'TicketUpdated':
    case 'TicketDeleted':
    case 'StatusChanged':
    case 'CriticalAlert':
      // Tickets match stations by id, tag or title, so every pin is re-evaluated (cheap; rare events).
      requestUrgentRefresh()
      return
    case 'StationTelemetryReceived': {
      const summary = ev.summary && typeof ev.summary === 'object' ? ev.summary as Record<string, unknown> : null
      const status = typeof summary?.operationalStatus === 'string' ? summary.operationalStatus.toUpperCase() : ''
      if (!status) return
      for (const id of [summary?.handle, summary?.stationId, ev.stationName]) {
        const handle = typeof id === 'string' && id ? handleByKey.value.get(id.toUpperCase()) : undefined
        if (!handle) continue
        if (pinStates[handle]?.status !== status) requestUrgentRefresh(handle)
        return
      }
    }
  }
}

let unsubscribe: (() => void) | null = null
let unmounted = false

onMounted(async () => {
  try {
    const { getMaintenanceService } = await import('~/services/maintenance')
    if (unmounted) return
    const service = getMaintenanceService()
    if (service && typeof service.subscribeToEvents === 'function') unsubscribe = service.subscribeToEvents(onHubEvent)
  } catch {
    // No hub: the staggered refresh still keeps the pins current.
  }
})

onBeforeUnmount(() => {
  unmounted = true
  unsubscribe?.()
  unsubscribe = null
  if (urgentTimer) clearTimeout(urgentTimer)
  urgentTimer = null
})

const isPinShown = (handle: string, state: StationPinState | undefined) => {
  if (handle === props.activePin || handle === props.hoveredHandle) return true
  if (props.filterMode === 'hover_only') return false
  if (props.filterMode === 'alerts_only') return !!state?.alert
  return true
}

const pins = computed(() => {
  const cw = props.containerWidth
  const ch = props.containerHeight
  const vb = props.viewBox
  if (cw <= 0 || ch <= 0 || !vb.width || !vb.height) return []
  const scale = Math.min(cw / vb.width, ch / vb.height)
  const expanded = new Set(props.expandedHandles)
  const result: Array<{ handle: string; left: number; top: number; state: StationPinState | undefined }> = []
  for (const entity of props.entities) {
    const handle = handleOf(entity)
    // An open station card replaces the pin.
    if (!handle || expanded.has(handle)) continue
    const state = pinStates[handle]
    if (!isPinShown(handle, state)) continue
    const anchor = projectSvgPointToScreen(entity.position, vb, cw, ch)
    if (!anchor) continue
    // The tip touches the top edge of the station glyph.
    const placement = computePinPlacement({
      anchorX: anchor.x,
      anchorY: anchor.y - STATION_GLYPH_HALF * scale,
      containerWidth: cw,
      containerHeight: ch,
      insets: PLANT_MAP_OVERLAY_INSETS
    })
    if (!placement.visible) continue
    result.push({ handle, left: placement.left, top: placement.top, state })
  }
  return result
})

const cards = computed(() => {
  const cw = props.containerWidth
  const ch = props.containerHeight
  if (cw <= 0 || ch <= 0 || props.expandedHandles.length === 0) return []
  const result: Array<{ handle: string; name: string; placement: OverlayPlacement }> = []
  for (const handle of props.expandedHandles) {
    const entity = props.entities.find(e => handleOf(e) === handle)
    if (!entity) continue
    const anchor = projectSvgPointToScreen(entity.position, props.viewBox, cw, ch)
    if (!anchor) continue
    const placement = computeOverlayPlacement({
      anchorX: anchor.x,
      anchorY: anchor.y,
      containerWidth: cw,
      containerHeight: ch,
      size: PLANT_MAP_CARD_SIZE,
      insets: PLANT_MAP_OVERLAY_INSETS,
      gapAbove: 12,
      gapBelow: 16
    })
    if (placement.visible) result.push({ handle, name: entity.name || handle, placement })
  }
  return result
})
</script>

<template>
  <div class="absolute inset-0 pointer-events-none overflow-hidden z-20" data-map-overlays>
    <StationMapPin
      v-for="pin in pins"
      :key="`pin-${pin.handle}`"
      :handle="pin.handle"
      :state="pin.state"
      :left="pin.left"
      :top="pin.top"
      :selected="pin.handle === activePin"
      @activate="emit('toggle-expand', pin.handle)"
    />

    <div
      v-for="card in cards"
      :key="`card-${card.handle}`"
      class="absolute pointer-events-none flex items-start"
      :data-overlay-handle="card.handle"
      :style="{ left: `${card.placement.left}px`, top: `${card.placement.top}px`, width: `${card.placement.width}px`, zIndex: 60 }"
    >
      <StationTelemetryBubble
        :handle="card.handle"
        :name="card.name"
        :kpi="resolveKpi(card.handle)"
        :sla="resolveSla(card.handle)"
        :is-flipped-bottom="card.placement.isFlippedBottom"
        :max-width-px="card.placement.width"
        :max-height-px="card.placement.height"
        :arrow-offset-px="card.placement.arrowOffset"
        :show-oee="showOee"
        :show-yield="showYield"
        :show-parts-hour="showPartsHour"
        :show-sla="showSla"
        :show-oscillation="showOscillation"
        @toggle-expand="emit('toggle-expand', card.handle)"
        @select-station="emit('select-station', card.handle, card.name)"
        @create-ticket="emit('create-ticket', card.handle)"
        @view-ticket="(id: string) => emit('view-ticket', id)"
      />
    </div>
  </div>
</template>
