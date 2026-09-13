<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
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
  Check,
  ShieldCheck,
  AlertTriangle
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
  upperLimit: number
  lowerLimit: number
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
    targetValue: 88.0,
    upperLimit: 96.0,
    lowerLimit: 82.0,
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
    upperLimit: 45.0,
    lowerLimit: 15.0,
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
    upperLimit: 100.0,
    lowerLimit: 97.0,
    unit: '%'
  }
]

const pinnedWidgets = ref<PinnedKpiWidget[]>([...defaultWidgets])

onMounted(() => {
  if (typeof window !== 'undefined') {
    const saved = localStorage.getItem('heimdall_pinned_kpis')
    if (saved) {
      try {
        const parsed = JSON.parse(saved)
        if (Array.isArray(parsed) && parsed.length > 0) {
          pinnedWidgets.value = parsed.map(w => ({
            ...w,
            upperLimit: w.upperLimit ?? (w.targetValue ? Math.round((w.targetValue * 1.08) * 10) / 10 : 100),
            lowerLimit: w.lowerLimit ?? (w.targetValue ? Math.round((w.targetValue * 0.92) * 10) / 10 : 0)
          }))
        } else {
          pinnedWidgets.value = defaultWidgets
        }
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

// SPC Threshold Goals
const targetMean = ref(88.0)
const upperControlLimit = ref(96.0)
const lowerControlLimit = ref(82.0)
const isSaved = ref(false)

const metricMeta = computed(() => {
  switch (selectedMetric.value) {
    case 'availability':
      return { unit: '%', defaultVal: 96.5, defaultTarget: 95.0, defaultUcl: 99.0, defaultLcl: 90.0 }
    case 'performance':
      return { unit: '%', defaultVal: 93.8, defaultTarget: 92.0, defaultUcl: 97.0, defaultLcl: 85.0 }
    case 'quality':
      return { unit: '%', defaultVal: 99.2, defaultTarget: 98.5, defaultUcl: 100.0, defaultLcl: 96.0 }
    case 'mtbf':
      return { unit: 'hrs', defaultVal: 380, defaultTarget: 300, defaultUcl: 500, defaultLcl: 200 }
    case 'mttr':
      return { unit: 'min', defaultVal: 28.0, defaultTarget: 30.0, defaultUcl: 45.0, defaultLcl: 15.0 }
    case 'faults':
      return { unit: 'incidents/shift', defaultVal: 3.2, defaultTarget: 5.0, defaultUcl: 8.0, defaultLcl: 1.0 }
    case 'oee':
    default:
      return { unit: '%', defaultVal: 91.3, defaultTarget: 88.0, defaultUcl: 96.0, defaultLcl: 82.0 }
  }
})

// Synchronize inputs when metric changes
watch(selectedMetric, () => {
  targetMean.value = metricMeta.value.defaultTarget
  upperControlLimit.value = metricMeta.value.defaultUcl
  lowerControlLimit.value = metricMeta.value.defaultLcl
}, { immediate: true })

const getSpcStatus = (val: number, ucl: number, lcl: number) => {
  if (ucl != null && val > ucl) {
    return { label: 'UCL Exceeded', class: 'text-rose-400 border-rose-500/30 bg-rose-950/30' }
  }
  if (lcl != null && val < lcl) {
    return { label: 'Below LCL', class: 'text-amber-400 border-amber-500/30 bg-amber-950/30' }
  }
  return { label: 'Within Limits', class: 'text-emerald-400 border-emerald-500/30 bg-emerald-950/30' }
}

const currentPreviewStatus = computed(() => {
  return getSpcStatus(metricMeta.value.defaultVal, upperControlLimit.value, lowerControlLimit.value)
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
    targetValue: targetMean.value,
    upperLimit: upperControlLimit.value,
    lowerLimit: lowerControlLimit.value,
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
          <div class="p-2 rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300">
            <Sliders class="w-4 h-4" />
          </div>
          <div>
            <h3 class="text-sm font-semibold text-slate-100">Custom KPI Widget & Chart Builder</h3>
            <p class="text-xs text-slate-400">Parameterize KPI metrics with statistical Upper/Lower Control Limits and Mean targets.</p>
          </div>
        </div>
        <Badge variant="outline" class="text-xs font-mono border-slate-700 bg-slate-950 text-slate-300">
          SPC / OT Analytics
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
              class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-zinc-500"
              placeholder="e.g. Line 2 Screwing OEE Rate"
            />
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <!-- Target Metric -->
            <div class="space-y-1.5">
              <label class="text-xs font-medium text-slate-300">Target Metric</label>
              <select
                v-model="selectedMetric"
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-zinc-500"
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
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-zinc-500"
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
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-zinc-500"
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
                class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-100 focus:outline-none focus:border-zinc-500"
              >
                <option value="shift">Per Shift (8 Hours)</option>
                <option value="hourly">Hourly Rolling</option>
                <option value="daily">Daily Plant Aggregate (24h)</option>
              </select>
            </div>
          </div>

          <!-- Statistical Process Control Limits (Mean Goal, UCL, LCL) -->
          <div class="p-3.5 rounded-xl bg-slate-950/70 border border-slate-800 space-y-3">
            <div class="flex items-center justify-between">
              <label class="text-xs font-semibold text-slate-200 flex items-center gap-1.5">
                <Target class="w-3.5 h-3.5 text-amber-400" />
                <span>KPI Goals & Statistical Process Control (SPC) Limits</span>
              </label>
              <span class="text-[10px] font-mono text-slate-400">Mean Target & Control Bands</span>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <!-- Mean Target Goal -->
              <div class="space-y-1">
                <div class="flex justify-between items-center text-[11px]">
                  <span class="text-slate-300 font-medium">Target / Mean</span>
                  <span class="font-mono text-amber-400">{{ targetMean }} {{ metricMeta.unit }}</span>
                </div>
                <input
                  v-model.number="targetMean"
                  type="number"
                  step="0.1"
                  class="w-full px-2.5 py-1.5 rounded-lg bg-slate-900 border border-slate-700 text-xs text-slate-100 font-mono focus:outline-none focus:border-amber-500"
                />
              </div>

              <!-- Upper Control Limit (UCL) -->
              <div class="space-y-1">
                <div class="flex justify-between items-center text-[11px]">
                  <span class="text-slate-300 font-medium">Upper Limit (UCL)</span>
                  <span class="font-mono text-rose-400">{{ upperControlLimit }} {{ metricMeta.unit }}</span>
                </div>
                <input
                  v-model.number="upperControlLimit"
                  type="number"
                  step="0.1"
                  class="w-full px-2.5 py-1.5 rounded-lg bg-slate-900 border border-slate-700 text-xs text-slate-100 font-mono focus:outline-none focus:border-rose-500"
                />
              </div>

              <!-- Lower Control Limit (LCL) -->
              <div class="space-y-1">
                <div class="flex justify-between items-center text-[11px]">
                  <span class="text-slate-300 font-medium">Lower Limit (LCL)</span>
                  <span class="font-mono text-amber-400">{{ lowerControlLimit }} {{ metricMeta.unit }}</span>
                </div>
                <input
                  v-model.number="lowerControlLimit"
                  type="number"
                  step="0.1"
                  class="w-full px-2.5 py-1.5 rounded-lg bg-slate-900 border border-slate-700 text-xs text-slate-100 font-mono focus:outline-none focus:border-amber-500"
                />
              </div>
            </div>
          </div>

          <div class="pt-2">
            <Button
              data-testid="pin-kpi-btn"
              @click="handlePinWidget"
              class="w-full bg-zinc-800 hover:bg-zinc-700 text-white text-xs font-semibold py-2 rounded-lg flex items-center justify-center gap-2 transition-all shadow-md"
            >
              <Check v-if="isSaved" class="w-4 h-4 text-emerald-300" />
              <Pin v-else class="w-4 h-4 text-zinc-300" />
              <span>{{ isSaved ? 'Widget Pinned to Dashboard!' : 'Pin Custom KPI Widget' }}</span>
            </Button>
          </div>
        </div>

        <!-- Live Widget Preview Canvas (5 cols) -->
        <div class="lg:col-span-5 p-5 rounded-xl bg-slate-950 border border-slate-800 flex flex-col justify-between space-y-4">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span class="font-mono text-[11px] uppercase tracking-wider text-zinc-300">Live Widget Preview</span>
            <Badge variant="outline" class="text-[10px] border-slate-800 text-slate-400">{{ selectedInterval }}</Badge>
          </div>

          <div class="space-y-1">
            <h4 class="text-sm font-semibold text-white">{{ builderTitle || 'Custom Metric Tile' }}</h4>
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span>{{ selectedLine }}</span>
              <span class="font-mono text-[11px] text-amber-400">Target: {{ targetMean }} {{ metricMeta.unit }}</span>
            </div>
          </div>

          <!-- Dynamic SVG Preview Based on Selected Visual Type -->
          <div class="h-32 flex items-center justify-center py-2">
            <!-- 1. GAUGE -->
            <div v-if="selectedVisual === 'gauge'" class="flex flex-col items-center justify-center">
              <div class="relative flex items-center justify-center">
                <svg class="w-24 h-24 transform -rotate-90">
                  <circle cx="48" cy="48" r="38" stroke="#232730" stroke-width="8" fill="none" />
                  <circle
                    cx="48"
                    cy="48"
                    r="38"
                    stroke="#57715b"
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
              <div class="flex items-center gap-3 text-[10px] font-mono mt-2 text-slate-400">
                <span class="text-amber-400">LCL: {{ lowerControlLimit }}</span>
                <span class="text-emerald-400">Mean: {{ targetMean }}</span>
                <span class="text-rose-400">UCL: {{ upperControlLimit }}</span>
              </div>
            </div>

            <!-- 2. BULLET / PROGRESS -->
            <div v-else-if="selectedVisual === 'bullet'" class="w-full space-y-3 px-4">
              <div class="flex justify-between text-xs font-mono">
                <span class="text-slate-300">Actual: {{ metricMeta.defaultVal }} {{ metricMeta.unit }}</span>
                <span class="text-amber-400">Goal: {{ targetMean }} {{ metricMeta.unit }}</span>
              </div>
              <div class="w-full bg-slate-900 h-4 rounded-full overflow-hidden border border-slate-800 relative">
                <!-- Safe control zone band -->
                <div
                  class="absolute top-0 bottom-0 bg-emerald-500/10 border-x border-emerald-500/20"
                  :style="{
                    left: `${Math.max(0, Math.min(lowerControlLimit, 100))}%`,
                    width: `${Math.max(0, Math.min(upperControlLimit - lowerControlLimit, 100))}%`
                  }"
                ></div>

                <!-- Actual bar -->
                <div
                  class="bg-zinc-500 h-full rounded-full transition-all opacity-80"
                  :style="{ width: `${Math.min(metricMeta.defaultVal, 100)}%` }"
                ></div>

                <!-- LCL Marker -->
                <div
                  class="absolute top-0 bottom-0 w-0.5 bg-amber-400/80 z-10"
                  :style="{ left: `${Math.min(lowerControlLimit, 100)}%` }"
                  title="Lower Control Limit"
                ></div>

                <!-- Target Mean Marker -->
                <div
                  class="absolute top-0 bottom-0 w-1 bg-amber-400 z-20 shadow-[0_0_6px_rgba(251,191,36,0.6)]"
                  :style="{ left: `${Math.min(targetMean, 100)}%` }"
                  title="Target Mean Goal"
                ></div>

                <!-- UCL Marker -->
                <div
                  class="absolute top-0 bottom-0 w-0.5 bg-rose-400/80 z-10"
                  :style="{ left: `${Math.min(upperControlLimit, 100)}%` }"
                  title="Upper Control Limit"
                ></div>
              </div>
              <div class="flex justify-between text-[10px] font-mono text-slate-500">
                <span>LCL: {{ lowerControlLimit }}</span>
                <span class="text-amber-400">Target: {{ targetMean }}</span>
                <span>UCL: {{ upperControlLimit }}</span>
              </div>
            </div>

            <!-- 3. TREND -->
            <div v-else-if="selectedVisual === 'trend'" class="w-full h-full flex flex-col items-center justify-center relative">
              <svg viewBox="0 0 200 80" class="w-full h-20">
                <!-- UCL Line -->
                <line x1="0" y1="20" x2="200" y2="20" stroke="#f43f5e" stroke-width="1" stroke-dasharray="3,3" opacity="0.6" />
                <text x="5" y="16" fill="#f43f5e" font-size="6" font-family="monospace">UCL: {{ upperControlLimit }}</text>

                <!-- Mean Line -->
                <line x1="0" y1="40" x2="200" y2="40" stroke="#fbbf24" stroke-width="1" stroke-dasharray="4,2" opacity="0.7" />
                <text x="5" y="36" fill="#fbbf24" font-size="6" font-family="monospace">Target: {{ targetMean }}</text>

                <!-- LCL Line -->
                <line x1="0" y1="65" x2="200" y2="65" stroke="#f59e0b" stroke-width="1" stroke-dasharray="3,3" opacity="0.6" />
                <text x="5" y="61" fill="#f59e0b" font-size="6" font-family="monospace">LCL: {{ lowerControlLimit }}</text>

                <!-- Actual Trend Curve -->
                <path
                  d="M 10 60 Q 50 20, 100 45 T 190 30"
                  fill="none"
                  stroke="#57715b"
                  stroke-width="2.5"
                  stroke-linecap="round"
                />
                <circle cx="190" cy="30" r="3.5" fill="#768f79" />
              </svg>
            </div>

            <!-- 4. BAR -->
            <div v-else class="w-full h-full flex items-end justify-center gap-3 pb-2 px-6">
              <div class="w-7 bg-slate-800 rounded-t h-12"></div>
              <div class="w-7 bg-slate-800 rounded-t h-16"></div>
              <div class="w-7 bg-zinc-600 rounded-t h-20"></div>
              <div class="w-7 bg-zinc-500 rounded-t h-24"></div>
            </div>
          </div>

          <div class="text-[11px] text-slate-400 border-t border-slate-800/80 pt-2 flex justify-between font-mono items-center">
            <span>Actual: {{ metricMeta.defaultVal }} {{ metricMeta.unit }}</span>
            <Badge variant="outline" :class="currentPreviewStatus.class" class="text-[10px] font-mono border">
              {{ currentPreviewStatus.label }}
            </Badge>
          </div>
        </div>
      </div>
    </div>

    <!-- Section 2: Currently Pinned KPI Dashboard Grid -->
    <div class="space-y-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Pin class="w-4 h-4 text-zinc-400" />
          <h4 class="text-sm font-semibold text-slate-100">Pinned Production KPI Tiles ({{ pinnedWidgets.length }})</h4>
        </div>
        <span class="text-xs text-slate-400 font-mono">Live Real-Time OT Refresh</span>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <div
          v-for="widget in pinnedWidgets"
          :key="widget.id"
          class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-sm space-y-4 relative group hover:border-zinc-700 transition-colors"
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

          <!-- Value & SPC Thresholds -->
          <div class="flex items-baseline justify-between font-mono">
            <div>
              <span class="text-3xl font-bold text-white">{{ widget.currentValue }}</span>
              <span class="text-xs font-normal text-slate-400 ml-1">{{ widget.unit }}</span>
            </div>
            <div class="text-right text-xs space-y-0.5">
              <div class="flex items-center gap-1.5 justify-end">
                <span class="text-[10px] text-slate-500 uppercase">Target:</span>
                <span class="text-amber-400 font-medium">{{ widget.targetValue }} {{ widget.unit }}</span>
              </div>
              <div class="flex items-center gap-2 text-[10px] text-slate-400 justify-end">
                <span v-if="widget.upperLimit != null" class="text-rose-400/90 font-mono">UCL: {{ widget.upperLimit }}</span>
                <span v-if="widget.lowerLimit != null" class="text-amber-400/90 font-mono">LCL: {{ widget.lowerLimit }}</span>
              </div>
            </div>
          </div>

          <!-- Mini SPC Indicator Bar -->
          <div class="w-full bg-slate-950 h-2 rounded-full overflow-hidden border border-slate-800 relative">
            <div
              class="bg-zinc-500 h-full rounded-full transition-all"
              :style="{ width: `${Math.min((widget.currentValue / (widget.targetValue || 1)) * 100, 100)}%` }"
            ></div>
          </div>

          <div class="flex items-center justify-between text-[11px] text-slate-400 font-mono pt-1">
            <span>Interval: {{ widget.interval }}</span>
            <Badge
              variant="outline"
              :class="getSpcStatus(widget.currentValue, widget.upperLimit, widget.lowerLimit).class"
              class="text-[10px] font-mono border"
            >
              {{ getSpcStatus(widget.currentValue, widget.upperLimit, widget.lowerLimit).label }}
            </Badge>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
