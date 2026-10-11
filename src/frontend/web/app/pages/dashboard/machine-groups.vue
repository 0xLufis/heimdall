<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  FolderTree,
  ArrowLeft,
  Plus,
  Trash2,
  Cpu,
  Layers,
  Sparkles,
  Search,
  ExternalLink,
  Shield,
  Clock,
  Activity,
  CheckCircle2,
  AlertTriangle,
  Zap,
  Building2,
  GitBranch,
  FileCode,
  Tag,
  KeyRound,
  Users
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import MachinePictogram from '~/components/machines/MachinePictogram.vue'
import { getGroupColor } from '~/utils/machineGroupColors'
import { DEMO_PERSONAS } from '~/composables/useAuthSession'

definePageMeta({
  layout: 'shadcn-dashboard'
})

// ─── Active Tab State ────────────────────────────────────────────────────────
type GroupTab = 'lines' | 'technologies' | 'clusters'
const activeTab = ref<GroupTab>('lines')

const tabs: { id: GroupTab; label: string; icon: any }[] = [
  { id: 'lines', label: 'Lines & Customer Contracts', icon: Building2 },
  { id: 'technologies', label: 'Dynamic Technological Groups', icon: Cpu },
  { id: 'clusters', label: 'Operational Clusters', icon: FolderTree }
]

// ─── State ───────────────────────────────────────────────────────────────────
const isLoading = ref(false)
const searchQuery = ref('')
const machines = ref<any[]>([])
const groups = ref<any[]>([])
const recipes = ref<any[]>([])
const personnelGroups = ref<any[]>([])

// ─── Modals State ────────────────────────────────────────────────────────────
const isCreateClusterModalOpen = ref(false)
const newCluster = ref({
  name: '',
  description: '',
  color: 'indigo',
  leadEngineerId: '',
  assignedMachineIds: [] as string[]
})

// ─── Fetch Data ──────────────────────────────────────────────────────────────
const fetchData = async () => {
  isLoading.value = true
  try {
    const [machRes, grpRes, recRes, pgRes] = await Promise.allSettled([
      $fetch<any[]>('/api/machines?includeDecommissioned=true').catch(() => $fetch<any[]>('/api/proxy/v1/machine')),
      $fetch<any[]>('/api/machine-groups'),
      $fetch<any[]>('/api/delegations/recipes'),
      $fetch<any[]>('/api/personnel/groups')
    ])

    if (machRes.status === 'fulfilled' && Array.isArray(machRes.value)) {
      machines.value = machRes.value
    } else {
      machines.value = [
        {
          id: '11111111-1111-1111-1111-111111111111',
          name: 'OP10-Load',
          displayName: 'Automatic Raw Material Infeed',
          customIdentifier: 'LINE1-OP10',
          machineType: 'Assembly',
          groupId: 'Line 1',
          technologies: ['Fastening', 'Pneumatics'],
          status: 'active'
        },
        {
          id: '22222222-2222-2222-2222-222222222222',
          name: 'OP20-Weld',
          displayName: 'Laser Robotic Welding Cell',
          customIdentifier: 'LINE1-OP20',
          machineType: 'Welding',
          groupId: 'Line 1',
          technologies: ['Laser', 'Welding', 'Robotics'],
          status: 'active'
        },
        {
          id: '33333333-3333-3333-3333-333333333333',
          name: 'OP30-Vision',
          displayName: 'Cognex AI Inspection Chamber',
          customIdentifier: 'LINE2-OP30',
          machineType: 'Test',
          groupId: 'Line 2',
          technologies: ['Vision', 'AI_Inspection'],
          status: 'maintenance'
        },
        {
          id: '44444444-4444-4444-4444-444444444444',
          name: 'OP40-Fasten',
          displayName: 'Multi-Spindle Screw Fastening Station',
          customIdentifier: 'LINE1-OP40',
          machineType: 'Fastening',
          groupId: 'Line 1',
          technologies: ['Fastening', 'Robotics'],
          status: 'active'
        },
        {
          id: '55555555-5555-5555-5555-555555555555',
          name: 'OP50-Dispense',
          displayName: 'Precision Adhesive Gasket Dispenser',
          customIdentifier: 'LINE2-OP50',
          machineType: 'Dispensing',
          groupId: 'Line 2',
          technologies: ['Dispensing', 'Fluidics'],
          status: 'inactive'
        },
        {
          id: '66666666-6666-6666-6666-666666666666',
          name: 'OP60-Robot',
          displayName: '6-Axis KUKA Palletizing Robot',
          customIdentifier: 'LINE2-OP60',
          machineType: 'Robotics',
          groupId: 'Line 2',
          technologies: ['Robotics', 'MaterialHandling'],
          status: 'active'
        }
      ]
    }

    if (grpRes.status === 'fulfilled' && Array.isArray(grpRes.value)) groups.value = grpRes.value
    if (recRes.status === 'fulfilled' && Array.isArray(recRes.value)) recipes.value = recRes.value
    if (pgRes.status === 'fulfilled' && Array.isArray(pgRes.value)) personnelGroups.value = pgRes.value
  } catch (err) {
    console.error('Failed to load machine groups data:', err)
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  fetchData()
})

