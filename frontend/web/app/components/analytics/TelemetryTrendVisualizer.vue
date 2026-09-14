<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Badge } from '~/components/ui/badge'
import { Card } from '~/components/ui/card'
import { useTelemetryMetrics } from '~/composables/useTelemetryMetrics'
import {
  TrendingUp,
  AlertTriangle,
  Clock,
  Sliders,
  Maximize2,
  RefreshCw,
  HelpCircle,
  ShieldAlert
} from 'lucide-vue-next'

export interface TelemetryPoint {
  timestamp: string
  value: number
  isAnomaly: boolean
  zScore: number
}

export interface AnomalyDetail {
  timestamp: string
  metric: string
  value: number
  expectedValue: number
  zScore: number
  severity: string
  description: string
}

export interface TrendSeriesData {
  machineId: string
  metricName: string
  metricKey?: string
  unit: string
  nominalValue: number
  upperTolerance: number
  lowerTolerance: number
  points: TelemetryPoint[]
  detectedAnomalies: AnomalyDetail[]
  source?: string
  isUserDefined?: boolean
}

const props = defineProps<{
  initialMachineId?: string
}>()

const {
  metrics: telemetryMetrics,
  fetchMetrics: fetchTelemetryMetrics
} = useTelemetryMetrics()

const activeMachineId = ref(props.initialMachineId || 'm-op20')
const activeMetric = ref<string>('cycle_time')
const activeRange = ref<'1h' | '8h' | '24h' | '7d'>('8h')
const isLoading = ref(false)

const trendData = ref<TrendSeriesData>({
  machineId: 'm-op20',
  metricName: 'Cycle Time Deviation',
  unit: 'ms',
  nominalValue: 1200.0,
  upperTolerance: 1280.0,
  lowerTolerance: 1120.0,
  points: [],
  detectedAnomalies: []
})

const hoveredPoint = ref<TelemetryPoint | null>(null)
const hoveredAnomaly = ref<AnomalyDetail | null>(null)

const machinesList = [
  { id: 'm-op20', name: 'OP20-Weld (Laser Robotic Cell)' },
  { id: 'm-op50', name: 'OP50-Fasten (Multi-Spindle Fastener)' },
  { id: 'm-op10', name: 'OP10-Dispense (Polymer Bonding Station)' },
  { id: 'm-op30', name: 'OP30-Robotics (KUKA Vision Cell)' },
  { id: 'm-op80', name: 'OP80-EOL (High Voltage Test Chamber)' }
]

const fetchTrends = async () => {
  isLoading.value = true
  try {
    const data = await $fetch<TrendSeriesData>(`/api/analytics/trends`, {
      params: {
        machineId: activeMachineId.value,
        metric: activeMetric.value,
        range: activeRange.value
      }
    })
    if (data && data.points) {
      trendData.value = data
    }
  } catch {
    // Graceful fallback to proxy endpoint if available
    try {
      const fallback = await $fetch<TrendSeriesData>(`/api/proxy/v1/analytics/trends`, {
        params: {
          machineId: activeMachineId.value,
          metric: activeMetric.value,
          range: activeRange.value
        }
      })
      if (fallback && fallback.points) {
        trendData.value = fallback
      }
    } catch {
      // Retain local state
    }
  } finally {
    isLoading.value = false
  }
}

watch([activeMachineId, activeMetric, activeRange], () => {
  fetchTrends()
})

onMounted(async () => {
  await fetchTelemetryMetrics()
  fetchTrends()
})

// SVG Coordinates calculation
const svgWidth = 800
const svgHeight = 260
const padding = { top: 25, right: 30, bottom: 35, left: 55 }

const chartWidth = computed(() => svgWidth - padding.left - padding.right)
const chartHeight = computed(() => svgHeight - padding.top - padding.bottom)

