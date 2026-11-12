<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { IndustrialController } from '~/types/domain'
import { 
  Monitor, Cpu, Activity, HardDrive, Network, ShieldCheck, 
  ShieldAlert, Pin, PinOff, Search, Layers, Server, 
  Terminal, CheckCircle2, AlertTriangle, Info, Copy, Check,
  MapPin, Link, Eye
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'

const props = defineProps<{
  controller: IndustrialController
}>()

const emit = defineEmits<{
  (e: 'link-dxf', controller: IndustrialController): void
  (e: 'locate-map', handle: string): void
  (e: 'unpin-dxf', controller: IndustrialController): void
  (e: 'quick-view', controller: IndustrialController): void
}>()

type PropertyCategory = 'Identity & Network' | 'Resources' | 'PLC & Runtime' | 'Reported System Info' | 'Installed Components' | 'Stations & Ownership'

interface ControllerProperty {
  id: string
  name: string
  category: PropertyCategory
  value: string
  status?: 'success' | 'warning' | 'error' | 'info' | 'neutral'
  badge?: string
  description?: string
}

// Local storage key for persistent user pinning
const PINNED_STORAGE_KEY = 'heimdall_pinned_controller_props'

const defaultPinnedIds = ['status', 'cpu_load', 'ram_load', 'disk_free']
const pinnedPropertyIds = ref<string[]>([])
const selectedCategory = ref<string>('All')
const propertySearchQuery = ref<string>('')
const copiedPropertyId = ref<string | null>(null)

onMounted(() => {
  try {
    const saved = localStorage.getItem(PINNED_STORAGE_KEY)
    if (saved) {
      pinnedPropertyIds.value = JSON.parse(saved)
    } else {
      pinnedPropertyIds.value = [...defaultPinnedIds]
    }
  } catch {
    pinnedPropertyIds.value = [...defaultPinnedIds]
  }
})

const togglePin = (propId: string) => {
  if (pinnedPropertyIds.value.includes(propId)) {
    pinnedPropertyIds.value = pinnedPropertyIds.value.filter(id => id !== propId)
  } else {
    pinnedPropertyIds.value.push(propId)
  }
  try {
    localStorage.setItem(PINNED_STORAGE_KEY, JSON.stringify(pinnedPropertyIds.value))
  } catch (e) {
    console.error('Failed to save pinned properties:', e)
  }
}

const isPinned = (propId: string) => pinnedPropertyIds.value.includes(propId)

const copyValue = async (propId: string, val: string) => {
  try {
    await navigator.clipboard.writeText(val)
    copiedPropertyId.value = propId
    setTimeout(() => {
      if (copiedPropertyId.value === propId) copiedPropertyId.value = null
    }, 1800)
  } catch (e) {
    console.error('Failed to copy property value:', e)
  }
}

const humanize = (key: string) =>
  key.replace(/[_-]+/g, ' ').replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/\s+/g, ' ').trim()

const stringify = (value: unknown): string => {
  if (value === null || value === undefined) return ''
  if (typeof value === 'string') return value
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  try { return JSON.stringify(value) } catch { return String(value) }
}

const relativeTime = (iso: string | null | undefined) => {
  if (!iso) return 'never'
  const ms = Date.now() - Date.parse(iso)
  if (!Number.isFinite(ms)) return 'unknown'
  if (ms < 60_000) return 'just now'
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)} min ago`
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)} h ago`
  return `${Math.floor(ms / 86_400_000)} d ago`
}

/** `system_metadata` keys that are rendered in a dedicated section (or are internal links). */
const STRUCTURED_METADATA_KEYS = new Set(['AdsPlcMemory', 'RemoteAccess', 'BeckhoffRT', 'IPAddress', 'ipAddress'])

/**
 * Properties of THIS controller, taken only from what the backend holds for it: its record,
 * the agent's last report (`systemMetadata`, resource averages, disk info), its registered
 * components and its station bindings. Anything that was not reported is simply absent.
 */