// ─── Data Transformations ────────────────────────────────────────────────────

// 1. Production Lines with Interface Contracts
const productionLines = computed(() => {
  const lineContracts: Record<string, any> = {
    'Line 1': {
      contract: 'AUDI-PPE-REV3',
      cycleTimeSec: 42,
      bufferCapacity: 8,
      safetyZone: 'Zone-A-Interlock'
    },
    'Line 2': {
      contract: 'BMW-GEN5-V2.1',
      cycleTimeSec: 38,
      bufferCapacity: 12,
      safetyZone: 'Zone-B-Enclosure'
    },
    'Line 06 — Module Assembly (AUDI)': {
      contract: 'AUDI-MOD-2026',
      cycleTimeSec: 45,
      bufferCapacity: 10,
      safetyZone: 'Zone-Mod-06'
    },
    'Line 09 — Pack Assembly & End of Line': {
      contract: 'VW-MEB-STD4',
      cycleTimeSec: 60,
      bufferCapacity: 6,
      safetyZone: 'Zone-EOL-HV'
    }
  }

  const map: Record<string, any> = {}
  for (const m of machines.value) {
    const lName = m.groupId || 'Default Line'
    if (!map[lName]) {
      const contractData = lineContracts[lName] || {
        contract: 'STD-LINE-SPEC-V1',
        cycleTimeSec: 45,
        bufferCapacity: 10,
        safetyZone: 'General-Line-Interlock'
      }
      map[lName] = {
        name: lName,
        interfaceContract: contractData.contract,
        targetCycleTimeSec: contractData.cycleTimeSec,
        bufferCapacity: contractData.bufferCapacity,
        safetyZone: contractData.safetyZone,
        stations: []
      }
    }
    map[lName].stations.push(m)
  }
  return Object.values(map)
})

