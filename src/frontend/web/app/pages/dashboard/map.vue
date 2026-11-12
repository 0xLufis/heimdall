<script setup lang="ts">
import { ref, onMounted, computed, nextTick, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import InteractiveMapCanvas from '~/components/map/InteractiveMapCanvas.vue'
import MapPinningDialog from '~/components/dashboard/MapPinningDialog.vue'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { MonitorIcon, MapPinIcon, Cpu, Layers, ChevronDown, Map as MapIcon } from 'lucide-vue-next'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import { useStations } from '~/composables/useStations'
import { useControllers } from '~/composables/useControllers'
import { useGlobalContextMenu } from '~/composables/useGlobalContextMenu'
import type { ContextMenuContextData } from '~/types/contextMenu'
import { useMaintenance } from '~/composables/useMaintenance'
import { useStationTelemetry, type StationTelemetryKpi } from '~/composables/useStationTelemetry'
import { resolvePreferredTechnician } from '~/utils/technicianInheritance'
import { queryText, resolveMapFocus } from '~/utils/deepLinkQueries'
import { Activity, Flame, Gauge, Clock, ChevronRight, AlertTriangle, CheckCircle2, Radio, Zap } from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()
const route = useRoute()
const { stations, fetchStations, updateStationPin } = useStations()
const { controllers, fetchControllers, updateControllerPin } = useControllers()
const { openContextMenu } = useGlobalContextMenu()
const { tickets, fetchTickets } = useMaintenance()
const { stationKpis, stationKpiList, stationKpisError, fetchStationKpis, getStationKpi, getStationSla, getStationOscillation } = useStationTelemetry()

// Overlay Controls
const showTelemetryOverlays = ref(true)
const overlayFilterMode = ref<'all' | 'alerts_only' | 'hover_only'>('alerts_only')
const showOeeBadge = ref(true)
const showYieldBadge = ref(true)
const showPartsHourBadge = ref(true)
const showSlaBadge = ref(true)
const showOscillationBadge = ref(true)
const expandedHandles = ref<string[]>([])

const handleCreateTicket = (handle: string) => {
  router.push({
    path: '/dashboard/tickets/new',
    query: { stationId: handle }
  })
}

const handleViewTicket = (ticketId: string) => {
  router.push(`/dashboard/tickets/${ticketId}`)
}

const activeStationKpi = computed(() => {
  if (!activePin.value) return null
  return getStationKpi(activePin.value)
})

const activeStationSla = computed(() => {
  if (!activePin.value) return null
  return getStationSla(activePin.value, tickets.value)
})

const activeStationOscillation = computed(() => {
  if (!activePin.value) return null
  return getStationOscillation(activePin.value)
})

const fleetKpiSummary = computed(() => {
  // stationKpis is indexed by handle *and* stationId; use the de-duplicated list for aggregates.
  const kpis = stationKpiList.value
  const allTickets = tickets.value || []
  const openTkts = allTickets.filter(t => t.status !== 'Resolved' && t.status !== 'Closed')
  const breachedCount = openTkts.filter(t => t.slaDueAt && new Date(t.slaDueAt).getTime() < Date.now()).length

  if (kpis.length === 0) {
    // No reported telemetry: show an explicit empty state rather than placeholder numbers.
    return { hasData: false, avgOee: null, avgYield: null, totalThroughput: null, totalSlaTickets: openTkts.length, breachedCount }
  }

  // Aggregate only over stations that actually reported each value.
  const reported = (pick: (k: StationTelemetryKpi) => number | null) =>
    kpis.map(pick).filter((v): v is number => v !== null)
  const mean = (vals: number[]) => (vals.length ? Number((vals.reduce((a, b) => a + b, 0) / vals.length).toFixed(1)) : null)
  const avgOee = mean(reported(k => k.oee))
  const avgYield = mean(reported(k => k.yield))
  const rates = reported(k => k.partsPerHour)
  const totalThroughput = rates.length ? Math.round(rates.reduce((a, b) => a + b, 0)) : null

  const hasData = avgOee !== null || avgYield !== null || totalThroughput !== null
  return { hasData, avgOee, avgYield, totalThroughput, totalSlaTickets: openTkts.length, breachedCount }
})

const resetMapView = () => {
  activePin.value = null
  activeBlockName.value = null
  selectedAssetId.value = null
  currentPlanId.value = 'production_hall'
  router.push('/dashboard/map')
  fetchStations()
  fetchControllers()
  fetchTickets()
  fetchStationKpis()
}

const availableFloorPlans = [
  { id: 'production_hall', name: 'Master Production Hall', url: '/sample/production_hall.dxf', badge: 'Integrated Hall' },
  { id: 'line_a', name: 'Line A - Pre-Assembly', url: '/sample/LINE-A.dxf', badge: 'Cell Line' },
  { id: 'line_b', name: 'Line B - Screwing & Fastening', url: '/sample/LINE-B.dxf', badge: 'Cell Line' },
  { id: 'line_c', name: 'Line C - Vision & Quality', url: '/sample/LINE-C.dxf', badge: 'Cell Line' },
  { id: 'line_d', name: 'Line D - Dispensing & Bonding', url: '/sample/LINE-D.dxf', badge: 'Cell Line' },
  { id: 'line_e', name: 'Line E - Robotic Welding', url: '/sample/LINE-E.dxf', badge: 'Cell Line' },
  { id: 'line_f', name: 'Line F - Mechanical Assembly', url: '/sample/LINE-F.dxf', badge: 'Cell Line' },
  { id: 'line_g', name: 'Line G - High Voltage Battery', url: '/sample/LINE-G.dxf', badge: 'Cell Line' },
  { id: 'line_h', name: 'Line H - Powertrain Cells', url: '/sample/LINE-H.dxf', badge: 'Cell Line' },
  { id: 'line_i', name: 'Line I - Subassembly Line', url: '/sample/LINE-I.dxf', badge: 'Cell Line' },
  { id: 'line_j', name: 'Line J - EOL Final Testing', url: '/sample/LINE-J.dxf', badge: 'Cell Line' },
  { id: 'assembly_line', name: 'Assembly Line Overview', url: '/sample/assembly_line.dxf', badge: 'Overview' }
]

const currentPlanId = ref('production_hall')
const currentPlan = computed(() => availableFloorPlans.find(p => p.id === currentPlanId.value) || availableFloorPlans[0])

const activePin = ref<string | null>(null)
const activeBlockName = ref<string | null>(null)
const isPinningDialogOpen = ref(false)
const selectedAssetId = ref<string | null>(null)
const sidebarScrollContainer = ref<any>(null)
const assetItemRefs = new Map<string, HTMLElement>()

const setAssetItemRef = (id: string, el: any) => {
  if (el) {
    assetItemRefs.set(id, el.$el || el)
  } else {
    assetItemRefs.delete(id)
  }
}

const scrollToAsset = async (handle: string) => {
  if (!handle) return

  // 1. Direct match with Client PC
  let targetAsset = pinnedAssets.value.find(a => a.handle === handle && a.type === 'Client PC')

  // 2. Machine match whose associated controller PC is in pinnedAssets
  if (!targetAsset) {
    const station = stations.value.find(s => s.pinnedObjectHandle === handle)
    if (station && station.controllers && station.controllers.length > 0) {
      const controllerIds = station.controllers.map((c: any) => c.controllerId || c.id)
      targetAsset = pinnedAssets.value.find(a => a.type === 'Client PC' && controllerIds.includes(a.id))
    }
  }

  // 3. Fallback to any pinned asset with this handle (e.g. Machine)
  if (!targetAsset) {
    targetAsset = pinnedAssets.value.find(a => a.handle === handle)
  }

  if (targetAsset) {
    selectedAssetId.value = targetAsset.id
    await nextTick()
    const container = sidebarScrollContainer.value?.$el || sidebarScrollContainer.value
    const el = assetItemRefs.get(targetAsset.id) ||
      container?.querySelector?.(`[data-asset-id="${targetAsset.id}"]`) ||
      (typeof document !== 'undefined' ? document.getElementById(`pinned-asset-${targetAsset.id}`) : null)

    if (el && typeof (el as HTMLElement).scrollIntoView === 'function') {
      (el as HTMLElement).scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    }
  } else {
    selectedAssetId.value = null
  }
}

const handleObjectClick = (handle: string, blockName: string) => {
  activePin.value = handle
  activeBlockName.value = blockName
  scrollToAsset(handle)
}

const handleObjectDblClick = (handle: string, blockName: string) => {
  activePin.value = handle
  activeBlockName.value = blockName
  isPinningDialogOpen.value = true
  scrollToAsset(handle)
}

const handleMapClick = () => {
  activePin.value = null
  activeBlockName.value = null
  selectedAssetId.value = null
  expandedHandles.value = []
}

const selectAsset = (asset: any) => {
  activePin.value = asset.handle
  selectedAssetId.value = asset.id
}

const handlePinUpdate = async (type: 'machine' | 'client' | 'lateral', targetId: string, associatedIds: string[]) => {
  if (!activePin.value) return

  try {
    if (type === 'machine') {
      await updateStationPin(targetId, activePin.value, associatedIds)
    } else if (type === 'client') {
      await updateControllerPin(targetId, activePin.value, associatedIds)
    }
    await Promise.all([fetchStations(), fetchControllers()])
  } catch (e) {
    console.error('Failed to update spatial mapping:', e)
  }
}

const handleObjectContextMenu = (handle: string, blockName: string, e: MouseEvent, scrollSidebar = true) => {
  activePin.value = handle
  activeBlockName.value = blockName
  if (scrollSidebar) scrollToAsset(handle)

  const station = stations.value.find(s => s.pinnedObjectHandle === handle || s.name === handle || s.customIdentifier === handle)
  const controller = controllers.value.find(c => c.pinnedObjectHandle === handle || c.hostname === handle || c.name === handle)

  let resolvedController = controller
  if (!resolvedController && station?.controllers && station.controllers.length > 0) {
    const cid = station.controllers[0].controllerId || (station.controllers[0] as any).id
    resolvedController = controllers.value.find(c => c.id === cid)
  }

  let resolvedStation = station
  if (!resolvedStation && controller?.controlledMachines && controller.controlledMachines.length > 0) {
    const mid = controller.controlledMachines[0].id
    resolvedStation = stations.value.find(s => s.id === mid)
  }

  let ownerTeam: { id?: string; name: string } | undefined
  if (resolvedStation?.responsibleTeams && resolvedStation.responsibleTeams.length > 0) {
    ownerTeam = { name: resolvedStation.responsibleTeams[0].name }
  } else if (resolvedController?.responsibleTeams && resolvedController.responsibleTeams.length > 0) {
    ownerTeam = { name: resolvedController.responsibleTeams[0].name }
  }

  let ownerPerson: { id?: string; name: string } | undefined
  if ((resolvedStation as any)?.preferredTechnicianName) {
    ownerPerson = { name: (resolvedStation as any).preferredTechnicianName }
  } else if ((resolvedController as any)?.preferredTechnicianName) {
    ownerPerson = { name: (resolvedController as any).preferredTechnicianName }
  } else {
    const pref = resolvePreferredTechnician(resolvedStation?.id, (resolvedStation as any)?.machineType, undefined, [], [])
    if (pref?.technicianName) {
      ownerPerson = { name: pref.technicianName }
    }
  }

  const relevantTickets = tickets.value.filter(t => 
    (resolvedStation && (t.stationId === resolvedStation.id || t.stationId === resolvedStation.customIdentifier)) ||
    (t.tags && t.tags.includes(handle))
  )

  const contextData: ContextMenuContextData = {
    entityType: resolvedStation ? 'machine' : resolvedController ? 'controller' : 'map-node',
    entityId: resolvedStation?.id || resolvedController?.id || handle,
    entityName: resolvedStation?.name || resolvedController?.hostname || blockName || handle,
    handle,
    machineId: resolvedStation?.id,
    machineName: resolvedStation?.name || resolvedStation?.customIdentifier,
    controllerId: resolvedController?.id,
    controllerHostname: resolvedController?.hostname,
    ownerTeam,
    ownerPerson,
    tickets: relevantTickets.map(t => ({ id: t.id, title: t.title, status: t.status }))
  }

  openContextMenu(e, contextData)
}

const handleMapContextMenu = (e: MouseEvent) => {
  openContextMenu(e, {
    entityType: 'general',
    entityName: currentPlan.value.name,
    handle: currentPlan.value.id
  })
}

const handlePinnedAssetContextMenu = (asset: any, e: MouseEvent) => {
  // The right-clicked row is on screen already; scrolling the list would move it from under the menu.
  selectedAssetId.value = asset.id
  handleObjectContextMenu(asset.handle, asset.name, e, false)
}

const pinnedAssets = computed(() => {
  const list: any[] = []
  controllers.value.forEach(c => {
    if (c.pinnedObjectHandle) {
      list.push({ id: c.id, name: c.hostname || c.name, handle: c.pinnedObjectHandle, type: 'Client PC' })
    }
  })
  stations.value.forEach(m => {
    if (m.pinnedObjectHandle) {
      list.push({ id: m.id, name: m.customIdentifier || m.name, handle: m.pinnedObjectHandle, type: 'Machine' })
    }
  })
  return list
})

const activePinAssociations = computed(() => {
  if (!activePin.value) return []
  const asset = pinnedAssets.value.find(a => a.handle === activePin.value)
  if (!asset) return []

  if (asset.type === 'Machine') {
    const station = stations.value.find(s => s.id === asset.id)
    return station?.controllers?.map((c: any) => c.controllerId || c.id) || []
  } else {
    const pc = controllers.value.find(c => c.id === asset.id)
    return pc?.controlledMachines?.map((m: any) => m.id) || []
  }
})

const activePinEntityId = computed(() => {
  return pinnedAssets.value.find(a => a.handle === activePin.value)?.id || ''
})

const activePinType = computed(() => {
  const type = pinnedAssets.value.find(a => a.handle === activePin.value)?.type
  return type === 'Client PC' ? 'client' : 'machine'
})

// ── Deep link: ?stationId=<handle> (telemetry hub, context menu) ──────────────
// Selects the station on the plan and in the anchor list. The value is put on the map once per
// request; when the directories arrive later it is upgraded to the pinned asset, unless the user
// has selected something else in the meantime.
const focusRequest = computed(() => queryText(route?.query?.stationId))
let focusShown = ''
let focusResolved = ''

const applyFocusRequest = () => {
  const wanted = focusRequest.value
  if (!wanted) {
    focusShown = ''
    focusResolved = ''
    return
  }
  if (focusResolved === wanted) return
  const target = resolveMapFocus(wanted, stations.value, controllers.value)
  if (!target) return
  const isFirst = focusShown !== wanted
  const untouched = activePin.value === wanted || activePin.value === target.handle
  if (isFirst || (target.resolved && untouched)) {
    focusShown = wanted
    activePin.value = target.handle
    activeBlockName.value = target.name || null
  }
  if (target.resolved) {
    focusResolved = wanted
    if (isFirst || untouched) scrollToAsset(target.handle)
  }
}

watch([focusRequest, stations, controllers], applyFocusRequest, { immediate: true })

onMounted(() => {
  if (stations.value.length === 0) fetchStations()
  if (controllers.value.length === 0) fetchControllers()
  fetchTickets()
  fetchStationKpis()
})
</script>

<template>
  <div class="space-y-6 h-full text-foreground pb-12">
    <!-- Header with Floor Plan Dropdown Selector -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-border">
      <div
        role="button"
        tabindex="0"
        @click="resetMapView"
        @keydown.enter="resetMapView"
        class="flex items-center gap-3 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-muted/60"
        title="Click to reset map selection and reload CAD data"
      >
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-600 dark:text-indigo-400 group-hover:scale-105 group-hover:bg-indigo-500/20 transition-all">
          <MapIcon class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-foreground transition-colors">
            Plant Layout & Spatial CAD Mapping
          </h1>
          <p class="text-sm text-muted-foreground mt-0.5 transition-colors">
            Interactive AutoCAD (DXF) floor plan mapping of machines, sensors, and controllers
          </p>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <!-- Floor Plan Switcher -->
        <Popover>
          <PopoverTrigger as-child>
            <Button variant="outline" size="sm" class="bg-card border-border text-foreground rounded-lg h-8 px-3 text-xs font-medium flex items-center gap-2 hover:bg-muted transition-colors">
              <Layers class="size-3.5 text-indigo-600 dark:text-indigo-400" />
              <span>{{ currentPlan.name }}</span>
              <ChevronDown class="size-3 text-muted-foreground ml-1" />
            </Button>
          </PopoverTrigger>
          <PopoverContent align="end" class="w-80 p-1.5 bg-card border-border shadow-2xl text-foreground max-h-96 overflow-y-auto rounded-xl">
            <div class="px-3 py-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground border-b border-border mb-1">
              Select Plant CAD Drawing
            </div>
            <div
              v-for="plan in availableFloorPlans"
              :key="plan.id"
              @click="currentPlanId = plan.id"
              class="p-2 rounded-lg cursor-pointer transition-colors flex items-center justify-between group"
              :class="currentPlanId === plan.id ? 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-300 border border-indigo-500/30' : 'hover:bg-muted text-muted-foreground hover:text-foreground'"
            >
              <div class="flex flex-col">
                <span class="text-xs font-medium text-foreground">{{ plan.name }}</span>
                <span class="text-[11px] text-muted-foreground font-mono">{{ plan.url }}</span>
              </div>
              <span class="text-[10px] font-medium px-1.5 py-0.5 rounded bg-muted border border-border text-muted-foreground group-hover:text-foreground">
                {{ plan.badge }}
              </span>
            </div>
          </PopoverContent>
        </Popover>

        <div class="flex items-center gap-2 px-3 py-1 bg-emerald-500/10 border border-emerald-500/20 rounded-lg h-8">
          <span class="size-2 rounded-full bg-emerald-600 dark:bg-emerald-400 animate-pulse"></span>
          <span class="text-xs font-medium text-emerald-600 dark:text-emerald-400">Live Sync</span>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-4 gap-6 h-[calc(100vh-220px)]">
      <!-- Map Canvas Area -->
      <div class="lg:col-span-3 h-full rounded-xl overflow-hidden border border-border shadow-sm relative bg-card">
        <!-- Floating Telemetry HUD Toolbar -->
        <div class="absolute top-3 left-3 right-3 flex flex-wrap items-center justify-between gap-2 z-20 pointer-events-auto">
          <!-- Left: Overlay Mode Controls -->
          <div class="flex items-center gap-1.5 p-1 bg-slate-950/90 backdrop-blur-md rounded-xl border border-slate-800 shadow-xl">
            <!-- Master Toggle Button -->
            <button
              type="button"
              @click="showTelemetryOverlays = !showTelemetryOverlays"
              class="flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-semibold transition-all"
              :class="showTelemetryOverlays ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'"
            >
              <Activity class="size-3.5" :class="showTelemetryOverlays ? 'animate-pulse text-emerald-300' : ''" />
              <span>Telemetry HUD</span>
              <span
                class="size-1.5 rounded-full"
                :class="showTelemetryOverlays ? 'bg-emerald-400' : 'bg-slate-500'"
              ></span>
            </button>

            <!-- Mode Filter Pills -->
            <template v-if="showTelemetryOverlays">
              <div class="h-4 w-px bg-slate-800 mx-0.5"></div>

              <button
                type="button"
                @click="overlayFilterMode = 'alerts_only'"
                class="px-2 py-1 rounded-lg text-[11px] font-medium transition-colors"
                :class="overlayFilterMode === 'alerts_only' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40 font-semibold' : 'text-slate-400 hover:text-slate-200'"
                title="Display bubbles only on stations with active SLA tickets or OEE warnings"
              >
                Alerts & SLA
              </button>

              <button
                type="button"
                @click="overlayFilterMode = 'all'"
                class="px-2 py-1 rounded-lg text-[11px] font-medium transition-colors"
                :class="overlayFilterMode === 'all' ? 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/40 font-semibold' : 'text-slate-400 hover:text-slate-200'"
                title="Display telemetry bubbles on all visible station nodes"
              >
                All Nodes
              </button>

              <button
                type="button"
                @click="overlayFilterMode = 'hover_only'"
                class="px-2 py-1 rounded-lg text-[11px] font-medium transition-colors"
                :class="overlayFilterMode === 'hover_only' ? 'bg-slate-800 text-slate-200 font-semibold' : 'text-slate-400 hover:text-slate-200'"
                title="Show bubble only on hovered or selected station"
              >
                Hover Focus
              </button>

              <button
                type="button"
                @click="showOscillationBadge = !showOscillationBadge"
                class="px-2 py-1 rounded-lg text-[11px] font-medium transition-colors flex items-center gap-1"
                :class="showOscillationBadge ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 font-semibold' : 'text-slate-400 hover:text-slate-200'"
                title="Toggle sub-second machine oscillation & vibration readings"
              >
                <Radio class="size-3" :class="showOscillationBadge ? 'animate-pulse text-cyan-400' : ''" />
                <span>Dynamics</span>
              </button>
            </template>
          </div>

          <!-- Right: Fleet Stats Ticker -->
          <div class="hidden md:flex items-center gap-3 px-3 py-1 bg-slate-950/90 backdrop-blur-md rounded-xl border border-slate-800 shadow-xl text-xs font-mono">
            <template v-if="fleetKpiSummary.hasData">
              <div class="flex items-center gap-1.5 text-slate-300">
                <span class="text-slate-500">OEE:</span>
                <span class="font-bold text-emerald-400">{{ fleetKpiSummary.avgOee ?? '—' }}%</span>
              </div>
              <span class="text-slate-700">•</span>
              <div class="flex items-center gap-1.5 text-slate-300">
                <span class="text-slate-500">Yield:</span>
                <span class="font-bold text-cyan-400">{{ fleetKpiSummary.avgYield ?? '—' }}%</span>
              </div>
              <span class="text-slate-700">•</span>
              <div class="flex items-center gap-1.5 text-slate-300">
                <span class="text-slate-500">Rate:</span>
                <span class="font-bold text-indigo-400">{{ fleetKpiSummary.totalThroughput ?? '—' }} pcs/h</span>
              </div>
            </template>
            <div
              v-else
              class="flex items-center gap-1.5 text-slate-400"
              :title="stationKpisError || 'The backend has not reported any station KPIs yet'"
            >
              <span>{{ stationKpisError ? 'Telemetry unavailable' : 'No station telemetry' }}</span>
            </div>
            <span class="text-slate-700">•</span>
            <div class="flex items-center gap-1.5">
              <span class="text-slate-500">SLA:</span>
              <span
                class="font-bold px-1.5 py-0.2 rounded text-[11px]"
                :class="fleetKpiSummary.breachedCount > 0 ? 'bg-rose-500/20 text-rose-400 border border-rose-500/40' : 'bg-amber-500/20 text-amber-400 border border-amber-500/40'"
              >
                {{ fleetKpiSummary.totalSlaTickets }} Tickets
              </span>
            </div>
          </div>
        </div>

        <InteractiveMapCanvas
          :dxf-url="currentPlan.url"
          :active-pin="activePin"
          :show-telemetry-overlays="showTelemetryOverlays"
          :overlay-filter-mode="overlayFilterMode"
          :show-oee="showOeeBadge"
          :show-yield="showYieldBadge"
          :show-parts-hour="showPartsHourBadge"
          :show-sla="showSlaBadge"
          :show-oscillation="showOscillationBadge"
          :expanded-handles="expandedHandles"
          :station-kpis="stationKpis"
          :station-tickets="tickets"
          @object-clicked="handleObjectClick"
          @object-dblclicked="handleObjectDblClick"
          @object-contextmenu="handleObjectContextMenu"
          @map-clicked="handleMapClick"
          @map-contextmenu="handleMapContextMenu"
          @create-ticket="handleCreateTicket"
          @view-ticket="handleViewTicket"
        />

        <!-- Controls Legend Overlay -->
        <div class="absolute bottom-4 left-4 p-3 bg-card/90 backdrop-blur-md rounded-lg border border-border shadow-sm flex items-center gap-4 z-10 pointer-events-none text-xs">
          <div class="flex items-center gap-2">
            <div class="size-2.5 rounded-full bg-indigo-500 shadow-sm"></div>
            <span class="font-medium text-foreground">Interactive Handle</span>
          </div>
          <div class="flex items-center gap-2 border-l border-border pl-4">
            <span class="text-muted-foreground">Scroll to Zoom • Drag to Pan • Click Bubbles to Expand</span>
          </div>
        </div>
      </div>

      <!-- Spatial Sidebar -->
      <div class="h-full flex flex-col overflow-hidden">
        <Card class="border border-border shadow-sm flex-1 flex flex-col bg-card rounded-xl overflow-hidden">
          <CardHeader class="p-4 border-b border-border bg-muted/40">
            <CardTitle class="text-xs font-semibold text-muted-foreground uppercase tracking-wider flex items-center gap-2">
              <MapPinIcon class="size-3.5 text-indigo-600 dark:text-indigo-400" />
              Spatial Anchors ({{ pinnedAssets.length }})
            </CardTitle>
          </CardHeader>

          <!-- Active Station Live Telemetry Inspection Card -->
          <div v-if="activePin && !activeStationKpi" class="p-3 bg-muted/60 border-b border-border text-[11px] text-muted-foreground">
            <span class="font-mono font-bold text-foreground">{{ activePin }}</span>
            — no production telemetry reported for this station.
          </div>
          <div v-if="activeStationKpi" class="p-3 bg-muted/60 border-b border-border flex flex-col gap-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-foreground font-mono">{{ activePin }}</span>
              <span
                :class="{
                  'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border-emerald-500/30': activeStationKpi.operationalStatus === 'RUNNING',
                  'bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30': activeStationKpi.operationalStatus === 'IDLE' || activeStationKpi.operationalStatus === 'WARNING',
                  'bg-rose-500/15 text-rose-600 dark:text-rose-400 border-rose-500/30': activeStationKpi.operationalStatus === 'FAULTED'
                }"
                class="text-[9px] px-1.5 py-0.2 rounded font-semibold border uppercase"
              >
                {{ activeStationKpi.operationalStatus }}
              </span>
            </div>
            <p class="text-[11px] text-muted-foreground truncate">{{ activeStationKpi.name }}</p>

            <!-- At-A-Glance Stats Grid -->
            <div class="grid grid-cols-3 gap-1.5 p-2 bg-card rounded-lg border border-border text-center">
              <div>
                <span class="text-[9px] text-muted-foreground uppercase font-semibold">OEE</span>
                <p class="text-xs font-bold font-mono text-emerald-600 dark:text-emerald-400">{{ activeStationKpi.oee ?? '—' }}%</p>
              </div>
              <div class="border-l border-border">
                <span class="text-[9px] text-muted-foreground uppercase font-semibold">Yield</span>
                <p class="text-xs font-bold font-mono text-cyan-600 dark:text-cyan-400">{{ activeStationKpi.yield ?? '—' }}%</p>
              </div>
              <div class="border-l border-border">
                <span class="text-[9px] text-muted-foreground uppercase font-semibold">Rate</span>
                <p class="text-xs font-bold font-mono text-indigo-600 dark:text-indigo-400">{{ activeStationKpi.partsPerHour ?? '—' }}/h</p>
              </div>
            </div>

            <!-- Active Machine Sub-Second Dynamics -->
            <div v-if="activeStationOscillation" class="flex items-center justify-between p-1.5 bg-cyan-500/10 border border-cyan-500/20 rounded-lg text-[10px] font-mono">
              <div class="flex items-center gap-1.5 text-cyan-600 dark:text-cyan-300">
                <Radio class="size-3 animate-pulse text-cyan-500 shrink-0" />
                <span>Vib: <strong>{{ activeStationOscillation.vibrationMmS.toFixed(2) }}</strong> mm/s</span>
              </div>
              <span class="text-muted-foreground">{{ activeStationOscillation.spindleRpm !== null ? Math.round(activeStationOscillation.spindleRpm) : '—' }} RPM</span>
            </div>

            <!-- Active SLA Section -->
            <div v-if="activeStationSla && activeStationSla.openTickets > 0" class="flex items-center justify-between p-1.5 bg-amber-500/10 border border-amber-500/30 rounded-lg text-[10px]">
              <div class="flex items-center gap-1.5 text-amber-700 dark:text-amber-300 font-medium truncate mr-1">
                <AlertTriangle class="size-3 shrink-0" />
                <span>{{ activeStationSla.openTickets }} Active Tickets</span>
              </div>
              <span class="font-mono font-bold text-amber-600 dark:text-amber-400 shrink-0">{{ activeStationSla.earliestDueText }}</span>
            </div>
          </div>
          <CardContent ref="sidebarScrollContainer" class="p-0 overflow-y-auto flex-1 bg-muted/10">
            <div v-if="pinnedAssets.length === 0" class="p-10 text-center">
              <div class="size-10 bg-muted rounded-xl mx-auto mb-3 flex items-center justify-center text-muted-foreground">
                <MapPinIcon class="size-5" />
              </div>
              <p class="text-xs font-medium text-muted-foreground">No pins assigned yet</p>
              <p class="text-[11px] text-muted-foreground/70 mt-1">Click an object on the map to inspect or assign</p>
            </div>
            <div v-else class="divide-y divide-border/60">
              <div
                v-for="asset in pinnedAssets"
                :key="asset.id"
                :id="`pinned-asset-${asset.id}`"
                :data-asset-id="asset.id"
                :data-handle="asset.handle"
                :ref="(el) => setAssetItemRef(asset.id, el)"
                @click="selectAsset(asset)"
                @contextmenu="handlePinnedAssetContextMenu(asset, $event)"
                :class="(activePin === asset.handle || selectedAssetId === asset.id) ? 'bg-muted border-l-2 border-l-indigo-600 ring-1 ring-indigo-500/20' : 'hover:bg-muted/50'"
                class="p-4 transition-all cursor-pointer group"
              >
                <div class="flex justify-between items-start mb-1">
                  <p class="text-xs font-semibold text-foreground group-hover:text-indigo-600 dark:group-hover:text-indigo-300 transition-colors">{{ asset.name }}</p>
                  <span class="text-[10px] font-medium px-1.5 py-0.5 bg-muted text-indigo-700 dark:text-indigo-400 border border-border rounded">{{ asset.type }}</span>
                </div>
                <div class="flex items-center justify-between mt-1">
                  <span class="text-[11px] font-mono text-muted-foreground group-hover:text-foreground transition-colors">Ref: {{ asset.handle }}</span>
                  <MonitorIcon v-if="asset.type === 'Client PC'" class="size-3.5 text-muted-foreground" />
                  <Cpu v-else class="size-3.5 text-muted-foreground" />
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- Mapping Overlay Dialog -->
    <DashboardMapPinningDialog
      v-model:open="isPinningDialogOpen"
      :handle="activePin || ''"
      :object-name="activeBlockName || ''"
      :machines="stations"
      :clients="controllers"
      :initial-type="activePinType"
      :initial-id="activePinEntityId"
      :initial-associations="activePinAssociations"
      @pin="handlePinUpdate"
    />
  </div>
</template>
