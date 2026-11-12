<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Wrench,
  Gauge,
  Activity,
  ShieldAlert,
  Server,
  Boxes,
  Eye,
  RotateCw,
  RefreshCw,
  Factory
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

/**
 * Hardware schematic of ONE station, built from that station's own component tree
 * (`GET /api/v1/inventory/station-tree/{station}`: bound controllers + installed parts).
 *
 * There is no built-in sample cell: without a station the view asks for one, and a station
 * without registered hardware renders an explicit empty state. Positions are a layout aid only
 * (controllers on the top rail, parts on the process rails); they carry no physical meaning.
 */

export interface PhysicalNode {
  id: string
  code: string
  name: string
  subsystem: 'Controller' | 'Kinematics' | 'Pneumatics' | 'Vision' | 'Safety' | 'Component'
  status: 'normal' | 'warning' | 'critical' | 'idle'
  /** Inventory item id of the installed part (absent for controllers). */
  installedPartId?: string
  /** Controller id for controller nodes. */
  controllerId?: string
  serialNumber?: string | null
  manufacturer?: string | null
  technology?: string | null
  slot?: string | null
  deviceTag?: string | null
  equipmentStatus?: string | null
  description: string
  x: number
  y: number
}

interface StationOption {
  id: string
  label: string
}

const props = defineProps<{
  stationName?: string
  /** Station to show. When omitted the view offers its own station picker. */
  stationId?: string
  activeComponentId?: string
}>()

const emit = defineEmits<{
  'selectComponent': [component: PhysicalNode]
}>()

const stationOptions = ref<StationOption[]>([])
const pickedStationId = ref<string>('')
const tree = ref<any | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
const selectedNodeId = ref<string | null>(null)
let request = 0

const effectiveStationId = computed(() => props.stationId || pickedStationId.value || '')
const needsPicker = computed(() => !props.stationId)

const loadStationOptions = async () => {
  try {
    const machines = await $fetch<any[]>('/api/machines')
    stationOptions.value = (Array.isArray(machines) ? machines : []).map(m => ({
      id: String(m.id),
      label: `${m.displayName || m.name}${m.customIdentifier ? ` (${m.customIdentifier})` : ''}`
    }))
    if (!pickedStationId.value && stationOptions.value.length > 0) {
      pickedStationId.value = stationOptions.value[0].id
    }
  } catch (err: any) {
    loadError.value = err?.data?.message || err?.message || 'Could not load the station list.'
  }
}

const loadTree = async (stationId: string) => {
  const mine = ++request
  tree.value = null
  selectedNodeId.value = null
  loadError.value = null
  if (!stationId) return
  loading.value = true
  try {
    const res = await $fetch<any>(`/api/proxy/v1/inventory/station-tree/${encodeURIComponent(stationId)}`)
    if (mine !== request) return
    tree.value = res ?? null
  } catch (err: any) {
    if (mine !== request) return
    loadError.value = err?.statusCode === 404 || err?.response?.status === 404
      ? 'This station is not registered in the machine directory.'
      : (err?.data?.message || err?.message || 'Could not load the station hardware.')
  } finally {
    if (mine === request) loading.value = false
  }
}

watch(needsPicker, (needed) => { if (needed && stationOptions.value.length === 0) loadStationOptions() }, { immediate: true })
watch(effectiveStationId, (id) => loadTree(id), { immediate: true })

const classify = (part: any): PhysicalNode['subsystem'] => {
  const text = `${part.technology || ''} ${part.name || ''} ${part.displayName || ''} ${part.functionalSlotIdentifier || ''}`.toLowerCase()
  if (/vision|camera|optic|lens|sensor head/.test(text)) return 'Vision'
  if (/safety|light curtain|e-?stop|guard|interlock/.test(text)) return 'Safety'
  if (/pneum|valve|cylinder|vacuum|gripper/.test(text)) return 'Pneumatics'
  if (/servo|drive|motor|axis|robot|spindle|gantry|conveyor/.test(text)) return 'Kinematics'
  return 'Component'
}

/** Maps the backend EquipmentStatus onto the schematic's status colours. */
const equipmentStatus = (status: unknown): PhysicalNode['status'] => {
  switch (String(status ?? '').toLowerCase()) {
    case 'inmachine':
    case 'active':
      return 'normal'
    case 'underrepair':
      return 'warning'
    case 'decommissioned':
    case 'scrapped':
      return 'critical'
    default:
      return 'idle'
  }
}

const isRecentlyOnline = (lastOnline: unknown) => {
  if (typeof lastOnline !== 'string' || !lastOnline) return false
  const at = Date.parse(lastOnline)
  return Number.isFinite(at) && Date.now() - at < 5 * 60 * 1000
}

