<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Sliders,
  RotateCcw,
  Layout,
  Layers,
  Filter,
  CheckCircle2,
  RefreshCw,
  Sparkles
} from 'lucide-vue-next'
import { useDigitalTwin } from '~/composables/useDigitalTwin'
import DigitalTwinCanvas from './DigitalTwinCanvas.vue'
import DigitalTwinInspector from './DigitalTwinInspector.vue'
import DigitalTwinKpis from './DigitalTwinKpis.vue'
import DigitalTwinWaveform from './DigitalTwinWaveform.vue'
import DigitalTwinIncidentRadar from './DigitalTwinIncidentRadar.vue'
import DigitalTwinMachineGrid from './DigitalTwinMachineGrid.vue'
import DigitalTwinTechnologyMatrix from './DigitalTwinTechnologyMatrix.vue'
import DigitalTwinEdgeFleet from './DigitalTwinEdgeFleet.vue'
import DigitalTwinCustomizerModal from './DigitalTwinCustomizerModal.vue'

const {
  machines,
  tickets,
  controllers,
  isLoading,
  error,
  selectedMachineId,
  selectedMachine,
  activeLayer,
  searchQuery,
  selectedLine,
  selectedStatus,
  selectedTechnology,
  selectedCriticality,
  productionLines,
  technologyDisciplines,
  filteredMachines,
  plantMetrics,
  activeLineStopTickets,
  totalOpenTicketsCount,
  telemetryNoiseMap,
  layoutPreset,
  density,
  themeAccent,
  autoRefreshSeconds,
  isSimulationActive,
  widgetsConfig,
  fetchData,
  selectMachine,
  setLayer,
  setLayoutPreset,
  toggleWidget,
  setDensity,
  setThemeAccent,
  resetFilters
} = useDigitalTwin()

const isCustomizerOpen = ref(false)
const showInspector = ref(true)

const isWidgetVisible = (id: string) => {
  return widgetsConfig.value.find(w => w.id === id)?.visible ?? true
}

// Density wrapper styling class
const densityClass = computed(() => {
  if (density.value === 'compact') return 'space-y-3'
  if (density.value === 'spacious') return 'space-y-6'
  return 'space-y-4' // comfortable
})
</script>

