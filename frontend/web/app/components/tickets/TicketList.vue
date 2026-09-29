<script setup lang="ts">
import { ref, computed } from 'vue'
import { 
  Clock, 
  Cpu, 
  ChevronRight, 
  Search, 
  Filter, 
  ArrowUpDown, 
  AlertTriangle,
  Package,
  SlidersHorizontal,
  Flame,
  UserCheck,
  CheckCircle2,
  BookmarkCheck,
  Bot,
  User,
  CalendarClock
} from 'lucide-vue-next'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import type { MaintenanceTicket, TicketStatus, PendingReason } from '~/types/maintenance'
import {
  ANDON_STYLES,
  getAndonColorForStatus,
  getAndonPriorityStyle,
  PENDING_REASON_CONFIGS,
  type AndonColorType
} from '~/utils/andonColors'

const props = defineProps<{
  tickets: MaintenanceTicket[]
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'selectTicket', ticket: MaintenanceTicket): void
  (e: 'updateStatus', payload: { ticketId: string; status: string }): void
  (e: 'moveStatus', ticketId: string, status: TicketStatus): void
  (e: 'setPending', payload: { ticketId: string; reason: PendingReason; details?: string }): void
  (e: 'escalateTicket', ticketId: string, reason: string): void
  (e: 'resolveEscalation', ticketId: string): void
  (e: 'reserveTicket', ticketId: string): void
  (e: 'filterChange', payload: { status: string; priority: string; query: string; sortBy: string }): void
}>()

const activeStatusTab = ref<string>('all')
const activePriorityFilter = ref<string>('all')
const searchQuery = ref<string>('')
const pendingSubFilter = ref<'All' | PendingReason>('All')

// Table sorting state: null = natural lifecycle order, 'asc' = ascending, 'desc' = descending
type SortDirection = 'asc' | 'desc' | null
const activeSortColumn = ref<string | null>(null)
const activeSortDirection = ref<SortDirection>(null)

// ── Column Configuration ──────────────────────────────────────────────────
export interface ColumnConfig {
  key: string
  label: string
  required?: boolean
}

const ALL_COLUMNS: ColumnConfig[] = [
  { key: 'ticketId', label: 'Ticket ID', required: true },
  { key: 'machine', label: 'Machine Name & User ID', required: true },
  { key: 'controller', label: 'Agent Host (IPC)' },
  { key: 'prodCell', label: 'Prod Cell / Line' },
  { key: 'lineStop', label: 'Line Stop' },
  { key: 'department', label: 'Department' },
  { key: 'raisedAt', label: 'Raised At' },
  { key: 'startedAt', label: 'Started At' },
  { key: 'originator', label: 'Originator' },
  { key: 'worker', label: 'Currently Working' },
  { key: 'escalation', label: 'Escalation' },
  { key: 'priority', label: 'Priority' },
  { key: 'status', label: 'Status' },
  { key: 'sla', label: 'SLA Due' },
  { key: 'actions', label: 'Action', required: true }
]

const visibleColumns = ref<Set<string>>(new Set([
  'ticketId',
  'machine',
  'controller',
  'prodCell',
  'lineStop',
  'department',
  'raisedAt',
  'startedAt',
  'originator',
  'worker',
  'escalation',
  'priority',
  'status',
  'actions'
]))

const showColumnPicker = ref(false)

function toggleColumn(key: string) {
  if (visibleColumns.value.has(key)) {
    visibleColumns.value.delete(key)
  } else {
    visibleColumns.value.add(key)
  }
}

function isColVisible(key: string): boolean {
  return visibleColumns.value.has(key)
}

// ── Status Tabs ────────────────────────────────────────────────────────────
const statusTabs: { id: string; label: string; andonType?: AndonColorType }[] = [
  { id: 'all', label: 'All' },
  { id: 'Open', label: 'Open', andonType: 'cyan' },
  { id: 'InProgress', label: 'In Progress', andonType: 'blue' },
  { id: 'Pending', label: 'Pending', andonType: 'yellow' },
  { id: 'Resolved', label: 'Resolved', andonType: 'green' },
  { id: 'Closed', label: 'Closed', andonType: 'slate' }
]

