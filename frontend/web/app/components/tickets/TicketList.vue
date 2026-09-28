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
  Package
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
  (e: 'filterChange', payload: { status: string; priority: string; query: string; sortBy: string }): void
}>()

const activeStatusTab = ref<string>('all')
const activePriorityFilter = ref<string>('all')
const searchQuery = ref<string>('')
const sortByField = ref<string>('created_at')
const pendingSubFilter = ref<'All' | PendingReason>('All')

// Table sorting state: null = natural order, 'asc' = ascending, 'desc' = descending
type SortDirection = 'asc' | 'desc' | null
const activeSortColumn = ref<string | null>(null)
const activeSortDirection = ref<SortDirection>(null)

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

const getRowAndonBorderClass = (status: string) => {
  const andonType = getAndonColorForStatus(status)
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
      (t.assignedTechnicianName && t.assignedTechnicianName.toLowerCase().includes(q)) ||
      (t.tags && t.tags.some(tag => tag.toLowerCase().includes(q)))
    )
  }

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
      }
      return 0
    })
  }

  return list
})
</script>

<template>
  <div class="space-y-4">
    <!-- Filter Bar -->
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
        <div class="relative w-full md:w-64">
          <Search class="absolute left-3 top-2.5 size-4 text-muted-foreground" />
          <Input 
            v-model="searchQuery" 
            placeholder="Search tickets, stations, tags..." 
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
            <TableHead
              sortable
              :sort-direction="activeSortColumn === 'title' ? activeSortDirection : null"
              @sort="handleSort('title', $event)"
              class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-4"
            >
              Ticket &amp; Station
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
              Status &amp; Reason
            </TableHead>
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-muted-foreground py-3 px-4">
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
                      <span v-if="tkt.stationName" class="text-[11px] text-muted-foreground font-medium">[{{ tkt.stationName }}]</span>
                      <!-- Escalation Badge -->
                      <Badge
                        v-if="tkt.isEscalated"
                        variant="outline"
                        class="text-[9px] bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30 uppercase font-black px-1.5 py-0 flex items-center gap-1 animate-pulse"
                      >
                        <AlertTriangle class="size-2.5" />
                        Escalated
                      </Badge>
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
                    v-if="tkt.status === 'Pending'"
                    class="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-0.5 rounded border"
                    :class="PENDING_REASON_CONFIGS[tkt.pendingReason || 'None']?.badgeClass"
                  >
                    <Package class="size-3 shrink-0" />
                    <span>{{ PENDING_REASON_CONFIGS[tkt.pendingReason || 'None']?.label || 'None' }}</span>
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
