<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Layers,
  Flame,
  Network,
  AlertTriangle,
  ZoomIn,
  ZoomOut,
  RotateCcw,
  Maximize2,
  Minimize2,
  Activity,
  Cpu,
  Wrench,
  ChevronRight
} from 'lucide-vue-next'
import type { MachineTwinNode } from '~/composables/useDigitalTwin'

const props = defineProps<{
  machines: MachineTwinNode[]
  selectedMachineId: string | null
  activeLayer: 'status' | 'thermal' | 'network' | 'incidents'
  lineStopCount: number
}>()

const emit = defineEmits<{
  (e: 'select-machine', id: string): void
  (e: 'set-layer', layer: 'status' | 'thermal' | 'network' | 'incidents'): void
}>()

const zoomLevel = ref(1)
const isFullscreen = ref(false)
const canvasContainer = ref<HTMLElement | null>(null)

// Group machines by production line
const groupedLines = computed(() => {
  const groups: Record<string, MachineTwinNode[]> = {}
  props.machines.forEach(m => {
    const line = m.groupId || 'Line 1'
    if (!groups[line]) groups[line] = []
    groups[line].push(m)
  })
  return groups
})

const handleZoomIn = () => {
  zoomLevel.value = Math.min(1.8, Number((zoomLevel.value + 0.15).toFixed(2)))
}

const handleZoomOut = () => {
  zoomLevel.value = Math.max(0.65, Number((zoomLevel.value - 0.15).toFixed(2)))
}

const handleResetZoom = () => {
  zoomLevel.value = 1
}

const toggleFullscreen = () => {
  if (!canvasContainer.value) return
  if (!isFullscreen.value) {
    if (canvasContainer.value.requestFullscreen) {
      canvasContainer.value.requestFullscreen()
    }
    isFullscreen.value = true
  } else {
    if (document.exitFullscreen) {
      document.exitFullscreen()
    }
    isFullscreen.value = false
  }
}

// Helpers for visual node styling based on active layer
const getNodeBorderClass = (m: MachineTwinNode) => {
  const isSelected = m.id === props.selectedMachineId
  if (isSelected) return 'ring-2 ring-teal-400 border-teal-400 shadow-[0_0_15px_rgba(45,212,191,0.3)]'

  if (props.activeLayer === 'incidents') {
    if (m.operationalState === 'Faulted' || m.customIdentifier?.includes('OP20')) {
      return 'border-rose-500/80 shadow-[0_0_12px_rgba(244,63,94,0.3)]'
    }
  }

  if (props.activeLayer === 'thermal') {
    const temp = m.telemetry?.temperature || 30
    if (temp > 60) return 'border-amber-500/80 shadow-[0_0_12px_rgba(245,158,11,0.3)]'
    if (temp > 50) return 'border-teal-500/60'
    return 'border-slate-700'
  }

  if (m.status === 'active') {
    if (m.operationalState === 'Warning') return 'border-amber-500/80 ring-1 ring-amber-500/40'
    return 'border-emerald-500/60 ring-1 ring-emerald-500/20'
  }
  if (m.status === 'maintenance') return 'border-sky-500/80 ring-1 ring-sky-500/30'
  if (m.status === 'decommissioned' || m.status === 'dismantled') return 'border-zinc-800 opacity-60'
  return 'border-zinc-700'
}