const allProperties = computed<ControllerProperty[]>(() => {
  const c = props.controller
  const t = c.telemetry
  const meta = (c.systemMetadata && typeof c.systemMetadata === 'object' ? c.systemMetadata : {}) as Record<string, any>
  const list: ControllerProperty[] = []
  const add = (prop: ControllerProperty) => { if (prop.value !== '') list.push(prop) }

  // 1. Identity & network
  add({
    id: 'status',
    name: 'Connection Status',
    category: 'Identity & Network',
    value: t?.isOnline ? `Online · last report ${relativeTime(c.lastOnline)}` : `Offline · last report ${relativeTime(c.lastOnline)}`,
    status: t?.isOnline ? 'success' : 'warning',
    badge: t?.isOnline ? 'ONLINE' : 'OFFLINE',
    description: 'A controller counts as online when its agent reported within the last 5 minutes'
  })
  add({ id: 'hostname', name: 'Hostname', category: 'Identity & Network', value: c.hostname || '', status: 'neutral' })
  add({ id: 'ip_address', name: 'IP Address', category: 'Identity & Network', value: c.ipAddress || '', status: 'neutral' })
  add({ id: 'mac_address', name: 'MAC Address', category: 'Identity & Network', value: c.macAddress || '', status: 'neutral' })
  add({ id: 'machine_identifier', name: 'Machine Identifier', category: 'Identity & Network', value: c.machineIdentifier || '', status: 'neutral', description: 'Identity the edge agent reports under' })
  add({ id: 'ad_ou', name: 'Directory OU', category: 'Identity & Network', value: c.adOuPath || '', status: 'neutral' })
  add({ id: 'cad_pin', name: 'Floor-plan Tag', category: 'Identity & Network', value: c.pinnedObjectHandle || '', status: 'info' })
  const vnc = meta.RemoteAccess?.Vnc || meta.RemoteAccess?.vnc
  if (vnc && typeof vnc === 'object') {
    add({
      id: 'remote_vnc',
      name: 'Remote Desktop Endpoint',
      category: 'Identity & Network',
      value: `${vnc.Host || vnc.host || '(web UI host)'}:${vnc.Port ?? vnc.port}/${vnc.Path || vnc.path || 'websockify'}`,
      status: 'info',
      badge: 'VNC',
      description: 'noVNC endpoint linked to this controller'
    })
  }

  // 2. Resources (agent-reported averages)
  if (typeof t?.cpuUsagePercent === 'number') {
    add({
      id: 'cpu_load',
      name: 'CPU Load',
      category: 'Resources',
      value: `${t.cpuUsagePercent.toFixed(1)} %`,
      status: t.cpuUsagePercent > 85 ? 'error' : t.cpuUsagePercent > 65 ? 'warning' : 'success',
      badge: `${Math.round(t.cpuUsagePercent)}%`,
      description: 'Average CPU utilisation from the last agent report'
    })
  }
  if (typeof t?.ramUsagePercent === 'number') {
    add({
      id: 'ram_load',
      name: 'Memory Usage',
      category: 'Resources',
      value: `${t.ramUsagePercent.toFixed(1)} %`,
      status: t.ramUsagePercent > 90 ? 'error' : t.ramUsagePercent > 75 ? 'warning' : 'success',
      badge: `${Math.round(t.ramUsagePercent)}%`,
      description: 'Average memory utilisation from the last agent report'
    })
  }
  const disk = c.freeDiskSpace as any
  const totalFree = disk?.totalFreeGB ?? disk?.TotalFreeGB
  if (typeof totalFree === 'number') {
    const osFree = disk?.osDriveFreeGB ?? disk?.OsDriveFreeGB
    add({
      id: 'disk_free',
      name: 'Free Disk Space',
      category: 'Resources',
      value: `${totalFree.toFixed(1)} GB free${typeof osFree === 'number' ? ` · OS drive ${osFree.toFixed(1)} GB` : ''}`,
      status: typeof osFree === 'number' && osFree < 10 ? 'warning' : 'success',
      badge: `${Math.round(totalFree)} GB`
    })
    const drives = disk?.drives ?? disk?.Drives
    if (drives && typeof drives === 'object') {
      for (const [drive, free] of Object.entries(drives)) {
        if (typeof free !== 'number') continue
        add({ id: `disk_${drive}`, name: `Drive ${drive}`, category: 'Resources', value: `${free.toFixed(1)} GB free`, status: free < 10 ? 'warning' : 'neutral' })
      }
    }
  }

  // 3. PLC & runtime (only what an agent or PLC block reported)
  const rt = (t?.beckhoffRT || meta.BeckhoffRT) as Record<string, any> | undefined
  if (rt && typeof rt === 'object') {
    for (const [key, value] of Object.entries(rt)) {
      add({ id: `rt_${key}`, name: humanize(key), category: 'PLC & Runtime', value: stringify(value), status: 'info' })
    }
  }
  const plc = meta.AdsPlcMemory as Record<string, any> | undefined
  if (plc && typeof plc === 'object') {
    add({ id: 'plc_ams', name: 'AMS Net ID', category: 'PLC & Runtime', value: plc.AmsNetId ? `${plc.AmsNetId}${plc.AmsPort ? ` (port ${plc.AmsPort})` : ''}` : '', status: 'neutral' })
    add({ id: 'plc_symbol', name: 'Last PLC Symbol', category: 'PLC & Runtime', value: stringify(plc.SymbolName), status: 'info', description: stringify(plc.PlcTypeName) || undefined })
    add({ id: 'plc_quality', name: 'PLC Data Quality', category: 'PLC & Runtime', value: stringify(plc.Quality), status: /good/i.test(stringify(plc.Quality)) ? 'success' : 'warning' })
    add({ id: 'plc_updated', name: 'Last PLC Block', category: 'PLC & Runtime', value: plc.LastUpdated ? relativeTime(plc.LastUpdated) : '', status: 'neutral' })
  }

  // 4. Everything else the agent reported about the system
  for (const [key, value] of Object.entries(meta)) {
    if (STRUCTURED_METADATA_KEYS.has(key)) continue
    add({ id: `meta_${key}`, name: humanize(key), category: 'Reported System Info', value: stringify(value), status: 'neutral' })
  }
  if (!meta.OsVersion && !meta.OperatingSystem && t?.osVersion) {
    add({ id: 'os_version', name: 'Operating System', category: 'Reported System Info', value: t.osVersion, status: 'neutral' })
  }

  // 5. Registered hardware / software components
  for (const item of c.inventoryItems || []) {
    const details = item.metadata && typeof item.metadata === 'object'
      ? Object.entries(item.metadata).filter(([, v]) => v !== null && typeof v !== 'object').slice(0, 4).map(([k, v]) => `${humanize(k)}: ${v}`).join(' · ')
      : ''
    add({
      id: `item_${item.id}`,
      name: item.displayName || item.name,
      category: 'Installed Components',
      value: details || item.itemType || 'Registered component',
      status: 'neutral',
      badge: item.itemType
    })
  }

  // 6. Stations and ownership
  for (const st of c.controlledMachines || []) {
    add({
      id: `station_${st.id}`,
      name: 'Controlled Station',
      category: 'Stations & Ownership',
      value: st.customIdentifier ? `${st.customIdentifier} · ${st.name}` : st.name,
      status: 'info',
      badge: st.pinnedObjectHandle || undefined
    })
  }
  for (const team of c.responsibleTeams || []) {
    add({ id: `team_${team.id}`, name: 'Responsible Team', category: 'Stations & Ownership', value: team.name, status: 'neutral' })
  }
  add({ id: 'organization', name: 'Organization', category: 'Stations & Ownership', value: c.organizationId || '', status: 'neutral' })

  return list
})

