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
    dxfUrl: '/sample/production_hall.dxf',
    highlightedHandles: () => [],
    activePin: null
  }
)

const emit = defineEmits<{
  (e: 'object-clicked', handle: string, blockName: string): void
  (e: 'object-dblclicked', handle: string, blockName: string): void
  (e: 'object-contextmenu', handle: string, blockName: string, event: MouseEvent): void
  (e: 'map-clicked'): void
  (e: 'map-contextmenu', event: MouseEvent): void
}>()

const svgContainer = ref<SVGSVGElement | null>(null)
const { isLoading, error, parsedEntities, viewBox, loadAndParseDxf, zoom, pan } = useDxfMap(props.dxfUrl)

const hoveredEntity = ref<string | null>(null)

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

const handleEntityContextMenu = (entity: any, e: MouseEvent) => {
  e.stopPropagation()
  const handle = entity.handle || entity.block || entity.name || 'UNKNOWN_HANDLE'
  const name = entity.name || entity.block || entity.text || 'Station Block'
  emit('object-contextmenu', handle, name, e)
}

const handleMapContextMenu = (e: MouseEvent) => {
  emit('map-contextmenu', e)
}

const resetView = () => {
  loadAndParseDxf(props.dxfUrl)
}

const isHighlighted = (entity: any) => {
  const h = entity.handle || entity.block || entity.name
  return props.highlightedHandles.includes(h) || props.activePin === h
}

const isHovered = (entity: any) => {
  const h = entity.handle || entity.block || entity.name
  return hoveredEntity.value === h
}

const shouldShowLabel = (entity: any) => {
  return isHovered(entity) || isHighlighted(entity)
}

const formatPolylinePoints = (vertices: any[] = []) => {
  return vertices.map(v => `${v.x},${v.y}`).join(' ')
}

const getBlockColor = (blockName: string) => {
  const b = (blockName || '').toUpperCase()
  if (b.includes('VISION')) return '#4f7e76' // Muted Teal
  if (b.includes('WELD')) return '#c28532' // Industrial Amber/Gold
  if (b.includes('PACK')) return '#bf682e' // Industrial Terracotta/Bronze
  if (b.includes('SCREW') || b.includes('ROBOT')) return '#6c657a' // Muted Slate/Steel
  if (b.includes('DISPENS') || b.includes('CHEMICAL')) return '#a63d40' // Industrial Oxide Red
  if (b.includes('TEST') || b.includes('INSPECT')) return '#445847' // Anodized Sage
  if (b.includes('CNC') || b.includes('MECH')) return '#57715b' // Muted Machine Green
  return '#5c6773' // Tool Steel default
}

