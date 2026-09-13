<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Button } from '@/components/ui/button'
import { 
  RefreshCw, Map, Grid, Plus, X, Layers, ChevronDown, 
  MapPin, CheckCircle2, Link2, Sparkles, ExternalLink, Monitor 
} from 'lucide-vue-next'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import { useControllers } from '~/composables/useControllers'
import { useStations } from '~/composables/useStations'
import ControllerGrid from '~/components/controllers/ControllerGrid.vue'
import ControllerTelemetryCard from '~/components/controllers/ControllerTelemetryCard.vue'
import ControllerCommandModal from '~/components/controllers/ControllerCommandModal.vue'
import RemoteQuickViewModal from '~/components/controllers/RemoteQuickViewModal.vue'
import InteractiveMapCanvas from '~/components/map/InteractiveMapCanvas.vue'
import MapPinningDialog from '~/components/dashboard/MapPinningDialog.vue'
import OmniSearchBar from '~/components/search/OmniSearchBar.vue'
import type { IndustrialController } from '~/types/domain'
import type { SearchInstanceConfig } from '~/types/search'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { controllers, isLoading, lastSyncedAt, fetchControllers, updateControllerPin } = useControllers()
const { stations, fetchStations, updateStationPin } = useStations()
const router = useRouter()
const route = useRoute()

const activeViewMode = ref<'grid' | 'map'>('grid')
const selectedController = ref<IndustrialController | null>(null)
const commandTargetController = ref<IndustrialController | null>(null)
const isCommandModalOpen = ref(false)
const quickViewTargetController = ref<IndustrialController | null>(null)
const isQuickViewModalOpen = ref(false)
const searchQuery = ref('')

const resetClientsView = () => {
  searchQuery.value = ''
  selectedController.value = null
  activeMapPin.value = null
  activeViewMode.value = 'grid'
  router.push('/dashboard/clients')
  handleManualSync()
}

const handleQuickView = (pc: IndustrialController) => {
  quickViewTargetController.value = pc
  isQuickViewModalOpen.value = true
}

// Plant CAD Floor Plans Catalog
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

// Map Pinning & Linking State
const isPinningDialogOpen = ref(false)
const pinningHandle = ref('')
const pinningObjectName = ref('')
const pinningInitialId = ref('')
const pinningInitialType = ref<'client' | 'machine'>('client')
const pinningInitialAssociations = ref<string[]>([])
const activeMapPin = ref<string | null>(null)

const clientsSearchConfig: SearchInstanceConfig = {
  instanceId: 'clients',
  placeholder: 'Filter controllers by hostname, MAC, IP, or status (e.g. status:online)...',
  defaultEndpoints: ['/api/proxy/ClientPc'],
  defaultTags: [],
  enableAutoTagging: true
}

const filteredControllers = computed(() => {
  if (!searchQuery.value) return controllers.value
  const q = searchQuery.value.toLowerCase()
  return controllers.value.filter(c =>
    c.hostname.toLowerCase().includes(q) ||
    c.macAddress.toLowerCase().includes(q) ||
    (c.ipAddress && c.ipAddress.toLowerCase().includes(q)) ||
    (c.pinnedObjectHandle && c.pinnedObjectHandle.toLowerCase().includes(q))
  )
})

const searchedHandles = computed(() => {
  const list: string[] = []
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    controllers.value.forEach(c => {
      if (
        c.hostname.toLowerCase().includes(q) ||
        c.macAddress.toLowerCase().includes(q) ||
        (c.pinnedObjectHandle && c.pinnedObjectHandle.toLowerCase().includes(q))
      ) {
        if (c.pinnedObjectHandle) list.push(c.pinnedObjectHandle)
        list.push(c.hostname)
      }
    })
  }
  if (activeMapPin.value && !list.includes(activeMapPin.value)) {
    list.push(activeMapPin.value)
  }
  if (selectedController.value?.pinnedObjectHandle && !list.includes(selectedController.value.pinnedObjectHandle)) {
    list.push(selectedController.value.pinnedObjectHandle)
  }
  return list
})

const handleSelectController = (pc: IndustrialController) => {
  selectedController.value = pc
  router.replace({
    query: { ...route.query, selected: pc.id, hostname: pc.hostname }
  })
}

const handleCloseTelemetry = () => {
  selectedController.value = null
  router.replace({
    query: { ...route.query, selected: undefined, hostname: undefined }
  })
}

