<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Factory, Plus, RefreshCw } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import MachineToolbar from '~/components/machines/MachineToolbar.vue'
import MachineCardGrid from '~/components/machines/MachineCardGrid.vue'
import MachineLineHierarchy from '~/components/machines/MachineLineHierarchy.vue'
import StationPhysicalView from '~/components/inventory/StationPhysicalView.vue'
import StationDetailWorkbench from '~/components/machines/StationDetailWorkbench.vue'
import { useGlobalContextMenu } from '~/composables/useGlobalContextMenu'
import { resolvePreferredTechnician } from '~/utils/technicianInheritance'
import { useOrganizationMachineTypes } from '~/composables/useOrganizationMachineTypes'
import { parseMachinesQuery, findMachineByRef, filterMachineDirectory } from '~/utils/deepLinkQueries'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const router = useRouter()
const { openContextMenu } = useGlobalContextMenu()

// View & Filter States
const activeView = ref<'machines' | 'lines' | 'technologies' | 'schematic'>('machines')
const statusFilter = ref<string>('active_floor')
const searchQuery = ref<string>('')
const loading = ref<boolean>(false)

// Data states
const machines = ref<any[]>([])
const lineKpis = ref<Record<string, any>>({})

// Slide-out Workbench Inspector State (Eliminates blocking modal pop-ups)
const isWorkbenchOpen = ref(false)
const workbenchStation = ref<any>(null)
const workbenchTab = ref<'tree' | '3d-twin' | 'diagnostics' | 'lifecycle'>('tree')
const selectedSchematicStationId = ref<string>('')

const currentSchematicStation = computed(() => {
  if (selectedSchematicStationId.value) {
    const found = filteredMachines.value.find(m => m.id === selectedSchematicStationId.value)
    if (found) return found
  }
  return filteredMachines.value[0] || null
})

const findMachine = (idOrIdentifier: string) => findMachineByRef(machines.value, idOrIdentifier)

const openStation = (machine: any, tab: typeof workbenchTab.value = 'tree') => {
  workbenchStation.value = machine
  workbenchTab.value = tab
  isWorkbenchOpen.value = true
}

// Station requested by id before the machine list was loaded (deep link); resolved after the fetch.
const pendingStationId = ref<string | null>(null)

const openTreeForStation = (stationId: string) => {
  const target = findMachine(stationId)
  pendingStationId.value = target ? null : stationId
  // Until the list is loaded only the id is known; the workbench loads the tree by id.
  openStation(target || { id: stationId, name: stationId }, 'tree')
}

const open3dForStation = (m: any) => openStation(m, '3d-twin')

/** Opens the diagnostics of the station the clicked controller belongs to. */
const openQuickView = (ctrl: any, machine?: any) => {
  const parentMachine = machine
    || machines.value.find(m => m.controllers?.some((c: any) => (ctrl.id && c.id === ctrl.id) || (ctrl.hostname && c.hostname === ctrl.hostname)))
  if (!parentMachine) return
  openStation(parentMachine, 'diagnostics')
}

const openDecommissionModal = (m: any) => openStation(m, 'lifecycle')

// A controller node opens that station's diagnostics, a part node its hardware tree.
const handleSchematicComponentSelect = (node: any) => {
  if (!currentSchematicStation.value) return
  openStation(currentSchematicStation.value, node?.controllerId ? 'diagnostics' : 'tree')
}

const handleMachineContextMenu = (m: any, e: MouseEvent) => {
  const pref = resolvePreferredTechnician(m.id, m.machineType, m.groupId, [], [])
  openContextMenu(e, {
    entityType: 'machine',
    entityId: m.id,
    entityName: m.displayName || m.name || m.customIdentifier,
    handle: m.pinnedObjectHandle || undefined,
    machineId: m.id,
    machineName: m.name || m.customIdentifier,
    controllerId: m.controllers?.[0]?.id,
    controllerHostname: m.controllers?.[0]?.hostname,
    ownerTeam: m.responsibleTeams?.[0] ? { name: m.responsibleTeams[0].name } : undefined,
    ownerPerson: (m as any).preferredTechnicianName ? { name: (m as any).preferredTechnicianName } : (pref?.technicianName ? { name: pref.technicianName } : undefined)
  })
}

