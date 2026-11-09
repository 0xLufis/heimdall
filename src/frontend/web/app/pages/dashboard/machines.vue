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
const workbenchTab = ref<'tree' | 'diagnostics' | 'lifecycle'>('tree')
const selectedSchematicStationId = ref<string>('')

const currentSchematicStation = computed(() => {
  if (selectedSchematicStationId.value) {
    const found = filteredMachines.value.find(m => m.id === selectedSchematicStationId.value)
    if (found) return found
  }
  return filteredMachines.value[0] || null
})

const openTreeForStation = (stationId: string) => {
  const target = machines.value.find(m => m.id === stationId || m.name === stationId || m.customIdentifier === stationId) || { id: stationId, name: stationId }
  workbenchStation.value = target
  workbenchTab.value = 'tree'
  isWorkbenchOpen.value = true
}

const openQuickView = (ctrl: any) => {
  const parentMachine = machines.value.find(m => m.controllers?.some((c: any) => c.id === ctrl.id || c.hostname === ctrl.hostname)) || {
    id: ctrl.id || ctrl.hostname,
    name: ctrl.name || ctrl.hostname,
    controllers: [ctrl]
  }
  workbenchStation.value = parentMachine
  workbenchTab.value = 'diagnostics'
  isWorkbenchOpen.value = true
}

const openDecommissionModal = (m: any) => {
  workbenchStation.value = m
  workbenchTab.value = 'lifecycle'
  isWorkbenchOpen.value = true
}

const handleSchematicComponentSelect = (node: any) => {
  if (currentSchematicStation.value) {
    workbenchStation.value = currentSchematicStation.value
    workbenchTab.value = 'tree'
    isWorkbenchOpen.value = true
  }
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
      $fetch<any[]>('/api/machines').catch(() => $fetch<any[]>('/api/proxy/v1/Machine')),
      $fetch<any[]>('/api/proxy/v1/Analytics/kpis').catch(() => [])
    ])

    if (machRes.status === 'fulfilled' && Array.isArray(machRes.value)) {
      machines.value = machRes.value
    } else {
      machines.value = []
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

// Filtered machines
const filteredMachines = computed(() => {
  let list = machines.value

  // Status Filter
  if (statusFilter.value === 'active_floor') {
    list = list.filter(m => m.status === 'active' || m.status === 'maintenance')
  } else if (statusFilter.value === 'maintenance') {
    list = list.filter(m => m.status === 'maintenance')
  } else if (statusFilter.value === 'decommissioned') {
    list = list.filter(m => m.status === 'decommissioned' || m.status === 'dismantled')
  }

  // Filter out machine types/categories disabled by the organization
  list = list.filter(m => isMachineTypeVisible(m.machineType))

  // Search Query
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.trim().toLowerCase()
    list = list.filter(m =>
      (m.name && m.name.toLowerCase().includes(q)) ||
      (m.displayName && m.displayName.toLowerCase().includes(q)) ||
      (m.customIdentifier && m.customIdentifier.toLowerCase().includes(q)) ||
      (m.groupId && m.groupId.toLowerCase().includes(q)) ||
      (m.controllers && m.controllers.some((c: any) => c.hostname && c.hostname.toLowerCase().includes(q)))
    )
  }

  return list
})

const resetFilters = () => {
  searchQuery.value = ''
  statusFilter.value = 'active_floor'
  activeView.value = 'machines'
  fetchData()
}

// Watch query params
watch(() => route.query, (q) => {
  if (q.id || q.search || q.query) {
    searchQuery.value = (q.id || q.search || q.query) as string
    activeView.value = 'machines'
  }
  if (q.tree) {
    openTreeForStation(q.tree as string)
  }
  if (q.decommission) {
    const target = machines.value.find(m => m.id === q.decommission || m.customIdentifier === q.decommission)
    if (target) openDecommissionModal(target)
  }
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
      v-model:search-query="searchQuery"
      v-model:active-view="activeView"
      v-model:status-filter="statusFilter"
      :total-count="filteredMachines.length"
      @reset="resetFilters"
    />

    <!-- View: Stations Card Grid -->
    <MachineCardGrid
      v-if="activeView === 'machines'"
      :machines="filteredMachines"
      :loading="loading"
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
              {{ m.displayName || m.name }} ({{ m.groupId || 'Line 1' }})
            </option>
          </select>
        </div>

        <div class="flex items-center gap-2 text-[11px] text-muted-foreground font-mono">
          <span class="size-2 rounded-full bg-emerald-500 animate-pulse" />
          <span>Interactive Kinematic, Pneumatic & Fieldbus Topology</span>
        </div>
      </div>

      <StationPhysicalView
        :station-id="currentSchematicStation?.id || 'cell-01'"
        :station-name="currentSchematicStation?.displayName || currentSchematicStation?.name || 'Assembly Cell'"
        @select-component="handleSchematicComponentSelect"
      />
    </div>



    <!-- Unified Slide-out Station Detail Workbench -->
    <StationDetailWorkbench
      :open="isWorkbenchOpen"
      :machine="workbenchStation"
      :initial-tab="workbenchTab"
      @update:open="isWorkbenchOpen = $event"
      @refresh="fetchData"
    />
  </div>
</template>
