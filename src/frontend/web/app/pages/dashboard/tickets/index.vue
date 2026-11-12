<script setup lang="ts">
import { ref, computed, watch, defineAsyncComponent, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  Plus, Camera, RefreshCw, WifiOff, LayoutList, Columns,
  Activity, QrCode, Users, FolderTree, History, CheckCircle2, Wrench,
  Filter, X, FileText, Tv, SlidersHorizontal, Palette, Columns2
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuSubContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem
} from '~/components/ui/dropdown-menu'
import TicketPersonalHeader from '~/components/tickets/TicketPersonalHeader.vue'
import LineStopAlarmBanner from '~/components/tickets/LineStopAlarmBanner.vue'
import TicketMetricsOverview from '~/components/tickets/TicketMetricsOverview.vue'
import TicketList from '~/components/tickets/TicketList.vue'
import TicketKanbanBoard from '~/components/tickets/TicketKanbanBoard.vue'
import TicketResolvedLog from '~/components/tickets/TicketResolvedLog.vue'
import TagFilterBar from '~/components/tickets/TagFilterBar.vue'
import TicketDetailWorkbench from '~/components/tickets/TicketDetailWorkbench.vue'
import MachineQrModal from '~/components/tickets/MachineQrModal.vue'
const QrScanner = defineAsyncComponent(() => import('~/components/ui/qr-scanner/QrScanner.vue'))
import { useMaintenance } from '~/composables/useMaintenance'
import { useFeatureFlags } from '~/composables/useFeatureFlags'
import { useStations } from '~/composables/useStations'
import {
  parseTicketsQuery,
  normalizeTicketsDeepLink,
  filterTicketsByDeepLink,
  describeStationFilter
} from '~/utils/deepLinkQueries'
import { parseQrUri } from '~/utils/qrActionGenerator'
import { authClient } from '~/utils/auth-client'
import type { MaintenanceTicket, TicketStatus } from '~/types/maintenance'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()
const route = useRoute()

const {
  tickets,
  metrics,
  selectedTicket,
  isLoading,
  pendingOfflineCount,
  fetchTickets,
  updateStatus,
  setPending,
  escalateTicket,
  resolveEscalation,
  reserveTicket,
  qrPickup
} = useMaintenance()

// ── View Modes & Layout ────────────────────────────────────────────────────
const activeViewMode = ref<'list' | 'kanban' | 'resolved'>('list')
const fillMode = ref<'outline' | 'semi-filled' | 'filled'>('semi-filled')
const splitView = ref(false)

// ── Feature Flags ──────────────────────────────────────────────────────────
const { enableSimulation } = useFeatureFlags()

// ── Overlays & Tools (Zero Blocking Overlays) ──────────────────────────────
const showQrScanner = ref(false)
const showQrGeneratorModal = ref(false)

// ── Tag Filtering ──────────────────────────────────────────────────────────
const selectedTags = ref<string[]>([])

// ── Metric Filter from Hero Cards ──────────────────────────────────────────
const activeMetricFilter = ref<'open' | 'critical' | 'pending' | 'escalated' | 'overdue' | 'resolved' | 'sla' | null>(null)

const activeMetricFilterLabel = computed(() => {
  switch (activeMetricFilter.value) {
    case 'open': return 'Active Open Incidents'
    case 'critical': return 'Critical & High Alerts'
    case 'pending': return 'Pending Incidents'
    case 'escalated': return 'Escalated Incidents'
    case 'overdue': return 'Overdue SLA'
    case 'resolved': return 'Resolved & Closed'
    case 'sla': return 'SLA Health Incidents'
    default: return ''
  }
})

const onMetricFilterChange = (filter: string | null) => {
  activeMetricFilter.value = filter as any
  if (filter === 'resolved') {
    activeViewMode.value = 'resolved'
  } else if (activeViewMode.value === 'resolved' && filter !== null) {
    activeViewMode.value = 'list'
  }
}

