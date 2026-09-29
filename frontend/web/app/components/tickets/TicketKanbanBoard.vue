<script setup lang="ts">
import { ref } from 'vue'
import type { MaintenanceTicket, TicketStatus, PendingReason } from '~/types/maintenance'
import { setDragImageAtClickPoint } from '~/utils/reorderList'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import {
  Clock, ArrowRight, Layers,
  ChevronDown, SlidersHorizontal, AlertTriangle, X,
  Package, CheckCircle2, ShieldAlert
} from 'lucide-vue-next'
import {
  ANDON_STYLES,
  getAndonColorForStatus,
  getAndonPriorityStyle,
  PENDING_REASON_CONFIGS,
  type AndonColorType
} from '~/utils/andonColors'

const props = defineProps<{
  tickets: MaintenanceTicket[]
}>()

const emit = defineEmits<{
  (e: 'selectTicket', ticket: MaintenanceTicket): void
  (e: 'moveStatus', ticketId: string, status: TicketStatus): void
  (e: 'setPending', payload: { ticketId: string; reason: PendingReason; details?: string }): void
  (e: 'escalateTicket', ticketId: string, reason: string): void
  (e: 'resolveEscalation', ticketId: string): void
}>()

// ── Strictly 5 Canonical Columns ─────────────────────────────────────────────
const columns: {
  id: TicketStatus
  label: string
  andonType: AndonColorType
  nextStatus?: TicketStatus
  nextLabel?: string
}[] = [
  {
    id: 'Open',
    label: 'Open',
    andonType: 'cyan',
    nextStatus: 'InProgress',
    nextLabel: 'Start'
  },
  {
    id: 'InProgress',
    label: 'In Progress',
    andonType: 'blue',
    nextStatus: 'Resolved',
    nextLabel: 'Resolve'
  },
  {
    id: 'Pending',
    label: 'Pending',
    andonType: 'yellow',
    nextStatus: 'InProgress',
    nextLabel: 'Resume'
  },
  {
    id: 'Resolved',
    label: 'Resolved',
    andonType: 'green',
    nextStatus: 'Closed',
    nextLabel: 'Close'
  },
  {
    id: 'Closed',
    label: 'Closed',
    andonType: 'slate',
    nextStatus: 'Open',
    nextLabel: 'Re-Open'
  }
]

// ── State ────────────────────────────────────────────────────────────────────
const draggedTicketId = ref<string | null>(null)
const dragOverColumn = ref<string | null>(null)
const pendingSubFilter = ref<'All' | PendingReason>('All')

// Modal state for editing PendingReason
const activePendingTicket = ref<MaintenanceTicket | null>(null)
const pendingDraftReason = ref<PendingReason>('Parts')
const pendingDraftDetails = ref<string>('')

// Modal state for Quick Escalation
const activeEscalateTicket = ref<MaintenanceTicket | null>(null)
const escalateDraftReason = ref<string>('')

// ── Filtering ────────────────────────────────────────────────────────────────
const getTicketsByColumn = (colId: TicketStatus) => {
  return props.tickets.filter(t => {
    if (t.status !== colId) return false
    if (colId === 'Pending' && pendingSubFilter.value !== 'All') {
      return (t.pendingReason || 'None') === pendingSubFilter.value
    }
    return true
  })
}

const getPendingCountByReason = (reason: PendingReason | 'All') => {
  const pendingTickets = props.tickets.filter(t => t.status === 'Pending')
  if (reason === 'All') return pendingTickets.length
  return pendingTickets.filter(t => (t.pendingReason || 'None') === reason).length
}

// ── Drag & Drop ─────────────────────────────────────────────────────────────
function onDragStart(event: DragEvent, ticketId: string) {
  setDragImageAtClickPoint(event)
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', ticketId)
  }
  draggedTicketId.value = ticketId
}

function onDragOver(event: DragEvent, colId: string) {
  event.preventDefault()
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
  dragOverColumn.value = colId
}

function onDragLeave(colId: string) {
  if (dragOverColumn.value === colId) {
    dragOverColumn.value = null
  }
}