/** Spreads `count` items evenly on a horizontal rail between 12 % and 88 %. */
const railX = (index: number, count: number) => (count <= 1 ? 50 : 12 + (76 * index) / (count - 1))

const nodes = computed<PhysicalNode[]>(() => {
  const t = tree.value
  if (!t) return []
  const controllers: any[] = Array.isArray(t.controllers) ? t.controllers : []
  const parts: any[] = Array.isArray(t.installedParts) ? t.installedParts : []
  const out: PhysicalNode[] = []

  controllers.forEach((c, i) => {
    out.push({
      id: `ctrl-${c.id}`,
      code: c.hostname || c.name || 'Controller',
      name: c.name || c.hostname || 'Controller',
      subsystem: 'Controller',
      status: isRecentlyOnline(c.lastOnline) ? 'normal' : 'idle',
      controllerId: c.id,
      serialNumber: c.macAddress || null,
      description: [c.ipAddress ? `IP ${c.ipAddress}` : null, c.vlanId != null ? `VLAN ${c.vlanId}` : null,
        isRecentlyOnline(c.lastOnline) ? 'online' : 'no recent heartbeat'].filter(Boolean).join(' · '),
      x: railX(i, controllers.length),
      y: 20
    })
  })

  // Parts fill up to two process rails; at most 5 per rail keeps the labels readable.
  const perRail = Math.max(1, Math.min(5, Math.ceil(parts.length / 2)))
  parts.forEach((p, i) => {
    const rail = Math.floor(i / perRail)
    const inRail = Math.min(perRail, parts.length - rail * perRail)
    out.push({
      id: `part-${p.id}`,
      code: p.functionalSlotIdentifier || p.cad3dNodePath || p.serialNumber || p.name,
      name: p.displayName || p.name,
      subsystem: classify(p),
      status: equipmentStatus(p.equipmentStatus),
      installedPartId: p.id,
      serialNumber: p.serialNumber || null,
      manufacturer: p.manufacturerName || null,
      technology: p.technology || null,
      slot: p.functionalSlotIdentifier || null,
      deviceTag: p.cadReference?.deviceTag || p.cadReference?.DeviceTag || null,
      equipmentStatus: p.equipmentStatus || null,
      description: p.cadReference?.functionText || p.cadReference?.FunctionText || '',
      x: railX(i - rail * perRail, inRail),
      y: Math.min(86, 48 + rail * 24)
    })
  })
  return out
})

const selectedNode = computed(() => nodes.value.find(n => n.id === selectedNodeId.value) ?? null)
const alertCount = computed(() => nodes.value.filter(n => n.status === 'warning' || n.status === 'critical').length)
const title = computed(() => tree.value?.displayName || tree.value?.stationName || props.stationName || '')

// Follow a component selected elsewhere (inventory table / inspector).
watch([() => props.activeComponentId, nodes], ([activeId]) => {
  if (!activeId) return
  const target = String(activeId).toUpperCase()
  const match = nodes.value.find(n =>
    n.installedPartId?.toUpperCase() === target || n.code.toUpperCase() === target || n.serialNumber?.toUpperCase() === target)
  if (match) selectedNodeId.value = match.id
})

const selectNode = (node: PhysicalNode) => {
  selectedNodeId.value = node.id
  emit('selectComponent', node)
}

const iconFor = (subsystem: PhysicalNode['subsystem']) => {
  switch (subsystem) {
    case 'Controller': return Server
    case 'Kinematics': return RotateCw
    case 'Vision': return Eye
    case 'Pneumatics': return Gauge
    case 'Safety': return ShieldAlert
    default: return Wrench
  }
}

