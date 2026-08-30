<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Button } from '@/components/ui/button'
import { 
  RefreshCw, Map, Grid, Plus, X, Layers, ChevronDown, 
  MapPin, CheckCircle2, Link2, Sparkles, ExternalLink 
} from 'lucide-vue-next'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import { useControllers } from '~/composables/useControllers'
import { useStations } from '~/composables/useStations'
import ControllerGrid from '~/components/controllers/ControllerGrid.vue'
import ControllerTelemetryCard from '~/components/controllers/ControllerTelemetryCard.vue'
import ControllerCommandModal from '~/components/controllers/ControllerCommandModal.vue'
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
const searchQuery = ref('')

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
  <div class="space-y-8 pb-12">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Industrial Controllers</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">
          Edge IPC Telemetry, Multi-Runtime Software Diagnostics, AutoCAD DXF Tag Linking & Signed Commands
        </p>
      </div>

      <div class="flex items-center gap-3">
        <!-- View Mode Switcher -->
        <div class="bg-slate-900 p-1.5 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button
            variant="ghost"
            @click="activeViewMode = 'grid'"
            :class="activeViewMode === 'grid' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <Grid class="w-3.5 h-3.5 mr-1.5" />
            Grid View
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'map'"
            :class="activeViewMode === 'map' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <Map class="w-3.5 h-3.5 mr-1.5" />
            Plant CAD Map
          </Button>
        </div>

        <Button
          variant="outline"
          @click="handleManualSync"
          :disabled="isLoading"
          class="bg-slate-900 border-slate-800 text-slate-300 rounded-2xl px-5 h-11 hover:bg-slate-800 text-xs font-bold uppercase tracking-wider transition-all"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="w-3.5 h-3.5 mr-2 text-indigo-400" />
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
        class="absolute top-4 right-4 flex items-center gap-1 px-3 py-1.5 rounded-xl bg-slate-950/80 border border-slate-800 text-[10px] font-black text-slate-400 hover:text-white hover:border-slate-700 transition-all uppercase tracking-wider shadow-lg"
      >
        <X class="w-3.5 h-3.5" />
        <span>Close Telemetry</span>
      </button>
    </div>

    <!-- Main View Mode: Grid View -->
    <template v-if="activeViewMode === 'grid'">
      <ControllerGrid
        :controllers="filteredControllers"
        :loading="isLoading"
        @select="handleSelectController"
        @queue-command="handleQueueCommand"
        @link-dxf="handleOpenPinDialogForPc"
        @locate-dxf="handleLocatePin"
      />
    </template>

    <!-- Main View Mode: Interactive CAD Plant Map View -->
    <template v-else>
      <div class="space-y-4 animate-in fade-in duration-200">
        
        <!-- Floor Plan CAD Switcher Toolbar -->
        <div class="p-4 bg-slate-900/90 border border-slate-800 rounded-3xl flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-xl">
          <div class="flex items-center gap-3">
            <div class="p-2 rounded-xl bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              <Layers class="size-4" />
            </div>
            <div>
              <span class="text-[9px] font-black uppercase tracking-widest text-slate-500 block">Active Plant CAD Layout:</span>
              <h4 class="text-xs font-black text-slate-200 uppercase tracking-wide">{{ currentPlan.name }}</h4>
            </div>
          </div>

          <div class="flex items-center gap-2.5">
            <!-- Floor Plan Switcher Popover -->
            <Popover>
              <PopoverTrigger as-child>
                <Button variant="outline" class="bg-slate-950 border-slate-800 text-slate-200 rounded-xl h-10 px-4 text-xs font-black uppercase tracking-wider flex items-center gap-2 hover:bg-slate-900">
                  <Layers class="w-3.5 h-3.5 text-indigo-400" />
                  <span>{{ currentPlan.name }}</span>
                  <ChevronDown class="w-3 h-3 text-slate-500 ml-1" />
                </Button>
              </PopoverTrigger>
              <PopoverContent align="end" class="w-80 p-2 bg-slate-950 border-slate-800 shadow-2xl text-slate-200 max-h-96 overflow-y-auto custom-scrollbar">
                <div class="px-3 py-2 text-[10px] font-black uppercase tracking-widest text-slate-500 border-b border-slate-900 mb-1">
                  Select Plant CAD Drawing
                </div>
                <div
                  v-for="plan in availableFloorPlans"
                  :key="plan.id"
                  @click="currentPlanId = plan.id"
                  class="p-2.5 rounded-xl cursor-pointer transition-colors flex items-center justify-between group"
                  :class="currentPlanId === plan.id ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/30' : 'hover:bg-slate-900 text-slate-400'"
                >
                  <div class="flex flex-col">
                    <span class="text-xs font-bold group-hover:text-white" :class="{ 'text-white': currentPlanId === plan.id }">{{ plan.name }}</span>
                    <span class="text-[9px] text-slate-600 font-mono">{{ plan.badge }}</span>
                  </div>
                  <CheckCircle2 v-if="currentPlanId === plan.id" class="w-3.5 h-3.5 text-indigo-400" />
                </div>
              </PopoverContent>
            </Popover>

            <!-- Manual Pin Button -->
            <Button
              variant="outline"
              size="sm"
              @click="handleMapObjectDblClick('', 'Custom CAD Coordinate')"
              class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl h-10 px-4 text-xs font-black uppercase tracking-wider flex items-center gap-1.5 shadow-lg shadow-indigo-600/20 border-0"
            >
              <MapPin class="size-3.5" />
              <span>+ Pin Controller</span>
            </Button>
          </div>
        </div>

        <!-- Interactive Map Canvas Container -->
        <div class="h-[620px] rounded-3xl overflow-hidden border border-slate-800 shadow-2xl relative bg-slate-950">
          <InteractiveMapCanvas
            :dxf-url="currentPlan.url"
            :highlighted-handles="searchedHandles"
            :active-pin="activeMapPin || selectedController?.pinnedObjectHandle"
            @object-clicked="handleMapObjectClick"
            @object-dblclicked="handleMapObjectDblClick"
          />

          <!-- Map Usage Helper Overlay Pill -->
          <div class="absolute bottom-4 left-4 pointer-events-none bg-slate-950/90 border border-slate-800/80 px-3.5 py-2 rounded-2xl shadow-xl flex items-center gap-2 text-[10px] text-slate-400 font-medium backdrop-blur-sm">
            <Sparkles class="size-3 text-indigo-400 shrink-0" />
            <span>Click any DXF tag block to open its telemetry, or double-click to link/re-pin to a Controller PC.</span>
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
  background: rgba(99, 102, 241, 0.5);
  border-radius: 9999px;
}
</style>