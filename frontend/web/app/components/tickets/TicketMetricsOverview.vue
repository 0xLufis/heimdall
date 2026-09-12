<script setup lang="ts">
import { AlertTriangle, Clock, CheckCircle2, Wrench, Package, ShieldCheck } from 'lucide-vue-next'
import { Card, CardContent } from '~/components/ui/card'

const props = defineProps<{
  metrics?: {
    totalTickets: number
    openCount: number
    inProgressCount: number
    pendingPartsCount: number
    resolvedCount: number
    closedCount: number
    criticalCount: number
    overdueCount: number
    slaCompliancePercent: number
  }
}>()
</script>

<template>
  <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
    <!-- Total Active Tickets -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-slate-400">Total Open</span>
          <div class="text-xl font-bold text-slate-100 mt-0.5">
            {{ (metrics?.openCount || 0) + (metrics?.inProgressCount || 0) + (metrics?.pendingPartsCount || 0) }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-slate-800 text-slate-300">
          <Wrench class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Critical Alerts -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm" :class="{'border-rose-500/30 bg-rose-500/5': (metrics?.criticalCount || 0) > 0}">
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-rose-400">Critical</span>
          <div class="text-xl font-bold text-rose-400 mt-0.5">
            {{ metrics?.criticalCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-rose-500/10 text-rose-400 border border-rose-500/20">
          <AlertTriangle class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Pending Parts -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-amber-400">Pending Parts</span>
          <div class="text-xl font-bold text-amber-400 mt-0.5">
            {{ metrics?.pendingPartsCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/20">
          <Package class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Overdue SLA -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-amber-500">Overdue SLA</span>
          <div class="text-xl font-bold text-amber-500 mt-0.5">
            {{ metrics?.overdueCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-amber-500/10 text-amber-500 border border-amber-500/20">
          <Clock class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Resolved Today -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-emerald-400">Resolved</span>
          <div class="text-xl font-bold text-emerald-400 mt-0.5">
            {{ (metrics?.resolvedCount || 0) + (metrics?.closedCount || 0) }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
          <CheckCircle2 class="size-4" />
        </div>
      </CardContent>
    </Card>

    <!-- SLA Compliance -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardContent class="p-4 flex items-center justify-between">
        <div>
          <span class="text-xs font-medium text-indigo-400">SLA Health</span>
          <div class="text-xl font-bold text-indigo-400 mt-0.5">
            {{ metrics?.slaCompliancePercent ?? 100 }}%
          </div>
        </div>
        <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
          <ShieldCheck class="size-4" />
        </div>
      </CardContent>
    </Card>
  </div>
</template>