const clearMetricFilter = () => {
  activeMetricFilter.value = null
  if (activeViewMode.value === 'resolved') {
    activeViewMode.value = 'list'
  }
}

const resetPageFilters = () => {
  activeMetricFilter.value = null
  selectedTags.value = []
  activeViewMode.value = 'list'
  selectedTicket.value = null
  router.push('/dashboard/tickets')
  fetchTickets()
}

const availableTags = computed(() => {
  const set = new Set<string>()
  for (const t of tickets.value) {
    if (t.tags && Array.isArray(t.tags)) {
      for (const tag of t.tags) {
        set.add(tag)
      }
    }
  }
  return Array.from(set)
})

watch(() => [route.query, tickets.value], () => {
  const q = route.query
  if (q.create === 'true') {
    // replace: "back" from the form must not land on ?create=true and bounce forward again
    router.replace({
      path: '/dashboard/tickets/new',
      query: { ...route.query, create: undefined }
    })
    return
  }
  if (q.ticketId) {
    const match = tickets.value.find(t => t.id === q.ticketId)
    if (match) {
      selectedTicket.value = match
      if (!splitView.value) {
        router.push(`/dashboard/tickets/${q.ticketId}`)
      }
    }
  }
}, { immediate: true })

// ── URL filters (?query= ?stationId= ?handle= ?team= ?technician=) ─────────────
// The station directory widens station / team matching to every identifier a station is known
// by; it is only loaded when such a filter is present.
const { stations, fetchStations } = useStations({ autoFetch: false })
const deepLink = computed(() => parseTicketsQuery(route.query))
const urlFilter = computed(() => normalizeTicketsDeepLink(deepLink.value, tickets.value, stations.value))
const needsStationDirectory = computed(() => !!(deepLink.value.stationId || deepLink.value.handle || deepLink.value.team))
const loadStationDirectory = () => {
  if (needsStationDirectory.value && stations.value.length === 0) fetchStations()
}
watch(needsStationDirectory, loadStationDirectory)

/** Active URL filters as removable chips (each lists the query keys it stands for). */
const urlFilterChips = computed(() => {
  const link = urlFilter.value
  const chips: Array<{ key: string; label: string; value: string; params: string[] }> = []
  if (link.stationId || link.handle) {
    chips.push({ key: 'station', label: 'Station', value: describeStationFilter(link, stations.value), params: ['stationId', 'handle'] })
  }
  if (link.text) chips.push({ key: 'text', label: 'Search', value: link.text, params: ['query', 'search', ...(deepLink.value.text ? [] : ['stationId'])] })
  if (link.team) chips.push({ key: 'team', label: 'Team', value: link.team, params: ['team'] })
  if (link.technician) chips.push({ key: 'technician', label: 'Technician', value: link.technician, params: ['technician'] })
  return chips
})

const clearUrlFilter = (params: string[]) => {
  const query = { ...route.query }
  for (const key of params) delete query[key]
  router.replace({ query })
}

onMounted(async () => {
  loadStationDirectory()
  await fetchTickets()
})

const displayedTickets = computed(() => {
  let list = filterTicketsByDeepLink(tickets.value, urlFilter.value, stations.value)

  if (selectedTags.value.length > 0) {
    list = list.filter(t => {
      if (!t.tags || !Array.isArray(t.tags)) return false
      return selectedTags.value.some(sel => t.tags!.includes(sel))
    })
  }

  if (activeMetricFilter.value) {
    switch (activeMetricFilter.value) {
      case 'open':
        list = list.filter(t => t.status === 'Open' || t.status === 'InProgress' || t.status === 'Pending')
        break
      case 'critical':
        list = list.filter(t => (t.priority || '').toLowerCase() === 'critical' || (t.priority || '').toLowerCase() === 'high')
        break
      case 'pending':
        list = list.filter(t => t.status === 'Pending')
        break
      case 'escalated':
        list = list.filter(t => t.isEscalated)
        break
      case 'overdue':
        list = list.filter(t => t.slaDueAt && new Date(t.slaDueAt).getTime() < Date.now() && t.status !== 'Resolved' && t.status !== 'Closed')
        break
      case 'resolved':
        list = list.filter(t => t.status === 'Resolved' || t.status === 'Closed')
        break
      case 'sla':
        list = list.filter(t => t.slaDueAt && new Date(t.slaDueAt).getTime() < Date.now())
        break
    }
  }

  return list
})

