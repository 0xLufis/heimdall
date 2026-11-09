<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, computed } from 'vue'
import {
  RotateCcw,
  Box,
  Layers,
  Activity,
  Maximize2,
  Loader2,
  AlertCircle,
  Eye,
  CheckCircle2,
  AlertTriangle,
  Cpu
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useCadModelViewer } from '~/composables/useCadModelViewer'

const props = withDefaults(
  defineProps<{
    modelUrl?: string
    selectedSlot?: string | null
    components?: any[]
    height?: string
  }>(),
  {
    modelUrl: '/sample/station_digital_twin.glb',
    selectedSlot: null,
    components: () => [],
    height: '540px'
  }
)

const emit = defineEmits<{
  (e: 'select-slot', slotId: string): void
  (e: 'hover-slot', slotId: string | null): void
}>()

const containerRef = ref<HTMLDivElement | null>(null)
const canvasRef = ref<HTMLCanvasElement | null>(null)

const {
  isLoading,
  loadProgress,
  error,
  isWireframe,
  isStatusOverlayEnabled,
  hoveredNodeName,
  selectedNodeName,
  availableNodeNames,
  initViewer,
  loadModel,
  focusComponent,
  selectComponent,
  hoverComponent,
  toggleWireframe,
  toggleStatusOverlay,
  resetCamera,
  raycastFromEvent,
  dispose
} = useCadModelViewer()

let isMouseDown = false
let dragStartPos = { x: 0, y: 0 }
let didMove = false

onMounted(async () => {
  if (containerRef.value && canvasRef.value) {
    await initViewer(containerRef.value, canvasRef.value, {
      modelUrl: props.modelUrl,
      enableGrid: true
    })

    if (props.selectedSlot) {
      focusComponent(props.selectedSlot)
    }
  }
})

onUnmounted(() => {
  dispose()
})

// Watch modelUrl changes
watch(
  () => props.modelUrl,
  async (newUrl) => {
    if (newUrl && containerRef.value && canvasRef.value) {
      const THREE = await import('three')
      const { GLTFLoader } = await import('three/examples/jsm/loaders/GLTFLoader.js')
      await loadModel(newUrl, THREE, GLTFLoader)
    }
  }
)

// Watch incoming selectedSlot prop
watch(
  () => props.selectedSlot,
  (newSlot) => {
    if (newSlot && newSlot !== selectedNodeName.value) {
      focusComponent(newSlot)
    } else if (!newSlot) {
      selectComponent(null)
    }
  }
)

const activeSlotData = computed(() => {
  const targetName = selectedNodeName.value || hoveredNodeName.value
  if (!targetName) return null

  // Match against passed components
  const comp = props.components.find((c: any) =>
    (c.functionalSlotIdentifier && c.functionalSlotIdentifier.toUpperCase() === targetName.toUpperCase()) ||
    (c.cad3dNodePath && c.cad3dNodePath.toUpperCase() === targetName.toUpperCase()) ||
    (c.name && c.name.toUpperCase().includes(targetName.toUpperCase()))
  )

  return {
    nodeName: targetName,
    componentName: comp?.displayName || comp?.name || targetName,
    serialNumber: comp?.serialNumber,
    technology: comp?.technology || 'Mechanical/Pneumatic',
    equipmentStatus: comp?.equipmentStatus || 'InMachine'
  }
})

const handleMouseDown = (e: MouseEvent) => {
  if (e.button !== 0) return
  isMouseDown = true
  didMove = false
  dragStartPos = { x: e.clientX, y: e.clientY }
}

const handleMouseMove = async (e: MouseEvent) => {
  if (isMouseDown) {
    const dx = Math.abs(e.clientX - dragStartPos.x)
    const dy = Math.abs(e.clientY - dragStartPos.y)
    if (dx > 3 || dy > 3) {
      didMove = true
    }
  } else {
    // Hover raycasting
    const hitNode = await raycastFromEvent(e)
    if (hitNode !== hoveredNodeName.value) {
      hoverComponent(hitNode)
      emit('hover-slot', hitNode)
    }
  }
}

const handleMouseUp = async (e: MouseEvent) => {
  isMouseDown = false
  if (didMove) return // was an orbit drag, not a click

  const hitNode = await raycastFromEvent(e)
  if (hitNode) {
    emit('select-slot', hitNode)
    focusComponent(hitNode)
  }
}

const handleMouseLeave = () => {
  isMouseDown = false
  if (hoveredNodeName.value) {
    hoverComponent(null)
    emit('hover-slot', null)
  }
}
</script>

