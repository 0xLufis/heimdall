<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ChevronLeft,
  Cpu,
  Monitor,
  Wrench,
  AlertTriangle,
  Activity,
  History,
  Shield,
  Layers,
  Settings,
  Archive,
  ExternalLink,
  Plus,
  CheckCircle2,
  Tag as TagIcon,
  Binary,
  Key,
  FileText,
  Users,
  Compass,
  Zap,
  Sparkles,
  FileSpreadsheet,
  Clock,
  ArrowRight,
  ShieldAlert,
  FolderTree
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '~/components/ui/card'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '~/components/ui/tabs'
import MachinePictogram from '~/components/machines/MachinePictogram.vue'
import RemoteQuickViewModal from '~/components/controllers/RemoteQuickViewModal.vue'
import DecommissionMachineModal from '~/components/machines/DecommissionMachineModal.vue'
import ComponentPartHistoryDrawer from '~/components/machines/ComponentPartHistoryDrawer.vue'
import AssignPartModal from '~/components/machines/AssignPartModal.vue'
import DelegationTestResolverModal from '~/components/delegations/DelegationTestResolverModal.vue'
import TicketTelemetryProbeVisualizer from '~/components/tickets/TicketTelemetryProbeVisualizer.vue'
import { getGroupColor } from '~/utils/machineGroupColors'
import { useAuthSession } from '~/composables/useAuthSession'
import type { MachineComponentSlot } from '~/types/inventory'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const router = useRouter()
const id = route.params.id as string

const { isEngineer, isAdmin } = useAuthSession()

const machine = ref<any>(null)
const tickets = ref<any[]>([])
const componentsData = ref<{
  slots: MachineComponentSlot[]
  software: any[]
  licenses: any[]
  documents: any[]
  serviceLogs: any[]
}>({
  slots: [],
  software: [],
  licenses: [],
  documents: [],
  serviceLogs: []
})
const delegationBindings = ref<any[]>([])
const loading = ref(true)
const activeTab = ref('hardware')

// Modal and drawer controls
const isRemoteModalOpen = ref(false)
const remoteController = ref<any>(null)
const isDecommissionModalOpen = ref(false)
const isHistoryDrawerOpen = ref(false)
const selectedSlotForHistory = ref<MachineComponentSlot | null>(null)
const isAssignPartModalOpen = ref(false)
const selectedSlotForAssign = ref<MachineComponentSlot | null>(null)
const isTestResolverOpen = ref(false)

const groupColor = computed(() => getGroupColor(machine.value?.groupId || machine.value?.groupName))

const operationalStateClass = computed(() => {
  const state = machine.value?.operationalState || (machine.value?.status === 'active' ? 'Running' : 'Idle')
  switch (state.toLowerCase()) {
    case 'running':
      return { dot: 'bg-emerald-500 animate-pulse', text: 'text-emerald-500', badge: 'border-emerald-500/30 bg-emerald-500/10' }
    case 'idle':
      return { dot: 'bg-blue-500', text: 'text-blue-500', badge: 'border-blue-500/30 bg-blue-500/10' }
    case 'maintenance':
      return { dot: 'bg-amber-500', text: 'text-amber-500', badge: 'border-amber-500/30 bg-amber-500/10' }
    case 'down':
      return { dot: 'bg-rose-500 animate-ping', text: 'text-rose-500', badge: 'border-rose-500/30 bg-rose-500/10' }
    default:
      return { dot: 'bg-muted-foreground', text: 'text-muted-foreground', badge: 'border-border bg-muted/40' }
  }
})

