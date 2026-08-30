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
    <Card class="bg-card border-border shadow-sm">
      <CardContent class="p-3.5 flex items-center justify-between">
        <div>
          <span class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Total Open</span>
          <div class="text-xl font-bold text-foreground mt-0.5">
            {{ (metrics?.openCount || 0) + (metrics?.inProgressCount || 0) + (metrics?.pendingPartsCount || 0) }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-muted text-foreground border border-border">
          <Wrench class="h-4 w-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Critical Alerts -->
    <Card class="bg-card border-border shadow-sm" :class="{'border-destructive/40 bg-destructive/5': (metrics?.criticalCount || 0) > 0}">
      <CardContent class="p-3.5 flex items-center justify-between">
        <div>
          <span class="text-[10px] font-semibold uppercase tracking-wider text-destructive">Critical</span>
          <div class="text-xl font-bold text-destructive mt-0.5">
            {{ metrics?.criticalCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-destructive/10 text-destructive border border-destructive/20">
          <AlertTriangle class="h-4 w-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Pending Parts -->
    <Card class="bg-card border-border shadow-sm">
      <CardContent class="p-3.5 flex items-center justify-between">
        <div>
          <span class="text-[10px] font-semibold uppercase tracking-wider text-amber-500">Pending Parts</span>
          <div class="text-xl font-bold text-amber-500 mt-0.5">
            {{ metrics?.pendingPartsCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-amber-500/10 text-amber-500 border border-amber-500/20">
          <Package class="h-4 w-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Overdue SLA -->
    <Card class="bg-card border-border shadow-sm">
      <CardContent class="p-3.5 flex items-center justify-between">
        <div>
          <span class="text-[10px] font-semibold uppercase tracking-wider text-orange-500">Overdue SLA</span>
          <div class="text-xl font-bold text-orange-500 mt-0.5">
            {{ metrics?.overdueCount || 0 }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-orange-500/10 text-orange-500 border border-orange-500/20">
          <Clock class="h-4 w-4" />
        </div>
      </CardContent>
    </Card>

    <!-- Resolved Today -->
    <Card class="bg-card border-border shadow-sm">
      <CardContent class="p-3.5 flex items-center justify-between">
        <div>
          <span class="text-[10px] font-semibold uppercase tracking-wider text-emerald-500">Resolved</span>
          <div class="text-xl font-bold text-emerald-500 mt-0.5">
            {{ (metrics?.resolvedCount || 0) + (metrics?.closedCount || 0) }}
          </div>
        </div>
        <div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">
          <CheckCircle2 class="h-4 w-4" />
        </div>
      </CardContent>
    </Card>

    <!-- SLA Compliance -->
    <Card class="bg-card border-border shadow-sm">
      <CardContent class="p-3.5 flex items-center justify-between">
        <div>
          <span class="text-[10px] font-semibold uppercase tracking-wider text-primary">SLA Health</span>
          <div class="text-xl font-bold text-primary mt-0.5">
            {{ metrics?.slaCompliancePercent ?? 100 }}%
          </div>
        </div>
        <div class="p-2 rounded-lg bg-primary/10 text-primary border border-primary/20">
          <ShieldCheck class="h-4 w-4" />
        </div>
      </CardContent>
    </Card>
  </div>
</template>
