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
  RefreshCw,
  Users,
  UserCheck,
  ShieldAlert,
  Trash2,
  UserPlus,
  Sparkles,
  Box
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { useDedications } from '~/composables/useDedications'
import type { MachineDedications } from '~/types/maintenance'
import DynamicUiExtensions from '~/components/lua/DynamicUiExtensions.vue'
import Machine3dDigitalTwin from '~/components/cad/Machine3dDigitalTwin.vue'
import RemoteQuickViewModal from '~/components/controllers/RemoteQuickViewModal.vue'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { useControllers } from '~/composables/useControllers'
import { useStationTelemetry } from '~/composables/useStationTelemetry'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'
import type { IndustrialController } from '~/types/domain'

const props = withDefaults(
  defineProps<{
    open: boolean
    machine: any | null
    initialTab?: 'tree' | '3d-twin' | 'diagnostics' | 'lifecycle' | 'dedications'
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

const activeTab = ref<'tree' | '3d-twin' | 'diagnostics' | 'lifecycle' | 'dedications'>(props.initialTab || 'tree')
const treeLoading = ref(false)
const treeData = ref<any | null>(null)
const searchQuery = ref('')
const expandedNodes = ref<Record<string, boolean>>({})
const copiedControllerId = ref<string | null>(null)

// Fleet controller store (CPU / RAM / last seen). Read once here; the workbench does not poll.
const { controllers: fleetControllers, isOnline: isControllerOnline } = useControllers({ poll: false })
const { getStationKpi, getStationOscillation } = useStationTelemetry()
const { canExecuteRemote } = useRbacPermission()

// 3D Digital Twin state
const selected3dSlot = ref<string | null>(null)
const on3dSlotSelected = (slotId: string) => {
  selected3dSlot.value = slotId
}

// Only real installed parts bind to CAD meshes; an empty list leaves meshes unbound rather than
// presenting a fabricated bill of materials.
const installed3dParts = computed<any[]>(() => treeData.value?.installedParts ?? [])

// Dedications state
const { fetchMachineDedications, dedicateToMachine, removeMachineDedication } = useDedications()
const dedicationsData = ref<MachineDedications | null>(null)
const dedicationsLoading = ref(false)
const targetType = ref<'person' | 'group'>('person')
const selectedUserId = ref('')
const selectedGroupId = ref('')
const selectedTechnology = ref('General')
const isSubmittingDedication = ref(false)
const dedicationFeedback = ref('')
const availableUsers = ref<any[]>([])
const availableGroups = ref<any[]>([])

const availableTechnologies = computed(() => {
  const set = new Set<string>(['General', 'Controls', 'Mechanical'])
  if (dedicationsData.value?.technologies) {
    dedicationsData.value.technologies.forEach(t => set.add(t))
  }
  return Array.from(set)
})

const loadDedications = async () => {
  const machineId = props.machine?.id || props.machine?.customIdentifier
  if (!machineId) return
  dedicationsLoading.value = true
  dedicationFeedback.value = ''
  try {
    const [deds, usersRes, groupsRes] = await Promise.all([
      fetchMachineDedications(machineId).catch(() => null),
      $fetch<{ success: boolean; users: any[] }>('/api/users').catch(() => ({ success: false, users: [] })),
      $fetch<any[]>('/api/personnel/groups').catch(() => [])
    ])
    dedicationsData.value = deds
    availableUsers.value = usersRes?.users || []
    availableGroups.value = groupsRes || []
  } catch (err: any) {
    console.error('Failed to load dedications', err)
  } finally {
    dedicationsLoading.value = false
  }
}

const handleAssignDedication = async () => {
  const machineId = props.machine?.id || props.machine?.customIdentifier
  if (!machineId) return
  isSubmittingDedication.value = true
  dedicationFeedback.value = ''
  try {
    if (targetType.value === 'person') {
      const user = availableUsers.value.find(u => u.id === selectedUserId.value)
      if (!user) return
      await dedicateToMachine(
        machineId,
        'person',
        user.id,
        user.name || user.email,
        user.email,
        selectedTechnology.value
      )
      dedicationFeedback.value = `Dedicated ${user.name || user.email} (${selectedTechnology.value}) to this machine successfully.`
    } else {
      const group = availableGroups.value.find(g => g.id === selectedGroupId.value)
      if (!group) return
      await dedicateToMachine(
        machineId,
        'group',
        group.id,
        group.name,
        undefined,
        selectedTechnology.value
      )
      dedicationFeedback.value = `Dedicated maintenance group ${group.name} (${selectedTechnology.value}) to this machine successfully.`
    }
    await loadDedications()
    emit('refresh')
  } catch (err: any) {
    dedicationFeedback.value = err?.message || 'Failed to assign dedication.'
  } finally {
    isSubmittingDedication.value = false
  }
}

const handleRemoveDedication = async (targetId?: string, technology?: string) => {
  const machineId = props.machine?.id || props.machine?.customIdentifier
  if (!machineId) return
  dedicationsLoading.value = true
  try {
    await removeMachineDedication(machineId, { targetId, technology })
    dedicationFeedback.value = 'Machine dedication removed.'
    await loadDedications()
    emit('refresh')
  } catch (err: any) {
    dedicationFeedback.value = err?.message || 'Failed to remove dedication.'
  } finally {
    dedicationsLoading.value = false
  }
}

// Lifecycle state
const decommissionReason = ref('')
const targetDisposition = ref<'decommissioned' | 'dismantled'>('decommissioned')
const partsHandling = ref<'salvage_donor' | 'scrap' | 'retain'>('salvage_donor')
const decommissionDate = ref(new Date().toISOString().substring(0, 10))
const isSubmittingLifecycle = ref(false)
const lifecycleFeedback = ref('')

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

// Bumped per request so a slow response for a previously opened station cannot overwrite the current one.
let topologyRequest = 0

const fetchStationTopology = async (stationId: string) => {
  if (!stationId) return
  const request = ++topologyRequest
  // Never keep showing the previous station's tree / 3D model while the next one loads.
  if (treeData.value && String(treeData.value.stationId) !== String(stationId)) {
    treeData.value = null
    selected3dSlot.value = null
  }
  treeLoading.value = true
  try {
    const res = await $fetch<any>(`/api/proxy/v1/inventory/station-tree/${encodeURIComponent(stationId)}`)
    if (request !== topologyRequest) return
    if (res) {
      treeData.value = res
      expandAll()
      return
    }
  } catch (err) {
    if (request !== topologyRequest) return
    // Backend topology unavailable: show what we know from the machine record only,
    // never fabricated controllers/IPs/parts.
    console.warn('Failed to load station topology', err)
    treeData.value = {
      stationId: props.machine?.id || stationId,
      stationName: props.machine?.displayName || props.machine?.name || 'Station ' + stationId,
      lineName: props.machine?.lineGroupName || props.machine?.groupId || '',
      technology: props.machine?.machineType || '',
      controllers: props.machine?.controllers || [],
      parts: []
    }
    expandAll()
  } finally {
    if (request === topologyRequest) treeLoading.value = false
  }
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      if (props.initialTab) activeTab.value = props.initialTab
      lifecycleFeedback.value = ''
      if (props.machine) {
        fetchStationTopology(props.machine.id || props.machine.customIdentifier)
        loadDedications()
      }
    }
  },
  { immediate: true }
)

