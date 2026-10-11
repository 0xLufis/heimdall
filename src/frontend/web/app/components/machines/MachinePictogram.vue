<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    type?: string
    size?: 'sm' | 'md' | 'lg' | 'xl' | number
    class?: string
    colorClass?: string
  }>(),
  {
    type: 'default',
    size: 'md',
    class: '',
    colorClass: 'text-primary'
  }
)

const normalizedType = computed(() => {
  const t = (props.type || '').toLowerCase()
  if (t.includes('cnc') || t.includes('mill') || t.includes('machin')) return 'cnc'
  if (t.includes('lathe') || t.includes('turn')) return 'lathe'
  if (t.includes('robot') || t.includes('arm') || t.includes('manipulat')) return 'robot'
  if (t.includes('conveyor') || t.includes('feed') || t.includes('transport') || t.includes('assembly')) return 'conveyor'
  if (t.includes('press') || t.includes('stamp') || t.includes('hydraulic')) return 'press'
  if (t.includes('mold') || t.includes('inject')) return 'injection'
  if (t.includes('vision') || t.includes('aoi') || t.includes('camera') || t.includes('inspect')) return 'vision'
  if (t.includes('weld') || t.includes('solder') || t.includes('laser')) return 'welding'
  if (t.includes('dispens') || t.includes('glue') || t.includes('seal') || t.includes('gap')) return 'dispenser'
  if (t.includes('test') || t.includes('bench') || t.includes('meter')) return 'tester'
  return 'default'
})

const sizePixels = computed(() => {
  if (typeof props.size === 'number') return props.size
  switch (props.size) {
    case 'sm': return 20
    case 'md': return 32
    case 'lg': return 48
    case 'xl': return 64
    default: return 32
  }
})
</script>

