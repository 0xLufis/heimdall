<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import {
  Sliders,
  Pin,
  Trash2,
  Activity,
  BarChart3,
  TrendingUp,
  Target,
  Sparkles,
  Check
} from 'lucide-vue-next'

export interface PinnedKpiWidget {
  id: string
  title: string
  metric: string
  visualType: 'gauge' | 'trend' | 'bar' | 'bullet'
  targetLine: string
  interval: 'shift' | 'hourly' | 'daily'
  currentValue: number
  targetValue: number
  unit: string
}

const defaultWidgets: PinnedKpiWidget[] = [
  {
    id: 'kpi-1',
    title: 'Plant Master OEE Index',
    metric: 'oee',
    visualType: 'gauge',
    targetLine: 'All Lines',
    interval: 'shift',
    currentValue: 90.8,
    targetValue: 85.0,
    unit: '%'
  },
  {
    id: 'kpi-2',
    title: 'Pre-Assembly (L1) MTTR',
    metric: 'mttr',
    visualType: 'bullet',
    targetLine: 'Line 1 - Pre-Assembly',
    interval: 'hourly',
    currentValue: 28.0,
    targetValue: 30.0,
    unit: 'min'
  },
  {
    id: 'kpi-3',
    title: 'Robotics Welding Quality Rate',
    metric: 'quality',
    visualType: 'trend',
    targetLine: 'Line 5 - Robotic Welding Cell',
    interval: 'daily',
    currentValue: 99.1,
    targetValue: 98.5,
    unit: '%'
  }
]

const pinnedWidgets = ref<PinnedKpiWidget[]>([...defaultWidgets])

onMounted(() => {
  if (typeof window !== 'undefined') {
    const saved = localStorage.getItem('heimdall_pinned_kpis')
    if (saved) {
      try {
        pinnedWidgets.value = JSON.parse(saved)
      } catch {
        pinnedWidgets.value = defaultWidgets
      }
    } else {
      pinnedWidgets.value = defaultWidgets
      localStorage.setItem('heimdall_pinned_kpis', JSON.stringify(defaultWidgets))
    }
  }
})

// Builder Form State
const builderTitle = ref('Custom Line OEE')
const selectedMetric = ref<'oee' | 'availability' | 'performance' | 'quality' | 'mtbf' | 'mttr' | 'faults'>('oee')
const selectedVisual = ref<'gauge' | 'trend' | 'bar' | 'bullet'>('gauge')
const selectedLine = ref('Line 1 - Pre-Assembly')
const selectedInterval = ref<'shift' | 'hourly' | 'daily'>('shift')
const targetGoal = ref(92.0)
const isSaved = ref(false)

const metricMeta = computed(() => {
  switch (selectedMetric.value) {
    case 'availability':
      return { unit: '%', defaultVal: 96.5, defaultTarget: 95.0 }
    case 'performance':
      return { unit: '%', defaultVal: 93.8, defaultTarget: 92.0 }
    case 'quality':
      return { unit: '%', defaultVal: 99.2, defaultTarget: 98.5 }
    case 'mtbf':
      return { unit: 'hrs', defaultVal: 380, defaultTarget: 300 }
    case 'mttr':
      return { unit: 'min', defaultVal: 28.0, defaultTarget: 35.0 }
    case 'faults':
      return { unit: 'incidents/shift', defaultVal: 3.2, defaultTarget: 5.0 }
    case 'oee':
    default:
      return { unit: '%', defaultVal: 91.3, defaultTarget: 88.0 }
  }
})

const handlePinWidget = () => {
  const newWidget: PinnedKpiWidget = {
    id: `kpi-custom-${Date.now()}`,
    title: builderTitle.value || `${selectedMetric.value.toUpperCase()} Widget`,
    metric: selectedMetric.value,
    visualType: selectedVisual.value,
    targetLine: selectedLine.value,
    interval: selectedInterval.value,
    currentValue: metricMeta.value.defaultVal,
    targetValue: targetGoal.value || metricMeta.value.defaultTarget,
    unit: metricMeta.value.unit
  }

  pinnedWidgets.value.unshift(newWidget)
  if (typeof window !== 'undefined') {
    localStorage.setItem('heimdall_pinned_kpis', JSON.stringify(pinnedWidgets.value))
  }

  isSaved.value = true
  setTimeout(() => {
    isSaved.value = false
  }, 2000)
}

const handleRemoveWidget = (id: string) => {
  pinnedWidgets.value = pinnedWidgets.value.filter(w => w.id !== id)
  if (typeof window !== 'undefined') {
    localStorage.setItem('heimdall_pinned_kpis', JSON.stringify(pinnedWidgets.value))
  }
}
</script>

