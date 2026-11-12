<script setup lang="ts">
import { computed } from 'vue'
import { STATION_PIN_TONES, describeStationPin, type StationPinState } from '~/utils/stationPinState'
import { PLANT_MAP_PIN_SIZE } from '~/utils/telemetryOverlayPlacement'

const props = defineProps<{
  handle: string
  /** Sampled state (undefined until the first sample). */
  state?: StationPinState | null
  left: number
  top: number
  selected?: boolean
}>()

const emit = defineEmits<{ (e: 'activate'): void }>()

const tone = computed(() => props.state?.tone ?? 'unknown')
const color = computed(() => STATION_PIN_TONES[tone.value].color)
const label = computed(() => describeStationPin(props.handle, props.state))
</script>

<template>
  <!-- Static on purpose: an animated element over the floor-plan SVG repaints the whole drawing every frame. -->
  <button
    type="button"
    class="station-pin absolute pointer-events-auto p-0 border-0 bg-transparent cursor-pointer"
    :class="{ 'station-pin--selected': selected }"
    :style="{ left: `${left}px`, top: `${top}px`, width: `${PLANT_MAP_PIN_SIZE.width}px`, height: `${PLANT_MAP_PIN_SIZE.height}px` }"
    :title="label"
    :aria-label="label"
    :data-pin-handle="handle"
    :data-pin-tone="tone"
    @click.stop="emit('activate')"
  >
    <svg viewBox="0 0 14 18" :width="PLANT_MAP_PIN_SIZE.width" :height="PLANT_MAP_PIN_SIZE.height" aria-hidden="true" class="block overflow-visible">
      <path
        d="M7 17.4C7 17.4 13 11.6 13 7.1A6 6 0 0 0 1 7.1C1 11.6 7 17.4 7 17.4Z"
        :fill="color"
        stroke="#020617"
        stroke-opacity="0.85"
        stroke-width="1.2"
        stroke-linejoin="round"
      />
      <circle cx="7" cy="7" r="2.3" fill="#020617" fill-opacity="0.5" />
    </svg>
  </button>
</template>

<style scoped>
.station-pin {
  transform-origin: 50% 100%;
  transition: transform 120ms ease-out;
}
.station-pin:hover,
.station-pin:focus-visible {
  transform: scale(1.3);
  z-index: 40;
  outline: none;
}
.station-pin--selected {
  transform: scale(1.3);
  z-index: 35;
  filter: drop-shadow(0 0 2px #e2e8f0);
}
/* Critical pins get a static glow so they stand out without animation. */
.station-pin[data-pin-tone='critical']:not(.station-pin--selected) {
  filter: drop-shadow(0 0 2.5px #f43f5e);
}
</style>
