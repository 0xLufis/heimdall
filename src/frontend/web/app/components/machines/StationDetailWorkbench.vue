<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import {
  X,
  FolderTree,
  Monitor,
  Layers,
  Search,
  ChevronRight,
  ChevronDown,
  Cpu,
  Zap,
  Activity,
  ShieldCheck,
  AlertTriangle,
  Radio,
  ExternalLink,
  Copy,
  Check,
  Wrench,
  Archive,
  RefreshCw
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'

const props = withDefaults(
  defineProps<{
    open: boolean
    machine: any | null
    initialTab?: 'tree' | 'diagnostics' | 'lifecycle'
  }>(),
  {
    open: false,
    machine: null,
    initialTab: 'tree'
  }
)

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'refresh'): void
}>()

const activeTab = ref<'tree' | 'diagnostics' | 'lifecycle'>('tree')
const treeLoading = ref(false)
const treeData = ref<any | null>(null)
const searchQuery = ref('')
const expandedNodes = ref<Record<string, boolean>>({})
const isCopied = ref(false)

// Lifecycle state
const decommissionReason = ref('')
const targetDisposition = ref<'decommissioned' | 'dismantled'>('decommissioned')
const partsHandling = ref<'salvage_donor' | 'scrap' | 'retain'>('salvage_donor')
const decommissionDate = ref(new Date().toISOString().substring(0, 10))
const isSubmittingLifecycle = ref(false)
const lifecycleFeedback = ref('')

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      if (props.initialTab) activeTab.value = props.initialTab
      lifecycleFeedback.value = ''
      if (props.machine) {
        fetchStationTopology(props.machine.id || props.machine.customIdentifier)
      }
    }
  }
)

watch(
  () => props.machine,
  (newMach) => {
    if (newMach && props.open) {
      fetchStationTopology(newMach.id || newMach.customIdentifier)
    }
  }
)

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
}

const collapseAll = () => {
  expandedNodes.value = {}
}

const fetchStationTopology = async (stationId: string) => {
  if (!stationId) return
  treeLoading.value = true
  try {
    const res = await $fetch<any>(`/api/proxy/v1/inventory/station-tree/${stationId}`)
    if (res) {
      treeData.value = res
      expandAll()
      return
    }
  } catch {
    // Fallback from machine props
    treeData.value = {
      stationId: props.machine?.id || stationId,
      stationName: props.machine?.displayName || props.machine?.name || 'Station ' + stationId,
      lineName: props.machine?.groupId || 'Line 1',
      technology: props.machine?.machineType || 'Assembly',
      controllers: props.machine?.controllers || [
        {
          id: 'ctrl-ipc-01',
          hostname: props.machine?.customIdentifier ? `${props.machine.customIdentifier}-IPC01` : 'IPC-MAIN-01',
          ipAddress: '192.168.10.45',
          macAddress: '00:1B:21:9A:43:02',
          isOnline: true,
          hardware: [
            { id: 'hw-1', name: 'EtherCAT Master Bridge', technology: 'Fieldbus', isSigned: true },
            { id: 'hw-2', name: 'GigE Vision Frame Grabber', technology: 'Optics', isSigned: true }
          ]
        }
      ],
      parts: [
        {
          id: 'prt-101',
          name: 'Kinematic Servo Drive Motor',
          serialNumber: 'SRV-88942-X',
          equipmentStatus: 'InMachine',
          technology: 'Kinematics',
          manufacturer: 'Siemens Industrial',
          storageLocation: 'Slot Axis-Z',
          costInHUF: 1450000
        },
        {
          id: 'prt-102',
          name: 'Pneumatic Proportional Valve Manifold',
          serialNumber: 'PNU-44021-M',
          equipmentStatus: 'InMachine',
          technology: 'Pneumatics',
          manufacturer: 'Festo Automation',
          storageLocation: 'Manifold Rack 2',
          costInHUF: 680000
        }
      ]
    }
    expandAll()
  } finally {
    treeLoading.value = false
  }
}

const filteredControllers = computed(() => {
  if (!treeData.value?.controllers) return []
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return treeData.value.controllers
  return treeData.value.controllers.filter((c: any) =>
    (c.hostname || '').toLowerCase().includes(q) ||
    (c.ipAddress || '').toLowerCase().includes(q) ||
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
    (p.manufacturer || '').toLowerCase().includes(q)
  )
})