// ── Action Handlers ────────────────────────────────────────────────────────
function onSelectTicket(tkt: MaintenanceTicket) {
  selectedTicket.value = tkt
  if (!splitView.value) {
    router.push(`/dashboard/tickets/${tkt.id}`)
  }
}

function onQrScanned(code: string) {
  showQrScanner.value = false

  // Try parsing as action URI
  const actionPayload = parseQrUri(code)
  if (actionPayload) {
    if (actionPayload.action === 'report-incident') {
      router.push({
        path: '/dashboard/tickets/new',
        query: {
          stationId: actionPayload.stationId || '',
          machineType: actionPayload.machineType || '',
          groupId: actionPayload.groupId || ''
        }
      })
      return
    } else if (actionPayload.action === 'view-ticket' && actionPayload.ticketId) {
      router.push(`/dashboard/tickets/${encodeURIComponent(actionPayload.ticketId)}`)
      return
    }
  }

  // Fallback: direct station ID
  router.push({
    path: '/dashboard/tickets/new',
    query: { stationId: code }
  })
}

function onMoveStatus(ticketId: string, status: TicketStatus) {
  updateStatus(ticketId, status)
}

function onSetPending(payload: { ticketId: string; reason: any; details?: string }) {
  setPending(payload.ticketId, payload.reason, payload.details)
}

function onEscalateTicket(ticketId: string, reason: string) {
  escalateTicket(ticketId, reason)
}

function onResolveEscalation(ticketId: string) {
  resolveEscalation(ticketId)
}

async function onReserveTicket(ticketId: string) {
  const session = (authClient as any).useSession?.()
  const techName = session?.data?.value?.user?.name || 'On-Duty Tech'
  await reserveTicket(ticketId, techName)
  await fetchTickets()
}

async function onPickupTicket(ticketId: string) {
  const session = (authClient as any).useSession?.()
  const techName = session?.data?.value?.user?.name || 'On-Duty Tech'
  await qrPickup(ticketId, techName)
  await fetchTickets()
}

async function onResumeTicket(ticketId: string) {
  await updateStatus(ticketId, 'InProgress')
  await fetchTickets()
}
</script>

