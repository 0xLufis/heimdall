<script setup lang="ts">
import { computed } from 'vue'
import { Activity, Flame, ShieldAlert, Cpu } from 'lucide-vue-next'
import type { MachineTwinNode } from '~/composables/useDigitalTwin'

const props = defineProps<{
  machines: MachineTwinNode[]
  selectedMachine: MachineTwinNode | null
  telemetryNoiseMap: Record<string, { waveHistory: number[] }>
}>()

const activeMachine = computed(() => {
  return props.selectedMachine || props.machines[0] || null
})

// Generate SVG waveform path from waveHistory
const sparklinePoints = computed(() => {
  if (!activeMachine.value) return ''
  const history = props.telemetryNoiseMap[activeMachine.value.id]?.waveHistory || [1.1, 1.2, 1.3, 1.1, 1.4, 1.2, 1.1, 1.5, 1.2, 1.3]
  const width = 280
  const height = 48
  const minVal = 0.5
  const maxVal = 4.0

  return history
    .map((val, idx) => {
      const x = (idx / (history.length - 1)) * width
      const normalizedY = (val - minVal) / (maxVal - minVal)
      const y = height - normalizedY * height
      return `${x.toFixed(1)},${y.toFixed(1)}`
    })
    .join(' ')
})
</script>

<template>
  <div class="bg-[#15181e] rounded-xl p-5 border border-[#232730] shadow-sm flex flex-col justify-between">
    <!-- Header -->
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-2">
        <Activity class="w-4 h-4 text-teal-400" />
        <span class="text-xs font-bold uppercase tracking-wider text-zinc-200">Real-Time Waveform & SPC Bounds</span>
      </div>
      <span class="text-[11px] font-mono text-zinc-500">
        Active Probe: <strong class="text-zinc-300">{{ activeMachine?.customIdentifier || 'LINE1-OP20' }}</strong>
      </span>
    </div>

    <!-- Live Sparkline Graphic Area -->
    <div class="bg-[#0c0e12] rounded-xl p-4 border border-[#232730] relative overflow-hidden">
      <!-- Upper Control Limit (UCL) Line -->
      <div class="absolute left-4 right-4 top-3 border-b border-dashed border-rose-500/40 flex justify-between text-[9px] font-mono text-rose-400">
        <span>UCL: 2.80 mm/s (Trip Threshold)</span>
        <span>+3σ Limit</span>
      </div>

      <!-- Center Nominal Line -->
      <div class="absolute left-4 right-4 top-1/2 border-b border-dashed border-zinc-700/50 flex justify-between text-[9px] font-mono text-zinc-600">
        <span>Target: 1.20 mm/s (Nominal Operating Mean)</span>
        <span>μ Center</span>
      </div>

      <!-- SVG Sparkline Waveform -->
      <div class="py-6 flex items-center justify-center">
        <svg class="w-full h-16 overflow-visible" viewBox="0 0 280 48" preserveAspectRatio="none">
          <defs>
            <linearGradient id="waveGradient" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stop-color="#2dd4bf" stop-opacity="0.3" />
              <stop offset="100%" stop-color="#2dd4bf" stop-opacity="0.0" />
            </linearGradient>
          </defs>
          <!-- Area fill under line -->
          <polygon
            v-if="sparklinePoints"
            :points="`0,48 ${sparklinePoints} 280,48`"
            fill="url(#waveGradient)"
          />
          <!-- Stroke line -->
          <polyline
            v-if="sparklinePoints"
            fill="none"
            stroke="#2dd4bf"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            :points="sparklinePoints"
          />
        </svg>
      </div>

      <!-- Current reading pill -->
      <div class="flex items-center justify-between pt-2 border-t border-[#232730] text-xs font-mono">
        <div class="flex items-center gap-1.5 text-zinc-400">
          <span class="w-2 h-2 rounded-full bg-teal-400 animate-pulse"></span>
          <span>Probe: Axis-1 Bearing Accelerometer</span>
        </div>
        <div class="text-teal-300 font-bold">
          {{ activeMachine?.telemetry?.vibrationRms || '1.18' }} mm/s RMS
        </div>
      </div>
    </div>

    <!-- Statistical Summary Footer -->
    <div class="grid grid-cols-3 gap-2 mt-4 pt-3 border-t border-[#232730]/60 text-center text-xs">
      <div>
        <div class="text-[9px] font-mono text-zinc-500 uppercase">Cp / Cpk</div>
        <div class="text-xs font-mono font-bold text-zinc-200 mt-0.5">1.64 / 1.58</div>
      </div>
      <div>
        <div class="text-[9px] font-mono text-zinc-500 uppercase">Mean Deviation</div>
        <div class="text-xs font-mono font-bold text-teal-400 mt-0.5">&plusmn; 0.08 mm/s</div>
      </div>
      <div>
        <div class="text-[9px] font-mono text-zinc-500 uppercase">Signal Confidence</div>
        <div class="text-xs font-mono font-bold text-emerald-400 mt-0.5">99.8%</div>
      </div>
    </div>
  </div>
</template>
