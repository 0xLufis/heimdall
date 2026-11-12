<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import FleetStationsExplorer from '~/components/analytics/FleetStationsExplorer.vue'
import FleetAnalyticsSummary, { type FleetSummaryData } from '~/components/analytics/FleetAnalyticsSummary.vue'
import TelemetryTrendVisualizer from '~/components/analytics/TelemetryTrendVisualizer.vue'
import AlertRulesManager from '~/components/analytics/AlertRulesManager.vue'
import { useAlertRules } from '~/composables/useAlertRules'
import { WINDOW_OPTIONS } from '~/utils/fleetAnalytics'
import { Activity, BarChart3, Bell, RefreshCw, Sparkles, Wrench } from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { activeAlertsCount } = useAlertRules()
type AnalyticsTab = 'overview' | 'maintenance' | 'rules-alerts' | 'predictive'
const activeTab = ref<AnalyticsTab>('overview')

const tabs: { key: AnalyticsTab; label: string; icon: unknown }[] = [
  { key: 'overview', label: 'Fleet Overview', icon: Activity },
  { key: 'maintenance', label: 'Maintenance', icon: Wrench },
  { key: 'rules-alerts', label: 'Rules & Alerts', icon: Bell },
  { key: 'predictive', label: 'Metric Trends', icon: Sparkles }
]

// Maintenance summary (tickets, stock, controller connectivity) for its own look-back window.
const summaryData = ref<FleetSummaryData | null>(null)
const summaryWindowDays = ref(30)
const summaryLoading = ref(false)
const summaryError = ref<string | null>(null)
let summaryRequest = 0

const fetchFleetSummary = async () => {
  const request = ++summaryRequest
  summaryLoading.value = true
  try {
    const res = await $fetch<FleetSummaryData>('/api/analytics/fleet-summary', { query: { windowDays: summaryWindowDays.value } })
    if (request !== summaryRequest) return
    if (!res || typeof res !== 'object' || !res.maintenanceBacklog) throw new Error('Unexpected fleet summary response')
    summaryData.value = res
    summaryError.value = null
  } catch (err: any) {
    if (request !== summaryRequest) return
    summaryError.value = err?.data?.message || err?.statusMessage || err?.message || 'Fleet summary is unavailable'
  } finally {
    if (request === summaryRequest) summaryLoading.value = false
  }
}

// Loaded when the tab is first opened and whenever its window changes.
watch([activeTab, summaryWindowDays], ([tab, days], [, previousDays]) => {
  if (tab === 'maintenance' && (!summaryData.value || days !== previousDays)) fetchFleetSummary()
})

// The overview keeps the page chrome fixed and scrolls the station list inside its own card.
const rootClass = computed(() =>
  activeTab.value === 'overview'
    ? 'flex flex-col gap-4 lg:h-[calc(100dvh-var(--header-height)-4rem)]'
    : 'space-y-6 pb-12'
)
</script>

<template>
  <div :class="rootClass">
    <!-- Header and tabs -->
    <div class="shrink-0 flex flex-col lg:flex-row lg:items-center justify-between gap-3">
      <div class="flex items-center gap-2.5 min-w-0">
        <div class="p-2 rounded-lg bg-muted border border-border text-foreground shrink-0">
          <BarChart3 class="w-5 h-5" />
        </div>
        <div class="min-w-0">
          <h2 class="text-lg font-bold text-foreground leading-tight">Fleet Analytics</h2>
          <p class="text-xs text-muted-foreground truncate">
            Station telemetry, tickets and controller status, grouped the way you choose.
          </p>
        </div>
      </div>

      <div class="flex items-center bg-card p-1 rounded-xl border border-border text-xs font-medium overflow-x-auto" role="tablist">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          type="button"
          role="tab"
          :aria-selected="activeTab === tab.key"
          :data-testid="`analytics-tab-${tab.key}`"
          :class="activeTab === tab.key ? 'bg-muted text-foreground shadow-xs border border-border' : 'text-muted-foreground hover:text-foreground border border-transparent'"
          class="px-3.5 py-1.5 rounded-lg transition-colors flex items-center gap-2 shrink-0 cursor-pointer"
          @click="activeTab = tab.key"
        >
          <component :is="tab.icon" class="w-4 h-4" />
          <span>{{ tab.label }}</span>
          <Badge v-if="tab.key === 'rules-alerts' && activeAlertsCount > 0" class="text-[10px] px-1.5 py-0">
            {{ activeAlertsCount }}
          </Badge>
        </button>
      </div>
    </div>

    <div v-if="activeTab === 'overview'" class="flex-1 min-h-0">
      <FleetStationsExplorer />
    </div>

    <template v-else-if="activeTab === 'maintenance'">
      <div class="flex flex-wrap items-center gap-2">
        <label class="flex items-center gap-1.5 text-xs text-muted-foreground">
          <span>Tickets of the last</span>
          <select
            v-model.number="summaryWindowDays"
            class="h-8 rounded-md border border-border bg-background px-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          >
            <option v-for="option in WINDOW_OPTIONS" :key="option.days" :value="option.days">{{ option.label }}</option>
          </select>
        </label>
        <Button variant="outline" size="sm" class="h-8 text-xs gap-1.5" @click="fetchFleetSummary">
          <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': summaryLoading }" />
          <span>Refresh</span>
        </Button>
        <span v-if="summaryError" class="text-xs text-foreground" role="alert">Fleet summary could not be loaded: {{ summaryError }}.</span>
      </div>
      <FleetAnalyticsSummary v-if="summaryData" :summary="summaryData" :class="{ 'opacity-70': summaryLoading }" />
      <div v-else-if="summaryLoading" class="text-muted-foreground text-sm text-center py-16">Loading fleet summary…</div>
      <div v-else class="text-muted-foreground text-sm text-center py-16">Fleet summary unavailable.</div>
    </template>

    <template v-else-if="activeTab === 'rules-alerts'">
      <AlertRulesManager />
    </template>

    <template v-else-if="activeTab === 'predictive'">
      <TelemetryTrendVisualizer initial-machine-id="" />
    </template>
  </div>
</template>