<template>
  <div
    ref="containerRef"
    class="relative w-full rounded-xl overflow-hidden border border-border/60 bg-gradient-to-b from-slate-950/90 via-slate-900/95 to-slate-950 select-none shadow-2xl"
    :style="{ height: props.height }"
    @mousedown="handleMouseDown"
    @mousemove="handleMouseMove"
    @mouseup="handleMouseUp"
    @mouseleave="handleMouseLeave"
  >
    <!-- WebGL Canvas -->
    <canvas ref="canvasRef" class="w-full h-full block cursor-grab active:cursor-grabbing outline-none" />

    <!-- Top Left Controls & Badges -->
    <div class="absolute top-3 left-3 flex flex-wrap items-center gap-2 z-10 pointer-events-none">
      <Badge variant="outline" class="bg-background/80 backdrop-blur-md text-xs font-mono font-medium border-border/80 text-foreground px-2.5 py-1 shadow-sm">
        <Box class="w-3.5 h-3.5 mr-1.5 text-cyan-400" />
        3D Digital Twin
      </Badge>

      <Badge
        v-if="availableNodeNames.length > 0"
        variant="secondary"
        class="bg-background/70 backdrop-blur-md text-[11px] font-mono text-muted-foreground px-2 py-0.5 border border-border/40"
      >
        {{ availableNodeNames.length }} CAD Nodes
      </Badge>
    </div>

    <!-- Top Right Action Toolbar -->
    <div class="absolute top-3 right-3 flex items-center gap-1.5 z-10 bg-background/80 backdrop-blur-md border border-border/60 rounded-lg p-1 shadow-lg pointer-events-auto">
      <Button
        variant="ghost"
        size="icon-sm"
        class="h-7 w-7 text-muted-foreground hover:text-foreground"
        title="Reset Camera (Isometric View)"
        @click="resetCamera"
      >
        <RotateCcw class="w-3.5 h-3.5" />
      </Button>

      <Button
        variant="ghost"
        size="icon-sm"
        :class="['h-7 w-7', isWireframe ? 'text-primary bg-primary/10' : 'text-muted-foreground hover:text-foreground']"
        title="Toggle Wireframe Mode"
        @click="toggleWireframe"
      >
        <Layers class="w-3.5 h-3.5" />
      </Button>

      <Button
        variant="ghost"
        size="icon-sm"
        :class="['h-7 w-7', isStatusOverlayEnabled ? 'text-emerald-400 bg-emerald-500/10' : 'text-muted-foreground hover:text-foreground']"
        title="Toggle Real-Time Maintenance Health Colors"
        @click="toggleStatusOverlay"
      >
        <Activity class="w-3.5 h-3.5" />
      </Button>
    </div>

    <!-- Active Picked Component Floating Chip -->
    <div
      v-if="activeSlotData"
      class="absolute bottom-3 left-3 z-10 bg-background/90 backdrop-blur-md border border-border/80 rounded-lg p-2.5 shadow-xl max-w-sm pointer-events-auto animate-in fade-in slide-in-from-bottom-2 duration-150"
    >
      <div class="flex items-center justify-between gap-3 mb-1">
        <span class="text-xs font-mono font-semibold text-primary truncate">
          {{ activeSlotData.nodeName }}
        </span>
        <Badge
          :class="[
            'text-[10px] px-1.5 py-0 uppercase tracking-wider',
            activeSlotData.equipmentStatus === 'InMachine' || activeSlotData.equipmentStatus === 'active'
              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
              : 'bg-amber-500/10 text-amber-400 border-amber-500/20'
          ]"
        >
          {{ activeSlotData.equipmentStatus }}
        </Badge>
      </div>

      <div class="text-xs text-foreground font-medium truncate mb-0.5">
        {{ activeSlotData.componentName }}
      </div>

      <div class="flex items-center gap-2 text-[11px] text-muted-foreground font-mono">
        <span v-if="activeSlotData.serialNumber">S/N: {{ activeSlotData.serialNumber }}</span>
        <span>•</span>
        <span>{{ activeSlotData.technology }}</span>
      </div>
    </div>

    <!-- Bottom Right Controls Help Hint -->
    <div class="absolute bottom-3 right-3 text-[10px] text-muted-foreground/60 font-mono pointer-events-none select-none bg-background/40 backdrop-blur-sm px-2 py-1 rounded border border-border/20">
      Left Drag: Orbit • Right Drag: Pan • Scroll: Zoom • Click: Pick
    </div>

    <!-- Loading State Overlay -->
    <div
      v-if="isLoading"
      class="absolute inset-0 flex flex-col items-center justify-center bg-slate-950/85 backdrop-blur-sm z-20"
    >
      <Loader2 class="w-8 h-8 text-primary animate-spin mb-3" />
      <span class="text-xs font-mono text-muted-foreground tracking-wide">
        Loading 3D Digital Twin Mesh... {{ loadProgress }}%
      </span>
    </div>

    <!-- Error State Overlay -->
    <div
      v-if="error"
      class="absolute inset-0 flex flex-col items-center justify-center bg-slate-950/90 z-20 p-6 text-center"
    >
      <AlertCircle class="w-8 h-8 text-destructive mb-2" />
      <span class="text-sm font-semibold text-destructive mb-1">Failed to Load CAD Model</span>
      <span class="text-xs text-muted-foreground max-w-md mb-4">{{ error }}</span>
      <Button variant="outline" size="sm" @click="resetCamera">
        <RotateCcw class="w-3.5 h-3.5 mr-1.5" />
        Retry View
      </Button>
    </div>
  </div>
</template>
