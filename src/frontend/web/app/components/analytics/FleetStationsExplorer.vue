<script setup lang="ts">
/**
 * Fleet overview: KPI tiles, one filter row and the grouped station list in its own scrolling
 * viewport (sticky column and group headers, only the rows in view are rendered).
 *
 * Every figure is a roll-up of the rows from /api/analytics/fleet-stations (see
 * `~/utils/fleetAnalytics`); a value no station reports is shown as "No data" / "—".
 * The view (grouping, columns, sort, filters, window, collapsed groups, density, refresh interval)
 * is stored per browser.
 */
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useElementSize } from '@vueuse/core'
import {
  Activity, AlertTriangle, ArrowDown, ArrowUp, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown,
  Columns3, RefreshCw, RotateCcw, Rows2, Rows3, Search, Ticket
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Popover, PopoverContent, PopoverTrigger } from '~/components/ui/popover'
import { useFleetAnalytics } from '~/composables/useFleetAnalytics'
import {
  AGGREGATION_PREFIX, AUTO_REFRESH_OPTIONS, CATEGORY_LABELS, COLUMN_PRESETS, FLEET_COLUMNS, GROUP_DIMENSIONS,
  LIVE_STATE_META, NO_DATA_MARK, WINDOW_OPTIONS,
  activeFilterCount, computeVirtualWindow, dimensionOptions, filterStations, formatAge, formatColumnValue,
  formatDurationMinutes, getColumn, groupStations, loadViewState, orderGroups, rollUp, rollupCellText,
  rollupExplanation, sanitizeViewState, saveViewState, sortStations, stateOptions, stationCellText,
  stationContextData, stationMachineLink, stationTelemetryLink, stationTicketsLink,
  type ColumnCategory, type FleetColumn, type FleetRollup, type FleetStation, type FleetViewState, type LiveState
} from '~/utils/fleetAnalytics'

const NAME_COLUMN_WIDTH = 340
const COLUMN_HEADER_HEIGHT = 36
const GROUP_HEADER_HEIGHT = 44

const { stations, meta, isLoading, error, hasLoaded, context, refresh } = useFleetAnalytics()

// Server render and first client render use the default view; the stored one is applied on mount.
const view = ref<FleetViewState>(sanitizeViewState(null))
const viewReady = ref(false)

const reload = () => refresh(view.value.windowDays)

let refreshTimer: ReturnType<typeof setInterval> | null = null
const stopTimer = () => {
  if (refreshTimer) clearInterval(refreshTimer)
  refreshTimer = null
}
const startTimer = () => {
  stopTimer()
  const seconds = view.value.autoRefreshSeconds
  if (seconds <= 0) return
  refreshTimer = setInterval(() => {
    // A hidden tab keeps its last data; it reloads on the first tick after it is visible again.
    if (typeof document === 'undefined' || !document.hidden) reload()
  }, seconds * 1000)
}

onMounted(() => {
  view.value = loadViewState()
  viewReady.value = true
  reload()
  startTimer()
})
onBeforeUnmount(stopTimer)

watch(view, (value) => { if (viewReady.value) saveViewState(value) }, { deep: true })
watch(() => view.value.windowDays, () => { if (viewReady.value) reload() })
watch(() => view.value.autoRefreshSeconds, () => { if (viewReady.value) startTimer() })

// ── derived rows ───────────────────────────────────────────────────────────
const visibleColumns = computed<FleetColumn[]>(() =>
  view.value.columns.map(getColumn).filter((c): c is FleetColumn => !!c)
)
const filtered = computed(() => filterStations(stations.value, view.value.filters))
const sorted = computed(() => sortStations(filtered.value, view.value.sort, context.value))
const groups = computed(() =>
  orderGroups(groupStations(sorted.value, view.value.groupBy, context.value), view.value.groupOrder, view.value.sort)
)
const fleet = computed<FleetRollup>(() => rollUp(filtered.value, context.value))
const filterCount = computed(() => activeFilterCount(view.value.filters))

const windowLabel = computed(() => WINDOW_OPTIONS.find(o => o.days === view.value.windowDays)?.label ?? `${view.value.windowDays} d`)
const generatedAtText = computed(() => {
  const at = meta.value?.generatedAt ? new Date(meta.value.generatedAt) : null
  return at && !Number.isNaN(at.getTime()) ? at.toLocaleTimeString() : null
})

// ── KPI tiles ──────────────────────────────────────────────────────────────
interface Tile {
  key: string
  label: string
  /** null renders an explicit "No data". */
  value: string | null
  unit: string
  detail: string
}

const whole = (value: number) => Math.round(value).toLocaleString('en-US')

