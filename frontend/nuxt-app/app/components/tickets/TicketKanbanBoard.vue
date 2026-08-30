<script setup lang="ts">
import { ref } from 'vue'
import type { MaintenanceTicket, TicketStatus } from '~/types/maintenance'
import { Badge } from '~/components/ui/badge'
import { Clock, User, ArrowRight, CheckCircle, Wrench, AlertCircle } from 'lucide-vue-next'

const props = defineProps<{
  tickets: MaintenanceTicket[]
}>()

const emit = defineEmits<{
  (e: 'selectTicket', ticket: MaintenanceTicket): void
  (e: 'moveStatus', ticketId: string, status: TicketStatus): void
}>()

const columns: { id: TicketStatus; label: string; color: string; nextStatus?: TicketStatus; nextLabel?: string }[] = [
  { id: 'Open', label: 'Open', color: 'border-blue-500/30 bg-blue-500/10 text-blue-400', nextStatus: 'In_Progress', nextLabel: 'Start' },
  { id: 'In_Progress', label: 'In Progress', color: 'border-indigo-500/30 bg-indigo-500/10 text-indigo-400', nextStatus: 'Resolved', nextLabel: 'Resolve' },
  { id: 'Pending_Parts', label: 'Pending Parts', color: 'border-amber-500/30 bg-amber-500/10 text-amber-400', nextStatus: 'In_Progress', nextLabel: 'Resume' },
  { id: 'Resolved', label: 'Resolved', color: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-400' }
]

const draggedTicketId = ref<string | null>(null)
const dragOverColumn = ref<TicketStatus | null>(null)

const getTicketsByStatus = (status: TicketStatus) => {
  return props.tickets.filter(t => t.status === status)
}

const getPriorityClass = (priority: string) => {
  switch (priority) {
    case 'Critical':
      return 'bg-destructive/20 text-destructive border-destructive/30'
    case 'High':
      return 'bg-orange-500/20 text-orange-400 border-orange-500/30'
    case 'Medium':
      return 'bg-amber-500/20 text-amber-400 border-amber-500/30'
    default:
      return 'bg-slate-800 text-slate-400 border-slate-700'
  }
}

function onDragStart(event: DragEvent, ticketId: string) {
  draggedTicketId.value = ticketId
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', ticketId)
  }
}

function onDragOver(event: DragEvent, colId: TicketStatus) {
  event.preventDefault()
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
  dragOverColumn.value = colId
}

function onDragLeave(colId: TicketStatus) {
  if (dragOverColumn.value === colId) {
    dragOverColumn.value = null
  }
}

function onDrop(event: DragEvent, targetStatus: TicketStatus) {
  event.preventDefault()
  const ticketId = event.dataTransfer?.getData('text/plain') || draggedTicketId.value
  draggedTicketId.value = null
  dragOverColumn.value = null

  if (ticketId) {
    emit('moveStatus', ticketId, targetStatus)
  }
}

function onQuickMove(event: Event, ticketId: string, nextStatus: TicketStatus) {
  event.stopPropagation()
  emit('moveStatus', ticketId, nextStatus)
}
</script>

<template>
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
    <div
      v-for="col in columns"
      :key="col.id"
      @dragover="onDragOver($event, col.id)"
      @dragleave="onDragLeave(col.id)"
      @drop="onDrop($event, col.id)"
      class="bg-slate-950 border rounded-3xl p-4 flex flex-col gap-3 min-h-[480px] transition-colors duration-200"
      :class="dragOverColumn === col.id ? 'border-indigo-500 bg-indigo-950/20 shadow-inner' : 'border-slate-800'"
    >
      <!-- Column Header -->
      <div class="flex items-center justify-between pb-3 border-b border-slate-900 px-1">
        <div class="flex items-center gap-2">
          <span class="text-xs font-black uppercase tracking-wider text-slate-200">{{ col.label }}</span>
        </div>
        <span class="px-2 py-0.5 rounded-full text-[10px] font-black font-mono" :class="col.color">
          {{ getTicketsByStatus(col.id).length }}
        </span>
      </div>

      <!-- Tickets List in Column -->
      <div class="space-y-3 flex-1 overflow-y-auto pr-1">
        <div
          v-if="getTicketsByStatus(col.id).length === 0"
          class="h-32 flex items-center justify-center text-[10px] font-bold uppercase tracking-widest text-slate-600 border border-dashed border-slate-900 rounded-2xl"
        >
          No Incidents
        </div>

        <div
          v-for="ticket in getTicketsByStatus(col.id)"
          :key="ticket.id"
          draggable="true"
          @dragstart="onDragStart($event, ticket.id)"
          @click="emit('selectTicket', ticket)"
          class="p-4 bg-slate-900 border border-slate-800 hover:border-indigo-500/50 rounded-2xl cursor-grab active:cursor-grabbing transition-all shadow-md group flex flex-col justify-between gap-3 select-none"
        >
          <div>
            <div class="flex items-center justify-between gap-2 mb-2">
              <span class="text-[10px] font-mono font-bold text-slate-400">{{ ticket.ticketNumber }}</span>
              <Badge class="text-[9px] uppercase font-mono px-2 py-0.5 border" :class="getPriorityClass(ticket.priority)">
                {{ ticket.priority }}
              </Badge>
            </div>

            <h5 class="text-xs font-bold text-slate-200 group-hover:text-white line-clamp-2">{{ ticket.title }}</h5>

            <p class="text-[10px] text-indigo-400 font-bold uppercase tracking-wider mt-1.5 truncate">
              {{ ticket.stationName || 'Plant Station' }}
            </p>
          </div>

          <!-- Bottom Meta & Quick Transition -->
          <div class="pt-2 border-t border-slate-800/60 flex items-center justify-between gap-2">
            <div class="flex items-center gap-1 text-[10px] text-slate-500 truncate">
              <User class="w-3 h-3 text-slate-400 shrink-0" />
              <span class="truncate">{{ ticket.assignedTechnicianName || 'Unassigned' }}</span>
            </div>

            <!-- Quick Transition Button -->
            <button
              v-if="col.nextStatus"
              @click="onQuickMove($event, ticket.id, col.nextStatus)"
              class="px-2 py-1 bg-slate-800 hover:bg-indigo-600 text-[9px] font-black uppercase tracking-wider text-slate-300 hover:text-white rounded-lg transition-colors flex items-center gap-1 shrink-0"
              :title="`Move to ${col.nextLabel}`"
            >
              <span>{{ col.nextLabel }}</span>
              <ArrowRight class="w-2.5 h-2.5" />
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
