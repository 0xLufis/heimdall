<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Badge } from '~/components/ui/badge'
import { Card } from '~/components/ui/card'
import { useTelemetryMetrics } from '~/composables/useTelemetryMetrics'
import type { RollingTrends, RecipeForecast, RecipeItem } from '~/types/telemetryMetrics'
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
  statisticalTrends?: RollingTrends
  recipeForecast?: RecipeForecast
  availableRecipes?: RecipeItem[]
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

const activeMachineId = ref<string>(props.initialMachineId || '')
const activeMetric = ref<string>('cycle_time')
const activeRange = ref<'1h' | '8h' | '24h' | '7d'>('8h')
const activeRecipeId = ref<string>('')
const showLinearTrend = ref(true)
const showCorridor = ref(true)
const showMovingAverage = ref(true)
const isLoading = ref(false)

const trendData = ref<TrendSeriesData>({
  machineId: '',
  metricName: 'Cycle Time Deviation',
  unit: 'ms',
  nominalValue: 1200.0,
  upperTolerance: 1280.0,
  lowerTolerance: 1120.0,
  points: [],
  detectedAnomalies: [],
  availableRecipes: []
})

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
        range: activeRange.value,
        recipeId: activeRecipeId.value || undefined
      }
    })
    if (data && data.points) {
      trendData.value = data
    }
  } catch {
    trendData.value = { ...trendData.value, machineId: activeMachineId.value, points: [], detectedAnomalies: [] }
  } finally {
    isLoading.value = false
  }
}

watch([activeMachineId, activeMetric, activeRange, activeRecipeId], () => {
  fetchTrends()
})

