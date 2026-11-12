<script setup lang="ts">
/**
 * Maintenance summary of the fleet (backend `FleetSummaryDto`, GET /api/analytics/fleet-summary).
 * Every figure comes from stored tickets, stock items and client PCs for the selected look-back
 * window. A figure without data is rendered as "No data" together with the reason; nothing on
 * this panel is a constant.
 */
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { Badge } from '~/components/ui/badge'
import { AlertTriangle, ArrowUpRight, ChevronRight, Clock, Flame, ShieldAlert } from 'lucide-vue-next'
import { formatDurationMinutes } from '~/utils/fleetAnalytics'

export interface TopFaultingMachine {
  machineId: string
  name: string
  /** Custom identifier, else machine name. */
  identifier?: string
  line: string
  incidentCount: number
  openTicketCount: number
  totalDowntimeMinutes: number | null
  primaryAlarmCode: string
}

export interface BacklogAge {
  under24Hours: number
  oneToThreeDays: number
  overThreeDays: number
  criticalBreached: number
}

export interface StockDepletionItem {
  partNumber: string
  description: string
  currentStock: number
  minStockThreshold: number
  criticality: string
}

export interface FleetSummaryData {
  generatedAt?: string
  windowDays: number
  totalMachines: number
  controllerCount: number
  controllersOnline: number
  controllerOnlinePercentage: number | null
  controllerOnlineWindowSeconds: number
  incidentCount: number
  resolvedIncidentCount: number
  averageMttrMinutes: number | null
  averageMtbfHours: number | null
  mtbfMachineCount: number
  criticalSlaHours: number
  topFaultingMachines: TopFaultingMachine[]
  maintenanceBacklog: BacklogAge
  stockDepletion: StockDepletionItem[]
  stockDepletionTotal: number
}

const props = defineProps<{
  summary: FleetSummaryData
}>()

const router = useRouter()

const totalBacklog = computed(() => {
  const b = props.summary.maintenanceBacklog
  return b.under24Hours + b.oneToThreeDays + b.overThreeDays
})
const backlogShare = (count: number) => (totalBacklog.value > 0 ? `${(count / totalBacklog.value) * 100}%` : '0%')

const windowText = computed(() => `last ${props.summary.windowDays} d`)

interface Tile {
  key: string
  label: string
  value: string | null
  detail: string
}

const tiles = computed<Tile[]>(() => {
  const s = props.summary
  return [
    {
      key: 'incidents',
      label: `Tickets raised, ${windowText.value}`,
      value: String(s.incidentCount),
      detail: `${s.resolvedIncidentCount} of them resolved`
    },
    {
      key: 'mttr',
      label: 'Mean time to resolve',
      value: s.averageMttrMinutes !== null && s.averageMttrMinutes !== undefined ? formatDurationMinutes(s.averageMttrMinutes) : null,
      detail: s.resolvedIncidentCount > 0
        ? `Ticket creation to resolution · ${s.resolvedIncidentCount} resolved tickets of the ${windowText.value}`
        : `No ticket of the ${windowText.value} is resolved`
    },
    {
      key: 'repeat',
      label: 'Repeat-incident interval',
      value: s.averageMtbfHours !== null && s.averageMtbfHours !== undefined ? formatDurationMinutes(s.averageMtbfHours * 60) : null,
      detail: s.mtbfMachineCount > 0
        ? `Mean gap between tickets on the same machine · ${s.mtbfMachineCount} machine${s.mtbfMachineCount === 1 ? '' : 's'} with 2+ tickets`
        : `No machine had two tickets in the ${windowText.value}`
    },
    {
      key: 'controllers',
      label: 'Controllers online',
      value: s.controllerCount > 0 ? `${s.controllersOnline}/${s.controllerCount}` : null,
      detail: s.controllerCount > 0
        ? `Agents seen in the last ${Math.round(s.controllerOnlineWindowSeconds / 60)} min · connectivity, not production availability`
        : 'No client PC is registered'
    }
  ]
})

const openMachine = (m: TopFaultingMachine) => {
  router.push({ path: '/dashboard/machines', query: { search: m.identifier || m.name, tree: m.machineId } })
}
</script>

