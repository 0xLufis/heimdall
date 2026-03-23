<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
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

</script>

<template>
    <Card class="w-full h-full relative overflow-hidden bg-card border-border shadow-inner group p-0">
        <CardContent class="p-0 w-full h-full">
            <div v-if="loading" class="absolute inset-0 z-20 flex items-center justify-center bg-card">
                <p class="text-muted-foreground font-bold uppercase tracking-widest animate-pulse">Loading Map...</p>
            </div>
            
            <div v-else-if="error" class="absolute inset-0 z-20 flex items-center justify-center bg-card p-6 text-destructive">
                {{ error }}
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
                <g transform="scale(1, -1)">
                    <!-- Global standalone entities -->
                    <template v-for="(entity, idx) in entities" :key="`g-${idx}`">
                        <line v-if="entity.type === 'LINE'" 
                              :x1="entity.vertices[0].x" :y1="entity.vertices[0].y" 
                              :x2="entity.vertices[1].x" :y2="entity.vertices[1].y" 
                              stroke="currentColor" class="text-muted/30" stroke-width="0.5" stroke-linecap="round" />
                              
                        <!-- Render Block Inserts -->
                        <g v-if="entity.type === 'INSERT'" 
                           :transform="`translate(${entity.position.x}, ${entity.position.y}) rotate(${entity.rotation || 0})`"
                           @click.stop="emit('object-clicked', entity.handle, entity.name)"
                           class="cursor-pointer transition-all duration-300 hover:opacity-80 group/insert"
                        >
                            <!-- Invisible bounding box for easier clicking - slightly larger -->
                            <rect x="-20" y="-20" width="40" height="40" fill="transparent" class="group-hover/insert:fill-accent/10 transition-colors" />
                            
                            <template v-if="blocks[entity.name]">
                                <template v-for="(bEnt, bIdx) in blocks[entity.name].entities" :key="`b-${bIdx}`">
                                    <line v-if="bEnt.type === 'LINE'" 
                                          :x1="bEnt.vertices[0].x" :y1="bEnt.vertices[0].y" 
                                          :x2="bEnt.vertices[1].x" :y2="bEnt.vertices[1].y" 
                                          :stroke="isHighlighted(entity.handle) ? 'var(--primary)' : 'var(--muted-foreground)'" 
                                          :stroke-width="isHighlighted(entity.handle) ? 1.5 : 1" 
                                          stroke-linecap="round" />
                                          
                                    <circle v-if="bEnt.type === 'CIRCLE'" 
                                            :cx="bEnt.center.x" :cy="bEnt.center.y" :r="bEnt.radius" 
                                            fill="none" 
                                            :stroke="isHighlighted(entity.handle) ? 'var(--primary)' : 'var(--muted-foreground)'" 
                                            :stroke-width="isHighlighted(entity.handle) ? 1.5 : 1" />
                                            
                                    <text v-if="bEnt.type === 'TEXT'"
                                          :x="bEnt.startPoint.x" :y="-bEnt.startPoint.y"
                                          :fill="isHighlighted(entity.handle) ? 'var(--primary)' : 'var(--foreground)'"
                                          font-size="3"
                                          font-family="monospace"
                                          text-anchor="middle"
                                          dominant-baseline="central"
                                          transform="scale(1, -1)">
                                          {{ bEnt.text }}
                                    </text>
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