// Fetch real backend data
const fetchData = async () => {
  loading.value = true
  try {
    const [machRes, kpiRes] = await Promise.allSettled([
      // Decommissioned stations are part of this page (status filter), so ask for them explicitly.
      $fetch<any[]>('/api/machines', { query: { includeDecommissioned: true } }).catch(() => $fetch<any[]>('/api/proxy/v1/Machine')),
      $fetch<any[]>('/api/proxy/v1/Analytics/kpis').catch(() => [])
    ])

    if (machRes.status === 'fulfilled' && Array.isArray(machRes.value)) {
      machines.value = machRes.value
    } else {
      machines.value = []
    }
    machinesLoaded.value = true
    resolveLoadedDeepLinks()

    // Keep an open workbench pointing at the fresh record (and resolve a deep-linked station).
    const openId = pendingStationId.value || workbenchStation.value?.id
    const fresh = openId ? findMachine(openId) : null
    if (fresh && isWorkbenchOpen.value) {
      workbenchStation.value = fresh
      pendingStationId.value = null
    }

    if (kpiRes.status === 'fulfilled' && Array.isArray(kpiRes.value)) {
      const map: Record<string, any> = {}
      for (const k of kpiRes.value) {
        map[k.lineName] = k
        map[k.lineId] = k
      }
      lineKpis.value = map
    }
  } finally {
    loading.value = false
  }
}

const { isMachineTypeVisible } = useOrganizationMachineTypes()

// ── Deep links (?search= ?id= ?tree= ?decommission=) ───────────────────────────
const deepLink = computed(() => parseMachinesQuery(route.query))
const machinesLoaded = ref(false)

/** Station the directory is narrowed to by `?id=` (known once the list is loaded). */
const focusedMachine = computed(() => (deepLink.value.id ? findMachine(deepLink.value.id) : null))

// Filtered machines: status tab, organization machine types and text search; a focused station
// (deep link by id) is shown whatever those filters say.
const filteredMachines = computed(() => filterMachineDirectory(machines.value, {
  statusFilter: statusFilter.value,
  search: searchQuery.value,
  focusId: focusedMachine.value?.id,
  isTypeVisible: isMachineTypeVisible
}))

/** Removes consumed deep-link parameters so the same link can be followed again later. */
const dropQuery = (...keys: string[]) => {
  if (!keys.some(key => route.query[key] != null)) return
  const query = { ...route.query }
  for (const key of keys) delete query[key]
  router.replace({ query })
}

const clearFocus = () => dropQuery('id')

// Editing the search box or the status tab is the user taking over from an id deep link.
const onSearchInput = (value: string) => {
  searchQuery.value = value
  clearFocus()
}
const onStatusFilterChange = (value: string) => {
  statusFilter.value = value
  clearFocus()
}

const onWorkbenchOpenChange = (open: boolean) => {
  isWorkbenchOpen.value = open
  if (!open) dropQuery('tree', 'decommission')
}

// Text a deep link put into the search box (taken out again when the link no longer carries it).
let linkedSearch = ''
const pendingDecommission = ref('')

/** Deep-link parts that need the machine list; runs on every link change and after each load. */
const resolveLoadedDeepLinks = () => {
  if (!machinesLoaded.value) return
  // An id that names no station becomes a text search: "no match" instead of the full directory.
  if (deepLink.value.id && !focusedMachine.value && !deepLink.value.search) {
    searchQuery.value = deepLink.value.id
    linkedSearch = deepLink.value.id
  }
  if (pendingDecommission.value) {
    const target = findMachine(pendingDecommission.value)
    pendingDecommission.value = ''
    if (target) openDecommissionModal(target)
  }
}

const resetFilters = () => {
  searchQuery.value = ''
  linkedSearch = ''
  statusFilter.value = 'active_floor'
  activeView.value = 'machines'
  dropQuery('id', 'search', 'query')
  fetchData()
}

// Each parameter is applied when its own value changes, so an unrelated query update (closing
// the workbench drops ?tree=) never overwrites what the user typed meanwhile.
watch(() => deepLink.value.search, (search) => {
  if (search) {
    searchQuery.value = search
    linkedSearch = search
    activeView.value = 'machines'
  } else if (linkedSearch && searchQuery.value === linkedSearch) {
    searchQuery.value = ''
    linkedSearch = ''
  }
}, { immediate: true })

watch(() => deepLink.value.id, (id) => {
  if (!id) return
  activeView.value = 'machines'
  resolveLoadedDeepLinks()
}, { immediate: true })

watch(() => deepLink.value.tree, (tree) => {
  if (tree) openTreeForStation(tree)
}, { immediate: true })

// The decommission dialog needs the machine record: resolved now, or as soon as the list is loaded.
watch(() => deepLink.value.decommission, (ref) => {
  pendingDecommission.value = ref
  resolveLoadedDeepLinks()
}, { immediate: true })

