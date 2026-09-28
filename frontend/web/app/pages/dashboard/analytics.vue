<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import FleetAnalyticsSummary, { type FleetSummaryData } from '~/components/analytics/FleetAnalyticsSummary.vue'
import TelemetryTrendVisualizer from '~/components/analytics/TelemetryTrendVisualizer.vue'
import KpiGraphBuilder from '~/components/analytics/KpiGraphBuilder.vue'
import AlertRulesManager from '~/components/analytics/AlertRulesManager.vue'
import { useAlertRules } from '~/composables/useAlertRules'
import {
  Activity,
  BarChart3,
  Sliders,
  Sparkles,
  RefreshCw,
  Bell
} from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()
const { activeAlertsCount } = useAlertRules()
type AnalyticsTab = 'overview' | 'rules-alerts' | 'predictive' | 'builder'
const activeTab = ref<AnalyticsTab>('overview')
const isLoading = ref(false)

const resetAnalyticsView = () => {
  activeTab.value = 'overview'
  router.push('/dashboard/analytics')
  fetchFleetSummary()
}

const summaryData = ref<FleetSummaryData | null>(null)

const fetchFleetSummary = async () => {
  isLoading.value = true
  try {
    const res = await $fetch<FleetSummaryData>('/api/proxy/v1/analytics/fleet-summary')
    if (res) summaryData.value = res
  } catch {
    try {
      const fallback = await $fetch<FleetSummaryData>('/api/analytics/fleet-summary')
      if (fallback) summaryData.value = fallback
    } catch {
      // Data unavailable — component handles empty state
    }
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  fetchFleetSummary()
})
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-6 rounded-2xl bg-zinc-900/90 border border-zinc-800 shadow-sm">
      <div class="space-y-1">
        <div
          role="button"
          tabindex="0"
          @click="resetAnalyticsView"
          @keydown.enter="resetAnalyticsView"
          class="flex items-center gap-2.5 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-zinc-800/60"
        >
          <div class="p-2 rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300 group-hover:scale-105 group-hover:bg-zinc-700 transition-all">
            <BarChart3 class="w-5 h-5" />
          </div>
          <div>
            <h2 class="text-lg font-bold text-zinc-100 group-hover:text-white transition-colors">
              Fleet Analytics
            </h2>
            <p class="text-xs text-zinc-400 group-hover:text-zinc-300 transition-colors">
              Machine degradation modeling, anomaly detection, and custom KPIs.
            </p>
          </div>
        </div>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <Button
          variant="outline"
          size="sm"
          @click="fetchFleetSummary"
          class="border-zinc-800 bg-zinc-950 text-zinc-300 hover:text-white text-xs h-9"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="w-3.5 h-3.5 mr-1.5" />
          <span>Refresh</span>
        </Button>
      </div>
    </div>

    <!-- Navigation Tabs -->
    <div class="flex items-center bg-zinc-950 p-1 rounded-xl border border-zinc-800 text-xs font-medium overflow-x-auto">
      <button
        type="button"
        @click="activeTab = 'overview'"
        :class="activeTab === 'overview' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Activity class="w-4 h-4" />
        <span>Fleet Overview</span>
      </button>

      <button
        type="button"
        @click="activeTab = 'rules-alerts'"
        :class="activeTab === 'rules-alerts' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Bell class="w-4 h-4" />
        <span>Rules & Alerts</span>
        <Badge v-if="activeAlertsCount > 0" class="text-[10px] px-1.5 py-0 bg-rose-500 text-white">
          {{ activeAlertsCount }}
        </Badge>
      </button>

      <button
        type="button"
        @click="activeTab = 'predictive'"
        :class="activeTab === 'predictive' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Sparkles class="w-4 h-4" />
        <span>Predictive Drift</span>
      </button>

      <button
        type="button"
        @click="activeTab = 'builder'"
        :class="activeTab === 'builder' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Sliders class="w-4 h-4" />
        <span>KPI Builder</span>
      </button>
    </div>

    <template v-if="activeTab === 'overview'">
      <FleetAnalyticsSummary v-if="summaryData" :summary="summaryData" />
      <div v-else-if="isLoading" class="text-zinc-500 text-sm text-center py-16">Loading fleet summary...</div>
      <div v-else class="text-zinc-500 text-sm text-center py-16">Fleet summary unavailable.</div>
    </template>

    <template v-else-if="activeTab === 'rules-alerts'">
      <AlertRulesManager />
    </template>

    <template v-else-if="activeTab === 'predictive'">
      <TelemetryTrendVisualizer initial-machine-id="" />
    </template>

    <template v-else-if="activeTab === 'builder'">
      <KpiGraphBuilder />
    </template>
  </div>
</template>
