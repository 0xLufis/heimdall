<script setup lang="ts">
import { AlertTriangle, Clock, CheckCircle2, Wrench, Package, ShieldCheck } from 'lucide-vue-next'
import { Card, CardContent } from '~/components/ui/card'

const props = withDefaults(
  defineProps<{
    metrics?: {
      totalTickets: number
      openCount: number
      inProgressCount: number
      pendingPartsCount: number
      resolvedCount: number
      closedCount: number
      criticalCount: number
      overdueCount: number
      slaCompliancePercent: number
    }
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
        activeFilter === 'open' ? 'ring-2 ring-indigo-500 bg-slate-800/90 shadow-md' : 'hover:border-slate-700 hover:bg-slate-900/95',
        'bg-slate-900 border-slate-800 rounded-xl shadow-sm cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
      ]"
      title="Click to filter by active open tickets (click again to clear)"
    >
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-slate-400 group-hover:text-slate-300 transition-colors">Total Open</span>
          <div class="text-xl font-bold text-slate-100 mt-0.5">
            {{ (metrics?.openCount || 0) + (metrics?.inProgressCount || 0) + (metrics?.pendingPartsCount || 0) }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-slate-800 text-slate-300 group-hover:text-white transition-colors">
          <Wrench class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- 2. Critical Alerts -->
    <Card
      role="button"
      tabindex="0"
      @click="toggleFilter('critical')"
      @keydown.enter="toggleFilter('critical')"
      @keydown.space.prevent="toggleFilter('critical')"
      :class="[
        activeFilter === 'critical' ? 'ring-2 ring-rose-500 bg-rose-950/40 shadow-md border-rose-500/50' : ((metrics?.criticalCount || 0) > 0 ? 'border-rose-500/30 bg-rose-500/5 hover:bg-rose-500/10' : 'hover:border-slate-700'),
        'bg-slate-900 border-slate-800 rounded-xl shadow-sm cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
      ]"
      title="Click to filter by Critical / High severity incidents (click again to clear)"
    >
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-rose-400">Critical</span>
          <div class="text-xl font-bold text-rose-400 mt-0.5">
            {{ metrics?.criticalCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-rose-500/10 text-rose-400 border border-rose-500/20 group-hover:bg-rose-500/20 transition-colors">
          <AlertTriangle class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- 3. Pending Parts -->
    <Card
      role="button"
      tabindex="0"
      @click="toggleFilter('pending_parts')"
      @keydown.enter="toggleFilter('pending_parts')"
      @keydown.space.prevent="toggleFilter('pending_parts')"
      :class="[
        activeFilter === 'pending_parts' ? 'ring-2 ring-amber-500 bg-amber-950/40 shadow-md border-amber-500/50' : 'hover:border-slate-700',
        'bg-slate-900 border-slate-800 rounded-xl shadow-sm cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
      ]"
      title="Click to filter by Pending Parts status (click again to clear)"
    >
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-amber-400">Pending Parts</span>
          <div class="text-xl font-bold text-amber-400 mt-0.5">
            {{ metrics?.pendingPartsCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/20 group-hover:bg-amber-500/20 transition-colors">
          <Package class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- 4. Overdue SLA -->
    <Card
      role="button"
      tabindex="0"
      @click="toggleFilter('overdue')"
      @keydown.enter="toggleFilter('overdue')"
      @keydown.space.prevent="toggleFilter('overdue')"
      :class="[
        activeFilter === 'overdue' ? 'ring-2 ring-amber-500 bg-amber-950/40 shadow-md border-amber-500/50' : 'hover:border-slate-700',
        'bg-slate-900 border-slate-800 rounded-xl shadow-sm cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
      ]"
      title="Click to filter by overdue / SLA breached tickets (click again to clear)"
    >
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-amber-500">Overdue SLA</span>
          <div class="text-xl font-bold text-amber-500 mt-0.5">
            {{ metrics?.overdueCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-amber-500/10 text-amber-500 border border-amber-500/20 group-hover:bg-amber-500/20 transition-colors">
          <Clock class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- 5. Resolved Today -->
    <Card
      role="button"
      tabindex="0"
      @click="toggleFilter('resolved')"
      @keydown.enter="toggleFilter('resolved')"
      @keydown.space.prevent="toggleFilter('resolved')"
      :class="[
        activeFilter === 'resolved' ? 'ring-2 ring-emerald-500 bg-emerald-950/40 shadow-md border-emerald-500/50' : 'hover:border-slate-700',
        'bg-slate-900 border-slate-800 rounded-xl shadow-sm cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
      ]"
      title="Click to view Resolved and Closed tickets (click again to clear)"
    >
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-emerald-400">Resolved</span>
          <div class="text-xl font-bold text-emerald-400 mt-0.5">
            {{ (metrics?.resolvedCount || 0) + (metrics?.closedCount || 0) }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 group-hover:bg-emerald-500/20 transition-colors">
          <CheckCircle2 class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- 6. SLA Compliance Health -->
    <Card
      role="button"
      tabindex="0"
      @click="toggleFilter('sla')"
      @keydown.enter="toggleFilter('sla')"
      @keydown.space.prevent="toggleFilter('sla')"
      :class="[
        activeFilter === 'sla' ? 'ring-2 ring-indigo-500 bg-indigo-950/40 shadow-md border-indigo-500/50' : 'hover:border-slate-700',
        'bg-slate-900 border-slate-800 rounded-xl shadow-sm cursor-pointer transition-all duration-200 hover:-translate-y-0.5 select-none group'
      ]"
      title="Click to inspect SLA compliance (click again to clear)"
    >
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-indigo-400">SLA Health</span>
          <div class="text-xl font-bold text-indigo-400 mt-0.5">
            {{ metrics?.slaCompliancePercent ?? 100 }}%
          </div>
        </div>
        <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 group-hover:bg-indigo-500/20 transition-colors">
          <ShieldCheck class="size-4" />
        </div>
      </CardContent>
    </Card>
  </div>
</template>
