<script setup lang="ts">
import { ref, onMounted, computed, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import InteractiveMapCanvas from '~/components/map/InteractiveMapCanvas.vue'
import MapPinningDialog from '~/components/dashboard/MapPinningDialog.vue'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { MonitorIcon, MapPinIcon, Cpu, Layers, ChevronDown, Map as MapIcon } from 'lucide-vue-next'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import { useStations } from '~/composables/useStations'
import { useControllers } from '~/composables/useControllers'
import { useGlobalContextMenu, type ContextMenuContextData } from '~/composables/useGlobalContextMenu'
import { useMaintenance } from '~/composables/useMaintenance'
import { resolvePreferredTechnician } from '~/utils/technicianInheritance'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()
const { stations, fetchStations, updateStationPin } = useStations()
const { controllers, fetchControllers, updateControllerPin } = useControllers()
const { openContextMenu } = useGlobalContextMenu()
const { tickets, fetchTickets } = useMaintenance()

const resetMapView = () => {
  activePin.value = null
  activeBlockName.value = null
  selectedAssetId.value = null
  currentPlanId.value = 'production_hall'
  router.push('/dashboard/map')
  fetchStations()
  fetchControllers()
  fetchTickets()
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

const handleObjectContextMenu = (handle: string, blockName: string, e: MouseEvent) => {
  activePin.value = handle
  activeBlockName.value = blockName
  scrollToAsset(handle)

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
  handleObjectContextMenu(asset.handle, asset.name, e)
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

onMounted(() => {
  if (stations.value.length === 0) fetchStations()
  if (controllers.value.length === 0) fetchControllers()
  fetchTickets()
})
</script>

<template>
  <div class="space-y-6 h-full text-slate-100 pb-12">
    <!-- Header with Floor Plan Dropdown Selector -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div
        role="button"
        tabindex="0"
        @click="resetMapView"
        @keydown.enter="resetMapView"
        class="flex items-center gap-3 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-slate-900/60"
        title="Click to reset map selection and reload CAD data"
      >
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 group-hover:scale-105 group-hover:bg-indigo-500/20 transition-all">
          <MapIcon class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-100 group-hover:text-white transition-colors">
            Plant Spatial Layout & CAD Mapping
          </h1>
          <p class="text-sm text-slate-400 mt-0.5 group-hover:text-slate-300 transition-colors">
            Interactive AutoCAD (DXF) floor plan mapping of machines, sensors, and controllers
          </p>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <!-- Floor Plan Switcher -->
        <Popover>
          <PopoverTrigger as-child>
            <Button variant="outline" size="sm" class="bg-slate-900 border-slate-800 text-slate-200 rounded-lg h-8 px-3 text-xs font-medium flex items-center gap-2 hover:bg-slate-800 transition-colors">
              <Layers class="size-3.5 text-indigo-400" />
              <span>{{ currentPlan.name }}</span>
              <ChevronDown class="size-3 text-slate-500 ml-1" />
            </Button>
          </PopoverTrigger>
          <PopoverContent align="end" class="w-80 p-1.5 bg-slate-950 border-slate-800 shadow-2xl text-slate-200 max-h-96 overflow-y-auto rounded-xl">
            <div class="px-3 py-1.5 text-xs font-semibold uppercase tracking-wider text-slate-400 border-b border-slate-800 mb-1">
              Select Plant CAD Drawing
            </div>
            <div
              v-for="plan in availableFloorPlans"
              :key="plan.id"
              @click="currentPlanId = plan.id"
              class="p-2 rounded-lg cursor-pointer transition-colors flex items-center justify-between group"
              :class="currentPlanId === plan.id ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/30' : 'hover:bg-slate-900 text-slate-400 hover:text-slate-200'"
            >
              <div class="flex flex-col">
                <span class="text-xs font-medium text-slate-200">{{ plan.name }}</span>
                <span class="text-[11px] text-slate-500 font-mono">{{ plan.url }}</span>
              </div>
              <span class="text-[10px] font-medium px-1.5 py-0.5 rounded bg-slate-900 border border-slate-800 text-slate-400 group-hover:text-indigo-300">
                {{ plan.badge }}
              </span>
            </div>
          </PopoverContent>
        </Popover>

        <div class="flex items-center gap-2 px-3 py-1 bg-emerald-500/10 border border-emerald-500/20 rounded-lg h-8">
          <span class="size-2 rounded-full bg-emerald-400 animate-pulse"></span>
          <span class="text-xs font-medium text-emerald-400">Live Sync</span>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-4 gap-6 h-[calc(100vh-220px)]">
      <!-- Map Canvas Area -->
      <div class="lg:col-span-3 h-full rounded-xl overflow-hidden border border-slate-800 shadow-sm relative bg-slate-900">
        <InteractiveMapCanvas
          :dxf-url="currentPlan.url"
          :active-pin="activePin"
          @object-clicked="handleObjectClick"
          @object-dblclicked="handleObjectDblClick"
          @object-contextmenu="handleObjectContextMenu"
          @map-clicked="handleMapClick"
          @map-contextmenu="handleMapContextMenu"
        />

        <!-- Controls Legend Overlay -->
        <div class="absolute bottom-4 left-4 p-3 bg-slate-900/90 backdrop-blur-md rounded-lg border border-slate-800 shadow-sm flex items-center gap-4 z-10 pointer-events-none text-xs">
          <div class="flex items-center gap-2">
            <div class="size-2.5 rounded-full bg-indigo-500 shadow-sm"></div>
            <span class="font-medium text-slate-300">Interactive Handle</span>
          </div>
          <div class="flex items-center gap-2 border-l border-slate-800 pl-4">
            <span class="text-slate-400">Scroll to Zoom • Drag to Pan • Double-click to Map</span>
          </div>
        </div>
      </div>

      <!-- Spatial Sidebar -->
      <div class="h-full flex flex-col overflow-hidden">
        <Card class="border border-slate-800 shadow-sm flex-1 flex flex-col bg-slate-900 rounded-xl overflow-hidden">
          <CardHeader class="p-4 border-b border-slate-800 bg-slate-950/60">
            <CardTitle class="text-xs font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-2">
              <MapPinIcon class="size-3.5 text-indigo-400" />
              Spatial Anchors ({{ pinnedAssets.length }})
            </CardTitle>
          </CardHeader>
          <CardContent ref="sidebarScrollContainer" class="p-0 overflow-y-auto flex-1 bg-slate-950/40">
            <div v-if="pinnedAssets.length === 0" class="p-10 text-center">
              <div class="size-10 bg-slate-800 rounded-xl mx-auto mb-3 flex items-center justify-center text-slate-500">
                <MapPinIcon class="size-5" />
              </div>
              <p class="text-xs font-medium text-slate-400">No pins assigned yet</p>
              <p class="text-[11px] text-slate-500 mt-1">Click an object on the map to inspect or assign</p>
            </div>
            <div v-else class="divide-y divide-slate-800/60">
              <div
                v-for="asset in pinnedAssets"
                :key="asset.id"
                :id="`pinned-asset-${asset.id}`"
                :data-asset-id="asset.id"
                :data-handle="asset.handle"
                :ref="(el) => setAssetItemRef(asset.id, el)"
                @click="selectAsset(asset)"
                @contextmenu.prevent="handlePinnedAssetContextMenu(asset, $event)"
                :class="(activePin === asset.handle || selectedAssetId === asset.id) ? 'bg-slate-800 border-l-2 border-l-indigo-500 ring-1 ring-indigo-500/20' : 'hover:bg-slate-800/40'"
                class="p-4 transition-all cursor-pointer group"
              >
                <div class="flex justify-between items-start mb-1">
                  <p class="text-xs font-semibold text-slate-200 group-hover:text-indigo-300 transition-colors">{{ asset.name }}</p>
                  <span class="text-[10px] font-medium px-1.5 py-0.5 bg-slate-800 text-indigo-400 rounded">{{ asset.type }}</span>
                </div>
                <div class="flex items-center justify-between mt-1">
                  <span class="text-[11px] font-mono text-slate-500 group-hover:text-slate-400 transition-colors">Ref: {{ asset.handle }}</span>
                  <MonitorIcon v-if="asset.type === 'Client PC'" class="size-3.5 text-slate-500" />
                  <Cpu v-else class="size-3.5 text-slate-500" />
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
