<script setup lang="ts">
import { computed } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import CanvasSparkline from './CanvasSparkline.vue'
import IndustrialGauge from './IndustrialGauge.vue'
import type { useTelemetryStream } from '~/composables/useTelemetryStream'
import { Activity, Flame, Cpu, Gauge, Zap, Radio } from 'lucide-vue-next'

const props = defineProps<{
  stream: ReturnType<typeof useTelemetryStream>
  stationName?: string
}>()

const plcStateBadge = computed(() => {
  const state = props.stream.latestPlcState.value
  switch (state) {
    case 'RUN':
      return { label: 'RUNNING', variant: 'outline', class: 'border-emerald-500/40 text-emerald-500 bg-emerald-500/10' }
    case 'MAINT':
      return { label: 'MAINTENANCE', variant: 'outline', class: 'border-amber-500/40 text-amber-500 bg-amber-500/10' }
    case 'STOP':
      return { label: 'STOPPED', variant: 'outline', class: 'border-rose-500/40 text-rose-500 bg-rose-500/10' }
    default:
      return { label: 'STANDBY', variant: 'outline', class: 'border-zinc-700 text-zinc-400 bg-zinc-800/40' }
  }
})
</script>

<template>
  <div class="space-y-4">
    <!-- Header Stream Bar -->
    <div class="flex items-center justify-between px-1">
      <div class="flex items-center gap-3">
        <div class="flex items-center gap-2">
          <Radio class="w-4 h-4 text-cyan-400 animate-pulse" />
          <span class="text-xs font-bold uppercase tracking-wider text-foreground">
            {{ stationName || 'Active Station' }} Live Telemetry Stream
          </span>
        </div>
        <Badge :class="plcStateBadge.class" class="text-[10px] px-2 py-0.5 font-mono">
          PLC {{ plcStateBadge.label }}
        </Badge>
      </div>

      <div class="flex items-center gap-2 text-xs font-mono text-muted-foreground">
        <span>Signal:</span>
        <Badge
          variant="outline"
          :class="stream.connectionState.value === 'connected' ? 'text-emerald-400 border-emerald-500/30' : 'text-zinc-500 border-zinc-700'"
          class="text-[10px] uppercase"
        >
          {{ stream.connectionState.value }}
        </Badge>
      </div>
    </div>

    <!-- 4-Card OT Telemetry Grid -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <!-- 1. Cycle Time -->
      <Card class="border border-border bg-card shadow-xs">
        <CardHeader class="pb-1 pt-3 px-4 flex flex-row items-center justify-between">
          <span class="text-xs font-bold uppercase tracking-wider text-muted-foreground">Cycle Time</span>
          <Activity class="w-4 h-4 text-cyan-400" />
        </CardHeader>
        <CardContent class="pb-3 px-4 space-y-2">
          <div class="flex items-baseline justify-between">
            <span class="text-2xl font-black font-mono tracking-tight tabular-nums text-foreground">
              {{ stream.latestValues.value.cycleTimeMs || '--' }}
            </span>
            <span class="text-xs font-mono text-muted-foreground">ms</span>
          </div>
          <CanvasSparkline :buffer="stream.cycleTimeBuffer.value" color="#06b6d4" :height="42" />
        </CardContent>
      </Card>

      <!-- 2. Kinematic Vibration -->
      <Card class="border border-border bg-card shadow-xs">
        <CardHeader class="pb-1 pt-3 px-4 flex flex-row items-center justify-between">
          <span class="text-xs font-bold uppercase tracking-wider text-muted-foreground">Vibration RMS</span>
          <Zap class="w-4 h-4 text-amber-400" />
        </CardHeader>
        <CardContent class="pb-3 px-4 space-y-2">
          <div class="flex items-baseline justify-between">
            <span class="text-2xl font-black font-mono tracking-tight tabular-nums text-foreground">
              {{ Number(stream.latestValues.value.vibrationMmS || 0).toFixed(2) }}
            </span>
            <span class="text-xs font-mono text-muted-foreground">mm/s</span>
          </div>
          <CanvasSparkline :buffer="stream.vibrationBuffer.value" color="#f59e0b" :height="42" />
        </CardContent>
      </Card>

      <!-- 3. Spindle Temperature -->
      <Card class="border border-border bg-card shadow-xs">
        <CardHeader class="pb-1 pt-3 px-4 flex flex-row items-center justify-between">
          <span class="text-xs font-bold uppercase tracking-wider text-muted-foreground">Spindle Temp</span>
          <Flame class="w-4 h-4 text-rose-400" />
        </CardHeader>
        <CardContent class="pb-3 px-4 flex items-center justify-around">
          <IndustrialGauge
            :value="Number(stream.latestValues.value.spindleTempC || 0)"
            :min="20"
            :max="90"
            unit="°C"
            :warnThreshold="65"
            :critThreshold="80"
            :size="90"
          />
        </CardContent>
      </Card>

      <!-- 4. Spindle Speed RPM -->
      <Card class="border border-border bg-card shadow-xs">
        <CardHeader class="pb-1 pt-3 px-4 flex flex-row items-center justify-between">
          <span class="text-xs font-bold uppercase tracking-wider text-muted-foreground">Spindle Speed</span>
          <Gauge class="w-4 h-4 text-emerald-400" />
        </CardHeader>
        <CardContent class="pb-3 px-4 space-y-2">
          <div class="flex items-baseline justify-between">
            <span class="text-2xl font-black font-mono tracking-tight tabular-nums text-foreground">
              {{ stream.latestValues.value.spindleRpm || '--' }}
            </span>
            <span class="text-xs font-mono text-muted-foreground">RPM</span>
          </div>
          <CanvasSparkline :buffer="stream.spindleRpmBuffer.value" color="#10b981" :height="42" />
        </CardContent>
      </Card>
    </div>
  </div>
</template>