const fetchMachineData = async () => {
  loading.value = true
  try {
    const list = await $fetch<any[]>('/api/machines', {
      params: { includeDecommissioned: true }
    }).catch(() => [])

    const found = list.find((m: any) => m.id === id || m.customIdentifier === id || m.name === id)
    if (found) {
      machine.value = {
        ...found,
        operationalState: found.operationalState || (found.status === 'active' ? 'Running' : 'Idle'),
        criticality: found.criticality || 'Tier2_Major',
        technologies: found.technologies || ['Welding', 'Robotics', 'Laser']
      }
    } else {
      machine.value = {
        id,
        name: `Workcell ${id.slice(0, 8)}`,
        displayName: `Production Station ${id.slice(0, 8)}`,
        customIdentifier: `LINE1-OP20`,
        machineType: 'Welding',
        groupId: 'Line 1',
        status: 'active',
        operationalState: 'Running',
        criticality: 'Tier1_Critical',
        technologies: ['Laser', 'Welding', 'Robotics'],
        controllers: [{ id: 'c-1', hostname: 'IPC-L1-02', ipAddress: '192.168.1.111', isOnline: true }],
        responsibleTeams: [{ name: 'Controls & Electrical Ops' }]
      }
    }

    // Fetch Four Quadrants & Components data
    const compRes = await $fetch<any>(`/api/components/machine/${machine.value.id}`).catch(() => ({
      slots: [],
      software: [],
      licenses: [],
      documents: [],
      serviceLogs: []
    }))
    componentsData.value = compRes

    // Fetch Delegation bindings
    const bindingsRes = await $fetch<any[]>(`/api/delegations/machine/${machine.value.id}`).catch(() => [])
    delegationBindings.value = bindingsRes

    // Fetch tickets
    const allTickets = await $fetch<any[]>('/api/tickets').catch(() => [])
    tickets.value = allTickets.filter((t: any) =>
      t.stationId === machine.value?.id ||
      t.stationId === machine.value?.customIdentifier ||
      (t.tags && t.tags.includes(machine.value?.customIdentifier))
    )
  } catch (err) {
    console.error('Error fetching machine details:', err)
  } finally {
    loading.value = false
  }
}

const openHistoryDrawer = (slot: MachineComponentSlot) => {
  selectedSlotForHistory.value = slot
  isHistoryDrawerOpen.value = true
}

const openAssignPartModal = (slot?: MachineComponentSlot) => {
  selectedSlotForAssign.value = slot || null
  isAssignPartModalOpen.value = true
}

const openRemoteView = () => {
  const c = machine.value?.controllers?.[0]
  if (c) {
    remoteController.value = {
      id: c.id || c.hostname,
      name: c.hostname || machine.value?.name,
      hostname: c.hostname || 'IPC-Host',
      ipAddress: c.ipAddress || '127.0.0.1',
      isOnline: c.isOnline ?? true
    }
    isRemoteModalOpen.value = true
  }
}

const onPartAssigned = () => {
  fetchMachineData()
}

onMounted(() => {
  fetchMachineData()
})
</script>