<template>
  <svg
    :width="sizePixels"
    :height="sizePixels"
    viewBox="0 0 64 64"
    fill="none"
    xmlns="http://www.w3.org/2000/svg"
    :class="[colorClass, props.class]"
    class="shrink-0 transition-colors"
  >
    <!-- 1. CNC / MILLING CENTER -->
    <g v-if="normalizedType === 'cnc'">
      <!-- Machine Frame & Safety Enclosure -->
      <rect x="6" y="8" width="52" height="48" rx="4" stroke="currentColor" stroke-width="2.5" fill="currentColor" fill-opacity="0.05" />
      <rect x="12" y="14" width="40" height="26" rx="2" stroke="currentColor" stroke-width="1.8" stroke-dasharray="3 3" />
      <!-- Milling Head & Spindle -->
      <path d="M28 14V24H36V14" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
      <polygon points="30,24 34,24 32,30" fill="currentColor" />
      <!-- Workpiece & Clamping Bed -->
      <rect x="18" y="34" width="28" height="6" rx="1" fill="currentColor" fill-opacity="0.3" stroke="currentColor" stroke-width="1.5" />
      <!-- Base & Chip Pan -->
      <path d="M4 56H60" stroke="currentColor" stroke-width="3" stroke-linecap="round" />
      <circle cx="50" cy="46" r="2" fill="currentColor" />
    </g>

    <!-- 2. INDUSTRIAL LATHE / TURNING -->
    <g v-else-if="normalizedType === 'lathe'">
      <!-- Bed & Slideway -->
      <path d="M6 46H58" stroke="currentColor" stroke-width="3" stroke-linecap="round" />
      <rect x="8" y="20" width="16" height="26" rx="2" stroke="currentColor" stroke-width="2.2" fill="currentColor" fill-opacity="0.1" />
      <!-- Chuck Headstock -->
      <circle cx="24" cy="33" r="7" stroke="currentColor" stroke-width="2" />
      <circle cx="24" cy="33" r="2.5" fill="currentColor" />
      <!-- Workpiece Turning Axis -->
      <line x1="31" y1="33" x2="45" y2="33" stroke="currentColor" stroke-width="3" stroke-linecap="round" />
      <!-- Tailstock -->
      <polygon points="46,26 54,26 54,40 46,40 45,33" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <!-- Cutting Tool Carriage -->
      <rect x="34" y="38" width="8" height="8" rx="1" stroke="currentColor" stroke-width="1.8" fill="currentColor" />
    </g>

    <!-- 3. ARTICULATED ROBOT ARM -->
    <g v-else-if="normalizedType === 'robot'">
      <!-- Solid Heavy Pedestal Base -->
      <path d="M14 54H50" stroke="currentColor" stroke-width="3.5" stroke-linecap="round" />
      <rect x="22" y="46" width="20" height="8" rx="2" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <circle cx="32" cy="46" r="5" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.1" />
      <!-- Joint 1 & Upper Arm Link -->
      <line x1="32" y1="41" x2="24" y2="28" stroke="currentColor" stroke-width="3" stroke-linecap="round" />
      <circle cx="24" cy="28" r="4.5" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <!-- Joint 2 & Forearm Link -->
      <line x1="24" y1="28" x2="42" y2="18" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" />
      <circle cx="42" cy="18" r="3.5" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <!-- Wrist & End-Effector Gripper -->
      <line x1="42" y1="18" x2="48" y2="24" stroke="currentColor" stroke-width="2" />
      <path d="M48 20L53 25M48 28L53 23" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
    </g>

    <!-- 4. CONVEYOR BELT / TRANSFER LINE -->
    <g v-else-if="normalizedType === 'conveyor'">
      <!-- Rollers -->
      <circle cx="14" cy="38" r="6" stroke="currentColor" stroke-width="2.2" />
      <circle cx="14" cy="38" r="2" fill="currentColor" />
      <circle cx="32" cy="38" r="6" stroke="currentColor" stroke-width="2.2" />
      <circle cx="32" cy="38" r="2" fill="currentColor" />
      <circle cx="50" cy="38" r="6" stroke="currentColor" stroke-width="2.2" />
      <circle cx="50" cy="38" r="2" fill="currentColor" />
      <!-- Belt Top & Bottom loop -->
      <line x1="14" y1="32" x2="50" y2="32" stroke="currentColor" stroke-width="2.5" />
      <line x1="14" y1="44" x2="50" y2="44" stroke="currentColor" stroke-width="2.5" />
      <!-- Moving Pallet / Workpiece Box -->
      <rect x="25" y="18" width="14" height="14" rx="2" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <!-- Structural Legs -->
      <line x1="14" y1="44" x2="10" y2="56" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" />
      <line x1="50" y1="44" x2="54" y2="56" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" />
    </g>

    <!-- 5. HYDRAULIC / SERVO PRESS -->
    <g v-else-if="normalizedType === 'press'">
      <!-- C-Frame / H-Frame Structure -->
      <path d="M12 56V12H52V56" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
      <!-- Bed & Bolster Plate -->
      <rect x="8" y="50" width="48" height="6" rx="1" fill="currentColor" fill-opacity="0.4" stroke="currentColor" stroke-width="1.8" />
      <!-- Hydraulic Piston Cylinder -->
      <rect x="27" y="12" width="10" height="14" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <!-- Upper Moving Ram -->
      <rect x="20" y="26" width="24" height="8" rx="1" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.3" />
      <!-- Pressure Indicator Dial -->
      <circle cx="48" cy="22" r="4" stroke="currentColor" stroke-width="1.5" />
      <line x1="48" y1="22" x2="50" y2="20" stroke="currentColor" stroke-width="1.2" />
    </g>

    <!-- 6. INJECTION MOLDING -->
    <g v-else-if="normalizedType === 'injection'">
      <!-- Hopper -->
      <polygon points="12,12 24,12 20,22 16,22" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <!-- Injection Barrel / Screw -->
      <rect x="14" y="22" width="22" height="12" rx="1" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.1" />
      <line x1="18" y1="22" x2="18" y2="34" stroke="currentColor" stroke-width="1.5" stroke-dasharray="2 2" />
      <line x1="26" y1="22" x2="26" y2="34" stroke="currentColor" stroke-width="1.5" stroke-dasharray="2 2" />
      <!-- Nozzle -->
      <polygon points="36,25 42,27 42,29 36,31" fill="currentColor" />
      <!-- Mold Clamping Unit -->
      <rect x="42" y="16" width="14" height="24" rx="2" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <line x1="49" y1="16" x2="49" y2="40" stroke="currentColor" stroke-width="2" stroke-dasharray="3 3" />
      <!-- Frame Base -->
      <path d="M8 48H56" stroke="currentColor" stroke-width="3" stroke-linecap="round" />
    </g>

    <!-- 7. VISION / AOI CHAMBER -->
    <g v-else-if="normalizedType === 'vision'">
      <!-- Dark Inspection Enclosure -->
      <rect x="8" y="10" width="48" height="44" rx="4" stroke="currentColor" stroke-width="2.5" fill="currentColor" fill-opacity="0.05" />
      <!-- Telecentric Camera Body -->
      <rect x="26" y="14" width="12" height="10" rx="1.5" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.3" />
      <rect x="28" y="24" width="8" height="6" stroke="currentColor" stroke-width="1.8" />
      <!-- Ring Light Illuminator -->
      <ellipse cx="32" cy="32" rx="12" ry="4" stroke="currentColor" stroke-width="1.8" stroke-dasharray="2 2" />
      <!-- Vision Cone Beams -->
      <path d="M29 30L20 44M35 30L44 44" stroke="currentColor" stroke-width="1.5" stroke-dasharray="2 2" />
      <!-- Inspect Target PCB -->
      <rect x="22" y="42" width="20" height="4" rx="1" fill="currentColor" />
    </g>

    <!-- 8. LASER WELDING / SOLDERING CELL -->
    <g v-else-if="normalizedType === 'welding'">
      <!-- Enclosure -->
      <rect x="8" y="8" width="48" height="48" rx="4" stroke="currentColor" stroke-width="2.5" />
      <!-- Laser Emitter Head -->
      <polygon points="26,16 38,16 34,28 30,28" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <!-- Laser Arc Beam -->
      <line x1="32" y1="28" x2="32" y2="40" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" />
      <!-- Welding Spark Burst -->
      <path d="M32 40L27 36M32 40L37 36M32 40L28 44M32 40L36 44" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
      <!-- Metal Joint Seam -->
      <rect x="18" y="42" width="28" height="5" rx="1" fill="currentColor" fill-opacity="0.3" stroke="currentColor" stroke-width="1.5" />
    </g>

    <!-- 9. FLUID / GLUE DISPENSER -->
    <g v-else-if="normalizedType === 'dispenser'">
      <!-- Overhead Gantry -->
      <path d="M10 14H54" stroke="currentColor" stroke-width="2.8" stroke-linecap="round" />
      <rect x="24" y="12" width="16" height="6" rx="1" fill="currentColor" />
      <!-- Syringe Barrel & Cartridge -->
      <rect x="28" y="18" width="8" height="16" rx="1" stroke="currentColor" stroke-width="2" fill="currentColor" fill-opacity="0.2" />
      <line x1="32" y1="18" x2="32" y2="30" stroke="currentColor" stroke-width="2" />
      <!-- Dispensing Needle & Droplet -->
      <line x1="32" y1="34" x2="32" y2="42" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
      <circle cx="32" cy="46" r="2" fill="currentColor" />
      <!-- Workpiece Path Trace -->
      <path d="M18 52Q32 48 46 52" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-dasharray="3 3" />
    </g>

    <!-- 10. FUNCTIONAL TEST BENCH -->
    <g v-else-if="normalizedType === 'tester'">
      <!-- Test Chassis -->
      <rect x="10" y="12" width="44" height="40" rx="3" stroke="currentColor" stroke-width="2.5" fill="currentColor" fill-opacity="0.05" />
      <!-- Screen / Oscilloscope Display -->
      <rect x="16" y="18" width="20" height="14" rx="2" stroke="currentColor" stroke-width="1.8" fill="currentColor" fill-opacity="0.2" />
      <path d="M18 25L21 22L24 28L27 24L30 25L33 22" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
      <!-- Controls & Dials -->
      <circle cx="44" cy="22" r="3" stroke="currentColor" stroke-width="1.5" />
      <circle cx="44" cy="30" r="3" stroke="currentColor" stroke-width="1.5" />
      <!-- Test Lead Probes -->
      <path d="M22 40V48M42 40V48" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
    </g>

    <!-- 11. DEFAULT INDUSTRIAL CABINET / MACHINE -->
    <g v-else>
      <rect x="10" y="8" width="44" height="48" rx="4" stroke="currentColor" stroke-width="2.5" fill="currentColor" fill-opacity="0.05" />
      <!-- Status Beacon / Andon Lamp -->
      <rect x="28" y="2" width="8" height="6" rx="1" fill="currentColor" />
      <!-- Front Door Enclosure -->
      <line x1="32" y1="8" x2="32" y2="56" stroke="currentColor" stroke-width="1.8" />
      <!-- Vent Grille -->
      <line x1="16" y1="46" x2="26" y2="46" stroke="currentColor" stroke-width="1.5" />
      <line x1="16" y1="49" x2="26" y2="49" stroke="currentColor" stroke-width="1.5" />
      <!-- HMI Screen -->
      <rect x="36" y="14" width="14" height="12" rx="1" stroke="currentColor" stroke-width="1.5" fill="currentColor" fill-opacity="0.2" />
      <circle cx="40" cy="34" r="2" fill="currentColor" />
      <circle cx="46" cy="34" r="2" fill="currentColor" />
    </g>
  </svg>
</template>