function onDrop(event: DragEvent, targetColId: TicketStatus) {
  event.preventDefault()
  const ticketId = event.dataTransfer?.getData('text/plain') || draggedTicketId.value
  draggedTicketId.value = null
  dragOverColumn.value = null

  if (!ticketId) return
  const ticket = props.tickets.find(t => t.id === ticketId)
  if (!ticket) return

  if (targetColId === 'Pending') {
    openPendingSelector(event, ticket)
  } else {
    emit('moveStatus', ticketId, targetColId)
  }
}

function onQuickMove(event: Event, ticketId: string, nextStatus: TicketStatus) {
  event.stopPropagation()
  if (nextStatus === 'Pending') {
    const ticket = props.tickets.find(t => t.id === ticketId)
    if (ticket) {
      openPendingSelector(event, ticket)
      return
    }
  }
  emit('moveStatus', ticketId, nextStatus)
}

// ── Pending Modal Handlers ───────────────────────────────────────────────────
function openPendingSelector(event: Event, ticket: MaintenanceTicket) {
  event.stopPropagation()
  activePendingTicket.value = ticket
  pendingDraftReason.value = ticket.pendingReason || 'Parts'
  pendingDraftDetails.value = ticket.pendingDetails || ''
}

function applyPendingReason() {
  if (!activePendingTicket.value) return
  const t = activePendingTicket.value
  const newReason = pendingDraftReason.value
  const newDetails = pendingDraftDetails.value

  t.status = 'Pending'
  t.pendingReason = newReason
  t.pendingDetails = newDetails

  emit('setPending', {
    ticketId: t.id,
    reason: newReason,
    details: newDetails
  })
  emit('moveStatus', t.id, 'Pending')
  activePendingTicket.value = null
}

function closePendingSelector() {
  activePendingTicket.value = null
}

// ── Quick Escalation Handlers ────────────────────────────────────────────────
function openEscalateModal(event: Event, ticket: MaintenanceTicket) {
  event.stopPropagation()
  activeEscalateTicket.value = ticket
  escalateDraftReason.value = ''
}

function applyEscalation() {
  if (!activeEscalateTicket.value || !escalateDraftReason.value.trim()) return
  const t = activeEscalateTicket.value
  emit('escalateTicket', t.id, escalateDraftReason.value.trim())
  t.isEscalated = true
  t.escalationReason = escalateDraftReason.value.trim()
  activeEscalateTicket.value = null
}

function handleResolveEscalation(event: Event, ticket: MaintenanceTicket) {
  event.stopPropagation()
  emit('resolveEscalation', ticket.id)
  ticket.isEscalated = false
  ticket.escalationReason = null
}
</script>

