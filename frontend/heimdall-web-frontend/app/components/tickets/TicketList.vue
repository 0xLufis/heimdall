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
      return { class: 'bg-destructive/10 text-destructive border-destructive/30', label: 'CRITICAL' }
    case 'High':
      return { class: 'bg-amber-500/10 text-amber-500 border-amber-500/30', label: 'HIGH' }
    case 'Medium':
      return { class: 'bg-primary/10 text-primary border-primary/30', label: 'MEDIUM' }
    case 'Low':
    default:
      return { class: 'bg-muted text-muted-foreground border-border', label: 'LOW' }
  }
}

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'Open':
      return { class: 'bg-blue-500/10 text-blue-400 border-blue-500/30', label: 'Open' }
    case 'In_Progress':
      return { class: 'bg-indigo-500/10 text-indigo-400 border-indigo-500/30', label: 'In Progress' }
    case 'Pending_Parts':
      return { class: 'bg-amber-500/10 text-amber-400 border-amber-500/30', label: 'Pending Parts' }
    case 'Resolved':
      return { class: 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30', label: 'Resolved' }
    case 'Closed':
      return { class: 'bg-slate-800 text-slate-500 border-slate-700', label: 'Closed' }
    default:
      return { class: 'bg-slate-800 text-slate-400 border-slate-700', label: status }
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
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div class="flex items-center gap-2 flex-1 max-w-lg">
        <div class="relative flex-1">
          <Search class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500" />
          <Input 
            v-model="searchQuery"
            placeholder="Search tickets, stations, technicians..."
            class="pl-10 bg-slate-900 border-slate-800 rounded-xl text-xs"
            @input="handleFilter"
          />
        </div>
      </div>

      <div class="flex items-center gap-2 overflow-x-auto whitespace-nowrap">
        <!-- Status Tabs -->
        <div class="flex p-1 bg-slate-900 rounded-xl border border-slate-800 gap-1">
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
            :class="activeStatusTab === st.id ? 'bg-indigo-600 text-white shadow-md' : 'text-slate-500 hover:text-slate-300'"
            class="rounded-lg text-[9px] font-black uppercase px-3"
          >
            {{ st.label }}
          </Button>
        </div>

        <!-- Priority Select -->
        <select
          v-model="activePriorityFilter"
          @change="handleFilter"
          class="bg-slate-900 border border-slate-800 text-slate-300 text-[10px] font-black uppercase rounded-xl px-3 h-8 focus:outline-none"
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
    <div class="rounded-2xl border border-slate-800 bg-slate-950 overflow-hidden shadow-2xl">
      <Table>
        <TableHeader class="bg-slate-900/60">
          <TableRow class="border-b border-slate-800 hover:bg-transparent">
            <TableHead class="text-[10px] font-black uppercase tracking-widest text-slate-500 py-4 px-6">Ticket / Station</TableHead>
            <TableHead class="text-[10px] font-black uppercase tracking-widest text-slate-500">Priority</TableHead>
            <TableHead class="text-[10px] font-black uppercase tracking-widest text-slate-500">Status</TableHead>
            <TableHead class="text-[10px] font-black uppercase tracking-widest text-slate-500">Assigned Tech</TableHead>
            <TableHead class="text-[10px] font-black uppercase tracking-widest text-slate-500">SLA Due</TableHead>
            <TableHead class="text-[10px] font-black uppercase tracking-widest text-slate-500 text-right pr-6">Action</TableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          <template v-if="tickets.length === 0">
            <TableRow>
              <TableCell colspan="6" class="h-32 text-center text-slate-500 font-bold uppercase tracking-widest text-xs">
                No maintenance tickets match the selected filters.
              </TableCell>
            </TableRow>
          </template>

          <template v-else>
            <TableRow 
              v-for="tkt in tickets" 
              :key="tkt.id"
              class="border-b border-slate-800 hover:bg-slate-900/40 transition-colors group cursor-pointer"
              @click="emit('selectTicket', tkt)"
            >
              <!-- Ticket & Station -->
              <TableCell class="py-4 px-6">
                <div class="flex items-start gap-3">
                  <div class="p-2.5 rounded-xl bg-slate-900 border border-slate-800 text-indigo-400 group-hover:border-indigo-500/40 transition-colors">
                    <Cpu class="h-4 w-4" />
                  </div>
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-mono font-black text-indigo-400">{{ tkt.ticketNumber }}</span>
                      <span class="text-[10px] font-bold text-slate-500 uppercase">[{{ tkt.stationName }}]</span>
                    </div>
                    <h5 class="text-sm font-black text-slate-100 uppercase tracking-tight mt-0.5 group-hover:text-indigo-200 transition-colors">
                      {{ tkt.title }}
                    </h5>
                    <p class="text-[10px] text-slate-400 line-clamp-1 max-w-md mt-0.5">
                      {{ tkt.description }}
                    </p>
                  </div>
                </div>
              </TableCell>

              <!-- Priority -->
              <TableCell>
                <Badge variant="outline" :class="getPriorityBadge(tkt.priority).class" class="text-[9px] font-black uppercase tracking-widest px-2.5 py-0.5">
                  {{ getPriorityBadge(tkt.priority).label }}
                </Badge>
              </TableCell>

              <!-- Status -->
              <TableCell>
                <Badge variant="outline" :class="getStatusBadge(tkt.status).class" class="text-[9px] font-black uppercase tracking-widest px-2.5 py-0.5">
                  {{ getStatusBadge(tkt.status).label }}
                </Badge>
              </TableCell>

              <!-- Technician -->
              <TableCell class="text-xs">
                <div class="flex items-center gap-2">
                  <div class="w-6 h-6 rounded-full bg-slate-900 border border-slate-800 flex items-center justify-center text-slate-400 text-[10px] font-mono font-bold">
                    {{ tkt.assignedTechnicianName ? tkt.assignedTechnicianName.charAt(0) : '?' }}
                  </div>
                  <span class="text-slate-300 font-bold uppercase text-[11px]">
                    {{ tkt.assignedTechnicianName || 'Unassigned' }}
                  </span>
                </div>
              </TableCell>

              <!-- SLA Due -->
              <TableCell>
                <div class="flex items-center gap-1.5 font-mono text-xs" :class="formatSlaDue(tkt.slaDueAt, tkt.status).overdue ? 'text-rose-400 font-bold' : 'text-slate-400'">
                  <Clock class="h-3.5 w-3.5" />
                  <span>{{ formatSlaDue(tkt.slaDueAt, tkt.status).text }}</span>
                </div>
              </TableCell>

              <!-- Actions -->
              <TableCell class="text-right pr-6">
                <Button 
                  variant="ghost" 
                  size="sm"
                  class="h-8 w-8 p-0 text-slate-400 hover:text-white hover:bg-slate-800 rounded-lg"
                  @click.stop="emit('selectTicket', tkt)"
                >
                  <ChevronRight class="h-4 w-4" />
                </Button>
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </div>
  </div>
</template>