const getTabCount = (tabId: string) => {
  if (tabId === 'all') return props.tickets.length
  return props.tickets.filter(t => t.status === tabId).length
}

const getPendingCount = (reason: 'All' | PendingReason) => {
  const pendingTickets = props.tickets.filter(t => t.status === 'Pending')
  if (reason === 'All') return pendingTickets.length
  return pendingTickets.filter(t => (t.pendingReason || 'None') === reason).length
}

const getPriorityBadge = (priority: string) => {
  const andon = getAndonPriorityStyle(priority)
  return {
    class: andon.badgeClass,
    dotClass: andon.dotClass,
    label: andon.label.toUpperCase()
  }
}

const getStatusBadge = (status: string) => {
  const andonType = getAndonColorForStatus(status)
  const config = ANDON_STYLES[andonType]
  return {
    class: config.badgeClass,
    dotClass: config.dotClass,
    label: config.label
  }
}

const getRowAndonBorderClass = (tkt: MaintenanceTicket) => {
  if (tkt.isLineStop) {
    return 'border-l-4 border-l-rose-600 bg-rose-500/[0.02]'
  }
  const andonType = getAndonColorForStatus(tkt.status)
  return ANDON_STYLES[andonType].tableBorderClass
}

const formatSlaDue = (slaDueAt?: string, status?: string) => {
  if (status === 'Closed' || status === 'Resolved') return { text: 'Completed', overdue: false }
  if (!slaDueAt) return { text: 'No SLA', overdue: false }
  const due = new Date(slaDueAt)
  const now = new Date()
  const diffMs = due.getTime() - now.getTime()

  if (diffMs < 0) {
    const mins = Math.abs(Math.floor(diffMs / 60000))
    const hours = Math.floor(mins / 60)
    return { text: `Overdue by ${hours > 0 ? `${hours}h ` : ''}${mins % 60}m`, overdue: true }
  } else {
    const mins = Math.floor(diffMs / 60000)
    const hours = Math.floor(mins / 60)
    return { text: `Due in ${hours > 0 ? `${hours}h ` : ''}${mins % 60}m`, overdue: false }
  }
}

const priorityWeight: Record<string, number> = {
  Critical: 4,
  High: 3,
  Medium: 2,
  Low: 1
}

const lifecycleRank: Record<string, number> = {
  Open: 1,
  InProgress: 2,
  Pending: 3,
  Resolved: 4,
  Closed: 5
}

const handleSort = (column: string, direction: SortDirection) => {
  activeSortColumn.value = direction ? column : null
  activeSortDirection.value = direction
}

const processedTickets = computed(() => {
  let list = [...props.tickets]

  if (activeStatusTab.value !== 'all') {
    list = list.filter(t => t.status === activeStatusTab.value)
    if (activeStatusTab.value === 'Pending' && pendingSubFilter.value !== 'All') {
      list = list.filter(t => (t.pendingReason || 'None') === pendingSubFilter.value)
    }
  }

  if (activePriorityFilter.value !== 'all') {
    list = list.filter(t => t.priority === activePriorityFilter.value)
  }

  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase().trim()
    list = list.filter(t => 
      t.ticketNumber.toLowerCase().includes(q) ||
      t.title.toLowerCase().includes(q) ||
      (t.stationName && t.stationName.toLowerCase().includes(q)) ||
      (t.controllerName && t.controllerName.toLowerCase().includes(q)) ||
      (t.assignedTechnicianName && t.assignedTechnicianName.toLowerCase().includes(q)) ||
      (t.responsibleDepartment && t.responsibleDepartment.toLowerCase().includes(q)) ||
      (t.tags && t.tags.some(tag => tag.toLowerCase().includes(q)))
    )
  }

  // Sorting: explicit column sort OR default lifecycle sorting
  if (activeSortColumn.value && activeSortDirection.value) {
    const col = activeSortColumn.value
    const dir = activeSortDirection.value === 'asc' ? 1 : -1

    list.sort((a, b) => {
      let valA: any = ''
      let valB: any = ''

      if (col === 'title') {
        valA = a.title || ''
        valB = b.title || ''
        return dir * valA.localeCompare(valB)
      } else if (col === 'priority') {
        valA = priorityWeight[a.priority] || 0
        valB = priorityWeight[b.priority] || 0
        return dir * (valA - valB)
      } else if (col === 'status') {
        valA = a.status || ''
        valB = b.status || ''
        return dir * valA.localeCompare(valB)
      } else if (col === 'sla') {
        valA = a.slaDueAt ? new Date(a.slaDueAt).getTime() : 0
        valB = b.slaDueAt ? new Date(b.slaDueAt).getTime() : 0
        return dir * (valA - valB)
      } else if (col === 'raisedAt') {
        valA = new Date(a.createdAt).getTime()
        valB = new Date(b.createdAt).getTime()
        return dir * (valA - valB)
      }
      return 0
    })
  } else {
    // Default sorting follows ticket lifecycle (Open -> InProgress -> Pending -> Resolved -> Closed),
    // and tickets equal in primary sorting are secondarily sorted by time opened (newest first).
    list.sort((a, b) => {
      const rankA = lifecycleRank[a.status] || 99
      const rankB = lifecycleRank[b.status] || 99
      if (rankA !== rankB) {
        return rankA - rankB
      }
      const timeA = new Date(a.createdAt).getTime()
      const timeB = new Date(b.createdAt).getTime()
      return timeB - timeA
    })
  }

  return list
})
</script>

