<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Cpu,
  Layers,
  Wrench,
  Gauge,
  Thermometer,
  Zap,
  Activity,
  AlertTriangle,
  CheckCircle2,
  ShieldAlert,
  Radio,
  Server,
  ArrowRight,
  Boxes,
  Eye,
  RotateCw
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

interface PhysicalNode {
  id: string
  code: string
  name: string
  subsystem: 'Controller' | 'Kinematics' | 'Pneumatics' | 'Vision' | 'Safety' | 'Tooling'
  status: 'normal' | 'warning' | 'critical' | 'idle'
  telemetryMetric?: string
  telemetryValue?: string
  telemetryUnit?: string
  installedPartId?: string
  spareCount: number
  wearPercentage: number
  description: string
  x: number // SVG layout coordinate percentage
  y: number
}

const props = defineProps<{
  stationName?: string
  stationId?: string
  activeComponentId?: string
}>()

const emit = defineEmits<{
  'selectComponent': [component: PhysicalNode]
}>()

// Cell Subsystem Nodes
const nodes = ref<PhysicalNode[]>([
  {
    id: 'node-ctrl-1',
    code: 'IPC-1001',
    name: 'Beckhoff CX5140 Modular Controller',
    subsystem: 'Controller',
    status: 'normal',
    telemetryMetric: 'CPU Jitter',
    telemetryValue: '12',
    telemetryUnit: 'µs',
    installedPartId: 'prt-test-1',
    spareCount: 2,
    wearPercentage: 15,
    description: 'EtherCAT Master soft-PLC controlling real-time deterministic cell cycles',
    x: 18,
    y: 22
  },
  {
    id: 'node-spindle-2',
    code: 'BOSCH-REX-SPN',
    name: 'Bosch Rexroth Synchronous Spindle Axis',
    subsystem: 'Kinematics',
    status: 'normal',
    telemetryMetric: 'Spindle Speed',
    telemetryValue: '12,450',
    telemetryUnit: 'RPM',
    installedPartId: 'prt-spindle-01',
    spareCount: 1,
    wearPercentage: 42,
    description: 'High-precision drive axis for drilling and fastening operations',
    x: 52,
    y: 25
  },
  {
    id: 'node-pneum-3',
    code: 'FST-VLV-M5',
    name: 'Festo 5-Port Solenoid Valve Manifold',
    subsystem: 'Pneumatics',
    status: 'normal',
    telemetryMetric: 'Line Pressure',
    telemetryValue: '6.2',
    telemetryUnit: 'bar',
    installedPartId: 'prt-bulk-1',
    spareCount: 10,
    wearPercentage: 28,
    description: 'Air supply actuator manifold driving clamping nests and ejectors',
    x: 32,
    y: 65
  },
  {
    id: 'node-vision-4',
    code: 'KEYENCE-LR-TB',
    name: 'Keyence Optical Vision Inspection Head',
    subsystem: 'Vision',
    status: 'warning',
    telemetryMetric: 'Lens Drift',
    telemetryValue: '0.04',
    telemetryUnit: 'mm',
    installedPartId: 'prt-vision-01',
    spareCount: 0,
    wearPercentage: 78,
    description: 'High-speed CMOS camera validating part seated tolerance and dimension',
    x: 75,
    y: 35
  },
  {
    id: 'node-tool-5',
    code: 'SANDVIK-CARBIDE-D12',
    name: 'Sandvik Coromant Carbide Milling Cutter',
    subsystem: 'Tooling',
    status: 'normal',
    telemetryMetric: 'Wear Rating',
    telemetryValue: '310',
    telemetryUnit: 'cycles',
    installedPartId: 'prt-cutter-01',
    spareCount: 4,
    wearPercentage: 55,
    description: 'Multi-flute cutting bit loaded in automatic tool changer slot 1',
    x: 62,
    y: 68
  },
  {
    id: 'node-safety-6',
    code: 'SICK-OPT-CURTAIN',
    name: 'Sick DeTec4 Safety Optical Light Curtain',
    subsystem: 'Safety',
    status: 'normal',
    telemetryMetric: 'OSSD Channel',
    telemetryValue: 'Closed (Safe)',
    telemetryUnit: '',
    installedPartId: 'prt-curtain-01',
    spareCount: 1,
    wearPercentage: 8,
    description: 'Type 4 safety barrier guarding operator hand placement during cell cycle',
    x: 88,
    y: 72
  }
])

const selectedNode = ref<PhysicalNode>(nodes.value[1]) // Default select Spindle

const selectNode = (node: PhysicalNode) => {
  selectedNode.value = node
  emit('selectComponent', node)
}

