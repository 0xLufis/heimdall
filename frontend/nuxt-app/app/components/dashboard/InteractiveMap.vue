<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import DxfParser from 'dxf-parser'
import { Card, CardContent } from '~/components/ui/card'

const props = defineProps<{
  dxfUrl: string
  highlightedHandles?: string[]
  activePin?: string | null
}>()

const emit = defineEmits<{
  (e: 'object-clicked', handle: string, name: string): void
}>()

const loading = ref(true)
const dxfData = ref<any>(null)
const error = ref<string | null>(null)
const svgContainer = ref<SVGSVGElement | null>(null)

// For simple pan/zoom state
const viewBox = ref({ x: 0, y: -50, width: 200, height: 100 })
let isDragging = false
let startDrag = { x: 0, y: 0 }

const loadDxf = async () => {
  try {
    loading.value = true
    error.value = null
    const response = await fetch(props.dxfUrl)
    if (!response.ok) throw new Error('Failed to fetch DXF')
    const text = await response.text()
    const parser = new DxfParser()
    dxfData.value = parser.parseSync(text)
    
    if (dxfData.value) {
      // Calculate bounding box to set initial viewBox
      let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
      dxfData.value.entities.forEach((entity: any) => {
        if (entity.type === 'INSERT') {
          minX = Math.min(minX, entity.position.x - 20)
          maxX = Math.max(maxX, entity.position.x + 20)
          minY = Math.min(minY, entity.position.y - 20)
          maxY = Math.max(maxY, entity.position.y + 20)
        } else if (entity.type === 'LINE') {
          minX = Math.min(minX, entity.vertices[0].x, entity.vertices[1].x)
          maxX = Math.max(maxX, entity.vertices[0].x, entity.vertices[1].x)
          minY = Math.min(minY, entity.vertices[0].y, entity.vertices[1].y)
          maxY = Math.max(maxY, entity.vertices[0].y, entity.vertices[1].y)
        }
      })
      
      if (minX !== Infinity) {
        viewBox.value = {
          x: minX - 10,
          y: minY - 10,
          width: (maxX - minX) + 20,
          height: (maxY - minY) + 20
        }
      }
    }
  } catch (e: any) {
    console.error(e)
    error.value = "Failed to load DXF: " + e.message
  } finally {
    loading.value = false
  }
}

const handleWheel = (e: WheelEvent) => {
  e.preventDefault()
  const zoomFactor = 1.1
  const scale = e.deltaY > 0 ? zoomFactor : 1 / zoomFactor
  
  // Keep center fixed during zoom
  const newWidth = viewBox.value.width * scale
  const newHeight = viewBox.value.height * scale
  const dx = (viewBox.value.width - newWidth) / 2
  const dy = (viewBox.value.height - newHeight) / 2
  
  viewBox.value.x += dx
  viewBox.value.y += dy
  viewBox.value.width = newWidth
  viewBox.value.height = newHeight
}

const handleMouseDown = (e: MouseEvent) => {
  isDragging = true
  startDrag = { x: e.clientX, y: e.clientY }
}

const handleMouseMove = (e: MouseEvent) => {
  if (!isDragging) return
  
  const dx = e.clientX - startDrag.x
  const dy = e.clientY - startDrag.y
  
  // Convert screen pixels to SVG units
  const svgRect = svgContainer.value?.getBoundingClientRect()
  if (svgRect) {
    const scaleX = viewBox.value.width / svgRect.width
    const scaleY = viewBox.value.height / svgRect.height
    viewBox.value.x -= dx * scaleX // If dx is positive (mouse right), x must decrease to show more right area
    viewBox.value.y -= dy * scaleY // dy is positive (mouse down), y must decrease to move map down (SVG units)
  }
  
  startDrag = { x: e.clientX, y: e.clientY }
}

const handleMouseUp = () => {
  isDragging = false
}

onMounted(() => {
  if (props.dxfUrl) loadDxf()
  window.addEventListener('mouseup', handleMouseUp)
})

onUnmounted(() => {
  window.removeEventListener('mouseup', handleMouseUp)
})

watch(() => props.dxfUrl, () => {
  if (props.dxfUrl) loadDxf()
})

const entities = computed(() => dxfData.value?.entities || [])
const blocks = computed(() => dxfData.value?.blocks || {})