<template>
  <div class="space-y-4">
    <!-- Filter & Toolbar Bar -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-card border border-border p-4 rounded-2xl shadow-xs">
      <div class="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0">
        <button
          v-for="tab in statusTabs"
          :key="tab.id"
          type="button"
          @click="activeStatusTab = tab.id"
          :class="[
            'px-3 py-1.5 rounded-xl text-xs font-semibold whitespace-nowrap transition-all flex items-center gap-2 cursor-pointer',
            activeStatusTab === tab.id
              ? 'bg-primary text-primary-foreground shadow-xs'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground'
          ]"
        >
          <span
            v-if="tab.andonType"
            class="size-2 rounded-full shrink-0"
            :class="ANDON_STYLES[tab.andonType].dotClass"
          />
          <span>{{ tab.label }}</span>
          <span 
            class="text-[10px] px-1.5 py-0.2 rounded-full font-mono"
            :class="activeStatusTab === tab.id ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'"
          >
            {{ getTabCount(tab.id) }}
          </span>
        </button>
      </div>

      <div class="flex items-center gap-2.5">
        <div class="relative w-full md:w-56">
          <Search class="absolute left-3 top-2.5 size-4 text-muted-foreground" />
          <Input 
            v-model="searchQuery" 
            placeholder="Search tickets, hosts, tags..." 
            class="pl-9 h-9 text-xs bg-muted/30 border-border rounded-xl"
          />
        </div>

        <select
          v-model="activePriorityFilter"
          class="h-9 px-3 text-xs bg-muted/30 border border-border rounded-xl text-foreground focus:outline-hidden"
        >
          <option value="all">All Priorities</option>
          <option value="Critical">Critical</option>
          <option value="High">High</option>
          <option value="Medium">Medium</option>
          <option value="Low">Low</option>
        </select>

        <!-- Column Customizer Toggle -->
        <div class="relative">
          <Button
            variant="outline"
            size="sm"
            @click="showColumnPicker = !showColumnPicker"
            class="h-9 px-2.5 text-xs font-medium border-border bg-card flex items-center gap-1.5"
            title="Configure visible table columns"
          >
            <SlidersHorizontal class="size-3.5 text-indigo-600 dark:text-indigo-400" />
            <span class="hidden sm:inline">Columns</span>
          </Button>

          <!-- Column Picker Dropdown -->
          <div
            v-if="showColumnPicker"
            class="absolute right-0 top-11 z-30 w-56 p-2 bg-card border border-border rounded-xl shadow-xl space-y-1 text-xs"
          >
            <div class="px-2 py-1 font-bold text-[10px] uppercase text-muted-foreground border-b border-border mb-1">
              Toggle Columns
            </div>
            <div
              v-for="col in ALL_COLUMNS"
              :key="col.key"
              class="flex items-center justify-between px-2 py-1 rounded hover:bg-muted/60 cursor-pointer"
              @click="toggleColumn(col.key)"
            >
              <span :class="isColVisible(col.key) ? 'font-semibold text-foreground' : 'text-muted-foreground'">
                {{ col.label }}
              </span>
              <input
                type="checkbox"
                :checked="isColVisible(col.key)"
                class="rounded border-border text-primary size-3.5"
                @click.stop="toggleColumn(col.key)"
              />
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Pending Reason Sub-Filter (when Pending is selected) -->
    <div 
      v-if="activeStatusTab === 'Pending'" 
      class="flex flex-wrap items-center gap-1.5 p-3 rounded-2xl bg-amber-500/10 border border-amber-500/20 text-xs"
    >
      <span class="text-[10px] font-black uppercase tracking-wider text-amber-800 dark:text-amber-300 mr-2 flex items-center gap-1">
        <Filter class="size-3 text-amber-600" />
        Filter Pending Reason:
      </span>
      <button
        type="button"
        @click="pendingSubFilter = 'All'"
        class="px-2 py-1 rounded-lg text-xs font-semibold transition-colors cursor-pointer"
        :class="pendingSubFilter === 'All'
          ? 'bg-amber-500 text-amber-950 font-bold shadow-xs'
          : 'bg-background/80 text-foreground hover:bg-muted'"
      >
        All ({{ getPendingCount('All') }})
      </button>
      <button
        v-for="(cfg, rKey) in PENDING_REASON_CONFIGS"
        :key="rKey"
        type="button"
        @click="pendingSubFilter = rKey"
        class="px-2 py-1 rounded-lg text-xs font-semibold transition-colors flex items-center gap-1.5 cursor-pointer"
        :class="pendingSubFilter === rKey
          ? 'bg-amber-500 text-amber-950 font-bold shadow-xs'
          : 'bg-background/80 text-foreground hover:bg-muted'"
      >
        <span>{{ cfg.label }}</span>
        <span class="text-[10px] opacity-75 font-mono">({{ getPendingCount(rKey) }})</span>
      </button>
    </div>

    <!-- Tickets Table -->
    <div class="border border-border rounded-2xl overflow-hidden bg-card shadow-xs">
      <Table>
        <TableHeader class="bg-muted/40 border-b border-border">
          <TableRow>
            <TableHead v-if="isColVisible('ticketId')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Ticket ID
            </TableHead>
            <TableHead
              v-if="isColVisible('machine')"
              sortable
              :sort-direction="activeSortColumn === 'title' ? activeSortDirection : null"
              @sort="handleSort('title', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3"
            >
              Machine &amp; Operator
            </TableHead>
            <TableHead v-if="isColVisible('controller')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Agent Host (IPC)
            </TableHead>
            <TableHead v-if="isColVisible('prodCell')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Prod Cell / Line
            </TableHead>
            <TableHead v-if="isColVisible('lineStop')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Line Stop
            </TableHead>
            <TableHead v-if="isColVisible('department')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Department
            </TableHead>
            <TableHead
              v-if="isColVisible('raisedAt')"
              sortable
              :sort-direction="activeSortColumn === 'raisedAt' ? activeSortDirection : null"
              @sort="handleSort('raisedAt', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3"
            >
              Raised At
            </TableHead>
            <TableHead v-if="isColVisible('startedAt')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Started At
            </TableHead>
            <TableHead v-if="isColVisible('originator')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Originator
            </TableHead>
            <TableHead v-if="isColVisible('worker')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Assigned / Working
            </TableHead>
            <TableHead v-if="isColVisible('escalation')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3">
              Escalation
            </TableHead>
            <TableHead
              v-if="isColVisible('priority')"
              sortable
              :sort-direction="activeSortColumn === 'priority' ? activeSortDirection : null"
              @sort="handleSort('priority', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3"
            >
              Priority
            </TableHead>
            <TableHead
              v-if="isColVisible('status')"
              sortable
              :sort-direction="activeSortColumn === 'status' ? activeSortDirection : null"
              @sort="handleSort('status', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3"
            >
              Status &amp; Reason
            </TableHead>
            <TableHead
              v-if="isColVisible('sla')"
              sortable
              :sort-direction="activeSortColumn === 'sla' ? activeSortDirection : null"
              @sort="handleSort('sla', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-3"
            >
              SLA Due
            </TableHead>
            <TableHead v-if="isColVisible('actions')" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground text-right py-3 px-3">
              Action
            </TableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          <template v-if="processedTickets.length === 0">
            <TableRow>
              <TableCell :colspan="visibleColumns.size" class="h-32 text-center text-muted-foreground font-medium text-xs">
                No maintenance tickets match the selected filters.
              </TableCell>
            </TableRow>
          </template>

          <template v-else>
            <TableRow 
              v-for="tkt in processedTickets" 
              :key="tkt.id"
              class="border-b border-border hover:bg-muted/50 transition-colors group cursor-pointer"
              :class="getRowAndonBorderClass(tkt)"
              @click="emit('selectTicket', tkt)"
            >
              <!-- Ticket ID -->
              <TableCell v-if="isColVisible('ticketId')" class="py-3 px-3">
                <span class="text-xs font-mono font-bold text-indigo-600 dark:text-indigo-400">
                  {{ tkt.ticketNumber }}
                </span>
              </TableCell>

              <!-- Machine & Operator -->
              <TableCell v-if="isColVisible('machine')" class="py-3 px-3">
                <div class="flex items-start gap-2.5">
                  <div class="p-1.5 rounded-lg bg-muted text-indigo-600 dark:text-indigo-400 shrink-0">
                    <Cpu class="size-3.5" />
                  </div>
                  <div>
                    <div class="flex items-center gap-1.5">
                      <span class="text-xs font-bold text-foreground">{{ tkt.stationName || tkt.stationId || 'Station' }}</span>
                      <span v-if="tkt.externalOperatorName || tkt.externalOperatorId" class="text-[10px] font-mono text-muted-foreground">
                        ({{ tkt.externalOperatorName || tkt.externalOperatorId }})
                      </span>
                    </div>
                    <div class="text-xs text-foreground font-medium line-clamp-1 group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors">
                      {{ tkt.title }}
                    </div>
                  </div>
                </div>
              </TableCell>

              <!-- Agent Host (IPC) -->
              <TableCell v-if="isColVisible('controller')" class="py-3 px-3 text-xs font-mono text-muted-foreground">
                {{ tkt.controllerName || tkt.controllerId || 'Direct' }}
              </TableCell>

              <!-- Prod Cell / Line -->
              <TableCell v-if="isColVisible('prodCell')" class="py-3 px-3 text-xs text-foreground font-medium">
                {{ tkt.groupId || tkt.stationName || 'Line 01' }}
              </TableCell>

              <!-- Line Stop -->
              <TableCell v-if="isColVisible('lineStop')" class="py-3 px-3">
                <Badge
                  v-if="tkt.isLineStop"
                  variant="outline"
                  class="bg-rose-600 text-white text-[9px] font-black uppercase tracking-wider border-transparent flex items-center gap-1 animate-pulse"
                >
                  <Flame class="size-2.5" />
                  <span>STOP {{ tkt.lineStopDurationMinutes ? `(${tkt.lineStopDurationMinutes}m)` : '' }}</span>
                </Badge>
                <span v-else class="text-[11px] text-muted-foreground">No</span>
              </TableCell>

              <!-- Responsible Department -->
              <TableCell v-if="isColVisible('department')" class="py-3 px-3">
                <span v-if="tkt.responsibleDepartment" class="text-[11px] font-semibold text-indigo-600 dark:text-indigo-400 bg-indigo-500/10 px-2 py-0.5 rounded-md">
                  {{ tkt.responsibleDepartment }}
                </span>
                <span v-else class="text-[11px] text-muted-foreground">-</span>
              </TableCell>

              <!-- Raised At -->
              <TableCell v-if="isColVisible('raisedAt')" class="py-3 px-3 text-xs font-mono text-muted-foreground whitespace-nowrap">
                {{ new Date(tkt.createdAt).toLocaleDateString([], { month: 'numeric', day: 'numeric' }) }}
                {{ new Date(tkt.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
              </TableCell>

              <!-- Started At -->
              <TableCell v-if="isColVisible('startedAt')" class="py-3 px-3 text-xs font-mono text-muted-foreground whitespace-nowrap">
                <span v-if="tkt.startedAt || tkt.qrScannedAt" class="text-blue-600 dark:text-blue-400 font-medium">
                  {{ new Date(tkt.startedAt || tkt.qrScannedAt!).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
                  <span v-if="tkt.reactionTimeMinutes !== undefined" class="text-[10px] text-muted-foreground ml-1">
                    ({{ tkt.reactionTimeMinutes }}m RT)
                  </span>
                </span>
                <span v-else class="text-muted-foreground">-</span>
              </TableCell>

              <!-- Originator -->
              <TableCell v-if="isColVisible('originator')" class="py-3 px-3">
                <div class="flex items-center gap-1 text-[11px] font-medium">
                  <Bot v-if="tkt.originatorType === 'MachineAutomatic'" class="size-3 text-cyan-600" />
                  <CalendarClock v-else-if="tkt.originatorType === 'ScheduledMaintenance'" class="size-3 text-emerald-600" />
                  <User v-else class="size-3 text-indigo-600" />
                  <span class="truncate max-w-[90px]">
                    {{ tkt.originatorType || 'ManualUser' }}
                  </span>
                </div>
              </TableCell>

              <!-- Working Tech / Reserved -->
              <TableCell v-if="isColVisible('worker')" class="py-3 px-3 text-xs">
                <div class="flex items-center gap-1.5">
                  <div class="w-5 h-5 rounded-full bg-muted text-foreground flex items-center justify-center text-[10px] font-bold shrink-0">
                    {{ (tkt.assignedTechnicianName || tkt.reservedBy || '?').charAt(0) }}
                  </div>
                  <div class="truncate max-w-[100px]">
                    <span class="text-foreground font-medium text-xs block truncate">
                      {{ tkt.assignedTechnicianName || tkt.reservedBy || 'Unassigned' }}
                    </span>
                    <span v-if="tkt.reservedBy && !tkt.assignedTechnicianName" class="text-[9px] text-amber-600 dark:text-amber-400 font-bold block">
                      (Reserved)
                    </span>
                  </div>
                </div>
              </TableCell>

              <!-- Escalation -->
              <TableCell v-if="isColVisible('escalation')" class="py-3 px-3">
                <Badge
                  v-if="tkt.isEscalated"
                  variant="outline"
                  class="text-[9px] bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30 uppercase font-black px-1.5 py-0 flex items-center gap-1 animate-pulse"
                >
                  <AlertTriangle class="size-2.5" />
                  <span>Escalated {{ tkt.escalationHandoverState ? `[${tkt.escalationHandoverState}]` : '' }}</span>
                </Badge>
                <span v-else class="text-[11px] text-muted-foreground">-</span>
              </TableCell>

              <!-- Priority -->
              <TableCell v-if="isColVisible('priority')" class="py-3 px-3">
                <Badge variant="outline" :class="getPriorityBadge(tkt.priority).class" class="text-[10px] font-medium px-2 py-0.5 rounded-md inline-flex items-center gap-1.5">
                  <span class="size-1.5 rounded-full shrink-0" :class="getPriorityBadge(tkt.priority).dotClass" />
                  <span>{{ getPriorityBadge(tkt.priority).label }}</span>
                </Badge>
              </TableCell>

              <!-- Status -->
              <TableCell v-if="isColVisible('status')" class="py-3 px-3">
                <div class="flex flex-col gap-1 items-start">
                  <div
                    class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md border text-[11px] font-semibold"
                    :class="getStatusBadge(tkt.status).class"
                  >
                    <span class="size-1.5 rounded-full shrink-0" :class="getStatusBadge(tkt.status).dotClass" />
                    <span>{{ getStatusBadge(tkt.status).label }}</span>
                  </div>

                  <!-- Pending reason chip if Pending -->
                  <div 
                    v-if="tkt.status === 'Pending'"
                    class="inline-flex items-center gap-1 text-[10px] font-medium px-1.5 py-0.2 rounded border"
                    :class="PENDING_REASON_CONFIGS[tkt.pendingReason || 'None']?.badgeClass"
                  >
                    <Package class="size-2.5 shrink-0" />
                    <span>{{ PENDING_REASON_CONFIGS[tkt.pendingReason || 'None']?.label || 'None' }}</span>
                  </div>
                </div>
              </TableCell>

              <!-- SLA Due -->
              <TableCell v-if="isColVisible('sla')" class="py-3 px-3">
                <div class="flex items-center gap-1 font-mono text-[11px]" :class="formatSlaDue(tkt.slaDueAt, tkt.status).overdue ? 'text-rose-600 dark:text-rose-400 font-medium' : 'text-muted-foreground'">
                  <Clock class="size-3" />
                  <span>{{ formatSlaDue(tkt.slaDueAt, tkt.status).text }}</span>
                </div>
              </TableCell>

              <!-- Actions -->
              <TableCell v-if="isColVisible('actions')" class="text-right py-3 px-3">
                <div class="flex items-center justify-end gap-1.5" @click.stop>
                  <!-- Reservation Button for Technicians on Open Tickets -->
                  <Button
                    v-if="tkt.status === 'Open' && !tkt.reservedBy"
                    size="sm"
                    variant="outline"
                    class="h-7 px-2 text-[10px] font-bold text-amber-700 dark:text-amber-300 border-amber-500/30 hover:bg-amber-500/10 cursor-pointer"
                    @click="emit('reserveTicket', tkt.id)"
                    title="Reserve ticket for yourself"
                  >
                    <BookmarkCheck class="size-3 mr-0.5" />
                    Reserve
                  </Button>

                  <Button
                    v-if="tkt.status === 'Open'"
                    size="sm"
                    variant="outline"
                    class="h-7 px-2 text-[10px] font-black uppercase tracking-wider text-indigo-600 dark:text-indigo-400 border-indigo-500/30 hover:bg-indigo-500/10 cursor-pointer"
                    @click="emit('moveStatus', tkt.id, 'InProgress')"
                    title="Start work"
                  >
                    Start
                  </Button>
                  <Button
                    v-else-if="tkt.status === 'InProgress'"
                    size="sm"
                    variant="outline"
                    class="h-7 px-2 text-[10px] font-black uppercase tracking-wider text-emerald-600 dark:text-emerald-400 border-emerald-500/30 hover:bg-emerald-500/10 cursor-pointer"
                    @click="emit('moveStatus', tkt.id, 'Resolved')"
                    title="Mark Resolved"
                  >
                    Resolve
                  </Button>
                  <Button
                    v-else-if="tkt.status === 'Pending'"
                    size="sm"
                    variant="outline"
                    class="h-7 px-2 text-[10px] font-black uppercase tracking-wider text-blue-600 dark:text-blue-400 border-blue-500/30 hover:bg-blue-500/10 cursor-pointer"
                    @click="emit('moveStatus', tkt.id, 'InProgress')"
                    title="Resume work"
                  >
                    Resume
                  </Button>
                  <Button
                    v-else-if="tkt.status === 'Resolved'"
                    size="sm"
                    variant="outline"
                    class="h-7 px-2 text-[10px] font-black uppercase tracking-wider text-muted-foreground border-border hover:bg-muted cursor-pointer"
                    @click="emit('moveStatus', tkt.id, 'Closed')"
                    title="Close ticket"
                  >
                    Close
                  </Button>
                  <Button
                    v-else-if="tkt.status === 'Closed'"
                    size="sm"
                    variant="outline"
                    class="h-7 px-2 text-[10px] font-black uppercase tracking-wider text-cyan-600 dark:text-cyan-400 border-cyan-500/30 hover:bg-cyan-500/10 cursor-pointer"
                    @click="emit('moveStatus', tkt.id, 'Open')"
                    title="Re-Open ticket"
                  >
                    Re-Open
                  </Button>

                  <Button 
                    variant="ghost" 
                    size="sm" 
                    class="size-7 p-0 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg cursor-pointer"
                    @click="emit('selectTicket', tkt)"
                    title="View ticket details"
                  >
                    <ChevronRight class="size-4" />
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </div>
  </div>
</template>
