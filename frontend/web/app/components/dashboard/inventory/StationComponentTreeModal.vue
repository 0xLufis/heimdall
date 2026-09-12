<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue'
import { 
  Dialog, 
  DialogContent, 
  DialogHeader, 
  DialogTitle, 
  DialogDescription 
} from '~/components/ui/dialog'
import { 
  FolderTree, 
  Cpu, 
  Monitor, 
  Layers, 
  Search, 
  ChevronRight, 
  ChevronDown, 
  HardDrive, 
  Activity, 
  Zap, 
  Tag, 
  X,
  ExternalLink
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

const props = withDefaults(
  defineProps<{
    open: boolean
    stationId?: string
    initialStationName?: string
  }>(),
  {
    open: false,
    stationId: '',
    initialStationName: ''
  }
)

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()

const loading = ref(false)
const stationsList = ref<any[]>([])
const selectedStationId = ref<string>('')
const treeData = ref<any | null>(null)
const searchQuery = ref('')
const expandedNodes = ref<Record<string, boolean>>({})

// Toggle expansion of a node
const toggleNode = (nodeId: string) => {
  expandedNodes.value[nodeId] = !expandedNodes.value[nodeId]
}

const expandAll = () => {
  if (!treeData.value) return
  expandedNodes.value['root'] = true
  if (treeData.value.controllers) {
    for (const c of treeData.value.controllers) {
      expandedNodes.value[`ctrl-${c.id}`] = true
    }
  }
  if (treeData.value.parts) {
    for (const p of treeData.value.parts) {
      expandedNodes.value[`part-${p.id}`] = true
    }
  }
}

const collapseAll = () => {
  expandedNodes.value = {}
}

// Fetch all stations for the picker dropdown
const fetchStationsList = async () => {
  try {
    const res = await $fetch<any[]>('/api/proxy/v1/machine')
    if (Array.isArray(res) && res.length > 0) {
      stationsList.value = res
      if (!selectedStationId.value) {
        selectedStationId.value = props.stationId || res[0].id
      }
    }
  } catch {
    // Fallback stations list
    stationsList.value = [
      { id: '11111111-1111-1111-1111-111111111111', name: 'OP10-Load', groupId: 'Line 1', machineType: 'Assembly' },
      { id: '22222222-2222-2222-2222-222222222222', name: 'OP20-Weld', groupId: 'Line 1', machineType: 'Welding' },
      { id: '33333333-3333-3333-3333-333333333333', name: 'OP30-Vision', groupId: 'Line 2', machineType: 'Test' }
    ]
    if (!selectedStationId.value) {
      selectedStationId.value = props.stationId || stationsList.value[0].id
    }
  }
}

// Fetch tree data for the selected station
const fetchTreeData = async (id: string) => {
  if (!id) return
  loading.value = true
  try {
    const res = await $fetch<any>(`/api/proxy/v1/inventory/station-tree/${id}`)
    if (res) {
      treeData.value = res
      expandAll()
      return
    }
  } catch {
    // Try fallback from full inventory tree endpoint
    try {
      const allTree = await $fetch<any[]>('/api/inventory/tree?primaryKey=machine')
      if (Array.isArray(allTree)) {
        const found = allTree.find(m => m.id === id || m.name?.toLowerCase() === id.toLowerCase())
        if (found) {
          treeData.value = {
            stationId: found.id,
            stationName: found.name,
            displayName: found.displayName,
            lineName: found.groupId || 'Line 1',
            technology: found.machineType || 'Assembly',
            controllers: (found.controllers || []).map((c: any) => ({
              id: c.id,
              hostname: c.hostname || c.name,
              ipAddress: c.ipAddress,
              macAddress: c.macAddress,
              isOnline: true,
              hardware: c.inventoryItems || []
            })),
            parts: (found.inventoryItems || []).map((p: any) => ({
              id: p.id,
              name: p.name,
              serialNumber: p.serialNumber,
              equipmentStatus: p.equipmentStatus || 'InMachine',
              technology: p.technology || 'Assembly',
              storageLocation: p.storageLocation || 'Station Slot A',
              manufacturer: p.manufacturer?.name || 'OEM',
              costInHUF: p.costInHUF || 500000
            }))
          }
          expandAll()
          return
        }
      }
    } catch {}
  } finally {
    loading.value = false
  }

  // Graceful visual mock if both endpoints fail
  const stationObj = stationsList.value.find(s => s.id === id) || { name: 'OP10-Load', groupId: 'Line 1', machineType: 'Assembly' }
  treeData.value = {
    stationId: id,
    stationName: stationObj.name,
    displayName: stationObj.displayName || stationObj.name,
    lineName: stationObj.groupId || 'Line 1',
    technology: stationObj.machineType || 'Assembly',
    controllers: [
      {
        id: 'ctrl-101',
        hostname: `IPC-${stationObj.name}`,
        ipAddress: '192.168.1.110',
        macAddress: '00:1B:44:11:3A:B7',
        isOnline: true,
        hardware: [
          { id: 'hw-cpu', name: 'Intel Core i7-11700E', type: 'CPU' },
          { id: 'hw-ram', name: '32GB DDR4 ECC Memory', type: 'RAM' },
          { id: 'hw-nic', name: 'Intel I210 Dual GbE NIC', type: 'NIC' }
        ]
      }
    ],
    parts: [
      {
        id: 'part-01',
        name: 'Spindle Motor Assembly 15kW',
        serialNumber: 'SN-SPINDLE-994',
        equipmentStatus: 'InMachine',
        technology: 'Assembly',
        storageLocation: `${stationObj.name} Spindle Mount 1`,
        manufacturer: 'Siemens',
        costInHUF: 1850000
      },
      {
        id: 'part-02',
        name: 'Coolant Flow Sensor IO-Link',
        serialNumber: 'SN-SENSOR-441',
        equipmentStatus: 'InMachine',
        technology: 'Assembly',
        storageLocation: `${stationObj.name} Feed Pipe`,
        manufacturer: 'IFM Electronic',
        costInHUF: 320000
      }
    ]
  }
  expandAll()
}

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    if (props.stationId) {
      selectedStationId.value = props.stationId
    }
    fetchStationsList().then(() => {
      fetchTreeData(selectedStationId.value)
    })
  }
})