const getStatusColor = (status: PhysicalNode['status']) => {
  switch (status) {
    case 'normal':
      return {
        fill: 'fill-emerald-500',
        stroke: 'stroke-emerald-400',
        bg: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/40',
        glow: 'shadow-emerald-500/30'
      }
    case 'warning':
      return {
        fill: 'fill-amber-500',
        stroke: 'stroke-amber-400',
        bg: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
        glow: 'shadow-amber-500/30'
      }
    case 'critical':
      return {
        fill: 'fill-rose-500',
        stroke: 'stroke-rose-400',
        bg: 'bg-rose-500/20 text-rose-400 border-rose-500/40',
        glow: 'shadow-rose-500/30'
      }
    default:
      return {
        fill: 'fill-slate-500',
        stroke: 'stroke-slate-400',
        bg: 'bg-slate-500/20 text-slate-400 border-slate-500/40',
        glow: 'shadow-slate-500/30'
      }
  }
}
</script>

<template>
  <div class="p-5 rounded-2xl bg-card border border-border space-y-5 select-none shadow-xs">
    <!-- Top Bar: Station Cell Metadata & Status Overview -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-border">
      <div>
        <div class="flex items-center gap-2">
          <Badge variant="outline" class="text-[10px] font-mono px-2 py-0.5 border-primary/40 text-primary bg-primary/5 uppercase">
            Station Hardware Schematic
          </Badge>
          <span class="text-xs font-mono text-muted-foreground">ID: {{ stationId || 'STN-OP10-PREASSY' }}</span>
        </div>
        <h3 class="text-base font-black text-foreground mt-0.5 flex items-center gap-2">
          <span>{{ stationName || 'OP-10 Robotic Fastening & Assembly Cell' }}</span>
          <span class="inline-flex items-center gap-1 text-[11px] font-mono font-medium text-emerald-400">
            <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
            Live State Synchronized
          </span>
        </h3>
      </div>

      <div class="flex items-center gap-3">
        <!-- Subsystem Count Pills -->
        <div class="flex items-center gap-1.5 text-xs font-mono">
          <span class="text-muted-foreground">Components:</span>
          <Badge variant="outline" class="text-xs px-2 py-0.5 bg-muted/40 font-bold">
            {{ nodes.length }} Active
          </Badge>
        </div>

        <div class="flex items-center gap-1.5 text-xs font-mono">
          <span class="text-muted-foreground">Alerts:</span>
          <Badge variant="outline" class="text-xs px-2 py-0.5 border-amber-500/40 text-amber-400 bg-amber-500/10 font-bold">
            {{ nodes.filter(n => n.status === 'warning' || n.status === 'critical').length }} Warning
          </Badge>
        </div>
      </div>
    </div>

    <!-- MAIN INTERACTIVE CELL SCHEMATIC (SVG / Canvas Hybrid) -->
    <div class="relative w-full aspect-[21/9] min-h-[320px] rounded-2xl bg-slate-950 border border-slate-800 p-4 overflow-hidden flex flex-col justify-between shadow-inner">
      <!-- Grid Backplane -->
      <div class="absolute inset-0 bg-[radial-gradient(#1e293b_1px,transparent_1px)] [background-size:24px_24px] opacity-40 pointer-events-none" />

      <!-- Physical Cell Chassis Silhouette Overlay -->
      <svg class="absolute inset-0 w-full h-full pointer-events-none stroke-slate-800/80" fill="none" stroke-width="1.5">
        <!-- Station Frame Perimeter -->
        <rect x="5%" y="10%" width="90%" height="80%" rx="16" stroke-dasharray="4 4" />
        <!-- Infeed Conveyor Line -->
        <line x1="5%" y1="50%" x2="30%" y2="50%" stroke-width="2" />
        <!-- Processing Axis Guideway -->
        <line x1="30%" y1="50%" x2="70%" y2="50%" stroke-width="3" stroke="#334155" />
        <!-- Outfeed Transfer Line -->
        <line x1="70%" y1="50%" x2="95%" y2="50%" stroke-width="2" />
        <!-- Signal Link from Controller to Spindle -->
        <path d="M 220 120 C 320 120, 380 140, 520 150" stroke="#0ea5e9" stroke-width="1" stroke-dasharray="3 3" opacity="0.6" />
      </svg>

      <!-- Component Interactive Nodes Placed in Physical Space -->
      <div
        v-for="node in nodes"
        :key="node.id"
        @click="selectNode(node)"
        class="absolute -translate-x-1/2 -translate-y-1/2 cursor-pointer transition-all duration-200 group"
        :style="{ left: `${node.x}%`, top: `${node.y}%` }"
      >
        <!-- Node Container Box -->
        <div
          class="relative p-2.5 rounded-xl border backdrop-blur-md transition-all duration-200 shadow-lg flex items-center gap-2.5"
          :class="[
            selectedNode?.id === node.id
              ? 'bg-slate-900 border-primary ring-2 ring-primary/50 scale-105 shadow-primary/20 z-20'
              : 'bg-slate-900/90 border-slate-700 hover:border-slate-500 hover:scale-102 z-10'
          ]"
        >
          <!-- Status Ring Pulse -->
          <div class="relative w-8 h-8 rounded-lg flex items-center justify-center shrink-0 border" :class="getStatusColor(node.status).bg">
            <component :is="node.subsystem === 'Controller' ? Server : node.subsystem === 'Kinematics' ? RotateCw : node.subsystem === 'Vision' ? Eye : node.subsystem === 'Pneumatics' ? Gauge : node.subsystem === 'Safety' ? ShieldAlert : Wrench" class="w-4 h-4" />
            <span
              v-if="node.status !== 'idle'"
              class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border-2 border-slate-950"
              :class="node.status === 'normal' ? 'bg-emerald-400' : node.status === 'warning' ? 'bg-amber-400 animate-pulse' : 'bg-rose-500 animate-ping'"
            />
          </div>

          <!-- Label & Telemetry Metric -->
          <div class="space-y-0.5 min-w-[120px]">
            <div class="flex items-center justify-between gap-1">
              <span class="font-mono text-[9px] font-bold text-slate-400 uppercase tracking-wider truncate max-w-[90px]">
                {{ node.code }}
              </span>
              <span class="text-[9px] font-mono font-bold" :class="getStatusColor(node.status).bg.split(' ')[1]">
                {{ node.subsystem }}
              </span>
            </div>

            <div class="text-xs font-bold text-slate-100 truncate max-w-[130px]" :title="node.name">
              {{ node.name }}
            </div>

            <!-- Embedded Live Telemetry Value -->
            <div v-if="node.telemetryMetric" class="flex items-baseline justify-between pt-0.5 text-[10px] font-mono border-t border-slate-800">
              <span class="text-slate-500">{{ node.telemetryMetric }}:</span>
              <span class="font-bold text-cyan-400">{{ node.telemetryValue }} <span class="text-[9px] font-normal text-slate-400">{{ node.telemetryUnit }}</span></span>
            </div>
          </div>
        </div>
      </div>

      <!-- Footer Info Bar inside Schematic -->
      <div class="relative z-10 flex items-center justify-between text-[11px] font-mono text-slate-400 bg-slate-900/80 px-4 py-2 rounded-xl border border-slate-800/80">
        <div class="flex items-center gap-4">
          <span class="flex items-center gap-1.5"><span class="w-2 h-2 rounded-full bg-emerald-400" /> In-Spec Operation</span>
          <span class="flex items-center gap-1.5"><span class="w-2 h-2 rounded-full bg-amber-400" /> Tolerance Warning</span>
          <span class="flex items-center gap-1.5"><span class="w-2 h-2 rounded-full bg-rose-500" /> Safety Halt</span>
        </div>
        <div>
          <span>Tip: Tap any machine node to inspect linked spare inventory & wear rating</span>
        </div>
      </div>
    </div>

    <!-- Contextual Node Detail Footer Banner (When a node is tapped) -->
    <div v-if="selectedNode" class="p-4 rounded-xl bg-muted/20 border border-border flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div class="flex items-start gap-3">
        <div class="p-2.5 rounded-xl border shrink-0" :class="getStatusColor(selectedNode.status).bg">
          <Activity class="w-5 h-5" />
        </div>
        <div class="space-y-1">
          <div class="flex items-center gap-2">
            <span class="text-xs font-mono font-bold uppercase text-primary">{{ selectedNode.code }}</span>
            <Badge variant="outline" class="text-[10px] font-mono uppercase px-1.5 py-0">
              {{ selectedNode.subsystem }} Module
            </Badge>
            <span class="text-xs font-mono" :class="selectedNode.spareCount > 0 ? 'text-emerald-400' : 'text-rose-400 font-bold'">
              {{ selectedNode.spareCount }} Replacement Spares in Crib
            </span>
          </div>
          <h4 class="text-sm font-bold text-foreground">{{ selectedNode.name }}</h4>
          <p class="text-xs text-muted-foreground">{{ selectedNode.description }}</p>
        </div>
      </div>

      <!-- Quick Actions for selected hardware node -->
      <div class="flex items-center gap-2 shrink-0">
        <div class="text-right pr-2 border-r border-border font-mono text-xs">
          <span class="text-[10px] text-muted-foreground block uppercase">Lifecycle Wear</span>
          <span class="font-bold" :class="selectedNode.wearPercentage > 70 ? 'text-rose-400' : 'text-foreground'">
            {{ selectedNode.wearPercentage }}% Consumed
          </span>
        </div>

        <Button
          size="sm"
          @click="emit('selectComponent', selectedNode)"
          class="h-8 text-xs gap-1.5 bg-primary text-primary-foreground font-bold cursor-pointer shadow-xs"
        >
          <Boxes class="w-3.5 h-3.5" />
          <span>Inspect in Registry</span>
        </Button>
      </div>
    </div>
  </div>
</template>
