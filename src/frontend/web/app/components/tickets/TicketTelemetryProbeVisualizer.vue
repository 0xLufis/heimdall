<script setup lang="ts">
import { computed } from 'vue'
import {
  Activity,
  AlertTriangle,
  Radio,
  Gauge,
  TrendingUp,
  Clock,
  Layers,
  Cpu
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import type { TicketTelemetryProbe } from '~/types/maintenance'

const props = defineProps<{
  probes: TicketTelemetryProbe[] | undefined | null
  alarmCode?: string | null
  controllerSource?: string | null
  targetComponentBmk?: string | null
}>()

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'ThresholdExceeded':
      return {
        class: 'bg-rose-500/15 text-rose-600 dark:text-rose-400 border-rose-500/30',
        label: 'Threshold Exceeded'
      }
    case 'AlarmBitSet':
      return {
        class: 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30',
        label: 'Alarm Bit Tripped'
      }
    case 'RateOfChangeSpike':
      return {
        class: 'bg-purple-500/15 text-purple-600 dark:text-purple-400 border-purple-500/30',
        label: 'Rate of Change Spike'
      }
    default:
      return {
        class: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border-emerald-500/30',
        label: 'Nominal'
      }
  }
}

const formatTimestamp = (iso: string) => {
  try {
    return new Date(iso).toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      fractionalSecondDigits: 3
    })
  } catch {
    return iso
  }
}

// Generate simple SVG sparkline path for localized probe buffer
const generateSparklinePath = (points: Array<{ value: number }>) => {
  if (!points || points.length < 2) return ''
  const values = points.map(p => p.value)
  const min = Math.min(...values)
  const max = Math.max(...values)
  const range = max - min || 1
  const width = 160
  const height = 36

  return points
    .map((p, i) => {
      const x = (i / (points.length - 1)) * width
      const y = height - ((p.value - min) / range) * (height - 6) - 3
      return `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`
    })
    .join(' ')
}
</script>

<template>
  <div v-if="probes && probes.length > 0" class="space-y-3">
    <!-- Section Header with Controller Context -->
    <div class="flex flex-wrap items-center justify-between gap-2 p-3 rounded-xl bg-muted/40 border border-border">
      <div class="flex items-center gap-2">
        <div class="p-1.5 rounded-lg bg-rose-500/10 text-rose-500 border border-rose-500/20">
          <Activity class="size-4 animate-pulse" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <span class="text-xs font-bold text-foreground">Machine-Raised Telemetry Probes</span>
            <span v-if="alarmCode" class="font-mono text-[10px] px-1.5 py-0.2 rounded bg-rose-500/10 text-rose-600 dark:text-rose-400 font-bold border border-rose-500/30">
              {{ alarmCode }}
            </span>
          </div>
          <p class="text-[11px] text-muted-foreground">
            Point-in-time diagnostic capture from controller <code class="font-mono text-primary font-bold">{{ controllerSource || 'PLC' }}</code>
          </p>
        </div>
      </div>

      <div v-if="targetComponentBmk" class="flex items-center gap-1.5 text-xs">
        <span class="text-muted-foreground">Target BMK:</span>
        <Badge variant="outline" class="font-mono text-xs font-bold bg-background">
          {{ targetComponentBmk }}
        </Badge>
      </div>
    </div>

    <!-- Probe Cards Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
      <div
        v-for="probe in probes"
        :key="probe.probeId"
        class="p-4 rounded-xl border border-border bg-card/60 shadow-xs space-y-3"
      >
        <!-- Probe Header -->
        <div class="flex items-start justify-between gap-2">
          <div class="space-y-0.5">
            <div class="text-xs font-bold text-foreground flex items-center gap-1.5">
              <Radio class="size-3 text-primary" />
              <span>{{ probe.metricName || probe.probeId }}</span>
            </div>
            <div class="font-mono text-[10px] text-muted-foreground truncate max-w-[220px]">
              {{ probe.datapointKey }}
            </div>
          </div>
          <span
            class="px-2 py-0.5 rounded-md text-[10px] font-bold border shrink-0"
            :class="getStatusBadge(probe.triggerStatus).class"
          >
            {{ getStatusBadge(probe.triggerStatus).label }}
          </span>
        </div>

        <!-- Metric Reading & Tolerance Range -->
        <div class="flex items-baseline justify-between pt-1 border-t border-border/50">
          <div>
            <div class="font-mono text-xl font-black tracking-tight" :class="probe.triggerStatus === 'ThresholdExceeded' ? 'text-rose-500' : 'text-foreground'">
              <span v-if="probe.numericValue !== undefined && probe.numericValue !== null">
                {{ probe.numericValue.toFixed(2) }}
              </span>
              <span v-else>
                {{ probe.stringValue || 'TRIPPED' }}
              </span>
              <span class="text-xs font-sans font-normal text-muted-foreground ml-1">
                {{ probe.unit || '' }}
              </span>
            </div>
          </div>

          <div v-if="probe.nominalLow !== undefined && probe.nominalHigh !== undefined" class="text-right text-[10px] text-muted-foreground font-mono">
            <div>Nominal Range:</div>
            <div class="font-bold text-foreground">
              [{{ probe.nominalLow }} .. {{ probe.nominalHigh }}] {{ probe.unit }}
            </div>
          </div>
        </div>

        <!-- Localized High-Resolution Sparkline Buffer -->
        <div v-if="probe.probeBuffer && probe.probeBuffer.length > 1" class="space-y-1 bg-muted/20 p-2.5 rounded-lg border border-border/60">
          <div class="flex items-center justify-between text-[10px] text-muted-foreground">
            <span class="flex items-center gap-1">
              <TrendingUp class="size-3" />
              High-Res Buffer (&plusmn;30s trigger window)
            </span>
            <span class="font-mono">{{ probe.probeBuffer.length }} samples</span>
          </div>
          <div class="h-9 w-full flex items-center justify-center">
            <svg class="w-full h-9 overflow-visible" viewBox="0 0 160 36">
              <path
                :d="generateSparklinePath(probe.probeBuffer)"
                fill="none"
                stroke="currentColor"
                class="text-rose-500"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </div>
        </div>

        <!-- Sample Timestamp -->
        <div class="flex items-center justify-between text-[10px] text-muted-foreground pt-1 border-t border-border/30">
          <span class="flex items-center gap-1">
            <Clock class="size-3" />
            Sampled at: {{ formatTimestamp(probe.sampledAt) }}
          </span>
          <span class="font-mono text-[9px] uppercase tracking-wider text-muted-foreground/70">
            {{ probe.probeId }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>