onMounted(async () => {
  await Promise.all([fetchTelemetryMetrics(), loadMachines()])
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
  const reg = trendData.value.statisticalTrends?.linearRegression
  const pts = trendData.value.points
  if (!reg || !pts.length) return null
  const yStart = reg.intercept
  const yEnd = reg.intercept + reg.slope * (pts.length - 1)
  return {
    x1: getX(0, pts.length),
    y1: getY(yStart),
    x2: getX(pts.length - 1, pts.length),
    y2: getY(yEnd)
  }
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
  <div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6">
    <!-- Header Controls: Machine Selector, Recipe Selector & Time Range -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
      <div class="space-y-1">
        <div class="flex items-center gap-2">
          <TrendingUp class="w-4 h-4 text-primary" />
          <h3 class="text-base font-semibold text-foreground">Metric Trends & Statistical Forecast</h3>
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
          Linear regression drift analysis, standard deviation corridors (±1.5σ), and recipe tool wear forecasting.
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

        <select
          v-model="activeRecipeId"
          class="px-3 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          title="Product Recipe Baseline Envelope"
        >
          <option
            v-for="r in trendData.availableRecipes || []"
            :key="r.recipeId"
            :value="r.recipeId"
          >
            {{ r.recipeName }}
          </option>
        </select>

        <div class="flex items-center bg-muted/40 p-1 rounded-lg border border-border text-xs font-medium">
          <button
            type="button"
            @click="activeRange = '1h'"
            :class="activeRange === '1h' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded transition-all cursor-pointer"
          >
            1h
          </button>
          <button
            type="button"
            @click="activeRange = '8h'"
            :class="activeRange === '8h' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded transition-all cursor-pointer"
          >
            8h
          </button>
          <button
            type="button"
            @click="activeRange = '24h'"
            :class="activeRange === '24h' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded transition-all cursor-pointer"
          >
            24h
          </button>
          <button
            type="button"
            @click="activeRange = '7d'"
            :class="activeRange === '7d' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded transition-all cursor-pointer"
          >
            7d
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

    <!-- Recipe-Based Forecast Banner -->
    <div
      v-if="trendData.recipeForecast"
      class="p-4 rounded-xl border transition-all"
      :class="{
        'bg-emerald-500/10 border-emerald-500/30 text-emerald-900 dark:text-emerald-200': trendData.recipeForecast.toolWearStatus === 'Nominal',
        'bg-amber-500/10 border-amber-500/30 text-amber-900 dark:text-amber-200': trendData.recipeForecast.toolWearStatus === 'Warning',
        'bg-rose-500/10 border-rose-500/30 text-rose-900 dark:text-rose-200': trendData.recipeForecast.toolWearStatus === 'CriticalImpendingFailure'
      }"
    >
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
        <div class="flex items-start gap-3">
          <div class="p-2 rounded-lg bg-background/60 border border-current/20 shrink-0">
            <Wrench class="w-4 h-4" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="font-bold text-xs uppercase tracking-wide">Recipe Forecast: {{ trendData.recipeForecast.recipeName }}</span>
              <Badge
                variant="outline"
                class="text-[10px] px-1.5 py-0 uppercase"
                :class="{
                  'border-emerald-500 text-emerald-700 dark:text-emerald-300': trendData.recipeForecast.toolWearStatus === 'Nominal',
                  'border-amber-500 text-amber-700 dark:text-amber-300': trendData.recipeForecast.toolWearStatus === 'Warning',
                  'border-rose-500 text-rose-700 dark:text-rose-300': trendData.recipeForecast.toolWearStatus === 'CriticalImpendingFailure'
                }"
              >
                {{ trendData.recipeForecast.toolWearStatus }}
              </Badge>
            </div>
            <p class="text-xs opacity-90 mt-0.5 font-mono">
              {{ trendData.recipeForecast.recommendedAction }}
            </p>
          </div>
        </div>

        <div class="flex items-center gap-4 shrink-0 text-xs font-mono">
          <div>
            <span class="block opacity-75 text-[10px]">Drift Rate</span>
            <span class="font-bold">{{ trendData.recipeForecast.driftPercentage > 0 ? '+' : '' }}{{ trendData.recipeForecast.driftPercentage }}%</span>
          </div>
          <div class="border-l border-current/20 pl-4">
            <span class="block opacity-75 text-[10px]">Cycles Until Failure</span>
            <span class="font-bold">{{ trendData.recipeForecast.forecastCyclesUntilFailure }} cycles</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Metric Tabs (Built-in + User Defined) -->
    <div class="flex items-center gap-2 border-b border-border pb-3 overflow-x-auto text-xs font-medium">
      <button
        v-for="m in telemetryMetrics"
        :key="m.key"
        type="button"
        @click="activeMetric = m.key"
        :class="activeMetric === m.key ? 'bg-muted text-foreground border-border shadow-xs font-semibold' : 'bg-background text-muted-foreground border-border hover:text-foreground hover:bg-muted/40'"
        class="px-3.5 py-1.5 rounded-lg border transition-all shrink-0 flex items-center gap-1.5 cursor-pointer"
      >
        <span>{{ m.name }} ({{ m.unit }})</span>
        <Badge v-if="m.isUserDefined" variant="outline" class="text-[9px] px-1 py-0 border-primary/30 text-primary">
          Custom
        </Badge>
      </button>
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

      <div v-if="trendData.statisticalTrends?.linearRegression" class="text-[11px] font-mono text-muted-foreground flex items-center gap-2">
        <span>Slope: {{ trendData.statisticalTrends.linearRegression.slope > 0 ? '+' : '' }}{{ trendData.statisticalTrends.linearRegression.slope }}/pt</span>
        <span>R²: {{ trendData.statisticalTrends.linearRegression.r2 }}</span>
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
        <g fill="currentColor" class="text-muted-foreground" font-size="9" font-family="monospace" text-anchor="end">
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
        <span class="text-muted-foreground block text-[11px]">Current Value</span>
        <span class="text-foreground font-bold text-sm">{{ currentVal }} {{ trendData.unit }}</span>
      </div>
      <div class="p-3 rounded-lg bg-muted/30 border border-border space-y-0.5">
        <span class="text-muted-foreground block text-[11px]">Period Mean (μ)</span>
        <span class="text-foreground font-bold text-sm">{{ meanVal }} {{ trendData.unit }}</span>
      </div>
      <div class="p-3 rounded-lg bg-muted/30 border border-border space-y-0.5">
        <span class="text-muted-foreground block text-[11px]">Nominal Target</span>
        <span class="text-foreground font-bold text-sm">{{ trendData.nominalValue }} {{ trendData.unit }}</span>
      </div>
      <div class="p-3 rounded-lg bg-muted/30 border border-border space-y-0.5">
        <span class="text-muted-foreground block text-[11px]">Statistical Outliers</span>
        <span
          :class="trendData.detectedAnomalies.length > 0 ? 'text-rose-600 dark:text-rose-400' : 'text-emerald-600 dark:text-emerald-400'"
          class="font-bold text-sm"
        >
          {{ trendData.detectedAnomalies.length }} Anomalies
        </span>
      </div>
    </div>

    <!-- Active Degradation Alerts List (if any detected) -->
    <div
      v-if="trendData.detectedAnomalies.length > 0"
      class="p-4 rounded-xl bg-amber-500/10 border border-amber-500/30 space-y-2 text-xs"
    >
      <div class="flex items-center gap-2 text-amber-800 dark:text-amber-300 font-semibold">
        <ShieldAlert class="w-4 h-4 text-amber-600 dark:text-amber-400" />
        <span>Statistical Degradation Alert Triggered</span>
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