const getNodeStatusBadge = (m: MachineTwinNode) => {
  if (m.status === 'decommissioned' || m.status === 'dismantled') {
    return { text: 'DECOM', color: 'bg-zinc-800 text-zinc-400 border-zinc-700' }
  }
  if (m.status === 'maintenance') {
    return { text: 'MAINT', color: 'bg-sky-500/10 text-sky-400 border-sky-500/30' }
  }
  if (m.operationalState === 'Warning') {
    return { text: 'WARN', color: 'bg-amber-500/10 text-amber-400 border-amber-500/30' }
  }
  if (m.operationalState === 'Faulted') {
    return { text: 'STOP', color: 'bg-rose-500/10 text-rose-400 border-rose-500/30' }
  }
  if (m.status === 'active') {
    return { text: 'RUNNING', color: 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30' }
  }
  return { text: 'IDLE', color: 'bg-zinc-800 text-zinc-400 border-zinc-700' }
}
</script>

<template>
  <div
    ref="canvasContainer"
    class="relative w-full rounded-xl bg-[#0e1116] border border-[#232730] overflow-hidden shadow-2xl flex flex-col min-h-[460px]"
  >
    <!-- Digital Twin Top Toolbar -->
    <div class="px-4 py-3 bg-[#15181e]/90 backdrop-blur-md border-b border-[#232730] flex flex-wrap items-center justify-between gap-3 z-20">
      <div class="flex items-center gap-3">
        <div class="flex items-center gap-2">
          <div class="relative flex h-3 w-3">
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span class="relative inline-flex rounded-full h-3 w-3 bg-emerald-500"></span>
          </div>
          <span class="text-xs font-semibold uppercase tracking-wider text-zinc-200">Digital Twin Topography</span>
        </div>
        <span class="text-xs text-zinc-500 font-mono">|</span>
        <span class="text-xs text-zinc-400 font-mono hidden sm:inline">2D Spatial Topography & Live Telemetry Topography</span>
      </div>

      <!-- Layer Switcher -->
      <div class="flex items-center gap-1 bg-[#0c0e12] p-1 rounded-lg border border-[#232730]">
        <button
          @click="emit('set-layer', 'status')"
          class="flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-medium transition-all"
          :class="activeLayer === 'status' ? 'bg-zinc-800 text-white shadow-xs' : 'text-zinc-400 hover:text-zinc-200'"
          title="Operational Status Layer"
        >
          <Layers class="w-3.5 h-3.5" />
          <span>Status</span>
        </button>

        <button
          @click="emit('set-layer', 'thermal')"
          class="flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-medium transition-all"
          :class="activeLayer === 'thermal' ? 'bg-zinc-800 text-amber-300 shadow-xs' : 'text-zinc-400 hover:text-zinc-200'"
          title="Thermal & Vibration Heatmap"
        >
          <Flame class="w-3.5 h-3.5" />
          <span>Heatmap</span>
        </button>

        <button
          @click="emit('set-layer', 'network')"
          class="flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-medium transition-all"
          :class="activeLayer === 'network' ? 'bg-zinc-800 text-sky-300 shadow-xs' : 'text-zinc-400 hover:text-zinc-200'"
          title="Edge IPC & Network Links"
        >
          <Network class="w-3.5 h-3.5" />
          <span>OT Bus</span>
        </button>

        <button
          @click="emit('set-layer', 'incidents')"
          class="flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-medium transition-all"
          :class="activeLayer === 'incidents' ? 'bg-zinc-800 text-rose-300 shadow-xs' : 'text-zinc-400 hover:text-zinc-200'"
          title="Incidents & Line Stops"
        >
          <AlertTriangle class="w-3.5 h-3.5" />
          <span>Incidents</span>
        </button>
      </div>

      <!-- Zoom & Canvas Controls -->
      <div class="flex items-center gap-1">
        <button
          @click="handleZoomOut"
          class="p-1.5 rounded-lg bg-[#0c0e12] border border-[#232730] text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          title="Zoom Out"
        >
          <ZoomOut class="w-4 h-4" />
        </button>
        <span class="text-[11px] font-mono text-zinc-400 px-1 select-none">{{ Math.round(zoomLevel * 100) }}%</span>
        <button
          @click="handleZoomIn"
          class="p-1.5 rounded-lg bg-[#0c0e12] border border-[#232730] text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          title="Zoom In"
        >
          <ZoomIn class="w-4 h-4" />
        </button>
        <button
          @click="handleResetZoom"
          class="p-1.5 rounded-lg bg-[#0c0e12] border border-[#232730] text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          title="Reset View"
        >
          <RotateCcw class="w-4 h-4" />
        </button>
        <button
          @click="toggleFullscreen"
          class="p-1.5 rounded-lg bg-[#0c0e12] border border-[#232730] text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          title="Toggle Fullscreen"
        >
          <Maximize2 v-if="!isFullscreen" class="w-4 h-4" />
          <Minimize2 v-else class="w-4 h-4" />
        </button>
      </div>
    </div>

    <!-- Spatial Factory Floor Grid Canvas -->
    <div
      class="flex-1 w-full p-8 overflow-auto relative select-none flex flex-col justify-center min-h-[380px]"
      :style="{
        backgroundImage: 'radial-gradient(rgba(255, 255, 255, 0.05) 1px, transparent 1px)',
        backgroundSize: '24px 24px'
      }"
    >
      <div
        class="transition-transform duration-200 origin-top-left flex flex-col gap-10 min-w-max pb-6"
        :style="{ transform: `scale(${zoomLevel})` }"
      >
        <!-- Render each Production Line -->
        <div
          v-for="(lineMachines, lineName) in groupedLines"
          :key="lineName"
          class="relative bg-[#15181e]/60 border border-[#232730] rounded-2xl p-5 shadow-lg"
        >
          <!-- Production Line Label & Status -->
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2.5">
              <span class="text-xs font-bold uppercase tracking-widest text-zinc-400">{{ lineName }}</span>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono font-medium bg-zinc-800/80 text-zinc-300 border border-zinc-700">
                {{ lineMachines.length }} Stations
              </span>
            </div>
            <div class="flex items-center gap-2 text-[11px] font-mono text-zinc-500">
              <span class="inline-block w-2 h-2 rounded-full bg-emerald-500"></span>
              Flow Direction: OP10 &rarr; OP60
            </div>
          </div>

          <!-- Production Line Conveyor Track & Connected Stations -->
          <div class="relative flex items-center gap-6 pt-3">
            <!-- Background Interconnecting Process Pipeline -->
            <div class="absolute left-8 right-8 top-[52px] h-1.5 bg-[#232730] -z-0 rounded-full overflow-hidden">
              <div class="h-full w-full bg-gradient-to-r from-teal-500/20 via-emerald-500/40 to-teal-500/20 animate-pulse"></div>
            </div>

            <!-- Station Node Cards along the line -->
            <div
              v-for="(machine, sIdx) in lineMachines"
              :key="machine.id"
              @click="emit('select-machine', machine.id)"
              class="relative z-10 w-44 rounded-xl bg-[#15181e] border p-3.5 cursor-pointer transition-all hover:-translate-y-1 hover:shadow-xl group"
              :class="getNodeBorderClass(machine)"
            >
              <!-- Top node header: ID & Status Badge -->
              <div class="flex items-center justify-between mb-2">
                <span class="text-[11px] font-mono font-bold tracking-tight text-zinc-200 group-hover:text-teal-300 transition-colors">
                  {{ machine.customIdentifier }}
                </span>
                <span
                  class="px-1.5 py-0.5 rounded text-[9px] font-mono font-bold uppercase border"
                  :class="getNodeStatusBadge(machine).color"
                >
                  {{ getNodeStatusBadge(machine).text }}
                </span>
              </div>

              <!-- Machine Type / Name -->
              <div class="text-xs font-medium text-zinc-300 truncate mb-2.5" :title="machine.displayName || machine.name">
                {{ machine.displayName || machine.name }}
              </div>

              <!-- Visual Representation / Iconography & Mini Gauge -->
              <div class="bg-[#0c0e12] rounded-lg p-2 border border-[#232730] flex items-center justify-between gap-2">
                <div class="flex flex-col">
                  <span class="text-[9px] font-mono text-zinc-500 uppercase">Type</span>
                  <span class="text-[11px] font-medium text-zinc-300 truncate max-w-[85px]">{{ machine.machineType }}</span>
                </div>
                <div class="text-right flex flex-col items-end">
                  <span class="text-[9px] font-mono text-zinc-500 uppercase">
                    {{ activeLayer === 'thermal' ? 'Temp' : 'Vib' }}
                  </span>
                  <span
                    class="text-[11px] font-mono font-semibold"
                    :class="activeLayer === 'thermal' ? 'text-amber-400' : 'text-teal-400'"
                  >
                    {{ activeLayer === 'thermal' ? `${machine.telemetry?.temperature || 35}°C` : `${machine.telemetry?.vibrationRms || 1.1} mm/s` }}
                  </span>
                </div>
              </div>

              <!-- Bottom Meta / Edge IPC / Criticality tag -->
              <div class="mt-2.5 pt-2 border-t border-[#232730] flex items-center justify-between text-[10px] text-zinc-500">
                <span class="font-mono flex items-center gap-1 truncate max-w-[100px]">
                  <Cpu class="w-3 h-3 text-zinc-400" />
                  {{ machine.controllers?.[0]?.hostname || 'IPC-EDGE' }}
                </span>
                <span
                  v-if="machine.criticality === 'Tier1_Critical'"
                  class="text-[9px] font-bold text-rose-400 font-mono"
                  title="Tier 1 Critical Bottleneck"
                >
                  T1
                </span>
                <span v-else class="text-[9px] font-mono text-zinc-500">
                  T2
                </span>
              </div>

              <!-- Connecting flow arrow to next station -->
              <div
                v-if="sIdx < lineMachines.length - 1"
                class="absolute -right-4 top-[48px] transform -translate-y-1/2 z-20 text-zinc-600 group-hover:text-teal-400 transition-colors"
              >
                <ChevronRight class="w-3.5 h-3.5" />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Bottom Status Legend -->
    <div class="px-4 py-2.5 bg-[#15181e]/80 border-t border-[#232730] flex flex-wrap items-center justify-between text-xs text-zinc-400 gap-2 z-20">
      <div class="flex items-center gap-4">
        <span class="font-mono text-[10px] uppercase text-zinc-500">Legend:</span>
        <div class="flex items-center gap-1.5 font-mono text-[11px]">
          <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
          <span>Running</span>
        </div>
        <div class="flex items-center gap-1.5 font-mono text-[11px]">
          <span class="w-2 h-2 rounded-full bg-amber-500"></span>
          <span>Warning</span>
        </div>
        <div class="flex items-center gap-1.5 font-mono text-[11px]">
          <span class="w-2 h-2 rounded-full bg-sky-500"></span>
          <span>Maintenance</span>
        </div>
        <div class="flex items-center gap-1.5 font-mono text-[11px]">
          <span class="w-2 h-2 rounded-full bg-rose-500"></span>
          <span>Line Stop</span>
        </div>
        <div class="flex items-center gap-1.5 font-mono text-[11px]">
          <span class="w-2 h-2 rounded-full bg-zinc-600"></span>
          <span>Decommissioned</span>
        </div>
      </div>

      <div class="text-[11px] font-mono text-zinc-500">
        Click any station to open the Digital Twin Inspector
      </div>
    </div>
  </div>
</template>
