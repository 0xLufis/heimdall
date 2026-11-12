<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Activity,
  Layers,
  SlidersHorizontal,
  Bell,
  RefreshCw,
  Search,
  AlertTriangle,
  Factory,
  MapPin,
  Ticket,
  Monitor,
  Radio,
  Gauge,
  FlaskConical,
  ChevronRight
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import { Input } from '~/components/ui/input'
import AlertRulesManager from '~/components/analytics/AlertRulesManager.vue'
import LiveTelemetryGrid from '~/components/telemetry/LiveTelemetryGrid.vue'
import SimulatorControlModal from '~/components/dashboard/SimulatorControlModal.vue'
import { useControllers } from '~/composables/useControllers'
import { useTelemetryStream } from '~/composables/useTelemetryStream'
import { useTelemetryMetrics } from '~/composables/useTelemetryMetrics'
import { useTelemetryTemplates } from '~/composables/useTelemetryTemplates'
import { useStationTelemetry, type StationTelemetryKpi } from '~/composables/useStationTelemetry'
import { useFeatureFlags } from '~/composables/useFeatureFlags'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const router = useRouter()

type TelemetryTab = 'live' | 'templates' | 'configurations' | 'alerts'
const TABS: TelemetryTab[] = ['live', 'templates', 'configurations', 'alerts']
const activeTab = ref<TelemetryTab>('live')

watch(() => route.query.tab, (newTab) => {
  if (typeof newTab === 'string' && (TABS as string[]).includes(newTab)) {
    activeTab.value = newTab as TelemetryTab
  }
}, { immediate: true })

const setTab = (tab: TelemetryTab) => {
  activeTab.value = tab
  router.replace({ query: { ...route.query, tab } })
}

const { controllers, isLoading: controllersLoading, fetchControllers } = useControllers()
const { catalogItems } = useTelemetryMetrics()
const { templates, activeTemplate, setActiveTemplate } = useTelemetryTemplates()
const { stationKpiList, stationKpisError, isLoading: stationsLoading, lastUpdated, fetchStationKpis } = useStationTelemetry()
const { enableSimulation } = useFeatureFlags()

// ── Station directory ───────────────────────────────────────────────────────
type StatusFilter = 'all' | 'reporting' | 'attention' | 'silent'
const stationSearch = ref('')
const statusFilter = ref<StatusFilter>('all')
const selectedStationId = ref<string>('')
const isSimulatorOpen = ref(false)

/** A station counts as reporting when its last sample is younger than this. */
const REPORTING_WINDOW_MS = 5 * 60 * 1000
const clock = ref(Date.now())

const isReporting = (kpi: StationTelemetryKpi) => {
  if (!kpi.lastTelemetryAt) return false
  const at = Date.parse(kpi.lastTelemetryAt)
  return Number.isFinite(at) && clock.value - at <= REPORTING_WINDOW_MS
}
const needsAttention = (kpi: StationTelemetryKpi) =>
  kpi.operationalStatus === 'FAULTED' || kpi.operationalStatus === 'WARNING' || kpi.hasSlaBreach || kpi.criticalTicketCount > 0

const stations = computed(() =>
  [...stationKpiList.value].sort((a, b) =>
    (a.lineName || '~').localeCompare(b.lineName || '~') || a.name.localeCompare(b.name, undefined, { numeric: true })
  )
)

const fleet = computed(() => {
  const list = stations.value
  const reporting = list.filter(isReporting)
  const withOee = list.filter(s => typeof s.oee === 'number')
  return {
    total: list.length,
    reporting: reporting.length,
    running: list.filter(s => s.operationalStatus === 'RUNNING').length,
    attention: list.filter(needsAttention).length,
    silent: list.length - reporting.length,
    avgOee: withOee.length ? withOee.reduce((sum, s) => sum + (s.oee as number), 0) / withOee.length : null,
    openTickets: list.reduce((sum, s) => sum + s.activeTicketCount, 0)
  }
})

const filteredStations = computed(() => {
  const q = stationSearch.value.trim().toLowerCase()
  return stations.value.filter(s => {
    if (statusFilter.value === 'reporting' && !isReporting(s)) return false
    if (statusFilter.value === 'attention' && !needsAttention(s)) return false
    if (statusFilter.value === 'silent' && isReporting(s)) return false
    if (!q) return true
    return s.name.toLowerCase().includes(q) || s.handle.toLowerCase().includes(q) || (s.lineName || '').toLowerCase().includes(q)
  })
})