onMounted(() => {
  fetchData()
})
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-2">
          <Factory class="w-6 h-6 text-primary" />
          <span>Machine Directory & Plant Hierarchy</span>
        </h1>
        <p class="text-xs text-muted-foreground mt-1">
          Monitor shopfloor stations, IPC controllers, kinematic cells, and BOM hierarchies
        </p>
      </div>

      <div class="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          @click="fetchData"
          :disabled="loading"
          class="h-8 text-xs gap-1.5"
        >
          <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': loading }" />
          <span>Refresh</span>
        </Button>
      </div>
    </div>

    <!-- Toolbar: Search, View Switch, Filters -->
    <MachineToolbar
      :search-query="searchQuery"
      v-model:active-view="activeView"
      :status-filter="statusFilter"
      :total-count="filteredMachines.length"
      @update:search-query="onSearchInput"
      @update:status-filter="onStatusFilterChange"
      @reset="resetFilters"
    />

    <!-- Station focused by a deep link (?id=): the directory shows it alone until released -->
    <div
      v-if="focusedMachine"
      data-testid="machine-focus-chip"
      class="flex items-center justify-between gap-3 px-3.5 py-2 bg-primary/10 border border-primary/20 rounded-xl text-xs text-foreground"
    >
      <span>
        Showing station <strong>{{ focusedMachine.displayName || focusedMachine.name || focusedMachine.customIdentifier }}</strong> only
      </span>
      <button
        type="button"
        class="text-xs font-medium text-primary hover:underline cursor-pointer shrink-0"
        @click="clearFocus"
      >
        Show all stations
      </button>
    </div>

    <!-- View: Stations Card Grid -->
    <MachineCardGrid
      v-if="activeView === 'machines'"
      :machines="filteredMachines"
      :loading="loading"
      @open="openStation($event)"
      @open3d="open3dForStation"
      @open-tree="openTreeForStation"
      @open-quick-view="openQuickView"
      @open-decommission="openDecommissionModal"
      @context-menu="handleMachineContextMenu"
    />

    <!-- View: Line or Technology Hierarchy -->
    <MachineLineHierarchy
      v-else-if="activeView === 'lines' || activeView === 'technologies'"
      :machines="filteredMachines"
      :mode="activeView === 'lines' ? 'lines' : 'technologies'"
      :line-kpis="lineKpis"
      @open="openStation($event)"
      @open3d="open3dForStation"
      @open-tree="openTreeForStation"
      @open-quick-view="openQuickView"
      @open-decommission="openDecommissionModal"
      @context-menu="handleMachineContextMenu"
    />

    <!-- View: Physical Station Schematic Topology -->
    <div v-else-if="activeView === 'schematic'" class="space-y-4">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3.5 rounded-2xl bg-card border border-border shadow-xs">
        <div class="flex items-center gap-2.5">
          <span class="text-xs font-bold uppercase tracking-wider text-muted-foreground">Select Cell / Station:</span>
          <select
            v-model="selectedSchematicStationId"
            class="h-8 px-2.5 text-xs bg-background border border-border rounded-lg text-foreground font-mono font-semibold focus:outline-none focus:border-primary"
          >
            <option v-for="m in filteredMachines" :key="m.id" :value="m.id">
              {{ m.displayName || m.name }} ({{ m.lineGroupName || m.groupId || 'Unassigned' }})
            </option>
          </select>
        </div>

        <div class="flex items-center gap-2 text-[11px] text-muted-foreground font-mono">
          <span class="size-2 rounded-full bg-emerald-500 animate-pulse" />
          <span>Interactive Kinematic, Pneumatic & Fieldbus Topology</span>
        </div>
      </div>

      <div v-if="!currentSchematicStation" class="text-center py-12 border border-dashed border-border rounded-xl">
        <p class="text-xs text-muted-foreground font-mono">No station matches the current filter criteria.</p>
      </div>
      <StationPhysicalView
        v-else
        :station-id="currentSchematicStation.id"
        :station-name="currentSchematicStation.displayName || currentSchematicStation.name"
        @select-component="handleSchematicComponentSelect"
      />
    </div>



    <!-- Unified Slide-out Station Detail Workbench -->
    <StationDetailWorkbench
      :open="isWorkbenchOpen"
      :machine="workbenchStation"
      :initial-tab="workbenchTab"
      @update:open="onWorkbenchOpenChange"
      @refresh="fetchData"
    />
  </div>
</template>
