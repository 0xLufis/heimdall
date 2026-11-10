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
import { useCadModelViewer, DEFAULT_CAD_MODEL_URL, type CadNodeStatus } from '~/composables/useCadModelViewer'
import { useStationTelemetry } from '~/composables/useStationTelemetry'
import { computeOverlayPlacement } from '~/utils/telemetryOverlayPlacement'

const props = withDefaults(
  defineProps<{
    modelUrl?: string
    selectedSlot?: string | null
    components?: any[]
    height?: string
    /** Station whose live telemetry is shown in the HUD; without it no telemetry is displayed. */
    stationId?: string | null
  }>(),
  {
    modelUrl: DEFAULT_CAD_MODEL_URL,
    selectedSlot: null,
    components: () => [],
    height: '540px',
    stationId: null
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

/** Vibration RMS above which the station is flagged (ISO 10816 class II "unsatisfactory" boundary). */
const VIBRATION_ALERT_MM_S = 2.8

/**
 * Live dynamics for the station hosting this model. Telemetry is reported per station, not per
 * CAD node, so the HUD labels it as station-level; null when nothing has been received.
 */
const stationOscillation = computed(() => (props.stationId ? getStationOscillation(props.stationId) : null))

const findComponentForNode = (nodeName: string) => {
  const target = nodeName.toUpperCase()
  return props.components.find((c: any) =>
    c?.functionalSlotIdentifier?.toUpperCase() === target ||
    c?.cad3dNodePath?.toUpperCase() === target
  ) ?? null
}

/** Maps backend EquipmentStatus (App.Contracts.Enums.EquipmentStatus) onto a CAD tint. */
const equipmentStatusToNodeStatus = (status: unknown): CadNodeStatus | null => {
  switch (String(status ?? '').toLowerCase()) {
    case 'inmachine':
    case 'active':
      return 'healthy'
    case 'underrepair':
      return 'warning'
    case 'instorage':
    case 'decommissioned':
    case 'scrapped':
      return 'fault'
    default:
      return null
  }
}

const nodeStatusMap = computed(() => {
  const map: Record<string, CadNodeStatus> = {}
  for (const c of props.components || []) {
    const status = equipmentStatusToNodeStatus(c?.equipmentStatus)
    if (!status) continue
    for (const key of [c?.cad3dNodePath, c?.functionalSlotIdentifier]) {
      if (typeof key === 'string' && key) map[key.toUpperCase()] = status
    }
  }
  return map
})

const getNodeStatus = (nodeName: string): CadNodeStatus | null => nodeStatusMap.value[nodeName.toUpperCase()] ?? null

const getClamped3dPlacement = (proj: { x: number; y: number }, nodeName: string) => {
  const isExp = expandedNode.value === nodeName
  const placement = computeOverlayPlacement({
    anchorX: proj.x,
    anchorY: proj.y,
    containerWidth: containerRef.value?.clientWidth || 800,
    containerHeight: containerRef.value?.clientHeight || 540,
    size: isExp ? { width: 240, height: 170 } : { width: 180, height: 28 },
    insets: { top: 48, right: 12, bottom: 12, left: 12 },
    gapAbove: 8,
    gapBelow: 12
  })

  return {
    visible: placement.visible,
    style: {
      left: `${placement.left}px`,
      top: `${placement.top}px`,
      width: `${placement.width}px`,
      maxHeight: `${placement.height}px`,
      zIndex: selectedNodeName.value === nodeName ? 40 : 20
    }
  }
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
  setNodeStatuses,
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
    setNodeStatuses(nodeStatusMap.value)
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

watch(nodeStatusMap, (statuses) => setNodeStatuses(statuses))

const reloadModel = async (url: string) => {
  if (!url || !containerRef.value || !canvasRef.value) return
  try {
    const THREE = await import('three')
    const { GLTFLoader } = await import('three/examples/jsm/loaders/GLTFLoader.js')
    await loadModel(url, THREE, GLTFLoader)
  } catch {
    // loadModel already surfaced the failure through `error`
  }
}

// Watch modelUrl changes
watch(() => props.modelUrl, (newUrl) => reloadModel(newUrl))

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
    (c.name && c.name.toUpperCase() === targetName.toUpperCase())
  )

  return {
    nodeName: targetName,
    componentName: comp?.displayName || comp?.name || targetName,
    serialNumber: comp?.serialNumber,
    technology: comp?.technology || null,
    equipmentStatus: comp?.equipmentStatus || 'Unbound'
  }
})

const handleMouseDown = (e: MouseEvent) => {
  if (e.button !== 0) return
  isMouseDown = true
  didMove = false
  dragStartPos = { x: e.clientX, y: e.clientY }
}