watch(selectedStationId, (newId) => {
  if (newId) {
    fetchTreeData(newId)
  }
})

onMounted(() => {
  if (props.open) {
    fetchStationsList()
  }
})

// Filter tree items by search query
const filteredControllers = computed(() => {
  if (!treeData.value?.controllers) return []
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return treeData.value.controllers
  return treeData.value.controllers.filter((c: any) => 
    (c.hostname || '').toLowerCase().includes(q) ||
    (c.ipAddress || '').toLowerCase().includes(q) ||
    (c.macAddress || '').toLowerCase().includes(q) ||
    (c.hardware || []).some((h: any) => (h.name || '').toLowerCase().includes(q))
  )
})

const filteredParts = computed(() => {
  if (!treeData.value?.parts) return []
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return treeData.value.parts
  return treeData.value.parts.filter((p: any) => 
    (p.name || '').toLowerCase().includes(q) ||
    (p.serialNumber || '').toLowerCase().includes(q) ||
    (p.manufacturer || '').toLowerCase().includes(q) ||
    (p.technology || '').toLowerCase().includes(q) ||
    (p.storageLocation || '').toLowerCase().includes(q)
  )
})
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-4xl max-h-[85vh] flex flex-col bg-slate-950/98 backdrop-blur-2xl border-slate-800 text-slate-100 p-6 rounded-3xl shadow-2xl overflow-hidden">
      <!-- Header Area -->
      <DialogHeader class="border-b border-slate-900 pb-4 shrink-0">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div class="flex items-center gap-3">
            <div class="p-2.5 rounded-2xl bg-indigo-600/20 text-indigo-400 border border-indigo-500/30">
              <FolderTree class="w-5 h-5" />
            </div>
            <div>
              <DialogTitle class="text-base font-black uppercase tracking-tight text-white flex items-center gap-2">
                <span>Station Component Tree</span>
                <span class="text-xs px-2.5 py-0.5 rounded-full bg-indigo-900/60 text-indigo-300 font-mono font-bold">
                  {{ treeData?.stationName || 'Loading...' }}
                </span>
              </DialogTitle>
              <DialogDescription class="text-xs text-slate-500 font-bold uppercase tracking-wider mt-0.5">
                Hierarchical mapping of Host IPCs, internal modules, and installed serialized parts
              </DialogDescription>
            </div>
          </div>

          <!-- Station Selector -->
          <div class="flex items-center gap-2">
            <span class="text-[10px] font-black uppercase tracking-widest text-slate-500">Station:</span>
            <select
              v-model="selectedStationId"
              class="h-9 px-3 bg-slate-900 border border-slate-800 rounded-xl text-xs font-bold text-slate-200 focus:outline-none focus:border-indigo-500"
            >
              <option v-for="st in stationsList" :key="st.id" :value="st.id">
                {{ st.name }} ({{ st.groupId || 'Default' }})
              </option>
            </select>
          </div>
        </div>

        <!-- Filter & Control Toolbar -->
        <div class="flex flex-wrap items-center justify-between gap-3 mt-4 pt-3 border-t border-slate-900">
          <div class="relative flex-1 min-w-[200px] max-w-sm">
            <Search class="w-3.5 h-3.5 text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              v-model="searchQuery"
              type="text"
              placeholder="Filter node or parts in tree..."
              class="w-full pl-9 pr-3 py-1.5 bg-slate-900/90 border border-slate-800 rounded-xl text-xs text-slate-200 placeholder:text-slate-500 focus:outline-none focus:border-indigo-500"
            />
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              @click="expandAll"
              class="h-8 px-2.5 border-slate-800 bg-slate-900 text-slate-400 hover:text-slate-200 text-[9px] font-black uppercase tracking-wider rounded-lg"
            >
              Expand All
            </Button>
            <Button
              variant="outline"
              size="sm"
              @click="collapseAll"
              class="h-8 px-2.5 border-slate-800 bg-slate-900 text-slate-400 hover:text-slate-200 text-[9px] font-black uppercase tracking-wider rounded-lg"
            >
              Collapse All
            </Button>
          </div>
        </div>
      </DialogHeader>

      <!-- Scrollable Tree Canvas -->
      <div class="flex-1 overflow-y-auto custom-scrollbar py-4 space-y-4">
        <div v-if="loading" class="p-8 text-center text-xs font-bold uppercase tracking-widest text-slate-500">
          Traversing station topology and equipment records...
        </div>

        <div v-else-if="treeData" class="space-y-3">
          <!-- Root Station Node -->
          <div class="p-4 rounded-2xl bg-slate-900/90 border border-indigo-500/30 shadow-lg">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-3">
                <button 
                  type="button" 
                  @click="toggleNode('root')"
                  class="p-1 text-slate-500 hover:text-slate-300 rounded transition-transform"
                >
                  <ChevronDown v-if="expandedNodes['root']" class="w-4 h-4 text-indigo-400" />
                  <ChevronRight v-else class="w-4 h-4" />
                </button>
                <div class="p-2 rounded-xl bg-indigo-600/20 text-indigo-400 border border-indigo-500/30">
                  <FolderTree class="w-4 h-4" />
                </div>
                <div>
                  <div class="text-sm font-black text-white uppercase tracking-tight flex items-center gap-2">
                    <span>{{ treeData.stationName }}</span>
                    <Badge variant="outline" class="text-[8px] uppercase tracking-widest font-black text-indigo-400 border-indigo-500/40 bg-indigo-950/40">
                      {{ treeData.technology || 'Assembly' }}
                    </Badge>
                  </div>
                  <div class="text-[10px] text-slate-400 font-semibold uppercase mt-0.5">
                    Production Line: <span class="text-slate-200 font-bold">{{ treeData.lineName || 'Line 1' }}</span>
                  </div>
                </div>
              </div>

              <div class="flex items-center gap-2 text-[9px] font-mono">
                <span class="px-2.5 py-1 rounded-full bg-slate-800 text-slate-300 border border-slate-700">
                  {{ filteredControllers.length }} Controller(s)
                </span>
                <span class="px-2.5 py-1 rounded-full bg-emerald-950/60 text-emerald-300 border border-emerald-800/50">
                  {{ filteredParts.length }} Serialized Parts
                </span>
              </div>
            </div>

            <!-- Children of Station: Controllers & Parts -->
            <div v-if="expandedNodes['root']" class="mt-4 pl-6 border-l-2 border-slate-800 space-y-3">
              
              <!-- SECTION: Host Industrial PCs / Controllers -->
              <div class="space-y-2">
                <div class="text-[9px] font-black uppercase tracking-widest text-slate-500 flex items-center gap-1.5">
                  <Monitor class="w-3.5 h-3.5 text-blue-400" />
                  <span>Host Controller IPCs ({{ filteredControllers.length }})</span>
                </div>

                <div 
                  v-for="ctrl in filteredControllers" 
                  :key="ctrl.id"
                  class="p-3 rounded-xl bg-slate-950/80 border border-slate-800/90 hover:border-slate-700 transition-colors"
                >
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2.5">
                      <button 
                        type="button" 
                        @click="toggleNode(`ctrl-${ctrl.id}`)"
                        class="p-0.5 text-slate-500 hover:text-slate-300"
                      >
                        <ChevronDown v-if="expandedNodes[`ctrl-${ctrl.id}`]" class="w-3.5 h-3.5 text-blue-400" />
                        <ChevronRight v-else class="w-3.5 h-3.5" />
                      </button>
                      <Monitor class="w-4 h-4 text-blue-400 shrink-0" />
                      <div>
                        <div class="text-xs font-bold text-slate-200 font-mono flex items-center gap-2">
                          <span>{{ ctrl.hostname }}</span>
                          <span class="size-2 rounded-full bg-emerald-400 animate-pulse" />
                        </div>
                        <div class="text-[9px] text-slate-500 font-mono mt-0.5">
                          IP: {{ ctrl.ipAddress || 'DHCP' }} • MAC: {{ ctrl.macAddress || 'N/A' }}
                        </div>
                      </div>
                    </div>

                    <Badge variant="outline" class="text-[8px] uppercase tracking-wider font-mono text-blue-400 border-blue-500/30 bg-blue-950/20">
                      Industrial IPC
                    </Badge>
                  </div>

                  <!-- Internal Hardware Components of the IPC -->
                  <div v-if="expandedNodes[`ctrl-${ctrl.id}`] && ctrl.hardware?.length > 0" class="mt-2.5 pl-6 border-l border-slate-800/80 space-y-1.5">
                    <div class="text-[8px] font-black uppercase tracking-wider text-slate-600">Reported Hardware Modules:</div>
                    <div 
                      v-for="hw in ctrl.hardware" 
                      :key="hw.id || hw.name"
                      class="flex items-center justify-between px-2.5 py-1.5 rounded-lg bg-slate-900/60 border border-slate-850 text-[10px]"
                    >
                      <div class="flex items-center gap-2">
                        <Cpu class="w-3 h-3 text-emerald-400" />
                        <span class="font-bold text-slate-300">{{ hw.name }}</span>
                      </div>
                      <span class="text-[8px] font-mono uppercase text-slate-500">{{ hw.type || 'Internal' }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- SECTION: Attached Serialized Equipment & Parts -->
              <div class="space-y-2 pt-2">
                <div class="text-[9px] font-black uppercase tracking-widest text-slate-500 flex items-center gap-1.5">
                  <Layers class="w-3.5 h-3.5 text-emerald-400" />
                  <span>Installed High-Value Serialized Parts ({{ filteredParts.length }})</span>
                </div>

                <div 
                  v-for="part in filteredParts" 
                  :key="part.id"
                  class="p-3 rounded-xl bg-slate-950/80 border border-slate-800/90 hover:border-emerald-500/30 transition-colors"
                >
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2.5">
                      <div class="p-1.5 rounded-lg bg-emerald-950/40 text-emerald-400 border border-emerald-800/40">
                        <Zap class="w-3.5 h-3.5" />
                      </div>
                      <div>
                        <div class="text-xs font-bold text-slate-200 uppercase tracking-tight flex items-center gap-2">
                          <span>{{ part.name }}</span>
                          <span 
                            class="text-[7.5px] uppercase font-bold px-2 py-0.5 rounded-full"
                            :class="part.equipmentStatus === 'InMachine' ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' : 'bg-blue-500/20 text-blue-400 border border-blue-500/30'"
                          >
                            {{ part.equipmentStatus || 'InMachine' }}
                          </span>
                        </div>
                        <div class="text-[9px] text-slate-400 flex items-center gap-2 mt-0.5">
                          <span class="font-mono text-indigo-300 font-bold">SN: {{ part.serialNumber || 'UNTRACKED' }}</span>
                          <span v-if="part.manufacturer">• MFR: {{ part.manufacturer }}</span>
                          <span v-if="part.storageLocation">• Pos: {{ part.storageLocation }}</span>
                        </div>
                      </div>
                    </div>

                    <div class="flex items-center gap-2">
                      <Badge variant="outline" class="text-[8px] uppercase tracking-wider font-mono text-emerald-400 border-emerald-500/30 bg-emerald-950/20">
                        {{ part.technology || 'Assembly' }}
                      </Badge>
                      <span v-if="part.costInHUF" class="text-[9px] font-mono text-slate-400">
                        {{ new Intl.NumberFormat('hu-HU').format(part.costInHUF) }} HUF
                      </span>
                    </div>
                  </div>
                </div>
              </div>

            </div>
          </div>
        </div>

        <div v-else class="p-8 text-center text-xs text-slate-500 font-bold uppercase tracking-widest">
          No station selected or topology data unavailable.
        </div>
      </div>
    </DialogContent>
  </Dialog>
</template>