const tiles = computed<Tile[]>(() => {
  const r = fleet.value
  const metric = (key: string) => r.metrics[key] ?? { value: null, n: 0 }
  const shown = (key: string) => {
    const column = getColumn(key)
    const value = metric(key).value
    return column && value !== null ? formatColumnValue(value, column) : null
  }
  const total = r.stationCount
  const all = stations.value.length
  const oee = metric('oee')
  const attainment = metric('attainment')
  const mttr = metric('mttr')
  const controllers = metric('controllers')
  const slaHours = meta.value?.criticalSlaHours ?? 0
  const onlineMinutes = Math.round((meta.value?.controllerOnlineWindowSeconds ?? 0) / 60)

  return [
    {
      key: 'stations',
      label: total === all ? 'Stations' : `Stations (filtered, of ${all})`,
      value: hasLoaded.value ? whole(total) : null,
      unit: '',
      detail: `${r.reporting} reporting · ${r.states.STALE} stale · ${r.states.NO_DATA} no data`
    },
    {
      key: 'oee',
      label: 'OEE (mean)',
      value: shown('oee'),
      unit: '%',
      detail: oee.n > 0 ? `Mean of the ${oee.n} of ${total} stations that report OEE` : 'No station reports OEE'
    },
    {
      key: 'attainment',
      label: 'Throughput vs target',
      value: shown('attainment'),
      unit: '%',
      detail: attainment.n > 0
        ? `${whole(attainment.numerator ?? 0)} of ${whole(attainment.denominator ?? 0)} parts/h summed over ${attainment.n} stations`
        : 'No station reports throughput and target'
    },
    {
      key: 'tickets',
      label: 'Open tickets',
      value: hasLoaded.value ? shown('openTickets') : null,
      unit: '',
      detail: `${whole(metric('criticalTickets').value ?? 0)} critical · ${whole(metric('slaBreaches').value ?? 0)} stations with a critical ticket open over ${slaHours} h`
    },
    {
      key: 'incidents',
      label: `Incidents, last ${windowLabel.value}`,
      value: hasLoaded.value ? shown('incidents') : null,
      unit: '',
      detail: mttr.value !== null
        ? `MTTR ${formatDurationMinutes(mttr.value)} over ${whole(mttr.denominator ?? 0)} resolved`
        : 'No ticket of the window is resolved yet'
    },
    {
      key: 'controllers',
      label: 'Stations with PCs online',
      value: controllers.n > 0 ? `${controllers.numerator}/${controllers.denominator}` : null,
      unit: '',
      detail: controllers.n > 0
        ? `All bound client PCs seen in the last ${onlineMinutes} min · ${total - controllers.n} stations have no controller`
        : 'No controller is bound to these stations'
    }
  ]
})

/** Bar segments of a roll-up: states with telemetry first, the "no data" share as the track. */
const BAR_STATES: LiveState[] = ['RUNNING', 'IDLE', 'WARNING', 'FAULTED', 'MAINTENANCE', 'UNKNOWN', 'STALE']
const barSegments = (rollup: FleetRollup) =>
  BAR_STATES.filter(state => rollup.states[state] > 0).map(state => ({ state, count: rollup.states[state] }))
const stateSummary = (rollup: FleetRollup) =>
  [...BAR_STATES, 'NO_DATA' as LiveState]
    .filter(state => rollup.states[state] > 0)
    .map(state => `${rollup.states[state]} ${LIVE_STATE_META[state].label.toLowerCase()}`)
    .join(' · ') || 'No stations'

// ── filters ────────────────────────────────────────────────────────────────
type ListFilterKey = 'lines' | 'organizations' | 'machineTypes'

const listFilters = computed(() => {
  const defs: { key: ListFilterKey; label: string; dimension: 'line' | 'organization' | 'machineType' }[] = [
    { key: 'lines', label: 'Line', dimension: 'line' },
    { key: 'organizations', label: 'Organization', dimension: 'organization' },
    { key: 'machineTypes', label: 'Machine type', dimension: 'machineType' }
  ]
  return defs
    .map(def => ({ ...def, options: dimensionOptions(stations.value, def.dimension), selected: view.value.filters[def.key] }))
    // A dimension with a single value (one organization for a tenant user) is not a useful filter.
    .filter(def => def.options.length > 1 || def.selected.length > 0)
})
const stateChips = computed(() => stateOptions(stations.value))

const toggleListFilter = (key: ListFilterKey, option: string) => {
  const list = view.value.filters[key]
  const index = list.indexOf(option)
  if (index === -1) list.push(option)
  else list.splice(index, 1)
}
const clearListFilter = (key: ListFilterKey) => { view.value.filters[key] = [] }
const toggleState = (state: string) => {
  const list = view.value.filters.states
  const index = list.indexOf(state as LiveState)
  if (index === -1) list.push(state as LiveState)
  else list.splice(index, 1)
}
const clearFilters = () => {
  view.value.filters = { search: '', lines: [], organizations: [], machineTypes: [], states: [], attentionOnly: false }
}
const resetView = () => { view.value = sanitizeViewState(null) }