<template>
  <div class="relative w-full flex flex-col min-h-screen bg-[#0c0e12] text-zinc-100">
    <!-- Top Action & Customizer Ribbon -->
    <div class="px-6 py-3 bg-[#11141a] border-b border-[#232730] flex flex-wrap items-center justify-between gap-3 sticky top-16 z-20">
      <div class="flex items-center gap-3">
        <!-- Layout Presets Quick Chips -->
        <span class="text-[10px] font-mono uppercase tracking-wider text-zinc-500 font-bold hidden sm:inline">
          View Preset:
        </span>
        <div class="flex items-center gap-1 bg-[#0c0e12] p-1 rounded-lg border border-[#232730]">
          <button
            @click="setLayoutPreset('command-center')"
            class="px-2.5 py-1 rounded text-xs font-medium transition-all"
            :class="layoutPreset === 'command-center' ? 'bg-zinc-800 text-teal-300 font-semibold' : 'text-zinc-400 hover:text-zinc-200'"
          >
            Command Center
          </button>
          <button
            @click="setLayoutPreset('spatial-focus')"
            class="px-2.5 py-1 rounded text-xs font-medium transition-all"
            :class="layoutPreset === 'spatial-focus' ? 'bg-zinc-800 text-teal-300 font-semibold' : 'text-zinc-400 hover:text-zinc-200'"
          >
            Spatial Focus
          </button>
          <button
            @click="setLayoutPreset('maintenance-lead')"
            class="px-2.5 py-1 rounded text-xs font-medium transition-all"
            :class="layoutPreset === 'maintenance-lead' ? 'bg-zinc-800 text-rose-300 font-semibold' : 'text-zinc-400 hover:text-zinc-200'"
          >
            Maintenance Lead
          </button>
          <button
            @click="setLayoutPreset('controls-engineer')"
            class="px-2.5 py-1 rounded text-xs font-medium transition-all"
            :class="layoutPreset === 'controls-engineer' ? 'bg-zinc-800 text-teal-300 font-semibold' : 'text-zinc-400 hover:text-zinc-200'"
          >
            Controls &amp; Waves
          </button>
        </div>
      </div>

      <!-- Customization Hub & Refresh Actions -->
      <div class="flex items-center gap-2">
        <button
          @click="fetchData"
          class="p-1.5 rounded-lg bg-[#0c0e12] border border-[#232730] text-zinc-400 hover:text-white transition-colors"
          title="Manual Telemetry Refresh"
        >
          <RefreshCw class="w-4 h-4" :class="{ 'animate-spin': isLoading }" />
        </button>

        <button
          @click="isCustomizerOpen = true"
          class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[#0c0e12] hover:bg-zinc-800 border border-[#232730] hover:border-zinc-700 text-zinc-200 text-xs font-semibold transition-all shadow-xs"
        >
          <Sliders class="w-3.5 h-3.5 text-teal-400" />
          <span>Customize Layout</span>
        </button>
      </div>
    </div>

    <!-- Main Workspace with Split Inspector -->
    <div class="flex-1 flex overflow-hidden">
      <!-- Left Dashboard Widgets Area -->
      <main class="flex-1 p-6 overflow-y-auto max-h-[calc(100vh-120px)]" :class="densityClass">
        <!-- 1. KPI MATRIX WIDGET -->
        <section v-if="isWidgetVisible('kpi-matrix')">
          <DigitalTwinKpis :metrics="plantMetrics" />
        </section>

        <!-- 2. SPATIAL TOPOGRAPHY CANVAS WIDGET -->
        <section v-if="isWidgetVisible('twin-canvas')">
          <DigitalTwinCanvas
            :machines="machines"
            :selected-machine-id="selectedMachineId"
            :active-layer="activeLayer"
            :line-stop-count="plantMetrics.lineStopsCount"
            @select-machine="id => { selectMachine(id); showInspector = true }"
            @set-layer="setLayer"
          />
        </section>

        <!-- 3. LIVE WAVEFORMS & INCIDENT RADAR DUAL ROW -->
        <div
          v-if="isWidgetVisible('live-waveforms') || isWidgetVisible('incident-radar')"
          class="grid grid-cols-1 lg:grid-cols-2 gap-4"
        >
          <DigitalTwinWaveform
            v-if="isWidgetVisible('live-waveforms')"
            :machines="machines"
            :selected-machine="selectedMachine"
            :telemetry-noise-map="telemetryNoiseMap"
          />

          <DigitalTwinIncidentRadar
            v-if="isWidgetVisible('incident-radar')"
            :tickets="tickets"
            :line-stop-count="plantMetrics.lineStopsCount"
          />
        </div>

        <!-- 4. CONNECTED MACHINE FLEET DIRECTORY WIDGET -->
        <section v-if="isWidgetVisible('fleet-matrix')">
          <DigitalTwinMachineGrid
            :machines="filteredMachines"
            :selected-machine-id="selectedMachineId"
            :search-query="searchQuery"
            :selected-line="selectedLine"
            :selected-status="selectedStatus"
            :selected-technology="selectedTechnology"
            :selected-criticality="selectedCriticality"
            :production-lines="productionLines"
            :technology-disciplines="technologyDisciplines"
            @update:search-query="val => searchQuery = val"
            @update:selected-line="val => selectedLine = val"
            @update:selected-status="val => selectedStatus = val"
            @update:selected-technology="val => selectedTechnology = val"
            @update:selected-criticality="val => selectedCriticality = val"
            @select-machine="id => { selectMachine(id); showInspector = true }"
            @reset-filters="resetFilters"
          />
        </section>

        <!-- 5. TECHNOLOGY BREAKDOWN & EDGE IPC FLEET DUAL ROW -->
        <div
          v-if="isWidgetVisible('tech-breakdown') || isWidgetVisible('edge-status')"
          class="grid grid-cols-1 lg:grid-cols-2 gap-4"
        >
          <DigitalTwinTechnologyMatrix
            v-if="isWidgetVisible('tech-breakdown')"
            :machines="machines"
            :selected-technology="selectedTechnology"
            @select-technology="t => selectedTechnology = t"
          />

          <DigitalTwinEdgeFleet
            v-if="isWidgetVisible('edge-status')"
            :controllers="controllers"
          />
        </div>
      </main>

      <!-- Right Docked Digital Twin Inspector Drawer -->
      <DigitalTwinInspector
        v-if="showInspector && selectedMachine"
        :machine="selectedMachine"
        :active-tickets="tickets"
        @close="showInspector = false"
      />
    </div>

    <!-- Customizer Studio Modal -->
    <DigitalTwinCustomizerModal
      :is-open="isCustomizerOpen"
      :widgets="widgetsConfig"
      :layout-preset="layoutPreset"
      :density="density"
      :theme-accent="themeAccent"
      :auto-refresh-seconds="autoRefreshSeconds"
      :is-simulation-active="isSimulationActive"
      @close="isCustomizerOpen = false"
      @toggle-widget="toggleWidget"
      @set-preset="setLayoutPreset"
      @set-density="setDensity"
      @set-theme="setThemeAccent"
      @update:auto-refresh-seconds="sec => autoRefreshSeconds = sec"
      @update:is-simulation-active="val => isSimulationActive = val"
    />
  </div>
</template>
