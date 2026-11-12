<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Badge } from '~/components/ui/badge'
import { Card } from '~/components/ui/card'
import {
  TrendingUp,
  AlertTriangle,
  Clock,
  Sliders,
  Maximize2,
  RefreshCw,
  HelpCircle,
  ShieldAlert,
  Wrench,
  CheckCircle2,
  Activity
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

/** Mirrors the backend `RollingTrendsDto` (flat regression fields). */
export interface TrendStatistics {
  linearRegressionSlope: number
  linearRegressionIntercept: number
  r2Score: number
  movingAverageWindow: number
  movingAverages: number[]
  upperStdDevCorridor: number[]
  lowerStdDevCorridor: number[]
  trendDirection: 'Stable' | 'DriftingUp' | 'DriftingDown' | 'Volatile'
}

export interface TrendMetricOption {
  key: string
  name: string
  unit: string
  sampleCount: number
}

/** Mirrors the backend `MachineTrendSeriesDto` (GET /api/analytics/trends). */
export interface TrendSeriesData {
  machineId: string
  metricName: string
  metricKey?: string
  unit: string
  /** Target the station itself reports for the metric; null when it reports none. */
  nominalValue: number | null
  upperTolerance: number | null
  lowerTolerance: number | null
  points: TelemetryPoint[]
  detectedAnomalies: AnomalyDetail[]
  statisticalTrends?: TrendStatistics | null
  /** Samples the backend keeps per station metric, and how many of them exist right now. */
  bufferCapacity?: number
  bufferedSampleCount?: number
  bufferedFrom?: string | null
  bufferedTo?: string | null
  /** Station metrics that currently have buffered samples. */
  availableMetrics?: TrendMetricOption[]
}

const props = defineProps<{
  initialMachineId?: string
}>()

const activeMachineId = ref<string>(props.initialMachineId || '')
const activeMetric = ref<string>('cycle_time_ms')
// The history is an in-memory buffer of the last samples per metric, so only short windows exist.
const RANGES = [
  { key: '5m', label: '5 min' },
  { key: '15m', label: '15 min' },
  { key: 'all', label: 'All buffered' }
] as const
type TrendRange = typeof RANGES[number]['key']
const activeRange = ref<TrendRange>('all')
const showLinearTrend = ref(true)
const showCorridor = ref(true)
const showMovingAverage = ref(true)
const isLoading = ref(false)

const emptySeries = (): TrendSeriesData => ({
  machineId: '',
  metricName: '',
  unit: '',
  nominalValue: null,
  upperTolerance: null,
  lowerTolerance: null,
  points: [],
  detectedAnomalies: [],
  statisticalTrends: null,
  availableMetrics: []
})
const trendData = ref<TrendSeriesData>(emptySeries())
const hasLoaded = ref(false)
const loadError = ref<string | null>(null)

const hoveredPoint = ref<TelemetryPoint | null>(null)

// Real machines from the tenant-scoped machine registry (no hardcoded demo ids).
const machinesList = ref<{ id: string; name: string }[]>([])
const loadMachines = async () => {
  try {
    const res = await $fetch<any>('/api/machines')
    const list: any[] = Array.isArray(res) ? res : (res?.machines ?? res?.items ?? [])
    machinesList.value = list
      .filter(m => m?.id && m.status !== 'decommissioned')
      .map(m => ({ id: String(m.id), name: [m.customIdentifier, m.displayName || m.name].filter(Boolean).join(' · ') }))
    if (!activeMachineId.value && machinesList.value.length > 0) activeMachineId.value = machinesList.value[0]!.id
  } catch {
    machinesList.value = []
  }
}

const fetchTrends = async () => {
  if (!activeMachineId.value) return
  isLoading.value = true
  try {
    const data = await $fetch<TrendSeriesData>(`/api/analytics/trends`, {
      params: {
        machineId: activeMachineId.value,
        metric: activeMetric.value,
        range: activeRange.value
      }
    })
    if (!data || !Array.isArray(data.points)) throw new Error('Unexpected trend response')
    trendData.value = { ...emptySeries(), ...data }
    loadError.value = null
    // The station may not report the selected metric: move to one it does report.
    const available = data.availableMetrics ?? []
    if (available.length > 0 && !available.some(m => m.key === activeMetric.value)) {
      activeMetric.value = available[0]!.key
    }
  } catch (err: any) {
    trendData.value = { ...emptySeries(), machineId: activeMachineId.value }
    loadError.value = err?.data?.message || err?.statusMessage || err?.message || 'Trend data is unavailable'
  } finally {
    hasLoaded.value = true
    isLoading.value = false
  }
}

watch([activeMachineId, activeMetric, activeRange], () => {
  fetchTrends()
})

onMounted(async () => {
  await loadMachines()
  fetchTrends()
})

const metricTabs = computed<TrendMetricOption[]>(() => trendData.value.availableMetrics ?? [])
const hasPoints = computed(() => trendData.value.points.length > 0)
const limits = computed(() =>
  [trendData.value.nominalValue, trendData.value.upperTolerance, trendData.value.lowerTolerance]
    .filter((v): v is number => typeof v === 'number' && Number.isFinite(v))
)
const timeText = (iso?: string | null) => {
  const at = iso ? new Date(iso) : null
  return at && !Number.isNaN(at.getTime()) ? at.toLocaleTimeString() : null
}
const coverageText = computed(() => {
  const d = trendData.value
  if (!d.bufferedSampleCount) return null
  const from = timeText(d.bufferedFrom)
  const to = timeText(d.bufferedTo)
  return `${d.points.length} of ${d.bufferedSampleCount} buffered samples shown${from && to ? ` · buffer covers ${from} – ${to}` : ''}`
})

// SVG Coordinates calculation
const svgWidth = 800
const svgHeight = 260
const padding = { top: 25, right: 30, bottom: 35, left: 55 }

const chartWidth = computed(() => svgWidth - padding.left - padding.right)
const chartHeight = computed(() => svgHeight - padding.top - padding.bottom)

const yBounds = computed(() => {
  if (!trendData.value.points.length) {
    return { min: 0, max: 1 }
  }
  const vals = trendData.value.points.map(p => p.value)
  const dataMin = Math.min(...vals, ...limits.value)
  const dataMax = Math.max(...vals, ...limits.value)
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

// Statistical Moving Average Path
const movingAveragePath = computed(() => {
  const mas = trendData.value.statisticalTrends?.movingAverages
  if (!mas || !mas.length) return ''
  return mas.map((val, i) => `${i === 0 ? 'M' : 'L'} ${getX(i, mas.length)} ${getY(val)}`).join(' ')
})

// Standard Deviation Corridor Area Path
const corridorAreaPath = computed(() => {
  const upper = trendData.value.statisticalTrends?.upperStdDevCorridor
  const lower = trendData.value.statisticalTrends?.lowerStdDevCorridor
  if (!upper || !lower || !upper.length || !lower.length) return ''
  const upperPath = upper.map((v, i) => `${i === 0 ? 'M' : 'L'} ${getX(i, upper.length)} ${getY(v)}`).join(' ')
  const lowerPathReversed = lower.slice().reverse().map((v, i) => {
    const origIdx = lower.length - 1 - i
    return `L ${getX(origIdx, lower.length)} ${getY(v)}`
  }).join(' ')
  return `${upperPath} ${lowerPathReversed} Z`
})

// Linear Regression Trend Line
const regressionCoords = computed(() => {
  const reg = trendData.value.statisticalTrends
  const pts = trendData.value.points
  if (!reg || !pts.length) return null
  const yStart = reg.linearRegressionIntercept
  const yEnd = reg.linearRegressionIntercept + reg.linearRegressionSlope * (pts.length - 1)
  return {
    x1: getX(0, pts.length),
    y1: getY(yStart),
    x2: getX(pts.length - 1, pts.length),
    y2: getY(yEnd)
  }
})

// Summary statistics
// Null without samples: the summary then says "No data" instead of repeating a target.
const currentVal = computed<number | null>(() => {
  const pts = trendData.value.points
  return pts.length ? pts[pts.length - 1]!.value : null
})

const meanVal = computed<number | null>(() => {
  const pts = trendData.value.points
  if (!pts.length) return null
  const sum = pts.reduce((acc, p) => acc + p.value, 0)
  return Math.round((sum / pts.length) * 100) / 100
})
</script>

<template>
  <div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6">
    <!-- Header Controls: Machine Selector, Recipe Selector & Time Range -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
      <div class="space-y-1">
        <div class="flex items-center gap-2">
          <TrendingUp class="w-4 h-4 text-primary" />
          <h3 class="text-base font-semibold text-foreground">Metric Trends</h3>
          <Badge
            v-if="trendData.statisticalTrends"
            variant="outline"
            :class="{
              'border-emerald-500/30 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10': trendData.statisticalTrends.trendDirection === 'Stable',
              'border-amber-500/30 text-amber-600 dark:text-amber-400 bg-amber-500/10': trendData.statisticalTrends.trendDirection === 'DriftingUp' || trendData.statisticalTrends.trendDirection === 'DriftingDown',
              'border-rose-500/30 text-rose-600 dark:text-rose-400 bg-rose-500/10': trendData.statisticalTrends.trendDirection === 'Volatile'
            }"
            class="text-[10px] uppercase font-mono px-2 py-0.5"
          >
            {{ trendData.statisticalTrends.trendDirection }}
          </Badge>
        </div>
        <p class="text-xs text-muted-foreground">
          Recent samples of one station metric with linear regression, moving average and a ±1.5σ corridor.
          The backend keeps the last {{ trendData.bufferCapacity || 200 }} samples per metric in memory, so only a short, recent window exists.
        </p>
      </div>

      <!-- Machine Selector & Time Window Tabs -->
      <div class="flex flex-wrap items-center gap-2">
        <select
          v-model="activeMachineId"
          class="px-3 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
        >
          <option v-for="m in machinesList" :key="m.id" :value="m.id">{{ m.name }}</option>
        </select>

        <div class="flex items-center bg-muted/40 p-1 rounded-lg border border-border text-xs font-medium">
          <button
            v-for="range in RANGES"
            :key="range.key"
            type="button"
            @click="activeRange = range.key"
            :class="activeRange === range.key ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded transition-all cursor-pointer"
          >
            {{ range.label }}
          </button>
        </div>

        <button
          type="button"
          @click="fetchTrends"
          class="p-2 rounded-lg bg-background hover:bg-muted border border-border text-muted-foreground hover:text-foreground transition-colors cursor-pointer"
          title="Refresh Timeseries Data"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="w-3.5 h-3.5" />
        </button>
      </div>
    </div>

    <!-- Metric tabs: the metrics this station has buffered samples for -->
    <div class="flex items-center gap-2 border-b border-border pb-3 overflow-x-auto text-xs font-medium">
      <button
        v-for="m in metricTabs"
        :key="m.key"
        type="button"
        @click="activeMetric = m.key"
        :class="activeMetric === m.key ? 'bg-muted text-foreground border-border shadow-xs font-semibold' : 'bg-background text-muted-foreground border-border hover:text-foreground hover:bg-muted/40'"
        class="px-3.5 py-1.5 rounded-lg border transition-all shrink-0 flex items-center gap-1.5 cursor-pointer"
      >
        <span>{{ m.name }}<template v-if="m.unit"> ({{ m.unit }})</template></span>
      </button>
      <span v-if="hasLoaded && metricTabs.length === 0" class="text-muted-foreground font-normal" data-testid="trend-no-metrics">
        {{ loadError ? `Trend data could not be loaded: ${loadError}.` : 'This station has no buffered telemetry samples.' }}
      </span>
    </div>

    <!-- Statistical Overlays Toggle Bar -->
    <div class="flex flex-wrap items-center justify-between gap-3 text-xs">
      <div class="flex items-center gap-3">
        <label class="flex items-center gap-1.5 cursor-pointer select-none text-muted-foreground hover:text-foreground">
          <input type="checkbox" v-model="showLinearTrend" class="rounded border-border text-primary focus:ring-ring" />
          <span>Linear Regression</span>
        </label>
        <label class="flex items-center gap-1.5 cursor-pointer select-none text-muted-foreground hover:text-foreground">
          <input type="checkbox" v-model="showMovingAverage" class="rounded border-border text-primary focus:ring-ring" />
          <span>Moving Average (5 pt)</span>
        </label>
        <label class="flex items-center gap-1.5 cursor-pointer select-none text-muted-foreground hover:text-foreground">
          <input type="checkbox" v-model="showCorridor" class="rounded border-border text-primary focus:ring-ring" />
          <span>Std Dev Corridor (±1.5σ)</span>
        </label>
      </div>

      <div class="text-[11px] font-mono text-muted-foreground flex items-center gap-3 flex-wrap">
        <span v-if="coverageText" data-testid="trend-coverage">{{ coverageText }}</span>
        <template v-if="trendData.statisticalTrends">
          <span>Slope: {{ trendData.statisticalTrends.linearRegressionSlope > 0 ? '+' : '' }}{{ trendData.statisticalTrends.linearRegressionSlope }}/sample</span>
          <span>R²: {{ trendData.statisticalTrends.r2Score }}</span>
        </template>
      </div>
    </div>

    <!-- Interactive SVG Chart Canvas -->
    <div class="relative bg-muted/20 rounded-xl p-3 border border-border overflow-hidden">
      <svg
        viewBox="0 0 800 260"
        class="w-full h-64 overflow-visible select-none"
      >
        <defs>
          <linearGradient id="trendGradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#57715b" stop-opacity="0.3" />
            <stop offset="100%" stop-color="#57715b" stop-opacity="0.0" />
          </linearGradient>
          <linearGradient id="corridorGradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#6366f1" stop-opacity="0.15" />
            <stop offset="100%" stop-color="#6366f1" stop-opacity="0.05" />
          </linearGradient>
        </defs>

        <!-- Grid Lines -->
        <g stroke="currentColor" class="text-border" stroke-width="1">
          <line :x1="padding.left" :y1="padding.top" :x2="svgWidth - padding.right" :y2="padding.top" />
          <line :x1="padding.left" :y1="padding.top + chartHeight * 0.25" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight * 0.25" />
          <line :x1="padding.left" :y1="padding.top + chartHeight * 0.5" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight * 0.5" />
          <line :x1="padding.left" :y1="padding.top + chartHeight * 0.75" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight * 0.75" />
          <line :x1="padding.left" :y1="padding.top + chartHeight" :x2="svgWidth - padding.right" :y2="padding.top + chartHeight" />
        </g>

        <!-- Standard Deviation Corridor (±1.5σ) -->
        <path
          v-if="showCorridor && corridorAreaPath"
          :d="corridorAreaPath"
          fill="url(#corridorGradient)"
          stroke="#818cf8"
          stroke-width="0.8"
          stroke-dasharray="2,2"
          opacity="0.8"
        />

        <!-- Target reported by the station itself (drawn only when it reports one) -->
        <g v-if="hasPoints && trendData.nominalValue != null">
          <line :x1="padding.left" :y1="getY(trendData.nominalValue)" :x2="svgWidth - padding.right" :y2="getY(trendData.nominalValue)" stroke="#f59e0b" stroke-width="1" opacity="0.7" />
          <text :x="svgWidth - padding.right - 4" :y="getY(trendData.nominalValue) - 4" fill="#f59e0b" font-size="9" font-family="monospace" text-anchor="end">
            STATION TARGET {{ trendData.nominalValue }}
          </text>
        </g>

        <!-- Y-Axis Ticks -->
        <g v-if="hasPoints" fill="currentColor" class="text-muted-foreground" font-size="9" font-family="monospace" text-anchor="end">
          <text :x="padding.left - 8" :y="padding.top + 4">{{ yBounds.max }}</text>
          <text :x="padding.left - 8" :y="padding.top + chartHeight / 2 + 3">{{ Math.round((yBounds.max + yBounds.min) / 2) }}</text>
          <text :x="padding.left - 8" :y="padding.top + chartHeight">{{ yBounds.min }}</text>
        </g>

        <!-- Shaded Raw Telemetry Trend Area -->
        <path :d="svgAreaPath" fill="url(#trendGradient)" />

        <!-- Raw Telemetry Line Path -->
        <path :d="svgPath" fill="none" stroke="#768f79" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />

        <!-- Rolling Moving Average Line (5 pt) -->
        <path
          v-if="showMovingAverage && movingAveragePath"
          :d="movingAveragePath"
          fill="none"
          stroke="#06b6d4"
          stroke-width="1.8"
          stroke-linecap="round"
        />

        <!-- Linear Regression Trend Line -->
        <line
          v-if="showLinearTrend && regressionCoords"
          :x1="regressionCoords.x1"
          :y1="regressionCoords.y1"
          :x2="regressionCoords.x2"
          :y2="regressionCoords.y2"
          stroke="#ec4899"
          stroke-width="2"
          stroke-dasharray="5,5"
        />

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

      <div
        v-if="hasLoaded && !hasPoints"
        class="absolute inset-0 flex items-center justify-center text-xs text-muted-foreground text-center px-6"
        data-testid="trend-no-data"
      >
        {{ trendData.bufferedSampleCount
          ? 'No sample of this metric falls inside the selected window.'
          : 'No samples are buffered for this station and metric.' }}
      </div>

      <!-- Hover Tooltip -->
      <div
        v-if="hoveredPoint"
        class="absolute top-4 right-4 p-3 rounded-lg bg-popover/95 border border-border text-popover-foreground text-xs shadow-xl space-y-1 backdrop-blur-md"
      >
        <div class="flex items-center justify-between gap-3 text-muted-foreground font-mono text-[10px]">
          <span>{{ new Date(hoveredPoint.timestamp).toLocaleTimeString() }}</span>
          <Badge
            v-if="hoveredPoint.isAnomaly"
            variant="destructive"
            class="text-[9px] px-1 py-0 h-4"
          >
            Anomaly ({{ hoveredPoint.zScore }}σ)
          </Badge>
        </div>
        <div class="font-mono text-base font-bold text-foreground">
          {{ hoveredPoint.value }} <span class="text-xs font-normal text-muted-foreground">{{ trendData.unit }}</span>
        </div>
      </div>
    </div>

    <!-- Summary Metrics Bar & Detected Anomalies -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-3 pt-2 text-xs font-mono">
      <div class="p-3 rounded-lg bg-muted/30 border border-border space-y-0.5">
        <span class="text-muted-foreground block text-[11px]">Latest sample</span>
        <span v-if="currentVal !== null" class="text-foreground font-bold text-sm">{{ currentVal }} {{ trendData.unit }}</span>
        <span v-else class="text-muted-foreground text-sm">No data</span>
      </div>
      <div class="p-3 rounded-lg bg-muted/30 border border-border space-y-0.5">
        <span class="text-muted-foreground block text-[11px]">Mean of shown samples (μ)</span>
        <span v-if="meanVal !== null" class="text-foreground font-bold text-sm">{{ meanVal }} {{ trendData.unit }}</span>
        <span v-else class="text-muted-foreground text-sm">No data</span>
      </div>
      <div class="p-3 rounded-lg bg-muted/30 border border-border space-y-0.5">
        <span class="text-muted-foreground block text-[11px]">Station target</span>
        <span v-if="trendData.nominalValue != null" class="text-foreground font-bold text-sm">{{ trendData.nominalValue }} {{ trendData.unit }}</span>
        <span v-else class="text-muted-foreground text-sm">Not reported</span>
      </div>
      <div class="p-3 rounded-lg bg-muted/30 border border-border space-y-0.5">
        <span class="text-muted-foreground block text-[11px]">Outliers (|z| &gt; 2.5)</span>
        <span v-if="hasPoints" class="text-foreground font-bold text-sm">{{ trendData.detectedAnomalies.length }} in {{ trendData.points.length }} samples</span>
        <span v-else class="text-muted-foreground text-sm">No data</span>
      </div>
    </div>

    <!-- Active Degradation Alerts List (if any detected) -->
    <div
      v-if="trendData.detectedAnomalies.length > 0"
      class="p-4 rounded-xl bg-amber-500/10 border border-amber-500/30 space-y-2 text-xs"
    >
      <div class="flex items-center gap-2 text-amber-800 dark:text-amber-300 font-semibold">
        <ShieldAlert class="w-4 h-4 text-amber-600 dark:text-amber-400" />
        <span>Samples far from the mean of the shown window</span>
      </div>
      <p
        v-for="(a, idx) in trendData.detectedAnomalies.slice(0, 2)"
        :key="idx"
        class="text-amber-800/90 dark:text-amber-200/90 font-mono text-[11px]"
      >
        • {{ a.description }} (Value: {{ a.value }} {{ trendData.unit }} vs Expected: {{ a.expectedValue }} {{ trendData.unit }})
      </p>
    </div>
  </div>
</template>
