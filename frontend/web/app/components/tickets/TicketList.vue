<script setup lang="ts">
import { ref, computed } from 'vue'
import { 
  AlertOctagon, 
  Clock, 
  User, 
  Cpu, 
  ChevronRight, 
  Search, 
  Filter, 
  ArrowUpDown, 
  CheckCircle2, 
  AlertCircle,
  Package,
  FileCheck,
  FlaskConical,
  Database,
  Globe,
  Lock,
  Layers
} from 'lucide-vue-next'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import type { MaintenanceTicket } from '~/server/utils/ticketsStore'
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
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'selectTicket', ticket: MaintenanceTicket): void
  (e: 'updateStatus', payload: { ticketId: string; status: string }): void
  (e: 'filterChange', payload: { status: string; priority: string; query: string; sortBy: string }): void
}>()

const activeStatusTab = ref<string>('all')
const activePriorityFilter = ref<string>('all')
const searchQuery = ref<string>('')
const sortByField = ref<string>('created_at')
const pendingSubFilter = ref<'All' | PendingReason>('All')

const pendingIconMap: Record<string, any> = {
  Package,
  FileCheck,
  FlaskConical,
  Database,
  Globe,
  Lock
}

const statusTabs: { id: string; label: string; andonType?: AndonColorType }[] = [
  { id: 'all', label: 'All' },
  { id: 'Open', label: 'Open', andonType: 'cyan' },
  { id: 'In_Progress', label: 'In Progress', andonType: 'blue' },
  { id: 'Pending', label: 'Pending', andonType: 'yellow' },
  { id: 'Escalated', label: 'Escalated', andonType: 'red' },
  { id: 'Resolved', label: 'Resolved', andonType: 'green' },
  { id: 'Closed_Unresolved', label: 'Unresolved', andonType: 'slate' }
]

const getTabCount = (tabId: string) => {
  if (tabId === 'all') return props.tickets.length
  return props.tickets.filter(t => getCanonicalColumn(t.status) === tabId || t.status === tabId).length
}

const getPendingCount = (reason: 'All' | PendingReason) => {
  const pendingTickets = props.tickets.filter(t => getCanonicalColumn(t.status) === 'Pending')
  if (reason === 'All') return pendingTickets.length
  return pendingTickets.filter(t => getTicketPendingReason(t) === reason).length
}

const getPriorityBadge = (priority: string) => {
  const andon = getAndonPriorityStyle(priority)
  return {
    class: andon.badgeClass,
    dotClass: andon.dotClass,
    label: andon.label
  }
}

const getStatusBadge = (status: string) => {
  const col = getCanonicalColumn(status)
  const andonType = getAndonColorForStatus(status)
  const config = ANDON_STYLES[andonType]
  return {
    class: config.badgeClass,
    dotClass: config.dotClass,
    koreanLabel: config.koreanLabel,
    label: col === 'Closed_Unresolved' ? 'Unresolved' : col.replace(/_/g, ' ')
  }
}

const getRowAndonBorderClass = (status: string) => {
  const andonType = getAndonColorForStatus(status)
  return ANDON_STYLES[andonType].tableBorderClass
}

const getPendingReasonDetails = (tkt: MaintenanceTicket) => {
  const reason = getTicketPendingReason(tkt)
  const config = PENDING_REASONS.find(r => r.id === reason) || PENDING_REASONS[0]
  const authorityLabel = tkt.pendingAuthority 
    ? CLOSURE_AUTHORITIES.find(a => a.id === tkt.pendingAuthority)?.label || tkt.pendingAuthority
    : undefined
  return {
    reason,
    config,
    authority: authorityLabel
  }
}

