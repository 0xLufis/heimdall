<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useDxfMap } from '~/composables/useDxfMap'
import { ZoomIn, ZoomOut, Maximize2, Loader2, AlertCircle } from 'lucide-vue-next'

const props = withDefaults(
  defineProps<{
    dxfUrl?: string
    highlightedHandles?: string[]
    activePin?: string | null
  }>(),
  {
    dxfUrl: '/sample/assembly_line.dxf',
    highlightedHandles: () => [],
    activePin: null
  }
)

const emit = defineEmits<{
  (e: 'object-clicked', handle: string, blockName: string): void
  (e: 'object-dblclicked', handle: string, blockName: string): void
  (e: 'map-clicked'): void
}>()

const svgContainer = ref<SVGSVGElement | null>(null)
const { isLoading, error, parsedEntities, viewBox, loadAndParseDxf, zoom, pan } = useDxfMap(props.dxfUrl)

let isDragging = false
let startDrag = { x: 0, y: 0 }
let hasMoved = false

const handleMouseDown = (e: MouseEvent) => {
  if (e.button !== 0) return
  isDragging = true
  hasMoved = false
  startDrag = { x: e.clientX, y: e.clientY }
}

const handleMouseMove = (e: MouseEvent) => {
  if (!isDragging) return
  const dx = e.clientX - startDrag.x
  const dy = e.clientY - startDrag.y
  if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
    hasMoved = true
  }
  const scale = viewBox.value.width / (svgContainer.value?.clientWidth || 800)
  pan(-dx * scale, -dy * scale)
  startDrag = { x: e.clientX, y: e.clientY }
}

const handleMouseUp = () => {
  isDragging = false
}

const handleWheel = (e: WheelEvent) => {
  e.preventDefault()
  const factor = e.deltaY > 0 ? 1.1 : 0.9
  zoom(factor)
}

const handleEntityClick = (entity: any, e: MouseEvent) => {
  e.stopPropagation()
  if (hasMoved) return
  const handle = entity.handle || entity.block || entity.name || 'UNKNOWN_HANDLE'
  const name = entity.name || entity.block || entity.text || 'Station Block'
  emit('object-clicked', handle, name)
}

const handleEntityDblClick = (entity: any, e: MouseEvent) => {
  e.stopPropagation()
  const handle = entity.handle || entity.block || entity.name || 'UNKNOWN_HANDLE'
  const name = entity.name || entity.block || entity.text || 'Station Block'
  emit('object-dblclicked', handle, name)
}

const resetView = () => {
  loadAndParseDxf(props.dxfUrl)
}

const isHighlighted = (entity: any) => {
  const h = entity.handle || entity.block || entity.name
  return props.highlightedHandles.includes(h) || props.activePin === h
}

const formatPolylinePoints = (vertices: any[] = []) => {
  return vertices.map(v => `${v.x},${v.y}`).join(' ')
}

const getBlockColor = (blockName: string) => {
  switch (blockName) {
    case 'ROBOT_CELL': return '#a855f7' // Purple
    case 'CNC_MACHINE': return '#3b82f6' // Blue
    case 'ASSEMBLY_STATION': return '#10b981' // Emerald
    case 'PACKAGING_STATION': return '#f59e0b' // Amber
    default: return '#6366f1' // Indigo
  }
}

onMounted(() => {
  loadAndParseDxf(props.dxfUrl)
})

watch(() => props.dxfUrl, (newUrl) => {
  if (newUrl) loadAndParseDxf(newUrl)
})
</script>