// ── columns & sorting ──────────────────────────────────────────────────────
const columnCategories = computed(() => {
  const order: ColumnCategory[] = ['identity', 'production', 'condition', 'maintenance', 'connectivity']
  return order.map(category => ({
    category,
    label: CATEGORY_LABELS[category],
    columns: FLEET_COLUMNS.filter(c => c.category === category)
  }))
})
const toggleColumn = (key: string) => {
  const index = view.value.columns.indexOf(key)
  if (index !== -1) {
    view.value.columns.splice(index, 1)
    return
  }
  // Keep the catalog order so a re-enabled column returns to its usual place.
  const order = FLEET_COLUMNS.map(c => c.key)
  view.value.columns = [...view.value.columns, key].sort((a, b) => order.indexOf(a) - order.indexOf(b))
}
const applyPreset = (columns: string[]) => { view.value.columns = [...columns] }

const toggleSort = (key: string) => {
  const sort = view.value.sort
  if (sort.key === key) {
    view.value.sort = { key, direction: sort.direction === 'asc' ? 'desc' : 'asc' }
    return
  }
  const column = getColumn(key)
  // Numbers start with the largest first, names and other text with A.
  view.value.sort = { key, direction: column?.kind === 'metric' ? 'desc' : 'asc' }
}
const sortableColumns = computed(() => [{ key: 'name', label: 'Station name' }, ...visibleColumns.value.map(c => ({ key: c.key, label: c.label }))])

// ── grouping ───────────────────────────────────────────────────────────────
const collapsedKeys = computed(() => new Set(view.value.collapsed[view.value.groupBy] ?? []))
const isCollapsed = (key: string) => collapsedKeys.value.has(key)
const toggleGroup = (key: string) => {
  const next = new Set(collapsedKeys.value)
  if (next.has(key)) next.delete(key)
  else next.add(key)
  view.value.collapsed = { ...view.value.collapsed, [view.value.groupBy]: Array.from(next) }
}
const collapseAll = () => {
  view.value.collapsed = { ...view.value.collapsed, [view.value.groupBy]: groups.value.map(g => g.key) }
}
const expandAll = () => {
  view.value.collapsed = { ...view.value.collapsed, [view.value.groupBy]: [] }
}

// ── scrolling viewport ─────────────────────────────────────────────────────
const viewport = ref<HTMLElement | null>(null)
const { height: viewportHeight } = useElementSize(viewport)
const scrollTop = ref(0)
let scrollFrame = 0
const onScroll = () => {
  if (scrollFrame) return
  scrollFrame = requestAnimationFrame(() => {
    scrollFrame = 0
    scrollTop.value = viewport.value?.scrollTop ?? 0
  })
}
onBeforeUnmount(() => { if (scrollFrame) cancelAnimationFrame(scrollFrame) })

const rowHeight = computed(() => (view.value.density === 'compact' ? 32 : 44))
const gridTemplateColumns = computed(() =>
  [`${NAME_COLUMN_WIDTH}px`, ...visibleColumns.value.map(c => `${c.width}px`)].join(' ')
)
const totalWidth = computed(() => NAME_COLUMN_WIDTH + visibleColumns.value.reduce((sum, c) => sum + c.width, 0))

const virtual = computed(() =>
  computeVirtualWindow(
    groups.value.map(g => ({ rowCount: g.rows.length, collapsed: isCollapsed(g.key) })),
    {
      // The sticky column header covers the first pixels of the viewport.
      scrollTop: scrollTop.value,
      viewportHeight: viewportHeight.value,
      rowHeight: rowHeight.value,
      headerHeight: GROUP_HEADER_HEIGHT
    }
  )
)
const visibleRows = (index: number): FleetStation[] => {
  const section = virtual.value.sections[index]
  const group = groups.value[index]
  return section && group ? group.rows.slice(section.start, section.end) : []
}

const stateTitle = (row: FleetStation) => {
  const metaState = LIVE_STATE_META[row.liveState]
  const last = row.lastTelemetryAt ? `Last telemetry ${formatAge(row.telemetryAgeSeconds)} ago` : 'Never reported telemetry'
  const reported = row.liveState === 'STALE' ? ` Last reported state: ${row.operationalStatus}.` : ''
  return `${metaState.description} ${last}.${reported}`
}
</script>

