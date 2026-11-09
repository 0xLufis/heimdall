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
  Cpu,
  Radio,
  Zap,
  Gauge
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useCadModelViewer } from '~/composables/useCadModelViewer'
import { useStationTelemetry } from '~/composables/useStationTelemetry'

const props = withDefaults(
  defineProps<{
    modelUrl?: string
    selectedSlot?: string | null
    components?: any[]
    height?: string
    stationId?: string
  }>(),
  {
    modelUrl: '/sample/station_digital_twin.glb',
    selectedSlot: null,
    components: () => [],
    height: '540px',
    stationId: 'L01-OP010'
  }
)

const emit = defineEmits<{
  (e: 'select-slot', slotId: string): void
  (e: 'hover-slot', slotId: string | null): void
}>()

const containerRef = ref<HTMLDivElement | null>(null)
const canvasRef = ref<HTMLCanvasElement | null>(null)
const show3dOverlays = ref(true)
const expandedNode = ref<string | null>(null)

const { getStationOscillation } = useStationTelemetry()

const getNodeTelemetry = (nodeName: string) => {
  const osc = getStationOscillation(props.stationId || 'L01-OP010')
  let hash = 0
  for (let i = 0; i < nodeName.length; i++) {
    hash = (hash << 5) - hash + nodeName.charCodeAt(i)
    hash |= 0
  }
  hash = Math.abs(hash)
  const vibOffset = ((hash % 7) - 3) * 0.08
  const vib = Math.max(0.4, Number((osc.vibrationMmS + vibOffset).toFixed(2)))
  const temp = Math.round(osc.temperatureC + ((hash % 5) - 2) * 2)
  const isHealthy = vib < 2.5
  return { vib, temp, isHealthy, osc }
}

const {
  isLoading,
  loadProgress,
  error,
  isWireframe,
  isStatusOverlayEnabled,
  hoveredNodeName,
  selectedNodeName,
  availableNodeNames,
  projectedNodes,
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

      <Button
        variant="ghost"
        size="icon-sm"
        :class="['h-7 w-7', show3dOverlays ? 'text-cyan-400 bg-cyan-500/10' : 'text-muted-foreground hover:text-foreground']"
        title="Toggle 3D HUD Telemetry Overlays"
        @click="show3dOverlays = !show3dOverlays"
      >
        <Radio class="w-3.5 h-3.5" :class="show3dOverlays ? 'animate-pulse text-cyan-400' : ''" />
      </Button>
    </div>

    <!-- 3D HUD Telemetry Overlays Layer -->
    <div
      v-if="show3dOverlays && !isLoading && !error"
      class="absolute inset-0 pointer-events-none overflow-hidden z-15"
    >
      <template v-for="(proj, nodeName) in projectedNodes" :key="`hud-3d-${nodeName}`">
        <div
          v-if="proj.isVisible"
          class="absolute pointer-events-auto transition-transform duration-75 select-none"
          :style="{
            left: `${proj.x}px`,
            top: `${proj.y}px`,
            transform: 'translate(-50%, -100%)',
            zIndex: selectedNodeName === nodeName ? 40 : 20
          }"
        >
          <!-- Compact 3D Pill Badge -->
          <div
            v-if="expandedNode !== nodeName"
            @click.stop="focusComponent(String(nodeName)); emit('select-slot', String(nodeName)); expandedNode = String(nodeName)"
            class="group flex items-center gap-1.5 px-2 py-1 bg-slate-950/90 hover:bg-slate-900 border rounded-full shadow-xl backdrop-blur-md cursor-pointer transition-all duration-150 transform hover:scale-105"
            :class="[
              selectedNodeName === nodeName
                ? 'border-cyan-400 ring-2 ring-cyan-400/50 bg-slate-900'
                : hoveredNodeName === nodeName
                ? 'border-amber-400 ring-1 ring-amber-400/40'
                : 'border-slate-700/80 hover:border-cyan-500/60'
            ]"
            :title="`Slot ${nodeName}: Click to focus in 3D & view telemetry`"
          >
            <span
              class="w-1.5 h-1.5 rounded-full shrink-0"
              :class="getNodeTelemetry(String(nodeName)).isHealthy ? 'bg-emerald-400 animate-pulse' : 'bg-amber-400 animate-ping'"
            />
            <span class="text-[10px] font-mono font-bold text-slate-200 group-hover:text-cyan-300 transition-colors">
              {{ nodeName }}
            </span>
            <span
              class="text-[9px] font-mono px-1 py-0.2 rounded font-semibold border"
              :class="getNodeTelemetry(String(nodeName)).isHealthy ? 'bg-cyan-500/10 text-cyan-400 border-cyan-500/30' : 'bg-amber-500/15 text-amber-400 border-amber-500/30'"
            >
              {{ getNodeTelemetry(String(nodeName)).vib }} mm/s
            </span>
          </div>

          <!-- Expanded 3D HUD Telemetry Card -->
          <div
            v-else
            @click.stop
            class="w-60 bg-slate-950/95 border border-cyan-500/60 shadow-2xl rounded-xl p-2.5 backdrop-blur-xl text-slate-100 flex flex-col gap-2 animate-in fade-in zoom-in-95 duration-150"
          >
            <div class="flex items-center justify-between pb-1.5 border-b border-slate-800">
              <div class="flex items-center gap-1.5 truncate">
                <Cpu class="size-3.5 text-cyan-400 shrink-0" />
                <span class="text-xs font-mono font-bold text-white truncate">{{ nodeName }}</span>
              </div>
              <button
                type="button"
                @click.stop="expandedNode = null"
                class="p-0.5 text-slate-400 hover:text-white rounded hover:bg-slate-800 transition-colors"
                title="Collapse"
              >
                <RotateCcw class="size-3" />
              </button>
            </div>

            <!-- Mini Telemetry Stat Grid -->
            <div class="grid grid-cols-2 gap-1.5 text-center">
              <div class="bg-slate-900/80 p-1 rounded border border-slate-800 text-[10px] font-mono">
                <span class="text-[8px] uppercase text-slate-400 block font-sans">Vibration</span>
                <span class="font-bold text-cyan-300">{{ getNodeTelemetry(String(nodeName)).vib }} mm/s</span>
              </div>
              <div class="bg-slate-900/80 p-1 rounded border border-slate-800 text-[10px] font-mono">
                <span class="text-[8px] uppercase text-slate-400 block font-sans">Temperature</span>
                <span class="font-bold text-amber-300">{{ getNodeTelemetry(String(nodeName)).temp }} °C</span>
              </div>
            </div>

            <div class="flex items-center justify-between gap-1 pt-0.5">
              <button
                type="button"
                @click.stop="focusComponent(String(nodeName))"
                class="flex-1 py-1 px-2 rounded bg-cyan-600/30 hover:bg-cyan-600/50 border border-cyan-500/40 text-[9px] font-semibold text-cyan-200 transition-colors text-center"
              >
                Focus 3D View
              </button>
              <button
                type="button"
                @click.stop="expandedNode = null"
                class="py-1 px-2 rounded bg-slate-900 hover:bg-slate-800 border border-slate-800 text-[9px] text-slate-300"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      </template>
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
