<script setup lang="ts">
import { ref, computed } from 'vue'
import type { MaintenanceTicket, TicketStatus } from '~/types/maintenance'
import { setDragImageAtClickPoint } from '~/utils/reorderList'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import {
  Clock, User, ArrowRight, CheckCircle, Wrench, AlertCircle,
  ArrowUpRight, ShieldAlert, CheckCircle2, XCircle, Tag, Layers,
  Package, FileCheck, FlaskConical, Database, Globe, Lock,
  ChevronDown, SlidersHorizontal, Filter, AlertTriangle, X
} from 'lucide-vue-next'
import {
  ANDON_STYLES,
  getAndonColorForStatus,
  getAndonPriorityStyle,
  getCanonicalColumn,
  getTicketPendingReason,
  PENDING_REASONS,
  CLOSURE_AUTHORITIES,
  type AndonColorType,
  type PendingReason,
  type ClosureAuthorityRole
} from '~/utils/andonColors'

const props = defineProps<{
  tickets: MaintenanceTicket[]
}>()

const emit = defineEmits<{
  (e: 'selectTicket', ticket: MaintenanceTicket): void
  (e: 'moveStatus', ticketId: string, status: TicketStatus): void
  (e: 'updateTicketPending', payload: {
    ticketId: string
    status: TicketStatus
    pendingReason: PendingReason
    pendingAuthority?: ClosureAuthorityRole
  }): void
}>()

// ── Canonical 6 Kanban Columns ───────────────────────────────────────────────
const columns: {
  id: 'Open' | 'In_Progress' | 'Pending' | 'Escalated' | 'Resolved' | 'Closed_Unresolved'
  statusId: TicketStatus
  label: string
  koreanLabel: string
  andonType: AndonColorType
  nextStatus?: TicketStatus
  nextLabel?: string
}[] = [
  {
    id: 'Open',
    statusId: 'Open',
    label: 'Open',
    koreanLabel: '호출 (CALL)',
    andonType: 'cyan',
    nextStatus: 'In_Progress',
    nextLabel: 'Start'
  },
  {
    id: 'In_Progress',
    statusId: 'In_Progress',
    label: 'In Progress',
    koreanLabel: '진행 (ACTIVE)',
    andonType: 'blue',
    nextStatus: 'Pending',
    nextLabel: 'Pending'
  },
  {
    id: 'Pending',
    statusId: 'Pending',
    label: 'Pending',
    koreanLabel: '경고 (CAUTION)',
    andonType: 'yellow',
    nextStatus: 'In_Progress',
    nextLabel: 'Resume'
  },
  {
    id: 'Escalated',
    statusId: 'Escalated',
    label: 'Escalated',
    koreanLabel: '정지 (ALARM)',
    andonType: 'red',
    nextStatus: 'In_Progress',
    nextLabel: 'Take Over'
  },
  {
    id: 'Resolved',
    statusId: 'Resolved',
    label: 'Resolved',
    koreanLabel: '정상 (NORMAL)',
    andonType: 'green',
    nextStatus: 'Closed_Unresolved',
    nextLabel: 'Close'
  },
  {
    id: 'Closed_Unresolved',
    statusId: 'Closed_Unresolved',
    label: 'Unresolved',
    koreanLabel: '종료 (CLOSED)',
    andonType: 'slate',
    nextStatus: 'Open',
    nextLabel: 'Re-Open'
  }
]

// ── State ────────────────────────────────────────────────────────────────────
const draggedTicketId = ref<string | null>(null)
const dragOverColumn = ref<string | null>(null)
const pendingSubFilter = ref<'All' | PendingReason>('All')

// Modal / popover for editing a ticket's pending reason & authority
const activePendingTicket = ref<MaintenanceTicket | null>(null)
const pendingDraftReason = ref<PendingReason>('Parts')
const pendingDraftAuthority = ref<ClosureAuthorityRole>('Group_Leader')

