<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import {
  Activity,
  AlertTriangle,
  Clock,
  PackageCheck,
  ShieldAlert,
  CheckCircle2,
  TrendingDown,
  ChevronRight,
  Flame,
  ArrowUpRight,
  Bell
} from 'lucide-vue-next'
import { useAlertRules } from '~/composables/useAlertRules'

export interface TopFaultingMachine {
  machineId: string
  name: string
  line: string
  incidentCount: number
  totalDowntimeMinutes: number
  primaryAlarmCode: string
  healthIndex: number
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
  totalMachineHours: number
  availabilityPercentage: number
  averageMtbfHours: number
  averageMttrMinutes: number
  slaCompliancePercentage: number
  topFaultingMachines: TopFaultingMachine[]
  maintenanceBacklog: BacklogAge
  stockDepletion: StockDepletionItem[]
}

const props = defineProps<{
  summary: FleetSummaryData
}>()

const router = useRouter()
const { activeAlertsCount, criticalAlertsCount } = useAlertRules()

const totalBacklog = computed(() => {
  const b = props.summary.maintenanceBacklog
  return b.under24Hours + b.oneToThreeDays + b.overThreeDays
})
</script>

<template>
  <div class="space-y-6">
    <!-- Top Row: High-level Plant Health & Availability Metric Cards -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      <!-- 1. Plant Availability Gauge -->
      <Card class="bg-card border-border text-foreground p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-muted-foreground">
          <span class="font-medium">Fleet Availability</span>
          <Activity class="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-3xl font-bold font-mono text-foreground">{{ summary.availabilityPercentage }}%</span>
          <Badge variant="outline" class="text-[10px] bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30">
            +0.4% this week
          </Badge>
        </div>
        <div class="space-y-1.5 text-xs text-muted-foreground">
          <div class="w-full bg-muted/60 h-2 rounded-full overflow-hidden border border-border">
            <div
              class="bg-emerald-500 h-full rounded-full transition-all duration-500"
              :style="{ width: `${summary.availabilityPercentage}%` }"
            ></div>
          </div>
          <div class="flex justify-between text-[11px] text-muted-foreground font-mono">
            <span>Operating: {{ summary.totalMachineHours }} hrs</span>
            <span>Target: 95.0%</span>
          </div>
        </div>
      </Card>

      <!-- 2. MTBF Metric -->
      <Card class="bg-card border-border text-foreground p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-muted-foreground">
          <span class="font-medium">Mean Time Between Failures</span>
          <Clock class="w-4 h-4 text-teal-600 dark:text-teal-400" />
        </div>
        <div class="flex items-baseline gap-2 my-3">
          <span class="text-3xl font-bold font-mono text-foreground">{{ summary.averageMtbfHours }}</span>
          <span class="text-sm font-medium text-muted-foreground">Hours</span>
        </div>
        <div class="text-xs text-muted-foreground flex items-center gap-1.5 font-mono">
          <span class="text-emerald-600 dark:text-emerald-400 font-semibold">↑ +14.2 hrs</span>
          <span class="text-muted-foreground/80">vs 30-day trailing avg</span>
        </div>
      </Card>

      <!-- 3. MTTR Metric -->
      <Card class="bg-card border-border text-foreground p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-muted-foreground">
          <span class="font-medium">Mean Time to Repair</span>
          <TrendingDown class="w-4 h-4 text-muted-foreground" />
        </div>
        <div class="flex items-baseline gap-2 my-3">
          <span class="text-3xl font-bold font-mono text-foreground">{{ summary.averageMttrMinutes }}</span>
          <span class="text-sm font-medium text-muted-foreground">Minutes</span>
        </div>
        <div class="text-xs text-muted-foreground flex items-center gap-1.5 font-mono">
          <span class="text-emerald-600 dark:text-emerald-400 font-semibold">↓ -3.8 min</span>
          <span class="text-muted-foreground/80">rapid dispatch response</span>
        </div>
      </Card>

      <!-- 4. Critical SLA Compliance -->
      <Card class="bg-card border-border text-foreground p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-muted-foreground">
          <span class="font-medium">Ticket SLA Compliance</span>
          <CheckCircle2 class="w-4 h-4 text-amber-600 dark:text-amber-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-3xl font-bold font-mono text-foreground">{{ summary.slaCompliancePercentage }}%</span>
          <Badge
            :class="summary.slaCompliancePercentage >= 95 ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30' : 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30'"
            class="text-[10px] border"
          >
            Tier 1 Target
          </Badge>
        </div>
        <div class="text-xs text-muted-foreground font-mono">
          <span>P1/P2 resolution compliance: </span>
          <span class="text-foreground font-semibold">98.1% on-time</span>
        </div>
      </Card>
    </div>

    <!-- Active Telemetry Breaches / Rule Alerts Alert Banner -->
    <div
      v-if="activeAlertsCount > 0"
      class="p-4 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-between flex-wrap gap-3"
    >
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg bg-amber-500/15 border border-amber-500/25 text-amber-600 dark:text-amber-400">
          <Bell class="w-4 h-4" />
        </div>
        <div>
          <div class="text-xs font-semibold text-amber-800 dark:text-amber-200 flex items-center gap-2">
            <span>{{ activeAlertsCount }} Active Threshold Breaches in Factory Fleet</span>
            <Badge v-if="criticalAlertsCount > 0" class="text-[10px] bg-rose-500/20 text-rose-700 dark:text-rose-300 border-rose-500/40 border">
              {{ criticalAlertsCount }} Critical
            </Badge>
          </div>
          <p class="text-[11px] text-amber-800/80 dark:text-amber-300/80 mt-0.5">
            Real-time rules engine actively monitoring thermal, vibration, and Soft-PLC cycle jitter with automated maintenance ticket dispatch.
          </p>
        </div>
      </div>

      <button
        type="button"
        @click="router.push('/dashboard/tickets')"
        class="px-3 py-1.5 rounded-lg bg-amber-500/15 hover:bg-amber-500/25 text-amber-800 dark:text-amber-200 border border-amber-500/30 text-xs font-medium flex items-center gap-1.5 transition-all cursor-pointer"
      >
        <span>View Incident Tickets</span>
        <ChevronRight class="w-3.5 h-3.5" />
      </button>
    </div>

    <!-- Second Row: Top-Faulting Machines Ranking & Backlog Distribution -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Top-Faulting Machines Ranking (2 cols) -->
      <div class="lg:col-span-2 bg-card border border-border rounded-2xl p-6 shadow-sm space-y-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Flame class="w-4 h-4 text-amber-600 dark:text-amber-400" />
            <h4 class="text-sm font-semibold text-foreground">Top-Faulting Machines & Degradation Ranking</h4>
          </div>
          <span class="text-xs text-muted-foreground font-mono">Ranked by Incident Frequency (Past 30 Days)</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-xs text-left">
            <thead class="text-muted-foreground border-b border-border uppercase text-[10px] font-mono">
              <tr>
                <th class="py-2.5 px-3">Machine / Cell</th>
                <th class="py-2.5 px-3">Line</th>
                <th class="py-2.5 px-3 text-center">Incidents</th>
                <th class="py-2.5 px-3 text-center">Downtime</th>
                <th class="py-2.5 px-3">Frequent Alarm</th>
                <th class="py-2.5 px-3 text-right">Health Index</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border font-medium">
              <tr
                v-for="m in summary.topFaultingMachines"
                :key="m.machineId"
                @click="router.push(`/dashboard/machines?id=${encodeURIComponent(m.machineId)}`)"
                class="hover:bg-muted/40 transition-colors cursor-pointer group"
                title="Click to view machine in machinery catalog"
              >
                <td class="py-3 px-3">
                  <div class="font-semibold text-foreground group-hover:text-primary transition-colors flex items-center gap-1.5">
                    <span>{{ m.name }}</span>
                    <ChevronRight class="size-3 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
                  </div>
                  <div class="text-[11px] font-mono text-muted-foreground">{{ m.machineId }}</div>
                </td>
                <td class="py-3 px-3 text-foreground">{{ m.line }}</td>
                <td class="py-3 px-3 text-center font-mono text-amber-600 dark:text-amber-300 font-semibold">{{ m.incidentCount }}</td>
                <td class="py-3 px-3 text-center font-mono text-muted-foreground">{{ m.totalDowntimeMinutes }} min</td>
                <td class="py-3 px-3">
                  <span class="px-1.5 py-0.5 rounded bg-muted/60 border border-border text-[10px] font-mono text-rose-700 dark:text-rose-300">
                    {{ m.primaryAlarmCode }}
                  </span>
                </td>
                <td class="py-3 px-3 text-right">
                  <div class="inline-flex items-center gap-1.5 font-mono font-bold">
                    <span
                      :class="m.healthIndex < 75 ? 'text-rose-600 dark:text-rose-400' : (m.healthIndex < 85 ? 'text-amber-600 dark:text-amber-400' : 'text-emerald-600 dark:text-emerald-400')"
                    >
                      {{ m.healthIndex }}%
                    </span>
                    <span
                      class="size-2 rounded-full"
                      :class="m.healthIndex < 75 ? 'bg-rose-500 animate-pulse' : (m.healthIndex < 85 ? 'bg-amber-400' : 'bg-emerald-400')"
                    ></span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Maintenance Backlog Age Distribution (1 col) -->
      <div class="bg-card border border-border rounded-2xl p-6 shadow-sm flex flex-col justify-between space-y-4">
        <div>
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2">
              <Clock class="w-4 h-4 text-muted-foreground" />
              <h4 class="text-sm font-semibold text-foreground">Maintenance Backlog Age</h4>
            </div>
            <span class="text-xs font-mono text-muted-foreground">{{ totalBacklog }} Open</span>
          </div>

          <!-- Stacked Bar of Age Distribution -->
          <div class="w-full bg-muted/60 h-3 rounded-full overflow-hidden border border-border flex my-4">
            <div
              class="bg-emerald-500 h-full transition-all"
              :style="{ width: `${(summary.maintenanceBacklog.under24Hours / (totalBacklog || 1)) * 100}%` }"
              title="Under 24h"
            ></div>
            <div
              class="bg-teal-600 h-full transition-all"
              :style="{ width: `${(summary.maintenanceBacklog.oneToThreeDays / (totalBacklog || 1)) * 100}%` }"
              title="1 - 3 Days"
            ></div>
            <div
              class="bg-amber-500 h-full transition-all"
              :style="{ width: `${(summary.maintenanceBacklog.overThreeDays / (totalBacklog || 1)) * 100}%` }"
              title="Over 3 Days"
            ></div>
          </div>

          <div class="space-y-3 text-xs">
            <NuxtLink
              to="/dashboard/tickets"
              class="flex items-center justify-between p-2 rounded-lg bg-muted/30 border border-border hover:border-primary/40 hover:bg-muted/60 transition-all cursor-pointer group"
              title="View all incidents under 24 hours"
            >
              <div class="flex items-center gap-2">
                <span class="size-2.5 rounded-full bg-emerald-500"></span>
                <span class="text-foreground transition-colors">Under 24 Hours</span>
              </div>
              <span class="font-mono font-bold text-foreground">{{ summary.maintenanceBacklog.under24Hours }}</span>
            </NuxtLink>
            <NuxtLink
              to="/dashboard/tickets"
              class="flex items-center justify-between p-2 rounded-lg bg-muted/30 border border-border hover:border-primary/40 hover:bg-muted/60 transition-all cursor-pointer group"
              title="View backlog incidents 1 to 3 days old"
            >
              <div class="flex items-center gap-2">
                <span class="size-2.5 rounded-full bg-teal-500"></span>
                <span class="text-foreground transition-colors">1 – 3 Days Old</span>
              </div>
              <span class="font-mono font-bold text-foreground">{{ summary.maintenanceBacklog.oneToThreeDays }}</span>
            </NuxtLink>
            <NuxtLink
              to="/dashboard/tickets"
              class="flex items-center justify-between p-2 rounded-lg bg-muted/30 border border-border hover:border-primary/40 hover:bg-muted/60 transition-all cursor-pointer group"
              title="View aged backlog incidents over 3 days"
            >
              <div class="flex items-center gap-2">
                <span class="size-2.5 rounded-full bg-amber-500"></span>
                <span class="text-foreground transition-colors">Over 3 Days Old</span>
              </div>
              <span class="font-mono font-bold text-foreground">{{ summary.maintenanceBacklog.overThreeDays }}</span>
            </NuxtLink>
            <NuxtLink
              to="/dashboard/tickets?critical=true"
              class="flex items-center justify-between p-2 rounded-lg bg-rose-500/10 border border-rose-500/30 text-rose-700 dark:text-rose-300 hover:bg-rose-500/20 hover:border-rose-500/50 transition-all cursor-pointer group"
              title="Filter to critical SLA breached incidents"
            >
              <div class="flex items-center gap-2">
                <ShieldAlert class="w-3.5 h-3.5 text-rose-600 dark:text-rose-400 group-hover:scale-110 transition-transform" />
                <span class="group-hover:text-rose-800 dark:group-hover:text-rose-200 transition-colors">Critical SLA Breaches</span>
              </div>
              <span class="font-mono font-bold text-rose-700 dark:text-rose-300">{{ summary.maintenanceBacklog.criticalBreached }}</span>
            </NuxtLink>
          </div>
        </div>

        <div class="pt-3 border-t border-border text-[11px] text-muted-foreground">
          Average turnaround time is currently performing within target operational envelopes.
        </div>
      </div>
    </div>

    <!-- Third Row: Critical Stock Depletion Warnings -->
    <div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <AlertTriangle class="w-4 h-4 text-rose-600 dark:text-rose-400" />
          <h4 class="text-sm font-semibold text-foreground">Critical Spare Parts Depletion Alerts</h4>
        </div>
        <NuxtLink
          to="/dashboard/inventory"
          class="text-xs text-indigo-600 dark:text-indigo-400 hover:text-indigo-700 dark:hover:text-indigo-300 flex items-center gap-1 font-medium transition-colors"
        >
          <span>Manage Stock</span>
          <ArrowUpRight class="w-3 h-3" />
        </NuxtLink>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div
          v-for="stock in summary.stockDepletion"
          :key="stock.partNumber"
          class="p-4 rounded-xl bg-muted/30 border border-border space-y-2 relative overflow-hidden"
        >
          <div class="flex items-start justify-between">
            <span class="font-mono font-semibold text-xs text-foreground">{{ stock.partNumber }}</span>
            <Badge
              :class="stock.criticality === 'Critical' ? 'bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30' : 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30'"
              class="text-[10px] border"
            >
              {{ stock.criticality }}
            </Badge>
          </div>
          <p class="text-xs text-muted-foreground line-clamp-1">{{ stock.description }}</p>
          <div class="flex items-baseline justify-between pt-1 border-t border-border font-mono text-xs">
            <span class="text-muted-foreground">Current Qty:</span>
            <span class="text-rose-600 dark:text-rose-400 font-bold">{{ stock.currentStock }} / {{ stock.minStockThreshold }} min</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
