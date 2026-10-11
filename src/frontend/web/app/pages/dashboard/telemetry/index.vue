<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Activity,
  Cpu,
  HardDrive,
  Gauge,
  Radio,
  Zap,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  SlidersHorizontal,
  Layers,
  Search,
  Wifi,
  WifiOff,
  Bell,
  Pin,
  Check,
  Settings2,
  Database,
  Filter,
  Eye,
  Maximize2
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import { Input } from '~/components/ui/input'
import AlertRulesManager from '~/components/analytics/AlertRulesManager.vue'
import HeimdallSearchBar from '~/components/search/HeimdallSearchBar.vue'
import { useControllers } from '~/composables/useControllers'
import { useTelemetryMetrics } from '~/composables/useTelemetryMetrics'
import { useTelemetryTemplates } from '~/composables/useTelemetryTemplates'
import type { TelemetryDataPointCatalogItem } from '~/types/telemetryMetrics'
import type { SearchInstanceConfig, SearchResultItem } from '~/types/search'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const router = useRouter()

type TelemetryTab = 'live' | 'templates' | 'configurations' | 'alerts'
const activeTab = ref<TelemetryTab>('live')

// Sync route query parameters (tab, selected controller, metric, search)
watch(() => route.query, (q) => {
  if (q.tab && ['live', 'templates', 'configurations', 'alerts'].includes(q.tab as string)) {
    activeTab.value = q.tab as TelemetryTab
  }
  if (q.controllerId || q.selected) {
    const targetId = (q.controllerId || q.selected) as string
    const found = controllers.value.find(c => c.id === targetId || c.hostname === targetId)
    if (found) {
      selectedControllerId.value = found.id
    }
  }
  if (q.metric) {
    const m = (q.metric as string).toLowerCase()
    if (m.includes('cycle')) visibleWidgets.value.cycle = true
    if (m.includes('spindle')) visibleWidgets.value.spindle = true
    if (m.includes('vibration')) visibleWidgets.value.vibration = true
    if (m.includes('hydraulic') || m.includes('pressure')) visibleWidgets.value.pneumatics = true
    if (m.includes('cpu')) visibleWidgets.value.cpu = true
    if (m.includes('ram') || m.includes('memory')) visibleWidgets.value.ram = true
  }
  if (q.search) {
    searchQuery.value = q.search as string
  }
}, { immediate: true })

const setTab = (tab: TelemetryTab) => {
  activeTab.value = tab
  router.push({ query: { ...route.query, tab } })
}

const { controllers, isLoading: controllersLoading, fetchControllers } = useControllers()
const {
  metrics,
  catalogItems,
  isCatalogLoading,
  fetchCatalog,
  pinToOverview,
  getPinnedWidgets
} = useTelemetryMetrics()
const { templates, activeTemplate, setActiveTemplate } = useTelemetryTemplates()

// Live Telemetry Tab State
const selectedControllerId = ref<string>('')
const filterOnlineOnly = ref(false)
const searchQuery = ref('')
const pinnedSuccessId = ref<string | null>(null)

// Telemetry Heimdall Search Bar Configuration - scoped strictly to telemetry search points
const telemetrySearchConfig: SearchInstanceConfig = {
  instanceId: 'telemetry',
  placeholder: 'Search telemetry data points: spindle, vibration, cycle time, category:thermal, unit:RPM...',
  enableAutoTagging: true,
  allowedTagKeys: ['metric', 'category', 'unit', 'datatype', 'source', 'quality', 'key']
}

const onSearch = (q: string) => {
  searchQuery.value = q
}

const isGaugeMatchingSearch = (gaugeKey: string) => {
  if (!searchQuery.value.trim()) return true
  const q = searchQuery.value.toLowerCase().trim()
  if (gaugeKey === 'cycle') {
    return q.includes('cycle') || q.includes('tact') || q.includes('time') || 'station cycle time'.includes(q)
  }
  if (gaugeKey === 'spindle') {
    return q.includes('spindle') || q.includes('speed') || q.includes('rpm') || q.includes('temp') || q.includes('bearing') || 'spindle rotary speed'.includes(q)
  }
  if (gaugeKey === 'vibration') {
    return q.includes('vib') || q.includes('rms') || q.includes('el3632') || 'vibration velocity rms'.includes(q)
  }
  if (gaugeKey === 'cpu') {
    return q.includes('cpu') || q.includes('workload') || q.includes('utilization') || 'ipc cpu workload'.includes(q)
  }
  if (gaugeKey === 'ram') {
    return q.includes('ram') || q.includes('mem') || 'memory load'.includes(q)
  }
  if (gaugeKey === 'pneumatics') {
    return q.includes('pneu') || q.includes('press') || q.includes('bar') || 'pneumatic pressure'.includes(q)
  }
  return false
}

