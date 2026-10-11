<script setup lang="ts">
import { computed } from 'vue'
import {
  Activity,
  AlertOctagon,
  CheckCircle2,
  Clock,
  Flame,
  Gauge,
  TrendingUp,
  Cpu
} from 'lucide-vue-next'

const props = defineProps<{
  metrics: {
    totalMachines: number
    activeMachines: number
    warningMachines: number
    maintenanceMachines: number
    decommissionedMachines: number
    lineStopsCount: number
    oeePercent: number
    availabilityPercent: number
    avgTemperature: number
    avgVibrationRms: number
    mttrMinutes: number
    mtbfHours: number
  }
}>()
</script>

<template>
  <div class="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3">
    <!-- 1. OEE Metric -->
    <div class="bg-[#15181e] rounded-xl p-3.5 border border-[#232730] flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-teal-500/40 transition-colors">
      <div class="flex items-center justify-between text-zinc-400">
        <span class="text-[10px] font-mono uppercase tracking-wider font-semibold">Plant OEE</span>
        <TrendingUp class="w-4 h-4 text-teal-400" />
      </div>
      <div class="mt-2">
        <div class="text-2xl font-bold font-mono tracking-tight text-white flex items-baseline gap-1">
          <span>{{ metrics.oeePercent }}</span>
          <span class="text-xs text-zinc-400 font-normal">%</span>
        </div>
        <div class="flex items-center gap-1.5 mt-1 text-[10px] font-mono text-zinc-400">
          <span class="text-emerald-400 font-semibold">Avail: {{ metrics.availabilityPercent }}%</span>
          <span>•</span>
          <span>Target: 95%</span>
        </div>
      </div>
      <div class="absolute bottom-0 left-0 right-0 h-0.5 bg-gradient-to-r from-teal-500 to-emerald-500"></div>
    </div>

    <!-- 2. Active Machines / Total -->
    <div class="bg-[#15181e] rounded-xl p-3.5 border border-[#232730] flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-emerald-500/40 transition-colors">
      <div class="flex items-center justify-between text-zinc-400">
        <span class="text-[10px] font-mono uppercase tracking-wider font-semibold">Active Fleet</span>
        <CheckCircle2 class="w-4 h-4 text-emerald-400" />
      </div>
      <div class="mt-2">
        <div class="text-2xl font-bold font-mono tracking-tight text-white flex items-baseline gap-1.5">
          <span>{{ metrics.activeMachines }}</span>
          <span class="text-xs text-zinc-500 font-mono font-normal">/ {{ metrics.totalMachines }} Nodes</span>
        </div>
        <div class="flex items-center gap-2 mt-1 text-[10px] font-mono">
          <span v-if="metrics.warningMachines > 0" class="text-amber-400 font-medium">
            {{ metrics.warningMachines }} Warnings
          </span>
          <span v-if="metrics.maintenanceMachines > 0" class="text-sky-400">
            {{ metrics.maintenanceMachines }} Maint
          </span>
          <span v-if="metrics.warningMachines === 0 && metrics.maintenanceMachines === 0" class="text-emerald-400">
            100% Nominal
          </span>
        </div>
      </div>
      <div class="absolute bottom-0 left-0 right-0 h-0.5 bg-emerald-500/50"></div>
    </div>

    <!-- 3. Line Stops Alert -->
    <div
      class="bg-[#15181e] rounded-xl p-3.5 border flex flex-col justify-between shadow-sm relative overflow-hidden group transition-all"
      :class="metrics.lineStopsCount > 0 ? 'border-rose-500/60 bg-rose-950/10' : 'border-[#232730]'"
    >
      <div class="flex items-center justify-between text-zinc-400">
        <span class="text-[10px] font-mono uppercase tracking-wider font-semibold">Line Stops</span>
        <AlertOctagon class="w-4 h-4" :class="metrics.lineStopsCount > 0 ? 'text-rose-400 animate-pulse' : 'text-zinc-600'" />
      </div>
      <div class="mt-2">
        <div class="text-2xl font-bold font-mono tracking-tight flex items-baseline gap-1.5" :class="metrics.lineStopsCount > 0 ? 'text-rose-400' : 'text-zinc-200'">
          <span>{{ metrics.lineStopsCount }}</span>
          <span class="text-xs text-zinc-500 font-normal">Active</span>
        </div>
        <div class="mt-1 text-[10px] font-mono" :class="metrics.lineStopsCount > 0 ? 'text-rose-300 font-bold' : 'text-zinc-500'">
          {{ metrics.lineStopsCount > 0 ? 'IMMEDIATE DISPATCH' : 'Zero Bottlenecks' }}
        </div>
      </div>
      <div class="absolute bottom-0 left-0 right-0 h-0.5" :class="metrics.lineStopsCount > 0 ? 'bg-rose-500 animate-pulse' : 'bg-zinc-800'"></div>
    </div>

    <!-- 4. Sensor Stress (Vibration RMS) -->
    <div class="bg-[#15181e] rounded-xl p-3.5 border border-[#232730] flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-teal-500/40 transition-colors">
      <div class="flex items-center justify-between text-zinc-400">
        <span class="text-[10px] font-mono uppercase tracking-wider font-semibold">Fleet Vibration</span>
        <Activity class="w-4 h-4 text-teal-400" />
      </div>
      <div class="mt-2">
        <div class="text-2xl font-bold font-mono tracking-tight text-white flex items-baseline gap-1">
          <span>{{ metrics.avgVibrationRms }}</span>
          <span class="text-xs text-zinc-400 font-normal">mm/s</span>
        </div>
        <div class="flex items-center gap-1.5 mt-1 text-[10px] font-mono text-zinc-400">
          <span class="text-teal-400 font-medium">RMS Velocity</span>
          <span>•</span>
          <span class="text-zinc-500">ISO 10816-3</span>
        </div>
      </div>
      <div class="absolute bottom-0 left-0 right-0 h-0.5 bg-teal-500/50"></div>
    </div>

    <!-- 5. Mean Temp -->
    <div class="bg-[#15181e] rounded-xl p-3.5 border border-[#232730] flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-amber-500/40 transition-colors">
      <div class="flex items-center justify-between text-zinc-400">
        <span class="text-[10px] font-mono uppercase tracking-wider font-semibold">Mean Temp</span>
        <Flame class="w-4 h-4 text-amber-400" />
      </div>
      <div class="mt-2">
        <div class="text-2xl font-bold font-mono tracking-tight text-white flex items-baseline gap-1">
          <span>{{ metrics.avgTemperature }}</span>
          <span class="text-xs text-zinc-400 font-normal">°C</span>
        </div>
        <div class="flex items-center gap-1.5 mt-1 text-[10px] font-mono text-zinc-400">
          <span class="text-amber-400">Motor Envelopes</span>
          <span>•</span>
          <span class="text-zinc-500">&lt;65°C Nom</span>
        </div>
      </div>
      <div class="absolute bottom-0 left-0 right-0 h-0.5 bg-amber-500/50"></div>
    </div>

    <!-- 6. MTTR / Response Time -->
    <div class="bg-[#15181e] rounded-xl p-3.5 border border-[#232730] flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-sky-500/40 transition-colors">
      <div class="flex items-center justify-between text-zinc-400">
        <span class="text-[10px] font-mono uppercase tracking-wider font-semibold">Mean TTR</span>
        <Clock class="w-4 h-4 text-sky-400" />
      </div>
      <div class="mt-2">
        <div class="text-2xl font-bold font-mono tracking-tight text-white flex items-baseline gap-1">
          <span>{{ metrics.mttrMinutes }}</span>
          <span class="text-xs text-zinc-400 font-normal">min</span>
        </div>
        <div class="flex items-center gap-1.5 mt-1 text-[10px] font-mono text-zinc-400">
          <span class="text-sky-400">MTBF: {{ metrics.mtbfHours }}h</span>
        </div>
      </div>
      <div class="absolute bottom-0 left-0 right-0 h-0.5 bg-sky-500/50"></div>
    </div>
  </div>
</template>
