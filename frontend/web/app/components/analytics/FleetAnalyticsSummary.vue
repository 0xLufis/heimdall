<script setup lang="ts">
import { computed } from 'vue'
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
  ArrowUpRight
} from 'lucide-vue-next'

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
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Fleet Availability</span>
          <Activity class="w-4 h-4 text-emerald-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-3xl font-bold font-mono text-white">{{ summary.availabilityPercentage }}%</span>
          <Badge variant="outline" class="text-[10px] bg-emerald-950/40 text-emerald-300 border-emerald-500/30">
            +0.4% this week
          </Badge>
        </div>
        <div class="space-y-1.5 text-xs text-slate-400">
          <div class="w-full bg-slate-950 h-2 rounded-full overflow-hidden border border-slate-800">
            <div
              class="bg-emerald-500 h-full rounded-full transition-all duration-500"
              :style="{ width: `${summary.availabilityPercentage}%` }"
            ></div>
          </div>
          <div class="flex justify-between text-[11px] text-slate-500 font-mono">
            <span>Operating: {{ summary.totalMachineHours }} hrs</span>
            <span>Target: 95.0%</span>
          </div>
        </div>
      </Card>

      <!-- 2. MTBF Metric -->
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Mean Time Between Failures</span>
          <Clock class="w-4 h-4 text-sky-400" />
        </div>
        <div class="flex items-baseline gap-2 my-3">
          <span class="text-3xl font-bold font-mono text-white">{{ summary.averageMtbfHours }}</span>
          <span class="text-sm font-medium text-slate-400">Hours</span>
        </div>
        <div class="text-xs text-slate-400 flex items-center gap-1.5 font-mono">
          <span class="text-emerald-400 font-semibold">↑ +14.2 hrs</span>
          <span class="text-slate-500">vs 30-day trailing avg</span>
        </div>
      </Card>

      <!-- 3. MTTR Metric -->
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Mean Time to Repair</span>
          <TrendingDown class="w-4 h-4 text-indigo-400" />
        </div>
        <div class="flex items-baseline gap-2 my-3">
          <span class="text-3xl font-bold font-mono text-white">{{ summary.averageMttrMinutes }}</span>
          <span class="text-sm font-medium text-slate-400">Minutes</span>
        </div>
        <div class="text-xs text-slate-400 flex items-center gap-1.5 font-mono">
          <span class="text-emerald-400 font-semibold">↓ -3.8 min</span>
          <span class="text-slate-500">rapid dispatch response</span>
        </div>
      </Card>

      <!-- 4. Critical SLA Compliance -->
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Ticket SLA Compliance</span>
          <CheckCircle2 class="w-4 h-4 text-amber-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-3xl font-bold font-mono text-white">{{ summary.slaCompliancePercentage }}%</span>
          <Badge
            :class="summary.slaCompliancePercentage >= 95 ? 'bg-emerald-950/40 text-emerald-300 border-emerald-500/30' : 'bg-amber-950/40 text-amber-300 border-amber-500/30'"
            class="text-[10px] border"
          >
            Tier 1 Target
          </Badge>
        </div>
        <div class="text-xs text-slate-400 font-mono">
          <span>P1/P2 resolution compliance: </span>
          <span class="text-slate-200 font-semibold">98.1% on-time</span>
        </div>
      </Card>
    </div>

    <!-- Second Row: Top-Faulting Machines Ranking & Backlog Distribution -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Top-Faulting Machines Ranking (2 cols) -->
      <div class="lg:col-span-2 bg-slate-900/90 border border-slate-800 rounded-2xl p-6 shadow-sm space-y-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Flame class="w-4 h-4 text-amber-400" />
            <h4 class="text-sm font-semibold text-slate-100">Top-Faulting Machines & Degradation Ranking</h4>
          </div>
          <span class="text-xs text-slate-400 font-mono">Ranked by Incident Frequency (Past 30 Days)</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-xs text-left">
            <thead class="text-slate-400 border-b border-slate-800 uppercase text-[10px] font-mono">
              <tr>
                <th class="py-2.5 px-3">Machine / Cell</th>
                <th class="py-2.5 px-3">Line</th>
                <th class="py-2.5 px-3 text-center">Incidents</th>
                <th class="py-2.5 px-3 text-center">Downtime</th>
                <th class="py-2.5 px-3">Frequent Alarm</th>
                <th class="py-2.5 px-3 text-right">Health Index</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800/60 font-medium">
              <tr
                v-for="m in summary.topFaultingMachines"
                :key="m.machineId"
                class="hover:bg-slate-800/40 transition-colors"
              >
                <td class="py-3 px-3">
                  <div class="font-semibold text-slate-200">{{ m.name }}</div>
                  <div class="text-[11px] font-mono text-slate-400">{{ m.machineId }}</div>
                </td>
                <td class="py-3 px-3 text-slate-300">{{ m.line }}</td>
                <td class="py-3 px-3 text-center font-mono text-amber-300 font-semibold">{{ m.incidentCount }}</td>
                <td class="py-3 px-3 text-center font-mono text-slate-300">{{ m.totalDowntimeMinutes }} min</td>
                <td class="py-3 px-3">
                  <span class="px-1.5 py-0.5 rounded bg-slate-950 border border-slate-800 text-[10px] font-mono text-rose-300">
                    {{ m.primaryAlarmCode }}
                  </span>
                </td>
                <td class="py-3 px-3 text-right">
                  <div class="inline-flex items-center gap-1.5 font-mono font-bold">
                    <span
                      :class="m.healthIndex < 75 ? 'text-rose-400' : (m.healthIndex < 85 ? 'text-amber-400' : 'text-emerald-400')"
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
      <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 shadow-sm flex flex-col justify-between space-y-4">
        <div>
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2">
              <Clock class="w-4 h-4 text-indigo-400" />
              <h4 class="text-sm font-semibold text-slate-100">Maintenance Backlog Age</h4>
            </div>
            <span class="text-xs font-mono text-slate-400">{{ totalBacklog }} Open</span>
          </div>

          <!-- Stacked Bar of Age Distribution -->
          <div class="w-full bg-slate-950 h-3 rounded-full overflow-hidden border border-slate-800 flex my-4">
            <div
              class="bg-emerald-500 h-full transition-all"
              :style="{ width: `${(summary.maintenanceBacklog.under24Hours / (totalBacklog || 1)) * 100}%` }"
              title="Under 24h"
            ></div>
            <div
              class="bg-sky-500 h-full transition-all"
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
            <div class="flex items-center justify-between p-2 rounded-lg bg-slate-950/60 border border-slate-800/80">
              <div class="flex items-center gap-2">
                <span class="size-2.5 rounded-full bg-emerald-500"></span>
                <span class="text-slate-300">Under 24 Hours</span>
              </div>
              <span class="font-mono font-bold text-slate-100">{{ summary.maintenanceBacklog.under24Hours }}</span>
            </div>
            <div class="flex items-center justify-between p-2 rounded-lg bg-slate-950/60 border border-slate-800/80">
              <div class="flex items-center gap-2">
                <span class="size-2.5 rounded-full bg-sky-500"></span>
                <span class="text-slate-300">1 – 3 Days Old</span>
              </div>
              <span class="font-mono font-bold text-slate-100">{{ summary.maintenanceBacklog.oneToThreeDays }}</span>
            </div>
            <div class="flex items-center justify-between p-2 rounded-lg bg-slate-950/60 border border-slate-800/80">
              <div class="flex items-center gap-2">
                <span class="size-2.5 rounded-full bg-amber-500"></span>
                <span class="text-slate-300">Over 3 Days Old</span>
              </div>
              <span class="font-mono font-bold text-slate-100">{{ summary.maintenanceBacklog.overThreeDays }}</span>
            </div>
            <div class="flex items-center justify-between p-2 rounded-lg bg-rose-950/30 border border-rose-500/30 text-rose-300">
              <div class="flex items-center gap-2">
                <ShieldAlert class="w-3.5 h-3.5 text-rose-400" />
                <span>Critical SLA Breaches</span>
              </div>
              <span class="font-mono font-bold text-rose-300">{{ summary.maintenanceBacklog.criticalBreached }}</span>
            </div>
          </div>
        </div>

        <div class="pt-3 border-t border-slate-800 text-[11px] text-slate-400">
          Average turnaround time is currently performing within target operational envelopes.
        </div>
      </div>
    </div>

    <!-- Third Row: Critical Stock Depletion Warnings -->
    <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 shadow-sm space-y-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <AlertTriangle class="w-4 h-4 text-rose-400" />
          <h4 class="text-sm font-semibold text-slate-100">Critical Spare Parts Depletion Alerts</h4>
        </div>
        <NuxtLink
          to="/dashboard/inventory"
          class="text-xs text-indigo-400 hover:text-indigo-300 flex items-center gap-1 font-medium transition-colors"
        >
          <span>Manage Stock</span>
          <ArrowUpRight class="w-3 h-3" />
        </NuxtLink>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div
          v-for="stock in summary.stockDepletion"
          :key="stock.partNumber"
          class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2 relative overflow-hidden"
        >
          <div class="flex items-start justify-between">
            <span class="font-mono font-semibold text-xs text-slate-200">{{ stock.partNumber }}</span>
            <Badge
              :class="stock.criticality === 'Critical' ? 'bg-rose-950/60 text-rose-300 border-rose-500/40' : 'bg-amber-950/60 text-amber-300 border-amber-500/40'"
              class="text-[10px] border"
            >
              {{ stock.criticality }}
            </Badge>
          </div>
          <p class="text-xs text-slate-400 line-clamp-1">{{ stock.description }}</p>
          <div class="flex items-baseline justify-between pt-1 border-t border-slate-800/80 font-mono text-xs">
            <span class="text-slate-400">Current Qty:</span>
            <span class="text-rose-400 font-bold">{{ stock.currentStock }} / {{ stock.minStockThreshold }} min</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
