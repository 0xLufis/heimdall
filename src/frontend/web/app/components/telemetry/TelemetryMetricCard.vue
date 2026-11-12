<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { Component } from 'vue'
import { Card, CardContent } from '~/components/ui/card'
import CanvasSparkline from './CanvasSparkline.vue'
import type { CircularBuffer } from '~/utils/CircularBuffer'

/**
 * One live measurement: current value, trend, and min / avg / max of the buffered window.
 * A value that was never reported renders as an explicit "not reported" state, never as 0.
 */
const props = withDefaults(
  defineProps<{
    label: string
    unit: string
    value: number | null
    buffer: CircularBuffer
    icon?: Component
    color?: string
    digits?: number
    /** Values at/above these are flagged; leave unset when no limit is known for the station. */
    warnAt?: number | null
    critAt?: number | null
    /** Reference the value is compared with (e.g. target cycle time), in the same unit. */
    target?: number | null
    targetLabel?: string
  }>(),
  {
    color: '#06b6d4',
    digits: 1,
    warnAt: null,
    critAt: null,
    target: null,
    targetLabel: 'Target'
  }
)

const fmt = (v: number | null | undefined) =>
  typeof v === 'number' && Number.isFinite(v) ? v.toFixed(props.digits) : '—'

const level = computed<'ok' | 'warn' | 'crit' | 'none'>(() => {
  if (props.value === null) return 'none'
  if (props.critAt !== null && props.value >= props.critAt) return 'crit'
  if (props.warnAt !== null && props.value >= props.warnAt) return 'warn'
  return 'ok'
})

// The ring buffer is mutated in place; re-read its statistics whenever the current value changes.
const stats = ref<{ min: number; avg: number; max: number; count: number } | null>(null)
watch(
  () => [props.value, props.buffer, props.buffer.version] as const,
  () => {
    const count = props.buffer.length
    stats.value = count > 0
      ? { min: props.buffer.min(), avg: props.buffer.avg(), max: props.buffer.max(), count }
      : null
  },
  { immediate: true }
)

const deltaToTarget = computed(() => {
  if (props.value === null || props.target === null || props.target === 0) return null
  return ((props.value - props.target) / props.target) * 100
})
</script>

<template>
  <Card
    class="border bg-card shadow-xs transition-colors"
    :class="{
      'border-rose-500/50': level === 'crit',
      'border-amber-500/50': level === 'warn',
      'border-border': level === 'ok' || level === 'none'
    }"
  >
    <CardContent class="p-4 space-y-2">
      <div class="flex items-center justify-between">
        <span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">{{ label }}</span>
        <component :is="icon" v-if="icon" class="w-4 h-4" :style="{ color }" />
      </div>

      <div class="flex items-baseline justify-between gap-2">
        <span
          class="text-2xl font-black font-mono tracking-tight tabular-nums"
          :class="{
            'text-rose-500': level === 'crit',
            'text-amber-500': level === 'warn',
            'text-foreground': level === 'ok',
            'text-muted-foreground/60': level === 'none'
          }"
        >
          {{ fmt(value) }}
        </span>
        <span class="text-xs font-mono text-muted-foreground">{{ unit }}</span>
      </div>

      <CanvasSparkline :buffer="buffer" :color="color" :height="40" :reference="target" />

      <div v-if="value === null" class="text-[10px] text-muted-foreground">Not reported by this station</div>
      <div v-else class="flex items-center justify-between text-[10px] font-mono text-muted-foreground">
        <span v-if="stats && stats.count > 1">
          min {{ fmt(stats.min) }} · avg {{ fmt(stats.avg) }} · max {{ fmt(stats.max) }}
        </span>
        <span v-else>Collecting trend…</span>
        <span
          v-if="deltaToTarget !== null"
          :title="`${targetLabel}: ${fmt(target)} ${unit}`"
          :class="Math.abs(deltaToTarget) > 10 ? 'text-amber-500' : 'text-emerald-500'"
        >
          {{ deltaToTarget > 0 ? '+' : '' }}{{ deltaToTarget.toFixed(0) }}% vs {{ targetLabel.toLowerCase() }}
        </span>
      </div>
    </CardContent>
  </Card>
</template>