<template>
  <div class="relative w-full h-full bg-slate-950 select-none overflow-hidden flex flex-col justify-center items-center">
    <!-- Loading State -->
    <div v-if="isLoading" class="flex flex-col items-center justify-center gap-3 p-12 text-slate-500">
      <Loader2 class="w-8 h-8 animate-spin text-indigo-500" />
      <span class="text-xs font-black uppercase tracking-widest">Rendering Plant CAD Topography...</span>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="flex flex-col items-center justify-center gap-3 p-12 text-rose-400">
      <AlertCircle class="w-8 h-8" />
      <span class="text-xs font-bold">{{ error }}</span>
      <button @click="resetView" class="px-4 py-2 bg-slate-900 border border-slate-800 rounded-xl text-xs font-bold text-slate-300">
        Retry
      </button>
    </div>

    <!-- Interactive SVG Canvas -->
    <svg
      v-else
      ref="svgContainer"
      class="w-full h-full cursor-grab active:cursor-grabbing"
      :viewBox="`${viewBox.x} ${viewBox.y} ${viewBox.width} ${viewBox.height}`"
      @mousedown="handleMouseDown"
      @mousemove="handleMouseMove"
      @mouseup="handleMouseUp"
      @mouseleave="handleMouseUp"
      @wheel="handleWheel"
      @click="emit('map-clicked')"
    >
      <defs>
        <!-- Grid Pattern -->
        <pattern id="cad-grid" width="20" height="20" patternUnits="userSpaceOnUse">
          <path d="M 20 0 L 0 0 0 20" fill="none" stroke="#1e293b" stroke-width="0.3" stroke-dasharray="2,2" />
        </pattern>
        <!-- Tank Fill Pattern -->
        <pattern id="tank-hatch" width="4" height="4" patternUnits="userSpaceOnUse">
          <line x1="0" y1="0" x2="4" y2="4" stroke="#0284c7" stroke-width="0.5" />
        </pattern>
      </defs>

      <!-- Grid Background -->
      <rect :x="viewBox.x" :y="viewBox.y" :width="viewBox.width" :height="viewBox.height" fill="url(#cad-grid)" />

      <g>
        <!-- Layer 1: Factory Walls (LWPOLYLINE) -->
        <template v-for="(entity, idx) in parsedEntities" :key="`wall-${idx}`">
          <polyline
            v-if="(entity.type === 'LWPOLYLINE' || entity.type === 'POLYLINE') && entity.vertices"
            :points="formatPolylinePoints(entity.vertices)"
            stroke="#64748b"
            stroke-width="1.8"
            stroke-linejoin="round"
            stroke-linecap="round"
            fill="none"
          />
        </template>

        <!-- Layer 2: Partition & Conveyor Lines -->
        <template v-for="(entity, idx) in parsedEntities" :key="`line-${idx}`">
          <line
            v-if="entity.type === 'LINE' && entity.vertices && entity.vertices.length >= 2"
            :x1="entity.vertices[0].x"
            :y1="entity.vertices[0].y"
            :x2="entity.vertices[1].x"
            :y2="entity.vertices[1].y"
            :stroke="entity.layer === 'CONVEYORS' ? '#3b82f6' : '#475569'"
            :stroke-width="entity.layer === 'CONVEYORS' ? '1.2' : '0.8'"
            :stroke-dasharray="entity.layer === 'CONVEYORS' ? '4,2' : 'none'"
          />
        </template>

        <!-- Layer 3: Chemical Tanks & Circles (e.g. C-TANK-4) -->
        <template v-for="(entity, idx) in parsedEntities" :key="`circ-${idx}`">
          <g 
            v-if="entity.type === 'CIRCLE' && entity.center"
            class="cursor-pointer group"
            @click="handleEntityClick(entity, $event)"
            @dblclick="handleEntityDblClick(entity, $event)"
          >
            <!-- Highlight Ring -->
            <circle
              v-if="isHighlighted(entity)"
              :cx="entity.center.x"
              :cy="entity.center.y"
              :r="(entity.radius || 10) + 4"
              class="fill-indigo-500/20 stroke-indigo-400 stroke-1 animate-pulse"
            />
            <!-- Tank Body -->
            <circle
              :cx="entity.center.x"
              :cy="entity.center.y"
              :r="entity.radius || 10"
              fill="#082f49"
              :stroke="isHighlighted(entity) ? '#38bdf8' : '#0284c7'"
              stroke-width="1.5"
              class="transition-colors group-hover:stroke-sky-300"
            />
            <!-- Inner Center Dot -->
            <circle
              :cx="entity.center.x"
              :cy="entity.center.y"
              r="2"
              fill="#38bdf8"
            />
            <!-- Tank Handle Label -->
            <text
              v-if="entity.handle"
              :x="entity.center.x"
              :y="entity.center.y + 16"
              fill="#38bdf8"
              font-size="3.5"
              font-family="monospace"
              font-weight="bold"
              text-anchor="middle"
            >
              {{ entity.handle }}
            </text>
          </g>
        </template>

        <!-- Layer 4: Production Hall Zone Labels (TEXT) -->
        <template v-for="(entity, idx) in parsedEntities" :key="`text-${idx}`">
          <text
            v-if="entity.type === 'TEXT' && entity.startPoint"
            :x="entity.startPoint.x"
            :y="entity.startPoint.y"
            fill="#94a3b8"
            font-size="4.5"
            font-family="monospace"
            font-weight="900"
            letter-spacing="0.08em"
            text-anchor="middle"
            class="select-none pointer-events-none"
          >
            {{ entity.text }}
          </text>
        </template>

        <!-- Layer 5: Interactive Station Blocks (INSERT) -->
        <template v-for="(entity, idx) in parsedEntities" :key="`insert-${idx}`">
          <g
            v-if="entity.type === 'INSERT' && entity.position"
            :transform="`translate(${entity.position.x}, ${entity.position.y})`"
            class="cursor-pointer group"
            @click="handleEntityClick(entity, $event)"
            @dblclick="handleEntityDblClick(entity, $event)"
          >
            <!-- Highlight Ring -->
            <rect
              v-if="isHighlighted(entity)"
              x="-12"
              y="-12"
              width="24"
              height="24"
              rx="4"
              class="fill-indigo-500/20 stroke-indigo-400 stroke-1 animate-pulse"
            />

            <!-- Station Block Housing -->
            <rect
              x="-8"
              y="-8"
              width="16"
              height="16"
              rx="3"
              :fill="isHighlighted(entity) ? '#312e81' : '#0f172a'"
              :stroke="isHighlighted(entity) ? '#818cf8' : getBlockColor(entity.name)"
              stroke-width="1.2"
              class="transition-colors group-hover:stroke-white shadow-sm"
            />

            <!-- Block Type Badge / Icon -->
            <rect
              x="-6"
              y="-6"
              width="12"
              height="12"
              rx="2"
              :fill="isHighlighted(entity) ? '#4338ca' : '#1e293b'"
              class="transition-colors"
            />

            <!-- Status Indicator Dot -->
            <circle
              cx="0"
              cy="0"
              r="1.8"
              :fill="isHighlighted(entity) ? '#ffffff' : '#10b981'"
              class="animate-pulse"
            />

            <!-- Station Handle Badge Label -->
            <text
              :x="0"
              :y="13"
              :fill="isHighlighted(entity) ? '#a5b4fc' : '#cbd5e1'"
              font-size="3.2"
              font-family="monospace"
              font-weight="bold"
              text-anchor="middle"
              class="transition-colors group-hover:fill-white font-mono"
            >
              {{ entity.handle || entity.name }}
            </text>
          </g>
        </template>
      </g>
    </svg>

    <!-- Map Zoom / Reset Floating Controls -->
    <div class="absolute bottom-6 right-6 flex flex-col gap-2 z-10">
      <button
        type="button"
        @click="zoom(0.85)"
        class="p-2.5 bg-slate-900/90 hover:bg-slate-800 text-slate-300 rounded-xl border border-slate-800 shadow-xl transition-all"
        title="Zoom In"
      >
        <ZoomIn class="w-4 h-4" />
      </button>

      <button
        type="button"
        @click="zoom(1.15)"
        class="p-2.5 bg-slate-900/90 hover:bg-slate-800 text-slate-300 rounded-xl border border-slate-800 shadow-xl transition-all"
        title="Zoom Out"
      >
        <ZoomOut class="w-4 h-4" />
      </button>

      <button
        type="button"
        @click="resetView"
        class="p-2.5 bg-slate-900/90 hover:bg-slate-800 text-slate-300 rounded-xl border border-slate-800 shadow-xl transition-all"
        title="Reset Bounds"
      >
        <Maximize2 class="w-4 h-4" />
      </button>
    </div>
  </div>
</template>
