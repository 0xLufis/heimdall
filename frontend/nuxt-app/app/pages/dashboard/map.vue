<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import InteractiveMapCanvas from '~/components/map/InteractiveMapCanvas.vue'
import MapPinningDialog from '~/components/dashboard/MapPinningDialog.vue'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { MonitorIcon, MapPinIcon, Cpu, Layers, ChevronDown } from 'lucide-vue-next'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import { useStations } from '~/composables/useStations'
import { useControllers } from '~/composables/useControllers'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { stations, fetchStations, updateStationPin } = useStations()
const { controllers, fetchControllers, updateControllerPin } = useControllers()

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

const handleObjectClick = (handle: string, blockName: string) => {
  activePin.value = handle
  activeBlockName.value = blockName
}

const handleObjectDblClick = (handle: string, blockName: string) => {
  activePin.value = handle
  activeBlockName.value = blockName
  isPinningDialogOpen.value = true
}

const handleMapClick = () => {
  activePin.value = null
  activeBlockName.value = null
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
</script>

<template>
  <div class="space-y-8 h-full bg-slate-950 text-slate-100 pb-12">
    <!-- Header with Floor Plan Dropdown Selector -->
    <div class="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Plant Layout & Spatial CAD</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest leading-none">
          Interactive AutoCAD (DXF) Mapping of Machines, Sensors & Controllers
        </p>
      </div>

      <div class="flex items-center gap-3">
        <!-- Floor Plan Switcher -->
        <Popover>
          <PopoverTrigger as-child>
            <Button variant="outline" class="bg-slate-900 border-slate-800 text-slate-200 rounded-2xl h-11 px-4 text-xs font-black uppercase tracking-wider flex items-center gap-2 hover:bg-slate-800">
              <Layers class="w-4 h-4 text-indigo-400" />
              <span>{{ currentPlan.name }}</span>
              <ChevronDown class="w-3.5 h-3.5 text-slate-500 ml-1" />
            </Button>
          </PopoverTrigger>
          <PopoverContent align="end" class="w-80 p-2 bg-slate-950 border-slate-800 shadow-2xl text-slate-200 max-h-96 overflow-y-auto">
            <div class="px-3 py-2 text-[10px] font-black uppercase tracking-widest text-slate-500 border-b border-slate-900 mb-1">
              Select Plant CAD Drawing
            </div>
            <div
              v-for="plan in availableFloorPlans"
              :key="plan.id"
              @click="currentPlanId = plan.id"
              class="p-2.5 rounded-xl cursor-pointer transition-colors flex items-center justify-between group"
              :class="currentPlanId === plan.id ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/30' : 'hover:bg-slate-900 text-slate-400 hover:text-slate-200'"
            >
              <div class="flex flex-col">
                <span class="text-xs font-bold">{{ plan.name }}</span>
                <span class="text-[9px] text-slate-500 font-mono">{{ plan.url }}</span>
              </div>
              <span class="text-[8px] font-black uppercase px-2 py-0.5 rounded bg-slate-900 border border-slate-800 text-slate-400 group-hover:text-indigo-300">
                {{ plan.badge }}
              </span>
            </div>
          </PopoverContent>
        </Popover>

        <div class="flex items-center gap-2 px-4 py-2 bg-emerald-950/20 border border-emerald-900/30 rounded-2xl h-11">
          <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
          <span class="text-[10px] font-black text-emerald-500 uppercase tracking-widest">Live Sync</span>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-4 gap-8 h-[calc(100vh-220px)]">
      <!-- Map Canvas Area -->
      <div class="lg:col-span-3 h-full rounded-3xl overflow-hidden border border-slate-800 shadow-2xl relative bg-slate-900">
        <InteractiveMapCanvas
          :dxf-url="currentPlan.url"
          :active-pin="activePin"
          @object-clicked="handleObjectClick"
          @object-dblclicked="handleObjectDblClick"
          @map-clicked="handleMapClick"
        />

        <!-- Controls Legend Overlay -->
        <div class="absolute bottom-6 left-6 p-4 bg-slate-900/90 backdrop-blur-md rounded-2xl border border-slate-800 shadow-xl flex gap-6 z-10 pointer-events-none">
          <div class="flex items-center gap-2">
            <div class="w-3 h-3 rounded-full bg-indigo-500 shadow-[0_0_8px_rgba(99,102,241,0.5)]"></div>
            <span class="text-[9px] font-black uppercase tracking-widest text-slate-400">Interactive Object Handle</span>
          </div>
          <div class="flex items-center gap-2 border-l border-slate-800 pl-6">
            <span class="text-[9px] font-black uppercase tracking-widest text-slate-500">Scroll to Zoom • Drag to Pan</span>
          </div>
        </div>
      </div>

      <!-- Spatial Sidebar -->
      <div class="h-full flex flex-col gap-6 overflow-hidden">
        <Card class="border-none shadow-sm flex-1 flex flex-col bg-slate-900/50 rounded-3xl overflow-hidden">
          <CardHeader class="pb-4 border-b border-slate-800 bg-slate-900">
            <CardTitle class="text-[10px] font-black text-slate-400 uppercase tracking-widest flex items-center gap-2">
              <MapPinIcon class="h-3.5 w-3.5 text-indigo-400" />
              Spatial Anchors ({{ pinnedAssets.length }})
            </CardTitle>
          </CardHeader>
          <CardContent class="p-0 overflow-y-auto flex-1 bg-slate-950/20">
            <div v-if="pinnedAssets.length === 0" class="p-12 text-center">
              <div class="w-12 h-12 bg-slate-900 rounded-2xl mx-auto mb-4 flex items-center justify-center">
                <MapPinIcon class="h-6 w-6 text-slate-700" />
              </div>
              <p class="text-[10px] font-black text-slate-600 uppercase tracking-widest">No pins assigned yet</p>
              <p class="text-[9px] text-slate-700 mt-2">Click an object on the map to start pinning</p>
            </div>
            <div v-else class="divide-y divide-slate-800">
              <div
                v-for="asset in pinnedAssets"
                :key="asset.id"
                @click="activePin = asset.handle"
                :class="activePin === asset.handle ? 'bg-slate-800 border-l-4 border-l-indigo-500' : 'hover:bg-slate-900/50'"
                class="p-5 transition-all cursor-pointer group"
              >
                <div class="flex justify-between items-start mb-1">
                  <p class="text-sm font-black text-slate-200 group-hover:text-white transition-colors">{{ asset.name }}</p>
                  <span class="text-[8px] font-black uppercase tracking-widest px-1.5 py-0.5 bg-slate-800 text-indigo-400 rounded">{{ asset.type }}</span>
                </div>
                <div class="flex items-center justify-between mt-1">
                  <span class="text-[10px] font-mono text-slate-500 group-hover:text-slate-400 transition-colors">Ref: {{ asset.handle }}</span>
                  <MonitorIcon v-if="asset.type === 'Client PC'" class="h-3.5 w-3.5 text-slate-600" />
                  <Cpu v-else class="h-3.5 w-3.5 text-slate-600" />
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
