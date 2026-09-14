<script setup lang="ts">
import { ref, computed, watch, defineAsyncComponent } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  Plus, Camera, RefreshCw, WifiOff, LayoutList, Columns,
  Activity, QrCode, Users, FolderTree, History, CheckCircle2, Wrench,
  Filter, X
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import TicketMetricsOverview from '~/components/tickets/TicketMetricsOverview.vue'
import TicketList from '~/components/tickets/TicketList.vue'
import TicketKanbanBoard from '~/components/tickets/TicketKanbanBoard.vue'
import TicketResolvedLog from '~/components/tickets/TicketResolvedLog.vue'
import TagFilterBar from '~/components/tickets/TagFilterBar.vue'
import TicketCreateModal from '~/components/tickets/TicketCreateModal.vue'
import TicketDetailDrawer from '~/components/tickets/TicketDetailDrawer.vue'
import SimulatorControlModal from '~/components/dashboard/SimulatorControlModal.vue'
import MachineQrModal from '~/components/tickets/MachineQrModal.vue'
import PreferredTechniciansModal from '~/components/tickets/PreferredTechniciansModal.vue'
import MachineGroupManagerModal from '~/components/tickets/MachineGroupManagerModal.vue'
const QrScanner = defineAsyncComponent(() => import('~/components/ui/qr-scanner/QrScanner.vue'))
import { useMaintenance } from '~/composables/useMaintenance'
import { parseQrUri } from '~/utils/qrActionGenerator'
import type { MaintenanceTicket, TicketStatus } from '~/types/maintenance'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()
const route = useRoute()

const {
  tickets,
  metrics,
  selectedTicket,
  isLoading,
  pendingOfflineCount,
  fetchTickets,
  updateStatus
} = useMaintenance()

// ── View Modes ─────────────────────────────────────────────────────────────
const activeViewMode = ref<'list' | 'kanban' | 'resolved'>('list')

// ── Modals & Drawers ───────────────────────────────────────────────────────
const showCreateModal = ref(false)
const showDetailDrawer = ref(false)
const showQrScanner = ref(false)
const showSimulatorModal = ref(false)
const showQrGeneratorModal = ref(false)
const showDelegationModal = ref(false)
const showGroupManagerModal = ref(false)

// ── Prefills for Ticket Creation ───────────────────────────────────────────
const prefilledStationId = ref('')
const prefilledMachineType = ref('')
const prefilledGroupId = ref('')

// ── Tag Filtering ──────────────────────────────────────────────────────────
const selectedTags = ref<string[]>([])

// ── Metric Filter from Hero Cards ──────────────────────────────────────────
const activeMetricFilter = ref<'open' | 'critical' | 'pending_parts' | 'overdue' | 'resolved' | 'sla' | null>(null)

const activeMetricFilterLabel = computed(() => {
  switch (activeMetricFilter.value) {
    case 'open': return 'Active Open Incidents'
    case 'critical': return 'Critical & High Alerts'
    case 'pending_parts': return 'Pending Parts'
    case 'overdue': return 'Overdue SLA'
    case 'resolved': return 'Resolved & Closed'
    case 'sla': return 'SLA Health Incidents'
    default: return ''
  }
})

const onMetricFilterChange = (filter: string | null) => {
  activeMetricFilter.value = filter as any
  if (filter === 'resolved') {
    activeViewMode.value = 'resolved'
  } else if (activeViewMode.value === 'resolved' && filter !== null) {
    activeViewMode.value = 'list'
  }
}

const clearMetricFilter = () => {
  activeMetricFilter.value = null
  if (activeViewMode.value === 'resolved') {
    activeViewMode.value = 'list'
  }
}

const resetPageFilters = () => {
  activeMetricFilter.value = null
  selectedTags.value = []
  prefilledStationId.value = ''
  activeViewMode.value = 'list'
  router.push('/dashboard/tickets')
  fetchTickets()
}