const isHighlighted = (handle: string) => {
    if (props.highlightedHandles && props.highlightedHandles.includes(handle)) return true
    if (props.activePin === handle) return true
    return false
}

// "Real-like" assets mapping - using high-quality SVG paths for industrial components
const blockAssets = {
  'CNC_MACHINE': {
    color: 'emerald',
    path: 'M -8 -6 h 16 v 12 h -16 z M -6 -4 h 12 v 5 h -12 z M -6 2 h 2 v 2 h -2 z M -2 2 h 2 v 2 h -2 z M 2 2 h 2 v 2 h -2 z',
    detail: 'M -4 -2 h 8 M -4 0 h 8'
  },
  'ROBOT_CELL': {
    color: 'blue',
    path: 'M -10 -10 h 20 v 20 h -20 z M -3 -3 a 3 3 0 1 0 6 0 a 3 3 0 1 0 -6 0 M 0 0 l 6 6 l 2 -2',
    detail: 'M -10 -10 l 20 20 M -10 10 l 20 -20'
  },
  'ASSEMBLY_STATION': {
    color: 'orange',
    path: 'M -7 -5 h 14 v 10 h -14 z M -5 -3 h 10 v 6 h -10 z',
    detail: 'M -2 -1 l 4 0 M 0 -3 l 0 6'
  },
  'PACKAGING_STATION': {
    color: 'purple',
    path: 'M -8 -8 h 16 v 16 h -16 z M -4 -4 h 8 v 8 h -8 z',
    detail: 'M -8 0 h 16 M 0 -8 v 16'
  }
}

const getBlockAsset = (name: string) => {
  return (blockAssets as any)[name] || null
}

</script>