/** Stations grouped by line for the directory (stations without a line come last). */
const stationGroups = computed(() => {
  const groups = new Map<string, StationTelemetryKpi[]>()
  for (const s of filteredStations.value) {
    const line = s.lineName || 'Unassigned'
    if (!groups.has(line)) groups.set(line, [])
    groups.get(line)!.push(s)
  }
  return Array.from(groups, ([line, items]) => ({ line, items }))
})

const selectedStation = computed<StationTelemetryKpi | null>(() =>
  stations.value.find(s => s.stationId === selectedStationId.value) ?? null
)

const selectStation = (station: StationTelemetryKpi) => {
  selectedStationId.value = station.stationId
  router.replace({ query: { ...route.query, station: station.stationId, controllerId: undefined } })
}

/**
 * Resolves the station to show: `?station=` (id or handle), else the first station controlled by
 * `?controllerId=`, else keeps the current choice, else the first reporting station.
 */
const resolveSelection = () => {
  const list = stations.value
  if (list.length === 0) return
  const wanted = typeof route.query.station === 'string' ? route.query.station.toUpperCase() : ''
  if (wanted) {
    const match = list.find(s => s.stationId.toUpperCase() === wanted || s.handle.toUpperCase() === wanted)
    if (match) { selectedStationId.value = match.stationId; return }
  }
  const controllerId = typeof route.query.controllerId === 'string' ? route.query.controllerId : ''
  if (controllerId) {
    const controller = controllers.value.find(c => c.id === controllerId)
    const controlled = (controller?.controlledMachines || []).map(m => m.id)
    const match = list.find(s => controlled.includes(s.stationId))
    if (match) { selectedStationId.value = match.stationId; return }
  }
  if (list.some(s => s.stationId === selectedStationId.value)) return
  selectedStationId.value = (list.find(isReporting) ?? list[0]).stationId
}

watch([stations, () => route.query.station, () => route.query.controllerId, controllers], resolveSelection, { immediate: true })

// ── Live stream of the selected station ─────────────────────────────────────
const stream = useTelemetryStream({
  target: () => (selectedStation.value ? { stationId: selectedStation.value.stationId, handle: selectedStation.value.handle } : null),
  bufferCapacity: 120
})

// Show the last known values immediately; live samples take over as they arrive.
watch(selectedStation, (station) => {
  if (station) stream.seed(station)
}, { immediate: true })

const stationControllers = computed(() => {
  const id = selectedStation.value?.stationId
  if (!id) return []
  return controllers.value.filter(c => (c.controlledMachines || []).some(m => m.id === id))
})

const refresh = async () => {
  await Promise.all([fetchStationKpis(), fetchControllers(true)])
}

let clockTimer: ReturnType<typeof setInterval> | null = null
let kpiPollTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  // The shared KPI store may hold a previous page's snapshot: refresh it for this view.
  fetchStationKpis()
  clockTimer = setInterval(() => { clock.value = Date.now() }, 5000)
  // Production KPIs and ticket counts change slowly; dynamics arrive over the live stream.
  kpiPollTimer = setInterval(() => { fetchStationKpis() }, 30_000)
})

onBeforeUnmount(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (kpiPollTimer) clearInterval(kpiPollTimer)
})

// ── Formatting helpers ──────────────────────────────────────────────────────
const fmt = (value: number | null | undefined, digits = 1) =>
  typeof value === 'number' && Number.isFinite(value) ? value.toFixed(digits) : '—'
const fmtInt = (value: number | null | undefined) =>
  typeof value === 'number' && Number.isFinite(value) ? Math.round(value).toLocaleString() : '—'

const relativeTime = (iso: string | null) => {
  if (!iso) return 'never'
  const ms = clock.value - Date.parse(iso)
  if (!Number.isFinite(ms)) return 'unknown'
  if (ms < 60_000) return 'just now'
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)} min ago`
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)} h ago`
  return `${Math.floor(ms / 86_400_000)} d ago`
}

