<script setup lang="ts">
import { computed, useId } from 'vue'
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
  Layers,
  Zap,
  Radio
} from 'lucide-vue-next'
import { useStationTelemetry, type StationTelemetryKpi, type StationSlaSummary, type StationOscillationData } from '~/composables/useStationTelemetry'

const props = withDefaults(
  defineProps<{
    handle: string
    name?: string
    /** null / undefined: no telemetry has been reported for this station yet. */
    kpi?: StationTelemetryKpi | null
    sla?: StationSlaSummary
    oscillation?: StationOscillationData | null
    isExpanded?: boolean
    mode?: 'compact' | 'expanded'
    showYield?: boolean
    showPartsHour?: boolean
    showOee?: boolean
    showSla?: boolean
    showOscillation?: boolean
    isFlippedBottom?: boolean
    /** Hard size limits from the parent's viewport-clamping so the DOM box never exceeds the computed one. */
    maxWidthPx?: number
    maxHeightPx?: number
    /** Horizontal position of the pointer arrow (anchor) relative to the card's left edge. */
    arrowOffsetPx?: number
  }>(),
  {
    name: '',
    kpi: null,
    oscillation: null,
    isExpanded: false,
    mode: 'compact',
    showYield: true,
    showPartsHour: true,
    showOee: true,
    showSla: true,
    showOscillation: true,
    isFlippedBottom: false
  }
)

const emit = defineEmits<{
  (e: 'toggle-expand'): void
  (e: 'select-station', handle: string): void
  (e: 'create-ticket', handle: string): void
  (e: 'view-ticket', ticketId: string): void
}>()

const { getStationOscillation } = useStationTelemetry()

const activeOscillation = computed<StationOscillationData | null>(() => {
  return props.oscillation ?? getStationOscillation(props.handle)
})

// SVG ids are document-global: derive a unique, attribute-safe id per instance.
const gradientId = `station-spark-${useId() ?? ''}-${(props.handle || '').replace(/[^A-Za-z0-9_-]/g, '_')}`

const lastSampleText = computed(() => {
  const ts = activeOscillation.value?.timestamp
  if (!ts || !Number.isFinite(ts)) return null
  return new Date(ts).toLocaleTimeString()
})

const partsRatePct = computed(() => {
  const k = props.kpi
  if (!k || k.partsPerHour === null || k.targetPartsPerHour === null || !(k.targetPartsPerHour > 0)) return 0
  return Math.max(0, Math.min(100, (k.partsPerHour / k.targetPartsPerHour) * 100))
})

/** Formats a reported value; '—' when the PLC has not reported it. */
const fmt = (v: number | null | undefined, digits?: number) =>
  typeof v === 'number' && Number.isFinite(v) ? (digits === undefined ? String(v) : v.toFixed(digits)) : '—'

const pct = (v: number | null | undefined) => Math.max(0, Math.min(100, Number.isFinite(v) ? (v as number) : 0))

const cardStyle = computed(() => {
  const style: Record<string, string> = {}
  if (props.maxWidthPx && props.maxWidthPx > 0) {
    style.width = `${Math.min(320, props.maxWidthPx)}px`
    style.maxWidth = `${props.maxWidthPx}px`
  }
  if (props.maxHeightPx && props.maxHeightPx > 0) style.maxHeight = `${props.maxHeightPx}px`
  return style
})

const pillStyle = computed(() => (props.maxWidthPx && props.maxWidthPx > 0 ? { maxWidth: `${props.maxWidthPx}px` } : {}))

const arrowStyle = computed(() => {
  if (props.arrowOffsetPx === undefined) return {}
  // Arrow is a 12px rotated square: center it on the anchor.
  return { left: `${Math.max(6, props.arrowOffsetPx - 6)}px` }
})

const sparklinePoints = computed(() => {
  const history = activeOscillation.value?.history || []
  if (history.length <= 1) return '0,15 100,15'

  const min = Math.min(...history, 0.4)
  const max = Math.max(...history, 3.2)
  const range = max - min || 1.0

  return history
    .map((val, idx) => {
      const x = (idx / (history.length - 1)) * 100
      const y = 26 - ((val - min) / range) * 22
      return `${x.toFixed(1)},${y.toFixed(1)}`
    })
    .join(' ')
})

const sparklineAreaPoints = computed(() => {
  const pts = sparklinePoints.value
  if (!pts) return '0,28 100,28'
  return `0,28 ${pts} 100,28`
})

const latestPoint = computed(() => {
  const history = activeOscillation.value?.history || []
  if (history.length === 0) return { x: 100, y: 14 }
  const min = Math.min(...history, 0.4)
  const max = Math.max(...history, 3.2)
  const range = max - min || 1.0
  const lastVal = history[history.length - 1]
  const y = 26 - ((lastVal - min) / range) * 22
  return { x: 100, y: Number(y.toFixed(1)) }
})

const oeeColorClass = computed(() => {
  const val = props.kpi?.oee
  if (val === undefined || val === null) return 'text-slate-400 bg-slate-500/10 border-slate-500/30'
  if (val >= 85) return 'text-emerald-400 bg-emerald-500/10 border-emerald-500/30'
  if (val >= 70) return 'text-amber-400 bg-amber-500/10 border-amber-500/30'
  return 'text-rose-400 bg-rose-500/10 border-rose-500/30'
})