<template>
    <Card class="w-full h-full relative overflow-hidden bg-slate-950 border-slate-800 shadow-2xl group p-0">
        <CardContent class="p-0 w-full h-full">
            <div v-if="loading" class="absolute inset-0 z-20 flex items-center justify-center bg-slate-950/80 backdrop-blur-sm">
                <div class="flex flex-col items-center gap-4">
                    <div class="w-12 h-12 border-4 border-slate-800 border-t-emerald-500 rounded-full animate-spin"></div>
                    <p class="text-slate-400 text-[10px] font-black uppercase tracking-widest animate-pulse">Synchronizing Plant Layout...</p>
                </div>
            </div>
            
            <div v-else-if="error" class="absolute inset-0 z-20 flex items-center justify-center bg-slate-950 p-6 text-red-400">
                <div class="text-center">
                    <p class="text-xs font-black uppercase tracking-widest mb-2">Protocol Error</p>
                    <p class="text-[10px] opacity-70">{{ error }}</p>
                </div>
            </div>

            <svg 
                v-else
                ref="svgContainer"
                class="w-full h-full cursor-grab active:cursor-grabbing outline-none"
                :viewBox="`${viewBox.x} ${viewBox.y} ${viewBox.width} ${viewBox.height}`"
                @wheel="handleWheel"
                @mousedown="handleMouseDown"
                @mousemove="handleMouseMove"
                @mouseleave="handleMouseUp"
                xmlns="http://www.w3.org/2000/svg"
            >
                <!-- Background Grid -->
                <defs>
                    <pattern id="grid" width="20" height="20" patternUnits="userSpaceOnUse">
                        <path d="M 20 0 L 0 0 0 20" fill="none" stroke="rgba(255,255,255,0.03)" stroke-width="0.5"/>
                    </pattern>
                    <filter id="glow">
                        <feGaussianBlur stdDeviation="1.5" result="coloredBlur"/>
                        <feMerge>
                            <feMergeNode in="coloredBlur"/>
                            <feMergeNode in="SourceGraphic"/>
                        </feMerge>
                    </filter>
                </defs>
                <rect :x="viewBox.x" :y="viewBox.y" :width="viewBox.width" :height="viewBox.height" fill="url(#grid)" />

                <g transform="scale(1, -1)">
                    <!-- Global standalone entities -->
                    <template v-for="(entity, idx) in entities" :key="`g-${idx}`">
                        <line v-if="entity.type === 'LINE'" 
                              :x1="entity.vertices[0].x" :y1="entity.vertices[0].y" 
                              :x2="entity.vertices[1].x" :y2="entity.vertices[1].y" 
                              stroke="rgba(100, 116, 139, 0.4)" stroke-width="0.8" stroke-linecap="round" />
                        
                        <text v-if="entity.type === 'TEXT'"
                              :x="entity.startPoint.x" :y="-entity.startPoint.y"
                              fill="rgba(148, 163, 184, 0.6)"
                              font-size="4"
                              font-weight="900"
                              font-family="system-ui"
                              text-anchor="middle"
                              dominant-baseline="central"
                              transform="scale(1, -1)"
                              class="uppercase tracking-tighter pointer-events-none">
                              {{ entity.text }}
                        </text>
                              
                        <!-- Render Block Inserts -->
                        <g v-if="entity.type === 'INSERT'" 
                           :transform="`translate(${entity.position.x}, ${entity.position.y}) rotate(${entity.rotation || 0})`"
                           @click.stop="emit('object-clicked', entity.handle, entity.name)"
                           class="cursor-pointer transition-all duration-300 group/insert"
                        >
                            <!-- Selection Glow -->
                            <rect v-if="isHighlighted(entity.handle)" 
                                  x="-15" y="-15" width="30" height="30" 
                                  fill="rgba(16, 185, 129, 0.1)" 
                                  stroke="rgba(16, 185, 129, 0.5)" 
                                  stroke-width="1"
                                  stroke-dasharray="2,2"
                                  class="animate-[pulse_2s_infinite]" />

                            <!-- Invisible bounding box for easier clicking -->
                            <rect x="-20" y="-20" width="40" height="40" fill="transparent" />
                            
                            <!-- Real-like Asset Rendering -->
                            <template v-if="getBlockAsset(entity.name)">
                                <path :d="getBlockAsset(entity.name).path" 
                                      :fill="isHighlighted(entity.handle) ? 'rgba(16, 185, 129, 0.2)' : 'rgba(30, 41, 59, 0.7)'"
                                      :stroke="isHighlighted(entity.handle) ? '#10b981' : '#64748b'"
                                      stroke-width="1.5"
                                      stroke-linejoin="round"
                                      class="transition-colors duration-300 group-hover/insert:stroke-slate-300"
                                />
                                <path :d="getBlockAsset(entity.name).detail" 
                                      fill="none"
                                      :stroke="isHighlighted(entity.handle) ? '#34d399' : '#475569'"
                                      stroke-width="0.8"
                                      class="opacity-60"
                                />
                                <!-- Identifier Label -->
                                <text :y="14" 
                                      fill="white" 
                                      font-size="3" 
                                      font-weight="bold" 
                                      text-anchor="middle" 
                                      transform="scale(1, -1)"
                                      class="opacity-0 group-hover/insert:opacity-100 transition-opacity uppercase tracking-widest font-mono">
                                      {{ entity.name.replace('_', ' ') }}
                                </text>
                            </template>

                            <!-- Fallback DXF Rendering -->
                            <template v-else-if="blocks[entity.name]">
                                <template v-for="(bEnt, bIdx) in blocks[entity.name].entities" :key="`b-${bIdx}`">
                                    <line v-if="bEnt.type === 'LINE'" 
                                          :x1="bEnt.vertices[0].x" :y1="bEnt.vertices[0].y" 
                                          :x2="bEnt.vertices[1].x" :y2="bEnt.vertices[1].y" 
                                          :stroke="isHighlighted(entity.handle) ? '#10b981' : '#475569'" 
                                          :stroke-width="isHighlighted(entity.handle) ? 1.5 : 1" 
                                          stroke-linecap="round" />
                                          
                                    <circle v-if="bEnt.type === 'CIRCLE'" 
                                            :cx="bEnt.center.x" :cy="bEnt.center.y" :r="bEnt.radius" 
                                            fill="none" 
                                            :stroke="isHighlighted(entity.handle) ? '#10b981' : '#475569'" 
                                            :stroke-width="isHighlighted(entity.handle) ? 1.5 : 1" />
                                </template>
                            </template>
                        </g>
                    </template>
                </g>
            </svg>
        </CardContent>
    </Card>
</template>

<style scoped>
svg {
    user-select: none;
    -webkit-user-select: none;
}
</style>