// Pinned properties filtered for quick top view
const pinnedProperties = computed(() => {
  return allProperties.value.filter(p => pinnedPropertyIds.value.includes(p.id))
})

// Filtered properties based on active category and search
const filteredProperties = computed(() => {
  let list = allProperties.value
  if (selectedCategory.value !== 'All') {
    list = list.filter(p => p.category === selectedCategory.value)
  }
  if (propertySearchQuery.value) {
    const q = propertySearchQuery.value.toLowerCase()
    list = list.filter(p => 
      p.name.toLowerCase().includes(q) || 
      p.value.toLowerCase().includes(q) ||
      p.category.toLowerCase().includes(q) ||
      (p.description && p.description.toLowerCase().includes(q)) ||
      (p.badge && p.badge.toLowerCase().includes(q))
    )
  }
  return list
})

const CATEGORY_ORDER: PropertyCategory[] = [
  'Identity & Network',
  'Resources',
  'PLC & Runtime',
  'Reported System Info',
  'Installed Components',
  'Stations & Ownership'
]

/** Only categories this controller actually has data for. */
const categories = computed<string[]>(() => [
  'All',
  ...CATEGORY_ORDER.filter(cat => allProperties.value.some(p => p.category === cat))
])

const getStatusColor = (status?: string) => {
  switch (status) {
    case 'success': return 'text-emerald-700 dark:text-emerald-400 bg-emerald-500/10 border-emerald-500/20'
    case 'warning': return 'text-amber-700 dark:text-amber-400 bg-amber-500/10 border-amber-500/20'
    case 'error': return 'text-rose-700 dark:text-rose-400 bg-rose-500/10 border-rose-500/20'
    case 'info': return 'text-sky-700 dark:text-sky-400 bg-sky-500/10 border-sky-500/20'
    default: return 'text-muted-foreground bg-muted/60 border-border'
  }
}
</script>

