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
  AlertTriangle,
  Plus,
  Search,
  Filter,
  ArrowUpDown,
  Tag,
  Clock,
  X,
  ChevronDown,
  ChevronUp
} from 'lucide-vue-next'

export type KpiTag = 'OEE' | 'Energy' | 'Thermal' | 'Hydraulic'
export type TimeAggregation = '1m' | '5m' | '1h' | 'shift' | '24h' | 'custom'

export interface PinnedKpiWidget {
  id: string
  title: string
  metric: string
  visualType: 'gauge' | 'trend' | 'bar' | 'bullet'
  targetLine: string
  machineId?: string
  interval: 'shift' | 'hourly' | 'daily'
  timeAggregation: TimeAggregation
  currentValue: number
  targetValue: number
  upperLimit: number
  lowerLimit: number
  unit: string
  tag: KpiTag
  priority: 'Critical' | 'High' | 'Normal'
  author: string
}

const defaultWidgets: PinnedKpiWidget[] = [
  {
    id: 'kpi-1',
    title: 'Plant Master OEE Index',
    metric: 'oee',
    visualType: 'gauge',
    targetLine: 'All Lines',
    interval: 'shift',
    timeAggregation: 'shift',
    currentValue: 90.8,
    targetValue: 88.0,
    upperLimit: 96.0,
    lowerLimit: 82.0,
    unit: '%',
    tag: 'OEE',
    priority: 'Critical',
    author: 'Chief Operations Officer'
  },
  {
    id: 'kpi-2',
    title: 'Pre-Assembly (L1) MTTR',
    metric: 'mttr',
    visualType: 'bullet',
    targetLine: 'Line 1 - Pre-Assembly',
    interval: 'hourly',
    timeAggregation: '1h',
    currentValue: 28.0,
    targetValue: 30.0,
    upperLimit: 45.0,
    lowerLimit: 15.0,
    unit: 'min',
    tag: 'OEE',
    priority: 'High',
    author: 'Shift Lead Tech'
  },
  {
    id: 'kpi-3',
    title: 'Robotics Welding Quality Rate',
    metric: 'quality',
    visualType: 'trend',
    targetLine: 'Line 5 - Robotic Welding Cell',
    interval: 'daily',
    timeAggregation: '24h',
    currentValue: 99.1,
    targetValue: 98.5,
    upperLimit: 100.0,
    lowerLimit: 97.0,
    unit: '%',
    tag: 'OEE',
    priority: 'Normal',
    author: 'Robotics Engineer'
  },
  {
    id: 'kpi-4',
    title: 'Robotics Cell Total Active Power',
    metric: 'energy',
    visualType: 'trend',
    targetLine: 'Line 5 - Robotic Welding Cell',
    interval: 'hourly',
    timeAggregation: '1m',
    currentValue: 42.6,
    targetValue: 40.0,
    upperLimit: 55.0,
    lowerLimit: 20.0,
    unit: 'kW',
    tag: 'Energy',
    priority: 'High',
    author: 'Energy Facility Lead'
  },
  {
    id: 'kpi-5',
    title: 'Spindle Bearing Thermal Load',
    metric: 'thermal',
    visualType: 'gauge',
    targetLine: 'Line 2 - Fastening',
    interval: 'shift',
    timeAggregation: '5m',
    currentValue: 52.4,
    targetValue: 48.0,
    upperLimit: 65.0,
    lowerLimit: 25.0,
    unit: '°C',
    tag: 'Thermal',
    priority: 'Critical',
    author: 'Maintenance Specialist'
  },
  {
    id: 'kpi-6',
    title: 'Main Clamping Hydraulic Circuit',
    metric: 'hydraulic',
    visualType: 'bar',
    targetLine: 'Line 4 - Dispensing',
    interval: 'hourly',
    timeAggregation: '1h',
    currentValue: 168.0,
    targetValue: 160.0,
    upperLimit: 210.0,
    lowerLimit: 130.0,
    unit: 'bar',
    tag: 'Hydraulic',
    priority: 'Normal',
    author: 'Fluid Power Tech'
  }
]