<template>
  <div class="space-y-6">
    <!-- Ticket and connectivity figures for the look-back window -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <div
        v-for="tile in tiles"
        :key="tile.key"
        class="rounded-xl border border-border bg-card px-4 py-3 min-w-0"
        :data-testid="`summary-tile-${tile.key}`"
      >
        <div class="text-xs font-medium text-muted-foreground">{{ tile.label }}</div>
        <div class="mt-1">
          <span v-if="tile.value !== null" class="text-2xl font-semibold text-foreground">{{ tile.value }}</span>
          <span v-else class="text-base font-medium text-muted-foreground leading-8">No data</span>
        </div>
        <div class="mt-1 text-[11px] text-muted-foreground">{{ tile.detail }}</div>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Machines with the most tickets in the window -->
      <div class="lg:col-span-2 bg-card border border-border rounded-xl p-5 space-y-4">
        <div class="flex items-center justify-between gap-3 flex-wrap">
          <div class="flex items-center gap-2">
            <Flame class="w-4 h-4 text-muted-foreground" />
            <h4 class="text-sm font-semibold text-foreground">Machines with the most tickets</h4>
          </div>
          <span class="text-xs text-muted-foreground">Tickets created in the {{ windowText }}</span>
        </div>

        <p v-if="summary.topFaultingMachines.length === 0" class="py-8 text-center text-xs text-muted-foreground" data-testid="summary-no-faulting">
          No ticket was raised for a machine in the {{ windowText }}.
        </p>
        <div v-else class="overflow-x-auto">
          <table class="w-full text-xs text-left">
            <thead class="text-muted-foreground border-b border-border text-[11px] font-medium">
              <tr>
                <th class="py-2 px-3 font-medium">Machine</th>
                <th class="py-2 px-3 font-medium">Line</th>
                <th class="py-2 px-3 font-medium text-right">Tickets</th>
                <th class="py-2 px-3 font-medium text-right">Still open</th>
                <th class="py-2 px-3 font-medium text-right">Reported downtime</th>
                <th class="py-2 px-3 font-medium">Most frequent alarm</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr
                v-for="m in summary.topFaultingMachines"
                :key="m.machineId"
                v-ctx="{ entityType: 'machine', entityId: m.machineId, entityName: m.name, machineId: m.machineId, machineName: m.identifier || m.name }"
                @click="openMachine(m)"
                class="hover:bg-muted transition-colors cursor-pointer group"
                title="Open this machine in the machine directory"
              >
                <td class="py-2.5 px-3">
                  <div class="font-medium text-foreground flex items-center gap-1.5">
                    <span>{{ m.name }}</span>
                    <ChevronRight class="size-3 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
                  </div>
                </td>
                <td class="py-2.5 px-3" :class="m.line ? 'text-foreground' : 'text-muted-foreground'">{{ m.line || '—' }}</td>
                <td class="py-2.5 px-3 text-right font-mono tabular-nums text-foreground">{{ m.incidentCount }}</td>
                <td class="py-2.5 px-3 text-right font-mono tabular-nums text-foreground">{{ m.openTicketCount }}</td>
                <td class="py-2.5 px-3 text-right font-mono tabular-nums" :class="m.totalDowntimeMinutes == null ? 'text-muted-foreground' : 'text-foreground'">
                  {{ formatDurationMinutes(m.totalDowntimeMinutes) }}
                </td>
                <td class="py-2.5 px-3">
                  <span v-if="m.primaryAlarmCode" class="px-1.5 py-0.5 rounded bg-muted border border-border text-[11px] font-mono text-foreground">{{ m.primaryAlarmCode }}</span>
                  <span v-else class="text-muted-foreground">—</span>
                </td>
              </tr>
            </tbody>
          </table>
          <p class="mt-2 text-[11px] text-muted-foreground">
            Downtime is the line-stop time recorded on the tickets; "—" means no ticket recorded one.
          </p>
        </div>
      </div>

      <!-- Age of the open tickets -->
      <div class="bg-card border border-border rounded-xl p-5 space-y-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Clock class="w-4 h-4 text-muted-foreground" />
            <h4 class="text-sm font-semibold text-foreground">Open tickets by age</h4>
          </div>
          <span class="text-xs font-mono text-muted-foreground">{{ totalBacklog }} open</span>
        </div>

        <div
          v-if="totalBacklog > 0"
          class="flex h-2 w-full gap-0.5"
          role="img"
          :aria-label="`Open tickets: ${summary.maintenanceBacklog.under24Hours} under 24 hours, ${summary.maintenanceBacklog.oneToThreeDays} one to three days, ${summary.maintenanceBacklog.overThreeDays} over three days`"
        >
          <span v-if="summary.maintenanceBacklog.under24Hours > 0" class="h-full rounded-full bg-sky-400 dark:bg-sky-300" :style="{ width: backlogShare(summary.maintenanceBacklog.under24Hours) }" />
          <span v-if="summary.maintenanceBacklog.oneToThreeDays > 0" class="h-full rounded-full bg-sky-600 dark:bg-sky-500" :style="{ width: backlogShare(summary.maintenanceBacklog.oneToThreeDays) }" />
          <span v-if="summary.maintenanceBacklog.overThreeDays > 0" class="h-full rounded-full bg-sky-800 dark:bg-sky-700" :style="{ width: backlogShare(summary.maintenanceBacklog.overThreeDays) }" />
        </div>
        <p v-else class="text-xs text-muted-foreground">No open tickets.</p>

        <div class="space-y-2 text-xs">
          <NuxtLink to="/dashboard/tickets" class="flex items-center justify-between p-2 rounded-lg border border-border hover:bg-muted transition-colors">
            <span class="flex items-center gap-2 text-foreground"><span class="size-2.5 rounded-full bg-sky-400 dark:bg-sky-300" />Under 24 hours</span>
            <span class="font-mono font-semibold text-foreground">{{ summary.maintenanceBacklog.under24Hours }}</span>
          </NuxtLink>
          <NuxtLink to="/dashboard/tickets" class="flex items-center justify-between p-2 rounded-lg border border-border hover:bg-muted transition-colors">
            <span class="flex items-center gap-2 text-foreground"><span class="size-2.5 rounded-full bg-sky-600 dark:bg-sky-500" />1 – 3 days</span>
            <span class="font-mono font-semibold text-foreground">{{ summary.maintenanceBacklog.oneToThreeDays }}</span>
          </NuxtLink>
          <NuxtLink to="/dashboard/tickets" class="flex items-center justify-between p-2 rounded-lg border border-border hover:bg-muted transition-colors">
            <span class="flex items-center gap-2 text-foreground"><span class="size-2.5 rounded-full bg-sky-800 dark:bg-sky-700" />Over 3 days</span>
            <span class="font-mono font-semibold text-foreground">{{ summary.maintenanceBacklog.overThreeDays }}</span>
          </NuxtLink>
          <NuxtLink to="/dashboard/tickets" class="flex items-center justify-between p-2 rounded-lg border border-border hover:bg-muted transition-colors">
            <span class="flex items-center gap-2 text-foreground">
              <ShieldAlert class="w-3.5 h-3.5 text-rose-600 dark:text-rose-400" />
              Critical, open longer than {{ summary.criticalSlaHours }} h
            </span>
            <span class="font-mono font-semibold text-foreground">{{ summary.maintenanceBacklog.criticalBreached }}</span>
          </NuxtLink>
        </div>
      </div>
    </div>

    <!-- Stock at or below its minimum -->
    <div class="bg-card border border-border rounded-xl p-5 space-y-4">
      <div class="flex items-center justify-between gap-3 flex-wrap">
        <div class="flex items-center gap-2">
          <AlertTriangle class="w-4 h-4 text-muted-foreground" />
          <h4 class="text-sm font-semibold text-foreground">Stock at or below its minimum</h4>
          <span v-if="summary.stockDepletionTotal > summary.stockDepletion.length" class="text-xs text-muted-foreground">
            showing {{ summary.stockDepletion.length }} of {{ summary.stockDepletionTotal }}
          </span>
        </div>
        <NuxtLink to="/dashboard/inventory" class="text-xs text-foreground hover:underline flex items-center gap-1">
          <span>Manage stock</span>
          <ArrowUpRight class="w-3 h-3" />
        </NuxtLink>
      </div>

      <p v-if="summary.stockDepletion.length === 0" class="py-4 text-center text-xs text-muted-foreground" data-testid="summary-no-stock">
        No stock item with a minimum threshold is at or below it.
      </p>
      <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
        <div v-for="stock in summary.stockDepletion" :key="stock.partNumber" class="p-3 rounded-lg border border-border space-y-1.5">
          <div class="flex items-start justify-between gap-2">
            <span class="font-mono font-semibold text-xs text-foreground truncate" :title="stock.partNumber">{{ stock.partNumber }}</span>
            <Badge variant="outline" class="text-[10px] shrink-0">{{ stock.criticality === 'Critical' ? 'Out of stock' : 'Low' }}</Badge>
          </div>
          <p class="text-xs text-muted-foreground line-clamp-1">{{ stock.description }}</p>
          <div class="flex items-baseline justify-between pt-1 border-t border-border font-mono text-xs">
            <span class="text-muted-foreground">In stock / minimum</span>
            <span class="text-foreground font-semibold">{{ stock.currentStock }} / {{ stock.minStockThreshold }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
