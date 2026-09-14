<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import FleetAnalyticsSummary, { type FleetSummaryData } from '~/components/analytics/FleetAnalyticsSummary.vue'
import TelemetryTrendVisualizer from '~/components/analytics/TelemetryTrendVisualizer.vue'
import KpiGraphBuilder from '~/components/analytics/KpiGraphBuilder.vue'
import PowerBiTileEmbed from '~/components/analytics/PowerBiTileEmbed.vue'
import GrafanaMassTelemetryEmbed from '~/components/analytics/GrafanaMassTelemetryEmbed.vue'
import AlertRulesManager from '~/components/analytics/AlertRulesManager.vue'
import { useAlertRules } from '~/composables/useAlertRules'
import {
  Activity,
  BarChart3,
  Sliders,
  Layers,
  Sparkles,
  RefreshCw,
  Clock,
  ExternalLink,
  Server,
  Bell
} from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()
const { activeAlertsCount, criticalAlertsCount } = useAlertRules()
type AnalyticsTab = 'overview' | 'rules-alerts' | 'predictive' | 'builder' | 'powerbi' | 'grafana'
const activeTab = ref<AnalyticsTab>('overview')
const isLoading = ref(false)

const resetAnalyticsView = () => {
  activeTab.value = 'overview'
  router.push('/dashboard/analytics')
  fetchFleetSummary()
}