const oeeBarColor = computed(() => {
  const val = props.kpi?.oee ?? 0
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
  switch (props.kpi?.operationalStatus) {
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
      :style="pillStyle"
      class="compact-pill group flex items-center gap-1.5 overflow-hidden px-2 py-1 bg-slate-950/90 hover:bg-slate-900 border border-slate-700/80 hover:border-indigo-500/60 rounded-full shadow-lg backdrop-blur-md cursor-pointer transition-all duration-200 transform hover:scale-105"
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

      <!-- No telemetry reported for this station -->
      <span
        v-if="!kpi"
        class="no-telemetry inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-medium border font-mono text-slate-400 bg-slate-500/10 border-slate-500/30"
      >
        <Gauge class="size-2.5 shrink-0" />
        <span>No telemetry</span>
      </span>

      <template v-else>
      <!-- OEE Badge -->
      <span
        v-if="showOee"
        :class="oeeColorClass"
        class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-bold border font-mono"
      >
        <Gauge class="size-2.5 shrink-0" />
        <span>{{ fmt(kpi.oee) }}%</span>
      </span>

      <!-- Yield & Parts/Hour -->
      <span
        v-if="showYield && showPartsHour"
        class="text-[10px] text-slate-300 font-mono font-medium hidden sm:inline"
      >
        {{ fmt(kpi.yield) }}% · {{ fmt(kpi.partsPerHour) }}/h
      </span>
      <span
        v-else-if="showYield"
        class="text-[10px] text-slate-300 font-mono font-medium hidden sm:inline"
      >
        {{ fmt(kpi.yield) }}%
      </span>
      <span
        v-else-if="showPartsHour"
        class="text-[10px] text-slate-300 font-mono font-medium hidden sm:inline"
      >
        {{ fmt(kpi.partsPerHour) }}/h
      </span>
      </template>

      <!-- Sub-Second Live Vibration & Stream Indicator -->
      <span
        v-if="showOscillation && activeOscillation"
        class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-mono border tracking-tight"
        :class="activeOscillation.vibrationMmS > 3.0 ? 'text-amber-400 bg-amber-500/15 border-amber-500/35' : 'text-cyan-400 bg-cyan-500/10 border-cyan-500/30'"
        :title="`Sub-Second Oscillation: ${activeOscillation.vibrationMmS.toFixed(2)} mm/s RMS | Spindle: ${fmt(activeOscillation.spindleRpm, 0)} RPM`"
      >
        <span class="w-1.5 h-1.5 rounded-full bg-cyan-400 animate-pulse shrink-0" />
        <span>{{ activeOscillation.vibrationMmS.toFixed(1) }} mm/s</span>
      </span>

      <!-- Expand Indicator Chevron -->
      <ChevronDown class="size-3 text-slate-400 group-hover:text-indigo-400 transition-colors" />
    </div>

    <!-- Expanded Full HUD Card View -->
    <div
      v-else
      @click.stop
      :style="cardStyle"
      class="expanded-card relative w-72 sm:w-80 max-h-[min(500px,calc(100vh-140px))] overflow-y-auto custom-scrollbar bg-slate-950/95 border border-slate-700 shadow-2xl rounded-xl p-3 backdrop-blur-xl text-slate-100 flex flex-col gap-2.5 animate-in fade-in zoom-in-95 duration-150"
    >
      <!-- Pointer Connector Indicator -->
      <div
        v-if="isFlippedBottom"
        :style="arrowStyle"
        class="absolute -top-1.5 left-6 w-3 h-3 bg-slate-950 border-t border-l border-slate-700 transform rotate-45 pointer-events-none"
      />
      <div
        v-else
        :style="arrowStyle"
        class="absolute -bottom-1.5 left-6 w-3 h-3 bg-slate-950 border-b border-r border-slate-700 transform rotate-45 pointer-events-none"
      />
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
                {{ kpi ? kpi.operationalStatus : 'NO DATA' }}
              </span>
            </div>
            <p class="text-[10px] text-slate-400 truncate max-w-[170px]" :title="kpi?.name || name || handle">
              {{ kpi?.name || name || handle }}
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
      <div
        v-if="!kpi"
        class="no-telemetry p-2 bg-slate-900/80 rounded-lg border border-slate-800/80 text-[10px] text-slate-400"
      >
        No production telemetry has been reported for this station yet.
      </div>
      <div v-else class="grid grid-cols-3 gap-2 p-2 bg-slate-900/80 rounded-lg border border-slate-800/80">
        <!-- OEE Metric Column -->
        <div class="flex flex-col">
          <span class="text-[9px] uppercase font-semibold text-slate-400 tracking-wider">OEE</span>
          <span class="text-sm font-black font-mono mt-0.5" :class="oeeColorClass.split(' ')[0]">
            {{ fmt(kpi.oee) }}%
          </span>
          <div class="w-full bg-slate-800 rounded-full h-1 mt-1 overflow-hidden">
            <div :class="oeeBarColor" class="h-full rounded-full" :style="{ width: `${pct(kpi.oee)}%` }"></div>
          </div>
          <span class="text-[8px] text-slate-400 mt-1 font-mono">
            A:{{ fmt(kpi.availability) }}% P:{{ fmt(kpi.performance) }}%
          </span>
        </div>

        <!-- YIELD Metric Column -->
        <div class="flex flex-col border-l border-slate-800 pl-2">
          <span class="text-[9px] uppercase font-semibold text-slate-400 tracking-wider">Yield</span>
          <span class="text-sm font-black font-mono text-cyan-400 mt-0.5">
            {{ fmt(kpi.yield) }}%
          </span>
          <div class="w-full bg-slate-800 rounded-full h-1 mt-1 overflow-hidden">
            <div class="bg-cyan-500 h-full rounded-full" :style="{ width: `${pct(kpi.yield)}%` }"></div>
          </div>
          <span class="text-[8px] text-slate-400 mt-1 font-mono">
            Scrap: {{ fmt(kpi.scrapParts) }} pcs
          </span>
        </div>

        <!-- PARTS/HOUR Metric Column -->
        <div class="flex flex-col border-l border-slate-800 pl-2">
          <span class="text-[9px] uppercase font-semibold text-slate-400 tracking-wider">Parts / h</span>
          <span class="text-sm font-black font-mono text-indigo-400 mt-0.5">
            {{ fmt(kpi.partsPerHour) }}
          </span>
          <div class="w-full bg-slate-800 rounded-full h-1 mt-1 overflow-hidden">
            <div class="bg-indigo-500 h-full rounded-full" :style="{ width: `${partsRatePct}%` }"></div>
          </div>
          <span class="text-[8px] text-slate-400 mt-1 font-mono">
            Tact: {{ fmt(kpi.cycleTimeSeconds) }}s
          </span>
        </div>
      </div>

      <!-- Sub-Second Live Dynamics & Oscillation Waveform -->
      <div v-if="showOscillation && activeOscillation" class="p-2 bg-slate-900/85 rounded-lg border border-slate-800/90 flex flex-col gap-1.5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1.5">
            <Radio class="size-3 text-cyan-400 animate-pulse" />
            <span class="text-[10px] font-bold uppercase tracking-wider text-slate-300">Live Machine Dynamics</span>
          </div>

          <div
            v-if="lastSampleText"
            class="flex items-center gap-1 px-1.5 py-0.5 rounded bg-emerald-500/10 border border-emerald-500/30 text-[9px] font-mono text-emerald-400 font-semibold"
            :title="'Timestamp of the last sample received from the backend'"
          >
            <span>{{ lastSampleText }}</span>
          </div>
        </div>

        <!-- Real-Time Sparkline Waveform -->
        <div class="relative w-full h-9 bg-slate-950/80 rounded border border-slate-800 overflow-hidden flex items-center px-1">
          <svg class="w-full h-7 overflow-visible" viewBox="0 0 100 28" preserveAspectRatio="none">
            <defs>
              <linearGradient :id="gradientId" x1="0%" y1="0%" x2="0%" y2="100%">
                <stop offset="0%" stop-color="#06b6d4" stop-opacity="0.45" />
                <stop offset="100%" stop-color="#06b6d4" stop-opacity="0.0" />
              </linearGradient>
            </defs>
            <polygon :points="sparklineAreaPoints" :fill="`url(#${gradientId})`" />
            <polyline :points="sparklinePoints" fill="none" stroke="#22d3ee" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
            <circle :cx="latestPoint.x" :cy="latestPoint.y" r="2.5" fill="#38bdf8" />
          </svg>
        </div>

        <!-- Dynamic Metrics Grid -->
        <div class="grid grid-cols-3 gap-1.5 text-center">
          <div class="bg-slate-950/70 p-1 rounded border border-slate-800/70">
            <div class="text-[8px] uppercase text-slate-400 font-medium">Vibration</div>
            <div class="text-[11px] font-mono font-bold text-cyan-300">
              {{ activeOscillation.vibrationMmS.toFixed(2) }} <span class="text-[8px] font-normal text-slate-400">mm/s</span>
            </div>
          </div>
          <div class="bg-slate-950/70 p-1 rounded border border-slate-800/70">
            <div class="text-[8px] uppercase text-slate-400 font-medium">Spindle</div>
            <div class="text-[11px] font-mono font-bold text-indigo-300">
              {{ fmt(activeOscillation.spindleRpm, 0) }} <span class="text-[8px] font-normal text-slate-400">RPM</span>
            </div>
          </div>
          <div class="bg-slate-950/70 p-1 rounded border border-slate-800/70">
            <div class="text-[8px] uppercase text-slate-400 font-medium">Power</div>
            <div class="text-[11px] font-mono font-bold text-amber-300">
              {{ fmt(activeOscillation.powerKw, 1) }} <span class="text-[8px] font-normal text-slate-400">kW</span>
            </div>
          </div>
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
  display: inline-block;
  will-change: transform;
}
</style>
