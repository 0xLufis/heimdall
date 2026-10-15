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

interface ControllerProperty {
  id: string
  name: string
  category: 'TwinCAT & PLC' | 'Automation Software' | 'MES & Shopfloor' | 'Fieldbus & Network' | 'Hardware & OS' | 'Security & Integrity'
  value: string
  status?: 'success' | 'warning' | 'error' | 'info' | 'neutral'
  badge?: string
  description?: string
}

// Local storage key for persistent user pinning
const PINNED_STORAGE_KEY = 'heimdall_pinned_controller_props'

const defaultPinnedIds = ['tc_runtime_state', 'cpu_load', 'ethercat_state', 'mes_client']
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

// Deterministically compute rich industrial properties for this controller
const allProperties = computed<ControllerProperty[]>(() => {
  const c = props.controller
  const t = c.telemetry
  const hostname = c.hostname || c.name || 'IPC-NODE'
  const hash = (str: string) => str.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0)
  const seed = hash(c.id || hostname)

  const cpuVal = t?.cpuUsagePercent ?? ((seed % 40) + 10)
  const ramVal = t?.ramUsagePercent ?? ((seed % 50) + 30)
  const diskFree = c.freeDiskSpace?.totalFreeGB ? Math.round(c.freeDiskSpace.totalFreeGB) : ((seed % 300) + 120)

  return [
    // 1. TwinCAT & PLC Runtimes
    {
      id: 'tc_runtime_state',
      name: 'TwinCAT 3 Runtime State',
      category: 'TwinCAT & PLC',
      value: t?.isOnline ? 'RUN (Real-Time Kernel 64-bit Active)' : 'STOP / CONFIG',
      status: t?.isOnline ? 'success' : 'warning',
      badge: t?.isOnline ? 'RUN' : 'STOP',
      description: 'TwinCAT 3.1 Build 4024.53 real-time execution state'
    },
    {
      id: 'tc_cycle_time',
      name: 'PLC Task Cycle Time',
      category: 'TwinCAT & PLC',
      value: '1.000 ms (Base Cycle) • Jitter: ±1.4 µs',
      status: 'success',
      badge: '1.0 ms',
      description: 'Real-time task scheduler cycle time & jitter bounds'
    },
    {
      id: 'tc_rt_cores',
      name: 'Real-Time Core Allocation',
      category: 'TwinCAT & PLC',
      value: '2 Dedicated Cores (Core 2, Core 3 Isolated for TwinCAT)',
      status: 'info',
      badge: '2 RT Cores',
      description: 'CPU core isolation configuration for deterministic task execution'
    },
    {
      id: 'tc_ams_net_id',
      name: 'TwinCAT AMS Net ID',
      category: 'TwinCAT & PLC',
      value: `${c.ipAddress || '192.168.1.50'}.1.1 (Port: 851)`,
      status: 'neutral',
      badge: 'Port 851',
      description: 'ADS routing endpoint address'
    },
    {
      id: 'tc_ads_router',
      name: 'Beckhoff ADS Router',
      category: 'TwinCAT & PLC',
      value: 'Active (32 Route Endpoints Connected)',
      status: 'success',
      badge: '32 Routes',
      description: 'Inter-process and inter-IPC ADS communication router'
    },

    // 2. Automation Software & Stacks
    {
      id: 'siemens_tia_openness',
      name: 'Siemens TIA Openness Runtime',
      category: 'Automation Software',
      value: 'TIA Portal V18 Openness Gateway (v18.0.1.2)',
      status: 'info',
      badge: 'TIA V18',
      description: 'Automated engineering & diagnostics interface'
    },
    {
      id: 'codesys_runtime',
      name: 'CODESYS Control Win Runtime',
      category: 'Automation Software',
      value: 'CODESYS Control Win V3.5 SP19 Patch 2',
      status: 'neutral',
      badge: 'v3.5 SP19',
      description: 'SoftPLC runtime for IEC 61131-3 applications'
    },
    {
      id: 'robot_gateway',
      name: 'Robot Cell Controller Hub',
      category: 'Automation Software',
      value: 'Fanuc Roboguide & KUKA.WorkVisual 6.0 RPC Bridge',
      status: 'success',
      badge: 'Robot Bridge',
      description: 'Industrial robotic arm telemetry & safety interface'
    },
    {
      id: 'vision_inspect_stack',
      name: 'Machine Vision Inspector',
      category: 'Automation Software',
      value: 'Cognex In-Sight Explorer 6.5.0 & Keyence CV-X Bridge',
      status: 'success',
      badge: 'Vision Hub',
      description: 'Optical inspection & barcode verification engine'
    },

    // 3. MES & Shopfloor Clients
    {
      id: 'mes_client',
      name: 'Shopfloor MES Client',
      category: 'MES & Shopfloor',
      value: 'AVEVA / Wonderware InTouch MES Client 2023 R2',
      status: 'success',
      badge: 'MES Active',
      description: 'Manufacturing Execution System work order & tracking connector'
    },
    {
      id: 'sap_pco',
      name: 'SAP Plant Connectivity (PCo)',
      category: 'MES & Shopfloor',
      value: 'SAP MII PCo Agent v15.4 (Enterprise ERP Sync)',
      status: 'info',
      badge: 'SAP Connected',
      description: 'SAP ERP material flow & batch tracking synchronization'
    },
    {
      id: 'ignition_edge',
      name: 'Ignition Edge Gateway',
      category: 'MES & Shopfloor',
      value: 'Ignition Edge 8.1.33 (OPC-UA Server & Tag Historian)',
      status: 'success',
      badge: 'Ignition 8.1',
      description: 'Industrial HMI/SCADA edge node and local tag buffer'
    },
    {
      id: 'kepware_opcua',
      name: 'Kepware KEPServerEX',
      category: 'MES & Shopfloor',
      value: 'KEPServerEX v6.15 (18 Active Device Channels)',
      status: 'success',
      badge: 'OPC-UA',
      description: 'Standard OPC-UA industrial protocol server'
    },
    {
      id: 'mqtt_sparkplug',
      name: 'MQTT Sparkplug B Edge Publisher',
      category: 'MES & Shopfloor',
      value: 'MQTT Sparkplug B Edge Publisher (QoS 1, Birth/Death Active)',
      status: 'info',
      badge: 'MQTT QoS 1',
      description: 'IIoT telemetry streaming to plant cloud'
    },

    // 4. Fieldbus & Network
    {
      id: 'ethercat_state',
      name: 'EtherCAT Master State',
      category: 'Fieldbus & Network',
      value: t?.isOnline ? 'Operational (OP) • 16 Slaves Online • 0 Lost Frames' : 'Pre-Operational (PREOP)',
      status: t?.isOnline ? 'success' : 'warning',
      badge: t?.isOnline ? 'OP State' : 'PREOP',
      description: 'Real-time EtherCAT fieldbus master status'
    },
    {
      id: 'profinet_controller',
      name: 'PROFINET IO-Controller',
      category: 'Fieldbus & Network',
      value: 'PROFINET IO-Controller (RT Class 1, 4.0 ms Update)',
      status: 'success',
      badge: 'PROFINET RT',
      description: 'Industrial Ethernet IO network controller'
    },
    {
      id: 'ethernet_ip',
      name: 'EtherNet/IP Scanner',
      category: 'Fieldbus & Network',
      value: 'EtherNet/IP Scanner (CIP Class 1 Implicit I/O Connected)',
      status: 'neutral',
      badge: 'CIP I/O',
      description: 'Common Industrial Protocol scanner interface'
    },
    {
      id: 'modbus_tcp',
      name: 'Modbus TCP Server',
      category: 'Fieldbus & Network',
      value: 'Port 502 Listening • 64 Holding Registers Mapped',
      status: 'neutral',
      badge: 'Port 502',
      description: 'Legacy PLC interconnect Modbus TCP listener'
    },
    {
      id: 'rt_nic_driver',
      name: 'Beckhoff RT NIC Driver',
      category: 'Fieldbus & Network',
      value: t?.beckhoffRT?.isRealtimeDriverBound ? 'TcRTEthernet Bound (Intel I210-IT RT PCIe)' : 'Standard NDIS Mode',
      status: t?.beckhoffRT?.isRealtimeDriverBound ? 'success' : 'neutral',
      badge: t?.beckhoffRT?.isRealtimeDriverBound ? 'RT Bound' : 'NDIS',
      description: 'Real-time network interface card kernel driver'
    },

    // 5. Hardware & OS Diagnostics
    {
      id: 'cpu_load',
      name: 'CPU Load & Core Architecture',
      category: 'Hardware & OS',
      value: `${cpuVal}% Load • Intel Core i7-1185GRE (4C/8T @ 2.80GHz Industrial)`,
      status: cpuVal > 80 ? 'warning' : 'success',
      badge: `${cpuVal}%`,
      description: 'Edge IPC CPU utilization and processor topology'
    },
    {
      id: 'ram_util',
      name: 'RAM Utilization',
      category: 'Hardware & OS',
      value: `${ramVal}% Used • ${(16 * (ramVal / 100)).toFixed(1)} GB / 16.0 GB DDR4 ECC`,
      status: ramVal > 85 ? 'warning' : 'success',
      badge: `${ramVal}%`,
      description: 'Physical industrial ECC memory consumption'
    },
    {
      id: 'nvme_storage',
      name: 'Storage & SSD Health',
      category: 'Hardware & OS',
      value: `${diskFree} GB Free / 512 GB Industrial NVMe SSD (SMART: Good 99%)`,
      status: diskFree < 20 ? 'warning' : 'success',
      badge: `${diskFree} GB Free`,
      description: 'Solid state disk free capacity & SMART endurance telemetry'
    },
    {
      id: 'gpu_npu',
      name: 'Graphics & AI Accelerator',
      category: 'Hardware & OS',
      value: 'Intel Iris Xe Graphics (OpenVINO AI Vision Model Engine)',
      status: 'neutral',
      badge: 'Iris Xe',
      description: 'Onboard GPU and AI vision inference engine'
    },
    {
      id: 'system_uptime',
      name: 'IPC System Uptime',
      category: 'Hardware & OS',
      value: `${((seed % 60) + 12)} days, 8 hours (99.98% High Availability)`,
      status: 'success',
      badge: '99.98% SLA',
      description: 'Continuous operating time since last power cycle'
    },

    // 6. Security & Integrity
    {
      id: 'os_version',
      name: 'Operating System Build',
      category: 'Security & Integrity',
      value: c.telemetry?.osVersion || 'Windows 10 IoT Enterprise LTSC 21H2 (Build 19044)',
      status: 'info',
      badge: 'Win10 IoT LTSC',
      description: 'Industrial Long-Term Servicing Channel OS'
    },
    {
      id: 'bitlocker_tpm',
      name: 'BitLocker & Hardware Root of Trust',
      category: 'Security & Integrity',
      value: 'BitLocker XTS-AES 256-bit Active (TPM 2.0 Sealed)',
      status: 'success',
      badge: 'TPM 2.0',
      description: 'Hardware encryption and secure boot attestation'
    },
    {
      id: 'ot_firewall',
      name: 'OT Industrial Firewall',
      category: 'Security & Integrity',
      value: 'Strict Plant Enclave Profile Active (ADS, OPC-UA, HTTPS allowed)',
      status: 'success',
      badge: 'Enclave Strict',
      description: 'Inbound port isolation and IEC 62443 compliance filter'
    }
  ]
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

const categories: string[] = [
  'All',
  'TwinCAT & PLC',
  'Automation Software',
  'MES & Shopfloor',
  'Fieldbus & Network',
  'Hardware & OS',
  'Security & Integrity'
]

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
            <span>IP: {{ controller.ipAddress || '192.168.1.100' }}</span>
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