<template>
  <div class="space-y-8">
    <!-- Section 1: Interactive KPI Graph Builder Card -->
    <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 shadow-sm space-y-6">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Sliders class="w-4 h-4" />
          </div>
          <div>
            <h3 class="text-sm font-semibold text-slate-100">Custom KPI Widget & Chart Builder</h3>
            <p class="text-xs text-slate-400">Design, parameterize, and pin custom KPI analytics tiles to plant dashboards.</p>
          </div>
        </div>
        <Badge variant="outline" class="text-xs font-mono border-slate-700 bg-slate-950 text-slate-300">
          Role: Engineering Administrator / Controls Engineer
        </Badge>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Configuration Controls (7 cols) -->
        <div class="lg:col-span-7 space-y-4">
          <!-- Widget Title -->
          <div class="space-y-1.5">
            <label class="text-xs font-medium text-slate-300">Widget Title</label>
            <input
              v-model="builderTitle"
              type="text"
              class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              placeholder="e.g. Line 2 Screwing OEE Rate"
            />
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <!-- Target Metric -->
            <div class="space-y-1.5">
              <label class="text-xs font-medium text-slate-300">Target Metric</label>
              <select
                v-model="selectedMetric"
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              >
                <option value="oee">OEE (Overall Equipment Effectiveness)</option>
                <option value="availability">Plant Availability %</option>
                <option value="performance">Line Performance Rate %</option>
                <option value="quality">First-Pass Quality Yield %</option>
                <option value="mtbf">MTBF (Mean Time Between Failures)</option>
                <option value="mttr">MTTR (Mean Time to Repair)</option>
                <option value="faults">Fault Frequency Index</option>
              </select>
            </div>

            <!-- Target Production Line -->
            <div class="space-y-1.5">
              <label class="text-xs font-medium text-slate-300">Production Scope</label>
              <select
                v-model="selectedLine"
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              >
                <option value="All Lines">All Production Lines (Fleet-Wide)</option>
                <option value="Line 1 - Pre-Assembly">Line 1 - Pre-Assembly</option>
                <option value="Line 2 - Screwing & Fastening">Line 2 - Screwing & Fastening</option>
                <option value="Line 3 - Vision & Quality">Line 3 - Vision & Quality</option>
                <option value="Line 4 - Dispensing & Bonding">Line 4 - Dispensing & Bonding</option>
                <option value="Line 5 - Robotic Welding Cell">Line 5 - Robotic Welding Cell</option>
                <option value="Line 7 - High Voltage Battery">Line 7 - High Voltage Battery</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <!-- Visual Type -->
            <div class="space-y-1.5">
              <label class="text-xs font-medium text-slate-300">Chart Visualization Type</label>
              <select
                v-model="selectedVisual"
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              >
                <option value="gauge">Radial SVG Dial Gauge</option>
                <option value="bullet">Target vs Actual Bullet Chart</option>
                <option value="trend">Historical Area Trend Line</option>
                <option value="bar">Interval Comparison Bar Chart</option>
              </select>
            </div>

            <!-- Aggregation Window -->
            <div class="space-y-1.5">
              <label class="text-xs font-medium text-slate-300">Aggregation Interval</label>
              <select
                v-model="selectedInterval"
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              >
                <option value="shift">Per Shift (8 Hours)</option>
                <option value="hourly">Hourly Rolling</option>
                <option value="daily">Daily Plant Aggregate (24h)</option>
              </select>
            </div>
          </div>

          <div class="pt-3">
            <Button
              @click="handlePinWidget"
              class="w-full bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold py-2 rounded-lg flex items-center justify-center gap-2 transition-all shadow-md"
            >
              <Check v-if="isSaved" class="w-4 h-4 text-emerald-300" />
              <Pin v-else class="w-4 h-4" />
              <span>{{ isSaved ? 'Widget Pinned to Dashboard!' : 'Pin Custom KPI Widget' }}</span>
            </Button>
          </div>
        </div>

        <!-- Live Widget Preview Canvas (5 cols) -->
        <div class="lg:col-span-5 p-5 rounded-xl bg-slate-950 border border-slate-800 flex flex-col justify-between space-y-4">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span class="font-mono text-[11px] uppercase tracking-wider text-indigo-400">Live Widget Preview</span>
            <Badge variant="outline" class="text-[10px] border-slate-800 text-slate-400">{{ selectedInterval }}</Badge>
          </div>

          <div class="space-y-1">
            <h4 class="text-sm font-semibold text-white">{{ builderTitle || 'Custom Metric Tile' }}</h4>
            <span class="text-xs text-slate-400">{{ selectedLine }}</span>
          </div>

          <!-- Dynamic SVG Preview Based on Selected Visual Type -->
          <div class="h-32 flex items-center justify-center py-2">
            <!-- 1. GAUGE -->
            <div v-if="selectedVisual === 'gauge'" class="flex flex-col items-center justify-center">
              <div class="relative flex items-center justify-center">
                <svg class="w-24 h-24 transform -rotate-90">
                  <circle cx="48" cy="48" r="38" stroke="#1e293b" stroke-width="8" fill="none" />
                  <circle
                    cx="48"
                    cy="48"
                    r="38"
                    stroke="#6366f1"
                    stroke-width="8"
                    fill="none"
                    stroke-dasharray="238.7"
                    :stroke-dashoffset="238.7 - (238.7 * Math.min(metricMeta.defaultVal, 100)) / 100"
                    stroke-linecap="round"
                  />
                </svg>
                <div class="absolute text-center">
                  <span class="text-base font-bold font-mono text-white">{{ metricMeta.defaultVal }}</span>
                  <span class="text-[10px] text-slate-400 block -mt-1">{{ metricMeta.unit }}</span>
                </div>
              </div>
            </div>

            <!-- 2. BULLET / PROGRESS -->
            <div v-else-if="selectedVisual === 'bullet'" class="w-full space-y-3 px-4">
              <div class="flex justify-between text-xs font-mono">
                <span class="text-slate-400">Actual: {{ metricMeta.defaultVal }} {{ metricMeta.unit }}</span>
                <span class="text-amber-400">Goal: {{ targetGoal }} {{ metricMeta.unit }}</span>
              </div>
              <div class="w-full bg-slate-900 h-3 rounded-full overflow-hidden border border-slate-800 relative">
                <div
                  class="bg-indigo-500 h-full rounded-full transition-all"
                  :style="{ width: `${Math.min(metricMeta.defaultVal, 100)}%` }"
                ></div>
                <div
                  class="absolute top-0 bottom-0 w-1 bg-amber-400 z-10"
                  :style="{ left: `${Math.min(targetGoal, 100)}%` }"
                ></div>
              </div>
            </div>

            <!-- 3. TREND -->
            <div v-else-if="selectedVisual === 'trend'" class="w-full h-full flex items-center justify-center">
              <svg viewBox="0 0 200 80" class="w-full h-20">
                <path
                  d="M 10 60 Q 50 20, 100 45 T 190 15"
                  fill="none"
                  stroke="#6366f1"
                  stroke-width="3"
                  stroke-linecap="round"
                />
                <circle cx="190" cy="15" r="4" fill="#818cf8" />
              </svg>
            </div>

            <!-- 4. BAR -->
            <div v-else class="w-full h-full flex items-end justify-center gap-3 pb-2 px-6">
              <div class="w-7 bg-slate-800 rounded-t h-12"></div>
              <div class="w-7 bg-slate-800 rounded-t h-16"></div>
              <div class="w-7 bg-indigo-600 rounded-t h-20"></div>
              <div class="w-7 bg-indigo-500 rounded-t h-24"></div>
            </div>
          </div>

          <div class="text-[11px] text-slate-400 border-t border-slate-800/80 pt-2 flex justify-between font-mono">
            <span>Status: Normal Range</span>
            <span class="text-emerald-400">Target Compliant</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Section 2: Currently Pinned KPI Dashboard Grid -->
    <div class="space-y-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Pin class="w-4 h-4 text-indigo-400" />
          <h4 class="text-sm font-semibold text-slate-100">Pinned Production KPI Tiles ({{ pinnedWidgets.length }})</h4>
        </div>
        <span class="text-xs text-slate-400 font-mono">Live Real-Time OT Refresh</span>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <div
          v-for="widget in pinnedWidgets"
          :key="widget.id"
          class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-sm space-y-4 relative group"
        >
          <!-- Delete button -->
          <button
            type="button"
            @click="handleRemoveWidget(widget.id)"
            class="absolute top-4 right-4 p-1.5 rounded-md bg-slate-950 hover:bg-rose-950/80 text-slate-500 hover:text-rose-400 border border-slate-800 transition-colors opacity-0 group-hover:opacity-100"
            title="Unpin Widget"
          >
            <Trash2 class="w-3.5 h-3.5" />
          </button>

          <div class="space-y-1 pr-6">
            <h5 class="text-sm font-semibold text-slate-100">{{ widget.title }}</h5>
            <span class="text-xs text-slate-400 block truncate">{{ widget.targetLine }}</span>
          </div>

          <!-- Value & Target -->
          <div class="flex items-baseline justify-between font-mono">
            <div>
              <span class="text-3xl font-bold text-white">{{ widget.currentValue }}</span>
              <span class="text-xs font-normal text-slate-400 ml-1">{{ widget.unit }}</span>
            </div>
            <div class="text-right text-xs">
              <span class="text-slate-500 block">Goal</span>
              <span class="text-amber-400 font-medium">{{ widget.targetValue }} {{ widget.unit }}</span>
            </div>
          </div>

          <!-- Mini Indicator -->
          <div class="w-full bg-slate-950 h-2 rounded-full overflow-hidden border border-slate-800">
            <div
              class="bg-indigo-500 h-full rounded-full transition-all"
              :style="{ width: `${Math.min((widget.currentValue / (widget.targetValue || 1)) * 100, 100)}%` }"
            ></div>
          </div>

          <div class="flex items-center justify-between text-[11px] text-slate-400 font-mono pt-1">
            <span>Interval: {{ widget.interval }}</span>
            <Badge variant="outline" class="text-[10px] border-slate-700 bg-slate-950 text-emerald-400">
              Active
            </Badge>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