const formatSlaDue = (slaDueAt: string, status: string) => {
  if (status === 'Closed' || status === 'Resolved') return { text: 'Completed', overdue: false }
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

type SortColumn = 'ticket' | 'priority' | 'status' | 'tech' | 'sla'
const activeSortColumn = ref<SortColumn | null>(null)
const activeSortDirection = ref<'asc' | 'desc' | null>(null)

const priorityWeights: Record<string, number> = {
  Critical: 4,
  High: 3,
  Medium: 2,
  Low: 1
}

const statusWeights: Record<string, number> = {
  Open: 1,
  In_Progress: 2,
  Pending: 3,
  Pending_Parts: 3,
  Closure_Pending: 3,
  Pending_Validation: 3,
  Waiting_On_Feedback: 3,
  Escalated: 4,
  Escalated_External: 4,
  Resolved: 5,
  Closed: 5,
  Closed_Unresolved: 6,
  Cancelled: 6,
  Archived: 6
}

const handleSort = (column: SortColumn, direction: 'asc' | 'desc' | null) => {
  if (direction === null) {
    activeSortColumn.value = null
    activeSortDirection.value = null
  } else {
    activeSortColumn.value = column
    activeSortDirection.value = direction
  }
}

const processedTickets = computed(() => {
  let list = [...props.tickets]

  // 1. Status Filter Tab (matches canonical column or literal status)
  if (activeStatusTab.value !== 'all') {
    list = list.filter(t => getCanonicalColumn(t.status) === activeStatusTab.value || t.status === activeStatusTab.value)
    
    // Sub-filter for Pending reasons
    if (activeStatusTab.value === 'Pending' && pendingSubFilter.value !== 'All') {
      list = list.filter(t => getTicketPendingReason(t) === pendingSubFilter.value)
    }
  }

  // 2. Priority Filter Select
  if (activePriorityFilter.value !== 'all') {
    list = list.filter(t => t.priority === activePriorityFilter.value)
  }

  // 3. Search Query Filter
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase().trim()
    list = list.filter(t =>
      t.ticketNumber?.toLowerCase().includes(q) ||
      t.stationName?.toLowerCase().includes(q) ||
      t.title?.toLowerCase().includes(q) ||
      t.description?.toLowerCase().includes(q) ||
      t.assignedTechnicianName?.toLowerCase().includes(q) ||
      (t.pendingReason && t.pendingReason.toLowerCase().includes(q))
    )
  }

  // 4. 3-State Sorting (Asc -> Desc -> Restore)
  if (!activeSortColumn.value || !activeSortDirection.value) {
    return list
  }

  const col = activeSortColumn.value
  const dir = activeSortDirection.value

  return list.sort((a, b) => {
    let cmp = 0
    switch (col) {
      case 'ticket': {
        const aVal = `${a.ticketNumber || ''} ${a.stationName || ''} ${a.title || ''}`.toLowerCase()
        const bVal = `${b.ticketNumber || ''} ${b.stationName || ''} ${b.title || ''}`.toLowerCase()
        cmp = aVal.localeCompare(bVal)
        break
      }
      case 'priority': {
        const aVal = priorityWeights[a.priority] ?? 0
        const bVal = priorityWeights[b.priority] ?? 0
        cmp = aVal - bVal
        break
      }
      case 'status': {
        const aVal = statusWeights[a.status] ?? 0
        const bVal = statusWeights[b.status] ?? 0
        cmp = aVal - bVal
        break
      }
      case 'tech': {
        const aVal = (a.assignedTechnicianName || '').toLowerCase()
        const bVal = (b.assignedTechnicianName || '').toLowerCase()
        if (!aVal && bVal) cmp = 1
        else if (aVal && !bVal) cmp = -1
        else cmp = aVal.localeCompare(bVal)
        break
      }
      case 'sla': {
        const aVal = a.slaDueAt ? new Date(a.slaDueAt).getTime() : Infinity
        const bVal = b.slaDueAt ? new Date(b.slaDueAt).getTime() : Infinity
        cmp = aVal - bVal
        break
      }
    }
    return dir === 'desc' ? -cmp : cmp
  })
})

function handleFilter() {
  emit('filterChange', {
    status: activeStatusTab.value,
    priority: activePriorityFilter.value,
    query: searchQuery.value,
    sortBy: sortByField.value
  })
}
</script>