const pinnedWidgets = ref<PinnedKpiWidget[]>([...defaultWidgets])
const isBuilderOpen = ref(true)

// Filter & Sort States
const searchQuery = ref('')
const selectedTagFilter = ref<'All' | KpiTag>('All')
const selectedLineFilter = ref('All')
const selectedSort = ref<'priority' | 'title' | 'val-desc' | 'val-asc' | 'line'>('priority')

onMounted(() => {
  if (typeof window !== 'undefined') {
    const saved = localStorage.getItem('heimdall_pinned_kpis')
    if (saved) {
      try {
        const parsed = JSON.parse(saved)
        if (Array.isArray(parsed) && parsed.length > 0) {
          pinnedWidgets.value = parsed.map(w => ({
            ...w,
            tag: w.tag || 'OEE',
            priority: w.priority || 'Normal',
            author: w.author || 'System Engineer',
            timeAggregation: w.timeAggregation || 'shift',
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
const builderTitle = ref('Custom Production KPI')
const selectedMetric = ref<'oee' | 'energy' | 'thermal' | 'hydraulic' | 'availability' | 'performance' | 'quality' | 'mtbf' | 'mttr' | 'faults'>('oee')
const selectedVisual = ref<'gauge' | 'trend' | 'bar' | 'bullet'>('gauge')
const selectedLine = ref('Line 1 - Pre-Assembly')
const selectedTag = ref<KpiTag>('OEE')
const selectedPriority = ref<'Critical' | 'High' | 'Normal'>('Normal')
const selectedAggregation = ref<TimeAggregation>('shift')
const builderAuthor = ref('Maintenance Engineer')

// SPC Threshold Goals
const targetMean = ref(88.0)
const upperControlLimit = ref(96.0)
const lowerControlLimit = ref(82.0)
const isSaved = ref(false)

const metricMeta = computed(() => {
  switch (selectedMetric.value) {
    case 'energy':
      return { unit: 'kW', defaultVal: 45.2, defaultTarget: 40.0, defaultUcl: 60.0, defaultLcl: 20.0 }
    case 'thermal':
      return { unit: '°C', defaultVal: 54.0, defaultTarget: 48.0, defaultUcl: 65.0, defaultLcl: 25.0 }
    case 'hydraulic':
      return { unit: 'bar', defaultVal: 165.0, defaultTarget: 160.0, defaultUcl: 210.0, defaultLcl: 130.0 }
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
watch(selectedMetric, (m) => {
  targetMean.value = metricMeta.value.defaultTarget
  upperControlLimit.value = metricMeta.value.defaultUcl
  lowerControlLimit.value = metricMeta.value.defaultLcl
  if (m === 'energy') selectedTag.value = 'Energy'
  else if (m === 'thermal') selectedTag.value = 'Thermal'
  else if (m === 'hydraulic') selectedTag.value = 'Hydraulic'
  else selectedTag.value = 'OEE'
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
    interval: selectedAggregation.value === '1h' || selectedAggregation.value === '1m' || selectedAggregation.value === '5m' ? 'hourly' : (selectedAggregation.value === '24h' ? 'daily' : 'shift'),
    timeAggregation: selectedAggregation.value,
    currentValue: metricMeta.value.defaultVal,
    targetValue: targetMean.value,
    upperLimit: upperControlLimit.value,
    lowerLimit: lowerControlLimit.value,
    unit: metricMeta.value.unit,
    tag: selectedTag.value,
    priority: selectedPriority.value,
    author: builderAuthor.value || 'Engineer'
  }

  pinnedWidgets.value.unshift(newWidget)
  if (typeof window !== 'undefined') {
    localStorage.setItem('heimdall_pinned_kpis', JSON.stringify(pinnedWidgets.value))
  }

  isSaved.value = true
  setTimeout(() => {
    isSaved.value = false
  }, 1500)
}

const handleRemoveWidget = (id: string) => {
  pinnedWidgets.value = pinnedWidgets.value.filter(w => w.id !== id)
  if (typeof window !== 'undefined') {
    localStorage.setItem('heimdall_pinned_kpis', JSON.stringify(pinnedWidgets.value))
  }
}

// Filtered and Sorted Widgets
const filteredWidgets = computed(() => {
  let list = [...pinnedWidgets.value]

  // 1. Search Query
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase().trim()
    list = list.filter(w =>
      w.title.toLowerCase().includes(q) ||
      w.targetLine.toLowerCase().includes(q) ||
      w.author.toLowerCase().includes(q) ||
      w.tag.toLowerCase().includes(q)
    )
  }

  // 2. Tag Filter
  if (selectedTagFilter.value !== 'All') {
    list = list.filter(w => w.tag === selectedTagFilter.value)
  }

  // 3. Line Filter
  if (selectedLineFilter.value !== 'All') {
    list = list.filter(w => w.targetLine === selectedLineFilter.value)
  }

  // 4. Sort
  list.sort((a, b) => {
    if (selectedSort.value === 'priority') {
      const pMap: Record<string, number> = { Critical: 3, High: 2, Normal: 1 }
      return (pMap[b.priority] || 0) - (pMap[a.priority] || 0)
    }
    if (selectedSort.value === 'title') {
      return a.title.localeCompare(b.title)
    }
    if (selectedSort.value === 'val-desc') {
      return b.currentValue - a.currentValue
    }
    if (selectedSort.value === 'val-asc') {
      return a.currentValue - b.currentValue
    }
    if (selectedSort.value === 'line') {
      return a.targetLine.localeCompare(b.targetLine)
    }
    return 0
  })

  return list
})

const availableLines = [
  'Line 1 - Pre-Assembly',
  'Line 2 - Fastening',
  'Line 4 - Dispensing',
  'Line 5 - Robotic Welding Cell',
  'Line 7 - Battery EOL',
  'All Lines'
]
</script>

<template>
  <div class="space-y-6">
    <!-- Header Controls: Title, Filter Bar & Launch Builder Button -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 p-5 bg-card border border-border rounded-2xl shadow-xs">
      <div>
        <div class="flex items-center gap-2">
          <Activity class="w-4 h-4 text-primary" />
          <h3 class="text-base font-bold text-foreground">Operational KPI Dashboard</h3>
          <Badge variant="outline" class="font-mono text-[10px] px-2 py-0.5">
            {{ filteredWidgets.length }} Widgets
          </Badge>
        </div>
        <p class="text-xs text-muted-foreground mt-0.5">
          Filtered industrial telemetry metrics with Statistical Process Control (SPC) thresholds.
        </p>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <Button
          @click="isBuilderOpen = !isBuilderOpen"
          variant="outline"
          size="sm"
          class="gap-1.5 text-xs font-semibold cursor-pointer"
        >
          <Sliders class="w-3.5 h-3.5" />
          <span>{{ isBuilderOpen ? 'Hide KPI Builder' : 'Open KPI Builder' }}</span>
          <ChevronUp v-if="isBuilderOpen" class="w-3.5 h-3.5" />
          <ChevronDown v-else class="w-3.5 h-3.5" />
        </Button>
      </div>
    </div>

    <!-- Section 1: On-Demand Expandable KPI Builder Drawer -->
    <div
      v-show="isBuilderOpen"
      class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6 transition-all"
    >
      <div class="flex items-center justify-between border-b border-border pb-3">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-muted border border-border text-foreground">
            <Sliders class="w-4 h-4" />
          </div>
          <div>
            <h4 class="text-sm font-semibold text-foreground">Custom KPI Graph & Tolerance Builder</h4>
            <span class="text-xs text-muted-foreground font-mono">Define custom thresholds, SPC bounds, and aggregations</span>
          </div>
        </div>

        <button
          type="button"
          @click="isBuilderOpen = false"
          class="p-1.5 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
          title="Close Builder Drawer"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Builder Form -->
        <div class="lg:col-span-2 space-y-4">
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div class="sm:col-span-2 space-y-1">
              <label class="text-xs font-medium text-foreground">Widget Title</label>
              <input
                type="text"
                v-model="builderTitle"
                placeholder="e.g. Master Fastening Cycle OEE"
                class="w-full px-3 py-2 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-9"
              />
            </div>

            <div class="space-y-1">
              <label class="text-xs font-medium text-foreground">Tag Category</label>
              <select
                v-model="selectedTag"
                class="w-full px-3 py-2 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-9"
              >
                <option value="OEE">OEE</option>
                <option value="Energy">Energy</option>
                <option value="Thermal">Thermal</option>
                <option value="Hydraulic">Hydraulic</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div class="space-y-1">
              <label class="text-xs font-medium text-foreground">Telemetry Metric</label>
              <select
                v-model="selectedMetric"
                class="w-full px-3 py-2 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-9"
              >
                <option value="oee">Overall Equipment Effectiveness (OEE)</option>
                <option value="energy">Active Electrical Power (kW)</option>
                <option value="thermal">Motor / Bearing Temperature (°C)</option>
                <option value="hydraulic">Hydraulic Fluid Pressure (bar)</option>
                <option value="availability">Operational Availability (%)</option>
                <option value="performance">Speed Performance Index (%)</option>
                <option value="quality">First-Pass Yield Quality (%)</option>
                <option value="mtbf">Mean Time Between Failures (hrs)</option>
                <option value="mttr">Mean Time to Repair (min)</option>
                <option value="faults">Micro-Stoppage Frequency (shift)</option>
              </select>
            </div>

            <div class="space-y-1">
              <label class="text-xs font-medium text-foreground">Target Production Line</label>
              <select
                v-model="selectedLine"
                class="w-full px-3 py-2 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-9"
              >
                <option v-for="l in availableLines" :key="l" :value="l">{{ l }}</option>
              </select>
            </div>

            <div class="space-y-1">
              <label class="text-xs font-medium text-foreground">Time Aggregation</label>
              <select
                v-model="selectedAggregation"
                class="w-full px-3 py-2 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-9"
              >
                <option value="1m">1 Minute (Ultra-High Res)</option>
                <option value="5m">5 Minutes (Micro-Cycle)</option>
                <option value="1h">1 Hour (Hourly Tact)</option>
                <option value="shift">Shift (8-Hour Shift)</option>
                <option value="24h">24 Hours (Daily)</option>
                <option value="custom">Custom Range</option>
              </select>
            </div>
          </div>

          <!-- SPC Thresholds -->
          <div class="p-4 rounded-xl bg-muted/20 border border-border space-y-3">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-foreground">KPI Goals & Statistical Process Control (SPC) Limits</span>
              <Badge variant="outline" class="text-[10px] px-2 py-0.5" :class="currentPreviewStatus.class">
                Status: {{ currentPreviewStatus.label }}
              </Badge>
            </div>

            <div class="grid grid-cols-3 gap-3 font-mono text-xs">
              <div class="space-y-1">
                <span class="text-muted-foreground block text-[10px]">Lower Limit (LCL)</span>
                <input
                  type="number"
                  v-model.number="lowerControlLimit"
                  class="w-full px-3 py-1.5 bg-background border border-border rounded-lg text-xs font-mono text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
                />
              </div>
              <div class="space-y-1">
                <span class="text-muted-foreground block text-[10px]">Target / Mean</span>
                <input
                  type="number"
                  v-model.number="targetMean"
                  class="w-full px-3 py-1.5 bg-background border border-border rounded-lg text-xs font-mono text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
                />
              </div>
              <div class="space-y-1">
                <span class="text-muted-foreground block text-[10px]">Upper Limit (UCL)</span>
                <input
                  type="number"
                  v-model.number="upperControlLimit"
                  class="w-full px-3 py-1.5 bg-background border border-border rounded-lg text-xs font-mono text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
                />
              </div>
            </div>
          </div>
        </div>

        <!-- Live Preview Card & Save Action -->
        <div class="flex flex-col justify-between p-4 rounded-xl bg-muted/30 border border-border space-y-4">
          <div class="space-y-2">
            <span class="text-xs font-bold text-muted-foreground uppercase tracking-wide">Widget Preview</span>
            <div class="font-mono text-2xl font-bold text-foreground">
              {{ metricMeta.defaultVal }} <span class="text-xs font-normal text-muted-foreground">{{ metricMeta.unit }}</span>
            </div>
            <p class="text-xs text-muted-foreground font-mono">
              Target: {{ targetMean }} {{ metricMeta.unit }} (LCL: {{ lowerControlLimit }} / UCL: {{ upperControlLimit }})
            </p>
          </div>

          <Button
            @click="handlePinWidget"
            data-testid="pin-kpi-btn"
            class="w-full gap-2 cursor-pointer font-semibold"
          >
            <Check v-if="isSaved" class="w-4 h-4 text-emerald-400" />
            <Pin v-else class="w-4 h-4" />
            <span>{{ isSaved ? 'Widget Created!' : 'Save & Pin Widget' }}</span>
          </Button>
        </div>
      </div>
    </div>

    <!-- Filter & Sort Bar -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-3 p-3 bg-muted/20 border border-border rounded-xl">
      <!-- Search Input (using semantic type="search") -->
      <div class="relative flex-1 max-w-sm">
        <Search class="absolute left-2.5 top-2.5 w-3.5 h-3.5 text-muted-foreground" />
        <input
          type="search"
          v-model="searchQuery"
          placeholder="Filter by title, line, author..."
          class="w-full pl-8 pr-3 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-8"
        />
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <!-- Tag Filter Pills -->
        <div class="flex items-center bg-background border border-border p-0.5 rounded-lg text-xs font-medium">
          <button
            type="button"
            @click="selectedTagFilter = 'All'"
            :class="selectedTagFilter === 'All' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded transition-all cursor-pointer"
          >
            All
          </button>
          <button
            v-for="t in (['OEE', 'Energy', 'Thermal', 'Hydraulic'] as KpiTag[])"
            :key="t"
            type="button"
            @click="selectedTagFilter = t"
            :class="selectedTagFilter === t ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded transition-all cursor-pointer"
          >
            {{ t }}
          </button>
        </div>

        <!-- Line Filter Dropdown -->
        <select
          v-model="selectedLineFilter"
          class="px-2.5 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-8"
        >
          <option value="All">All Lines</option>
          <option v-for="l in availableLines" :key="l" :value="l">{{ l }}</option>
        </select>

        <!-- Sort Dropdown -->
        <select
          v-model="selectedSort"
          class="px-2.5 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring h-8"
        >
          <option value="priority">Sort by Priority</option>
          <option value="title">Sort by Title (A-Z)</option>
          <option value="val-desc">Sort by Value (High to Low)</option>
          <option value="val-asc">Sort by Value (Low to High)</option>
          <option value="line">Sort by Line</option>
        </select>
      </div>
    </div>

    <!-- Active KPI Widgets Grid -->
    <div v-if="filteredWidgets.length > 0" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <Card
        v-for="w in filteredWidgets"
        :key="w.id"
        class="border border-border bg-card shadow-xs relative group flex flex-col justify-between"
      >
        <CardHeader class="pb-2">
          <div class="flex items-start justify-between gap-2">
            <div>
              <div class="flex items-center gap-1.5 flex-wrap">
                <Badge
                  variant="outline"
                  class="text-[9px] px-1.5 py-0 font-mono"
                  :class="{
                    'border-indigo-500/40 text-indigo-600 dark:text-indigo-400 bg-indigo-500/10': w.tag === 'OEE',
                    'border-amber-500/40 text-amber-600 dark:text-amber-400 bg-amber-500/10': w.tag === 'Energy',
                    'border-rose-500/40 text-rose-600 dark:text-rose-400 bg-rose-500/10': w.tag === 'Thermal',
                    'border-sky-500/40 text-sky-600 dark:text-sky-400 bg-sky-500/10': w.tag === 'Hydraulic'
                  }"
                >
                  {{ w.tag }}
                </Badge>
                <Badge
                  v-if="w.priority === 'Critical'"
                  variant="destructive"
                  class="text-[9px] px-1 py-0 h-4"
                >
                  Critical
                </Badge>
                <span class="text-[10px] text-muted-foreground font-mono flex items-center gap-1">
                  <Clock class="w-2.5 h-2.5" /> {{ w.timeAggregation }}
                </span>
              </div>
              <CardTitle class="text-sm font-bold text-foreground mt-1 line-clamp-1">
                {{ w.title }}
              </CardTitle>
              <p class="text-[11px] text-muted-foreground mt-0.5">
                {{ w.targetLine }}
              </p>
            </div>

            <button
              type="button"
              @click="handleRemoveWidget(w.id)"
              class="opacity-0 group-hover:opacity-100 p-1.5 rounded-lg text-muted-foreground hover:text-rose-600 hover:bg-rose-500/10 transition-all cursor-pointer"
              title="Remove widget"
            >
              <Trash2 class="w-3.5 h-3.5" />
            </button>
          </div>
        </CardHeader>

        <CardContent class="space-y-4 pt-1">
          <!-- Main Metric Value Display -->
          <div class="flex items-baseline justify-between border-b border-border/50 pb-2">
            <div class="font-mono text-2xl font-black text-foreground">
              {{ w.currentValue }} <span class="text-xs font-normal text-muted-foreground">{{ w.unit }}</span>
            </div>
            <div class="text-right">
              <Badge
                variant="outline"
                class="text-[10px] px-2 py-0.5 font-mono"
                :class="getSpcStatus(w.currentValue, w.upperLimit, w.lowerLimit).class"
              >
                {{ getSpcStatus(w.currentValue, w.upperLimit, w.lowerLimit).label }}
              </Badge>
            </div>
          </div>

          <!-- Mini SPC Bounds Bar -->
          <div class="space-y-1">
            <div class="flex items-center justify-between text-[10px] font-mono text-muted-foreground">
              <span>LCL: {{ w.lowerLimit }}</span>
              <span>Target: {{ w.targetValue }}</span>
              <span>UCL: {{ w.upperLimit }}</span>
            </div>
            <div class="w-full bg-muted rounded-full h-1.5 overflow-hidden flex relative">
              <div
                class="h-full bg-primary rounded-full transition-all"
                :style="{
                  width: `${Math.min(100, Math.max(5, ((w.currentValue - w.lowerLimit) / (w.upperLimit - w.lowerLimit || 1)) * 100))}%`
                }"
              />
            </div>
          </div>

          <!-- Author Footnote -->
          <div class="pt-1 text-[10px] text-muted-foreground/80 font-mono flex items-center justify-between">
            <span>Author: {{ w.author }}</span>
            <span class="capitalize">{{ w.visualType }} View</span>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Empty State -->
    <div v-else class="text-center py-16 bg-muted/10 border border-border rounded-2xl p-8 space-y-3">
      <Filter class="w-8 h-8 text-muted-foreground mx-auto" />
      <h4 class="text-sm font-semibold text-foreground">No KPI Widgets Match Criteria</h4>
      <p class="text-xs text-muted-foreground max-w-sm mx-auto">
        Adjust your search query or tag filters, or construct a new custom KPI widget using the builder.
      </p>
      <Button size="sm" @click="searchQuery = ''; selectedTagFilter = 'All'; selectedLineFilter = 'All'">
        Reset Filters
      </Button>
    </div>
  </div>
</template>
