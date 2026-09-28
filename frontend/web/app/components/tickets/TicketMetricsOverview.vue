<script setup lang="ts">
import { AlertTriangle, Clock, CheckCircle2, Wrench, Package, ShieldCheck, Flame } from 'lucide-vue-next'
import { Card, CardContent } from '~/components/ui/card'
import type { TicketMetrics } from '~/types/maintenance'

const props = withDefaults(
  defineProps<{
    metrics?: TicketMetrics
    activeFilter?: string | null
  }>(),
  {
    activeFilter: null
  }
)

const emit = defineEmits<{
  (e: 'filter-change', filter: string | null): void
}>()

const toggleFilter = (filterKey: string) => {
  if (props.activeFilter === filterKey) {
    emit('filter-change', null)
  } else {
    emit('filter-change', filterKey)
  }
}
</script>

<template>
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
          <span class="text-xs font-medium text-muted-foreground group-hover:text-foreground transition-colors">Total Active</span>
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
</template>