// 2. Dynamic Technological Groups (queried by technologyKey)
const technologicalGroups = computed(() => {
  const TECH_CONFIGS: Record<string, { discipline: string; requiredCert: string; description: string }> = {
    Laser: { discipline: 'Safety', requiredCert: 'CERT-LASER-4', description: 'Enclosed fiber and CO2 laser welding and cutting cells' },
    Robotics: { discipline: 'Controls', requiredCert: 'SKILL-ROBOT-KUKA', description: '6-axis articulated and SCARA industrial robotics' },
    Vision: { discipline: 'Quality', requiredCert: 'CERT-COGNEX-DEV', description: 'Deep-learning optical inspection and surface defect AI' },
    Dispensing: { discipline: 'Process', requiredCert: 'SKILL-FLUIDICS', description: 'Polyurethane and thermal interface paste metering' },
    Fastening: { discipline: 'Mechanical', requiredCert: 'SKILL-TORQUE-CAL', description: 'Multi-spindle automatic screw driving with angle-torque monitoring' },
    Welding: { discipline: 'Mechanical', requiredCert: 'CERT-WELD-DGUV', description: 'Ultrasonic and laser beam joining stations' },
    Assembly: { discipline: 'Mechanical', requiredCert: 'SKILL-GENERAL-MECH', description: 'Infeed conveyors, pneumatic indexing, and pressing tooling' }
  }

  const map: Record<string, any> = {}
  for (const m of machines.value) {
    const techs = m.technologies && m.technologies.length > 0
      ? m.technologies
      : [m.machineType || 'Assembly']

    for (const t of techs) {
      if (!map[t]) {
        const config = TECH_CONFIGS[t] || {
          discipline: 'General',
          requiredCert: 'STANDARD-MAINT',
          description: `Industrial ${t} machinery and associated control systems`
        }
        map[t] = {
          technologyKey: t,
          discipline: config.discipline,
          requiredCert: config.requiredCert,
          description: config.description,
          machines: []
        }
      }
      if (!map[t].machines.find((x: any) => x.id === m.id)) {
        map[t].machines.push(m)
      }
    }
  }
  return Object.values(map)
})

// 3. Operational Clusters
const operationalClusters = computed(() => {
  return groups.value.filter(g => g.groupType === 'Manual' || !g.groupType)
})

// ─── Filtered Lists ──────────────────────────────────────────────────────────
const filteredLines = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return productionLines.value
  return productionLines.value.filter(l =>
    l.name.toLowerCase().includes(q) ||
    l.interfaceContract.toLowerCase().includes(q)
  )
})

const filteredTechnologies = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return technologicalGroups.value
  return technologicalGroups.value.filter(t =>
    t.technologyKey.toLowerCase().includes(q) ||
    t.description.toLowerCase().includes(q) ||
    t.discipline.toLowerCase().includes(q)
  )
})

const filteredClusters = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return operationalClusters.value
  return operationalClusters.value.filter(c =>
    c.name?.toLowerCase().includes(q) ||
    c.description?.toLowerCase().includes(q) ||
    c.leadEngineerName?.toLowerCase().includes(q)
  )
})

// ─── Actions ─────────────────────────────────────────────────────────────────
const handleCreateCluster = async () => {
  try {
    const lead = DEMO_PERSONAS.find(p => p.id === newCluster.value.leadEngineerId)
    await $fetch('/api/machine-groups', {
      method: 'POST',
      body: {
        name: newCluster.value.name,
        description: newCluster.value.description,
        color: newCluster.value.color,
        groupType: 'Manual',
        leadEngineerId: lead?.id,
        leadEngineerName: lead?.name,
        machineIds: newCluster.value.assignedMachineIds
      }
    })
    isCreateClusterModalOpen.value = false
    fetchData()
  } catch (err: any) {
    alert(err?.data?.message || 'Failed to create operational cluster')
  }
}

const handleDeleteCluster = async (id: string) => {
  if (!confirm('Are you sure you want to delete this operational cluster?')) return
  try {
    await $fetch(`/api/machine-groups/${id}`, { method: 'DELETE' })
    fetchData()
  } catch (err) {
    console.error(err)
  }
}

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'active':
      return { label: 'Online', class: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border-emerald-500/30' }
    case 'maintenance':
      return { label: 'Maintenance', class: 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30' }
    case 'inactive':
      return { label: 'Offline', class: 'bg-slate-500/15 text-slate-600 dark:text-slate-400 border-slate-500/30' }
    default:
      return { label: status || 'Unknown', class: 'bg-muted text-muted-foreground border-border' }
  }
}
</script>