watch(
  () => props.initialTab,
  (newTab) => {
    if (newTab) activeTab.value = newTab
  }
)

watch(
  () => props.machine,
  (newMach) => {
    if (newMach && props.open) {
      fetchStationTopology(newMach.id || newMach.customIdentifier)
      loadDedications()
    }
  }
)

watch(
  activeTab,
  (tab) => {
    if (tab === 'dedications' && props.open && props.machine) {
      loadDedications()
    }
  }
)

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

/**
 * Controllers bound to this station (station tree, else the machine record), enriched with the
 * fleet store's live resource figures when the controller is known there. Nothing is invented:
 * a station without controllers yields an empty list and values that were never reported stay null.
 */
const stationControllers = computed<IndustrialController[]>(() => {
  const source: any[] = treeData.value?.controllers?.length ? treeData.value.controllers : (props.machine?.controllers || [])
  return source.filter(Boolean).map((c: any) => {
    const fleet = fleetControllers.value.find(f => f.id === c.id)
    const lastOnline = c.lastOnline || c.lastSeen || fleet?.lastOnline || null
    return {
      ...(fleet || {}),
      id: c.id,
      name: c.name || c.hostname || fleet?.name || 'Controller',
      hostname: c.hostname || fleet?.hostname || c.name || '',
      macAddress: c.macAddress || fleet?.macAddress || '',
      ipAddress: c.ipAddress || fleet?.ipAddress,
      lastOnline,
      isOnline: isControllerOnline(lastOnline),
      vlanId: c.vlanId ?? null,
      telemetry: fleet?.telemetry
    } as IndustrialController & { isOnline: boolean; vlanId: number | null }
  })
})

const stationTelemetryKey = computed(() => String(props.machine?.id || props.machine?.customIdentifier || ''))
const stationKpi = computed(() => (stationTelemetryKey.value ? getStationKpi(stationTelemetryKey.value) : null))
const stationOscillation = computed(() => (stationTelemetryKey.value ? getStationOscillation(stationTelemetryKey.value) : null))

