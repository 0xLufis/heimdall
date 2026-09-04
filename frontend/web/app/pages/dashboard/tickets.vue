<script setup lang="ts">
import { ref, computed, defineAsyncComponent } from 'vue'
import {
  Plus, Camera, RefreshCw, WifiOff, LayoutList, Columns,
  Activity, QrCode, Users, FolderTree, History, CheckCircle2
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

const displayedTickets = computed(() => {
  if (selectedTags.value.length === 0) return tickets.value
  return tickets.value.filter(t => {
    if (!t.tags || !Array.isArray(t.tags)) return false
    return selectedTags.value.some(sel => t.tags!.includes(sel))
  })
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
    <div class="flex flex-col xl:flex-row xl:items-end justify-between gap-4">
      <div>
        <div class="flex items-center gap-3">
          <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">
            Maintenance & Floor Incidents
          </h3>
          <Badge
            v-if="pendingOfflineCount > 0"
            variant="outline"
            class="border-amber-500/40 text-amber-400 bg-amber-500/10 text-xs font-black uppercase tracking-widest flex items-center gap-1.5 px-3 py-1"
          >
            <WifiOff class="h-3.5 w-3.5" />
            <span>{{ pendingOfflineCount }} Offline Queued</span>
          </Badge>
        </div>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">
          Live Floor Incident Lifecycle, 4-Tier Templates, Teams OOO Exclusion & Recursive Groups
        </p>
      </div>

      <!-- Action Toolbar -->
      <div class="flex flex-wrap items-center gap-2.5">
        <!-- 3-Way View Switch -->
        <div class="bg-slate-900 p-1 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button
            variant="ghost"
            @click="activeViewMode = 'list'"
            :class="activeViewMode === 'list' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-3 py-1.5 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <LayoutList class="w-3.5 h-3.5 mr-1" />
            List
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'kanban'"
            :class="activeViewMode === 'kanban' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-3 py-1.5 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <Columns class="w-3.5 h-3.5 mr-1" />
            Kanban (8)
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'resolved'"
            :class="activeViewMode === 'resolved' ? 'bg-emerald-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-3 py-1.5 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <History class="w-3.5 h-3.5 mr-1" />
            Resolved Log
          </Button>
        </div>

        <!-- Management Modals Buttons -->
        <Button
          variant="outline"
          @click="showGroupManagerModal = true"
          class="border-slate-800 bg-slate-900/80 text-slate-300 hover:bg-slate-800 rounded-2xl px-3.5 h-10 text-xs font-bold uppercase tracking-wider flex items-center gap-1.5"
          title="Manage recursive machine groups and technology clusters"
        >
          <FolderTree class="h-4 w-4 text-indigo-400" />
          <span>Machine Groups</span>
        </Button>

        <Button
          variant="outline"
          @click="showDelegationModal = true"
          class="border-slate-800 bg-slate-900/80 text-slate-300 hover:bg-slate-800 rounded-2xl px-3.5 h-10 text-xs font-bold uppercase tracking-wider flex items-center gap-1.5"
          title="Manage shift attendance, engineer dedication, and Teams OOO state"
        >
          <Users class="h-4 w-4 text-cyan-400" />
          <span>Delegation & OOO</span>
        </Button>

        <Button
          variant="outline"
          @click="showQrGeneratorModal = true"
          class="border-slate-800 bg-slate-900/80 text-slate-300 hover:bg-slate-800 rounded-2xl px-3.5 h-10 text-xs font-bold uppercase tracking-wider flex items-center gap-1.5"
          title="Generate Actionable QR Code URIs"
        >
          <QrCode class="h-4 w-4 text-purple-400" />
          <span>Action QR</span>
        </Button>

        <Button
          variant="outline"
          @click="showSimulatorModal = true"
          class="border-indigo-500/30 bg-indigo-500/10 text-indigo-300 hover:bg-indigo-500/20 rounded-2xl px-3.5 h-10 text-xs font-bold uppercase tracking-wider flex items-center gap-1.5"
        >
          <Activity class="h-4 w-4 text-indigo-400" />
          <span>Fleet Sim</span>
        </Button>

        <Button
          variant="outline"
          @click="showQrScanner = true"
          class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 rounded-2xl px-4 h-10 text-xs font-bold uppercase tracking-wider flex items-center gap-1.5"
        >
          <Camera class="h-4 w-4 text-indigo-400" />
          <span>Scan QR</span>
        </Button>

        <button
          type="button"
          @click="showCreateModal = true"
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-5 h-10 text-xs font-bold uppercase tracking-wider flex items-center gap-2 border-0 shadow-lg cursor-pointer transition-colors"
        >
          <Plus class="h-4 w-4" />
          <span>Report Incident</span>
        </button>
      </div>
    </div>

    <!-- QR Scanner Modal Popup -->
    <div v-if="showQrScanner" class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
      <QrScanner @scanned="onQrScanned" @close="showQrScanner = false" />
    </div>

    <!-- Metrics Cards Overview -->
    <TicketMetricsOverview :metrics="metrics || undefined" />

    <!-- Tag Cloud Filter Bar -->
    <div v-if="availableTags.length > 0" class="p-3 bg-slate-900/60 border border-slate-800/80 rounded-2xl">
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