const STATUS_STYLE: Record<PhysicalNode['status'], { bg: string; text: string; dot: string; label: string }> = {
  normal: { bg: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/40', text: 'text-emerald-400', dot: 'bg-emerald-400', label: 'In service' },
  warning: { bg: 'bg-amber-500/20 text-amber-400 border-amber-500/40', text: 'text-amber-400', dot: 'bg-amber-400', label: 'Under repair' },
  critical: { bg: 'bg-rose-500/20 text-rose-400 border-rose-500/40', text: 'text-rose-400', dot: 'bg-rose-500', label: 'Out of service' },
  idle: { bg: 'bg-slate-500/20 text-slate-400 border-slate-500/40', text: 'text-slate-400', dot: 'bg-slate-500', label: 'No status' }
}
</script>

<template>
  <div class="p-5 rounded-2xl bg-card border border-border space-y-5 select-none shadow-xs">
    <!-- Top bar: which station, how much hardware -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-border">
      <div class="min-w-0">
        <div class="flex items-center gap-2 flex-wrap">
          <Badge variant="outline" class="text-[10px] font-mono px-2 py-0.5 border-primary/40 text-primary bg-primary/5 uppercase">
            Station Hardware Schematic
          </Badge>
          <span v-if="tree?.customIdentifier || effectiveStationId" class="text-xs font-mono text-muted-foreground truncate">
            {{ tree?.customIdentifier || effectiveStationId }}
          </span>
        </div>
        <h3 class="text-base font-black text-foreground mt-0.5 truncate">
          {{ title || (effectiveStationId ? 'Station' : 'No station selected') }}
        </h3>
      </div>

      <div class="flex items-center gap-3 flex-wrap">
        <select
          v-if="needsPicker"
          v-model="pickedStationId"
          aria-label="Station"
          class="h-8 px-2.5 text-xs bg-background border border-border rounded-lg text-foreground font-mono focus:outline-none focus:border-primary max-w-[260px]"
        >
          <option v-if="stationOptions.length === 0" value="">No stations available</option>
          <option v-for="s in stationOptions" :key="s.id" :value="s.id">{{ s.label }}</option>
        </select>

        <div class="flex items-center gap-1.5 text-xs font-mono">
          <span class="text-muted-foreground">Components:</span>
          <Badge variant="outline" class="text-xs px-2 py-0.5 bg-muted/40 font-bold">{{ nodes.length }}</Badge>
        </div>
        <div v-if="alertCount > 0" class="flex items-center gap-1.5 text-xs font-mono">
          <span class="text-muted-foreground">Attention:</span>
          <Badge variant="outline" class="text-xs px-2 py-0.5 border-amber-500/40 text-amber-500 bg-amber-500/10 font-bold">{{ alertCount }}</Badge>
        </div>
      </div>
    </div>

    <!-- Schematic -->
    <div class="relative w-full aspect-[21/9] min-h-[320px] rounded-2xl bg-slate-950 border border-slate-800 p-4 overflow-hidden flex flex-col justify-end shadow-inner">
      <div class="absolute inset-0 bg-[radial-gradient(#1e293b_1px,transparent_1px)] [background-size:24px_24px] opacity-40 pointer-events-none" />

      <!-- Rails: controllers on top, installed hardware below -->
      <svg class="absolute inset-0 w-full h-full pointer-events-none stroke-slate-800/80" fill="none" stroke-width="1.5" aria-hidden="true">
        <rect x="4%" y="6%" width="92%" height="84%" rx="16" stroke-dasharray="4 4" />
        <line x1="8%" y1="20%" x2="92%" y2="20%" stroke="#0ea5e9" stroke-dasharray="3 3" opacity="0.5" />
        <line x1="8%" y1="48%" x2="92%" y2="48%" stroke-width="2" stroke="#334155" />
      </svg>

      <!-- States -->
      <div v-if="loading" class="absolute inset-0 flex flex-col items-center justify-center gap-2 text-slate-400 z-10">
        <RefreshCw class="w-6 h-6 animate-spin" />
        <span class="text-xs font-mono">Loading station hardware…</span>
      </div>
      <div v-else-if="!effectiveStationId" class="absolute inset-0 flex flex-col items-center justify-center gap-2 text-slate-400 z-10 text-center p-6">
        <Factory class="w-7 h-7" />
        <span class="text-sm font-semibold text-slate-200">Select a station</span>
        <span class="text-xs">The schematic is built from the selected station's controllers and installed parts.</span>
      </div>
      <div v-else-if="loadError" class="absolute inset-0 flex flex-col items-center justify-center gap-3 text-center p-6 z-10">
        <span class="text-sm font-semibold text-rose-400">{{ loadError }}</span>
        <Button variant="outline" size="sm" class="h-7 text-xs cursor-pointer" @click="loadTree(effectiveStationId)">Retry</Button>
      </div>
      <div v-else-if="nodes.length === 0" class="absolute inset-0 flex flex-col items-center justify-center gap-2 text-slate-400 z-10 text-center p-6">
        <Boxes class="w-7 h-7" />
        <span class="text-sm font-semibold text-slate-200">No hardware registered for this station</span>
        <span class="text-xs">Bind a controller or install parts on the station to see them here.</span>
      </div>

      <!-- Component nodes -->
      <template v-else>
        <button
          v-for="node in nodes"
          :key="node.id"
          type="button"
          class="absolute -translate-x-1/2 -translate-y-1/2 cursor-pointer transition-all duration-200 text-left focus:outline-hidden"
          :style="{ left: `${node.x}%`, top: `${node.y}%` }"
          :aria-pressed="selectedNodeId === node.id"
          :aria-label="`${node.subsystem}: ${node.name}`"
          @click="selectNode(node)"
        >
          <div
            class="relative p-2 rounded-xl border backdrop-blur-md transition-all duration-200 shadow-lg flex items-center gap-2"
            :class="selectedNodeId === node.id
              ? 'bg-slate-900 border-primary ring-2 ring-primary/50 scale-105 z-20'
              : 'bg-slate-900/90 border-slate-700 hover:border-slate-500 z-10'"
          >
            <div class="relative w-8 h-8 rounded-lg flex items-center justify-center shrink-0 border" :class="STATUS_STYLE[node.status].bg">
              <component :is="iconFor(node.subsystem)" class="w-4 h-4" />
              <span class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border-2 border-slate-950" :class="STATUS_STYLE[node.status].dot" />
            </div>
            <div class="min-w-[96px] max-w-[140px]">
              <div class="flex items-center justify-between gap-1">
                <span class="font-mono text-[9px] font-bold text-slate-400 uppercase tracking-wider truncate">{{ node.code }}</span>
              </div>
              <div class="text-[11px] font-bold text-slate-100 truncate" :title="node.name">{{ node.name }}</div>
              <div class="text-[9px] font-mono" :class="STATUS_STYLE[node.status].text">{{ node.subsystem }}</div>
            </div>
          </div>
        </button>
      </template>

      <div class="relative z-10 flex items-center justify-between gap-4 text-[11px] font-mono text-slate-400 bg-slate-900/80 px-4 py-2 rounded-xl border border-slate-800/80 flex-wrap">
        <div class="flex items-center gap-4 flex-wrap">
          <span v-for="(style, key) in STATUS_STYLE" :key="key" class="flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full" :class="style.dot" /> {{ style.label }}
          </span>
        </div>
        <span>Select a component to inspect it</span>
      </div>
    </div>

    <!-- Selected component -->
    <div v-if="selectedNode" class="p-4 rounded-xl bg-muted/20 border border-border flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div class="flex items-start gap-3 min-w-0">
        <div class="p-2.5 rounded-xl border shrink-0" :class="STATUS_STYLE[selectedNode.status].bg">
          <Activity class="w-5 h-5" />
        </div>
        <div class="space-y-1 min-w-0">
          <div class="flex items-center gap-2 flex-wrap">
            <span class="text-xs font-mono font-bold uppercase text-primary">{{ selectedNode.code }}</span>
            <Badge variant="outline" class="text-[10px] font-mono uppercase px-1.5 py-0">{{ selectedNode.subsystem }}</Badge>
            <span class="text-xs font-mono" :class="STATUS_STYLE[selectedNode.status].text">
              {{ selectedNode.equipmentStatus || STATUS_STYLE[selectedNode.status].label }}
            </span>
          </div>
          <h4 class="text-sm font-bold text-foreground truncate">{{ selectedNode.name }}</h4>
          <p class="text-xs text-muted-foreground">
            <span v-if="selectedNode.description">{{ selectedNode.description }}</span>
            <span v-if="selectedNode.manufacturer"> · {{ selectedNode.manufacturer }}</span>
            <span v-if="selectedNode.serialNumber"> · S/N {{ selectedNode.serialNumber }}</span>
            <span v-if="selectedNode.deviceTag"> · {{ selectedNode.deviceTag }}</span>
            <span v-if="selectedNode.technology"> · {{ selectedNode.technology }}</span>
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <NuxtLink
          v-if="selectedNode.installedPartId"
          :to="`/dashboard/inventory/${selectedNode.installedPartId}`"
          class="inline-flex items-center h-8 px-3 rounded-md bg-primary text-primary-foreground text-xs font-bold hover:bg-primary/90 gap-1.5"
        >
          <Boxes class="w-3.5 h-3.5" />
          <span>Open in Inventory</span>
        </NuxtLink>
        <NuxtLink
          v-else-if="selectedNode.controllerId"
          :to="{ path: '/dashboard/clients', query: { selected: selectedNode.controllerId, hostname: selectedNode.code } }"
          class="inline-flex items-center h-8 px-3 rounded-md bg-primary text-primary-foreground text-xs font-bold hover:bg-primary/90 gap-1.5"
        >
          <Server class="w-3.5 h-3.5" />
          <span>Open Controller</span>
        </NuxtLink>
      </div>
    </div>
  </div>
</template>