const hasNoMatchingGauges = computed(() => {
  if (!searchQuery.value.trim()) return false
  return !(
    (visibleWidgets.value.cycle && isGaugeMatchingSearch('cycle')) ||
    (visibleWidgets.value.spindle && isGaugeMatchingSearch('spindle')) ||
    (visibleWidgets.value.vibration && isGaugeMatchingSearch('vibration')) ||
    (visibleWidgets.value.cpu && isGaugeMatchingSearch('cpu')) ||
    (visibleWidgets.value.pneumatics && isGaugeMatchingSearch('pneumatics'))
  )
})

const handleSearchResult = (item: SearchResultItem) => {
  // Telemetry metric / sensor point selection
  const id = (item.id || '').toLowerCase()
  const name = (item.name || '').toLowerCase()
  const disp = (item.displayName || '').toLowerCase()

  if (id.includes('cycle') || name.includes('cycle') || disp.includes('cycle')) {
    visibleWidgets.value.cycle = true
  } else if (id.includes('spindle') || name.includes('spindle') || disp.includes('spindle') || id.includes('temp') || name.includes('temp')) {
    visibleWidgets.value.spindle = true
  } else if (id.includes('vibration') || name.includes('vibration') || disp.includes('vibration')) {
    visibleWidgets.value.vibration = true
  } else if (id.includes('hydraulic') || id.includes('pressure') || name.includes('pressure') || disp.includes('pressure')) {
    visibleWidgets.value.pneumatics = true
  } else if (id.includes('cpu') || name.includes('cpu') || disp.includes('cpu')) {
    visibleWidgets.value.cpu = true
  } else if (id.includes('ram') || id.includes('memory') || name.includes('memory') || disp.includes('memory')) {
    visibleWidgets.value.ram = true
  }

  catalogSearch.value = item.name || item.id

  if (activeTab.value !== 'live' && activeTab.value !== 'configurations') {
    setTab('live')
  }
}

// Live widget visibility toggles (Customizable live telemetry layout)
const visibleWidgets = ref({
  cycle: true,
  cpu: true,
  ram: true,
  spindle: true,
  vibration: true,
  pneumatics: true
})
const cardDensity = ref<'normal' | 'compact'>('normal')

const activeController = computed(() => {
  if (selectedControllerId.value) {
    const found = controllers.value.find(c => c.id === selectedControllerId.value)
    if (found) return found
  }
  return controllers.value[0] || null
})

const filteredControllers = computed(() => {
  let list = controllers.value
  if (filterOnlineOnly.value) {
    list = list.filter(c => c.telemetry?.isOnline ?? c.isOnline)
  }
  return list
})

const fleetStats = computed(() => {
  const total = controllers.value.length
  const online = controllers.value.filter(c => c.telemetry?.isOnline ?? c.isOnline).length
  const avgCpu = total > 0
    ? Math.round(controllers.value.reduce((acc, c) => acc + (c.telemetry?.cpuUsagePercent ?? 0), 0) / total)
    : 42
  const avgRam = total > 0
    ? Math.round(controllers.value.reduce((acc, c) => acc + (c.telemetry?.ramUsagePercent ?? 0), 0) / total)
    : 58

  return { total, online, offline: total - online, avgCpu, avgRam }
})

// Dynamic Catalog Filter State
const catalogSearch = ref('')
const selectedCatalogCategory = ref('all')
const selectedCatalogType = ref('all')

const filteredCatalog = computed(() => {
  let list = catalogItems.value
  if (selectedCatalogCategory.value !== 'all') {
    list = list.filter(item => item.category.toLowerCase() === selectedCatalogCategory.value.toLowerCase())
  }
  if (selectedCatalogType.value !== 'all') {
    list = list.filter(item => item.dataType.toLowerCase() === selectedCatalogType.value.toLowerCase())
  }
  if (catalogSearch.value.trim()) {
    const q = catalogSearch.value.toLowerCase().trim()
    list = list.filter(item =>
      item.key.toLowerCase().includes(q) ||
      item.name.toLowerCase().includes(q) ||
      item.pathOrSymbol.toLowerCase().includes(q) ||
      item.description.toLowerCase().includes(q)
    )
  }
  return list
})