const STATUS_DOT: Record<string, string> = {
  RUNNING: 'bg-emerald-500',
  IDLE: 'bg-sky-500',
  WARNING: 'bg-amber-500',
  FAULTED: 'bg-rose-500',
  MAINTENANCE: 'bg-violet-500',
  UNKNOWN: 'bg-muted-foreground/40'
}

const pct = (value: number | null) => (typeof value === 'number' ? Math.max(0, Math.min(100, value)) : 0)
const throughputPct = computed(() => {
  const s = selectedStation.value
  if (!s || s.partsPerHour === null || !s.targetPartsPerHour) return null
  return (s.partsPerHour / s.targetPartsPerHour) * 100
})

// ── Catalog tab ─────────────────────────────────────────────────────────────
const catalogSearch = ref('')
const filteredCatalog = computed(() => {
  if (!catalogSearch.value) return catalogItems.value
  const q = catalogSearch.value.toLowerCase()
  return catalogItems.value.filter(item =>
    item.name.toLowerCase().includes(q) ||
    item.key.toLowerCase().includes(q) ||
    item.pathOrSymbol?.toLowerCase().includes(q)
  )
})
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-2">
          <Activity class="w-6 h-6 text-primary" />
          <span>Telemetry & Edge Monitoring Hub</span>
        </h1>
        <p class="text-xs text-muted-foreground mt-1">
          Live station dynamics and production KPIs as reported by the plant's PLCs
          <span v-if="lastUpdated"> · KPIs refreshed {{ relativeTime(lastUpdated.toISOString()) }}</span>
        </p>
      </div>

      <div class="flex items-center gap-2">
        <Button
          v-if="enableSimulation"
          variant="outline"
          size="sm"
          class="h-8 text-xs gap-1.5 cursor-pointer"
          title="Control the development fleet simulator"
          @click="isSimulatorOpen = true"
        >
          <FlaskConical class="w-3.5 h-3.5 text-amber-500" />
          <span>Simulator</span>
        </Button>
        <Button
          variant="outline"
          size="sm"
          :disabled="stationsLoading || controllersLoading"
          class="h-8 text-xs gap-1.5 cursor-pointer"
          @click="refresh"
        >
          <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': stationsLoading || controllersLoading }" />
          <span>Refresh</span>
        </Button>
      </div>
    </div>

    <!-- Tabs Navigation -->
    <div class="flex items-center gap-1 border-b border-border pb-1 overflow-x-auto">
      <button
        type="button"
        @click="setTab('live')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'live' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Activity class="w-3.5 h-3.5" />
        <span>Live Telemetry</span>
      </button>

      <button
        type="button"
        @click="setTab('templates')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'templates' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Layers class="w-3.5 h-3.5" />
        <span>Layout Templates</span>
        <Badge variant="outline" class="text-[9px] px-1 py-0 font-mono">{{ templates?.length || 0 }}</Badge>
      </button>

      <button
        type="button"
        @click="setTab('configurations')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'configurations' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <SlidersHorizontal class="w-3.5 h-3.5" />
        <span>Data Point Catalog</span>
      </button>

      <button
        type="button"
        @click="setTab('alerts')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'alerts' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Bell class="w-3.5 h-3.5" />
        <span>Alert Rules</span>
      </button>
    </div>

    <!-- Tab 1: Live Streaming -->
    <div v-if="activeTab === 'live'" class="space-y-5">
      <!-- Backend unreachable / request failed -->
      <div
        v-if="stationKpisError"
        class="flex items-center gap-3 p-3 rounded-xl border border-rose-500/30 bg-rose-500/10 text-xs text-rose-700 dark:text-rose-300"
      >
        <AlertTriangle class="w-4 h-4 shrink-0" />
        <span class="flex-1">Station telemetry could not be loaded: {{ stationKpisError }}</span>
        <Button variant="outline" size="sm" class="h-7 text-[11px] cursor-pointer" @click="refresh">Retry</Button>
      </div>

      <!-- Fleet summary (each tile filters the station directory) -->
      <div class="grid grid-cols-2 lg:grid-cols-5 gap-3">
        <button
          type="button"
          class="text-left p-3.5 rounded-xl border bg-card transition-colors cursor-pointer hover:border-primary/50"
          :class="statusFilter === 'reporting' ? 'border-primary ring-1 ring-primary/40' : 'border-border'"
          :aria-pressed="statusFilter === 'reporting'"
          @click="statusFilter = statusFilter === 'reporting' ? 'all' : 'reporting'"
        >
          <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Reporting</div>
          <div class="text-2xl font-black font-mono tabular-nums text-foreground mt-1">
            {{ fleet.reporting }}<span class="text-sm text-muted-foreground font-medium"> / {{ fleet.total }}</span>
          </div>
          <div class="text-[10px] text-muted-foreground mt-0.5">stations with a sample in the last 5 min</div>
        </button>

        <div class="p-3.5 rounded-xl border border-border bg-card">
          <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Running</div>
          <div class="text-2xl font-black font-mono tabular-nums text-emerald-600 dark:text-emerald-400 mt-1">{{ fleet.running }}</div>
          <div class="text-[10px] text-muted-foreground mt-0.5">stations in production state</div>
        </div>

        <button
          type="button"
          class="text-left p-3.5 rounded-xl border bg-card transition-colors cursor-pointer hover:border-amber-500/50"
          :class="statusFilter === 'attention' ? 'border-amber-500 ring-1 ring-amber-500/40' : 'border-border'"
          :aria-pressed="statusFilter === 'attention'"
          @click="statusFilter = statusFilter === 'attention' ? 'all' : 'attention'"
        >
          <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Needs Attention</div>
          <div
            class="text-2xl font-black font-mono tabular-nums mt-1"
            :class="fleet.attention > 0 ? 'text-amber-600 dark:text-amber-400' : 'text-foreground'"
          >
            {{ fleet.attention }}
          </div>
          <div class="text-[10px] text-muted-foreground mt-0.5">faulted, warning, critical ticket or SLA breach</div>
        </button>

        <div class="p-3.5 rounded-xl border border-border bg-card">
          <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Average OEE</div>
          <div class="text-2xl font-black font-mono tabular-nums text-foreground mt-1">
            {{ fmt(fleet.avgOee) }}<span v-if="fleet.avgOee !== null" class="text-sm text-muted-foreground font-medium"> %</span>
          </div>
          <div class="text-[10px] text-muted-foreground mt-0.5">across stations reporting OEE</div>
        </div>

        <NuxtLink
          to="/dashboard/tickets"
          class="block p-3.5 rounded-xl border border-border bg-card transition-colors hover:border-primary/50 col-span-2 lg:col-span-1"
        >
          <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Open Tickets</div>
          <div class="text-2xl font-black font-mono tabular-nums text-foreground mt-1">{{ fleet.openTickets }}</div>
          <div class="text-[10px] text-muted-foreground mt-0.5">on monitored stations · open board</div>
        </NuxtLink>
      </div>

      <!-- No stations at all -->
      <div
        v-if="!stationsLoading && stations.length === 0 && !stationKpisError"
        class="p-10 rounded-xl border border-dashed border-border text-center space-y-2"
      >
        <Factory class="w-8 h-8 mx-auto text-muted-foreground/50" />
        <p class="text-sm font-semibold text-foreground">No stations are registered for this organization</p>
        <p class="text-xs text-muted-foreground">
          Stations appear here once they exist in the machine directory.
          <NuxtLink to="/dashboard/machines" class="text-primary hover:underline">Open machines</NuxtLink>
        </p>
      </div>

      <div v-else class="grid grid-cols-1 lg:grid-cols-12 gap-5">
        <!-- Station directory -->
        <Card class="lg:col-span-4 xl:col-span-3 border border-border bg-card shadow-xs self-start">
          <CardHeader class="p-3 pb-2 space-y-2">
            <div class="relative">
              <Search class="absolute left-2.5 top-2.5 w-3.5 h-3.5 text-muted-foreground" />
              <Input
                v-model="stationSearch"
                placeholder="Filter stations or lines…"
                aria-label="Filter stations"
                class="pl-8 text-xs h-8 bg-background border-border"
              />
            </div>
            <div class="flex items-center gap-1 text-[11px]" role="group" aria-label="Station status filter">
              <button
                v-for="opt in ([['all', 'All'], ['reporting', 'Reporting'], ['attention', 'Attention'], ['silent', 'Silent']] as const)"
                :key="opt[0]"
                type="button"
                class="px-2 py-1 rounded-md font-medium transition-colors cursor-pointer"
                :class="statusFilter === opt[0] ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-muted hover:text-foreground'"
                :aria-pressed="statusFilter === opt[0]"
                @click="statusFilter = opt[0]"
              >
                {{ opt[1] }}
              </button>
            </div>
          </CardHeader>
          <CardContent class="p-2 pt-0 max-h-[640px] overflow-y-auto">
            <div v-if="stationsLoading && stations.length === 0" class="space-y-1.5 p-1">
              <div v-for="i in 6" :key="i" class="h-11 rounded-lg bg-muted/50 animate-pulse" />
            </div>
            <p v-else-if="filteredStations.length === 0" class="text-xs text-muted-foreground text-center py-8">
              No station matches this filter.
            </p>
            <div v-for="group in stationGroups" :key="group.line" class="mb-2">
              <div class="px-2 py-1 text-[10px] font-bold uppercase tracking-wider text-muted-foreground flex items-center justify-between">
                <span class="truncate">{{ group.line }}</span>
                <span class="font-mono">{{ group.items.length }}</span>
              </div>
              <button
                v-for="s in group.items"
                :key="s.stationId"
                v-ctx:station="s"
                type="button"
                class="w-full text-left px-2.5 py-2 rounded-lg flex items-center gap-2.5 transition-colors cursor-pointer focus:outline-hidden focus-visible:ring-2 focus-visible:ring-primary/50"
                :class="s.stationId === selectedStationId ? 'bg-primary/10 border border-primary/40' : 'border border-transparent hover:bg-muted/60'"
                :aria-current="s.stationId === selectedStationId ? 'true' : undefined"
                @click="selectStation(s)"
              >
                <span
                  class="size-2.5 rounded-full shrink-0"
                  :class="[STATUS_DOT[s.operationalStatus] || STATUS_DOT.UNKNOWN, isReporting(s) ? 'ring-2 ring-emerald-500/25' : '']"
                  :title="`${s.operationalStatus}${isReporting(s) ? ' · reporting' : ' · no recent sample'}`"
                />
                <span class="min-w-0 flex-1">
                  <span class="block text-xs font-semibold text-foreground truncate">{{ s.name }}</span>
                  <span class="block text-[10px] font-mono text-muted-foreground truncate">
                    {{ s.handle }} · {{ s.lastTelemetryAt ? relativeTime(s.lastTelemetryAt) : 'no telemetry' }}
                  </span>
                </span>
                <span class="text-right shrink-0">
                  <span class="block text-xs font-mono font-bold tabular-nums text-foreground">{{ s.oee !== null ? `${fmt(s.oee, 0)}%` : '—' }}</span>
                  <span
                    v-if="s.activeTicketCount > 0"
                    class="block text-[10px] font-mono"
                    :class="s.hasSlaBreach || s.criticalTicketCount > 0 ? 'text-rose-500' : 'text-amber-500'"
                  >
                    {{ s.activeTicketCount }} ticket{{ s.activeTicketCount === 1 ? '' : 's' }}
                  </span>
                </span>
              </button>
            </div>
          </CardContent>
        </Card>

        <!-- Selected station -->
        <div class="lg:col-span-8 xl:col-span-9 space-y-5">
          <div v-if="!selectedStation" class="p-10 rounded-xl border border-dashed border-border text-center text-xs text-muted-foreground">
            Select a station to see its live telemetry.
          </div>

          <template v-else>
            <!-- Station identity + navigation -->
            <div class="flex items-start justify-between gap-3 flex-wrap p-4 rounded-xl border border-border bg-card">
              <div class="min-w-0">
                <div class="flex items-center gap-2 text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
                  <span>{{ selectedStation.handle }}</span>
                  <span v-if="selectedStation.lineName">· {{ selectedStation.lineName }}</span>
                </div>
                <h2 class="text-lg font-bold text-foreground truncate">{{ selectedStation.name }}</h2>
                <div class="flex items-center gap-2 mt-1.5 flex-wrap">
                  <Badge
                    v-if="selectedStation.hasSlaBreach"
                    variant="outline"
                    class="text-[10px] border-rose-500/40 text-rose-600 dark:text-rose-400 bg-rose-500/10"
                  >
                    SLA breached
                  </Badge>
                  <Badge
                    v-if="selectedStation.criticalTicketCount > 0"
                    variant="outline"
                    class="text-[10px] border-rose-500/40 text-rose-600 dark:text-rose-400 bg-rose-500/10"
                  >
                    {{ selectedStation.criticalTicketCount }} critical
                  </Badge>
                  <span class="text-[11px] text-muted-foreground">
                    {{ selectedStation.activeTicketCount }} open ticket{{ selectedStation.activeTicketCount === 1 ? '' : 's' }}
                  </span>
                </div>
              </div>

              <div class="flex items-center gap-2 flex-wrap">
                <NuxtLink
                  :to="{ path: '/dashboard/machines', query: { search: selectedStation.name, tree: selectedStation.stationId } }"
                  class="inline-flex items-center h-8 px-3 rounded-md border border-border text-xs font-medium text-foreground hover:bg-muted gap-1.5"
                >
                  <Factory class="w-3.5 h-3.5" />
                  <span>Machine</span>
                </NuxtLink>
                <NuxtLink
                  :to="{ path: '/dashboard/map', query: { stationId: selectedStation.handle } }"
                  class="inline-flex items-center h-8 px-3 rounded-md border border-border text-xs font-medium text-foreground hover:bg-muted gap-1.5"
                >
                  <MapPin class="w-3.5 h-3.5" />
                  <span>Plant map</span>
                </NuxtLink>
                <NuxtLink
                  :to="{ path: '/dashboard/tickets', query: { stationId: selectedStation.stationId, handle: selectedStation.handle } }"
                  class="inline-flex items-center h-8 px-3 rounded-md border border-border text-xs font-medium text-foreground hover:bg-muted gap-1.5"
                >
                  <Ticket class="w-3.5 h-3.5" />
                  <span>Tickets</span>
                </NuxtLink>
                <NuxtLink
                  :to="{ path: '/dashboard/tickets/new', query: { stationId: selectedStation.stationId, stationName: selectedStation.name } }"
                  class="inline-flex items-center h-8 px-3 rounded-md bg-primary text-primary-foreground text-xs font-semibold hover:bg-primary/90 gap-1.5"
                >
                  <span>Report issue</span>
                </NuxtLink>
              </div>
            </div>

            <!-- Live dynamics -->
            <LiveTelemetryGrid
              :stream="stream"
              :station-name="selectedStation.name"
              :target-cycle-time-seconds="selectedStation.targetCycleTimeSeconds"
            />

            <div
              v-if="stream.signalState.value === 'waiting'"
              class="p-3 rounded-xl border border-dashed border-border text-xs text-muted-foreground flex items-start gap-2"
            >
              <Radio class="w-4 h-4 shrink-0 mt-0.5" />
              <span>
                No telemetry has been received from this station{{ selectedStation.lastTelemetryAt ? ' recently' : '' }}.
                Values appear as soon as its controller publishes a PLC telemetry block.
                <template v-if="enableSimulation">
                  In a development environment start the fleet simulator (<code class="font-mono text-foreground">just start simulator</code>)
                  or open the <button type="button" class="text-primary hover:underline cursor-pointer" @click="isSimulatorOpen = true">simulator controls</button>.
                </template>
              </span>
            </div>

            <!-- Production KPIs -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Card class="border border-border bg-card shadow-xs">
                <CardHeader class="p-4 pb-2">
                  <CardTitle class="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                    <Gauge class="w-4 h-4 text-primary" />
                    <span>Overall Equipment Effectiveness</span>
                  </CardTitle>
                </CardHeader>
                <CardContent class="p-4 pt-0 space-y-3">
                  <div class="flex items-baseline gap-2">
                    <span class="text-3xl font-black font-mono tabular-nums text-foreground">{{ fmt(selectedStation.oee) }}</span>
                    <span class="text-sm text-muted-foreground">%</span>
                  </div>
                  <div
                    v-for="row in [
                      { label: 'Availability', value: selectedStation.availability, color: 'bg-sky-500' },
                      { label: 'Performance', value: selectedStation.performance, color: 'bg-violet-500' },
                      { label: 'Quality', value: selectedStation.quality, color: 'bg-emerald-500' }
                    ]"
                    :key="row.label"
                    class="space-y-1"
                  >
                    <div class="flex items-center justify-between text-[11px]">
                      <span class="text-muted-foreground">{{ row.label }}</span>
                      <span class="font-mono font-semibold text-foreground">{{ row.value !== null ? `${fmt(row.value)} %` : 'not reported' }}</span>
                    </div>
                    <div class="h-1.5 rounded-full bg-muted overflow-hidden" role="presentation">
                      <div class="h-full rounded-full transition-all duration-500" :class="row.color" :style="{ width: `${pct(row.value)}%` }" />
                    </div>
                  </div>
                </CardContent>
              </Card>

              <Card class="border border-border bg-card shadow-xs">
                <CardHeader class="p-4 pb-2">
                  <CardTitle class="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                    <Factory class="w-4 h-4 text-primary" />
                    <span>Production</span>
                  </CardTitle>
                </CardHeader>
                <CardContent class="p-4 pt-0 space-y-3">
                  <div class="space-y-1">
                    <div class="flex items-center justify-between text-[11px]">
                      <span class="text-muted-foreground">Throughput</span>
                      <span class="font-mono font-semibold text-foreground">
                        {{ fmtInt(selectedStation.partsPerHour) }}
                        <span class="text-muted-foreground font-normal">/ {{ fmtInt(selectedStation.targetPartsPerHour) }} parts/h</span>
                      </span>
                    </div>
                    <div class="h-1.5 rounded-full bg-muted overflow-hidden" role="presentation">
                      <div
                        class="h-full rounded-full transition-all duration-500"
                        :class="throughputPct !== null && throughputPct < 85 ? 'bg-amber-500' : 'bg-emerald-500'"
                        :style="{ width: `${pct(throughputPct)}%` }"
                      />
                    </div>
                  </div>

                  <div class="grid grid-cols-2 gap-2.5 pt-1">
                    <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
                      <div class="text-[9px] uppercase font-semibold text-muted-foreground">Cycle time</div>
                      <div class="text-sm font-mono font-bold text-foreground mt-0.5">
                        {{ fmt(selectedStation.cycleTimeSeconds, 1) }} s
                        <span class="text-[10px] text-muted-foreground font-normal">/ {{ fmt(selectedStation.targetCycleTimeSeconds, 1) }} s</span>
                      </div>
                    </div>
                    <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
                      <div class="text-[9px] uppercase font-semibold text-muted-foreground">First-pass yield</div>
                      <div class="text-sm font-mono font-bold text-foreground mt-0.5">{{ fmt(selectedStation.yield, 2) }} %</div>
                    </div>
                    <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
                      <div class="text-[9px] uppercase font-semibold text-muted-foreground">Parts produced</div>
                      <div class="text-sm font-mono font-bold text-foreground mt-0.5">{{ fmtInt(selectedStation.totalPartsProduced) }}</div>
                    </div>
                    <div class="p-2.5 rounded-lg bg-muted/40 border border-border">
                      <div class="text-[9px] uppercase font-semibold text-muted-foreground">Scrap</div>
                      <div class="text-sm font-mono font-bold text-foreground mt-0.5">{{ fmtInt(selectedStation.scrapParts) }}</div>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>

            <!-- Controllers bound to the station -->
            <Card class="border border-border bg-card shadow-xs">
              <CardHeader class="p-4 pb-2">
                <CardTitle class="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                  <Monitor class="w-4 h-4 text-primary" />
                  <span>Controllers</span>
                  <Badge variant="outline" class="text-[9px] font-mono">{{ stationControllers.length }}</Badge>
                </CardTitle>
              </CardHeader>
              <CardContent class="p-4 pt-0">
                <p v-if="stationControllers.length === 0" class="text-xs text-muted-foreground">
                  No controller is bound to this station, so nothing can publish telemetry for it.
                </p>
                <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-2.5">
                  <NuxtLink
                    v-for="c in stationControllers"
                    :key="c.id"
                    v-ctx:controller="c"
                    :to="{ path: '/dashboard/clients', query: { selected: c.id, hostname: c.hostname } }"
                    class="p-3 rounded-lg border border-border bg-muted/20 hover:border-primary/50 hover:bg-muted/40 transition-colors flex items-center gap-3 group"
                  >
                    <span class="size-2.5 rounded-full shrink-0" :class="c.telemetry?.isOnline ? 'bg-emerald-500' : 'bg-muted-foreground/40'" />
                    <span class="min-w-0 flex-1">
                      <span class="block text-xs font-semibold font-mono text-foreground truncate">{{ c.hostname || c.name }}</span>
                      <span class="block text-[10px] font-mono text-muted-foreground truncate">
                        {{ c.ipAddress || 'no address' }} · {{ c.telemetry?.isOnline ? 'online' : 'offline' }}
                      </span>
                    </span>
                    <span class="text-right text-[10px] font-mono text-muted-foreground shrink-0">
                      <span class="block">CPU {{ c.telemetry?.cpuUsagePercent != null ? `${Math.round(c.telemetry.cpuUsagePercent)}%` : '—' }}</span>
                      <span class="block">RAM {{ c.telemetry?.ramUsagePercent != null ? `${Math.round(c.telemetry.ramUsagePercent)}%` : '—' }}</span>
                    </span>
                    <ChevronRight class="w-4 h-4 text-muted-foreground group-hover:text-primary shrink-0" />
                  </NuxtLink>
                </div>
              </CardContent>
            </Card>
          </template>
        </div>
      </div>
    </div>

    <!-- Tab 2: Layout Templates -->
    <div v-else-if="activeTab === 'templates'" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <Card
        v-for="tmpl in (templates || [])"
        :key="tmpl.recipeId || (tmpl as any).id"
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
            <Badge v-if="(activeTemplate?.recipeId || (activeTemplate as any)?.id) === (tmpl.recipeId || (tmpl as any).id)" variant="default" class="text-[10px] px-2 py-0.5">
              Active
            </Badge>
          </div>
        </CardHeader>
        <CardContent class="pt-0 space-y-3">
          <div class="text-[11px] text-muted-foreground font-mono flex items-center justify-between">
            <span>Data Points: {{ tmpl.dataPoints?.length || (tmpl as any).widgets?.length || 4 }}</span>
            <span>Platform: {{ tmpl.targetSelector?.osPlatform || (tmpl as any).layoutType || 'Windows IPC' }}</span>
          </div>
          <Button
            variant="outline"
            size="sm"
            @click="setActiveTemplate(tmpl)"
            class="w-full text-xs cursor-pointer"
          >
            {{ (activeTemplate?.recipeId || (activeTemplate as any)?.id) === (tmpl.recipeId || (tmpl as any).id) ? 'Currently Applied' : 'Apply Layout Template' }}
          </Button>
        </CardContent>
      </Card>
    </div>

    <!-- Tab 3: Data Point Catalog -->
    <div v-else-if="activeTab === 'configurations'" class="space-y-4">
      <div class="flex items-center justify-between gap-3 p-3 bg-muted/20 border border-border rounded-xl">
        <div class="relative flex-1 max-w-sm">
          <Search class="absolute left-2.5 top-2.5 w-3.5 h-3.5 text-muted-foreground" />
          <Input
            v-model="catalogSearch"
            placeholder="Search PLC symbols or telemetry keys..."
            class="pl-8 text-xs h-8 bg-background border-border"
          />
        </div>
        <Badge variant="outline" class="text-[10px] font-mono">
          {{ filteredCatalog.length }} Signals Registered
        </Badge>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <Card v-for="item in filteredCatalog" :key="item.id" class="border border-border bg-card shadow-xs">
          <CardHeader class="pb-2">
            <div class="flex items-start justify-between">
              <div>
                <span class="text-[10px] font-mono text-cyan-500 uppercase">{{ item.category }}</span>
                <CardTitle class="text-xs font-bold text-foreground mt-0.5">{{ item.name }}</CardTitle>
              </div>
              <Badge variant="outline" class="text-[9px] font-mono">{{ item.unit || 'raw' }}</Badge>
            </div>
          </CardHeader>
          <CardContent class="text-xs space-y-1 font-mono text-muted-foreground">
            <div class="truncate text-[10px]">Symbol: {{ item.pathOrSymbol }}</div>
            <div class="text-[10px]">Freq: {{ item.updateFrequencyMs || 100 }}ms</div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- Tab 4: Alert Rules -->
    <div v-else-if="activeTab === 'alerts'">
      <AlertRulesManager />
    </div>

    <!-- Development fleet simulator controls (station faults, drift scenarios, line stops) -->
    <SimulatorControlModal v-if="enableSimulation" v-model:open="isSimulatorOpen" />
  </div>
</template>