<template>
  <div class="flex flex-col gap-3 min-h-0 h-full" data-testid="fleet-explorer">
    <!-- KPI tiles: roll-ups of the stations that pass the filters -->
    <div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-3 shrink-0">
      <div
        v-for="tile in tiles"
        :key="tile.key"
        class="rounded-xl border border-border bg-card px-4 py-3 min-w-0"
        :data-testid="`fleet-tile-${tile.key}`"
      >
        <div class="text-[11px] font-medium text-muted-foreground truncate" :title="tile.label">{{ tile.label }}</div>
        <div class="mt-1 flex items-baseline gap-1.5 min-w-0">
          <span v-if="tile.value !== null" class="text-2xl font-semibold text-foreground leading-tight">{{ tile.value }}</span>
          <span v-else class="text-base font-medium text-muted-foreground leading-8">No data</span>
          <span v-if="tile.value !== null && tile.unit" class="text-xs text-muted-foreground">{{ tile.unit }}</span>
        </div>
        <div
          v-if="tile.key === 'stations' && fleet.stationCount > 0"
          class="mt-1.5 flex h-2 w-full gap-0.5"
          role="img"
          :aria-label="`Live state of the stations: ${stateSummary(fleet)}`"
          :title="stateSummary(fleet)"
        >
          <span
            v-for="segment in barSegments(fleet)"
            :key="segment.state"
            class="h-full rounded-full"
            :class="LIVE_STATE_META[segment.state].dot"
            :style="{ flex: `${segment.count} 1 0%` }"
          />
          <span v-if="fleet.states.NO_DATA > 0" class="h-full rounded-full bg-muted" :style="{ flex: `${fleet.states.NO_DATA} 1 0%` }" />
        </div>
        <div class="mt-1 text-[11px] leading-snug text-muted-foreground line-clamp-2" :title="tile.detail">{{ tile.detail }}</div>
      </div>
    </div>

    <!-- One filter row for everything below it -->
    <div class="rounded-xl border border-border bg-card px-3 py-2.5 space-y-2 shrink-0">
      <div class="flex flex-wrap items-center gap-2">
        <label class="flex items-center gap-1.5 text-[11px] text-muted-foreground">
          <span>Tickets of the last</span>
          <select
            v-model.number="view.windowDays"
            class="h-8 rounded-md border border-border bg-background px-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            data-testid="fleet-window"
          >
            <option v-for="option in WINDOW_OPTIONS" :key="option.days" :value="option.days">{{ option.label }}</option>
          </select>
        </label>

        <label class="flex items-center gap-1.5 text-[11px] text-muted-foreground">
          <span>Group by</span>
          <select
            v-model="view.groupBy"
            class="h-8 rounded-md border border-border bg-background px-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            data-testid="fleet-group-by"
          >
            <option v-for="dimension in GROUP_DIMENSIONS" :key="dimension.key" :value="dimension.key">{{ dimension.label }}</option>
          </select>
        </label>

        <div class="relative">
          <Search class="absolute left-2 top-2 size-3.5 text-muted-foreground" />
          <input
            v-model="view.filters.search"
            type="search"
            placeholder="Station, line, type…"
            aria-label="Search stations"
            class="h-8 w-44 rounded-md border border-border bg-background pl-7 pr-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            data-testid="fleet-search"
          >
        </div>

        <Popover v-for="filter in listFilters" :key="filter.key">
          <PopoverTrigger as-child>
            <Button variant="outline" size="sm" class="h-8 text-xs gap-1.5" :data-testid="`fleet-filter-${filter.key}`">
              <span>{{ filter.label }}</span>
              <Badge v-if="filter.selected.length" class="px-1.5 py-0 text-[10px]">{{ filter.selected.length }}</Badge>
              <ChevronDown class="size-3 text-muted-foreground" />
            </Button>
          </PopoverTrigger>
          <PopoverContent align="start" class="w-80 p-2 max-h-80 overflow-y-auto custom-scrollbar">
            <label
              v-for="option in filter.options"
              :key="option.key"
              class="flex items-center gap-2 rounded px-2 py-1.5 text-xs cursor-pointer hover:bg-muted"
            >
              <input
                type="checkbox"
                class="rounded border-border"
                :checked="filter.selected.includes(option.key)"
                @change="toggleListFilter(filter.key, option.key)"
              >
              <span class="flex-1 truncate" :title="option.label">{{ option.label }}</span>
              <span class="font-mono text-muted-foreground">{{ option.count }}</span>
            </label>
            <button
              v-if="filter.selected.length"
              type="button"
              class="mt-1 w-full rounded px-2 py-1 text-left text-[11px] text-muted-foreground hover:bg-muted cursor-pointer"
              @click="clearListFilter(filter.key)"
            >
              Clear {{ filter.label.toLowerCase() }} filter
            </button>
          </PopoverContent>
        </Popover>

        <button
          type="button"
          class="h-8 inline-flex items-center gap-1.5 rounded-md border px-2.5 text-xs cursor-pointer transition-colors"
          :class="view.filters.attentionOnly ? 'border-foreground/40 bg-muted text-foreground font-medium' : 'border-border text-muted-foreground hover:text-foreground'"
          :aria-pressed="view.filters.attentionOnly"
          title="Faulted, warning or stale stations, open critical tickets and SLA breaches"
          data-testid="fleet-attention"
          @click="view.filters.attentionOnly = !view.filters.attentionOnly"
        >
          <AlertTriangle class="size-3.5" />
          <span>Needs attention</span>
          <span class="font-mono">{{ fleet.attention }}</span>
        </button>

        <div class="ml-auto flex flex-wrap items-center gap-2">
          <Popover>
            <PopoverTrigger as-child>
              <Button variant="outline" size="sm" class="h-8 text-xs gap-1.5" data-testid="fleet-columns">
                <Columns3 class="size-3.5" />
                <span>Columns</span>
                <span class="font-mono text-muted-foreground">{{ visibleColumns.length }}</span>
              </Button>
            </PopoverTrigger>
            <PopoverContent align="end" class="w-96 p-3 max-h-[28rem] overflow-y-auto custom-scrollbar space-y-3">
              <div class="flex flex-wrap items-center gap-1.5">
                <span class="text-[11px] text-muted-foreground">Presets</span>
                <button
                  v-for="preset in COLUMN_PRESETS"
                  :key="preset.key"
                  type="button"
                  class="rounded-md border border-border px-2 py-1 text-[11px] text-foreground hover:bg-muted cursor-pointer"
                  @click="applyPreset(preset.columns)"
                >
                  {{ preset.label }}
                </button>
              </div>
              <div v-for="group in columnCategories" :key="group.category" class="space-y-1">
                <div class="text-[11px] font-medium text-muted-foreground">{{ group.label }}</div>
                <label
                  v-for="column in group.columns"
                  :key="column.key"
                  class="flex items-start gap-2 rounded px-2 py-1 text-xs cursor-pointer hover:bg-muted"
                  :title="column.description"
                >
                  <input
                    type="checkbox"
                    class="mt-0.5 rounded border-border"
                    :checked="view.columns.includes(column.key)"
                    @change="toggleColumn(column.key)"
                  >
                  <span class="flex-1">
                    <span class="text-foreground">{{ column.label }}</span>
                    <span v-if="column.unit" class="text-muted-foreground"> · {{ column.unit }}</span>
                    <span class="block text-[10px] leading-snug text-muted-foreground">{{ column.description }}</span>
                  </span>
                </label>
              </div>
            </PopoverContent>
          </Popover>

          <label class="flex items-center gap-1.5 text-[11px] text-muted-foreground">
            <span>Sort</span>
            <select
              :value="view.sort.key"
              class="h-8 max-w-36 rounded-md border border-border bg-background px-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
              data-testid="fleet-sort"
              @change="toggleSort(($event.target as HTMLSelectElement).value)"
            >
              <option v-for="option in sortableColumns" :key="option.key" :value="option.key">{{ option.label }}</option>
            </select>
            <button
              type="button"
              class="h-8 w-8 inline-flex items-center justify-center rounded-md border border-border text-foreground hover:bg-muted cursor-pointer"
              :title="view.sort.direction === 'asc' ? 'Ascending — click for descending' : 'Descending — click for ascending'"
              @click="toggleSort(view.sort.key)"
            >
              <ArrowUp v-if="view.sort.direction === 'asc'" class="size-3.5" />
              <ArrowDown v-else class="size-3.5" />
            </button>
          </label>

          <label v-if="view.groupBy !== 'none'" class="flex items-center gap-1.5 text-[11px] text-muted-foreground">
            <span>Groups</span>
            <select
              v-model="view.groupOrder"
              class="h-8 rounded-md border border-border bg-background px-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
              title="Order of the groups: by name, or by the roll-up of the sorted column"
            >
              <option value="label">A–Z</option>
              <option value="metric">by sorted column</option>
            </select>
          </label>

          <button
            type="button"
            class="h-8 w-8 inline-flex items-center justify-center rounded-md border border-border text-foreground hover:bg-muted cursor-pointer"
            :title="view.density === 'compact' ? 'Compact rows — click for comfortable rows' : 'Comfortable rows — click for compact rows'"
            data-testid="fleet-density"
            @click="view.density = view.density === 'compact' ? 'comfortable' : 'compact'"
          >
            <Rows3 v-if="view.density === 'compact'" class="size-3.5" />
            <Rows2 v-else class="size-3.5" />
          </button>

          <label class="flex items-center gap-1.5 text-[11px] text-muted-foreground">
            <span>Refresh</span>
            <select
              v-model.number="view.autoRefreshSeconds"
              class="h-8 rounded-md border border-border bg-background px-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option v-for="option in AUTO_REFRESH_OPTIONS" :key="option.seconds" :value="option.seconds">{{ option.label }}</option>
            </select>
          </label>
          <Button variant="outline" size="sm" class="h-8 text-xs gap-1.5" data-testid="fleet-refresh" @click="reload">
            <RefreshCw class="size-3.5" :class="{ 'animate-spin': isLoading }" />
            <span>Refresh</span>
          </Button>
          <button
            type="button"
            class="h-8 w-8 inline-flex items-center justify-center rounded-md border border-border text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer"
            title="Reset grouping, columns, sorting and filters to the default view"
            data-testid="fleet-reset"
            @click="resetView"
          >
            <RotateCcw class="size-3.5" />
          </button>
        </div>
      </div>

      <!-- State legend; each entry also filters by that state -->
      <div class="flex flex-wrap items-center gap-1.5 text-[11px]">
        <span class="text-muted-foreground">Live state</span>
        <button
          v-for="chip in stateChips"
          :key="chip.key"
          type="button"
          class="inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 cursor-pointer transition-colors"
          :class="view.filters.states.includes(chip.key as LiveState) ? 'border-foreground/40 bg-muted text-foreground font-medium' : 'border-border text-muted-foreground hover:text-foreground'"
          :aria-pressed="view.filters.states.includes(chip.key as LiveState)"
          :title="LIVE_STATE_META[chip.key as LiveState].description"
          :data-testid="`fleet-state-${chip.key}`"
          @click="toggleState(chip.key)"
        >
          <span class="size-2 rounded-full" :class="LIVE_STATE_META[chip.key as LiveState].dot" />
          <span>{{ chip.label }}</span>
          <span class="font-mono">{{ chip.count }}</span>
        </button>
        <span v-if="hasLoaded && stateChips.length === 0" class="text-muted-foreground">no stations</span>
        <button
          v-if="filterCount > 0"
          type="button"
          class="ml-1 rounded-full border border-border px-2 py-0.5 text-muted-foreground hover:text-foreground cursor-pointer"
          data-testid="fleet-clear-filters"
          @click="clearFilters"
        >
          Clear {{ filterCount }} filter{{ filterCount === 1 ? '' : 's' }}
        </button>
        <span v-if="view.groupBy !== 'none' && groups.length > 1" class="ml-auto flex items-center gap-1">
          <button type="button" class="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-muted-foreground hover:text-foreground cursor-pointer" @click="collapseAll">
            <ChevronsDownUp class="size-3" /> Collapse all
          </button>
          <button type="button" class="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-muted-foreground hover:text-foreground cursor-pointer" @click="expandAll">
            <ChevronsUpDown class="size-3" /> Expand all
          </button>
        </span>
      </div>
    </div>

    <!-- Grouped station list: scrolls inside this card -->
    <div class="flex-1 min-h-[360px] lg:min-h-0 rounded-xl border border-border bg-card overflow-hidden flex flex-col">
      <div
        v-if="error"
        class="shrink-0 border-b border-border bg-muted px-3 py-2 text-xs text-foreground flex items-center gap-2"
        role="alert"
        data-testid="fleet-error"
      >
        <AlertTriangle class="size-3.5 text-amber-600 dark:text-amber-400 shrink-0" />
        <span>Fleet data could not be loaded: {{ error }}.<template v-if="hasLoaded"> Showing the data of {{ generatedAtText }}.</template></span>
      </div>

      <div v-if="!hasLoaded && !error" class="flex-1 flex items-center justify-center text-xs text-muted-foreground" data-testid="fleet-loading">
        Loading fleet stations…
      </div>
      <div v-else-if="!hasLoaded" class="flex-1 flex items-center justify-center text-xs text-muted-foreground">
        No fleet data to show.
      </div>
      <div v-else-if="stations.length === 0" class="flex-1 flex flex-col items-center justify-center gap-1 text-xs text-muted-foreground" data-testid="fleet-empty">
        <span class="text-sm text-foreground">No stations</span>
        <span>No machine is registered for your organization.</span>
      </div>
      <div v-else-if="filtered.length === 0" class="flex-1 flex flex-col items-center justify-center gap-2 text-xs text-muted-foreground" data-testid="fleet-no-match">
        <span class="text-sm text-foreground">No station matches the filters</span>
        <Button variant="outline" size="sm" class="h-8 text-xs" @click="clearFilters">Clear filters</Button>
      </div>

      <div
        v-else
        ref="viewport"
        class="flex-1 min-h-0 overflow-auto custom-scrollbar"
        :class="{ 'opacity-70': isLoading }"
        role="table"
        aria-label="Fleet stations"
        :aria-rowcount="filtered.length"
        data-testid="fleet-viewport"
        @scroll.passive="onScroll"
      >
        <div :style="{ minWidth: `${totalWidth}px` }">
          <!-- Column header -->
          <div
            class="sticky top-0 z-30 grid items-stretch border-b border-border bg-muted text-[11px] font-medium text-muted-foreground"
            :style="{ gridTemplateColumns, height: `${COLUMN_HEADER_HEIGHT}px` }"
            role="row"
          >
            <button
              type="button"
              class="sticky left-0 z-10 bg-muted px-3 flex items-center gap-1 text-left cursor-pointer hover:text-foreground"
              role="columnheader"
              :aria-sort="view.sort.key === 'name' ? (view.sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'"
              @click="toggleSort('name')"
            >
              <span>Station</span>
              <ArrowUp v-if="view.sort.key === 'name' && view.sort.direction === 'asc'" class="size-3" />
              <ArrowDown v-else-if="view.sort.key === 'name'" class="size-3" />
            </button>
            <button
              v-for="column in visibleColumns"
              :key="column.key"
              type="button"
              class="px-3 flex items-center gap-1 cursor-pointer hover:text-foreground min-w-0"
              :class="column.kind === 'metric' ? 'justify-end text-right' : 'text-left'"
              role="columnheader"
              :title="column.description"
              :aria-sort="view.sort.key === column.key ? (view.sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'"
              :data-testid="`fleet-col-${column.key}`"
              @click="toggleSort(column.key)"
            >
              <span class="truncate">{{ column.label }}</span>
              <span v-if="column.unit" class="shrink-0 font-normal opacity-70">{{ column.unit }}</span>
              <ArrowUp v-if="view.sort.key === column.key && view.sort.direction === 'asc'" class="size-3 shrink-0" />
              <ArrowDown v-else-if="view.sort.key === column.key" class="size-3 shrink-0" />
            </button>
          </div>

          <section
            v-for="(group, index) in groups"
            :key="group.key"
            :style="{ height: `${virtual.sections[index]?.height ?? GROUP_HEADER_HEIGHT}px` }"
            :data-testid="`fleet-group-${group.key}`"
          >
            <!-- Group header with the roll-up of each visible column -->
            <div
              class="sticky z-20 grid items-stretch border-b border-border bg-card text-xs"
              :style="{ top: `${COLUMN_HEADER_HEIGHT}px`, gridTemplateColumns, height: `${GROUP_HEADER_HEIGHT}px` }"
              role="row"
            >
              <button
                type="button"
                class="sticky left-0 z-10 bg-card px-3 flex items-center gap-2 text-left cursor-pointer min-w-0 hover:bg-muted"
                :aria-expanded="!isCollapsed(group.key)"
                :title="`${group.label} — ${stateSummary(group.rollup)}`"
                @click="toggleGroup(group.key)"
              >
                <ChevronRight class="size-3.5 shrink-0 text-muted-foreground transition-transform" :class="{ 'rotate-90': !isCollapsed(group.key) }" />
                <span class="font-semibold text-foreground truncate" :class="{ 'italic font-medium text-muted-foreground': group.isUnassigned }">{{ group.label }}</span>
                <span class="shrink-0 font-mono text-[11px] text-muted-foreground">{{ group.rows.length }}</span>
                <span class="ml-auto flex h-1.5 w-20 shrink-0 gap-0.5" aria-hidden="true">
                  <span
                    v-for="segment in barSegments(group.rollup)"
                    :key="segment.state"
                    class="h-full rounded-full"
                    :class="LIVE_STATE_META[segment.state].dot"
                    :style="{ flex: `${segment.count} 1 0%` }"
                  />
                  <span v-if="group.rollup.states.NO_DATA > 0" class="h-full rounded-full bg-muted" :style="{ flex: `${group.rollup.states.NO_DATA} 1 0%` }" />
                </span>
              </button>
              <div
                v-for="column in visibleColumns"
                :key="column.key"
                class="px-3 flex items-center min-w-0 text-foreground"
                :class="column.kind === 'metric' ? 'justify-end font-mono tabular-nums' : ''"
                role="cell"
                :title="column.kind === 'metric' ? rollupExplanation(group.rollup.metrics[column.key], column, group.rollup.stationCount) : undefined"
              >
                <template v-if="column.kind === 'metric'">
                  <span v-if="group.rollup.metrics[column.key]?.value != null && column.aggregate && AGGREGATION_PREFIX[column.aggregate]" class="mr-1 font-sans text-[10px] text-muted-foreground">{{ AGGREGATION_PREFIX[column.aggregate] }}</span>
                  <span class="truncate font-semibold" :class="{ 'font-normal text-muted-foreground': group.rollup.metrics[column.key]?.value == null }">{{ rollupCellText(group.rollup.metrics[column.key], column) }}</span>
                </template>
                <span v-else-if="column.kind === 'state'" class="truncate text-[11px] text-muted-foreground">
                  {{ group.rollup.reporting }}/{{ group.rollup.stationCount }} reporting
                </span>
              </div>
            </div>

            <!-- Rows in view -->
            <div
              v-if="!isCollapsed(group.key)"
              :style="{ paddingTop: `${virtual.sections[index]?.padTop ?? 0}px`, paddingBottom: `${virtual.sections[index]?.padBottom ?? 0}px` }"
              role="rowgroup"
            >
              <div
                v-for="row in visibleRows(index)"
                :key="row.stationId"
                v-ctx="stationContextData(row)"
                class="group/row grid items-stretch border-b border-border/60 text-xs hover:bg-muted"
                :style="{ gridTemplateColumns, height: `${rowHeight}px` }"
                role="row"
                data-testid="fleet-row"
              >
                <div class="sticky left-0 z-[5] bg-card group-hover/row:bg-muted pl-8 pr-3 flex items-center gap-2 min-w-0" role="cell">
                  <span class="size-2 shrink-0 rounded-full" :class="LIVE_STATE_META[row.liveState].dot" :title="LIVE_STATE_META[row.liveState].label" />
                  <NuxtLink
                    :to="stationMachineLink(row)"
                    class="truncate font-medium text-foreground hover:underline"
                    :title="`${row.name} — open in the machine directory`"
                  >
                    {{ row.name }}
                  </NuxtLink>
                  <span class="ml-auto hidden shrink-0 items-center gap-1 group-hover/row:flex group-focus-within/row:flex">
                    <NuxtLink :to="stationTelemetryLink(row)" class="rounded p-1 text-muted-foreground hover:text-foreground" title="Live telemetry of this station" aria-label="Live telemetry of this station">
                      <Activity class="size-3.5" />
                    </NuxtLink>
                    <NuxtLink :to="stationTicketsLink(row)" class="rounded p-1 text-muted-foreground hover:text-foreground" title="Tickets of this station" aria-label="Tickets of this station">
                      <Ticket class="size-3.5" />
                    </NuxtLink>
                  </span>
                </div>
                <div
                  v-for="column in visibleColumns"
                  :key="column.key"
                  class="px-3 flex items-center min-w-0"
                  :class="column.kind === 'metric' ? 'justify-end font-mono tabular-nums' : ''"
                  role="cell"
                >
                  <span v-if="column.kind === 'state'" class="inline-flex items-center gap-1.5 min-w-0" :title="stateTitle(row)">
                    <span class="size-2 shrink-0 rounded-full" :class="LIVE_STATE_META[row.liveState].dot" />
                    <span class="truncate text-foreground">{{ LIVE_STATE_META[row.liveState].label }}</span>
                    <span v-if="row.liveState === 'STALE'" class="shrink-0 text-muted-foreground">{{ formatAge(row.telemetryAgeSeconds) }}</span>
                  </span>
                  <span
                    v-else
                    class="truncate"
                    :class="stationCellText(row, column, context) === NO_DATA_MARK ? 'text-muted-foreground' : 'text-foreground'"
                    :title="column.kind === 'text' ? stationCellText(row, column, context) : undefined"
                  >{{ stationCellText(row, column, context) }}</span>
                </div>
              </div>
            </div>
          </section>
        </div>
      </div>

      <div v-if="hasLoaded" class="shrink-0 border-t border-border px-3 py-1.5 text-[11px] text-muted-foreground flex flex-wrap items-center gap-x-4 gap-y-0.5" data-testid="fleet-footer">
        <span>{{ filtered.length }} of {{ stations.length }} stations<template v-if="view.groupBy !== 'none'"> in {{ groups.length }} group{{ groups.length === 1 ? '' : 's' }}</template></span>
        <span>Telemetry columns: latest sample per station</span>
        <span>Ticket columns: created in the last {{ windowLabel }}</span>
        <span v-if="meta">Stale after {{ meta.telemetryStaleAfterSeconds }} s without telemetry</span>
        <span v-if="generatedAtText" class="ml-auto">Data as of {{ generatedAtText }}</span>
      </div>
    </div>
  </div>
</template>