const handlePinCard = (metricKey: string, metricName: string, unit: string, val: number) => {
  const machine = activeController.value
  const machineId = machine?.id || 'm-op20'
  const machineName = machine?.name || machine?.hostname || 'OP20 Laser Cell'
  
  pinToOverview({
    machineId,
    machineName,
    metricKey,
    metricName,
    unit,
    currentValue: val,
    status: 'nominal'
  })

  pinnedSuccessId.value = metricKey
  setTimeout(() => {
    pinnedSuccessId.value = null
  }, 2000)
}

onMounted(async () => {
  await fetchControllers(false)
  if (controllers.value.length > 0 && !selectedControllerId.value) {
    const fromQuery = (route.query.controllerId || route.query.selected) as string
    const found = fromQuery ? controllers.value.find(c => c.id === fromQuery || c.hostname === fromQuery) : null
    selectedControllerId.value = found ? found.id : controllers.value[0].id
  }
  await fetchCatalog()
})
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Hub Navigation Header -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 p-6 bg-card border border-border rounded-2xl shadow-xs">
      <div>
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-xl bg-primary/10 border border-primary/20 text-primary">
            <Radio class="w-5 h-5 animate-pulse" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-lg font-bold text-foreground">{{ $t('telemetry.title', 'Industrial Telemetry Hub') }}</h2>
              <Badge variant="outline" class="font-mono text-[10px] px-2 py-0.5 border-emerald-500/30 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10">
                {{ $t('telemetry.liveIngestionActive', 'Live Ingestion Active') }}
              </Badge>
            </div>
            <p class="text-xs text-muted-foreground mt-0.5">
              {{ $t('telemetry.hubDescription', 'Unified high-frequency stream monitoring, dynamic catalog discovery, and layout management.') }}
            </p>
          </div>
        </div>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <Button
          variant="outline"
          size="sm"
          @click="fetchControllers(true); fetchCatalog()"
          :disabled="controllersLoading"
          class="text-xs h-9 cursor-pointer"
        >
          <RefreshCw :class="{ 'animate-spin': controllersLoading }" class="w-3.5 h-3.5 mr-1.5" />
          <span>{{ $t('common.refreshFleet', 'Refresh Fleet') }}</span>
        </Button>
      </div>
    </div>

    <!-- Tab Selection Bar -->
    <div class="flex items-center gap-2 border-b border-border pb-2 overflow-x-auto text-xs font-medium">
      <button
        type="button"
        @click="setTab('live')"
        :class="activeTab === 'live' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Activity class="w-4 h-4" />
        <span>{{ $t('telemetry.liveTelemetry', 'Live Telemetry') }}</span>
      </button>

      <button
        type="button"
        @click="setTab('configurations')"
        :class="activeTab === 'configurations' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <SlidersHorizontal class="w-4 h-4" />
        <span>{{ $t('telemetry.configurationsAndCatalog', 'Configurations & Catalog') }}</span>
      </button>

      <button
        type="button"
        @click="setTab('alerts')"
        :class="activeTab === 'alerts' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Bell class="w-4 h-4" />
        <span>{{ $t('telemetry.rulesAndAlerts', 'Rules & Alerts') }}</span>
      </button>
    </div>

    <!-- ================================================================= -->
    <!-- TAB 1: LIVE TELEMETRY                                             -->
    <!-- ================================================================= -->
    <template v-if="activeTab === 'live'">
      <!-- Fleet Stats Strip -->
      <div class="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs font-mono">
        <div class="p-3 rounded-xl bg-card border border-border">
          <span class="text-muted-foreground block text-[11px]">Monitored Controllers</span>
          <span class="text-foreground font-bold text-base">{{ fleetStats.total }} Total</span>
        </div>
        <div class="p-3 rounded-xl bg-card border border-border">
          <span class="text-muted-foreground block text-[11px]">{{ $t('dashboard.activeClients', 'Active Nodes') }}</span>
          <span class="text-emerald-600 dark:text-emerald-400 font-bold text-base">
            {{ fleetStats.online }} {{ $t('common.online', 'Online') }}
          </span>
        </div>
        <div class="p-3 rounded-xl bg-card border border-border">
          <span class="text-muted-foreground block text-[11px]">{{ $t('telemetry.cpuWorkload', 'Average CPU Utilization') }}</span>
          <span class="text-foreground font-bold text-base">{{ fleetStats.avgCpu }}%</span>
        </div>
        <div class="p-3 rounded-xl bg-card border border-border">
          <span class="text-muted-foreground block text-[11px]">{{ $t('dashboard.avgUptime', 'Average Memory Load') }}</span>
          <span class="text-foreground font-bold text-base">{{ fleetStats.avgRam }}%</span>
        </div>
      </div>

      <!-- Heimdall Search Bar -->
      <div class="w-full">
        <HeimdallSearchBar
          :config="telemetrySearchConfig"
          :immediate="true"
          @search="onSearch"
          @select-result="handleSearchResult"
        />
      </div>

      <!-- Controls & Layout Customization Bar -->
      <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-3 p-4 bg-muted/20 border border-border rounded-xl">
        <div class="flex flex-wrap items-center gap-2">
          <!-- Controller Selector -->
          <select
            v-model="selectedControllerId"
            class="px-3 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          >
            <option v-if="filteredControllers.length === 0" disabled value="">
              {{ $t('clients.noControllersFound', 'No matching controllers') }}
            </option>
            <option v-for="c in filteredControllers" :key="c.id" :value="c.id">
              {{ c.name || c.hostname }} ({{ c.ipAddress || '127.0.0.1' }})
            </option>
          </select>

          <!-- Online Filter Switch -->
          <label class="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground cursor-pointer select-none">
            <input type="checkbox" v-model="filterOnlineOnly" class="rounded border-border text-primary" />
            <span>{{ $t('common.online', 'Online Nodes Only') }}</span>
          </label>
        </div>

        <!-- Widget Layout Customizer Toggles -->
        <div class="flex items-center gap-3 text-xs">
          <span class="text-muted-foreground font-medium flex items-center gap-1">
            <Settings2 class="w-3.5 h-3.5" /> {{ $t('telemetry.customizeLayout', 'Visible Gauges') }}:
          </span>
          <label class="flex items-center gap-1 cursor-pointer select-none text-muted-foreground hover:text-foreground">
            <input type="checkbox" v-model="visibleWidgets.cycle" class="rounded border-border text-primary" />
            <span>{{ $t('machines.cycleTime', 'Cycle') }}</span>
          </label>
          <label class="flex items-center gap-1 cursor-pointer select-none text-muted-foreground hover:text-foreground">
            <input type="checkbox" v-model="visibleWidgets.spindle" class="rounded border-border text-primary" />
            <span>{{ $t('telemetry.spindleRpm', 'Spindle') }}</span>
          </label>
          <label class="flex items-center gap-1 cursor-pointer select-none text-muted-foreground hover:text-foreground">
            <input type="checkbox" v-model="visibleWidgets.vibration" class="rounded border-border text-primary" />
            <span>{{ $t('telemetry.spindleVibration', 'Vibration') }}</span>
          </label>
          <label class="flex items-center gap-1 cursor-pointer select-none text-muted-foreground hover:text-foreground">
            <input type="checkbox" v-model="visibleWidgets.cpu" class="rounded border-border text-primary" />
            <span>{{ $t('telemetry.cpuWorkload', 'CPU') }}</span>
          </label>
          <label class="flex items-center gap-1 cursor-pointer select-none text-muted-foreground hover:text-foreground">
            <input type="checkbox" v-model="visibleWidgets.pneumatics" class="rounded border-border text-primary" />
            <span>{{ $t('telemetry.hydraulicPressure', 'Pressure') }}</span>
          </label>
        </div>
      </div>

      <!-- Live Stream Cards Grid with "Pin to Overview" Actions -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <!-- 1. Station Tact Cycle Time -->
        <Card v-if="visibleWidgets.cycle && isGaugeMatchingSearch('cycle')" class="border-border bg-card shadow-xs relative group">
          <CardHeader class="pb-2">
            <div class="flex items-start justify-between">
              <div>
                <Badge variant="outline" class="text-[9px] px-1.5 py-0 font-mono border-emerald-500/40 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10">
                  Real-Time ADS
                </Badge>
                <CardTitle class="text-sm font-bold text-foreground mt-1">
                  Station Cycle Time
                </CardTitle>
                <CardDescription class="text-[11px]">
                  TwinCAT PLC actual cycle duration
                </CardDescription>
              </div>
              <Button
                variant="ghost"
                size="sm"
                @click="handlePinCard('cycle_time', 'Station Cycle Time', 'ms', 1198.4)"
                class="h-7 px-2 text-xs gap-1 cursor-pointer"
                title="Pin this gauge directly to system overview dashboard"
              >
                <Check v-if="pinnedSuccessId === 'cycle_time'" class="w-3.5 h-3.5 text-emerald-500" />
                <Pin v-else class="w-3.5 h-3.5 text-muted-foreground group-hover:text-primary" />
                <span class="text-[10px]">{{ pinnedSuccessId === 'cycle_time' ? 'Pinned!' : 'Pin' }}</span>
              </Button>
            </div>
          </CardHeader>
          <CardContent class="space-y-3">
            <div class="flex items-baseline justify-between">
              <span class="font-mono text-2xl font-black text-foreground">1,198.4 <span class="text-xs font-normal text-muted-foreground">ms</span></span>
              <span class="text-xs font-mono text-emerald-600 dark:text-emerald-400 font-semibold">Nominal 1,200ms</span>
            </div>
            <!-- Sparkline simulation -->
            <div class="w-full bg-muted/40 h-8 rounded-lg flex items-end gap-1 px-1 py-1">
              <div class="bg-primary/60 rounded-xs flex-1 h-[65%]" />
              <div class="bg-primary/60 rounded-xs flex-1 h-[70%]" />
              <div class="bg-primary/60 rounded-xs flex-1 h-[60%]" />
              <div class="bg-primary/60 rounded-xs flex-1 h-[80%]" />
              <div class="bg-primary/80 rounded-xs flex-1 h-[75%]" />
              <div class="bg-primary rounded-xs flex-1 h-[72%]" />
            </div>
          </CardContent>
        </Card>

        <!-- 2. Spindle Speed Velocity -->
        <Card v-if="visibleWidgets.spindle && isGaugeMatchingSearch('spindle')" class="border-border bg-card shadow-xs relative group">
          <CardHeader class="pb-2">
            <div class="flex items-start justify-between">
              <div>
                <Badge variant="outline" class="text-[9px] px-1.5 py-0 font-mono border-sky-500/40 text-sky-600 dark:text-sky-400 bg-sky-500/10">
                  Drive Kinematics
                </Badge>
                <CardTitle class="text-sm font-bold text-foreground mt-1">
                  Spindle Rotary Speed
                </CardTitle>
                <CardDescription class="text-[11px]">
                  Beckhoff AX5000 servo encoder feedback
                </CardDescription>
              </div>
              <Button
                variant="ghost"
                size="sm"
                @click="handlePinCard('spindle_rpm', 'Spindle Rotary Speed', 'RPM', 3450)"
                class="h-7 px-2 text-xs gap-1 cursor-pointer"
                title="Pin this gauge directly to system overview dashboard"
              >
                <Check v-if="pinnedSuccessId === 'spindle_rpm'" class="w-3.5 h-3.5 text-emerald-500" />
                <Pin v-else class="w-3.5 h-3.5 text-muted-foreground group-hover:text-primary" />
                <span class="text-[10px]">{{ pinnedSuccessId === 'spindle_rpm' ? 'Pinned!' : 'Pin' }}</span>
              </Button>
            </div>
          </CardHeader>
          <CardContent class="space-y-3">
            <div class="flex items-baseline justify-between">
              <span class="font-mono text-2xl font-black text-foreground">3,450 <span class="text-xs font-normal text-muted-foreground">RPM</span></span>
              <span class="text-xs font-mono text-muted-foreground">Target 3,500</span>
            </div>
            <div class="w-full bg-muted/40 h-8 rounded-lg flex items-end gap-1 px-1 py-1">
              <div class="bg-sky-500/60 rounded-xs flex-1 h-[85%]" />
              <div class="bg-sky-500/60 rounded-xs flex-1 h-[87%]" />
              <div class="bg-sky-500/60 rounded-xs flex-1 h-[84%]" />
              <div class="bg-sky-500/60 rounded-xs flex-1 h-[88%]" />
              <div class="bg-sky-500/80 rounded-xs flex-1 h-[86%]" />
              <div class="bg-sky-500 rounded-xs flex-1 h-[87%]" />
            </div>
          </CardContent>
        </Card>

        <!-- 3. Spindle Vibration RMS -->
        <Card v-if="visibleWidgets.vibration && isGaugeMatchingSearch('vibration')" class="border-border bg-card shadow-xs relative group">
          <CardHeader class="pb-2">
            <div class="flex items-start justify-between">
              <div>
                <Badge variant="outline" class="text-[9px] px-1.5 py-0 font-mono border-amber-500/40 text-amber-600 dark:text-amber-400 bg-amber-500/10">
                  EL3632 Condition
                </Badge>
                <CardTitle class="text-sm font-bold text-foreground mt-1">
                  Vibration Velocity RMS
                </CardTitle>
                <CardDescription class="text-[11px]">
                  Triaxial piezoelectric accelerometer
                </CardDescription>
              </div>
              <Button
                variant="ghost"
                size="sm"
                @click="handlePinCard('vibration_rms_mms', 'Vibration Velocity RMS', 'mm/s', 1.48)"
                class="h-7 px-2 text-xs gap-1 cursor-pointer"
                title="Pin this gauge directly to system overview dashboard"
              >
                <Check v-if="pinnedSuccessId === 'vibration_rms_mms'" class="w-3.5 h-3.5 text-emerald-500" />
                <Pin v-else class="w-3.5 h-3.5 text-muted-foreground group-hover:text-primary" />
                <span class="text-[10px]">{{ pinnedSuccessId === 'vibration_rms_mms' ? 'Pinned!' : 'Pin' }}</span>
              </Button>
            </div>
          </CardHeader>
          <CardContent class="space-y-3">
            <div class="flex items-baseline justify-between">
              <span class="font-mono text-2xl font-black text-foreground">1.48 <span class="text-xs font-normal text-muted-foreground">mm/s</span></span>
              <span class="text-xs font-mono text-amber-600 dark:text-amber-400">Max Tol: 2.80</span>
            </div>
            <div class="w-full bg-muted/40 h-8 rounded-lg flex items-end gap-1 px-1 py-1">
              <div class="bg-amber-500/60 rounded-xs flex-1 h-[40%]" />
              <div class="bg-amber-500/60 rounded-xs flex-1 h-[45%]" />
              <div class="bg-amber-500/60 rounded-xs flex-1 h-[42%]" />
              <div class="bg-amber-500/60 rounded-xs flex-1 h-[52%]" />
              <div class="bg-amber-500/80 rounded-xs flex-1 h-[48%]" />
              <div class="bg-amber-500 rounded-xs flex-1 h-[50%]" />
            </div>
          </CardContent>
        </Card>

        <!-- 4. Host IPC CPU Utilization -->
        <Card v-if="visibleWidgets.cpu && isGaugeMatchingSearch('cpu')" class="border-border bg-card shadow-xs relative group">
          <CardHeader class="pb-2">
            <div class="flex items-start justify-between">
              <div>
                <Badge variant="outline" class="text-[9px] px-1.5 py-0 font-mono border-primary/40 text-primary bg-primary/10">
                  IPC Hardware
                </Badge>
                <CardTitle class="text-sm font-bold text-foreground mt-1">
                  IPC CPU Workload
                </CardTitle>
                <CardDescription class="text-[11px]">
                  Aggregated multi-core processing load
                </CardDescription>
              </div>
              <Button
                variant="ghost"
                size="sm"
                @click="handlePinCard('cpu_usage_pct', 'IPC CPU Workload', '%', 38.5)"
                class="h-7 px-2 text-xs gap-1 cursor-pointer"
                title="Pin this gauge directly to system overview dashboard"
              >
                <Check v-if="pinnedSuccessId === 'cpu_usage_pct'" class="w-3.5 h-3.5 text-emerald-500" />
                <Pin v-else class="w-3.5 h-3.5 text-muted-foreground group-hover:text-primary" />
                <span class="text-[10px]">{{ pinnedSuccessId === 'cpu_usage_pct' ? 'Pinned!' : 'Pin' }}</span>
              </Button>
            </div>
          </CardHeader>
          <CardContent class="space-y-3">
            <div class="flex items-baseline justify-between">
              <span class="font-mono text-2xl font-black text-foreground">38.5 <span class="text-xs font-normal text-muted-foreground">%</span></span>
              <span class="text-xs font-mono text-emerald-600 dark:text-emerald-400">Normal Range</span>
            </div>
            <div class="w-full bg-muted/40 h-8 rounded-lg flex items-end gap-1 px-1 py-1">
              <div class="bg-primary/50 rounded-xs flex-1 h-[35%]" />
              <div class="bg-primary/50 rounded-xs flex-1 h-[42%]" />
              <div class="bg-primary/50 rounded-xs flex-1 h-[38%]" />
              <div class="bg-primary/50 rounded-xs flex-1 h-[45%]" />
              <div class="bg-primary/70 rounded-xs flex-1 h-[36%]" />
              <div class="bg-primary rounded-xs flex-1 h-[39%]" />
            </div>
          </CardContent>
        </Card>

        <!-- 5. Pneumatic Header Pressure -->
        <Card v-if="visibleWidgets.pneumatics && isGaugeMatchingSearch('pneumatics')" class="border-border bg-card shadow-xs relative group">
          <CardHeader class="pb-2">
            <div class="flex items-start justify-between">
              <div>
                <Badge variant="outline" class="text-[9px] px-1.5 py-0 font-mono border-cyan-500/40 text-cyan-600 dark:text-cyan-400 bg-cyan-500/10">
                  OPC UA Valve
                </Badge>
                <CardTitle class="text-sm font-bold text-foreground mt-1">
                  Pneumatic Pressure
                </CardTitle>
                <CardDescription class="text-[11px]">
                  Header line regulated air pressure
                </CardDescription>
              </div>
              <Button
                variant="ghost"
                size="sm"
                @click="handlePinCard('pneumatic_pressure_bar', 'Pneumatic Pressure', 'bar', 6.24)"
                class="h-7 px-2 text-xs gap-1 cursor-pointer"
                title="Pin this gauge directly to system overview dashboard"
              >
                <Check v-if="pinnedSuccessId === 'pneumatic_pressure_bar'" class="w-3.5 h-3.5 text-emerald-500" />
                <Pin v-else class="w-3.5 h-3.5 text-muted-foreground group-hover:text-primary" />
                <span class="text-[10px]">{{ pinnedSuccessId === 'pneumatic_pressure_bar' ? 'Pinned!' : 'Pin' }}</span>
              </Button>
            </div>
          </CardHeader>
          <CardContent class="space-y-3">
            <div class="flex items-baseline justify-between">
              <span class="font-mono text-2xl font-black text-foreground">6.24 <span class="text-xs font-normal text-muted-foreground">bar</span></span>
              <span class="text-xs font-mono text-muted-foreground">Nominal 6.30</span>
            </div>
            <div class="w-full bg-muted/40 h-8 rounded-lg flex items-end gap-1 px-1 py-1">
              <div class="bg-cyan-500/60 rounded-xs flex-1 h-[78%]" />
              <div class="bg-cyan-500/60 rounded-xs flex-1 h-[80%]" />
              <div class="bg-cyan-500/60 rounded-xs flex-1 h-[79%]" />
              <div class="bg-cyan-500/60 rounded-xs flex-1 h-[81%]" />
              <div class="bg-cyan-500/80 rounded-xs flex-1 h-[78%]" />
              <div class="bg-cyan-500 rounded-xs flex-1 h-[79%]" />
            </div>
          </CardContent>
        </Card>

        <!-- No matching gauges fallback -->
        <div v-if="hasNoMatchingGauges" class="col-span-full p-8 text-center bg-card border border-border rounded-xl">
          <p class="text-sm font-medium text-foreground">{{ $t('telemetry.noMatchingGauges', 'No live telemetry gauges match your search query.') }}</p>
          <Button variant="outline" size="sm" class="mt-3 text-xs cursor-pointer" @click="searchQuery = ''">
            {{ $t('telemetry.clearSearch', 'Clear Telemetry Search') }}
          </Button>
        </div>
      </div>
    </template>

    <!-- ================================================================= -->
    <!-- TAB 2: TEMPLATES                                                  -->
    <!-- ================================================================= -->
    <template v-else-if="activeTab === 'templates'">
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <Card
          v-for="tmpl in templates"
          :key="tmpl.id"
          class="border border-border bg-card shadow-xs transition-all hover:border-primary/50 flex flex-col justify-between"
        >
          <CardHeader>
            <div class="flex items-start justify-between">
              <div>
                <CardTitle class="text-sm font-bold text-foreground">
                  {{ tmpl.name }}
                </CardTitle>
                <CardDescription class="text-xs mt-1">
                  {{ tmpl.description }}
                </CardDescription>
              </div>
              <Badge v-if="activeTemplate?.id === tmpl.id" variant="default" class="text-[10px] px-2 py-0.5">
                Active
              </Badge>
            </div>
          </CardHeader>
          <CardContent class="pt-0 space-y-3">
            <div class="text-[11px] text-muted-foreground font-mono flex items-center justify-between">
              <span>Widgets: {{ tmpl.widgets?.length || 4 }}</span>
              <span>Layout: {{ tmpl.layoutType || 'Grid 3x2' }}</span>
            </div>
            <Button
              variant="outline"
              size="sm"
              @click="setActiveTemplate(tmpl)"
              class="w-full text-xs cursor-pointer"
            >
              {{ activeTemplate?.id === tmpl.id ? 'Currently Applied' : 'Apply Layout Template' }}
            </Button>
          </CardContent>
        </Card>
      </div>
    </template>

    <!-- ================================================================= -->
    <!-- TAB 3: CONFIGURATIONS & DYNAMIC DATA-POINT CATALOG                -->
    <!-- ================================================================= -->
    <template v-else-if="activeTab === 'configurations'">
      <div class="space-y-4">
        <!-- Catalog Search and Filters -->
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-3 p-4 bg-muted/20 border border-border rounded-xl">
          <div class="relative flex-1 max-w-sm">
            <Search class="absolute left-2.5 top-2.5 w-3.5 h-3.5 text-muted-foreground" />
            <Input
              v-model="catalogSearch"
              placeholder="Search symbols, keys, or descriptions..."
              class="pl-8 text-xs h-8 bg-background border-border"
            />
          </div>

          <div class="flex flex-wrap items-center gap-2">
            <!-- Category Filter -->
            <select
              v-model="selectedCatalogCategory"
              class="px-2.5 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">All Categories</option>
              <option value="cycle">Cycle Time</option>
              <option value="thermal">Thermal</option>
              <option value="vibration">Vibration</option>
              <option value="resources">System Resources</option>
              <option value="pneumatics">Pneumatics</option>
              <option value="fieldbus">Fieldbus</option>
              <option value="motion">Motion</option>
            </select>

            <!-- Data Type Filter -->
            <select
              v-model="selectedCatalogType"
              class="px-2.5 py-1.5 bg-background border border-border rounded-lg text-xs font-medium text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">All Data Types</option>
              <option value="Float">Float (Analog)</option>
              <option value="Int">Int (Integer)</option>
              <option value="Boolean">Boolean (Discrete)</option>
              <option value="String">String (Text)</option>
            </select>
          </div>
        </div>

        <!-- Catalog Data-Point Table -->
        <div class="border border-border rounded-xl bg-card overflow-hidden shadow-xs">
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-xs">
              <thead>
                <tr class="border-b border-border bg-muted/40 text-muted-foreground font-mono text-[11px]">
                  <th class="p-3">DATA-POINT KEY</th>
                  <th class="p-3">NAME & DESCRIPTION</th>
                  <th class="p-3">TYPE</th>
                  <th class="p-3">UNIT</th>
                  <th class="p-3">POLL FREQUENCY</th>
                  <th class="p-3">SOURCE & SYMBOL PATH</th>
                  <th class="p-3">TARGET LIMITS</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border">
                <tr
                  v-for="item in filteredCatalog"
                  :key="item.id"
                  class="hover:bg-muted/30 transition-colors"
                >
                  <td class="p-3 font-mono font-semibold text-foreground">
                    {{ item.key }}
                  </td>
                  <td class="p-3">
                    <span class="font-medium text-foreground block">{{ item.name }}</span>
                    <span class="text-[11px] text-muted-foreground">{{ item.description }}</span>
                  </td>
                  <td class="p-3">
                    <Badge variant="outline" class="font-mono text-[10px] px-1.5 py-0">
                      {{ item.dataType }}
                    </Badge>
                  </td>
                  <td class="p-3 font-mono">
                    {{ item.unit || '---' }}
                  </td>
                  <td class="p-3 font-mono text-muted-foreground">
                    {{ item.updateFrequencyMs }}ms
                  </td>
                  <td class="p-3 font-mono text-[11px] text-muted-foreground max-w-xs truncate" :title="item.pathOrSymbol">
                    <span class="text-primary">{{ item.sourceType }}:</span> {{ item.pathOrSymbol || 'Auto-mapped' }}
                  </td>
                  <td class="p-3 font-mono text-[11px]">
                    <span class="text-muted-foreground">{{ item.lowerTolerance }} - {{ item.upperTolerance }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </template>

    <!-- ================================================================= -->
    <!-- TAB 4: RULES & ALERTS                                             -->
    <!-- ================================================================= -->
    <template v-else-if="activeTab === 'alerts'">
      <AlertRulesManager />
    </template>
  </div>
</template>