<template>
  <div class="space-y-4">
    <!-- Horizontal scrolling container for 5 Canonical Columns -->
    <div class="flex gap-4 overflow-x-auto pb-4 items-start min-h-[600px] custom-scrollbar">
      <div
        v-for="col in columns"
        :key="col.id"
        @dragover="onDragOver($event, col.id)"
        @dragleave="onDragLeave(col.id)"
        @drop="onDrop($event, col.id)"
        class="w-[330px] shrink-0 bg-card border rounded-3xl p-3.5 flex flex-col gap-3 min-h-[540px] transition-all duration-200 shadow-sm"
        :class="[
          dragOverColumn === col.id
            ? `${ANDON_STYLES[col.andonType].borderClass} ring-2 ring-primary/20 shadow-lg scale-[1.01]`
            : 'border-border'
        ]"
      >
        <!-- Column Header -->
        <div class="pb-3 border-b border-border px-1">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="size-2.5 rounded-full shrink-0" :class="ANDON_STYLES[col.andonType].dotClass" />
              <span class="text-xs font-black uppercase tracking-wider text-foreground">{{ col.label }}</span>
            </div>

            <!-- Total Column Count -->
            <span
              class="px-2.5 py-0.5 rounded-full text-[10px] font-black font-mono border"
              :class="ANDON_STYLES[col.andonType].badgeClass"
            >
              {{ getTicketsByColumn(col.id).length }}
            </span>
          </div>

          <!-- Pending Reason Sub-Filter Tabs -->
          <div v-if="col.id === 'Pending'" class="mt-2.5 pt-2 border-t border-border/60">
            <div class="flex items-center justify-between mb-1.5">
              <span class="text-[9px] font-bold text-muted-foreground uppercase tracking-widest flex items-center gap-1">
                <SlidersHorizontal class="w-2.5 h-2.5 text-amber-500" />
                Filter Reason
              </span>
              <span class="text-[9px] font-mono font-bold text-amber-700 dark:text-amber-300">
                {{ getPendingCountByReason('All') }} Total
              </span>
            </div>
            
            <div class="flex flex-wrap gap-1">
              <button
                type="button"
                @click="pendingSubFilter = 'All'"
                class="px-1.5 py-0.5 rounded-md text-[9px] font-bold uppercase transition-colors"
                :class="pendingSubFilter === 'All'
                  ? 'bg-amber-500 text-amber-950 font-black shadow-xs'
                  : 'bg-muted/80 text-muted-foreground hover:text-foreground'"
              >
                All
              </button>
              <button
                v-for="(cfg, rKey) in PENDING_REASON_CONFIGS"
                :key="rKey"
                type="button"
                @click="pendingSubFilter = rKey"
                class="px-1.5 py-0.5 rounded-md text-[9px] font-semibold uppercase transition-colors flex items-center gap-1"
                :class="pendingSubFilter === rKey
                  ? 'bg-amber-500 text-amber-950 font-black shadow-xs'
                  : 'bg-muted/80 text-muted-foreground hover:text-foreground'"
              >
                <span>{{ cfg.label }}</span>
                <span class="text-[8px] opacity-75 font-mono">({{ getPendingCountByReason(rKey) }})</span>
              </button>
            </div>
          </div>
        </div>

        <!-- Tickets List in Column -->
        <div class="space-y-3 flex-1 overflow-y-auto pr-1">
          <!-- Empty State -->
          <div
            v-if="getTicketsByColumn(col.id).length === 0"
            class="h-32 flex flex-col items-center justify-center gap-1 text-[10px] font-bold uppercase tracking-widest text-muted-foreground border border-dashed border-border rounded-2xl bg-muted/20"
          >
            <span class="size-2 rounded-full opacity-40" :class="ANDON_STYLES[col.andonType].dotClass" />
            <span>No Tickets</span>
          </div>

          <!-- Card -->
          <div
            v-for="ticket in getTicketsByColumn(col.id)"
            :key="ticket.id"
            draggable="true"
            @dragstart="onDragStart($event, ticket.id)"
            @dragend="draggedTicketId = null"
            @click="emit('selectTicket', ticket)"
            class="relative rounded-2xl border transition-all duration-200 cursor-grab active:cursor-grabbing shadow-xs hover:shadow-md group flex flex-col justify-between gap-3 select-none overflow-hidden"
            :class="[
              ticket.isEscalated ? 'border-rose-500/80 ring-2 ring-rose-500/40 bg-rose-500/[0.03]' : ANDON_STYLES[getAndonColorForStatus(ticket.status)].borderClass,
              ANDON_STYLES[getAndonColorForStatus(ticket.status)].cardBgClass,
              draggedTicketId === ticket.id ? 'opacity-40 border-dashed scale-[0.99]' : ''
            ]"
          >
            <!-- Top Color Stripe -->
            <div
              class="h-1.5 w-full shrink-0"
              :class="ticket.isEscalated ? 'bg-gradient-to-r from-rose-600 via-rose-500 to-rose-600 animate-pulse' : ANDON_STYLES[getAndonColorForStatus(ticket.status)].stripeClass"
            />

            <div class="p-3.5 pt-1 space-y-2.5">
              <!-- Top Row: Number + Status Lamp + Priority + Escalated Pill -->
              <div class="flex items-center justify-between gap-1.5 flex-wrap">
                <div class="flex items-center gap-1.5">
                  <span
                    class="size-2 rounded-full shrink-0"
                    :class="ticket.isEscalated ? 'bg-rose-500 animate-pulse' : ANDON_STYLES[getAndonColorForStatus(ticket.status)].dotClass"
                  />
                  <span class="text-[10px] font-mono font-bold text-muted-foreground">{{ ticket.ticketNumber }}</span>
                </div>

                <div class="flex items-center gap-1">
                  <!-- Priority Badge -->
                  <Badge
                    variant="outline"
                    class="text-[9px] uppercase font-mono px-2 py-0.2 rounded-md"
                    :class="getAndonPriorityStyle(ticket.priority).badgeClass"
                  >
                    {{ getAndonPriorityStyle(ticket.priority).label }}
                  </Badge>
                </div>
              </div>

              <!-- Escalation Banner if Active -->
              <div
                v-if="ticket.isEscalated"
                class="px-2 py-1 rounded-lg bg-rose-500/15 border border-rose-500/30 text-rose-700 dark:text-rose-300 text-[10px] font-bold flex items-center justify-between gap-1 animate-pulse"
              >
                <div class="flex items-center gap-1 truncate">
                  <AlertTriangle class="size-3 shrink-0 text-rose-500" />
                  <span class="truncate font-black uppercase tracking-wider">Escalated: {{ ticket.escalationReason || 'Flagged' }}</span>
                </div>
                <button
                  type="button"
                  @click.stop="handleResolveEscalation($event, ticket)"
                  class="text-[9px] underline hover:text-white shrink-0 font-medium cursor-pointer"
                  title="Resolve this escalation"
                >
                  Clear
                </button>
              </div>

              <!-- Ticket Title -->
              <h5 class="text-xs font-bold text-foreground group-hover:text-foreground/90 line-clamp-2 leading-snug">
                {{ ticket.title }}
              </h5>

              <!-- Station & Machine Type -->
              <div class="flex items-center gap-1.5 flex-wrap">
                <p class="text-[10px] text-indigo-600 dark:text-indigo-400 font-bold uppercase tracking-wider truncate">
                  {{ ticket.stationName || 'Plant Station' }}
                </p>
                <span v-if="ticket.machineType" class="text-[9px] font-mono px-1 rounded bg-muted text-muted-foreground border border-border">
                  {{ ticket.machineType }}
                </span>
              </div>

              <!-- SFC Workpiece Serial Badge -->
              <div v-if="ticket.sfc" class="flex items-center gap-1 text-[9px] font-mono text-cyan-600 dark:text-cyan-400">
                <Layers class="w-2.5 h-2.5 shrink-0" />
                <span class="truncate">{{ ticket.sfc }}</span>
              </div>

              <!-- Pending Reason Pill -->
              <div
                v-if="ticket.status === 'Pending'"
                class="pt-1.5 border-t border-amber-500/20"
              >
                <button
                  type="button"
                  @click.stop="openPendingSelector($event, ticket)"
                  class="w-full flex items-center justify-between gap-1 px-2 py-1 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 text-amber-800 dark:text-amber-300 text-[10px] font-bold transition-all text-left group/btn"
                  title="Click to modify pending reason"
                >
                  <div class="flex items-center gap-1.5 truncate">
                    <span class="size-1.5 rounded-full bg-amber-500 shrink-0" />
                    <span class="uppercase tracking-wider">
                      Pending: {{ PENDING_REASON_CONFIGS[ticket.pendingReason || 'None']?.label || 'None' }}
                    </span>
                  </div>
                  <ChevronDown class="w-3 h-3 text-amber-600 group-hover/btn:translate-y-0.5 transition-transform shrink-0" />
                </button>
              </div>

              <!-- Tags list -->
              <div v-if="ticket.tags && ticket.tags.length > 0" class="flex flex-wrap gap-1">
                <span
                  v-for="tag in ticket.tags.slice(0, 3)"
                  :key="tag"
                  class="text-[9px] font-mono px-1.5 py-0.5 rounded bg-muted text-muted-foreground border border-border"
                >
                  {{ tag }}
                </span>
                <span v-if="ticket.tags.length > 3" class="text-[8px] text-muted-foreground self-center">
                  +{{ ticket.tags.length - 3 }}
                </span>
              </div>
            </div>

            <!-- Bottom Meta & Actions -->
            <div class="p-3.5 pt-2 border-t border-border/80 flex items-center justify-between gap-2 bg-muted/10">
              <div class="flex items-center gap-1.5 text-[10px] text-muted-foreground truncate">
                <div class="size-4 rounded-full bg-muted flex items-center justify-center text-[8px] font-bold text-foreground shrink-0">
                  {{ ticket.assignedTechnicianName ? ticket.assignedTechnicianName.charAt(0) : '?' }}
                </div>
                <span class="truncate font-medium">{{ ticket.assignedTechnicianName || 'Unassigned' }}</span>
              </div>

              <div class="flex items-center gap-1">
                <!-- Escalate Action (if not already escalated) -->
                <button
                  v-if="!ticket.isEscalated"
                  @click="openEscalateModal($event, ticket)"
                  class="px-2 py-1 bg-rose-500/10 hover:bg-rose-500/20 text-rose-700 dark:text-rose-400 text-[9px] font-bold uppercase rounded-lg border border-rose-500/30 transition-colors cursor-pointer"
                  title="Escalate ticket"
                >
                  Escalate
                </button>

                <!-- InProgress: Set Pending Action -->
                <button
                  v-if="col.id === 'InProgress'"
                  @click.stop="openPendingSelector($event, ticket)"
                  class="px-2 py-1 bg-amber-500/10 hover:bg-amber-500/20 text-amber-800 dark:text-amber-400 text-[9px] font-bold uppercase rounded-lg border border-amber-500/30 transition-colors cursor-pointer"
                  title="Set ticket to Pending"
                >
                  Pending
                </button>

                <!-- Pending: Direct Resolve Action -->
                <button
                  v-if="col.id === 'Pending'"
                  @click.stop="onQuickMove($event, ticket.id, 'Resolved')"
                  class="px-2 py-1 bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-800 dark:text-emerald-400 text-[9px] font-bold uppercase rounded-lg border border-emerald-500/30 transition-colors cursor-pointer"
                  title="Resolve ticket directly"
                >
                  Resolve
                </button>

                <!-- Quick Transition Button -->
                <button
                  v-if="col.nextStatus"
                  @click="onQuickMove($event, ticket.id, col.nextStatus)"
                  class="px-2 py-1 bg-muted hover:bg-primary text-[9px] font-black uppercase tracking-wider text-foreground hover:text-primary-foreground rounded-lg transition-colors flex items-center gap-1 shrink-0 cursor-pointer shadow-2xs"
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
    </div>

    <!-- ── Interactive Modal: Select Pending Reason ── -->
    <div
      v-if="activePendingTicket"
      role="dialog"
      aria-modal="true"
      class="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm flex items-center justify-center p-4 animate-in fade-in duration-150"
      @click.self="closePendingSelector"
    >
      <div class="bg-card border border-border rounded-3xl shadow-2xl w-full max-w-md overflow-hidden text-foreground">
        <div class="p-5 border-b border-border bg-amber-500/10 flex items-center justify-between">
          <div class="flex items-center gap-2.5">
            <div class="p-2 rounded-xl bg-amber-500/20 text-amber-800 dark:text-amber-300">
              <Clock class="size-5" />
            </div>
            <div>
              <h4 class="text-sm font-black uppercase tracking-wider text-foreground">
                Set Pending Reason
              </h4>
              <p class="text-[10px] font-mono text-muted-foreground mt-0.5">
                {{ activePendingTicket.ticketNumber }} &mdash; {{ activePendingTicket.stationName }}
              </p>
            </div>
          </div>
          <Button
            variant="ghost"
            size="icon"
            class="h-8 w-8 text-muted-foreground hover:text-foreground rounded-lg cursor-pointer"
            @click="closePendingSelector"
          >
            <X class="size-4" />
          </Button>
        </div>

        <div class="p-5 space-y-4">
          <p class="text-xs text-muted-foreground leading-relaxed">
            Select the industrial condition holding this ticket in the Pending status:
          </p>

          <!-- 4 Canonical Pending Reasons -->
          <div class="grid grid-cols-2 gap-2">
            <button
              v-for="(cfg, rKey) in PENDING_REASON_CONFIGS"
              :key="rKey"
              type="button"
              @click="pendingDraftReason = rKey"
              class="p-2.5 rounded-xl border text-left flex items-start gap-2.5 transition-all cursor-pointer"
              :class="pendingDraftReason === rKey
                ? 'border-amber-500 bg-amber-500/15 ring-2 ring-amber-500/30 text-foreground font-bold shadow-xs'
                : 'border-border bg-muted/30 text-muted-foreground hover:bg-muted/70 hover:text-foreground'"
            >
              <div class="p-1 rounded-md bg-background border border-border/80 text-foreground shrink-0 mt-0.5">
                <Package class="size-3.5 text-amber-600 dark:text-amber-400" />
              </div>
              <div class="min-w-0">
                <div class="text-xs font-black leading-tight">{{ cfg.label }}</div>
                <div class="text-[9px] text-muted-foreground line-clamp-2 mt-0.5 leading-tight">
                  {{ cfg.description }}
                </div>
              </div>
            </button>
          </div>

          <!-- Pending Details Input -->
          <div class="space-y-1.5">
            <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
              Additional Details (optional)
            </label>
            <input
              v-model="pendingDraftDetails"
              type="text"
              placeholder="e.g., PO #49281 ordered from vendor, ETA 2 days"
              class="w-full px-3 py-2 bg-background border border-border rounded-xl text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden focus:ring-2 focus:ring-amber-500/50"
            />
          </div>
        </div>

        <div class="p-4 border-t border-border bg-muted/20 flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="sm"
            class="text-xs border-border cursor-pointer"
            @click="closePendingSelector"
          >
            Cancel
          </Button>
          <Button
            size="sm"
            class="bg-amber-600 hover:bg-amber-500 text-white font-black text-xs px-4 cursor-pointer shadow-xs"
            @click="applyPendingReason"
          >
            Apply Pending Reason
          </Button>
        </div>
      </div>
    </div>

    <!-- ── Interactive Modal: Escalate Ticket ── -->
    <div
      v-if="activeEscalateTicket"
      role="dialog"
      aria-modal="true"
      class="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm flex items-center justify-center p-4 animate-in fade-in duration-150"
      @click.self="activeEscalateTicket = null"
    >
      <div class="bg-card border border-rose-500/30 rounded-3xl shadow-2xl w-full max-w-md overflow-hidden text-foreground">
        <div class="p-5 border-b border-border bg-rose-500/10 flex items-center justify-between">
          <div class="flex items-center gap-2.5">
            <div class="p-2 rounded-xl bg-rose-500/20 text-rose-700 dark:text-rose-300">
              <ShieldAlert class="size-5" />
            </div>
            <div>
              <h4 class="text-sm font-black uppercase tracking-wider text-foreground">
                Escalate Ticket
              </h4>
              <p class="text-[10px] font-mono text-muted-foreground mt-0.5">
                {{ activeEscalateTicket.ticketNumber }} &mdash; {{ activeEscalateTicket.stationName }}
              </p>
            </div>
          </div>
          <Button
            variant="ghost"
            size="icon"
            class="h-8 w-8 text-muted-foreground hover:text-foreground rounded-lg cursor-pointer"
            @click="activeEscalateTicket = null"
          >
            <X class="size-4" />
          </Button>
        </div>

        <div class="p-5 space-y-4">
          <p class="text-xs text-muted-foreground leading-relaxed">
            Escalation flags this incident for priority management and engineering oversight. The ticket remains in its current status (<strong>{{ activeEscalateTicket.status }}</strong>) and can be de-escalated independently.
          </p>

          <div class="space-y-1.5">
            <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
              Escalation Reason *
            </label>
            <textarea
              v-model="escalateDraftReason"
              rows="3"
              placeholder="e.g. Critical machine downtime impacting Line 2 production. OEM support specialist required."
              class="w-full px-3 py-2 bg-background border border-border rounded-xl text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden focus:ring-2 focus:ring-rose-500/50 resize-none"
            />
          </div>
        </div>

        <div class="p-4 border-t border-border bg-muted/20 flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="sm"
            class="text-xs border-border cursor-pointer"
            @click="activeEscalateTicket = null"
          >
            Cancel
          </Button>
          <Button
            size="sm"
            :disabled="!escalateDraftReason.trim()"
            class="bg-rose-600 hover:bg-rose-500 text-white font-black text-xs px-4 cursor-pointer shadow-xs disabled:opacity-50"
            @click="applyEscalation"
          >
            Confirm Escalation
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  height: 8px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(15, 23, 42, 0.6);
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(51, 65, 85, 0.8);
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: rgba(130, 143, 159, 0.6);
}
</style>