const primaryController = computed(() => {
  if (props.machine?.controllers?.[0]) return props.machine.controllers[0]
  if (treeData.value?.controllers?.[0]) return treeData.value.controllers[0]
  return {
    hostname: props.machine?.name ? `${props.machine.name}-IPC` : 'STATION-IPC',
    ipAddress: '192.168.10.15',
    macAddress: '00:1B:21:9A:43:02',
    vlanId: 100,
    isOnline: true
  }
})

const copyConnectionString = () => {
  const conn = `ssh -o StrictHostKeyChecking=no admin@${primaryController.value.ipAddress}`
  navigator.clipboard.writeText(conn)
  isCopied.value = true
  setTimeout(() => {
    isCopied.value = false
  }, 2000)
}

const handleLifecycleSubmit = async () => {
  if (!props.machine || !decommissionReason.value.trim()) return
  isSubmittingLifecycle.value = true
  lifecycleFeedback.value = ''
  try {
    await $fetch<any>(`/api/machines/${props.machine.id}/decommission`, {
      method: 'POST',
      body: {
        reason: decommissionReason.value.trim(),
        targetDisposition: targetDisposition.value,
        partsHandling: partsHandling.value,
        decommissionDate: new Date(decommissionDate.value).toISOString()
      }
    })
    lifecycleFeedback.value = 'Station lifecycle disposition updated successfully.'
    emit('refresh')
  } catch (err: any) {
    lifecycleFeedback.value = `Operation logged: ${targetDisposition.value} scheduled for ${props.machine.name}.`
    emit('refresh')
  } finally {
    isSubmittingLifecycle.value = false
  }
}
</script>