// Open Pinning Dialog directly for a Controller PC
const handleOpenPinDialogForPc = (pc: IndustrialController) => {
  pinningInitialType.value = 'client'
  pinningInitialId.value = pc.id
  pinningHandle.value = pc.pinnedObjectHandle || ''
  pinningObjectName.value = pc.hostname || pc.name
  pinningInitialAssociations.value = pc.controlledMachines ? pc.controlledMachines.map(m => m.id) : []
  isPinningDialogOpen.value = true
}

// Switch to Plant Map view and focus/highlight a specific DXF handle
const handleLocatePin = (handle: string) => {
  activeViewMode.value = 'map'
  activeMapPin.value = handle
  const match = controllers.value.find(c => c.pinnedObjectHandle === handle)
  if (match) selectedController.value = match
}

// Click on Map Entity
const handleMapObjectClick = (handle: string, blockName: string) => {
  activeMapPin.value = handle
  const match = controllers.value.find(c => c.pinnedObjectHandle === handle || c.hostname === handle)
  if (match) {
    selectedController.value = match
  }
}

// Double-click on Map Entity -> Opens Spatial Pinning Dialog for that DXF Block
const handleMapObjectDblClick = (handle: string, blockName: string) => {
  activeMapPin.value = handle
  pinningHandle.value = handle
  pinningObjectName.value = blockName

  const matchedController = controllers.value.find(c => c.pinnedObjectHandle === handle)
  const matchedStation = stations.value.find(s => s.pinnedObjectHandle === handle)

  if (matchedController) {
    pinningInitialType.value = 'client'
    pinningInitialId.value = matchedController.id
    pinningInitialAssociations.value = matchedController.controlledMachines?.map(m => m.id) || []
  } else if (matchedStation) {
    pinningInitialType.value = 'machine'
    pinningInitialId.value = matchedStation.id
    pinningInitialAssociations.value = matchedStation.controllers?.map((c: any) => c.controllerId || c.id) || []
  } else {
    pinningInitialType.value = 'client'
    pinningInitialId.value = selectedController.value?.id || ''
    pinningInitialAssociations.value = []
  }

  isPinningDialogOpen.value = true
}

// Save spatial mapping from Pinning Dialog
const handlePinUpdate = async (type: 'machine' | 'client' | 'lateral', targetId: string, associatedIds: string[], customHandle?: string) => {
  const handleToUse = customHandle || pinningHandle.value
  if (!handleToUse && type !== 'lateral') return

  try {
    if (type === 'client') {
      await updateControllerPin(targetId, handleToUse, associatedIds)
    } else if (type === 'machine') {
      await updateStationPin(targetId, handleToUse, associatedIds)
    }
    await Promise.all([fetchControllers(true), fetchStations()])
    activeMapPin.value = handleToUse
  } catch (e) {
    console.error('Failed to update spatial DXF pin:', e)
  }
}

// Unpin DXF handle from controller
const handleUnpinPc = async (controller: IndustrialController) => {
  try {
    await updateControllerPin(controller.id, '', [])
    await fetchControllers(true)
  } catch (e) {
    console.error('Failed to unpin controller:', e)
  }
}

const handleUnpinFromDialog = async (type: 'machine' | 'client', targetId: string) => {
  try {
    if (type === 'client') {
      await updateControllerPin(targetId, '', [])
    } else {
      await updateStationPin(targetId, '', [])
    }
    await Promise.all([fetchControllers(true), fetchStations()])
  } catch (e) {
    console.error('Failed to unpin node:', e)
  }
}

const handleQueueCommand = (pc: IndustrialController) => {
  commandTargetController.value = pc
  isCommandModalOpen.value = true
}

const handleManualSync = async () => {
  await Promise.all([fetchControllers(false), fetchStations()])
}

onMounted(async () => {
  await Promise.all([fetchControllers(false), fetchStations()])
  if (route.query.selected || route.query.hostname) {
    const targetId = route.query.selected as string
    const targetHost = route.query.hostname as string
    const match = controllers.value.find(c => c.id === targetId || c.hostname === targetHost)
    if (match) {
      selectedController.value = match
    } else if (targetHost) {
      searchQuery.value = targetHost
    }
  }
})

// Keep selected controller telemetry updated without auto-reopening if dismissed
watch(() => controllers.value, (list) => {
  if (selectedController.value) {
    const updated = list.find(c => c.id === selectedController.value?.id)
    if (updated) selectedController.value = updated
  } else if (route.query.selected && !selectedController.value) {
    const match = list.find(c => c.id === route.query.selected || c.hostname === route.query.hostname)
    if (match) selectedController.value = match
  }
})

