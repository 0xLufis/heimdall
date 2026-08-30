<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
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
  // Convert screen delta to SVG viewBox delta
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
  const handle = entity.handle || entity.block || 'UNKNOWN_HANDLE'
  const name = entity.name || entity.block || 'Block'
  emit('object-clicked', handle, name)
}

const handleEntityDblClick = (entity: any, e: MouseEvent) => {
  e.stopPropagation()
  const handle = entity.handle || entity.block || 'UNKNOWN_HANDLE'
  const name = entity.name || entity.block || 'Block'
  emit('object-dblclicked', handle, name)
}

const resetView = () => {
  loadAndParseDxf(props.dxfUrl)
}

const isHighlighted = (entity: any) => {
  const h = entity.handle || entity.block
  return props.highlightedHandles.includes(h) || props.activePin === h
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
      <span class="text-xs font-black uppercase tracking-widest">Rendering CAD Topography...</span>
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
      <g>
        <template v-for="(entity, idx) in parsedEntities" :key="idx">
          <!-- Lines -->
          <line
            v-if="entity.type === 'LINE' && entity.vertices"
            :x1="entity.vertices[0].x"
            :y1="entity.vertices[0].y"
            :x2="entity.vertices[1].x"
            :y2="entity.vertices[1].y"
            stroke="#334155"
            stroke-width="1.2"
          />

          <!-- Circles -->
          <circle
            v-else-if="entity.type === 'CIRCLE' && entity.center"
            :cx="entity.center.x"
            :cy="entity.center.y"
            :r="entity.radius || 8"
            fill="#1e293b"
            stroke="#475569"
            stroke-width="1"
          />

          <!-- Inserted Machine Blocks / Anchors -->
          <g
            v-else-if="entity.type === 'INSERT' && entity.position"
            :transform="`translate(${entity.position.x}, ${entity.position.y})`"
            class="cursor-pointer group"
            @click="handleEntityClick(entity, $event)"
            @dblclick="handleEntityDblClick(entity, $event)"
          >
            <!-- Highlight Ring -->
            <rect
              v-if="isHighlighted(entity)"
              x="-18"
              y="-18"
              width="36"
              height="36"
              rx="8"
              class="fill-indigo-500/20 stroke-indigo-400 stroke-2 animate-pulse"
            />

            <!-- Machine Block Representation -->
            <rect
              x="-14"
              y="-14"
              width="28"
              height="28"
              rx="6"
              :class="isHighlighted(entity) ? 'fill-indigo-600 stroke-indigo-300' : 'fill-slate-800 hover:fill-slate-700 stroke-slate-600'"
              stroke-width="1.5"
              class="transition-colors"
            />

            <!-- Machine Icon Symbol -->
            <circle
              cx="0"
              cy="0"
              r="4"
              :fill="isHighlighted(entity) ? '#ffffff' : '#94a3b8'"
            />
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
