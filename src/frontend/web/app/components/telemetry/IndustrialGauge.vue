<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    value: number
    min?: number
    max?: number
    unit?: string
    label?: string
    warnThreshold?: number
    critThreshold?: number
    size?: number
  }>(),
  {
    min: 0,
    max: 100,
    unit: '',
    label: '',
    size: 110
  }
)

const normalizedValue = computed(() => {
  const clamped = Math.max(props.min, Math.min(props.max, props.value))
  return (clamped - props.min) / (props.max - props.min)
})

const statusColor = computed(() => {
  if (props.critThreshold !== undefined && props.value >= props.critThreshold) {
    return {
      text: 'text-rose-500',
      stroke: '#f43f5e',
      bg: 'bg-rose-500/10'
    }
  }
  if (props.warnThreshold !== undefined && props.value >= props.warnThreshold) {
    return {
      text: 'text-amber-500',
      stroke: '#f59e0b',
      bg: 'bg-amber-500/10'
    }
  }
  return {
    text: 'text-emerald-500',
    stroke: '#10b981',
    bg: 'bg-emerald-500/10'
  }
})

// Radius & Arc Geometry
const radius = 40
const circumference = 2 * Math.PI * radius
// 240 degree gauge arc
const arcLength = circumference * (240 / 360)
const strokeDashoffset = computed(() => {
  return arcLength - (normalizedValue.value * arcLength)
})
</script>

<template>
  <div class="flex flex-col items-center justify-center select-none" :style="{ width: `${size}px` }">
    <div class="relative flex items-center justify-center">
      <svg
        :width="size"
        :height="size"
        viewBox="0 0 100 100"
        class="transform -rotate-210"
      >
        <!-- Background Track -->
        <circle
          cx="50"
          cy="50"
          :r="radius"
          fill="none"
          stroke="currentColor"
          class="text-zinc-800 dark:text-zinc-800"
          stroke-width="8"
          stroke-linecap="round"
          :stroke-dasharray="`${arcLength} ${circumference}`"
        />
        <!-- Active Value Arc -->
        <circle
          cx="50"
          cy="50"
          :r="radius"
          fill="none"
          :stroke="statusColor.stroke"
          stroke-width="8"
          stroke-linecap="round"
          :stroke-dasharray="`${arcLength} ${circumference}`"
          :stroke-dashoffset="strokeDashoffset"
          class="transition-all duration-300 ease-out"
        />
      </svg>

      <!-- Center Value Readout -->
      <div class="absolute inset-0 flex flex-col items-center justify-center pt-2">
        <span class="text-base font-black font-mono tracking-tight tabular-nums" :class="statusColor.text">
          {{ value.toFixed(1) }}
        </span>
        <span class="text-[9px] font-mono uppercase text-muted-foreground -mt-0.5">
          {{ unit }}
        </span>
      </div>
    </div>

    <!-- Label Underneath -->
    <span v-if="label" class="text-[10px] font-medium text-muted-foreground uppercase tracking-wider text-center mt-1 truncate max-w-full">
      {{ label }}
    </span>
  </div>
</template>
