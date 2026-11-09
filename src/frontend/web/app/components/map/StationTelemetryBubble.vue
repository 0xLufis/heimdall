<script setup lang="ts">
import { computed } from 'vue'
import {
  Activity,
  AlertTriangle,
  Flame,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  X,
  Plus,
  ArrowUpRight,
  Clock,
  Gauge,
  Cpu,
  Layers
} from 'lucide-vue-next'
import type { StationTelemetryKpi, StationSlaSummary } from '~/composables/useStationTelemetry'

const props = withDefaults(
  defineProps<{
    handle: string
    name?: string
    kpi: StationTelemetryKpi
    sla?: StationSlaSummary
    isExpanded?: boolean
    mode?: 'compact' | 'expanded'
    showYield?: boolean
    showPartsHour?: boolean
    showOee?: boolean
    showSla?: boolean
  }>(),
  {
    name: '',
    isExpanded: false,
    mode: 'compact',
    showYield: true,
    showPartsHour: true,
    showOee: true,
    showSla: true
  }
)

const emit = defineEmits<{
  (e: 'toggle-expand'): void
  (e: 'select-station', handle: string): void
  (e: 'create-ticket', handle: string): void
  (e: 'view-ticket', ticketId: string): void
}>()

const oeeColorClass = computed(() => {
  const val = props.kpi.oee
  if (val >= 85) return 'text-emerald-400 bg-emerald-500/10 border-emerald-500/30'
  if (val >= 70) return 'text-amber-400 bg-amber-500/10 border-amber-500/30'
  return 'text-rose-400 bg-rose-500/10 border-rose-500/30'
})

const oeeBarColor = computed(() => {
  const val = props.kpi.oee
  if (val >= 85) return 'bg-emerald-500'
  if (val >= 70) return 'bg-amber-500'
  return 'bg-rose-500'
})

const slaBadgeClass = computed(() => {
  if (!props.sla || props.sla.slaHealth === 'none') {
    return 'text-emerald-400 bg-emerald-500/10 border-emerald-500/30'
  }
  if (props.sla.slaHealth === 'breached') {
    return 'text-rose-400 bg-rose-500/20 border-rose-500/50 animate-pulse'
  }
  if (props.sla.slaHealth === 'warning') {
    return 'text-amber-400 bg-amber-500/20 border-amber-500/40'
  }
  return 'text-sky-400 bg-sky-500/10 border-sky-500/30'
})

const statusBadgeClass = computed(() => {
  switch (props.kpi.operationalStatus) {
    case 'RUNNING':
      return 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30'
    case 'IDLE':
      return 'bg-amber-500/15 text-amber-400 border-amber-500/30'
    case 'WARNING':
      return 'bg-orange-500/15 text-orange-400 border-orange-500/30'
    case 'FAULTED':
      return 'bg-rose-500/20 text-rose-400 border-rose-500/40 animate-pulse'
    case 'MAINTENANCE':
      return 'bg-purple-500/15 text-purple-400 border-purple-500/30'
    default:
      return 'bg-slate-500/15 text-slate-400 border-slate-500/30'
  }
})
</script>