const yBounds = computed(() => {
  if (!trendData.value.points.length) {
    return { min: trendData.value.lowerTolerance, max: trendData.value.upperTolerance }
  }
  const vals = trendData.value.points.map(p => p.value)
  const dataMin = Math.min(...vals, trendData.value.lowerTolerance)
  const dataMax = Math.max(...vals, trendData.value.upperTolerance)
  const margin = (dataMax - dataMin) * 0.15 || 5
  return {
    min: Math.floor(dataMin - margin),
    max: Math.ceil(dataMax + margin)
  }
})

const getX = (index: number, total: number) => {
  if (total <= 1) return padding.left
  return padding.left + (index / (total - 1)) * chartWidth.value
}

const getY = (val: number) => {
  const { min, max } = yBounds.value
  if (max === min) return padding.top + chartHeight.value / 2
  const ratio = (val - min) / (max - min)
  return padding.top + (1 - ratio) * chartHeight.value
}

// Line and Area SVG paths
const svgPath = computed(() => {
  const pts = trendData.value.points
  if (!pts.length) return ''
  return pts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${getX(i, pts.length)} ${getY(p.value)}`).join(' ')
})

const svgAreaPath = computed(() => {
  const pts = trendData.value.points
  if (!pts.length) return ''
  const line = svgPath.value
  const firstX = getX(0, pts.length)
  const lastX = getX(pts.length - 1, pts.length)
  const bottomY = padding.top + chartHeight.value
  return `${line} L ${lastX} ${bottomY} L ${firstX} ${bottomY} Z`
})

// Summary statistics
const currentVal = computed(() => {
  const pts = trendData.value.points
  return pts.length ? pts[pts.length - 1].value : trendData.value.nominalValue
})

const meanVal = computed(() => {
  const pts = trendData.value.points
  if (!pts.length) return trendData.value.nominalValue
  const sum = pts.reduce((acc, p) => acc + p.value, 0)
  return Math.round((sum / pts.length) * 100) / 100
})
</script>

<template>
  <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 shadow-sm space-y-6">
    <!-- Header Controls: Machine Selector, Metric Switcher & Time Range -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
      <div class="space-y-1">
        <div class="flex items-center gap-2">
          <TrendingUp class="w-4 h-4 text-indigo-400" />
          <h3 class="text-base font-semibold text-slate-100">Predictive Telemetry Drift & Anomaly Tracker</h3>
        </div>
        <p class="text-xs text-slate-400">
          Rolling statistical analysis with Z-score outlier detection (|z| > 2.5σ warning, |z| > 3.0σ critical).
        </p>
      </div>

      <!-- Machine Selector & Time Window Tabs -->
      <div class="flex flex-wrap items-center gap-2">
        <select
          v-model="activeMachineId"
          class="px-3 py-1.5 bg-slate-950 border border-slate-800 rounded-lg text-xs font-medium text-slate-200 focus:outline-none focus:border-indigo-500"
        >
          <option v-for="m in machinesList" :key="m.id" :value="m.id">{{ m.name }}</option>
        </select>

        <div class="flex items-center bg-slate-950 p-1 rounded-lg border border-slate-800 text-xs font-medium">
          <button
            type="button"
            @click="activeRange = '1h'"
            :class="activeRange === '1h' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded transition-all"
          >
            1h
          </button>
          <button
            type="button"
            @click="activeRange = '8h'"
            :class="activeRange === '8h' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded transition-all"
          >
            8h (Shift)
          </button>
          <button
            type="button"
            @click="activeRange = '24h'"
            :class="activeRange === '24h' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded transition-all"
          >
            24h
          </button>
          <button
            type="button"
            @click="activeRange = '7d'"
            :class="activeRange === '7d' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded transition-all"
          >
            7d
          </button>
        </div>

        <button
          type="button"
          @click="fetchTrends"
          class="p-2 rounded-lg bg-slate-950 hover:bg-slate-800 border border-slate-800 text-slate-400 hover:text-slate-200 transition-colors"
          title="Refresh Timeseries Data"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="w-3.5 h-3.5" />
        </button>
      </div>
    </div>

    <!-- Metric Tabs (Built-in + User Defined) -->
    <div class="flex items-center gap-2 border-b border-slate-800 pb-3 overflow-x-auto text-xs font-medium">
      <button
        v-for="m in telemetryMetrics"
        :key="m.key"
        type="button"
        @click="activeMetric = m.key"
        :class="activeMetric === m.key ? 'bg-zinc-800 text-zinc-200 border-zinc-700 shadow-sm' : 'bg-slate-950 text-slate-400 border-slate-800 hover:text-slate-200'"
        class="px-3.5 py-1.5 rounded-lg border transition-all shrink-0 flex items-center gap-1.5 cursor-pointer"
      >
        <span>{{ m.name }} ({{ m.unit }})</span>
        <Badge v-if="m.isUserDefined" variant="outline" class="text-[9px] px-1 py-0 border-indigo-500/30 text-indigo-400">
          Custom
        </Badge>
      </button>
    </div>

    <!-- Interactive SVG Chart Canvas -->
    <div class="relative bg-slate-950 rounded-xl p-3 border border-slate-800 overflow-hidden">
      <svg
        viewBox="0 0 800 260"
        class="w-full h-64 overflow-visible select-none"
      >
        <defs>
          <linearGradient id="trendGradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#57715b" stop-opacity="0.3" />
            <stop offset="100%" stop-color="#57715b" stop-opacity="0.0" />
          </linearGradient>
        </defs>

        <!-- Grid Lines -->
        <g stroke="#232730" stroke-width="1">
          <line :x1="padding.left" :y1="padding.top" :x2="svgWidth - padding.right" :y2="padding.top" />
          <line :x1="padding.left" :y1="padding.top + chartHeight * 0.25" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight * 0.25" />
          <line :x1="padding.left" :y1="padding.top + chartHeight * 0.5" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight * 0.5" />
          <line :x1="padding.left" :y1="padding.top + chartHeight * 0.75" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight * 0.75" />
          <line :x1="padding.left" :y1="padding.top + chartHeight" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight" />
        </g>

        <!-- Nominal Tolerance Bands (Upper & Lower Limits) -->
        <g stroke="#f59e0b" stroke-width="1" stroke-dasharray="4,4" opacity="0.6">
          <line :x1="padding.left" :y1="getY(trendData.upperTolerance)" :x2="svgWidth - padding.right" :y2="getY(trendData.upperTolerance)" />
          <line :x1="padding.left" :y1="getY(trendData.lowerTolerance)" :x2="svgWidth - padding.right" :y2="getY(trendData.lowerTolerance)" />
        </g>

        <!-- Tolerance Labels -->
        <text :x="svgWidth - padding.right + 4" :y="getY(trendData.upperTolerance) + 3" fill="#f59e0b" font-size="9" font-family="monospace">
          MAX {{ trendData.upperTolerance }}
        </text>
        <text :x="svgWidth - padding.right + 4" :y="getY(trendData.lowerTolerance) + 3" fill="#f59e0b" font-size="9" font-family="monospace">
          MIN {{ trendData.lowerTolerance }}
        </text>

        <!-- Y-Axis Ticks -->
        <g fill="#828a94" font-size="9" font-family="monospace" text-anchor="end">
          <text :x="padding.left - 8" :y="padding.top + 4">{{ yBounds.max }}</text>
          <text :x="padding.left - 8" :y="padding.top + chartHeight / 2 + 3">{{ Math.round((yBounds.max + yBounds.min) / 2) }}</text>
          <text :x="padding.left - 8" :y="padding.top + chartHeight">{{ yBounds.min }}</text>
        </g>

        <!-- Shaded Trend Area -->
        <path :d="svgAreaPath" fill="url(#trendGradient)" />

        <!-- Line Trend Path -->
        <path :d="svgPath" fill="none" stroke="#768f79" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />

        <!-- Telemetry Points & Outlier Markers -->
        <g v-for="(p, i) in trendData.points" :key="i">
          <!-- Normal small point -->
          <circle
            v-if="!p.isAnomaly"
            :cx="getX(i, trendData.points.length)"
            :cy="getY(p.value)"
            r="2.5"
            fill="#768f79"
            class="hover:r-4 transition-all cursor-pointer"
            @mouseenter="hoveredPoint = p"
            @mouseleave="hoveredPoint = null"
          />

          <!-- Anomaly Outlier Pulsing Marker -->
          <g v-else>
            <circle
              :cx="getX(i, trendData.points.length)"
              :cy="getY(p.value)"
              r="6"
              fill="#f43f5e"
              opacity="0.3"
              class="animate-ping"
            />
            <circle
              :cx="getX(i, trendData.points.length)"
              :cy="getY(p.value)"
              r="4"
              fill="#f43f5e"
              stroke="#ffffff"
              stroke-width="1.5"
              class="cursor-pointer"
              @mouseenter="hoveredPoint = p"
              @mouseleave="hoveredPoint = null"
            />
          </g>
        </g>
      </svg>

      <!-- Hover Tooltip -->
      <div
        v-if="hoveredPoint"
        class="absolute top-4 right-4 p-3 rounded-lg bg-slate-900 border border-slate-700 text-xs shadow-xl space-y-1 backdrop-blur-md"
      >
        <div class="flex items-center justify-between gap-3 text-slate-400 font-mono text-[10px]">
          <span>{{ new Date(hoveredPoint.timestamp).toLocaleTimeString() }}</span>
          <Badge
            v-if="hoveredPoint.isAnomaly"
            variant="destructive"
            class="text-[9px] px-1 py-0 h-4"
          >
            Anomaly ({{ hoveredPoint.zScore }}σ)
          </Badge>
        </div>
        <div class="font-mono text-base font-bold text-white">
          {{ hoveredPoint.value }} <span class="text-xs font-normal text-slate-400">{{ trendData.unit }}</span>
        </div>
      </div>
    </div>

    <!-- Summary Metrics Bar & Detected Anomalies -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-3 pt-2 text-xs font-mono">
      <div class="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-0.5">
        <span class="text-slate-400 block text-[11px]">Current Value</span>
        <span class="text-slate-100 font-bold text-sm">{{ currentVal }} {{ trendData.unit }}</span>
      </div>
      <div class="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-0.5">
        <span class="text-slate-400 block text-[11px]">Period Mean (μ)</span>
        <span class="text-slate-100 font-bold text-sm">{{ meanVal }} {{ trendData.unit }}</span>
      </div>
      <div class="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-0.5">
        <span class="text-slate-400 block text-[11px]">Nominal Target</span>
        <span class="text-slate-100 font-bold text-sm">{{ trendData.nominalValue }} {{ trendData.unit }}</span>
      </div>
      <div class="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-0.5">
        <span class="text-slate-400 block text-[11px]">Detected Outliers</span>
        <span
          :class="trendData.detectedAnomalies.length > 0 ? 'text-rose-400' : 'text-emerald-400'"
          class="font-bold text-sm"
        >
          {{ trendData.detectedAnomalies.length }} Anomalies
        </span>
      </div>
    </div>

    <!-- Active Degradation Alerts List (if any detected) -->
    <div
      v-if="trendData.detectedAnomalies.length > 0"
      class="p-4 rounded-xl bg-amber-950/20 border border-amber-500/30 space-y-2 text-xs"
    >
      <div class="flex items-center gap-2 text-amber-300 font-semibold">
        <ShieldAlert class="w-4 h-4 text-amber-400" />
        <span>Statistical Degradation Alert Triggered</span>
      </div>
      <p
        v-for="(a, idx) in trendData.detectedAnomalies.slice(0, 2)"
        :key="idx"
        class="text-amber-200/90 font-mono text-[11px]"
      >
        • {{ a.description }} (Value: {{ a.value }} {{ trendData.unit }} vs Expected: {{ a.expectedValue }} {{ trendData.unit }})
      </p>
    </div>
  </div>
</template>