<template>
  <div class="space-y-6 pb-16">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-border">
      <div
        role="button"
        tabindex="0"
        @click="resetPageFilters"
        @keydown.enter="resetPageFilters"
        class="flex items-center gap-3 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-muted/50"
        title="Click to reset filters and refresh tickets"
      >
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-600 dark:text-indigo-400 group-hover:scale-105 group-hover:bg-indigo-500/20 transition-all">
          <Wrench class="size-6" />
        </div>
        <div>
          <div class="flex items-center gap-2.5">
            <h1 class="text-2xl font-bold tracking-tight text-foreground group-hover:text-foreground/90 transition-colors">
              Maintenance & Floor Incidents
            </h1>
            <Badge
              v-if="pendingOfflineCount > 0"
              variant="outline"
              class="border-amber-500/40 text-amber-700 dark:text-amber-400 bg-amber-500/10 text-xs font-medium flex items-center gap-1.5 px-2 py-0.5 rounded-md"
            >
              <WifiOff class="size-3" />
              <span>{{ pendingOfflineCount }} Offline Queued</span>
            </Badge>
          </div>
          <p class="text-sm text-muted-foreground mt-0.5 group-hover:text-foreground transition-colors">
            Plant floor incident lifecycle, 4-tier templates, technician delegation, and machine groups
          </p>
        </div>
      </div>

      <!-- Action Toolbar -->
      <div class="flex flex-wrap items-center gap-2">
        <!-- 3-Way View Switch + Split Mode Toggle -->
        <div class="bg-muted p-0.5 rounded-lg border border-border flex items-center gap-0.5">
          <Button
            variant="ghost"
            @click="activeViewMode = 'list'"
            :class="activeViewMode === 'list' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 transition-colors cursor-pointer"
          >
            <LayoutList class="w-3.5 h-3.5 mr-1" />
            List
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'kanban'"
            :class="activeViewMode === 'kanban' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 transition-colors cursor-pointer"
          >
            <Columns class="w-3.5 h-3.5 mr-1" />
            Kanban
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'resolved'"
            :class="activeViewMode === 'resolved' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 transition-colors cursor-pointer"
          >
            <History class="w-3.5 h-3.5 mr-1" />
            Resolved Log
          </Button>

          <div class="h-4 w-px bg-border mx-0.5" />

          <Button
            variant="ghost"
            @click="splitView = !splitView"
            :class="splitView ? 'bg-indigo-600 text-white shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 transition-colors cursor-pointer"
            title="Toggle split side workbench inspector"
          >
            <Columns2 class="w-3.5 h-3.5 mr-1" />
            Split
          </Button>
        </div>

        <!-- Compact Settings & Tools Menu -->
        <DropdownMenu>
          <DropdownMenuTrigger as-child>
            <Button
              variant="outline"
              size="sm"
              class="border-border bg-card hover:bg-muted text-foreground rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors cursor-pointer"
              title="Ticket view settings, display styles, and governance tools"
            >
              <SlidersHorizontal class="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
              <span>Settings & Views</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" class="w-64">
            <DropdownMenuLabel class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider px-2 py-1">
              Display & Card Style
            </DropdownMenuLabel>
            <DropdownMenuSub>
              <DropdownMenuSubTrigger class="text-xs flex items-center gap-2 cursor-pointer">
                <Palette class="size-3.5 text-indigo-500" />
                <span>Card Fill Mode</span>
                <Badge variant="outline" class="ml-auto text-[10px] py-0 px-1 capitalize">
                  {{ fillMode }}
                </Badge>
              </DropdownMenuSubTrigger>
              <DropdownMenuSubContent class="w-48">
                <DropdownMenuRadioGroup v-model="fillMode">
                  <DropdownMenuRadioItem value="outline" class="text-xs cursor-pointer">
                    <span>Outline (Minimal)</span>
                  </DropdownMenuRadioItem>
                  <DropdownMenuRadioItem value="semi-filled" class="text-xs cursor-pointer">
                    <span>Semi-Filled (Default)</span>
                  </DropdownMenuRadioItem>
                  <DropdownMenuRadioItem value="filled" class="text-xs cursor-pointer">
                    <span>Solid High-Contrast</span>
                  </DropdownMenuRadioItem>
                </DropdownMenuRadioGroup>
              </DropdownMenuSubContent>
            </DropdownMenuSub>

            <DropdownMenuSeparator />

            <DropdownMenuLabel class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider px-2 py-1">
              Shopfloor Displays
            </DropdownMenuLabel>
            <DropdownMenuItem as-child>
              <NuxtLink to="/dashboard/tickets/monitor" class="text-xs flex items-center gap-2 cursor-pointer w-full">
                <Tv class="size-3.5 text-indigo-500" />
                <span>TV Monitor Display</span>
              </NuxtLink>
            </DropdownMenuItem>
            <DropdownMenuItem @select="showQrGeneratorModal = true" class="text-xs flex items-center gap-2 cursor-pointer">
              <QrCode class="size-3.5 text-indigo-500" />
              <span>Generate Action QR</span>
            </DropdownMenuItem>

            <DropdownMenuSeparator />

            <DropdownMenuLabel class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider px-2 py-1">
              Templates & Governance
            </DropdownMenuLabel>
            <DropdownMenuItem as-child>
              <NuxtLink to="/dashboard/tickets/templates" class="text-xs flex items-center gap-2 cursor-pointer w-full">
                <FileText class="size-3.5 text-indigo-500" />
                <span>Ticket Templates Catalog</span>
              </NuxtLink>
            </DropdownMenuItem>
            <DropdownMenuItem as-child>
              <NuxtLink to="/dashboard/machine-groups" class="text-xs flex items-center gap-2 cursor-pointer w-full">
                <FolderTree class="size-3.5 text-indigo-500" />
                <span>Machine Groups & Clusters</span>
              </NuxtLink>
            </DropdownMenuItem>
            <DropdownMenuItem as-child>
              <NuxtLink to="/dashboard/delegations" class="text-xs flex items-center gap-2 cursor-pointer w-full">
                <Users class="size-3.5 text-cyan-500" />
                <span>Shift Roster & Delegations</span>
              </NuxtLink>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        <!-- Scan QR Button (Inline Toggle) -->
        <Button
          variant="outline"
          size="sm"
          @click="showQrScanner = !showQrScanner"
          class="border-border bg-card hover:bg-muted text-foreground rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors cursor-pointer"
        >
          <Camera class="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
          <span>{{ showQrScanner ? 'Close Scanner' : 'Scan QR' }}</span>
        </Button>

        <!-- Primary Action: Dedicated New Incident Page -->
        <NuxtLink to="/dashboard/tickets/new">
          <Button
            size="sm"
            class="bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg px-3.5 h-8 text-xs font-semibold flex items-center gap-1.5 shadow-xs transition-colors cursor-pointer"
          >
            <Plus class="h-3.5 w-3.5" />
            <span>Report Incident</span>
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- Dynamic In-Page QR Scanner (Zero Blocking Backdrop) -->
    <div
      v-if="showQrScanner"
      class="p-4 bg-card border border-border rounded-2xl shadow-xl relative animate-in fade-in duration-200"
    >
      <div class="flex items-center justify-between pb-3 mb-3 border-b border-border">
        <div class="flex items-center gap-2">
          <Camera class="size-4 text-indigo-500" />
          <span class="text-xs font-bold text-foreground">Scan Machine or Action QR Code</span>
        </div>
        <Button variant="ghost" size="sm" @click="showQrScanner = false" class="size-7 p-0 rounded-lg">
          <X class="size-4" />
        </Button>
      </div>
      <QrScanner @scanned="onQrScanned" @close="showQrScanner = false" />
    </div>

    <!-- Personal Dedicated Header (Hides completely if empty) -->
    <TicketPersonalHeader
      :tickets="tickets"
      @select-ticket="onSelectTicket"
      @pickup-ticket="onPickupTicket"
      @resume-ticket="onResumeTicket"
    />

    <!-- Persistent Line-Stop Alarm Banner -->
    <LineStopAlarmBanner
      :tickets="tickets"
      @select-ticket="onSelectTicket"
    />

    <!-- Metrics Cards Overview -->
    <TicketMetricsOverview
      :metrics="metrics || undefined"
      :active-filter="activeMetricFilter"
      @filter-change="onMetricFilterChange"
    />

    <!-- Active Metric Filter Chip Bar -->
    <div
      v-if="activeMetricFilter"
      class="flex items-center justify-between px-3.5 py-2 bg-indigo-500/10 border border-indigo-500/20 rounded-xl text-xs text-indigo-700 dark:text-indigo-300 animate-in fade-in duration-200 shadow-xs"
    >
      <div class="flex items-center gap-2">
        <Filter class="size-3.5 text-indigo-600 dark:text-indigo-400" />
        <span>Filtering by: <strong class="text-foreground capitalize">{{ activeMetricFilterLabel }}</strong></span>
        <span class="text-muted-foreground">({{ displayedTickets.length }} incident{{ displayedTickets.length === 1 ? '' : 's' }} matched)</span>
      </div>
      <button
        @click="clearMetricFilter"
        class="text-xs text-indigo-600 dark:text-indigo-300 hover:text-foreground flex items-center gap-1 font-medium px-2 py-0.5 rounded hover:bg-indigo-500/20 transition-colors cursor-pointer"
      >
        <X class="size-3.5" />
        <span>Clear Filter</span>
      </button>
    </div>

    <!-- URL Filter Chips: what a deep link (search, context menu, station view) narrowed the list to -->
    <div
      v-if="urlFilterChips.length > 0"
      data-testid="ticket-url-filters"
      class="flex flex-wrap items-center gap-2 px-3.5 py-2 bg-indigo-500/10 border border-indigo-500/20 rounded-xl text-xs text-indigo-700 dark:text-indigo-300 shadow-xs"
    >
      <Filter class="size-3.5 text-indigo-600 dark:text-indigo-400" />
      <span
        v-for="chip in urlFilterChips"
        :key="chip.key"
        class="inline-flex items-center gap-1.5 pl-2 pr-1 py-0.5 rounded-md bg-background/70 border border-indigo-500/20"
      >
        <span class="text-muted-foreground">{{ chip.label }}:</span>
        <strong class="text-foreground max-w-[260px] truncate">{{ chip.value }}</strong>
        <button
          type="button"
          class="p-0.5 rounded hover:bg-indigo-500/20 cursor-pointer"
          :aria-label="`Remove ${chip.label} filter`"
          @click="clearUrlFilter(chip.params)"
        >
          <X class="size-3" />
        </button>
      </span>
      <span class="text-muted-foreground">({{ displayedTickets.length }} incident{{ displayedTickets.length === 1 ? '' : 's' }} matched)</span>
    </div>

    <!-- Tag Cloud Filter Bar -->
    <div v-if="availableTags.length > 0" class="p-3 bg-card border border-border rounded-xl shadow-xs">
      <TagFilterBar
        :available-tags="availableTags"
        :selected-tags="selectedTags"
        @update:selected-tags="selectedTags = $event"
      />
    </div>

    <!-- Master-Detail Dual-Pane Workstation Container -->
    <div class="grid grid-cols-12 gap-5 items-start">
      <!-- Master View: List, Kanban, or Resolved Log -->
      <div
        class="transition-all duration-200"
        :class="selectedTicket && splitView ? 'col-span-12 lg:col-span-7 xl:col-span-8' : 'col-span-12'"
      >
        <template v-if="activeViewMode === 'list'">
          <TicketList
            :tickets="displayedTickets"
            :loading="isLoading"
            :fill-mode="fillMode"
            @selectTicket="onSelectTicket"
            @reserveTicket="onReserveTicket"
            @filterChange="fetchTickets"
          />
        </template>

        <template v-else-if="activeViewMode === 'kanban'">
          <TicketKanbanBoard
            :tickets="displayedTickets"
            :fill-mode="fillMode"
            @selectTicket="onSelectTicket"
            @moveStatus="onMoveStatus"
            @setPending="onSetPending"
            @escalateTicket="onEscalateTicket"
            @resolveEscalation="onResolveEscalation"
          />
        </template>

        <template v-else-if="activeViewMode === 'resolved'">
          <TicketResolvedLog
            :tickets="displayedTickets"
          />
        </template>
      </div>

      <!-- Detail Inspector Pane: Split Workbench Inspector (Zero Pop-ups, Zero Overlays) -->
      <div
        v-if="selectedTicket && splitView"
        class="col-span-12 lg:col-span-5 xl:col-span-4 sticky top-4"
      >
        <TicketDetailWorkbench
          :ticket="selectedTicket"
          @close="selectedTicket = null"
          @updated="fetchTickets"
        />
      </div>
    </div>

    <!-- Actionable Machine QR Code Generator Modal -->
    <MachineQrModal
      :open="showQrGeneratorModal"
      :station-id="'STATION-OP10-01'"
      :station-name="'OP10 Machining Cell'"
      :machine-type="'Milling'"
      :group-id="'grp-line06'"
      @close="showQrGeneratorModal = false"
    />
  </div>
</template>