<template>
  <div class="p-6 bg-card border border-border rounded-xl space-y-6 shadow-xs">
    <!-- Controller Identity Banner -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-border">
      <div class="flex items-center gap-3.5">
        <div class="p-2.5 bg-primary/10 border border-primary/20 rounded-lg text-primary">
          <Monitor class="w-6 h-6" />
        </div>
        <div>
          <div class="flex items-center gap-2.5">
            <h3 class="text-lg font-bold text-foreground tracking-tight">{{ controller.hostname || controller.name }}</h3>
            <span class="w-2 h-2 rounded-full" :class="controller.telemetry?.isOnline ? 'bg-emerald-500 shadow-[0_0_8px_rgba(52,211,153,0.8)] animate-pulse' : 'bg-muted-foreground/40'"></span>
          </div>
          <p class="text-xs font-mono text-muted-foreground flex items-center gap-2 mt-0.5">
            <span>MAC: {{ controller.macAddress }}</span>
            <span class="text-muted-foreground/40">•</span>
            <span>IP: {{ controller.ipAddress || 'not reported' }}</span>
            <span class="text-muted-foreground/40">•</span>
            <span class="text-primary font-medium">UUID: {{ controller.id.substring(0, 8) }}</span>
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2.5 flex-wrap">
        <Badge
          variant="outline"
          class="text-xs font-mono font-medium px-2.5 py-0.5 rounded-md"
          :class="controller.telemetry?.isOnline ? 'border-emerald-500/30 text-emerald-700 dark:text-emerald-400 bg-emerald-500/10' : 'border-border text-muted-foreground bg-muted/40'"
        >
          {{ controller.telemetry?.isOnline ? 'Live Telemetry Active' : 'Offline' }}
        </Badge>
        <Button
          variant="outline"
          size="sm"
          @click="emit('quick-view', controller)"
          class="h-8 rounded-lg bg-primary hover:bg-primary/90 text-primary-foreground border-0 text-xs font-semibold flex items-center gap-1.5 shadow-xs cursor-pointer transition-all active:scale-95"
          title="Launch Remote Quick View Session"
        >
          <Eye class="size-3.5" />
          <span>Remote Session</span>
        </Button>
      </div>
    </div>

    <!-- Spatial CAD / DXF Tag Link Banner -->
    <div class="p-3 bg-muted/30 border border-border rounded-lg flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-md bg-primary/10 text-primary border border-primary/20">
          <MapPin class="size-4" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-muted-foreground">AutoCAD (DXF) Spatial Tag:</span>
            <span v-if="controller.pinnedObjectHandle" class="text-xs font-mono font-medium text-primary bg-primary/10 border border-primary/30 px-2.5 py-0.5 rounded-md">
              {{ controller.pinnedObjectHandle }}
            </span>
            <span v-else class="text-xs font-mono text-amber-700 dark:text-amber-400 bg-amber-500/10 border border-amber-500/30 px-2 py-0.5 rounded-md">
              Unpinned (No CAD Coordinate Linked)
            </span>
          </div>
          <p class="text-xs text-muted-foreground mt-0.5">Physical equipment coordinate mapping on the factory floor plan.</p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <Button
          v-if="controller.pinnedObjectHandle"
          variant="outline"
          size="sm"
          @click="emit('locate-map', controller.pinnedObjectHandle)"
          class="h-8 rounded-lg bg-primary/10 hover:bg-primary/20 border-primary/30 text-primary text-xs font-medium flex items-center gap-1.5 cursor-pointer"
        >
          <MapPin class="size-3" />
          <span>Locate on CAD Map</span>
        </Button>
        <Button
          variant="outline"
          size="sm"
          @click="emit('link-dxf', controller)"
          class="h-8 rounded-lg bg-card hover:bg-accent border-border text-foreground text-xs font-medium flex items-center gap-1.5 cursor-pointer"
        >
          <Link class="size-3" />
          <span>{{ controller.pinnedObjectHandle ? 'Edit DXF Link' : '+ Link DXF Tag' }}</span>
        </Button>
        <Button
          v-if="controller.pinnedObjectHandle"
          variant="outline"
          size="sm"
          @click="emit('unpin-dxf', controller)"
          class="h-8 rounded-lg bg-card hover:bg-rose-500/10 border-border text-muted-foreground hover:text-rose-600 text-xs font-medium flex items-center gap-1.5 cursor-pointer"
          title="Remove the floor-plan tag (station bindings are kept)"
        >
          <PinOff class="size-3" />
          <span>Unpin</span>
        </Button>
      </div>
    </div>

    <!-- Section 1: User-Pinned Quick Metrics Grid -->
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Pin class="w-3.5 h-3.5 text-primary" />
          <span class="text-xs font-semibold text-foreground">
            Pinned Quick Metrics ({{ pinnedProperties.length }})
          </span>
        </div>
        <span class="text-xs text-muted-foreground">Click the pin icon on any property below to watch here</span>
      </div>

      <div v-if="pinnedProperties.length === 0" class="p-6 bg-muted/20 border border-dashed border-border rounded-lg text-center">
        <p class="text-xs text-muted-foreground">No properties pinned yet. Browse categories below and click <Pin class="w-3 h-3 inline mx-1 text-muted-foreground" /> to pin key metrics here.</p>
      </div>

      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <div
          v-for="prop in pinnedProperties"
          :key="`pinned-${prop.id}`"
          class="p-3.5 bg-muted/30 border border-border hover:border-primary/40 rounded-lg flex flex-col justify-between transition-all group"
        >
          <div class="flex items-start justify-between gap-2">
            <span class="text-xs font-medium text-muted-foreground truncate">{{ prop.name }}</span>
            <button
              type="button"
              @click="togglePin(prop.id)"
              class="text-primary hover:text-destructive transition-colors p-1 rounded hover:bg-accent cursor-pointer"
              title="Unpin property"
            >
              <Pin class="w-3 h-3 fill-primary" />
            </button>
          </div>

          <div class="my-2">
            <div class="text-sm font-semibold font-mono text-foreground group-hover:text-primary transition-colors truncate">
              {{ prop.value }}
            </div>
          </div>

          <div class="flex items-center justify-between pt-1 border-t border-border/60">
            <span class="text-xs text-muted-foreground">{{ prop.category }}</span>
            <Badge v-if="prop.badge" variant="outline" class="text-xs font-mono font-medium px-1.5 py-0.5 rounded-md" :class="getStatusColor(prop.status)">
              {{ prop.badge }}
            </Badge>
          </div>
        </div>
      </div>
    </div>

    <!-- Section 2: Complete Properties Inspector & Category Filter -->
    <div class="space-y-4 pt-2">
      <!-- Search & Category Filters -->
      <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
        <!-- Category Filter Tabs -->
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="cat in categories"
            :key="cat"
            type="button"
            @click="selectedCategory = cat"
            class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all border cursor-pointer"
            :class="selectedCategory === cat ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-accent'"
          >
            {{ cat }}
          </button>
        </div>

        <!-- Filter Search Box -->
        <div class="relative w-full lg:w-72">
          <Search class="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input
            v-model="propertySearchQuery"
            placeholder="Search properties (e.g. MES, TwinCAT)..."
            class="pl-9 pr-4 h-9 bg-background border-border rounded-lg text-xs text-foreground placeholder:text-muted-foreground focus-visible:ring-primary"
          />
        </div>
      </div>

      <!-- Properties Grid Table -->
      <div class="bg-card border border-border rounded-lg overflow-hidden divide-y divide-border">
        <template v-if="filteredProperties.length === 0">
          <div class="p-12 text-center text-muted-foreground">
            <p class="text-xs font-medium">No properties match your filter</p>
          </div>
        </template>

        <template v-else>
          <div
            v-for="prop in filteredProperties"
            :key="prop.id"
            class="p-3.5 flex flex-col sm:flex-row sm:items-center justify-between gap-3 hover:bg-muted/50 transition-colors group"
          >
            <!-- Name & Category -->
            <div class="sm:w-1/3 min-w-0">
              <div class="flex items-center gap-2">
                <span class="text-xs font-semibold text-foreground group-hover:text-primary transition-colors truncate">
                  {{ prop.name }}
                </span>
                <Badge v-if="prop.badge" variant="outline" class="text-xs font-mono px-1.5 py-0.5 rounded-md" :class="getStatusColor(prop.status)">
                  {{ prop.badge }}
                </Badge>
              </div>
              <p v-if="prop.description" class="text-xs text-muted-foreground truncate mt-0.5">{{ prop.description }}</p>
            </div>

            <!-- Value Display -->
            <div class="sm:w-1/2 min-w-0">
              <div class="text-xs font-mono font-medium text-foreground group-hover:text-primary transition-colors break-words">
                {{ prop.value }}
              </div>
              <span class="text-xs font-mono text-muted-foreground block mt-0.5">{{ prop.category }}</span>
            </div>

            <!-- Action Buttons: Copy & Pin -->
            <div class="flex items-center justify-end gap-2 shrink-0">
              <!-- Copy Button -->
              <button
                type="button"
                @click="copyValue(prop.id, prop.value)"
                class="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-accent transition-colors cursor-pointer"
                :title="copiedPropertyId === prop.id ? 'Copied!' : 'Copy value'"
              >
                <Check v-if="copiedPropertyId === prop.id" class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" />
                <Copy v-else class="w-3.5 h-3.5" />
              </button>

              <!-- Pin Toggle Button -->
              <button
                type="button"
                @click="togglePin(prop.id)"
                class="flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium transition-all border cursor-pointer"
                :class="isPinned(prop.id) 
                  ? 'bg-primary/15 border-primary/30 text-primary hover:bg-primary/25' 
                  : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-accent'"
                :title="isPinned(prop.id) ? 'Unpin from quick metrics' : 'Pin to quick metrics'"
              >
                <Pin class="w-3 h-3" :class="{ 'fill-primary text-primary': isPinned(prop.id) }" />
                <span>{{ isPinned(prop.id) ? 'Pinned' : 'Pin' }}</span>
              </button>
            </div>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>
