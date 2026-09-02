<script setup lang="ts">
import { ref } from 'vue'
import { Plus, Camera, RefreshCw, WifiOff, LayoutList, Columns } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import TicketMetricsOverview from '~/components/tickets/TicketMetricsOverview.vue'
import TicketList from '~/components/tickets/TicketList.vue'
import TicketKanbanBoard from '~/components/tickets/TicketKanbanBoard.vue'
import TicketCreateModal from '~/components/tickets/TicketCreateModal.vue'
import TicketDetailDrawer from '~/components/tickets/TicketDetailDrawer.vue'
import { QrScanner } from '~/components/ui/qr-scanner'
import { useMaintenance } from '~/composables/useMaintenance'
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

const activeViewMode = ref<'list' | 'kanban'>('list')
const showCreateModal = ref(false)
const showDetailDrawer = ref(false)
const showQrScanner = ref(false)
const prefilledStationId = ref('')

function onSelectTicket(tkt: MaintenanceTicket) {
  selectedTicket.value = tkt
  showDetailDrawer.value = true
}

function onTicketCreated() {
  fetchTickets()
}

function onQrScanned(code: string) {
  showQrScanner.value = false
  prefilledStationId.value = code
  showCreateModal.value = true
}

function onMoveStatus(ticketId: string, status: TicketStatus) {
  updateStatus(ticketId, status)
}
</script>

<template>
  <div class="space-y-8 pb-12">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
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
          Live Floor Incident Dispatching, SignalR WebSocket Push & PWA Offline Sync
        </p>
      </div>

      <div class="flex items-center gap-3">
        <!-- View Toggle (List vs Kanban) -->
        <div class="bg-slate-900 p-1.5 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button
            variant="ghost"
            @click="activeViewMode = 'list'"
            :class="activeViewMode === 'list' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <LayoutList class="w-3.5 h-3.5 mr-1.5" />
            List
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'kanban'"
            :class="activeViewMode === 'kanban' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <Columns class="w-3.5 h-3.5 mr-1.5" />
            Kanban
          </Button>
        </div>

        <Button
          variant="outline"
          @click="showQrScanner = true"
          class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 rounded-2xl px-5 h-11 text-xs font-bold uppercase tracking-wider flex items-center gap-2"
        >
          <Camera class="h-4 w-4 text-indigo-400" />
          <span>Scan Station QR</span>
        </Button>

        <Button
          @click="showCreateModal = true"
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-6 h-11 text-xs font-bold uppercase tracking-wider flex items-center gap-2 border-0 shadow-lg"
        >
          <Plus class="h-4 w-4" />
          <span>Report Incident</span>
        </Button>
      </div>
    </div>

    <!-- QR Scanner Modal Popup -->
    <div v-if="showQrScanner" class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
      <QrScanner @scanned="onQrScanned" @close="showQrScanner = false" />
    </div>

    <!-- Metrics Cards Overview -->
    <TicketMetricsOverview :metrics="metrics || undefined" />

    <!-- Main View: List or Kanban -->
    <template v-if="activeViewMode === 'list'">
      <TicketList
        :tickets="tickets"
        :loading="isLoading"
        @selectTicket="onSelectTicket"
        @filterChange="fetchTickets"
      />
    </template>

    <template v-else>
      <TicketKanbanBoard
        :tickets="tickets"
        @selectTicket="onSelectTicket"
        @moveStatus="onMoveStatus"
      />
    </template>

    <!-- Ticket Creation Modal -->
    <TicketCreateModal
      :open="showCreateModal"
      :prefilled-station="prefilledStationId"
      @update:open="showCreateModal = $event"
      @created="onTicketCreated"
    />

    <!-- Ticket Detail & Live Comment Drawer -->
    <TicketDetailDrawer
      :ticket="selectedTicket"
      :open="showDetailDrawer"
      @update:open="showDetailDrawer = $event"
      @updated="fetchTickets"
    />
  </div>
</template>