// ── Ticket Filtering per Column ──────────────────────────────────────────────
const getTicketsByCanonicalCol = (colId: 'Open' | 'In_Progress' | 'Pending' | 'Escalated' | 'Resolved' | 'Closed_Unresolved') => {
  return props.tickets.filter(t => {
    const col = getCanonicalColumn(t.status)
    if (col !== colId) return false

    // If viewing Pending column, respect the pending sub-type filter
    if (colId === 'Pending' && pendingSubFilter.value !== 'All') {
      const reason = getTicketPendingReason(t)
      return reason === pendingSubFilter.value
    }
    return true
  })
}

// Counts total tickets mapped to Pending for sub-filter badges
const getPendingCountByReason = (reason: PendingReason | 'All') => {
  const pendingTickets = props.tickets.filter(t => getCanonicalColumn(t.status) === 'Pending')
  if (reason === 'All') return pendingTickets.length
  return pendingTickets.filter(t => getTicketPendingReason(t) === reason).length
}

// ── Drag & Drop Handlers ─────────────────────────────────────────────────────
function onDragStart(event: DragEvent, ticketId: string) {
  setDragImageAtClickPoint(event)

  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', ticketId)
  }

  if (typeof requestAnimationFrame !== 'undefined') {
    requestAnimationFrame(() => {
      draggedTicketId.value = ticketId
    })
  } else {
    draggedTicketId.value = ticketId
  }
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