const getBadgeWidth = (text: string) => {
  return Math.max(18, (text || '').length * 3.6 + 8)
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
      <span class="text-xs font-black uppercase tracking-widest">Rendering Plant Layout Topography...</span>
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
      @contextmenu="handleMapContextMenu($event)"
    >
      <defs>
        <!-- CAD Grid Background Pattern -->
        <pattern id="cad-grid-pattern" width="25" height="25" patternUnits="userSpaceOnUse">
          <path d="M 25 0 L 0 0 0 25" fill="none" stroke="#232730" stroke-width="0.4" stroke-dasharray="2,2" />
        </pattern>
      </defs>

      <!-- Background Grid -->
      <rect :x="viewBox.x" :y="viewBox.y" :width="viewBox.width" :height="viewBox.height" fill="url(#cad-grid-pattern)" />

      <g>
        <!-- Layer 1: Floor Zones & Building Outlines (LWPOLYLINE) -->
        <template v-for="(entity, idx) in parsedEntities" :key="`poly-${idx}`">
          <polyline
            v-if="(entity.type === 'LWPOLYLINE' || entity.type === 'POLYLINE') && entity.vertices"
            :points="formatPolylinePoints(entity.vertices)"
            :stroke="entity.layer === 'BUILDING' || entity.layer === 'WALLS' ? '#474d57' : entity.layer === 'WALKWAYS' ? '#313740' : '#232730'"
            :stroke-width="entity.layer === 'BUILDING' || entity.layer === 'WALLS' ? '2.2' : '1.0'"
            :stroke-dasharray="entity.layer === 'WALKWAYS' ? '4,4' : 'none'"
            :fill="entity.layer === 'FLOOR' ? 'rgba(35, 39, 48, 0.3)' : 'none'"
            stroke-linejoin="round"
            stroke-linecap="round"
          />
        </template>

        <!-- Layer 2: Conveyor Lines & Tracks -->
        <template v-for="(entity, idx) in parsedEntities" :key="`line-${idx}`">
          <line
            v-if="entity.type === 'LINE' && entity.vertices && entity.vertices.length >= 2"
            :x1="entity.vertices[0].x"
            :y1="entity.vertices[0].y"
            :x2="entity.vertices[1].x"
            :y2="entity.vertices[1].y"
            :stroke="entity.layer === 'CONVEYORS' ? '#4f7e76' : entity.layer === 'SAFETY' ? '#c28532' : '#313740'"
            :stroke-width="entity.layer === 'CONVEYORS' ? '1.4' : '0.8'"
            :stroke-dasharray="entity.layer === 'SAFETY' ? '3,3' : entity.layer === 'CONVEYORS' ? '6,3' : 'none'"
          />
        </template>

        <!-- Layer 3: Circular Entities & Chemical Vessels -->
        <template v-for="(entity, idx) in parsedEntities" :key="`circ-${idx}`">
          <g 
            v-if="entity.type === 'CIRCLE' && entity.center"
            class="cursor-pointer group"
            @mouseenter="hoveredEntity = entity.handle || 'tank'"
            @mouseleave="hoveredEntity = null"
            @click="handleEntityClick(entity, $event)"
            @dblclick="handleEntityDblClick(entity, $event)"
            @contextmenu="handleEntityContextMenu(entity, $event)"
          >
            <!-- Highlight Ring -->
            <circle
              v-if="isHighlighted(entity)"
              :cx="entity.center.x"
              :cy="entity.center.y"
              :r="(entity.radius || 10) + 6"
              class="fill-teal-500/20 stroke-teal-400 stroke-2 animate-pulse"
            />
            <!-- Vessel Body -->
            <circle
              :cx="entity.center.x"
              :cy="entity.center.y"
              :r="entity.radius || 10"
              fill="#151e1c"
              :stroke="isHighlighted(entity) ? '#6d9a92' : '#3c635c'"
              stroke-width="1.8"
              class="transition-colors group-hover:stroke-teal-300"
            />
            <!-- Center Core -->
            <circle
              :cx="entity.center.x"
              :cy="entity.center.y"
              r="2.5"
              fill="#6d9a92"
            />
            <!-- Handle Label (Only shown on Hover or Search/Highlight) -->
            <g v-if="entity.handle && shouldShowLabel(entity)" class="pointer-events-none">
              <rect
                :x="entity.center.x - getBadgeWidth(entity.handle) / 2"
                :y="entity.center.y + (entity.radius || 10) + 3"
                :width="getBadgeWidth(entity.handle)"
                height="7"
                rx="2"
                fill="#030712"
                :stroke="isHighlighted(entity) ? '#38bdf8' : '#0ea5e9'"
                stroke-width="0.8"
                class="shadow-xl"
              />
              <text
                :x="entity.center.x"
                :y="entity.center.y + (entity.radius || 10) + 7.8"
                fill="#38bdf8"
                font-size="3.8"
                font-family="monospace"
                font-weight="bold"
                text-anchor="middle"
                class="select-none font-mono"
              >
                {{ entity.handle }}
              </text>
            </g>
          </g>
        </template>

        <!-- Layer 4: CAD Text & Zone Headers (with Halo Stroke) -->
        <template v-for="(entity, idx) in parsedEntities" :key="`txt-${idx}`">
          <text
            v-if="entity.type === 'TEXT' && entity.startPoint"
            :x="entity.startPoint.x"
            :y="entity.startPoint.y"
            :transform="entity.rotation ? `rotate(${-entity.rotation}, ${entity.startPoint.x}, ${entity.startPoint.y})` : undefined"
            fill="#94a3b8"
            :font-size="Math.max(3.5, (entity.textHeight || 5) * 0.9)"
            font-family="monospace"
            font-weight="bold"
            letter-spacing="0.06em"
            text-anchor="middle"
            class="select-none pointer-events-none cad-text-halo"
          >
            {{ entity.text }}
          </text>
        </template>

        <!-- Layer 5: Interactive Station Blocks (INSERT) -->
        <template v-for="(entity, idx) in parsedEntities" :key="`ins-${idx}`">
          <g
            v-if="entity.type === 'INSERT' && entity.position"
            :transform="`translate(${entity.position.x}, ${entity.position.y})`"
            class="cursor-pointer group"
            @mouseenter="hoveredEntity = entity.handle || entity.name"
            @mouseleave="hoveredEntity = null"
            @click="handleEntityClick(entity, $event)"
            @dblclick="handleEntityDblClick(entity, $event)"
            @contextmenu="handleEntityContextMenu(entity, $event)"
          >
            <!-- Highlight Glow Ring -->
            <rect
              v-if="isHighlighted(entity)"
              x="-16"
              y="-16"
              width="32"
              height="32"
              rx="6"
              class="fill-indigo-500/20 stroke-indigo-400 stroke-2 animate-pulse"
            />

            <!-- Station Housing Box -->
            <rect
              x="-10"
              y="-10"
              width="20"
              height="20"
              rx="4"
              :fill="isHighlighted(entity) ? '#2a2e34' : '#15181e'"
              :stroke="isHighlighted(entity) ? '#889098' : getBlockColor(entity.name)"
              stroke-width="1.4"
              class="transition-colors group-hover:stroke-white shadow-md"
            />

            <!-- Station Type Icon Core -->
            <rect
              x="-6.5"
              y="-6.5"
              width="13"
              height="13"
              rx="2.5"
              :fill="isHighlighted(entity) ? '#383e44' : '#232730'"
              class="transition-colors"
            />

            <!-- Status Dot -->
            <circle
              cx="0"
              cy="0"
              r="2.2"
              :fill="isHighlighted(entity) ? '#ffffff' : '#10b981'"
              class="animate-pulse"
            />

            <!-- Station Handle Badge (Only rendered on Hover or Search/Highlight) -->
            <g v-if="shouldShowLabel(entity)" class="pointer-events-none">
              <rect
                :x="-getBadgeWidth(entity.handle || entity.name) / 2"
                y="12"
                :width="getBadgeWidth(entity.handle || entity.name)"
                height="7"
                rx="2"
                fill="#030712"
                :stroke="isHighlighted(entity) ? '#818cf8' : '#38bdf8'"
                stroke-width="0.8"
                class="shadow-xl"
              />
              <text
                :x="0"
                y="16.8"
                :fill="isHighlighted(entity) ? '#c7d2fe' : '#e2e8f0'"
                font-size="3.8"
                font-family="monospace"
                font-weight="bold"
                text-anchor="middle"
                class="select-none font-mono"
              >
                {{ entity.handle || entity.name }}
              </text>
            </g>
          </g>
        </template>
      </g>
    </svg>

    <!-- Floating CAD Controls -->
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

<style scoped>
.cad-text-halo {
  paint-order: stroke fill;
  stroke: #030712;
  stroke-width: 1.8px;
  stroke-linecap: round;
  stroke-linejoin: round;
}
</style>