const availableTags = computed(() => {
  const set = new Set<string>()
  for (const t of tickets.value) {
    if (t.tags && Array.isArray(t.tags)) {
      for (const tag of t.tags) {
        set.add(tag)
      }
    }
  }
  return Array.from(set)
})

watch(() => [route.query, tickets.value], () => {
  const q = route.query
  if (q.stationId) {
    prefilledStationId.value = q.stationId as string
  }
  if (q.create === 'true') {
    showCreateModal.value = true
  }
  if (q.ticketId) {
    const match = tickets.value.find(t => t.id === q.ticketId)
    if (match) onSelectTicket(match)
  }
  if (q.team) {
    const teamStr = String(q.team)
    if (!selectedTags.value.includes(teamStr)) {
      selectedTags.value.push(teamStr)
    }
  }
  if (q.technician) {
    const techStr = String(q.technician)
    if (!selectedTags.value.includes(techStr)) {
      selectedTags.value.push(techStr)
    }
  }
}, { immediate: true })

const displayedTickets = computed(() => {
  let list = tickets.value
  const q = route.query

  if (q.stationId && q.create !== 'true') {
    const target = (q.stationId as string).toLowerCase()
    list = list.filter(t => 
      t.stationId?.toLowerCase() === target ||
      t.machineId?.toLowerCase() === target ||
      t.title?.toLowerCase().includes(target) ||
      (t.tags && t.tags.some(tag => tag.toLowerCase() === target))
    )
  }

  if (q.technician) {
    const tech = (q.technician as string).toLowerCase()
    list = list.filter(t => t.assignedTechnicianName?.toLowerCase().includes(tech))
  }

  if (selectedTags.value.length > 0) {
    list = list.filter(t => {
      if (!t.tags || !Array.isArray(t.tags)) return false
      return selectedTags.value.some(sel => t.tags!.includes(sel))
    })
  }

  if (activeMetricFilter.value) {
    switch (activeMetricFilter.value) {
      case 'open':
        list = list.filter(t => t.status === 'Open' || t.status === 'In_Progress' || t.status === 'Pending_Parts')
        break
      case 'critical':
        list = list.filter(t => (t.severity || '').toLowerCase() === 'critical' || (t.severity || '').toLowerCase() === 'high' || (t.title || '').toLowerCase().includes('critical'))
        break
      case 'pending_parts':
        list = list.filter(t => t.status === 'Pending_Parts')
        break
      case 'overdue':
        list = list.filter(t => (t as any).isOverdue || (t.tags && t.tags.includes('Overdue')))
        break
      case 'resolved':
        list = list.filter(t => t.status === 'Resolved' || t.status === 'Closed')
        break
      case 'sla':
        list = list.filter(t => (t as any).slaDueAt || (t as any).isOverdue)
        break
    }
  }

  return list
})

// ── Action Handlers ────────────────────────────────────────────────────────
function onSelectTicket(tkt: MaintenanceTicket) {
  selectedTicket.value = tkt
  showDetailDrawer.value = true
}

function onTicketCreated() {
  fetchTickets()
}

function onQrScanned(code: string) {
  showQrScanner.value = false

  // Try parsing as action URI
  const actionPayload = parseQrUri(code)
  if (actionPayload) {
    if (actionPayload.action === 'report-incident') {
      prefilledStationId.value = actionPayload.stationId || ''
      prefilledMachineType.value = actionPayload.machineType || ''
      prefilledGroupId.value = actionPayload.groupId || ''
      showCreateModal.value = true
      return
    } else if (actionPayload.action === 'view-ticket' && actionPayload.ticketId) {
      const match = tickets.value.find(t => t.id === actionPayload.ticketId)
      if (match) {
        onSelectTicket(match)
        return
      }
    }
  }

  // Fallback: direct station ID
  prefilledStationId.value = code
  prefilledMachineType.value = ''
  prefilledGroupId.value = ''
  showCreateModal.value = true
}