<template>
  <div class="space-y-6 pb-20 text-foreground max-w-7xl mx-auto">
    <!-- Top Breadcrumb Bar & Quick Actions -->
    <div class="flex items-center justify-between border-b border-border pb-4">
      <div class="flex items-center gap-2">
        <NuxtLink to="/dashboard/machines">
          <Button variant="ghost" size="sm" class="text-muted-foreground hover:text-foreground text-xs gap-1.5 -ml-2 cursor-pointer">
            <ChevronLeft class="size-4" />
            <span>Back to Production Floor</span>
          </Button>
        </NuxtLink>
        <span class="text-muted-foreground/40">/</span>
        <span class="font-mono text-xs font-semibold text-primary uppercase">
          {{ machine?.customIdentifier || id }}
        </span>
      </div>

      <div class="flex items-center gap-2">
        <Button
          size="sm"
          variant="outline"
          @click="isTestResolverOpen = true"
          class="text-xs h-8 gap-1.5 cursor-pointer border-purple-500/30 text-purple-600 dark:text-purple-400 hover:bg-purple-500/10"
        >
          <Sparkles class="size-3.5" />
          <span>Test Delegation Route</span>
        </Button>

        <Button
          v-if="machine?.controllers?.length > 0"
          size="sm"
          variant="outline"
          @click="openRemoteView()"
          class="text-xs h-8 gap-1.5 cursor-pointer border-indigo-500/30 text-indigo-600 dark:text-indigo-400 hover:bg-indigo-500/10"
        >
          <Monitor class="size-3.5" />
          <span>Remote IPC View</span>
        </Button>

        <Button
          size="sm"
          @click="openAssignPartModal()"
          class="text-xs h-8 gap-1.5 cursor-pointer font-bold"
        >
          <Plus class="size-3.5" />
          <span>Commission Component Slot</span>
        </Button>

        <Button
          v-if="machine?.status !== 'decommissioned' && machine?.status !== 'dismantled'"
          size="sm"
          variant="outline"
          @click="isDecommissionModalOpen = true"
          class="text-xs h-8 gap-1.5 cursor-pointer border-rose-500/30 text-rose-600 dark:text-rose-400 hover:bg-rose-500/10"
        >
          <Archive class="size-3.5" />
          <span>Decommission</span>
        </Button>
      </div>
    </div>

    <div v-if="loading" class="p-16 text-center text-muted-foreground text-sm font-medium">
      Loading machine aggregates, component slots, and telemetry...
    </div>

    <template v-else-if="machine">
      <!-- Machine Aggregate Hero Banner -->
      <Card class="border-2 shadow-sm rounded-2xl overflow-hidden bg-card" :class="groupColor.border">
        <div class="h-1.5 w-full" :style="{ backgroundColor: groupColor.hex }"></div>
        <CardContent class="p-6">
          <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-6">
            <div class="flex items-start gap-5">
              <div
                class="p-4 rounded-2xl border flex items-center justify-center shrink-0 shadow-xs"
                :class="[groupColor.border, groupColor.bg]"
              >
                <MachinePictogram
                  :type="machine.machineType"
                  :size="54"
                  :colorClass="groupColor.text"
                />
              </div>

              <div class="space-y-2">
                <div class="flex flex-wrap items-center gap-2.5">
                  <h1 class="text-2xl font-black tracking-tight text-foreground">
                    {{ machine.displayName || machine.name }}
                  </h1>
                  <span class="font-mono text-sm px-2.5 py-0.5 rounded-lg bg-muted text-foreground border border-border font-bold">
                    {{ machine.customIdentifier }}
                  </span>

                  <!-- Operational State Indicator -->
                  <div
                    class="px-2.5 py-0.5 rounded-full text-xs font-bold border flex items-center gap-1.5"
                    :class="operationalStateClass.badge"
                  >
                    <span class="size-2 rounded-full" :class="operationalStateClass.dot"></span>
                    <span :class="operationalStateClass.text">State: {{ machine.operationalState }}</span>
                  </div>

                  <!-- Criticality Tier -->
                  <Badge variant="outline" class="text-xs font-mono font-semibold">
                    {{ machine.criticality || 'Tier2_Major' }}
                  </Badge>

                  <!-- Line Interface Badge -->
                  <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold border flex items-center gap-1.5" :class="groupColor.badge">
                    <span class="size-1.5 rounded-full" :style="{ backgroundColor: groupColor.hex }"></span>
                    <span>Line: {{ machine.groupId || 'Line 1' }}</span>
                  </span>
                </div>

                <!-- Technologies & Controller Details -->
                <div class="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                  <div class="flex items-center gap-1.5">
                    <span class="font-semibold text-foreground">Dynamic Techs:</span>
                    <span
                      v-for="tech in (machine.technologies || [])"
                      :key="tech"
                      class="px-2 py-0.2 rounded-md bg-muted text-primary text-[11px] font-bold border border-border"
                    >
                      #{{ tech }}
                    </span>
                  </div>
                  <span>&bull;</span>
                  <span>IPC: <code class="font-mono text-primary font-bold">{{ machine.controllers?.[0]?.hostname || 'No IPC' }}</code></span>
                  <span>&bull;</span>
                  <span>Discipline Crew: <strong class="text-foreground">{{ machine.responsibleTeams?.[0]?.name || 'Controls Engineering' }}</strong></span>
                </div>
              </div>
            </div>

            <!-- Quadrant Quick Summary Counters -->
            <div class="grid grid-cols-4 gap-2.5 bg-muted/30 p-3 rounded-xl border border-border shrink-0 text-center">
              <div class="px-2">
                <div class="text-[10px] uppercase font-bold text-muted-foreground">HW Slots</div>
                <div class="font-mono text-lg font-bold text-foreground">{{ componentsData.slots.length }}</div>
              </div>
              <div class="h-8 w-px bg-border my-auto"></div>
              <div class="px-2">
                <div class="text-[10px] uppercase font-bold text-muted-foreground">SW Assets</div>
                <div class="font-mono text-lg font-bold text-primary">{{ componentsData.software.length }}</div>
              </div>
              <div class="h-8 w-px bg-border my-auto"></div>
              <div class="px-2">
                <div class="text-[10px] uppercase font-bold text-muted-foreground">Delegations</div>
                <div class="font-mono text-lg font-bold text-purple-500">{{ delegationBindings.length }}</div>
              </div>
              <div class="h-8 w-px bg-border my-auto"></div>
              <div class="px-2">
                <div class="text-[10px] uppercase font-bold text-muted-foreground">Tickets</div>
                <div class="font-mono text-lg font-bold text-amber-500">{{ tickets.length }}</div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- Quadrants Navigation Tabs -->
      <Tabs v-model="activeTab" class="w-full space-y-6">
        <TabsList class="grid grid-cols-6 max-w-4xl bg-muted/60 p-1 rounded-xl border border-border h-auto">
          <TabsTrigger value="hardware" class="text-xs font-semibold py-2 cursor-pointer gap-1.5">
            <Cpu class="size-3.5" />
            <span>1. Hardware</span>
          </TabsTrigger>
          <TabsTrigger value="software" class="text-xs font-semibold py-2 cursor-pointer gap-1.5">
            <Binary class="size-3.5" />
            <span>2. Software</span>
          </TabsTrigger>
          <TabsTrigger value="licenses" class="text-xs font-semibold py-2 cursor-pointer gap-1.5">
            <Key class="size-3.5" />
            <span>3. Licenses</span>
          </TabsTrigger>
          <TabsTrigger value="docs" class="text-xs font-semibold py-2 cursor-pointer gap-1.5">
            <FileText class="size-3.5" />
            <span>4. Docs</span>
          </TabsTrigger>
          <TabsTrigger value="delegations" class="text-xs font-semibold py-2 cursor-pointer gap-1.5">
            <Users class="size-3.5" />
            <span>Delegations</span>
          </TabsTrigger>
          <TabsTrigger value="journal" class="text-xs font-semibold py-2 cursor-pointer gap-1.5">
            <Activity class="size-3.5" />
            <span>Journal</span>
          </TabsTrigger>
        </TabsList>

        <!-- QUADRANT 1: HARDWARE COMPONENTS & SLOTS -->
        <TabsContent value="hardware" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-sm font-bold text-foreground">Operational Component Slots (Hardware Quadrant)</h2>
              <p class="text-xs text-muted-foreground">Installed equipment slots with physical part inventory links and EPLAN/CAD mappings</p>
            </div>
            <Button size="sm" variant="outline" @click="openAssignPartModal()" class="text-xs gap-1.5 cursor-pointer">
              <Plus class="size-3.5" />
              <span>Add Functional Slot</span>
            </Button>
          </div>

          <div v-if="componentsData.slots.length === 0" class="p-12 text-center rounded-2xl border border-dashed border-border text-xs text-muted-foreground space-y-3">
            <Cpu class="size-8 mx-auto text-muted-foreground/40" />
            <p>No component slots configured on this machine yet.</p>
            <Button size="sm" @click="openAssignPartModal()" class="text-xs font-bold">
              Commission First Component
            </Button>
          </div>

          <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card
              v-for="slot in componentsData.slots"
              :key="slot.id"
              class="border rounded-xl shadow-xs overflow-hidden hover:border-primary/50 transition-all bg-card"
            >
              <CardContent class="p-5 space-y-4">
                <div class="flex items-start justify-between gap-3">
                  <div class="space-y-1">
                    <div class="flex items-center gap-2">
                      <span class="font-mono text-xs font-black px-2 py-0.5 rounded bg-primary/10 text-primary border border-primary/20">
                        {{ slot.functionalSlotIdentifier }}
                      </span>
                      <Badge variant="outline" class="text-[10px]">{{ slot.category }}</Badge>
                    </div>
                    <h3 class="text-sm font-bold text-foreground">{{ slot.name }}</h3>
                  </div>

                  <div class="flex items-center gap-1.5 shrink-0">
                    <Button
                      variant="ghost"
                      size="sm"
                      @click="openHistoryDrawer(slot)"
                      class="text-xs h-7 gap-1 text-muted-foreground hover:text-foreground cursor-pointer px-2"
                      title="View replacement history"
                    >
                      <History class="size-3.5" />
                      <span>Ledger</span>
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      @click="openAssignPartModal(slot)"
                      class="text-xs h-7 gap-1 font-bold cursor-pointer px-2.5"
                    >
                      <Wrench class="size-3.5" />
                      <span>Swap Part</span>
                    </Button>
                  </div>
                </div>

                <!-- Installed Physical Part -->
                <div class="p-3 rounded-lg bg-muted/40 border border-border/70 space-y-1.5">
                  <div class="flex items-center justify-between text-[11px] text-muted-foreground">
                    <span class="uppercase font-bold tracking-wider">Installed Warehouse Part</span>
                    <span class="font-mono text-emerald-500 font-bold">Operational</span>
                  </div>
                  <div class="flex items-center justify-between text-xs font-mono font-bold text-foreground">
                    <span>Serial: {{ slot.currentPartSerialNumber || 'N/A' }}</span>
                    <span class="text-[11px] text-muted-foreground font-normal">Recipe: {{ slot.componentRecipeCode || 'Custom' }}</span>
                  </div>
                </div>

                <!-- EPLAN & CAD Reference Box -->
                <div v-if="slot.eplanCadRef" class="p-3 rounded-lg bg-background border border-border space-y-1.5">
                  <div class="text-[10px] uppercase font-bold text-muted-foreground flex items-center gap-1">
                    <FileSpreadsheet class="size-3" />
                    <span>EPLAN Schematics &amp; CAD Reference</span>
                  </div>
                  <div class="grid grid-cols-2 gap-2 text-xs font-mono">
                    <div>
                      <span class="text-muted-foreground text-[10px]">BMK: </span>
                      <strong class="text-primary">{{ slot.eplanCadRef.deviceTag || 'N/A' }}</strong>
                    </div>
                    <div>
                      <span class="text-muted-foreground text-[10px]">Page: </span>
                      <span>{{ slot.eplanCadRef.drawingPage || 'N/A' }}</span>
                    </div>
                    <div>
                      <span class="text-muted-foreground text-[10px]">CAD Handle: </span>
                      <span>{{ slot.eplanCadRef.cadHandle || 'N/A' }}</span>
                    </div>
                    <div>
                      <span class="text-muted-foreground text-[10px]">BOM Pos: </span>
                      <span>{{ slot.eplanCadRef.bomPosition || 'N/A' }}</span>
                    </div>
                  </div>
                  <div v-if="slot.eplanCadRef.functionText" class="text-[11px] text-muted-foreground italic border-t border-border/50 pt-1">
                    &ldquo;{{ slot.eplanCadRef.functionText }}&rdquo;
                  </div>
                </div>

                <!-- Tags (AutoTags + UserTags) -->
                <div v-if="slot.tags && slot.tags.length > 0" class="flex flex-wrap items-center gap-1.5 pt-1">
                  <span
                    v-for="tag in slot.tags"
                    :key="tag"
                    class="px-1.5 py-0.5 rounded text-[10px] font-medium bg-muted text-muted-foreground border border-border"
                  >
                    #{{ tag }}
                  </span>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <!-- QUADRANT 2: SOFTWARE ASSETS -->
        <TabsContent value="software" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-sm font-bold text-foreground">Software Quadrant (5 Concrete Asset Types)</h2>
              <p class="text-xs text-muted-foreground">Binaries, Runtimes, Configs, IEC 61131-3 Programs, and Parameter Recipes</p>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card
              v-for="sw in componentsData.software"
              :key="sw.id"
              class="border rounded-xl shadow-xs overflow-hidden bg-card"
            >
              <CardContent class="p-5 space-y-3">
                <div class="flex items-start justify-between gap-2">
                  <div class="space-y-1">
                    <div class="flex items-center gap-2">
                      <Badge variant="default" class="text-[10px] font-mono uppercase">
                        Type: {{ sw.softwareType }}
                      </Badge>
                      <Badge variant="outline" class="text-[10px] text-emerald-500 border-emerald-500/30">
                        {{ sw.status }}
                      </Badge>
                    </div>
                    <h3 class="text-sm font-bold text-foreground">{{ sw.name }}</h3>
                  </div>
                  <span class="font-mono text-xs font-bold text-primary px-2 py-0.5 rounded bg-muted">
                    {{ sw.version }}
                  </span>
                </div>

                <p v-if="sw.description" class="text-xs text-muted-foreground">
                  {{ sw.description }}
                </p>

                <div v-if="sw.checksumSha256" class="p-2.5 rounded-lg bg-muted/40 border border-border text-[11px] font-mono space-y-0.5">
                  <div class="text-[9px] uppercase font-bold text-muted-foreground">SHA-256 Cryptographic Checksum</div>
                  <div class="truncate text-foreground font-bold">{{ sw.checksumSha256 }}</div>
                </div>

                <div class="flex items-center justify-between text-xs text-muted-foreground pt-1 border-t border-border/50">
                  <span>Author: <strong class="text-foreground">{{ sw.author || 'System' }}</strong></span>
                  <span>Updated: {{ new Date(sw.updatedAt).toLocaleDateString() }}</span>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <!-- QUADRANT 3: LICENSES (AES-256-GCM) -->
        <TabsContent value="licenses" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-sm font-bold text-foreground">License Assets Quadrant (AES-256-GCM Encrypted)</h2>
              <p class="text-xs text-muted-foreground">TwinCAT System IDs, hardware dongles, node-locked MAC keys, and floating seats</p>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card
              v-for="lic in componentsData.licenses"
              :key="lic.id"
              class="border rounded-xl shadow-xs overflow-hidden bg-card"
            >
              <CardContent class="p-5 space-y-3">
                <div class="flex items-start justify-between gap-2">
                  <div class="space-y-1">
                    <Badge variant="outline" class="text-[10px] font-mono text-primary">
                      {{ lic.licenseType }}
                    </Badge>
                    <h3 class="text-sm font-bold text-foreground">{{ lic.name }}</h3>
                  </div>
                  <Badge variant="default" class="text-[10px]">{{ lic.status }}</Badge>
                </div>

                <div class="p-3 rounded-lg bg-muted/40 border border-border space-y-1.5 text-xs">
                  <div class="flex items-center justify-between">
                    <span class="text-muted-foreground">Vendor:</span>
                    <span class="font-bold text-foreground">{{ lic.vendor }}</span>
                  </div>
                  <div class="flex items-center justify-between">
                    <span class="text-muted-foreground">Seats:</span>
                    <span class="font-mono font-bold">{{ lic.seatsCount }} Seats</span>
                  </div>
                  <div v-if="lic.hardwareLockIdentifier" class="flex items-center justify-between">
                    <span class="text-muted-foreground">Hardware Lock:</span>
                    <span class="font-mono text-primary font-bold">{{ lic.hardwareLockIdentifier }}</span>
                  </div>
                  <div class="flex items-center justify-between">
                    <span class="text-muted-foreground">AES-256-GCM Key:</span>
                    <span class="font-mono text-emerald-500 font-bold">&bull;&bull;&bull;&bull; &bull;&bull;&bull;&bull; (Encrypted in DB)</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <!-- QUADRANT 4: DOCUMENTATION -->
        <TabsContent value="docs" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-sm font-bold text-foreground">Documentation Quadrant (Schematics, Diagrams, SOPs)</h2>
              <p class="text-xs text-muted-foreground">Direct PDF schematics with symbol bounding box coordinates</p>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Card
              v-for="doc in componentsData.documents"
              :key="doc.id"
              class="border rounded-xl shadow-xs overflow-hidden bg-card"
            >
              <CardContent class="p-5 space-y-3">
                <div class="flex items-start justify-between gap-2">
                  <Badge variant="outline" class="text-[10px]">{{ doc.documentType }}</Badge>
                  <span class="font-mono text-[10px] text-muted-foreground">{{ doc.revision }}</span>
                </div>
                <h3 class="text-sm font-bold text-foreground">{{ doc.title }}</h3>
                <div class="text-[11px] font-mono text-muted-foreground">
                  Doc #: {{ doc.documentNumber }}
                </div>
                <div class="pt-2 border-t border-border flex items-center justify-between text-xs">
                  <span class="text-muted-foreground">{{ (doc.fileSizeBytes / 1024 / 1024).toFixed(1) }} MB PDF</span>
                  <Button variant="ghost" size="sm" class="text-xs h-7 gap-1 text-primary cursor-pointer">
                    <ExternalLink class="size-3" />
                    <span>Open Drawing</span>
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <!-- DELEGATION RECIPES & RESPONSIBILITIES -->
        <TabsContent value="delegations" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-sm font-bold text-foreground">Bound Delegation Recipes (Recipes for Responsibility)</h2>
              <p class="text-xs text-muted-foreground">Multi-recipe bindings covering disciplines (Mechanical, Controls, Safety, Quality) with shift and on-call routing</p>
            </div>
            <Button size="sm" @click="isTestResolverOpen = true" class="text-xs font-bold gap-1.5 cursor-pointer">
              <Sparkles class="size-3.5" />
              <span>Simulate Delegation Resolution</span>
            </Button>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card
              v-for="b in delegationBindings"
              :key="b.id"
              class="border rounded-xl shadow-xs overflow-hidden bg-card"
            >
              <CardContent class="p-5 space-y-3">
                <div class="flex items-start justify-between gap-2">
                  <div class="space-y-1">
                    <div class="flex items-center gap-2">
                      <Badge variant="default" class="text-[10px]">{{ b.discipline }}</Badge>
                      <Badge variant="outline" class="text-[10px] text-emerald-500">Active Binding</Badge>
                    </div>
                    <h3 class="text-sm font-bold text-foreground">{{ b.delegationRecipeName }}</h3>
                  </div>
                </div>

                <div class="p-3 rounded-lg bg-muted/40 border border-border space-y-1 text-xs">
                  <div class="flex items-center justify-between">
                    <span class="text-muted-foreground">Coverage Schedules:</span>
                    <span class="font-medium text-foreground">Shift &bull; Office-Time &bull; On-Call</span>
                  </div>
                  <div class="flex items-center justify-between">
                    <span class="text-muted-foreground">Gated Skills:</span>
                    <span class="font-mono text-primary font-bold">CERT-LASER-4 / SKILL-TWINCAT3</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <!-- OPERATIONAL JOURNAL (MACHINE SERVICE LOGS) -->
        <TabsContent value="journal" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-sm font-bold text-foreground">Operational Service Journal</h2>
              <p class="text-xs text-muted-foreground">Audit journal of maintenance, shift handovers, and emergency repairs</p>
            </div>
          </div>

          <div class="space-y-3">
            <Card
              v-for="log in componentsData.serviceLogs"
              :key="log.id"
              class="border rounded-xl shadow-xs bg-card"
            >
              <CardContent class="p-4 space-y-2">
                <div class="flex items-center justify-between text-xs">
                  <div class="flex items-center gap-2">
                    <Badge variant="outline" class="font-bold text-[10px]">{{ log.entryType }}</Badge>
                    <span class="font-semibold text-foreground">{{ log.summary }}</span>
                  </div>
                  <span class="text-muted-foreground text-[11px] font-mono">
                    {{ new Date(log.timestamp).toLocaleString() }}
                  </span>
                </div>
                <p v-if="log.details" class="text-xs text-muted-foreground bg-muted/30 p-2.5 rounded-lg border border-border/50">
                  {{ log.details }}
                </p>
                <div class="text-[11px] text-muted-foreground flex items-center justify-between pt-1">
                  <span>Author: <strong>{{ log.author }}</strong></span>
                  <span v-if="log.linkedTicketId" class="text-primary font-mono">
                    Linked Ticket #{{ log.linkedTicketId.slice(0, 6) }}
                  </span>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>
    </template>

    <!-- Modals & Drawers -->
    <ComponentPartHistoryDrawer
      v-model:open="isHistoryDrawerOpen"
      :slot="selectedSlotForHistory"
    />

    <AssignPartModal
      v-model:open="isAssignPartModalOpen"
      :machine-id="machine?.id || id"
      :existing-slot="selectedSlotForAssign"
      @success="onPartAssigned"
    />

    <DelegationTestResolverModal
      v-model:open="isTestResolverOpen"
      :machines="machine ? [machine] : []"
    />

    <RemoteQuickViewModal
      v-model="isRemoteModalOpen"
      :controller="remoteController"
    />

    <DecommissionMachineModal
      v-model="isDecommissionModalOpen"
      :machine="machine"
      @success="fetchMachineData"
    />
  </div>
</template>