function onDrop(event: DragEvent, targetColId: 'Open' | 'In_Progress' | 'Pending' | 'Escalated' | 'Resolved' | 'Closed_Unresolved') {
  event.preventDefault()
  const ticketId = event.dataTransfer?.getData('text/plain') || draggedTicketId.value
  draggedTicketId.value = null
  dragOverColumn.value = null

  if (!ticketId) return

  const ticket = props.tickets.find(t => t.id === ticketId)
  if (!ticket) return

  if (targetColId === 'Pending') {
    // If dropping into Pending, open selector or assign default pending reason
    const reason = getTicketPendingReason(ticket)
    emit('moveStatus', ticketId, 'Pending')
    emit('updateTicketPending', {
      ticketId,
      status: 'Pending',
      pendingReason: reason,
      pendingAuthority: reason === 'Closure' ? ((ticket.pendingAuthority as ClosureAuthorityRole) || 'Group_Leader') : undefined
    })
  } else if (targetColId === 'Open') {
    emit('moveStatus', ticketId, 'Open')
  } else if (targetColId === 'In_Progress') {
    emit('moveStatus', ticketId, 'In_Progress')
  } else if (targetColId === 'Escalated') {
    emit('moveStatus', ticketId, 'Escalated')
  } else if (targetColId === 'Resolved') {
    emit('moveStatus', ticketId, 'Resolved')
  } else if (targetColId === 'Closed_Unresolved') {
    emit('moveStatus', ticketId, 'Closed_Unresolved')
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

// ── Interactive Pending Reason Selector ──────────────────────────────────────
function openPendingSelector(event: Event, ticket: MaintenanceTicket) {
  event.stopPropagation()
  activePendingTicket.value = ticket
  pendingDraftReason.value = getTicketPendingReason(ticket)
  pendingDraftAuthority.value = (ticket.pendingAuthority as ClosureAuthorityRole) || 'Group_Leader'
}

function applyPendingReason() {
  if (!activePendingTicket.value) return
  const t = activePendingTicket.value
  const newReason = pendingDraftReason.value
  const newAuth = newReason === 'Closure' ? pendingDraftAuthority.value : undefined

  t.pendingReason = newReason
  if (newAuth) t.pendingAuthority = newAuth
  t.status = 'Pending'

  emit('updateTicketPending', {
    ticketId: t.id,
    status: 'Pending',
    pendingReason: newReason,
    pendingAuthority: newAuth
  })

  emit('moveStatus', t.id, 'Pending')
  activePendingTicket.value = null
}

function closePendingSelector() {
  activePendingTicket.value = null
}

function getLatestTransition(ticket: MaintenanceTicket) {
  if (!ticket.comments || ticket.comments.length === 0) return null
  for (let i = ticket.comments.length - 1; i >= 0; i--) {
    const c = ticket.comments[i]
    if (c.transition) return c.transition
  }
  return null
}
</script>

<template>
  <div class="space-y-4">
    <!-- Horizontal scrolling container for 6 Canonical Samsung Andon Columns -->
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
        <!-- Column Header with Samsung Andon Status Lamp -->
        <div class="pb-3 border-b border-border px-1">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <!-- Andon Status Lamp Indicator -->
              <span class="size-2.5 rounded-full shrink-0" :class="ANDON_STYLES[col.andonType].dotClass" />
              <div>
                <span class="text-xs font-black uppercase tracking-wider text-foreground">{{ col.label }}</span>
                <span class="ml-1.5 text-[9px] font-mono text-muted-foreground uppercase hidden sm:inline-block">
                  {{ col.koreanLabel }}
                </span>
              </div>
            </div>

            <!-- Total Column Count -->
            <span
              class="px-2.5 py-0.5 rounded-full text-[10px] font-black font-mono border"
              :class="ANDON_STYLES[col.andonType].badgeClass"
            >
              {{ getTicketsByCanonicalCol(col.id).length }}
            </span>
          </div>

          <!-- Pending Column Sub-Category Filter Tabs -->
          <div v-if="col.id === 'Pending'" class="mt-2.5 pt-2 border-t border-border/60">
            <div class="flex items-center justify-between mb-1.5">
              <span class="text-[9px] font-bold text-muted-foreground uppercase tracking-widest flex items-center gap-1">
                <SlidersHorizontal class="w-2.5 h-2.5 text-amber-500" />
                Filter Reason
              </span>
              <span class="text-[9px] font-mono font-bold text-amber-700 dark:text-amber-300">
                {{ getPendingCountByReason('All') }} Total Pending
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
                v-for="r in PENDING_REASONS"
                :key="r.id"
                type="button"
                @click="pendingSubFilter = r.id"
                class="px-1.5 py-0.5 rounded-md text-[9px] font-semibold uppercase transition-colors flex items-center gap-1"
                :class="pendingSubFilter === r.id
                  ? 'bg-amber-500 text-amber-950 font-black shadow-xs'
                  : 'bg-muted/80 text-muted-foreground hover:text-foreground'"
              >
                <span>{{ r.shortLabel }}</span>
                <span class="text-[8px] opacity-75 font-mono">({{ getPendingCountByReason(r.id) }})</span>
              </button>
            </div>
          </div>
        </div>

        <!-- Tickets List in Column -->
        <div class="space-y-3 flex-1 overflow-y-auto pr-1">
          <!-- Empty State -->
          <div
            v-if="getTicketsByCanonicalCol(col.id).length === 0"
            class="h-32 flex flex-col items-center justify-center gap-1 text-[10px] font-bold uppercase tracking-widest text-muted-foreground border border-dashed border-border rounded-2xl bg-muted/20"
          >
            <span class="size-2 rounded-full opacity-40" :class="ANDON_STYLES[col.andonType].dotClass" />
            <span>No Incidents</span>
          </div>

          <!-- Samsung Andon Color-Coded Card -->
          <div
            v-for="ticket in getTicketsByCanonicalCol(col.id)"
            :key="ticket.id"
            draggable="true"
            @dragstart="onDragStart($event, ticket.id)"
            @dragend="draggedTicketId = null"
            @click="emit('selectTicket', ticket)"
            class="relative rounded-2xl border transition-all duration-200 cursor-grab active:cursor-grabbing shadow-xs hover:shadow-md group flex flex-col justify-between gap-3 select-none overflow-hidden"
            :class="[
              ANDON_STYLES[getAndonColorForStatus(ticket.status)].borderClass,
              ANDON_STYLES[getAndonColorForStatus(ticket.status)].cardBgClass,
              draggedTicketId === ticket.id ? 'opacity-40 border-dashed scale-[0.99]' : ''
            ]"
          >
            <!-- Top Samsung Andon Color Stripe -->
            <div
              class="h-1.5 w-full shrink-0"
              :class="ANDON_STYLES[getAndonColorForStatus(ticket.status)].stripeClass"
            />

            <div class="p-3.5 pt-1 space-y-2.5">
              <!-- Top Row: Ticket Number + Andon Status Lamp + Priority -->
              <div class="flex items-center justify-between gap-1.5 flex-wrap">
                <div class="flex items-center gap-1.5">
                  <!-- Andon Lamp Dot -->
                  <span
                    class="size-2 rounded-full shrink-0"
                    :class="ANDON_STYLES[getAndonColorForStatus(ticket.status)].dotClass"
                  />
                  <span class="text-[10px] font-mono font-bold text-muted-foreground">{{ ticket.ticketNumber }}</span>
                </div>

                <!-- Priority Badge with Samsung Andon styling -->
                <Badge
                  variant="outline"
                  class="text-[9px] uppercase font-mono px-2 py-0.2 rounded-md"
                  :class="getAndonPriorityStyle(ticket.priority).badgeClass"
                >
                  {{ getAndonPriorityStyle(ticket.priority).label }}
                </Badge>
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

              <!-- ── Pending Sub-Reason Indicator & Quick Change Selector ── -->
              <div
                v-if="getCanonicalColumn(ticket.status) === 'Pending'"
                class="pt-1.5 border-t border-amber-500/20"
              >
                <button
                  type="button"
                  @click.stop="openPendingSelector($event, ticket)"
                  class="w-full flex items-center justify-between gap-1 px-2 py-1 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 text-amber-800 dark:text-amber-300 text-[10px] font-bold transition-all text-left group/btn"
                  title="Click to change pending reason or closure authority"
                >
                  <div class="flex items-center gap-1.5 truncate">
                    <span class="size-1.5 rounded-full bg-amber-500 animate-ping shrink-0" />
                    <span class="uppercase tracking-wider">
                      Pending: {{ getTicketPendingReason(ticket) }}
                    </span>
                    <span
                      v-if="getTicketPendingReason(ticket) === 'Closure' && ticket.pendingAuthority"
                      class="text-[9px] opacity-80 font-mono"
                    >
                      ({{ ticket.pendingAuthority.replace(/_/g, ' ') }})
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

              <!-- Latest State Transition Badge -->
              <div
                v-if="getLatestTransition(ticket)"
                class="text-[9px] font-mono px-2 py-0.5 rounded bg-muted text-foreground border border-border flex items-center gap-1"
              >
                <span class="truncate">{{ getLatestTransition(ticket)!.fromStatus }}</span>
                <ArrowRight class="w-2.5 h-2.5 shrink-0 text-muted-foreground" />
                <span class="font-bold truncate text-primary">{{ getLatestTransition(ticket)!.toStatus }}</span>
              </div>

              <!-- External Escalation Badge -->
              <div
                v-if="ticket.externalEscalationTarget"
                class="text-[9px] font-mono px-2 py-0.5 rounded bg-rose-500/10 text-rose-700 dark:text-rose-300 border border-rose-500/20"
              >
                Target: {{ ticket.externalEscalationTarget }}
              </div>
            </div>

            <!-- Bottom Meta & Quick Transition -->
            <div class="p-3.5 pt-2 border-t border-border/80 flex items-center justify-between gap-2 bg-muted/10">
              <div class="flex items-center gap-1.5 text-[10px] text-muted-foreground truncate">
                <div class="size-4 rounded-full bg-muted flex items-center justify-center text-[8px] font-bold text-foreground shrink-0">
                  {{ ticket.assignedTechnicianName ? ticket.assignedTechnicianName.charAt(0) : '?' }}
                </div>
                <span class="truncate font-medium">{{ ticket.assignedTechnicianName || 'Unassigned' }}</span>
              </div>

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

    <!-- ── Interactive Modal: Select Pending Reason & Higher Authority Closure ── -->
    <div
      v-if="activePendingTicket"
      role="dialog"
      aria-modal="true"
      class="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm flex items-center justify-center p-4 animate-in fade-in duration-150"
      @click.self="closePendingSelector"
    >
      <div class="bg-card border border-border rounded-3xl shadow-2xl w-full max-w-md overflow-hidden text-foreground">
        <!-- Header -->
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

        <!-- Body -->
        <div class="p-5 space-y-4">
          <p class="text-xs text-muted-foreground leading-relaxed">
            Select why work is currently blocked or pending verification. This updates the factory floor Andon status board and notifies relevant support teams.
          </p>

          <!-- 6 Pending Reasons Grid -->
          <div class="grid grid-cols-2 gap-2">
            <button
              v-for="r in PENDING_REASONS"
              :key="r.id"
              type="button"
              @click="pendingDraftReason = r.id"
              class="p-2.5 rounded-xl border text-left flex items-start gap-2.5 transition-all cursor-pointer"
              :class="pendingDraftReason === r.id
                ? 'border-amber-500 bg-amber-500/15 ring-2 ring-amber-500/30 text-foreground font-bold shadow-xs'
                : 'border-border bg-muted/30 text-muted-foreground hover:bg-muted/70 hover:text-foreground'"
            >
              <!-- Icon -->
              <div class="p-1 rounded-md bg-background border border-border/80 text-foreground shrink-0 mt-0.5">
                <Package v-if="r.id === 'Parts'" class="size-3.5 text-amber-600 dark:text-amber-400" />
                <FileCheck v-else-if="r.id === 'Approval'" class="size-3.5 text-blue-600 dark:text-blue-400" />
                <FlaskConical v-else-if="r.id === 'Lab'" class="size-3.5 text-purple-600 dark:text-purple-400" />
                <Database v-else-if="r.id === 'SAP/Traceability'" class="size-3.5 text-teal-600 dark:text-teal-400" />
                <Globe v-else-if="r.id === 'External'" class="size-3.5 text-rose-600 dark:text-rose-400" />
                <Lock v-else-if="r.id === 'Closure'" class="size-3.5 text-cyan-600 dark:text-cyan-400" />
              </div>
              <div class="min-w-0">
                <div class="text-xs font-black leading-tight">{{ r.shortLabel }}</div>
                <div class="text-[9px] text-muted-foreground line-clamp-2 mt-0.5 leading-tight">
                  {{ r.description }}
                </div>
              </div>
            </button>
          </div>

          <!-- Higher Authority Selector (Conditional on Closure) -->
          <div
            v-if="pendingDraftReason === 'Closure'"
            class="p-3.5 rounded-2xl bg-cyan-500/10 border border-cyan-500/20 space-y-2 animate-in fade-in zoom-in-95 duration-150"
          >
            <div class="flex items-center gap-1.5 text-xs font-black text-cyan-800 dark:text-cyan-300">
              <ShieldAlert class="size-4 shrink-0 text-cyan-600 dark:text-cyan-400" />
              <span>Higher Authority Closure Sign-off Required</span>
            </div>
            <p class="text-[10px] text-muted-foreground">
              Select which role must authorize resolution and production sign-off before this incident can be permanently closed:
            </p>

            <div class="grid grid-cols-2 gap-1.5 pt-1">
              <button
                v-for="auth in CLOSURE_AUTHORITIES"
                :key="auth.id"
                type="button"
                @click="pendingDraftAuthority = auth.id"
                class="px-2.5 py-1.5 rounded-lg border text-xs font-bold transition-all text-center cursor-pointer"
                :class="pendingDraftAuthority === auth.id
                  ? 'border-cyan-500 bg-cyan-500/20 text-cyan-900 dark:text-cyan-100 ring-2 ring-cyan-500/40'
                  : 'border-border bg-background text-muted-foreground hover:text-foreground'"
              >
                {{ auth.label }}
              </button>
            </div>
          </div>
        </div>

        <!-- Footer -->
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
            Save &amp; Set Pending
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
