<script setup lang="ts">
import { computed } from 'vue'
import { Badge } from '~/components/ui/badge'
import TelemetryMetricCard from './TelemetryMetricCard.vue'
import type { useTelemetryStream } from '~/composables/useTelemetryStream'
import { Activity, Flame, Gauge, Zap, Radio, Timer, Waves } from 'lucide-vue-next'

const props = defineProps<{
  stream: ReturnType<typeof useTelemetryStream>
  stationName?: string
  /** Station target cycle time in seconds (from the station's production KPIs), when reported. */
  targetCycleTimeSeconds?: number | null
}>()

/** ISO 10816-3 zone boundaries for medium machines on rigid foundations (mm/s RMS). */
const VIBRATION_WARN_MM_S = 2.8
const VIBRATION_CRIT_MM_S = 7.1

const STATE_STYLES: Record<string, { label: string; class: string }> = {
  RUNNING: { label: 'RUNNING', class: 'border-emerald-500/40 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10' },
  IDLE: { label: 'IDLE', class: 'border-sky-500/40 text-sky-600 dark:text-sky-400 bg-sky-500/10' },
  WARNING: { label: 'WARNING', class: 'border-amber-500/40 text-amber-600 dark:text-amber-400 bg-amber-500/10' },
  FAULTED: { label: 'FAULTED', class: 'border-rose-500/40 text-rose-600 dark:text-rose-400 bg-rose-500/10' },
  MAINTENANCE: { label: 'MAINTENANCE', class: 'border-violet-500/40 text-violet-600 dark:text-violet-400 bg-violet-500/10' }
}

const stateBadge = computed(() =>
  STATE_STYLES[props.stream.latestPlcState.value] ?? { label: 'NO STATE REPORTED', class: 'border-border text-muted-foreground bg-muted/40' }
)

const formatAge = (ms: number | null) => {
  if (ms === null) return ''
  if (ms < 1500) return 'now'
  if (ms < 60_000) return `${Math.round(ms / 1000)} s ago`
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)} min ago`
  return `${Math.floor(ms / 3_600_000)} h ago`
}

const signalBadge = computed(() => {
  const age = formatAge(props.stream.ageMs.value)
  switch (props.stream.signalState.value) {
    case 'live':
      return { label: `LIVE · ${age}`, class: 'text-emerald-600 dark:text-emerald-400 border-emerald-500/30 bg-emerald-500/10', pulse: true }
    case 'stale':
      return { label: `STALE · last sample ${age}`, class: 'text-amber-600 dark:text-amber-400 border-amber-500/30 bg-amber-500/10', pulse: false }
    case 'waiting':
      return { label: 'WAITING FOR FIRST SAMPLE', class: 'text-muted-foreground border-border bg-muted/40', pulse: false }
    default:
      return { label: 'NO STATION SELECTED', class: 'text-muted-foreground border-border bg-muted/40', pulse: false }
  }
})

const targetCycleTimeMs = computed(() =>
  typeof props.targetCycleTimeSeconds === 'number' && props.targetCycleTimeSeconds > 0
    ? props.targetCycleTimeSeconds * 1000
    : null
)

const values = computed(() => props.stream.latestValues.value)
</script>

<template>
  <div class="space-y-4">
    <!-- Stream header: which station, what state, how fresh -->
    <div class="flex items-center justify-between gap-3 flex-wrap px-1">
      <div class="flex items-center gap-3 min-w-0">
        <div class="flex items-center gap-2 min-w-0">
          <Radio class="w-4 h-4 shrink-0" :class="signalBadge.pulse ? 'text-emerald-500 animate-pulse' : 'text-muted-foreground'" />
          <span class="text-xs font-bold uppercase tracking-wider text-foreground truncate">
            {{ stationName || 'No station selected' }}
          </span>
        </div>
        <Badge variant="outline" :class="stateBadge.class" class="text-[10px] px-2 py-0.5 font-mono shrink-0">
          {{ stateBadge.label }}
        </Badge>
      </div>

      <div class="flex items-center gap-2 text-[11px] font-mono text-muted-foreground">
        <span v-if="stream.sampleCount.value > 0">{{ stream.sampleCount.value }} samples</span>
        <Badge variant="outline" :class="signalBadge.class" class="text-[10px] uppercase">
          {{ signalBadge.label }}
        </Badge>
      </div>
    </div>

    <!-- Machine dynamics -->
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
      <TelemetryMetricCard
        label="Cycle Time"
        unit="ms"
        :digits="0"
        :value="values.cycleTimeMs"
        :buffer="stream.cycleTimeBuffer.value"
        :icon="Timer"
        color="#06b6d4"
        :target="targetCycleTimeMs"
      />
      <TelemetryMetricCard
        label="Vibration RMS"
        unit="mm/s"
        :digits="2"
        :value="values.vibrationMmS"
        :buffer="stream.vibrationBuffer.value"
        :icon="Waves"
        color="#f59e0b"
        :warn-at="VIBRATION_WARN_MM_S"
        :crit-at="VIBRATION_CRIT_MM_S"
      />
      <TelemetryMetricCard
        label="Temperature"
        unit="°C"
        :digits="1"
        :value="values.spindleTempC"
        :buffer="stream.temperatureBuffer.value"
        :icon="Flame"
        color="#f43f5e"
      />
      <TelemetryMetricCard
        label="Spindle Speed"
        unit="RPM"
        :digits="0"
        :value="values.spindleRpm"
        :buffer="stream.spindleRpmBuffer.value"
        :icon="Gauge"
        color="#10b981"
      />
      <TelemetryMetricCard
        label="Active Power"
        unit="kW"
        :digits="2"
        :value="values.powerKw"
        :buffer="stream.powerBuffer.value"
        :icon="Zap"
        color="#8b5cf6"
      />
      <TelemetryMetricCard
        label="Line Frequency"
        unit="Hz"
        :digits="2"
        :value="values.frequencyHz"
        :buffer="stream.frequencyBuffer.value"
        :icon="Activity"
        color="#0ea5e9"
      />
    </div>
  </div>
</template>