<template>
  <div class="station-telemetry-bubble font-sans select-none pointer-events-auto">
    <!-- Compact Pill Overlay View -->
    <div
      v-if="!isExpanded"
      @click.stop="emit('toggle-expand')"
      class="compact-pill group flex items-center gap-1.5 px-2 py-1 bg-slate-950/90 hover:bg-slate-900 border border-slate-700/80 hover:border-indigo-500/60 rounded-full shadow-lg backdrop-blur-md cursor-pointer transition-all duration-200 transform hover:scale-105"
      :title="`Station ${handle}: Click to inspect telemetry & SLA tickets`"
    >
      <!-- SLA Urgent Indicator (Shown first if tickets exist) -->
      <span
        v-if="showSla && sla && sla.openTickets > 0"
        :class="slaBadgeClass"
        class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-bold border font-mono tracking-tight"
      >
        <Flame v-if="sla.slaHealth === 'breached'" class="size-2.5 text-rose-400 shrink-0" />
        <AlertTriangle v-else class="size-2.5 text-amber-400 shrink-0" />
        <span>{{ sla.openTickets }} SLA</span>
      </span>

      <!-- OEE Badge -->
      <span
        v-if="showOee"
        :class="oeeColorClass"
        class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-bold border font-mono"
      >
        <Gauge class="size-2.5 shrink-0" />
        <span>{{ kpi.oee }}%</span>
      </span>

      <!-- Yield & Parts/Hour -->
      <span
        v-if="showYield && showPartsHour"
        class="text-[10px] text-slate-300 font-mono font-medium hidden sm:inline"
      >
        {{ kpi.yield }}% · {{ kpi.partsPerHour }}/h
      </span>
      <span
        v-else-if="showYield"
        class="text-[10px] text-slate-300 font-mono font-medium hidden sm:inline"
      >
        {{ kpi.yield }}%
      </span>
      <span
        v-else-if="showPartsHour"
        class="text-[10px] text-slate-300 font-mono font-medium hidden sm:inline"
      >
        {{ kpi.partsPerHour }}/h
      </span>

      <!-- Expand Indicator Chevron -->
      <ChevronDown class="size-3 text-slate-400 group-hover:text-indigo-400 transition-colors" />
    </div>

    <!-- Expanded Full HUD Card View -->
    <div
      v-else
      @click.stop
      class="expanded-card w-72 sm:w-80 bg-slate-950/95 border border-slate-700 shadow-2xl rounded-xl p-3 backdrop-blur-xl text-slate-100 flex flex-col gap-2.5 animate-in fade-in zoom-in-95 duration-150"
    >
      <!-- Card Header -->
      <div class="flex items-center justify-between pb-2 border-b border-slate-800">
        <div class="flex items-center gap-2">
          <div class="p-1 rounded bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Cpu class="size-3.5" />
          </div>
          <div>
            <div class="flex items-center gap-1.5">
              <span class="text-xs font-bold text-white tracking-wide font-mono">{{ handle }}</span>
              <span :class="statusBadgeClass" class="text-[9px] px-1.5 py-0.2 rounded font-semibold border uppercase">
                {{ kpi.operationalStatus }}
              </span>
            </div>
            <p class="text-[10px] text-slate-400 truncate max-w-[170px]" :title="kpi.name">
              {{ kpi.name }}
            </p>
          </div>
        </div>

        <button
          @click.stop="emit('toggle-expand')"
          class="p-1 text-slate-400 hover:text-white hover:bg-slate-800 rounded transition-colors"
          title="Collapse bubble"
        >
          <X class="size-3.5" />
        </button>
      </div>

      <!-- Key At-A-Glance Stats Grid (OEE, Yield, Parts/Hour) -->
      <div class="grid grid-cols-3 gap-2 p-2 bg-slate-900/80 rounded-lg border border-slate-800/80">
        <!-- OEE Metric Column -->
        <div class="flex flex-col">
          <span class="text-[9px] uppercase font-semibold text-slate-400 tracking-wider">OEE</span>
          <span class="text-sm font-black font-mono mt-0.5" :class="oeeColorClass.split(' ')[0]">
            {{ kpi.oee }}%
          </span>
          <div class="w-full bg-slate-800 rounded-full h-1 mt-1 overflow-hidden">
            <div :class="oeeBarColor" class="h-full rounded-full" :style="`width: ${Math.min(100, kpi.oee)}%`"></div>
          </div>
          <span class="text-[8px] text-slate-400 mt-1 font-mono">
            A:{{ kpi.availability }}% P:{{ kpi.performance }}%
          </span>
        </div>

        <!-- YIELD Metric Column -->
        <div class="flex flex-col border-l border-slate-800 pl-2">
          <span class="text-[9px] uppercase font-semibold text-slate-400 tracking-wider">Yield</span>
          <span class="text-sm font-black font-mono text-cyan-400 mt-0.5">
            {{ kpi.yield }}%
          </span>
          <div class="w-full bg-slate-800 rounded-full h-1 mt-1 overflow-hidden">
            <div class="bg-cyan-500 h-full rounded-full" :style="`width: ${Math.min(100, kpi.yield)}%`"></div>
          </div>
          <span class="text-[8px] text-slate-400 mt-1 font-mono">
            Scrap: {{ kpi.scrapParts }} pcs
          </span>
        </div>

        <!-- PARTS/HOUR Metric Column -->
        <div class="flex flex-col border-l border-slate-800 pl-2">
          <span class="text-[9px] uppercase font-semibold text-slate-400 tracking-wider">Parts / h</span>
          <span class="text-sm font-black font-mono text-indigo-400 mt-0.5">
            {{ kpi.partsPerHour }}
          </span>
          <div class="w-full bg-slate-800 rounded-full h-1 mt-1 overflow-hidden">
            <div class="bg-indigo-500 h-full rounded-full" :style="`width: ${Math.min(100, (kpi.partsPerHour / kpi.targetPartsPerHour) * 100)}%`"></div>
          </div>
          <span class="text-[8px] text-slate-400 mt-1 font-mono">
            Tact: {{ kpi.cycleTimeSeconds }}s
          </span>
        </div>
      </div>

      <!-- SLA Tickets Overlay Section -->
      <div class="p-2 bg-slate-900/60 rounded-lg border border-slate-800/80 flex flex-col gap-1.5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1.5">
            <Clock class="size-3 text-slate-400" />
            <span class="text-[10px] font-bold uppercase tracking-wider text-slate-300">SLA Tickets</span>
          </div>

          <span
            :class="slaBadgeClass"
            class="text-[9px] px-1.5 py-0.2 rounded font-mono font-bold border"
          >
            {{ sla?.earliestDueText || (sla?.openTickets ? `${sla.openTickets} Active` : 'SLA Compliant') }}
          </span>
        </div>

        <!-- Active Tickets List -->
        <div v-if="sla && sla.tickets && sla.tickets.length > 0" class="flex flex-col gap-1 max-h-24 overflow-y-auto pr-1">
          <div
            v-for="t in sla.tickets.slice(0, 3)"
            :key="t.id"
            @click="emit('view-ticket', t.id)"
            class="p-1.5 rounded bg-slate-950 hover:bg-slate-800 border border-slate-800/90 cursor-pointer flex items-center justify-between text-[10px] transition-colors group"
          >
            <div class="flex items-center gap-1.5 truncate mr-2">
              <span
                :class="{
                  'bg-rose-500/20 text-rose-400 border-rose-500/40': t.priority === 'Critical',
                  'bg-orange-500/20 text-orange-400 border-orange-500/40': t.priority === 'High',
                  'bg-amber-500/20 text-amber-400 border-amber-500/40': t.priority === 'Medium',
                  'bg-slate-500/20 text-slate-400 border-slate-500/40': t.priority === 'Low'
                }"
                class="text-[8px] font-mono px-1 py-0.2 rounded border uppercase font-bold shrink-0"
              >
                {{ t.priority }}
              </span>
              <span class="truncate text-slate-200 group-hover:text-indigo-300 transition-colors">
                {{ t.title }}
              </span>
            </div>

            <ArrowUpRight class="size-2.5 text-slate-500 group-hover:text-indigo-400 shrink-0" />
          </div>
        </div>

        <div v-else class="text-[10px] text-slate-400 py-1 flex items-center gap-1.5">
          <CheckCircle2 class="size-3 text-emerald-400 shrink-0" />
          <span>Zero active maintenance tickets on this station.</span>
        </div>
      </div>

      <!-- Action Footer -->
      <div class="flex items-center justify-between gap-2 pt-1">
        <button
          type="button"
          @click="emit('select-station', handle)"
          class="flex-1 py-1 px-2 rounded bg-slate-900 hover:bg-slate-800 border border-slate-800 text-[10px] font-semibold text-slate-200 transition-colors text-center"
        >
          Select Node
        </button>

        <button
          type="button"
          @click="emit('create-ticket', handle)"
          class="py-1 px-2 rounded bg-indigo-600 hover:bg-indigo-500 text-white text-[10px] font-semibold flex items-center gap-1 transition-colors"
        >
          <Plus class="size-2.5" />
          <span>Ticket</span>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.station-telemetry-bubble {
  transform: translate(-50%, -100%);
  margin-top: -10px;
}
</style>