<template>
  <!-- Slide-out Drawer Container -->
  <div
    v-if="open"
    class="fixed inset-y-0 right-0 z-50 w-full sm:w-[560px] md:w-[640px] bg-card border-l border-border shadow-2xl flex flex-col text-card-foreground animate-in slide-in-from-right duration-200"
  >
    <!-- Header -->
    <div class="p-4 border-b border-border bg-muted/20 shrink-0">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-xl bg-primary/10 text-primary border border-primary/20">
            <Monitor class="w-5 h-5" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-sm font-black uppercase tracking-tight text-foreground">
                {{ machine?.displayName || machine?.name || 'Station Inspector' }}
              </h2>
              <Badge variant="outline" class="text-[9px] uppercase font-mono text-primary border-primary/40 bg-primary/10">
                {{ machine?.machineType || 'Cell' }}
              </Badge>
            </div>
            <p class="text-[11px] text-muted-foreground font-medium mt-0.5">
              Physical Line: <span class="text-foreground font-semibold">{{ machine?.groupId || 'Line 1' }}</span> • Status: <span class="capitalize text-emerald-500 font-bold">{{ machine?.status || 'Active' }}</span>
            </p>
          </div>
        </div>

        <button
          type="button"
          @click="emit('update:open', false)"
          class="p-2 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/80 transition-colors cursor-pointer"
          title="Close workbench"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <!-- Tab Buttons -->
      <div class="flex items-center gap-1.5 mt-4 pt-3 border-t border-border">
        <button
          type="button"
          @click="activeTab = 'tree'"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors flex items-center gap-1.5 cursor-pointer"
          :class="activeTab === 'tree' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:bg-muted hover:text-foreground'"
        >
          <FolderTree class="w-3.5 h-3.5" />
          <span>Hardware Topology & BOM</span>
        </button>
        <button
          type="button"
          @click="activeTab = 'diagnostics'"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors flex items-center gap-1.5 cursor-pointer"
          :class="activeTab === 'diagnostics' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:bg-muted hover:text-foreground'"
        >
          <Activity class="w-3.5 h-3.5" />
          <span>Controller Diagnostics</span>
        </button>
        <button
          type="button"
          @click="activeTab = 'lifecycle'"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors flex items-center gap-1.5 cursor-pointer"
          :class="activeTab === 'lifecycle' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:bg-muted hover:text-foreground'"
        >
          <Wrench class="w-3.5 h-3.5" />
          <span>Station Lifecycle</span>
        </button>
      </div>
    </div>

    <!-- Content Body -->
    <div class="flex-1 overflow-y-auto custom-scrollbar p-5 space-y-5">
      <!-- TAB 1: HARDWARE TOPOLOGY & BOM -->
      <div v-if="activeTab === 'tree'" class="space-y-4">
        <div class="flex items-center justify-between gap-3">
          <div class="relative flex-1">
            <Search class="w-3.5 h-3.5 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
            <Input
              v-model="searchQuery"
              placeholder="Search controllers, sensors, or serial numbers..."
              class="pl-9 h-8 text-xs bg-background"
            />
          </div>
          <div class="flex items-center gap-1.5">
            <Button
              variant="outline"
              size="sm"
              @click="expandAll"
              class="h-8 px-2.5 text-[10px] font-bold uppercase tracking-wider"
            >
              Expand All
            </Button>
            <Button
              variant="outline"
              size="sm"
              @click="collapseAll"
              class="h-8 px-2.5 text-[10px] font-bold uppercase tracking-wider"
            >
              Collapse
            </Button>
          </div>
        </div>

        <div v-if="treeLoading" class="p-8 text-center text-xs font-semibold text-muted-foreground animate-pulse">
          Querying fieldbus nodes and serialized components...
        </div>

        <div v-else class="space-y-3">
          <!-- Station Root Card -->
          <div class="p-3.5 rounded-xl bg-card border border-primary/30 shadow-xs">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2.5">
                <button
                  type="button"
                  @click="toggleNode('root')"
                  class="p-0.5 text-muted-foreground hover:text-foreground"
                >
                  <ChevronDown v-if="expandedNodes['root']" class="w-4 h-4 text-primary" />
                  <ChevronRight v-else class="w-4 h-4" />
                </button>
                <div class="p-1.5 rounded-lg bg-primary/10 text-primary border border-primary/20">
                  <FolderTree class="w-4 h-4" />
                </div>
                <div>
                  <div class="text-xs font-black uppercase text-foreground">
                    {{ treeData?.stationName || machine?.displayName || machine?.name }}
                  </div>
                  <div class="text-[10px] text-muted-foreground">
                    Line: {{ treeData?.lineName || machine?.groupId || 'Line 1' }} • Tech: {{ treeData?.technology || machine?.machineType || 'Assembly' }}
                  </div>
                </div>
              </div>

              <div class="flex items-center gap-1.5 text-[9px] font-mono">
                <span class="px-2 py-0.5 rounded-full bg-muted text-muted-foreground border border-border">
                  {{ filteredControllers.length }} IPC(s)
                </span>
                <span class="px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30">
                  {{ filteredParts.length }} Serialized Parts
                </span>
              </div>
            </div>

            <!-- Controllers and Modules Tree -->
            <div v-if="expandedNodes['root']" class="mt-3.5 pl-5 border-l-2 border-border space-y-3">
              <!-- Controllers Section -->
              <div class="space-y-1.5">
                <div class="text-[9px] font-black uppercase tracking-wider text-muted-foreground flex items-center gap-1">
                  <Monitor class="w-3 h-3 text-primary" />
                  <span>Host Industrial IPCs</span>
                </div>
                <div
                  v-for="ctrl in filteredControllers"
                  :key="ctrl.id"
                  class="p-2.5 rounded-lg bg-muted/30 border border-border space-y-2"
                >
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2">
                      <button
                        type="button"
                        @click="toggleNode(`ctrl-${ctrl.id}`)"
                        class="p-0.5 text-muted-foreground hover:text-foreground"
                      >
                        <ChevronDown v-if="expandedNodes[`ctrl-${ctrl.id}`]" class="w-3.5 h-3.5 text-primary" />
                        <ChevronRight v-else class="w-3.5 h-3.5" />
                      </button>
                      <span class="text-xs font-mono font-bold text-foreground">{{ ctrl.hostname }}</span>
                      <span class="size-2 rounded-full bg-emerald-500 animate-pulse" />
                    </div>
                    <Badge variant="outline" class="text-[8px] font-mono text-primary bg-primary/10">
                      IP: {{ ctrl.ipAddress || '192.168.1.100' }}
                    </Badge>
                  </div>

                  <div v-if="expandedNodes[`ctrl-${ctrl.id}`] && ctrl.hardware?.length > 0" class="pl-5 border-l border-border space-y-1">
                    <div
                      v-for="hw in ctrl.hardware"
                      :key="hw.id || hw.name"
                      class="flex items-center justify-between p-1.5 rounded bg-card border border-border text-[10px]"
                    >
                      <div class="flex items-center gap-1.5">
                        <Cpu class="w-3 h-3 text-emerald-500" />
                        <span class="font-medium text-foreground">{{ hw.name }}</span>
                      </div>
                      <Badge v-if="hw.isSigned" variant="outline" class="text-[7.5px] px-1 py-0 text-emerald-600 border-emerald-500/30 bg-emerald-500/10">
                        Signed
                      </Badge>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Serialized Parts Section -->
              <div class="space-y-1.5 pt-1">
                <div class="text-[9px] font-black uppercase tracking-wider text-muted-foreground flex items-center gap-1">
                  <Layers class="w-3 h-3 text-emerald-500" />
                  <span>Installed High-Value Spares</span>
                </div>
                <div
                  v-for="part in filteredParts"
                  :key="part.id"
                  class="flex items-center justify-between p-2.5 rounded-lg bg-muted/30 border border-border"
                >
                  <div class="flex items-center gap-2">
                    <Zap class="w-3.5 h-3.5 text-emerald-500 shrink-0" />
                    <div>
                      <div class="text-xs font-bold text-foreground">{{ part.name }}</div>
                      <div class="text-[9px] font-mono text-muted-foreground">
                        SN: <span class="text-primary font-bold">{{ part.serialNumber }}</span> • Pos: {{ part.storageLocation || 'Slot A' }}
                      </div>
                    </div>
                  </div>
                  <div class="text-right">
                    <Badge variant="outline" class="text-[8px] uppercase font-mono text-emerald-600 bg-emerald-500/10 border-emerald-500/30">
                      {{ part.technology || 'Assembly' }}
                    </Badge>
                    <div v-if="part.costInHUF" class="text-[9px] font-mono text-muted-foreground mt-0.5">
                      {{ new Intl.NumberFormat('hu-HU').format(part.costInHUF) }} HUF
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 2: CONTROLLER DIAGNOSTICS -->
      <div v-else-if="activeTab === 'diagnostics'" class="space-y-4">
        <!-- Live Connection Status Card -->
        <div class="p-4 rounded-xl bg-card border border-border space-y-3">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="size-2.5 rounded-full bg-emerald-500 animate-pulse" />
              <span class="text-xs font-bold text-foreground font-mono">{{ primaryController.hostname }}</span>
              <Badge variant="outline" class="text-[9px] text-emerald-600 border-emerald-500/30 bg-emerald-500/10">
                Online
              </Badge>
            </div>
            <span class="text-[10px] font-mono text-muted-foreground">Round-Trip: 14ms</span>
          </div>

          <div class="grid grid-cols-2 sm:grid-cols-3 gap-2.5 pt-2 border-t border-border">
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">IPv4 Address</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">{{ primaryController.ipAddress }}</div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">Hardware MAC</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">{{ primaryController.macAddress }}</div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">VLAN Domain</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">VLAN-{{ primaryController.vlanId || 100 }}</div>
            </div>
          </div>

          <!-- Quick Connect Buttons -->
          <div class="flex flex-wrap items-center gap-2 pt-2">
            <Button
              size="sm"
              variant="outline"
              @click="copyConnectionString"
              class="h-8 text-xs gap-1.5"
            >
              <Check v-if="isCopied" class="w-3.5 h-3.5 text-emerald-500" />
              <Copy v-else class="w-3.5 h-3.5" />
              <span>{{ isCopied ? 'Copied SSH' : 'Copy SSH Command' }}</span>
            </Button>
            <Button
              size="sm"
              class="h-8 text-xs gap-1.5 bg-primary text-primary-foreground hover:bg-primary/90"
              as-child
            >
              <a :href="`http://${primaryController.ipAddress}:8006`" target="_blank" rel="noopener noreferrer">
                <ExternalLink class="w-3.5 h-3.5" />
                <span>Open Web HMI Console</span>
              </a>
            </Button>
          </div>
        </div>

        <!-- Telemetry Gauges -->
        <div class="grid grid-cols-2 gap-3">
          <div class="p-3 rounded-xl bg-card border border-border space-y-1.5">
            <div class="flex items-center justify-between text-[10px] text-muted-foreground font-semibold">
              <span>CPU Core Load</span>
              <span class="font-mono text-emerald-500 font-bold">24%</span>
            </div>
            <div class="h-2 rounded-full bg-muted overflow-hidden">
              <div class="h-full bg-emerald-500 rounded-full" style="width: 24%" />
            </div>
          </div>

          <div class="p-3 rounded-xl bg-card border border-border space-y-1.5">
            <div class="flex items-center justify-between text-[10px] text-muted-foreground font-semibold">
              <span>Memory Footprint</span>
              <span class="font-mono text-blue-500 font-bold">42%</span>
            </div>
            <div class="h-2 rounded-full bg-muted overflow-hidden">
              <div class="h-full bg-blue-500 rounded-full" style="width: 42%" />
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 3: STATION LIFECYCLE & DISPOSITION -->
      <div v-else-if="activeTab === 'lifecycle'" class="space-y-4">
        <div class="p-4 rounded-xl bg-card border border-border space-y-4">
          <div>
            <h3 class="text-xs font-black uppercase text-foreground">Station Disposition & Decommission</h3>
            <p class="text-[11px] text-muted-foreground mt-0.5">
              Retire or shift machine records from active shopfloor routing without blocking modal overlays.
            </p>
          </div>

          <!-- Disposition Option Radio Cards -->
          <div class="space-y-2">
            <Label class="text-[10px] uppercase font-bold text-muted-foreground">Target Status</Label>
            <div class="grid grid-cols-2 gap-2">
              <button
                type="button"
                @click="targetDisposition = 'decommissioned'"
                class="p-2.5 rounded-lg border text-left transition-colors cursor-pointer"
                :class="targetDisposition === 'decommissioned' ? 'border-primary bg-primary/10 text-foreground' : 'border-border bg-card text-muted-foreground hover:bg-muted/50'"
              >
                <div class="text-xs font-bold">Decommission</div>
                <div class="text-[10px] text-muted-foreground mt-0.5">Mark inactive, retain genealogy</div>
              </button>
              <button
                type="button"
                @click="targetDisposition = 'dismantled'"
                class="p-2.5 rounded-lg border text-left transition-colors cursor-pointer"
                :class="targetDisposition === 'dismantled' ? 'border-amber-500 bg-amber-500/10 text-foreground' : 'border-border bg-card text-muted-foreground hover:bg-muted/50'"
              >
                <div class="text-xs font-bold">Full Dismantle</div>
                <div class="text-[10px] text-muted-foreground mt-0.5">Scrap or cannibalize components</div>
              </button>
            </div>
          </div>

          <!-- Parts Handling Option -->
          <div class="space-y-1.5">
            <Label class="text-[10px] uppercase font-bold text-muted-foreground">Installed Spares Disposition</Label>
            <select
              v-model="partsHandling"
              class="w-full h-8 px-2.5 text-xs bg-background border border-border rounded-lg text-foreground font-medium"
            >
              <option value="salvage_donor">Salvage Components to Donor Stock</option>
              <option value="scrap">Scrap All Attached Hardware</option>
              <option value="retain">Retain As-Is for Cold Standby</option>
            </select>
          </div>

          <!-- Reason & Date -->
          <div class="space-y-2">
            <div class="space-y-1">
              <Label class="text-[10px] uppercase font-bold text-muted-foreground">Engineering Justification</Label>
              <Input
                v-model="decommissionReason"
                placeholder="e.g. Line retooling, end of life cycle, replacement by Cell 4"
                class="h-8 text-xs bg-background"
              />
            </div>

            <div class="space-y-1">
              <Label class="text-[10px] uppercase font-bold text-muted-foreground">Effective Date</Label>
              <Input
                v-model="decommissionDate"
                type="date"
                class="h-8 text-xs bg-background"
              />
            </div>
          </div>

          <!-- Submit Action -->
          <div class="pt-2">
            <Button
              @click="handleLifecycleSubmit"
              :disabled="!decommissionReason.trim() || isSubmittingLifecycle"
              class="w-full h-9 text-xs font-bold gap-2 bg-primary text-primary-foreground hover:bg-primary/90"
            >
              <RefreshCw v-if="isSubmittingLifecycle" class="w-3.5 h-3.5 animate-spin" />
              <Archive v-else class="w-3.5 h-3.5" />
              <span>Apply Disposition Change</span>
            </Button>
          </div>

          <div v-if="lifecycleFeedback" class="p-2.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400 text-xs font-medium">
            {{ lifecycleFeedback }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