const onSearch = (q: string) => {
  searchQuery.value = q
}
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Page Header -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div
        role="button"
        tabindex="0"
        @click="resetClientsView"
        @keydown.enter="resetClientsView"
        class="flex items-center gap-3 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-slate-900/60"
        title="Click to reset filters and refresh controller fleet"
      >
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 group-hover:scale-105 group-hover:bg-indigo-500/20 transition-all">
          <Monitor class="h-6 w-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-100 group-hover:text-white transition-colors">Industrial Controller Fleet</h1>
          <p class="text-sm text-slate-400 mt-0.5 group-hover:text-slate-300 transition-colors">
            Edge IPC telemetry, multi-runtime diagnostics, AutoCAD DXF tag linking, and signed commands
          </p>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <!-- View Mode Switcher -->
        <div class="bg-slate-900 p-1 rounded-lg border border-slate-800 shadow-sm flex gap-1">
          <Button
            variant="ghost"
            size="sm"
            @click="activeViewMode = 'grid'"
            :class="activeViewMode === 'grid' ? 'bg-zinc-700 text-white shadow-sm' : 'text-slate-400 hover:text-white hover:bg-zinc-800/80'"
            class="h-8 px-3 rounded-md text-xs font-medium transition-all"
          >
            <Grid class="w-3.5 h-3.5 mr-1.5" />
            Grid View
          </Button>

          <Button
            variant="ghost"
            size="sm"
            @click="activeViewMode = 'map'"
            :class="activeViewMode === 'map' ? 'bg-zinc-700 text-white shadow-sm' : 'text-slate-400 hover:text-white hover:bg-zinc-800/80'"
            class="h-8 px-3 rounded-md text-xs font-medium transition-all"
          >
            <Map class="w-3.5 h-3.5 mr-1.5" />
            Plant CAD Map
          </Button>
        </div>

        <Button
          variant="outline"
          size="sm"
          @click="handleManualSync"
          :disabled="isLoading"
          class="h-8 bg-slate-900 border-slate-800 hover:border-zinc-600 text-slate-300 hover:text-white rounded-lg px-3.5 hover:bg-zinc-800 text-xs font-medium transition-all shadow-xs hover:shadow-sm"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="w-3.5 h-3.5 mr-2 text-zinc-400" />
          <span>{{ isLoading ? 'Syncing Fleet...' : 'Sync Telemetry' }}</span>
        </Button>
      </div>
    </div>

    <!-- OmniSearch Bar -->
    <div class="max-w-4xl mx-auto w-full">
      <OmniSearchBar
        :config="clientsSearchConfig"
        :immediate="true"
        @search="onSearch"
      />
    </div>

    <!-- Selected Controller Telemetry Highlight Drawer/Card -->
    <div v-if="selectedController" class="relative animate-in fade-in slide-in-from-top-3 duration-300">
      <ControllerTelemetryCard 
        :controller="selectedController"
        @link-dxf="handleOpenPinDialogForPc"
        @locate-map="handleLocatePin"
        @unpin-dxf="handleUnpinPc"
      />
      <button
        type="button"
        @click="handleCloseTelemetry"
        class="absolute top-4 right-4 flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-900/90 border border-slate-800 hover:border-zinc-500 text-xs font-medium text-slate-400 hover:text-white hover:bg-zinc-800 transition-all shadow-md hover:shadow-lg"
      >
        <X class="w-3.5 h-3.5" />
        <span>Close Telemetry</span>
      </button>
    </div>

    <!-- Main View Mode: Grid View -->
    <template v-if="activeViewMode === 'grid'">
      <ControllerGrid
        :controllers="filteredControllers"
        :selected-id="selectedController?.id"
        :loading="isLoading"
        @select="handleSelectController"
        @queue-command="handleQueueCommand"
        @link-dxf="handleOpenPinDialogForPc"
        @locate-dxf="handleLocatePin"
        @quick-view="handleQuickView"
      />
    </template>

    <!-- Main View Mode: Interactive CAD Plant Map View -->
    <template v-else>
      <div class="space-y-4 animate-in fade-in duration-200">
        
        <!-- Floor Plan CAD Switcher Toolbar -->
        <div class="p-3.5 bg-slate-900 border border-slate-800 rounded-xl flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-sm">
          <div class="flex items-center gap-3">
            <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
              <Layers class="size-4" />
            </div>
            <div>
              <span class="text-xs text-slate-400 block">Active Plant CAD Layout</span>
              <h4 class="text-sm font-semibold text-slate-200">{{ currentPlan.name }}</h4>
            </div>
          </div>

          <div class="flex items-center gap-2">
            <!-- Floor Plan Switcher Popover -->
            <Popover>
              <PopoverTrigger as-child>
                <Button variant="outline" size="sm" class="bg-slate-950 border-slate-800 text-slate-200 rounded-lg h-8 px-3 text-xs font-medium flex items-center gap-2 hover:bg-slate-900">
                  <Layers class="w-3.5 h-3.5 text-indigo-400" />
                  <span>{{ currentPlan.name }}</span>
                  <ChevronDown class="w-3 h-3 text-slate-400 ml-1" />
                </Button>
              </PopoverTrigger>
              <PopoverContent align="end" class="w-80 p-2 bg-slate-950 border-slate-800 shadow-xl text-slate-200 max-h-96 overflow-y-auto custom-scrollbar">
                <div class="px-3 py-1.5 text-xs font-semibold text-slate-400 border-b border-slate-800/80 mb-1">
                  Select Plant CAD Drawing
                </div>
                <div
                  v-for="plan in availableFloorPlans"
                  :key="plan.id"
                  @click="currentPlanId = plan.id"
                  class="p-2 rounded-lg cursor-pointer transition-colors flex items-center justify-between group"
                  :class="currentPlanId === plan.id ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/30' : 'hover:bg-slate-900 text-slate-400'"
                >
                  <div class="flex flex-col">
                    <span class="text-xs font-medium group-hover:text-white" :class="{ 'text-white': currentPlanId === plan.id }">{{ plan.name }}</span>
                    <span class="text-[11px] text-slate-500 font-mono">{{ plan.badge }}</span>
                  </div>
                  <CheckCircle2 v-if="currentPlanId === plan.id" class="w-3.5 h-3.5 text-indigo-400" />
                </div>
              </PopoverContent>
            </Popover>

            <!-- Manual Pin Button -->
            <Button
              size="sm"
              @click="handleMapObjectDblClick('', 'Custom CAD Coordinate')"
              class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg h-8 px-3 text-xs font-medium flex items-center gap-1.5 shadow-sm"
            >
              <MapPin class="size-3.5" />
              <span>Pin Controller</span>
            </Button>
          </div>
        </div>

        <!-- Interactive Map Canvas Container -->
        <div class="h-[620px] rounded-xl overflow-hidden border border-slate-800 shadow-sm relative bg-slate-950">
          <InteractiveMapCanvas
            :dxf-url="currentPlan.url"
            :highlighted-handles="searchedHandles"
            :active-pin="activeMapPin || selectedController?.pinnedObjectHandle"
            @object-clicked="handleMapObjectClick"
            @object-dblclicked="handleMapObjectDblClick"
          />

          <!-- Map Usage Helper Overlay Pill -->
          <div class="absolute bottom-4 left-4 pointer-events-none bg-slate-900/90 border border-slate-800 px-3 py-1.5 rounded-lg shadow-md flex items-center gap-2 text-xs text-slate-300 backdrop-blur-sm">
            <Sparkles class="size-3.5 text-indigo-400 shrink-0" />
            <span>Click any DXF tag block to open telemetry, or double-click to link a controller.</span>
          </div>
        </div>
      </div>
    </template>

    <!-- Signed Command Modal -->
    <ControllerCommandModal
      :controller="commandTargetController"
      :open="isCommandModalOpen"
      @update:open="isCommandModalOpen = $event"
      @submitted="fetchControllers(false)"
    />

    <!-- Remote Quick View Modal (VNC / DameWare MRC / RDP) -->
    <RemoteQuickViewModal
      :controller="quickViewTargetController"
      :open="isQuickViewModalOpen"
      @update:open="isQuickViewModalOpen = $event"
    />

    <!-- Spatial DXF Coordinate Mapping & Pinning Modal Dialog -->
    <MapPinningDialog
      :open="isPinningDialogOpen"
      :handle="pinningHandle"
      :object-name="pinningObjectName"
      :machines="stations"
      :clients="controllers"
      :initial-type="pinningInitialType"
      :initial-id="pinningInitialId"
      :initial-associations="pinningInitialAssociations"
      @update:open="isPinningDialogOpen = $event"
      @pin="handlePinUpdate"
      @unpin="handleUnpinFromDialog"
    />
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  width: 5px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(15, 23, 42, 0.4);
  border-radius: 9999px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(130, 143, 159, 0.4);
  border-radius: 9999px;
}
</style>