function onMoveStatus(ticketId: string, status: TicketStatus) {
  updateStatus(ticketId, status)
}
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div
        role="button"
        tabindex="0"
        @click="resetPageFilters"
        @keydown.enter="resetPageFilters"
        class="flex items-center gap-3 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-slate-900/60"
        title="Click to reset filters and refresh tickets"
      >
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 group-hover:scale-105 group-hover:bg-indigo-500/20 transition-all">
          <Wrench class="size-6" />
        </div>
        <div>
          <div class="flex items-center gap-2.5">
            <h1 class="text-2xl font-bold tracking-tight text-slate-100 group-hover:text-white transition-colors">
              Maintenance & Incident Management
            </h1>
            <Badge
              v-if="pendingOfflineCount > 0"
              variant="outline"
              class="border-amber-500/40 text-amber-400 bg-amber-500/10 text-xs font-medium flex items-center gap-1.5 px-2 py-0.5 rounded-md"
            >
              <WifiOff class="size-3" />
              <span>{{ pendingOfflineCount }} Offline Queued</span>
            </Badge>
          </div>
          <p class="text-sm text-slate-400 mt-0.5 group-hover:text-slate-300 transition-colors">
            Plant floor incident lifecycle, 4-tier templates, technician delegation, and machine groups
          </p>
        </div>
      </div>

      <!-- Action Toolbar -->
      <div class="flex flex-wrap items-center gap-2">
        <!-- 3-Way View Switch -->
        <div class="bg-slate-900 p-0.5 rounded-lg border border-slate-800 flex gap-0.5">
          <Button
            variant="ghost"
            @click="activeViewMode = 'list'"
            :class="activeViewMode === 'list' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 transition-colors"
          >
            <LayoutList class="w-3.5 h-3.5 mr-1" />
            List
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'kanban'"
            :class="activeViewMode === 'kanban' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 transition-colors"
          >
            <Columns class="w-3.5 h-3.5 mr-1" />
            Kanban
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'resolved'"
            :class="activeViewMode === 'resolved' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 transition-colors"
          >
            <History class="w-3.5 h-3.5 mr-1" />
            Resolved Log
          </Button>
        </div>

        <!-- Management Navigation Buttons -->
        <NuxtLink to="/dashboard/machine-groups">
          <Button
            variant="outline"
            size="sm"
            class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors"
            title="Manage recursive machine groups and technology clusters"
          >
            <FolderTree class="h-3.5 w-3.5 text-indigo-400" />
            <span>Machine Groups</span>
          </Button>
        </NuxtLink>

        <NuxtLink to="/dashboard/delegations">
          <Button
            variant="outline"
            size="sm"
            class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors"
            title="Manage shift attendance, engineer dedication, and Teams OOO state"
          >
            <Users class="h-3.5 w-3.5 text-cyan-400" />
            <span>Delegation & Roster</span>
          </Button>
        </NuxtLink>

        <Button
          variant="outline"
          size="sm"
          @click="showQrGeneratorModal = true"
          class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors"
          title="Generate Actionable QR Code URIs"
        >
          <QrCode class="h-3.5 w-3.5 text-indigo-400" />
          <span>Action QR</span>
        </Button>

        <Button
          variant="outline"
          size="sm"
          @click="showSimulatorModal = true"
          class="border-indigo-500/30 bg-indigo-500/10 text-indigo-300 hover:bg-indigo-500/20 rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors"
        >
          <Activity class="h-3.5 w-3.5 text-indigo-400" />
          <span>Fleet Sim</span>
        </Button>

        <Button
          variant="outline"
          size="sm"
          @click="showQrScanner = true"
          class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors"
        >
          <Camera class="h-3.5 w-3.5 text-indigo-400" />
          <span>Scan QR</span>
        </Button>

        <Button
          size="sm"
          @click="showCreateModal = true"
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg px-3.5 h-8 text-xs font-medium flex items-center gap-1.5 shadow-sm transition-colors"
        >
          <Plus class="h-3.5 w-3.5" />
          <span>Report Incident</span>
        </Button>
      </div>
    </div>

    <!-- QR Scanner Modal Popup -->
    <div v-if="showQrScanner" class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
      <QrScanner @scanned="onQrScanned" @close="showQrScanner = false" />
    </div>

    <!-- Metrics Cards Overview -->
    <TicketMetricsOverview
      :metrics="metrics || undefined"
      :active-filter="activeMetricFilter"
      @filter-change="onMetricFilterChange"
    />

    <!-- Active Metric Filter Chip Bar -->
    <div
      v-if="activeMetricFilter"
      class="flex items-center justify-between px-3.5 py-2 bg-indigo-500/10 border border-indigo-500/20 rounded-xl text-xs text-indigo-300 animate-in fade-in duration-200 shadow-sm"
    >
      <div class="flex items-center gap-2">
        <Filter class="size-3.5 text-indigo-400" />
        <span>Filtering by: <strong class="text-white capitalize">{{ activeMetricFilterLabel }}</strong></span>
        <span class="text-slate-400">({{ displayedTickets.length }} incident{{ displayedTickets.length === 1 ? '' : 's' }} matched)</span>
      </div>
      <button
        @click="clearMetricFilter"
        class="text-xs text-indigo-300 hover:text-white flex items-center gap-1 font-medium px-2 py-0.5 rounded hover:bg-indigo-500/20 transition-colors"
      >
        <X class="size-3.5" />
        <span>Clear Filter</span>
      </button>
    </div>

    <!-- Tag Cloud Filter Bar -->
    <div v-if="availableTags.length > 0" class="p-3 bg-slate-900 border border-slate-800 rounded-xl shadow-sm">
      <TagFilterBar
        :available-tags="availableTags"
        :selected-tags="selectedTags"
        @update:selected-tags="selectedTags = $event"
      />
    </div>

    <!-- Main View Switcher -->
    <template v-if="activeViewMode === 'list'">
      <TicketList
        :tickets="displayedTickets"
        :loading="isLoading"
        @selectTicket="onSelectTicket"
        @filterChange="fetchTickets"
      />
    </template>

    <template v-else-if="activeViewMode === 'kanban'">
      <TicketKanbanBoard
        :tickets="displayedTickets"
        @selectTicket="onSelectTicket"
        @moveStatus="onMoveStatus"
      />
    </template>

    <template v-else-if="activeViewMode === 'resolved'">
      <TicketResolvedLog
        :tickets="displayedTickets"
      />
    </template>

    <!-- Ticket Creation Modal with 4-Tier Templates & Attachments -->
    <TicketCreateModal
      :open="showCreateModal"
      :prefilled-station="prefilledStationId"
      :prefilled-machine-type="prefilledMachineType"
      :prefilled-group-id="prefilledGroupId"
      @update:open="showCreateModal = $event"
      @created="onTicketCreated"
    />

    <!-- Ticket Detail & Live Comment Drawer with 8 Statuses & Lightbox -->
    <TicketDetailDrawer
      :ticket="selectedTicket"
      :open="showDetailDrawer"
      @update:open="showDetailDrawer = $event"
      @updated="fetchTickets"
    />

    <!-- Actionable Machine QR Code Generator Modal -->
    <MachineQrModal
      :open="showQrGeneratorModal"
      :station-id="prefilledStationId || 'STATION-OP10-01'"
      :station-name="'OP10 Machining Cell'"
      :machine-type="'Milling'"
      :group-id="'grp-line06'"
      @close="showQrGeneratorModal = false"
    />

    <!-- 3-Tier Delegation & Teams OOO Modal -->
    <PreferredTechniciansModal
      :open="showDelegationModal"
      @close="showDelegationModal = false"
    />

    <!-- Recursive Machine Group Manager Modal -->
    <MachineGroupManagerModal
      :open="showGroupManagerModal"
      @close="showGroupManagerModal = false"
      @groupSelected="prefilledGroupId = $event"
    />

    <!-- Interactive Fleet Simulator & Dev Generator Modal -->
    <SimulatorControlModal
      :open="showSimulatorModal"
      @update:open="showSimulatorModal = $event"
      @ticketCreated="fetchTickets"
    />
  </div>
</template>