const handleMouseMove = (e: MouseEvent) => {
  if (isMouseDown) {
    const dx = Math.abs(e.clientX - dragStartPos.x)
    const dy = Math.abs(e.clientY - dragStartPos.y)
    if (dx > 3 || dy > 3) {
      didMove = true
    }
  } else {
    // Hover raycasting
    const hitNode = raycastFromEvent(e)
    if (hitNode !== hoveredNodeName.value) {
      hoverComponent(hitNode)
      emit('hover-slot', hitNode)
    }
  }
}

const handleMouseUp = (e: MouseEvent) => {
  isMouseDown = false
  if (didMove) return // was an orbit drag, not a click

  const hitNode = raycastFromEvent(e)
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
          v-if="proj.isVisible && getClamped3dPlacement(proj, String(nodeName)).visible"
          class="absolute pointer-events-none flex justify-center transition-transform duration-75 select-none"
          :style="getClamped3dPlacement(proj, String(nodeName)).style"
        >
          <!-- Compact 3D Pill Badge -->
          <div
            v-if="expandedNode !== nodeName"
            @mousedown.stop
            @mouseup.stop
            @click.stop="focusComponent(String(nodeName)); emit('select-slot', String(nodeName)); expandedNode = String(nodeName)"
            class="hud-3d-pill pointer-events-auto max-w-full overflow-hidden group flex items-center gap-1.5 px-2 py-1 bg-slate-950/90 hover:bg-slate-900 border rounded-full shadow-xl backdrop-blur-md cursor-pointer transition-all duration-150 transform hover:scale-105"
            :class="[
              selectedNodeName === nodeName
                ? 'border-cyan-400 ring-2 ring-cyan-400/50 bg-slate-900'
                : hoveredNodeName === nodeName
                ? 'border-amber-400 ring-1 ring-amber-400/40'
                : 'border-slate-700/80 hover:border-cyan-500/60'
            ]"
            :title="`Slot ${nodeName}: Click to focus in 3D & view details`"
          >
            <span
              class="w-1.5 h-1.5 rounded-full shrink-0"
              :class="{
                'bg-emerald-400': getNodeStatus(String(nodeName)) === 'healthy',
                'bg-amber-400': getNodeStatus(String(nodeName)) === 'warning',
                'bg-rose-500': getNodeStatus(String(nodeName)) === 'fault',
                'bg-slate-500': !getNodeStatus(String(nodeName))
              }"
            />
            <span class="text-[10px] font-mono font-bold text-slate-200 group-hover:text-cyan-300 transition-colors truncate">
              {{ nodeName }}
            </span>
          </div>

          <!-- Expanded 3D HUD Card -->
          <div
            v-else
            @mousedown.stop
            @mouseup.stop
            @click.stop
            class="hud-3d-card pointer-events-auto w-full max-h-full overflow-y-auto bg-slate-950/95 border border-cyan-500/60 shadow-2xl rounded-xl p-2.5 backdrop-blur-xl text-slate-100 flex flex-col gap-2 animate-in fade-in zoom-in-95 duration-150"
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

            <div class="text-[10px] text-slate-300 truncate">
              {{ findComponentForNode(String(nodeName))?.displayName || findComponentForNode(String(nodeName))?.name || 'No component bound to this slot' }}
            </div>

            <!-- Station-level live dynamics (telemetry is reported per station, not per CAD node) -->
            <div v-if="stationOscillation" class="grid grid-cols-2 gap-1.5 text-center">
              <div class="bg-slate-900/80 p-1 rounded border border-slate-800 text-[10px] font-mono">
                <span class="text-[8px] uppercase text-slate-400 block font-sans">Station vibration</span>
                <span
                  class="font-bold"
                  :class="stationOscillation.vibrationMmS > VIBRATION_ALERT_MM_S ? 'text-amber-300' : 'text-cyan-300'"
                >{{ stationOscillation.vibrationMmS.toFixed(2) }} mm/s</span>
              </div>
              <div class="bg-slate-900/80 p-1 rounded border border-slate-800 text-[10px] font-mono">
                <span class="text-[8px] uppercase text-slate-400 block font-sans">Station temp.</span>
                <span class="font-bold text-amber-300">{{ stationOscillation.temperatureC !== null ? stationOscillation.temperatureC.toFixed(1) : '—' }} °C</span>
              </div>
            </div>
            <div v-else class="hud-3d-no-telemetry text-[10px] text-slate-400">
              No live telemetry received for this station.
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
        <span v-if="activeSlotData.serialNumber && activeSlotData.technology">•</span>
        <span v-if="activeSlotData.technology">{{ activeSlotData.technology }}</span>
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
      <Button variant="outline" size="sm" @click="reloadModel(props.modelUrl)">
        <RotateCcw class="w-3.5 h-3.5 mr-1.5" />
        Retry Load
      </Button>
    </div>
  </div>
</template>
