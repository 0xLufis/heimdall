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
  AlertCircle
} from 'lucide-vue-next'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import type { MaintenanceTicket } from '~/server/utils/ticketsStore'

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

const getPriorityBadge = (priority: string) => {
  switch (priority) {
    case 'Critical':
      return { class: 'bg-rose-500/10 text-rose-400 border border-rose-500/20', label: 'CRITICAL' }
    case 'High':
      return { class: 'bg-amber-500/10 text-amber-400 border border-amber-500/20', label: 'HIGH' }
    case 'Medium':
      return { class: 'bg-indigo-500/10 text-indigo-400 border border-indigo-500/20', label: 'MEDIUM' }
    case 'Low':
    default:
      return { class: 'bg-slate-800 text-slate-400 border border-slate-700', label: 'LOW' }
  }
}

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'Open':
      return { class: 'bg-blue-500/10 text-blue-400 border border-blue-500/20', label: 'Open' }
    case 'In_Progress':
      return { class: 'bg-indigo-500/10 text-indigo-400 border border-indigo-500/20', label: 'In Progress' }
    case 'Pending_Parts':
      return { class: 'bg-amber-500/10 text-amber-400 border border-amber-500/20', label: 'Pending Parts' }
    case 'Resolved':
      return { class: 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20', label: 'Resolved' }
    case 'Closed':
      return { class: 'bg-slate-800 text-slate-400 border border-slate-700', label: 'Closed' }
    default:
      return { class: 'bg-slate-800 text-slate-400 border border-slate-700', label: status }
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
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
      <div class="flex items-center gap-2 flex-1 max-w-sm">
        <div class="relative flex-1">
          <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-500" />
          <Input 
            v-model="searchQuery"
            placeholder="Search tickets, stations, technicians..."
            class="pl-9 pr-3 bg-slate-950 border-slate-800 rounded-lg text-xs h-8 text-slate-200"
            @input="handleFilter"
          />
        </div>
      </div>

      <div class="flex items-center gap-2 overflow-x-auto whitespace-nowrap">
        <!-- Status Tabs -->
        <div class="flex p-0.5 bg-slate-900 rounded-lg border border-slate-800 gap-0.5">
          <Button
            v-for="st in [
              { id: 'all', label: 'All' },
              { id: 'Open', label: 'Open' },
              { id: 'In_Progress', label: 'In Progress' },
              { id: 'Pending_Parts', label: 'Pending Parts' },
              { id: 'Resolved', label: 'Resolved' }
            ]"
            :key="st.id"
            variant="ghost"
            size="sm"
            @click="activeStatusTab = st.id; handleFilter()"
            :class="activeStatusTab === st.id ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="rounded-md text-xs font-medium px-2.5 h-7 transition-colors"
          >
            {{ st.label }}
          </Button>
        </div>

        <!-- Priority Select -->
        <select
          v-model="activePriorityFilter"
          @change="handleFilter"
          class="bg-slate-950 border border-slate-800 text-slate-300 text-xs font-medium rounded-lg px-2.5 h-8 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        >
          <option value="all">All Priorities</option>
          <option value="Critical">Critical</option>
          <option value="High">High</option>
          <option value="Medium">Medium</option>
          <option value="Low">Low</option>
        </select>
      </div>
    </div>

    <!-- Tickets Table -->
    <div class="rounded-xl border border-slate-800 bg-slate-900 overflow-hidden shadow-sm">
      <Table>
        <TableHeader class="bg-slate-950/60 border-b border-slate-800">
          <TableRow class="border-b border-slate-800 hover:bg-transparent">
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-slate-400 py-3 px-4">Ticket / Station</TableHead>
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-slate-400 py-3 px-4">Priority</TableHead>
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-slate-400 py-3 px-4">Status</TableHead>
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-slate-400 py-3 px-4">Assigned Tech</TableHead>
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-slate-400 py-3 px-4">SLA Due</TableHead>
            <TableHead class="text-xs font-semibold uppercase tracking-wider text-slate-400 text-right py-3 px-4">Action</TableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          <template v-if="tickets.length === 0">
            <TableRow>
              <TableCell colspan="6" class="h-32 text-center text-slate-500 font-medium text-xs">
                No maintenance tickets match the selected filters.
              </TableCell>
            </TableRow>
          </template>

          <template v-else>
            <TableRow 
              v-for="tkt in tickets" 
              :key="tkt.id"
              class="border-b border-slate-800/60 hover:bg-slate-800/40 transition-colors group cursor-pointer"
              @click="emit('selectTicket', tkt)"
            >
              <!-- Ticket & Station -->
              <TableCell class="py-3 px-4">
                <div class="flex items-start gap-3">
                  <div class="p-2 rounded-lg bg-slate-800 text-indigo-400 group-hover:text-indigo-300 transition-colors">
                    <Cpu class="size-4" />
                  </div>
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-mono font-medium text-indigo-400">{{ tkt.ticketNumber }}</span>
                      <span class="text-[11px] text-slate-500 font-medium">[{{ tkt.stationName }}]</span>
                    </div>
                    <h5 class="text-sm font-semibold text-slate-100 group-hover:text-indigo-300 transition-colors mt-0.5">
                      {{ tkt.title }}
                    </h5>
                    <p class="text-xs text-slate-400 line-clamp-1 max-w-md mt-0.5">
                      {{ tkt.description }}
                    </p>
                  </div>
                </div>
              </TableCell>

              <!-- Priority -->
              <TableCell class="py-3 px-4">
                <Badge variant="outline" :class="getPriorityBadge(tkt.priority).class" class="text-xs font-medium px-2 py-0.5 rounded-md">
                  {{ getPriorityBadge(tkt.priority).label }}
                </Badge>
              </TableCell>

              <!-- Status -->
              <TableCell class="py-3 px-4">
                <Badge variant="outline" :class="getStatusBadge(tkt.status).class" class="text-xs font-medium px-2 py-0.5 rounded-md">
                  {{ getStatusBadge(tkt.status).label }}
                </Badge>
              </TableCell>

              <!-- Technician -->
              <TableCell class="py-3 px-4 text-xs">
                <div class="flex items-center gap-2">
                  <div class="w-6 h-6 rounded-full bg-slate-800 text-slate-300 flex items-center justify-center text-xs font-semibold">
                    {{ tkt.assignedTechnicianName ? tkt.assignedTechnicianName.charAt(0) : '?' }}
                  </div>
                  <span class="text-slate-300 font-medium text-xs">
                    {{ tkt.assignedTechnicianName || 'Unassigned' }}
                  </span>
                </div>
              </TableCell>

              <!-- SLA Due -->
              <TableCell class="py-3 px-4">
                <div class="flex items-center gap-1.5 font-mono text-xs" :class="formatSlaDue(tkt.slaDueAt, tkt.status).overdue ? 'text-rose-400 font-medium' : 'text-slate-400'">
                  <Clock class="size-3.5" />
                  <span>{{ formatSlaDue(tkt.slaDueAt, tkt.status).text }}</span>
                </div>
              </TableCell>

              <!-- Actions -->
              <TableCell class="text-right py-3 px-4">
                <Button 
                  variant="ghost" 
                  size="sm"
                  class="size-8 p-0 text-slate-400 hover:text-white hover:bg-slate-800 rounded-lg"
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