const formatMetric = (value: number | null | undefined, digits = 1) =>
  typeof value === 'number' && Number.isFinite(value) ? value.toFixed(digits) : '—'

const formatLastSeen = (iso: string | null | undefined) => {
  if (!iso) return 'never reported'
  const ms = Date.now() - new Date(iso).getTime()
  if (!Number.isFinite(ms)) return 'unknown'
  if (ms < 60_000) return 'just now'
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)} min ago`
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)} h ago`
  return `${Math.floor(ms / 86_400_000)} d ago`
}

// Remote view of one specific controller; the modal resolves that controller's own endpoint.
const remoteViewController = ref<IndustrialController | null>(null)
const isRemoteViewOpen = ref(false)
const openRemoteView = (controller: IndustrialController) => {
  remoteViewController.value = controller
  isRemoteViewOpen.value = true
}

const copyConnectionString = async (controller: IndustrialController) => {
  const target = controller.ipAddress || controller.hostname
  if (!target) return
  try {
    await navigator.clipboard.writeText(`ssh ${target}`)
    copiedControllerId.value = controller.id
    setTimeout(() => {
      if (copiedControllerId.value === controller.id) copiedControllerId.value = null
    }, 2000)
  } catch {
    // Clipboard unavailable (insecure context): nothing to confirm.
  }
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
    // Surface the failure instead of reporting a fake "logged" success.
    lifecycleFeedback.value = `Failed to update lifecycle disposition: ${err?.data?.statusMessage || err?.statusMessage || err?.message || 'unknown error'}`
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

      <!-- Dynamic Lua UI Extensions -->
      <div v-if="machine" class="mt-3 pt-2 border-t border-border/60">
        <DynamicUiExtensions zone="MachineDetails" :context="{ machine_id: machine.id || machine.customIdentifier }" />
      </div>

      <!-- Tab Buttons -->
      <div class="flex items-center gap-1.5 mt-3 pt-3 border-t border-border">
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
          @click="activeTab = '3d-twin'"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors flex items-center gap-1.5 cursor-pointer"
          :class="activeTab === '3d-twin' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:bg-muted hover:text-foreground'"
        >
          <Box class="w-3.5 h-3.5 text-cyan-400" />
          <span>3D Digital Twin</span>
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
        <button
          type="button"
          @click="activeTab = 'dedications'"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors flex items-center gap-1.5 cursor-pointer"
          :class="activeTab === 'dedications' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:bg-muted hover:text-foreground'"
        >
          <Users class="w-3.5 h-3.5" />
          <span>Dedications</span>
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
                  v-ctx:inventory="part"
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

      <!-- TAB: 3D DIGITAL TWIN -->
      <div v-else-if="activeTab === '3d-twin'" class="space-y-4">
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-4">
          <!-- 3D Canvas Viewport (8 cols on lg) -->
          <div class="lg:col-span-8">
            <ClientOnly>
              <Machine3dDigitalTwin
                :key="`${treeData?.stationId || machine?.id || 'station'}|${treeData?.model3dUrl || ''}`"
                :model-url="treeData?.model3dUrl || null"
                :selected-slot="selected3dSlot"
                :station-id="machine?.id || machine?.customIdentifier || null"
                :components="installed3dParts"
                height="560px"
                @select-slot="on3dSlotSelected"
              />
              <template #fallback>
                <div class="w-full h-[560px] rounded-xl border border-border/60 bg-slate-950 flex flex-col items-center justify-center p-6 text-center">
                  <Box class="w-8 h-8 text-cyan-400 mb-2 animate-bounce" />
                  <span class="text-xs font-mono text-muted-foreground">Initializing 3D Digital Twin Engine...</span>
                </div>
              </template>
            </ClientOnly>
          </div>

          <!-- Digital Twin Component Explorer / Inspector (4 cols on lg) -->
          <div class="lg:col-span-4 flex flex-col gap-3">
            <div class="p-4 rounded-xl bg-card border border-border shadow-xs flex-1 flex flex-col">
              <div class="flex items-center justify-between mb-2">
                <div class="text-xs font-bold uppercase tracking-wider text-foreground flex items-center gap-1.5">
                  <Layers class="w-3.5 h-3.5 text-primary" />
                  <span>CAD Component Slots</span>
                </div>
                <Badge variant="outline" class="text-[10px] font-mono">
                  {{ installed3dParts.length }} Nodes
                </Badge>
              </div>
              <p class="text-[11px] text-muted-foreground leading-relaxed mb-3">
                Click any mechanical component in 3D or pick a slot below to zoom and inspect telemetry.
              </p>

              <!-- Scrollable Slot List -->
              <div class="space-y-1.5 max-h-[420px] overflow-y-auto custom-scrollbar pr-1">
                <div
                  v-for="part in installed3dParts"
                  :key="part.id || part.cad3dNodePath"
                  class="p-2.5 rounded-lg border transition-all cursor-pointer flex items-center justify-between"
                  :class="[
                    (selected3dSlot && (part.cad3dNodePath === selected3dSlot || part.functionalSlotIdentifier === selected3dSlot))
                      ? 'bg-primary/10 border-primary text-foreground ring-1 ring-primary/40'
                      : 'bg-muted/20 border-border/70 hover:bg-muted/40 text-muted-foreground'
                  ]"
                  @click="selected3dSlot = part.cad3dNodePath || part.functionalSlotIdentifier"
                >
                  <div class="flex items-center gap-2 truncate">
                    <div
                      class="w-2 h-2 rounded-full shrink-0"
                      :class="part.equipmentStatus === 'InMachine' || part.equipmentStatus === 'active' ? 'bg-emerald-500' : 'bg-amber-500'"
                    />
                    <div class="truncate">
                      <div class="text-xs font-semibold text-foreground truncate">
                        {{ part.name || part.displayName || part.cad3dNodePath }}
                      </div>
                      <div class="text-[10px] font-mono text-muted-foreground truncate">
                        Slot: <span class="font-bold text-primary">{{ part.cad3dNodePath || part.functionalSlotIdentifier }}</span>
                      </div>
                    </div>
                  </div>
                  <Badge variant="outline" class="text-[9px] uppercase font-mono shrink-0 ml-1">
                    {{ part.technology || 'Hardware' }}
                  </Badge>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 2: CONTROLLER DIAGNOSTICS -->
      <div v-else-if="activeTab === 'diagnostics'" class="space-y-4">
        <!-- Live station telemetry (as reported by the station's PLC; empty until it reports) -->
        <div class="p-4 rounded-xl bg-card border border-border space-y-3">
          <div class="flex items-center justify-between gap-2">
            <div class="flex items-center gap-2">
              <Activity class="w-4 h-4 text-cyan-500" />
              <span class="text-xs font-bold text-foreground">Station Telemetry</span>
              <Badge v-if="stationKpi" variant="outline" class="text-[9px] font-mono">
                {{ stationKpi.operationalStatus }}
              </Badge>
            </div>
            <NuxtLink
              v-if="stationTelemetryKey"
              :to="{ path: '/dashboard/telemetry', query: { station: stationTelemetryKey } }"
              class="text-[11px] font-medium text-primary hover:underline inline-flex items-center gap-1"
            >
              <span>Open live view</span>
              <ExternalLink class="w-3 h-3" />
            </NuxtLink>
          </div>

          <div v-if="stationKpi?.lastTelemetryAt || stationOscillation" class="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">OEE</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">{{ formatMetric(stationKpi?.oee) }} %</div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">Parts / h</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">
                {{ formatMetric(stationKpi?.partsPerHour, 0) }}
                <span v-if="stationKpi?.targetPartsPerHour != null" class="text-muted-foreground font-normal">/ {{ formatMetric(stationKpi.targetPartsPerHour, 0) }}</span>
              </div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">Vibration</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">{{ formatMetric(stationOscillation?.vibrationMmS, 2) }} mm/s</div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">Temperature</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">{{ formatMetric(stationOscillation?.temperatureC) }} °C</div>
            </div>
          </div>
          <p v-else class="text-[11px] text-muted-foreground">
            No telemetry has been received for this station yet.
          </p>
          <p v-if="stationKpi?.lastTelemetryAt" class="text-[10px] font-mono text-muted-foreground">
            Last sample {{ formatLastSeen(stationKpi.lastTelemetryAt) }}
          </p>
        </div>

        <!-- Controllers bound to this station -->
        <div v-if="stationControllers.length === 0" class="p-6 rounded-xl border border-dashed border-border text-center">
          <Monitor class="w-6 h-6 mx-auto mb-2 text-muted-foreground/50" />
          <p class="text-xs text-muted-foreground">No controller is bound to this station.</p>
        </div>

        <div
          v-for="ctrl in stationControllers"
          :key="ctrl.id || ctrl.hostname"
          v-ctx:controller="ctrl"
          class="p-4 rounded-xl bg-card border border-border space-y-3"
        >
          <div class="flex items-center justify-between gap-2">
            <div class="flex items-center gap-2 min-w-0">
              <span
                class="size-2.5 rounded-full shrink-0"
                :class="(ctrl as any).isOnline ? 'bg-emerald-500 animate-pulse' : 'bg-muted-foreground/40'"
              />
              <span class="text-xs font-bold text-foreground font-mono truncate">{{ ctrl.hostname || ctrl.name }}</span>
              <Badge
                variant="outline"
                class="text-[9px]"
                :class="(ctrl as any).isOnline ? 'text-emerald-600 border-emerald-500/30 bg-emerald-500/10' : 'text-muted-foreground border-border bg-muted/40'"
              >
                {{ (ctrl as any).isOnline ? 'Online' : 'Offline' }}
              </Badge>
            </div>
            <span class="text-[10px] font-mono text-muted-foreground shrink-0">Last seen {{ formatLastSeen(ctrl.lastOnline) }}</span>
          </div>

          <div class="grid grid-cols-2 sm:grid-cols-5 gap-2.5 pt-2 border-t border-border">
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">IPv4 Address</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">{{ ctrl.ipAddress || '—' }}</div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">Hardware MAC</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5 truncate">{{ ctrl.macAddress || '—' }}</div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">VLAN</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">{{ (ctrl as any).vlanId != null ? `VLAN-${(ctrl as any).vlanId}` : '—' }}</div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">CPU Load</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">
                {{ ctrl.telemetry?.cpuUsagePercent != null ? `${Math.round(ctrl.telemetry.cpuUsagePercent)} %` : '—' }}
              </div>
            </div>
            <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
              <span class="text-[9px] uppercase font-semibold text-muted-foreground">Memory</span>
              <div class="text-xs font-mono font-bold text-foreground mt-0.5">
                {{ ctrl.telemetry?.ramUsagePercent != null ? `${Math.round(ctrl.telemetry.ramUsagePercent)} %` : '—' }}
              </div>
            </div>
          </div>

          <div class="flex flex-wrap items-center gap-2 pt-1">
            <RbacTooltip :disabled="!canExecuteRemote" :tooltip="RBAC_TOOLTIPS.REMOTE_EXECUTION">
              <Button
                size="sm"
                :disabled="!canExecuteRemote || !ctrl.id"
                @click="openRemoteView(ctrl)"
                class="h-8 text-xs gap-1.5 bg-primary text-primary-foreground hover:bg-primary/90 cursor-pointer"
              >
                <Monitor class="w-3.5 h-3.5" />
                <span>Remote View</span>
              </Button>
            </RbacTooltip>
            <Button
              size="sm"
              variant="outline"
              :disabled="!ctrl.ipAddress && !ctrl.hostname"
              @click="copyConnectionString(ctrl)"
              class="h-8 text-xs gap-1.5 cursor-pointer"
            >
              <Check v-if="copiedControllerId === ctrl.id" class="w-3.5 h-3.5 text-emerald-500" />
              <Copy v-else class="w-3.5 h-3.5" />
              <span>{{ copiedControllerId === ctrl.id ? 'Copied' : 'Copy SSH Command' }}</span>
            </Button>
            <NuxtLink
              v-if="ctrl.id"
              :to="{ path: '/dashboard/clients', query: { selected: ctrl.id, hostname: ctrl.hostname } }"
              class="inline-flex items-center h-8 px-3 rounded-md border border-border text-xs font-medium text-foreground hover:bg-muted gap-1.5"
            >
              <ExternalLink class="w-3.5 h-3.5" />
              <span>Controller Details</span>
            </NuxtLink>
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

      <!-- TAB 4: DEDICATIONS & SPECIALISTS -->
      <div v-if="activeTab === 'dedications'" class="space-y-5">
        <div v-if="dedicationsLoading" class="p-8 text-center text-xs font-semibold text-muted-foreground animate-pulse">
          Loading machine dedications, specialists, and technological groups...
        </div>

        <div v-else class="space-y-4">
          <!-- Station Inferred / Required Technologies -->
          <div class="p-3.5 rounded-xl bg-card border border-border shadow-xs flex flex-wrap items-center justify-between gap-2">
            <div class="flex items-center gap-2">
              <Cpu class="w-4 h-4 text-primary" />
              <span class="text-xs font-bold uppercase tracking-wider text-foreground">
                Technology Requirements
              </span>
            </div>
            <div class="flex flex-wrap items-center gap-1.5">
              <Badge
                v-for="tech in dedicationsData?.technologies || ['Controls', 'Mechanical']"
                :key="tech"
                variant="outline"
                class="text-[10px] font-mono font-semibold"
                :class="tech === 'Vision' ? 'border-violet-500/40 text-violet-500 bg-violet-500/10' :
                        tech === 'Controls' ? 'border-sky-500/40 text-sky-500 bg-sky-500/10' :
                        tech === 'Mechanical' ? 'border-amber-500/40 text-amber-500 bg-amber-500/10' :
                        'border-border text-foreground bg-muted/30'"
              >
                {{ tech }}
              </Badge>
            </div>
          </div>

          <!-- Active Direct Dedications List (Multiple & Per-Technology) -->
          <div class="p-4 rounded-xl bg-card border border-border shadow-xs space-y-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UserCheck class="w-4 h-4 text-primary" />
                <h3 class="text-xs font-bold uppercase tracking-wider text-foreground">
                  Current Machine Dedication
                </h3>
              </div>
              <Badge
                v-if="dedicationsData?.directDedications?.some(d => d.targetType === 'person') || dedicationsData?.dedicatedPerson"
                variant="outline"
                class="text-[9px] uppercase font-mono text-emerald-500 border-emerald-500/30 bg-emerald-500/10"
              >
                Direct Specialist
              </Badge>
              <Badge
                v-else-if="dedicationsData?.directDedications?.some(d => d.targetType === 'group') || dedicationsData?.dedicatedGroup"
                variant="outline"
                class="text-[9px] uppercase font-mono text-indigo-500 border-indigo-500/30 bg-indigo-500/10"
              >
                Direct Group
              </Badge>
              <Badge
                v-else
                variant="outline"
                class="text-[9px] uppercase font-mono text-muted-foreground border-border bg-muted/40"
              >
                Not Assigned
              </Badge>
            </div>

            <!-- List of Multiple Direct Dedications -->
            <div v-if="dedicationsData?.directDedications?.length" class="space-y-2.5">
              <div
                v-for="ded in dedicationsData.directDedications"
                :key="ded.id || `${ded.targetId}_${ded.technology}`"
                class="p-3 rounded-lg bg-muted/30 border border-border flex items-center justify-between gap-3"
              >
                <div class="flex items-center gap-3">
                  <div
                    class="w-8 h-8 rounded-lg flex items-center justify-center text-xs font-bold shrink-0"
                    :class="ded.targetType === 'group'
                      ? 'bg-indigo-500/10 border border-indigo-500/20 text-indigo-500'
                      : 'bg-primary/10 border border-primary/20 text-primary'"
                  >
                    <Users v-if="ded.targetType === 'group'" class="w-4 h-4" />
                    <span v-else>{{ (ded.targetName || 'U').charAt(0).toUpperCase() }}</span>
                  </div>
                  <div>
                    <div class="text-xs font-semibold text-foreground flex items-center gap-1.5 flex-wrap">
                      <span>{{ ded.targetName }}</span>
                      <Badge
                        variant="outline"
                        class="text-[9px] font-mono px-1.5 py-0 uppercase"
                        :class="ded.technology === 'Vision' ? 'border-violet-500/40 text-violet-500 bg-violet-500/10' :
                                ded.technology === 'Controls' ? 'border-sky-500/40 text-sky-500 bg-sky-500/10' :
                                ded.technology === 'Mechanical' ? 'border-amber-500/40 text-amber-500 bg-amber-500/10' :
                                'border-emerald-500/30 text-emerald-500 bg-emerald-500/10'"
                      >
                        {{ ded.technology }}
                      </Badge>
                      <Badge
                        v-if="ded.isGroupMemberOverride"
                        class="text-[9px] bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/30 px-1.5 py-0"
                      >
                        ⚡ Group Member Override
                      </Badge>
                    </div>
                    <div class="text-[11px] text-muted-foreground font-mono mt-0.5">
                      {{ ded.targetEmail || (ded.targetType === 'group' ? 'Maintenance Group' : ded.targetId) }}
                    </div>
                    <p v-if="ded.isGroupMemberOverride" class="text-[10px] text-amber-600/90 dark:text-amber-400/90 mt-1">
                      Direct assignment supersedes '{{ ded.overriddenGroupName }}' group dedication for {{ ded.technology }} on this machine.
                    </p>
                  </div>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  @click="handleRemoveDedication(ded.targetId, ded.technology)"
                  class="h-7 px-2.5 text-xs text-rose-600 dark:text-rose-400 border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 rounded-lg cursor-pointer shrink-0"
                >
                  <Trash2 class="w-3.5 h-3.5 mr-1" />
                  Unassign
                </Button>
              </div>
            </div>

            <!-- Dedicated Person Card Fallback -->
            <div v-else-if="dedicationsData?.dedicatedPerson" class="p-3 rounded-lg bg-muted/30 border border-border flex items-center justify-between">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-xs font-bold text-primary shrink-0">
                  {{ (dedicationsData.dedicatedPerson.userName || 'U').charAt(0).toUpperCase() }}
                </div>
                <div>
                  <div class="text-xs font-semibold text-foreground flex items-center gap-1.5 flex-wrap">
                    <span>{{ dedicationsData.dedicatedPerson.userName }}</span>
                    <Badge variant="outline" class="text-[9px] font-mono px-1.5 py-0 uppercase border-emerald-500/30 text-emerald-500 bg-emerald-500/10">
                      {{ dedicationsData.dedicatedPerson.technology || 'General' }}
                    </Badge>
                    <Badge
                      v-if="dedicationsData.dedicatedPerson.isGroupMemberOverride"
                      class="text-[9px] bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/30 px-1.5 py-0"
                    >
                      ⚡ Group Member Override
                    </Badge>
                  </div>
                  <div class="text-[11px] text-muted-foreground font-mono mt-0.5">
                    {{ dedicationsData.dedicatedPerson.email || dedicationsData.dedicatedPerson.userId }}
                  </div>
                  <p v-if="dedicationsData.dedicatedPerson.isGroupMemberOverride" class="text-[10px] text-amber-600/90 dark:text-amber-400/90 mt-1">
                    Direct assignment supersedes '{{ dedicationsData.dedicatedPerson.overriddenGroupName }}' group dedication on this machine.
                  </p>
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                @click="handleRemoveDedication()"
                class="h-7 px-2.5 text-xs text-rose-600 dark:text-rose-400 border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 rounded-lg cursor-pointer shrink-0"
              >
                <Trash2 class="w-3.5 h-3.5 mr-1" />
                Unassign
              </Button>
            </div>

            <div v-else class="p-3 text-center rounded-lg bg-muted/20 border border-dashed border-border text-xs text-muted-foreground">
              No direct specialists or groups dedicated yet. Assign below per technology (e.g. Controls, Mechanical, Vision) or rely on automatic technological group coverage.
            </div>
          </div>

          <!-- Dedicate Person or Group Selector -->
          <div class="p-4 rounded-xl bg-card border border-border shadow-xs space-y-3.5">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UserPlus class="w-4 h-4 text-primary" />
                <h3 class="text-xs font-bold uppercase tracking-wider text-foreground">
                  Dedicate to Machine
                </h3>
              </div>
              <!-- Target Type Toggle -->
              <div class="flex items-center p-0.5 rounded-lg bg-muted border border-border text-[11px] font-semibold">
                <button
                  type="button"
                  @click="targetType = 'person'"
                  class="px-2.5 py-1 rounded-md transition-colors cursor-pointer"
                  :class="targetType === 'person' ? 'bg-card text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                >
                  Person
                </button>
                <button
                  type="button"
                  @click="targetType = 'group'"
                  class="px-2.5 py-1 rounded-md transition-colors cursor-pointer"
                  :class="targetType === 'group' ? 'bg-card text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                >
                  Group
                </button>
              </div>
            </div>

            <!-- Technology Scope Selection -->
            <div class="space-y-1.5">
              <Label class="text-[10px] uppercase font-bold text-muted-foreground">Technology Scope</Label>
              <select
                v-model="selectedTechnology"
                class="w-full h-8 px-2.5 text-xs rounded-md bg-background border border-border text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary cursor-pointer"
              >
                <option v-for="tech in availableTechnologies" :key="tech" :value="tech">
                  {{ tech === 'General' ? 'General (Complete Machine)' : `${tech} Support` }}
                </option>
              </select>
            </div>

            <!-- Person Selector -->
            <div v-if="targetType === 'person'" class="space-y-2">
              <Label class="text-[10px] uppercase font-bold text-muted-foreground">Select Specialist / Engineer</Label>
              <select
                v-model="selectedUserId"
                class="w-full h-8 px-2.5 text-xs rounded-md bg-background border border-border text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary cursor-pointer"
              >
                <option value="" disabled>Choose a technician or engineer...</option>
                <option v-for="u in availableUsers" :key="u.id" :value="u.id">
                  {{ u.name || u.email }} ({{ u.role || 'Member' }})
                </option>
              </select>
            </div>

            <!-- Group Selector -->
            <div v-else class="space-y-2">
              <Label class="text-[10px] uppercase font-bold text-muted-foreground">Select Maintenance Group</Label>
              <select
                v-model="selectedGroupId"
                class="w-full h-8 px-2.5 text-xs rounded-md bg-background border border-border text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary cursor-pointer"
              >
                <option value="" disabled>Choose a maintenance team...</option>
                <option v-for="g in availableGroups" :key="g.id" :value="g.id">
                  {{ g.name }} ({{ g.discipline || 'General' }})
                </option>
              </select>
            </div>

            <Button
              @click="handleAssignDedication"
              :disabled="isSubmittingDedication || (targetType === 'person' ? !selectedUserId : !selectedGroupId)"
              class="w-full h-8 text-xs font-bold gap-2 bg-primary text-primary-foreground hover:bg-primary/90 cursor-pointer"
            >
              <RefreshCw v-if="isSubmittingDedication" class="w-3.5 h-3.5 animate-spin" />
              <Check v-else class="w-3.5 h-3.5" />
              <span>Assign Dedication ({{ selectedTechnology }})</span>
            </Button>

            <div v-if="dedicationFeedback" class="p-2.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400 text-xs font-medium">
              {{ dedicationFeedback }}
            </div>
          </div>

          <!-- Technological Groups (Automatic Coverage & Member Superseding) -->
          <div class="p-4 rounded-xl bg-card border border-border shadow-xs space-y-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <Sparkles class="w-4 h-4 text-indigo-500" />
                <h3 class="text-xs font-bold uppercase tracking-wider text-foreground">
                  Technological Groups (Auto Coverage)
                </h3>
              </div>
              <Badge variant="outline" class="text-[9px] uppercase font-mono text-indigo-500 border-indigo-500/30 bg-indigo-500/10">
                Technology Primary
              </Badge>
            </div>
            <p class="text-[11px] text-muted-foreground">
              Teams automatically dedicated based on machine technology capabilities (<span class="text-foreground font-semibold">{{ machine?.machineType || 'General' }}</span>). Dedicated individual group members supersede generic team coverage.
            </p>

            <div v-if="dedicationsData?.technologicalGroupDedications?.length" class="space-y-2.5 pt-1">
              <div
                v-for="tg in dedicationsData.technologicalGroupDedications"
                :key="tg.groupId"
                class="p-3 rounded-lg border transition-colors"
                :class="tg.isSuperseded ? 'bg-amber-500/5 border-amber-500/30' : 'bg-emerald-500/5 border-emerald-500/30'"
              >
                <div class="flex items-start justify-between gap-2">
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-semibold text-foreground">{{ tg.groupName }}</span>
                      <Badge variant="outline" class="text-[9px] font-mono border-border">
                        {{ tg.technology }}
                      </Badge>
                    </div>
                    <div class="text-[11px] mt-1.5" :class="tg.isSuperseded ? 'text-amber-700 dark:text-amber-400 font-medium' : 'text-emerald-700 dark:text-emerald-400 font-medium'">
                      <span v-if="tg.isSuperseded" class="flex items-center gap-1">
                        <ShieldAlert class="w-3.5 h-3.5 shrink-0" />
                        <span>Dedicated member <strong>{{ tg.supersededByUserName }}</strong> supersedes this group on this machine.</span>
                      </span>
                      <span v-else class="flex items-center gap-1">
                        <Check class="w-3.5 h-3.5 shrink-0" />
                        <span>Automatically active: machine operates {{ tg.technology }} technology.</span>
                      </span>
                    </div>
                  </div>
                  <Badge
                    class="text-[9px] uppercase font-mono shrink-0"
                    :class="tg.isSuperseded ? 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30' : 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30'"
                  >
                    {{ tg.isSuperseded ? '⚡ Superseded' : '✓ Active Auto' }}
                  </Badge>
                </div>
              </div>
            </div>
            <div v-else class="p-3 text-center rounded-lg bg-muted/20 border border-dashed border-border text-xs text-muted-foreground">
              No matching technological groups found for this station's configuration.
            </div>
          </div>

          <!-- Inherited Machine Group Hierarchy Dedications -->
          <div v-if="dedicationsData?.inheritedDedications?.length" class="p-4 rounded-xl bg-card border border-border shadow-xs space-y-3">
            <div class="flex items-center gap-2">
              <Layers class="w-4 h-4 text-primary" />
              <h3 class="text-xs font-bold uppercase tracking-wider text-foreground">
                Inherited Dedications (Hierarchy)
              </h3>
            </div>
            <div class="space-y-2">
              <div
                v-for="inh in dedicationsData.inheritedDedications"
                :key="inh.machineGroupId"
                class="p-2.5 rounded-lg bg-muted/30 border border-border flex items-center justify-between text-xs"
              >
                <div>
                  <div class="font-semibold text-foreground flex items-center gap-1.5">
                    <span>{{ inh.machineGroupName }}</span>
                    <Badge variant="outline" class="text-[9px] uppercase font-mono text-muted-foreground border-border">
                      {{ inh.groupType }}
                    </Badge>
                  </div>
                  <p class="text-[11px] text-muted-foreground mt-0.5">
                    Assigned: <span class="text-foreground font-medium">{{ inh.dedicatedPerson?.userName || inh.dedicatedGroup?.groupName }}</span>
                  </p>
                </div>
                <Badge class="text-[9px] bg-primary/10 text-primary border-primary/20">
                  Inherited
                </Badge>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Remote view of the selected controller (resolves that controller's own endpoint) -->
    <RemoteQuickViewModal
      v-model:open="isRemoteViewOpen"
      :controller="remoteViewController"
    />
  </div>
</template>