const summaryData = ref<FleetSummaryData>({
  totalMachineHours: 2316,
  availabilityPercentage: 97.4,
  averageMtbfHours: 342.5,
  averageMttrMinutes: 34.2,
  slaCompliancePercentage: 96.8,
  topFaultingMachines: [
    { machineId: 'm-op20', name: 'OP20-Weld Laser Cell', line: 'Line 1 - Pre-Assembly', incidentCount: 14, totalDowntimeMinutes: 185, primaryAlarmCode: 'F-WELD-OPTIC-DIRT', healthIndex: 68.4 },
    { machineId: 'm-op50', name: 'OP50-Fasten Screwing Station', line: 'Line 2 - Fastening', incidentCount: 11, totalDowntimeMinutes: 142, primaryAlarmCode: 'E-TORQUE-OUT-OF-BOUNDS', healthIndex: 74.2 },
    { machineId: 'm-op10', name: 'OP10-Dispense Bonding Cell', line: 'Line 4 - Dispensing', incidentCount: 9, totalDowntimeMinutes: 98, primaryAlarmCode: 'W-NOZZLE-PRESSURE-LOW', healthIndex: 81.0 },
    { machineId: 'm-op30', name: 'OP30-Robotic Weld Station B', line: 'Line 5 - Robotic Welding', incidentCount: 7, totalDowntimeMinutes: 84, primaryAlarmCode: 'F-ROBOT-COLLISION-LIMIT', healthIndex: 85.5 },
    { machineId: 'm-op80', name: 'OP80-EOL Final High Voltage Test', line: 'Line 7 - Battery EOL', incidentCount: 5, totalDowntimeMinutes: 62, primaryAlarmCode: 'E-HV-ISOLATION-FAULT', healthIndex: 89.1 }
  ],
  maintenanceBacklog: {
    under24Hours: 8,
    oneToThreeDays: 4,
    overThreeDays: 2,
    criticalBreached: 1
  },
  stockDepletion: [
    { partNumber: 'SEW-DRV-MDX61B', description: 'SEW Movidrive Inverter Module', currentStock: 1, minStockThreshold: 3, criticality: 'Critical' },
    { partNumber: 'BECK-EL2008', description: 'Beckhoff 8-ch Digital Output Slice', currentStock: 2, minStockThreshold: 5, criticality: 'Warning' },
    { partNumber: 'PNOZ-X3-24V', description: 'Pilz Safety Relay 24VDC', currentStock: 0, minStockThreshold: 2, criticality: 'Critical' }
  ]
})

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
      // Retain defaults
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
    <!-- Header Hero Bar -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-6 rounded-2xl bg-zinc-900/90 border border-zinc-800 shadow-sm">
      <div class="space-y-1">
        <div
          role="button"
          tabindex="0"
          @click="resetAnalyticsView"
          @keydown.enter="resetAnalyticsView"
          class="flex items-center gap-2.5 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-zinc-800/60"
          title="Click to reset view and refresh analytics"
        >
          <div class="p-2 rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300 group-hover:scale-105 group-hover:bg-zinc-700 transition-all">
            <BarChart3 class="w-5 h-5" />
          </div>
          <div>
            <h2 class="text-lg font-bold text-zinc-100 flex items-center gap-2 group-hover:text-white transition-colors">
              <span>Predictive Maintenance & Fleet Analytics</span>
              <Badge variant="outline" class="text-[10px] font-mono border-zinc-700 bg-zinc-950 text-zinc-300">
                OT Analytics 2.0
              </Badge>
            </h2>
            <p class="text-xs text-zinc-400 group-hover:text-zinc-300 transition-colors">
              Fleet-wide machine degradation modeling, statistical Z-score anomaly detection, and enterprise BI connectivity.
            </p>
          </div>
        </div>
      </div>

      <!-- Quick Action Buttons -->
      <div class="flex items-center gap-2 shrink-0">
        <Button
          variant="outline"
          size="sm"
          @click="fetchFleetSummary"
          class="border-zinc-800 bg-zinc-950 text-zinc-300 hover:text-white text-xs h-9"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="w-3.5 h-3.5 mr-1.5" />
          <span>Sync Fleet Telemetry</span>
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
        <span>Fleet Overview & KPIs</span>
      </button>

      <button
        type="button"
        @click="activeTab = 'rules-alerts'"
        :class="activeTab === 'rules-alerts' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Bell class="w-4 h-4" />
        <span>Rules & Alert Automation</span>
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
        <span>Predictive Drift & Anomalies</span>
      </button>

      <button
        type="button"
        @click="activeTab = 'builder'"
        :class="activeTab === 'builder' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Sliders class="w-4 h-4" />
        <span>Custom KPI Builder</span>
      </button>

      <button
        type="button"
        @click="activeTab = 'powerbi'"
        :class="activeTab === 'powerbi' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Layers class="w-4 h-4" />
        <span>Power BI & External BI</span>
      </button>

      <button
        type="button"
        @click="activeTab = 'grafana'"
        :class="activeTab === 'grafana' ? 'bg-zinc-700 text-white shadow-sm border border-zinc-600/50' : 'text-zinc-400 hover:text-zinc-200'"
        class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 shrink-0 cursor-pointer"
      >
        <Server class="w-4 h-4" />
        <span>Grafana Mass Telemetry</span>
      </button>
    </div>

    <!-- Tab 1: Fleet Overview & KPIs -->
    <template v-if="activeTab === 'overview'">
      <FleetAnalyticsSummary :summary="summaryData" />
    </template>

    <!-- Tab 2: Rules & Alert Automation -->
    <template v-else-if="activeTab === 'rules-alerts'">
      <AlertRulesManager />
    </template>

    <!-- Tab 3: Predictive Drift & Anomalies -->
    <template v-else-if="activeTab === 'predictive'">
      <TelemetryTrendVisualizer initial-machine-id="m-op20" />
    </template>

    <!-- Tab 3: Custom KPI Builder -->
    <template v-else-if="activeTab === 'builder'">
      <KpiGraphBuilder />
    </template>

    <!-- Tab 4: Power BI & External BI -->
    <template v-else-if="activeTab === 'powerbi'">
      <PowerBiTileEmbed />
    </template>

    <!-- Tab 5: Grafana Mass Telemetry -->
    <template v-else-if="activeTab === 'grafana'">
      <GrafanaMassTelemetryEmbed />
    </template>
  </div>
</template>