<template>
  <div class="space-y-6 pb-16">
    <!-- Header & Breadcrumbs -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-border pb-5">
      <div>
        <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-primary mb-1">
          <NuxtLink to="/dashboard" class="hover:underline">Dashboard</NuxtLink>
          <span class="text-muted-foreground/50">/</span>
          <NuxtLink to="/dashboard/machines" class="hover:underline">Machines</NuxtLink>
          <span class="text-muted-foreground/50">/</span>
          <span class="text-muted-foreground">Machine Groups & Topology</span>
        </div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-3">
          <div class="p-2.5 rounded-2xl bg-primary/10 text-primary border border-primary/20 shadow-xs">
            <FolderTree class="h-6 w-6" />
          </div>
          <span>Machine Groupings & Production Topology</span>
        </h1>
        <p class="text-sm text-muted-foreground mt-1 max-w-3xl">
          Hierarchical production lines with customer interface contracts, dynamic technological groupings by domain keys, and operational clusters.
        </p>
      </div>

      <div class="flex items-center gap-2.5">
        <NuxtLink to="/dashboard/delegations">
          <Button variant="outline" size="sm" class="border-border bg-card hover:bg-muted text-foreground rounded-xl text-xs font-bold cursor-pointer">
            <Users class="w-3.5 h-3.5 mr-1.5 text-violet-500" />
            Delegations Matrix
          </Button>
        </NuxtLink>

        <Button
          v-if="activeTab === 'clusters'"
          size="sm"
          class="bg-primary text-primary-foreground rounded-xl text-xs font-bold cursor-pointer shadow-sm"
          @click="isCreateClusterModalOpen = true"
        >
          <Plus class="w-3.5 h-3.5 mr-1.5" />
          New Operational Cluster
        </Button>
      </div>
    </div>

    <!-- Navigation Tabs & Search Bar -->
    <div class="flex flex-col sm:flex-row items-center justify-between gap-4">
      <div class="flex items-center gap-1.5 p-1 bg-muted/30 border border-border rounded-2xl w-full sm:w-auto">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          type="button"
          @click="activeTab = tab.id"
          :class="[
            'flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer flex-1 sm:flex-initial justify-center',
            activeTab === tab.id
              ? 'bg-card text-foreground shadow-xs border border-border/80'
              : 'text-muted-foreground hover:text-foreground hover:bg-card/40'
          ]"
        >
          <component :is="tab.icon" class="w-4 h-4" />
          <span>{{ tab.label }}</span>
          <span
            v-if="tab.id === 'lines'"
            class="ml-1 text-[10px] px-1.5 py-0.2 rounded-full bg-blue-500/15 text-blue-600 dark:text-blue-400 font-mono"
          >
            {{ productionLines.length }}
          </span>
          <span
            v-else-if="tab.id === 'technologies'"
            class="ml-1 text-[10px] px-1.5 py-0.2 rounded-full bg-indigo-500/15 text-indigo-600 dark:text-indigo-400 font-mono"
          >
            {{ technologicalGroups.length }}
          </span>
          <span
            v-else-if="tab.id === 'clusters'"
            class="ml-1 text-[10px] px-1.5 py-0.2 rounded-full bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 font-mono"
          >
            {{ operationalClusters.length }}
          </span>
        </button>
      </div>

      <div class="relative w-full sm:w-72">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
        <Input
          v-model="searchQuery"
          placeholder="Filter groups and stations..."
          class="pl-9 h-9 text-xs rounded-xl bg-card border-border"
        />
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- TAB 1: PRODUCTION LINES & CUSTOMER CONTRACTS                            -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'lines'" class="space-y-6">
      <div v-if="filteredLines.length === 0" class="text-center py-16 bg-muted/10 border border-dashed border-border rounded-2xl">
        <Building2 class="size-10 text-muted-foreground mx-auto mb-2 opacity-50" />
        <h3 class="text-sm font-bold text-foreground">No Production Lines Found</h3>
        <p class="text-xs text-muted-foreground mt-1">Assign machines to line groups to establish customer interface contracts.</p>
      </div>

      <div v-for="line in filteredLines" :key="line.name" class="bg-card border border-border rounded-2xl p-5 shadow-xs space-y-4">
        <!-- Line Header & Interface Contract Specification -->
        <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-3 border-b border-border/60 pb-4">
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-lg font-black text-foreground">{{ line.name }}</h2>
              <Badge variant="outline" class="font-mono text-[10px] font-bold bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/30">
                <FileCode class="size-3 mr-1" />
                Contract: {{ line.interfaceContract }}
              </Badge>
            </div>
            <p class="text-xs text-muted-foreground mt-1">
              Customer assembly line interface conforming to OEM production specification and interlock rules.
            </p>
          </div>

          <!-- Key Contract Metrics -->
          <div class="flex items-center gap-3 text-xs font-mono">
            <div class="px-3 py-1.5 bg-muted/30 border border-border rounded-xl">
              <span class="text-[10px] text-muted-foreground block uppercase font-sans font-bold">Target Cycle Time</span>
              <span class="font-bold text-foreground">{{ line.targetCycleTimeSec }}s / part</span>
            </div>
            <div class="px-3 py-1.5 bg-muted/30 border border-border rounded-xl">
              <span class="text-[10px] text-muted-foreground block uppercase font-sans font-bold">Buffer Capacity</span>
              <span class="font-bold text-foreground">{{ line.bufferCapacity }} pallets</span>
            </div>
            <div class="px-3 py-1.5 bg-muted/30 border border-border rounded-xl">
              <span class="text-[10px] text-muted-foreground block uppercase font-sans font-bold">Safety Zone</span>
              <span class="font-bold text-emerald-600 dark:text-emerald-400 font-sans text-xs">{{ line.safetyZone }}</span>
            </div>
          </div>
        </div>

        <!-- Sequential Station Layout -->
        <div class="space-y-2">
          <div class="text-[11px] font-bold text-muted-foreground uppercase tracking-widest flex items-center justify-between">
            <span>Sequential Station Layout ({{ line.stations.length }} stations)</span>
            <span class="text-[10px] font-normal lowercase font-sans">material flow: left to right</span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-3">
            <div
              v-for="st in line.stations"
              :key="st.id"
              class="p-3 bg-muted/20 border border-border/80 rounded-xl hover:border-primary/40 transition-all flex flex-col justify-between"
            >
              <div class="space-y-2">
                <div class="flex items-start justify-between gap-2">
                  <div class="flex items-center gap-2">
                    <MachinePictogram :machine-type="st.machineType" class="size-6 text-primary" />
                    <div>
                      <div class="font-bold text-foreground text-xs leading-tight">{{ st.name }}</div>
                      <div class="font-mono text-[10px] text-muted-foreground">{{ st.customIdentifier || st.id.substring(0, 8) }}</div>
                    </div>
                  </div>
                  <Badge variant="outline" :class="['text-[9px] font-bold', getStatusBadge(st.status).class]">
                    {{ getStatusBadge(st.status).label }}
                  </Badge>
                </div>

                <div class="text-[11px] text-muted-foreground line-clamp-1">
                  {{ st.displayName || st.name }}
                </div>

                <div v-if="st.technologies && st.technologies.length > 0" class="flex flex-wrap gap-1">
                  <Badge
                    v-for="t in st.technologies"
                    :key="t"
                    variant="outline"
                    class="text-[9px] font-mono bg-card border-border text-muted-foreground"
                  >
                    {{ t }}
                  </Badge>
                </div>
              </div>

              <div class="pt-2 mt-2 border-t border-border/50 flex justify-end">
                <NuxtLink :to="`/dashboard/machines/${st.id}`">
                  <Button variant="ghost" size="sm" class="h-6 text-[10px] px-2 text-primary font-bold cursor-pointer">
                    Inspect Quadrants
                    <ExternalLink class="size-3 ml-1" />
                  </Button>
                </NuxtLink>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- TAB 2: DYNAMIC TECHNOLOGICAL GROUPS                                     -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <div v-else-if="activeTab === 'technologies'" class="space-y-6">
      <div v-if="filteredTechnologies.length === 0" class="text-center py-16 bg-muted/10 border border-dashed border-border rounded-2xl">
        <Cpu class="size-10 text-muted-foreground mx-auto mb-2 opacity-50" />
        <h3 class="text-sm font-bold text-foreground">No Technological Groups Found</h3>
        <p class="text-xs text-muted-foreground mt-1">Technological groupings are dynamically discovered from machine domain capabilities.</p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div
          v-for="tg in filteredTechnologies"
          :key="tg.technologyKey"
          class="bg-card border border-border rounded-2xl p-5 shadow-xs space-y-4 hover:border-indigo-500/40 transition-all"
        >
          <div class="flex items-start justify-between gap-3">
            <div>
              <div class="flex items-center gap-2">
                <Badge variant="outline" class="text-[10px] font-mono font-bold uppercase bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border-indigo-500/30">
                  <KeyRound class="size-2.5 mr-1" />
                  Tech Key: {{ tg.technologyKey }}
                </Badge>
                <Badge variant="outline" class="text-[10px] font-semibold bg-muted/40 border-border text-foreground">
                  {{ tg.discipline }} Discipline
                </Badge>
              </div>
              <h3 class="text-lg font-black text-foreground mt-1">{{ tg.technologyKey }} Technological Cluster</h3>
            </div>
            <div class="text-right">
              <span class="text-xs font-mono font-bold text-indigo-600 dark:text-indigo-400 block">{{ tg.machines.length }}</span>
              <span class="text-[10px] text-muted-foreground">Machines</span>
            </div>
          </div>

          <p class="text-xs text-muted-foreground leading-relaxed">
            {{ tg.description }}
          </p>

          <!-- Required Gating Qualification -->
          <div class="p-3 bg-muted/20 border border-border/80 rounded-xl flex items-center justify-between text-xs">
            <span class="text-muted-foreground">Required Gating Skill:</span>
            <Badge variant="outline" class="font-mono text-[10px] font-bold bg-amber-500/10 text-amber-700 dark:text-amber-400 border-amber-500/30">
              {{ tg.requiredCert }}
            </Badge>
          </div>

          <!-- Associated Machines -->
          <div class="space-y-1.5">
            <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest block">Classified Stations:</span>
            <div class="grid grid-cols-2 gap-2">
              <NuxtLink
                v-for="m in tg.machines"
                :key="m.id"
                :to="`/dashboard/machines/${m.id}`"
                class="p-2 bg-muted/30 border border-border/60 rounded-lg hover:border-primary/50 transition-colors flex items-center justify-between text-xs"
              >
                <div class="truncate mr-2">
                  <span class="font-bold text-foreground block truncate">{{ m.name }}</span>
                  <span class="text-[10px] text-muted-foreground font-mono truncate block">{{ m.groupId || 'Line 1' }}</span>
                </div>
                <Badge variant="outline" :class="['text-[8px] font-bold', getStatusBadge(m.status).class]">
                  {{ getStatusBadge(m.status).label }}
                </Badge>
              </NuxtLink>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- TAB 3: OPERATIONAL CLUSTERS (MANUAL)                                    -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <div v-else-if="activeTab === 'clusters'" class="space-y-6">
      <div v-if="filteredClusters.length === 0" class="text-center py-16 bg-muted/10 border border-dashed border-border rounded-2xl">
        <FolderTree class="size-10 text-muted-foreground mx-auto mb-2 opacity-50" />
        <h3 class="text-sm font-bold text-foreground">No Operational Clusters Configured</h3>
        <p class="text-xs text-muted-foreground mt-1">Create operational clusters to organize physical work cells and assign dedicated engineers.</p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <div
          v-for="c in filteredClusters"
          :key="c.id"
          class="bg-card border border-border rounded-2xl p-5 shadow-xs space-y-4 hover:border-primary/40 transition-all flex flex-col justify-between"
        >
          <div class="space-y-3">
            <div class="flex items-start justify-between gap-3">
              <div>
                <Badge variant="outline" class="text-[10px] font-bold uppercase tracking-wider bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30">
                  Operational Cluster
                </Badge>
                <h3 class="text-base font-black text-foreground mt-1 leading-snug">{{ c.name }}</h3>
              </div>
              <Button
                variant="ghost"
                size="sm"
                class="size-8 p-0 text-muted-foreground hover:text-rose-600 hover:bg-rose-500/10 rounded-lg cursor-pointer"
                @click="handleDeleteCluster(c.id)"
              >
                <Trash2 class="size-4" />
              </Button>
            </div>

            <p class="text-xs text-muted-foreground leading-relaxed">
              {{ c.description || 'Custom factory operational boundary for maintenance and shift tracking.' }}
            </p>

            <div class="p-3 bg-muted/20 border border-border/80 rounded-xl space-y-1.5 text-xs">
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Lead Engineer:</span>
                <span class="font-bold text-foreground">{{ c.leadEngineerName || 'Unassigned Lead' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Enclosed Machines:</span>
                <span class="font-mono font-bold text-foreground">{{ c.machineIds?.length || 0 }} Stations</span>
              </div>
            </div>

            <div v-if="c.machineIds && c.machineIds.length > 0" class="space-y-1 pt-1">
              <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest block">Assigned Machine IDs:</span>
              <div class="flex flex-wrap gap-1">
                <Badge
                  v-for="mId in c.machineIds"
                  :key="mId"
                  variant="outline"
                  class="text-[9px] font-mono bg-card border-border text-foreground"
                >
                  {{ mId }}
                </Badge>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- MODAL: CREATE OPERATIONAL CLUSTER                                       -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <Dialog v-model:open="isCreateClusterModalOpen">
      <DialogContent class="sm:max-w-md max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-base font-black">
            <FolderTree class="size-5 text-primary" />
            <span>Create Operational Cluster</span>
          </DialogTitle>
        </DialogHeader>

        <form @submit.prevent="handleCreateCluster" class="space-y-4 pt-2 text-xs">
          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Cluster Name</label>
            <Input v-model="newCluster.name" required placeholder="e.g. Cleanroom Cell A - Fastening" class="h-9" />
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Description & Scope</label>
            <Input v-model="newCluster.description" placeholder="Operational purpose and cell boundaries" class="h-9" />
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Lead Engineer</label>
            <select v-model="newCluster.leadEngineerId" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
              <option value="">-- Select Dedicated Engineer --</option>
              <option v-for="u in DEMO_PERSONAS" :key="u.id" :value="u.id">{{ u.name }}</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Assign Machines (Multi-Select)</label>
            <div class="grid grid-cols-1 gap-1.5 p-3 bg-muted/20 border border-border rounded-xl max-h-40 overflow-y-auto">
              <label
                v-for="m in machines"
                :key="m.id"
                class="flex items-center gap-2 p-1 rounded hover:bg-card cursor-pointer"
              >
                <input
                  type="checkbox"
                  :value="m.id"
                  v-model="newCluster.assignedMachineIds"
                  class="rounded"
                />
                <span class="font-medium text-foreground text-xs">{{ m.name }} ({{ m.customIdentifier || m.machineType }})</span>
              </label>
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-border">
            <Button type="button" variant="outline" size="sm" @click="isCreateClusterModalOpen = false">Cancel</Button>
            <Button type="submit" size="sm" class="bg-primary text-primary-foreground font-bold">Create Cluster</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  </div>
</template>
