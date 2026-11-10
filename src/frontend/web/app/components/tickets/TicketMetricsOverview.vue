<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import {
  AlertTriangle,
  Clock,
  CheckCircle2,
  Wrench,
  Package,
  ShieldCheck,
  Flame,
  Activity,
  QrCode,
  Laptop,
  ChevronDown,
  ChevronUp,
  Building2,
  Cpu
} from 'lucide-vue-next'
import { Card, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import type { TicketMetrics, StoppageStats } from '~/types/maintenance'

const props = withDefaults(
  defineProps<{
    metrics?: TicketMetrics
    activeFilter?: string | null
    stoppageStats?: StoppageStats | null
    allowRemoteStart?: boolean
  }>(),
  {
    activeFilter: null,
    stoppageStats: null,
    allowRemoteStart: true
  }
)

const emit = defineEmits<{
  (e: 'filter-change', filter: string | null): void
  (e: 'update:allowRemoteStart', val: boolean): void
}>()

const defaultStats: StoppageStats = {
  totalStoppageMinutesThisWeek: 0,
  totalLineStopIncidents: 0,
  departmentBreakdown: [],
  topWorstMachines: []
}

const localStoppageStats = ref<StoppageStats>(props.stoppageStats || defaultStats)
const localRemoteStart = ref(props.allowRemoteStart)
const showMonitorSection = ref(true)

async function fetchStoppageStats() {
  try {
    const res = await $fetch<StoppageStats>('/api/tickets/stoppage-stats')
    if (res) {
      localStoppageStats.value = res
    }
  } catch {}
}

onMounted(() => {
  if (props.stoppageStats) {
    localStoppageStats.value = props.stoppageStats
  } else {
    fetchStoppageStats()
  }
})

watch(() => props.stoppageStats, (newVal) => {
  if (newVal) localStoppageStats.value = newVal
}, { immediate: true })

watch(() => props.allowRemoteStart, (newVal) => {
  localRemoteStart.value = newVal
})

const toggleFilter = (filterKey: string) => {
  if (props.activeFilter === filterKey) {
    emit('filter-change', null)
  } else {
    emit('filter-change', filterKey)
  }
}

const toggleRemoteStart = () => {
  localRemoteStart.value = !localRemoteStart.value
  emit('update:allowRemoteStart', localRemoteStart.value)
}

function formatMinutes(mins: number): string {
  const h = Math.floor(mins / 60)
  const m = mins % 60
  if (h === 0) return `${m}m`
  return `${h}h ${m}m`
}
</script>

<template>
  <div class="space-y-3">
    <!-- Top 6 Hero Metric Cards -->
    <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
      <!-- 1. Total Active / Open Tickets -->
      <Card
        role="button"
        tabindex="0"
        @click="toggleFilter('open')"
        @keydown.enter="toggleFilter('open')"
        @keydown.space.prevent="toggleFilter('open')"
        :class="[
          activeFilter === 'open' ? 'ring-2 ring-indigo-500 bg-indigo-500/10 shadow-md border-indigo-500/40' : 'hover:border-border/80 hover:bg-muted/50',
          'bg-card border-border rounded-xl shadow-xs cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
        ]"
        title="Click to filter by active tickets (click again to clear)"
      >
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <span class="text-xs font-medium text-muted-foreground group-hover:text-foreground transition-colors">Total Open</span>
            <div class="text-xl font-bold text-foreground mt-0.5">
              {{ (metrics?.openCount || 0) + (metrics?.inProgressCount || 0) + (metrics?.pendingCount || 0) }}
            </div>
          </div>
          <div class="p-2 rounded-lg bg-muted text-muted-foreground group-hover:text-foreground transition-colors">
            <Wrench class="size-4" />
          </div>
        </CardContent>
      </Card>

      <!-- 2. Critical Incidents -->
      <Card
        role="button"
        tabindex="0"
        @click="toggleFilter('critical')"
        @keydown.enter="toggleFilter('critical')"
        @keydown.space.prevent="toggleFilter('critical')"
        :class="[
          activeFilter === 'critical' ? 'ring-2 ring-rose-500 bg-rose-500/15 shadow-md border-rose-500/50' : ((metrics?.criticalCount || 0) > 0 ? 'border-rose-500/30 bg-rose-500/5 hover:bg-rose-500/10' : 'hover:border-border/80 hover:bg-muted/50'),
          'bg-card border-border rounded-xl shadow-xs cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
        ]"
        title="Click to filter by Critical severity incidents"
      >
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <span class="text-xs font-medium text-rose-700 dark:text-rose-400">Critical</span>
            <div class="text-xl font-bold text-rose-700 dark:text-rose-400 mt-0.5">
              {{ metrics?.criticalCount || 0 }}
            </div>
          </div>
          <div class="p-2 rounded-lg bg-rose-500/10 text-rose-700 dark:text-rose-400 border border-rose-500/20 group-hover:bg-rose-500/20 transition-colors">
            <AlertTriangle class="size-4" />
          </div>
        </CardContent>
      </Card>

      <!-- 3. Pending Tickets -->
      <Card
        role="button"
        tabindex="0"
        @click="toggleFilter('pending')"
        @keydown.enter="toggleFilter('pending')"
        @keydown.space.prevent="toggleFilter('pending')"
        :class="[
          activeFilter === 'pending' ? 'ring-2 ring-amber-500 bg-amber-500/15 shadow-md border-amber-500/50' : 'hover:border-border/80 hover:bg-muted/50',
          'bg-card border-border rounded-xl shadow-xs cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
        ]"
        title="Click to filter by Pending status"
      >
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <span class="text-xs font-medium text-amber-800 dark:text-amber-400">Pending</span>
            <div class="text-xl font-bold text-amber-800 dark:text-amber-400 mt-0.5">
              {{ metrics?.pendingCount || 0 }}
            </div>
          </div>
          <div class="p-2 rounded-lg bg-amber-500/10 text-amber-800 dark:text-amber-400 border border-amber-500/20 group-hover:bg-amber-500/20 transition-colors">
            <Package class="size-4" />
          </div>
        </CardContent>
      </Card>

      <!-- 4. Escalated Tickets (Orthogonal Metric) -->
      <Card
        role="button"
        tabindex="0"
        @click="toggleFilter('escalated')"
        @keydown.enter="toggleFilter('escalated')"
        @keydown.space.prevent="toggleFilter('escalated')"
        :class="[
          activeFilter === 'escalated' ? 'ring-2 ring-rose-500 bg-rose-500/15 shadow-md border-rose-500/50' : ((metrics?.escalatedCount || 0) > 0 ? 'border-rose-500/40 bg-rose-500/10 hover:bg-rose-500/15' : 'hover:border-border/80 hover:bg-muted/50'),
          'bg-card border-border rounded-xl shadow-xs cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
        ]"
        title="Click to filter by Escalated incidents"
      >
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <span class="text-xs font-medium text-rose-700 dark:text-rose-400">Escalated</span>
            <div class="text-xl font-bold text-rose-700 dark:text-rose-400 mt-0.5">
              {{ metrics?.escalatedCount || 0 }}
            </div>
          </div>
          <div class="p-2 rounded-lg bg-rose-500/15 text-rose-600 dark:text-rose-400 border border-rose-500/30 group-hover:bg-rose-500/25 transition-colors">
            <Flame class="size-4" />
          </div>
        </CardContent>
      </Card>

      <!-- 5. Resolved / Closed -->
      <Card
        role="button"
        tabindex="0"
        @click="toggleFilter('resolved')"
        @keydown.enter="toggleFilter('resolved')"
        @keydown.space.prevent="toggleFilter('resolved')"
        :class="[
          activeFilter === 'resolved' ? 'ring-2 ring-emerald-500 bg-emerald-500/15 shadow-md border-emerald-500/50' : 'hover:border-border/80 hover:bg-muted/50',
          'bg-card border-border rounded-xl shadow-xs cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
        ]"
        title="Click to view Resolved and Closed tickets"
      >
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <span class="text-xs font-medium text-emerald-700 dark:text-emerald-400">Resolved / Closed</span>
            <div class="text-xl font-bold text-emerald-700 dark:text-emerald-400 mt-0.5">
              {{ (metrics?.resolvedCount || 0) + (metrics?.closedCount || 0) }}
            </div>
          </div>
          <div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border border-emerald-500/20 group-hover:bg-emerald-500/20 transition-colors">
            <CheckCircle2 class="size-4" />
          </div>
        </CardContent>
      </Card>

      <!-- 6. SLA Health -->
      <Card
        role="button"
        tabindex="0"
        @click="toggleFilter('sla')"
        @keydown.enter="toggleFilter('sla')"
        @keydown.space.prevent="toggleFilter('sla')"
        :class="[
          activeFilter === 'sla' ? 'ring-2 ring-indigo-500 bg-indigo-500/15 shadow-md border-indigo-500/50' : 'hover:border-border/80 hover:bg-muted/50',
          'bg-card border-border rounded-xl shadow-xs cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
        ]"
        title="Click to inspect SLA compliance"
      >
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <span class="text-xs font-medium text-indigo-700 dark:text-indigo-400">SLA Health</span>
            <div class="text-xl font-bold text-indigo-700 dark:text-indigo-400 mt-0.5">
              {{ metrics?.slaCompliancePercent ?? 100 }}%
            </div>
          </div>
          <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-700 dark:text-indigo-400 border border-indigo-500/20 group-hover:bg-indigo-500/20 transition-colors">
            <ShieldCheck class="size-4" />
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Plant Floor Monitor & Stoppage Overview Bar -->
    <div
      v-if="localStoppageStats"
      class="p-4 rounded-2xl bg-card border border-border shadow-xs space-y-3"
      data-testid="stoppage-monitor-overview"
    >
      <div class="flex items-center justify-between flex-wrap gap-2">
        <div class="flex items-center gap-2">
          <Activity class="size-4 text-rose-600 dark:text-rose-400" />
          <h3 class="text-xs font-black uppercase tracking-wider text-foreground">
            Plant Floor Monitor &amp; Line Stoppage Analytics
          </h3>
          <Badge variant="outline" class="border-rose-500/30 bg-rose-500/10 text-rose-700 dark:text-rose-300 text-[10px] font-bold">
            7-Day Window
          </Badge>
        </div>

        <!-- Configuration Toggle: Remote Start Allowed vs QR Only Start -->
        <div class="flex items-center gap-2 bg-muted/40 p-1 rounded-xl border border-border">
          <span class="text-[10px] font-bold text-muted-foreground uppercase px-1">Start Mode:</span>
          <button
            type="button"
            @click="toggleRemoteStart"
            :class="[
              'px-2.5 py-1 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer',
              localRemoteStart
                ? 'bg-primary text-primary-foreground shadow-xs'
                : 'bg-muted text-muted-foreground hover:text-foreground'
            ]"
            title="Toggle between Remote Start Allowed and QR-Only Start"
          >
            <Laptop v-if="localRemoteStart" class="size-3" />
            <QrCode v-else class="size-3" />
            <span>{{ localRemoteStart ? 'Remote Start Allowed' : 'QR-Only Start Required' }}</span>
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-3 pt-1">
        <!-- Weekly Stoppage Time -->
        <div class="p-3 bg-muted/20 border border-border rounded-xl space-y-1">
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-muted-foreground flex items-center gap-1">
              <Flame class="size-3 text-rose-500" />
              Weekly Line Stoppage Time
            </span>
            <span class="text-[10px] font-mono text-muted-foreground font-bold">
              {{ localStoppageStats.totalLineStopIncidents }} incidents
            </span>
          </div>
          <div class="text-lg font-black text-rose-700 dark:text-rose-400">
            {{ formatMinutes(localStoppageStats.totalStoppageMinutesThisWeek) }}
          </div>
          <p class="text-[10px] text-muted-foreground">
            Total active factory line halt duration across all departments this week
          </p>
        </div>

        <!-- Top Worst Machines -->
        <div class="p-3 bg-muted/20 border border-border rounded-xl space-y-1.5">
          <span class="text-[11px] font-semibold text-muted-foreground flex items-center gap-1">
            <Cpu class="size-3 text-indigo-500" />
            Top Worst Machines (Downtime)
          </span>
          <div class="space-y-1">
            <div
              v-for="(m, idx) in localStoppageStats.topWorstMachines.slice(0, 3)"
              :key="m.machineName"
              class="flex items-center justify-between text-xs font-mono"
            >
              <div class="flex items-center gap-1.5 truncate max-w-[170px]">
                <span class="text-[10px] font-bold text-muted-foreground">#{{ idx + 1 }}</span>
                <span class="truncate text-foreground font-semibold">{{ m.machineName }}</span>
              </div>
              <span class="font-bold text-rose-600 dark:text-rose-400">
                {{ formatMinutes(m.totalStoppageMinutes) }}
              </span>
            </div>
            <div v-if="localStoppageStats.topWorstMachines.length === 0" class="text-xs text-muted-foreground italic">
              No machine stoppages recorded
            </div>
          </div>
        </div>

        <!-- Department Stoppage Attribution -->
        <div class="p-3 bg-muted/20 border border-border rounded-xl space-y-1.5">
          <span class="text-[11px] font-semibold text-muted-foreground flex items-center gap-1">
            <Building2 class="size-3 text-cyan-500" />
            Department Stoppage Attribution
          </span>
          <div class="flex flex-wrap gap-1.5">
            <div
              v-for="d in localStoppageStats.departmentBreakdown.slice(0, 4)"
              :key="d.department"
              class="px-2 py-0.5 bg-background border border-border rounded-md text-[10px] font-mono flex items-center gap-1"
            >
              <span class="font-bold text-indigo-600 dark:text-indigo-400">{{ d.department }}:</span>
              <span class="text-foreground">{{ formatMinutes(d.stoppageMinutes) }}</span>
            </div>
            <div v-if="localStoppageStats.departmentBreakdown.length === 0" class="text-xs text-muted-foreground italic">
              No department downtime registered
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