<template>
  <div class="space-y-4">
    <!-- Toolbar & Filters -->
    <div class="flex flex-col gap-3">
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
        <div class="flex items-center gap-2 flex-1 max-w-sm">
          <div class="relative flex-1">
            <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
            <Input 
              v-model="searchQuery"
              placeholder="Search tickets, stations, technicians, pending reasons..."
              class="pl-9 pr-3 bg-background border-border rounded-lg text-xs h-8 text-foreground placeholder:text-muted-foreground"
              @input="handleFilter"
            />
          </div>
        </div>

        <div class="flex items-center gap-2 overflow-x-auto whitespace-nowrap">
          <!-- 6 Canonical Samsung Andon Status Tabs -->
          <div class="flex p-0.5 bg-muted rounded-lg border border-border gap-0.5">
            <Button
              v-for="st in statusTabs"
              :key="st.id"
              variant="ghost"
              size="sm"
              @click="activeStatusTab = st.id; handleFilter()"
              :class="activeStatusTab === st.id ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'"
              class="rounded-md text-xs font-medium px-2.5 h-7 transition-colors flex items-center gap-1.5"
            >
              <span
                v-if="st.andonType"
                class="size-1.5 rounded-full shrink-0"
                :class="ANDON_STYLES[st.andonType].dotClass"
              />
              <span>{{ st.label }}</span>
              <span class="text-[10px] opacity-75 font-mono">({{ getTabCount(st.id) }})</span>
            </Button>
          </div>

          <!-- Priority Select -->
          <select
            v-model="activePriorityFilter"
            @change="handleFilter"
            class="bg-background border border-border text-foreground text-xs font-medium rounded-lg px-2.5 h-8 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          >
            <option value="all">All Priorities</option>
            <option value="Critical">Critical</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>
        </div>
      </div>

      <!-- Pending Sub-Filter Ribbon (Visible when 'Pending' tab is active) -->
      <div 
        v-if="activeStatusTab === 'Pending'"
        class="flex items-center gap-1.5 p-1.5 bg-amber-500/10 border border-amber-500/20 rounded-lg overflow-x-auto animate-in fade-in duration-200"
      >
        <span class="text-[11px] font-bold text-amber-800 dark:text-amber-300 uppercase tracking-wider px-2 flex items-center gap-1">
          <Layers class="size-3 text-amber-600 dark:text-amber-400" />
          Pending Sub-Type:
        </span>
        <button
          type="button"
          @click="pendingSubFilter = 'All'"
          :class="[
            'px-2 py-0.5 rounded text-xs font-medium transition-all flex items-center gap-1',
            pendingSubFilter === 'All'
              ? 'bg-amber-500 text-white font-semibold shadow-xs'
              : 'text-amber-900/70 dark:text-amber-200/70 hover:bg-amber-500/20 hover:text-amber-950 dark:hover:text-amber-100'
          ]"
        >
          <span>All</span>
          <span class="text-[10px] font-mono">({{ getPendingCount('All') }})</span>
        </button>
        <button
          v-for="reason in PENDING_REASONS"
          :key="reason.id"
          type="button"
          @click="pendingSubFilter = reason.id"
          :class="[
            'px-2 py-0.5 rounded text-xs font-medium transition-all flex items-center gap-1',
            pendingSubFilter === reason.id
              ? 'bg-amber-500 text-white font-semibold shadow-xs'
              : 'text-amber-900/70 dark:text-amber-200/70 hover:bg-amber-500/20 hover:text-amber-950 dark:hover:text-amber-100'
          ]"
        >
          <component :is="pendingIconMap[reason.iconName]" class="size-3" />
          <span>{{ reason.shortLabel }}</span>
          <span class="text-[10px] font-mono">({{ getPendingCount(reason.id) }})</span>
        </button>
      </div>
    </div>

    <!-- Tickets Table -->
    <div class="rounded-xl border border-border bg-card overflow-hidden shadow-sm">
      <Table>
        <TableHeader class="bg-muted/50 border-b border-border">
          <TableRow class="border-b border-border hover:bg-transparent">
            <TableHead
              sortable
              :sort-direction="activeSortColumn === 'ticket' ? activeSortDirection : null"
              @sort="handleSort('ticket', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-4"
            >
              Ticket / Station
            </TableHead>
            <TableHead
              sortable
              :sort-direction="activeSortColumn === 'priority' ? activeSortDirection : null"
              @sort="handleSort('priority', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-4"
            >
              Priority
            </TableHead>
            <TableHead
              sortable
              :sort-direction="activeSortColumn === 'status' ? activeSortDirection : null"
              @sort="handleSort('status', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-4"
            >
              Status
            </TableHead>
            <TableHead
              sortable
              :sort-direction="activeSortColumn === 'tech' ? activeSortDirection : null"
              @sort="handleSort('tech', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-4"
            >
              Assigned Tech
            </TableHead>
            <TableHead
              sortable
              :sort-direction="activeSortColumn === 'sla' ? activeSortDirection : null"
              @sort="handleSort('sla', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-4"
            >
              SLA Due
            </TableHead>
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-muted-foreground text-right py-3 px-4">Action</TableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          <template v-if="processedTickets.length === 0">
            <TableRow>
              <TableCell colspan="6" class="h-32 text-center text-muted-foreground font-medium text-xs">
                No maintenance tickets match the selected filters.
              </TableCell>
            </TableRow>
          </template>

          <template v-else>
            <TableRow 
              v-for="tkt in processedTickets" 
              :key="tkt.id"
              class="border-b border-border hover:bg-muted/50 transition-colors group cursor-pointer"
              :class="getRowAndonBorderClass(tkt.status)"
              @click="emit('selectTicket', tkt)"
            >
              <!-- Ticket & Station -->
              <TableCell class="py-3 px-4">
                <div class="flex items-start gap-3">
                  <div class="p-2 rounded-lg bg-muted text-indigo-600 dark:text-indigo-400 group-hover:text-indigo-700 dark:group-hover:text-indigo-300 transition-colors">
                    <Cpu class="size-4" />
                  </div>
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-mono font-medium text-indigo-600 dark:text-indigo-400">{{ tkt.ticketNumber }}</span>
                      <span class="text-[11px] text-muted-foreground font-medium">[{{ tkt.stationName }}]</span>
                    </div>
                    <h5 class="text-sm font-semibold text-foreground group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors mt-0.5">
                      {{ tkt.title }}
                    </h5>
                    <p class="text-xs text-muted-foreground line-clamp-1 max-w-md mt-0.5">
                      {{ tkt.description }}
                    </p>
                  </div>
                </div>
              </TableCell>

              <!-- Priority -->
              <TableCell class="py-3 px-4">
                <Badge variant="outline" :class="getPriorityBadge(tkt.priority).class" class="text-xs font-medium px-2 py-0.5 rounded-md inline-flex items-center gap-1.5">
                  <span class="size-1.5 rounded-full shrink-0" :class="getPriorityBadge(tkt.priority).dotClass" />
                  <span>{{ getPriorityBadge(tkt.priority).label }}</span>
                </Badge>
              </TableCell>

              <!-- Status -->
              <TableCell class="py-3 px-4">
                <div class="flex flex-col gap-1 items-start">
                  <div
                    class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md border text-xs font-semibold"
                    :class="getStatusBadge(tkt.status).class"
                  >
                    <span class="size-2 rounded-full shrink-0" :class="getStatusBadge(tkt.status).dotClass" />
                    <span>{{ getStatusBadge(tkt.status).label }}</span>
                  </div>

                  <!-- Pending reason chip if Pending -->
                  <div 
                    v-if="getCanonicalColumn(tkt.status) === 'Pending'"
                    class="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-0.5 rounded border"
                    :class="getPendingReasonDetails(tkt).config.badgeColor"
                  >
                    <component :is="pendingIconMap[getPendingReasonDetails(tkt).config.iconName]" class="size-3 shrink-0" />
                    <span>{{ getPendingReasonDetails(tkt).reason }}</span>
                    <span v-if="getPendingReasonDetails(tkt).authority" class="text-[10px] opacity-80 font-normal">
                      • {{ getPendingReasonDetails(tkt).authority }}
                    </span>
                  </div>
                </div>
              </TableCell>

              <!-- Technician -->
              <TableCell class="py-3 px-4 text-xs">
                <div class="flex items-center gap-2">
                  <div class="w-6 h-6 rounded-full bg-muted text-foreground flex items-center justify-center text-xs font-semibold">
                    {{ tkt.assignedTechnicianName ? tkt.assignedTechnicianName.charAt(0) : '?' }}
                  </div>
                  <span class="text-foreground font-medium text-xs">
                    {{ tkt.assignedTechnicianName || 'Unassigned' }}
                  </span>
                </div>
              </TableCell>

              <!-- SLA Due -->
              <TableCell class="py-3 px-4">
                <div class="flex items-center gap-1.5 font-mono text-xs" :class="formatSlaDue(tkt.slaDueAt, tkt.status).overdue ? 'text-rose-600 dark:text-rose-400 font-medium' : 'text-muted-foreground'">
                  <Clock class="size-3.5" />
                  <span>{{ formatSlaDue(tkt.slaDueAt, tkt.status).text }}</span>
                </div>
              </TableCell>

              <!-- Actions -->
              <TableCell class="text-right py-3 px-4">
                <Button 
                  variant="ghost" 
                  size="sm" 
                  class="size-8 p-0 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg"
                  @click.stop="emit('selectTicket', tkt)"
                >
                  <ChevronRight class="size-4" />
                </Button>
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </div>
  </div>
</template>
