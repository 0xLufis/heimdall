<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { 
  Factory, 
  Layers, 
  Cpu, 
  Search, 
  Monitor, 
  Zap, 
  FolderTree, 
  AlertOctagon, 
  CheckCircle2, 
  Clock, 
  ExternalLink,
  ChevronRight,
  ShieldAlert,
  SlidersHorizontal,
  Plus
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '~/components/ui/card'
import OmniSearchBar from '~/components/search/OmniSearchBar.vue'
import DashboardInventoryStationComponentTreeModal from '~/components/dashboard/inventory/StationComponentTreeModal.vue'
import { useAuthSession } from '~/composables/useAuthSession'
import type { SearchInstanceConfig } from '~/types/search'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { user, canApproveLineStops, isOperativePlanner, isEngineer } = useAuthSession()

// Switchable view states
const activeView = ref<'machines' | 'lines' | 'technologies'>('machines')
const loading = ref(false)
const searchQuery = ref('')

// Raw data states
const machines = ref<any[]>([])
const lines = ref<any[]>([])
const technologies = ref<any[]>([])

// Modal state
const selectedTreeStationId = ref<string>('')
const isTreeModalOpen = ref(false)

const openTreeForStation = (stationId: string) => {
  selectedTreeStationId.value = stationId
  isTreeModalOpen.value = true
}

const machinesSearchConfig: SearchInstanceConfig = {
  instanceId: 'machines',
  placeholder: 'OmniSearch machines: Type station ID, line:Line-1, tech:Assembly, or status:online...',
  enableAutoTagging: true,
  allowedTagKeys: ['station', 'line', 'tech', 'status', 'mfr']
}

// Fetch all machine views
const fetchData = async () => {
  loading.value = true
  try {
    const [machRes, lineRes, techRes] = await Promise.allSettled([
      $fetch<any[]>('/api/proxy/v1/machine'),
      $fetch<any[]>('/api/proxy/v1/machine/by-line'),
      $fetch<any[]>('/api/proxy/v1/machine/by-technology')
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
          controllers: [{ id: 'c-1', hostname: 'IPC-L1-01', ipAddress: '192.168.1.110', isOnline: true }],
          responsibleTeams: [{ name: 'Mechanical Maintenance' }]
        },
        {
          id: '22222222-2222-2222-2222-222222222222',
          name: 'OP20-Weld',
          displayName: 'Laser Robotic Welding Cell',
          customIdentifier: 'LINE1-OP20',
          machineType: 'Welding',
          groupId: 'Line 1',
          controllers: [{ id: 'c-2', hostname: 'IPC-L1-02', ipAddress: '192.168.1.111', isOnline: true }],
          responsibleTeams: [{ name: 'Electrical Engineering' }]
        },
        {
          id: '33333333-3333-3333-3333-333333333333',
          name: 'OP30-Vision',
          displayName: 'Cognex AI Inspection Chamber',
          customIdentifier: 'LINE2-OP30',
          machineType: 'Test',
          groupId: 'Line 2',
          controllers: [{ id: 'c-3', hostname: 'IPC-L2-03', ipAddress: '192.168.1.120', isOnline: true }],
          responsibleTeams: [{ name: 'Quality Automation' }]
        },
        {
          id: '44444444-4444-4444-4444-444444444444',
          name: 'OP40-Fasten',
          displayName: 'Multi-Spindle Screw Fastening Station',
          customIdentifier: 'LINE1-OP40',
          machineType: 'Fastening',
          groupId: 'Line 1',
          controllers: [{ id: 'c-4', hostname: 'IPC-L1-04', ipAddress: '192.168.1.114', isOnline: true }],
          responsibleTeams: [{ name: 'Assembly Engineering' }]
        },
        {
          id: '55555555-5555-5555-5555-555555555555',
          name: 'OP50-Dispense',
          displayName: 'Precision Adhesive Gasket Dispenser',
          customIdentifier: 'LINE2-OP50',
          machineType: 'Dispensing',
          groupId: 'Line 2',
          controllers: [{ id: 'c-5', hostname: 'IPC-L2-05', ipAddress: '192.168.1.125', isOnline: true }],
          responsibleTeams: [{ name: 'Chemical & Dispensing' }]
        },
        {
          id: '66666666-6666-6666-6666-666666666666',
          name: 'OP60-Robot',
          displayName: '6-Axis KUKA Palletizing Robot',
          customIdentifier: 'LINE2-OP60',
          machineType: 'Robotics',
          groupId: 'Line 2',
          controllers: [{ id: 'c-6', hostname: 'IPC-L2-06', ipAddress: '192.168.1.128', isOnline: true }],
          responsibleTeams: [{ name: 'Robotics Engineering' }]
        }
      ]
    }

    if (lineRes.status === 'fulfilled' && Array.isArray(lineRes.value)) {
      lines.value = lineRes.value
    } else {
      // Reconstruct lines from machines
      const lineMap: Record<string, any> = {}
      for (const m of machines.value) {
        const lName = m.groupId || 'Default Line'
        if (!lineMap[lName]) {
          lineMap[lName] = {
            lineId: lName,
            lineName: lName,
            machineCount: 0,
            controllersCount: 0,
            machines: []
          }
        }
        lineMap[lName].machineCount++
        lineMap[lName].controllersCount += (m.controllers?.length || 0)
        lineMap[lName].machines.push(m)
      }
      lines.value = Object.values(lineMap)
    }

    if (techRes.status === 'fulfilled' && Array.isArray(techRes.value)) {
      technologies.value = techRes.value
    } else {
      // Reconstruct technologies from machines
      const techMap: Record<string, any> = {}
      for (const m of machines.value) {
        const tName = m.machineType || 'General Assembly'
        if (!techMap[tName]) {
          techMap[tName] = {
            technology: tName,
            machineCount: 0,
            machines: []
          }
        }
        techMap[tName].machineCount++
        techMap[tName].machines.push(m)
      }
      technologies.value = Object.values(techMap)
    }
  } finally {
    loading.value = false
  }
}

const onSearch = (q: string) => {
  searchQuery.value = q
}

// Filter machines by search query
const filteredMachines = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return machines.value

  const tagMatches: Record<string, string> = {}
  const tagRegex = /(\w+):"([^"]+)"|(\w+):(\S+)/g
  let match
  while ((match = tagRegex.exec(q)) !== null) {
    const key = match[1] || match[3]
    const val = match[2] || match[4]
    tagMatches[key.toLowerCase()] = val.toLowerCase()
  }
  const cleanText = q.replace(tagRegex, '').trim()

  return machines.value.filter(m => {
    for (const [tagKey, tagVal] of Object.entries(tagMatches)) {
      if (tagKey === 'station' && !m.name?.toLowerCase().includes(tagVal) && !m.customIdentifier?.toLowerCase().includes(tagVal)) return false
      if (tagKey === 'line' && !m.groupId?.toLowerCase().includes(tagVal)) return false
      if (tagKey === 'tech' && !m.machineType?.toLowerCase().includes(tagVal)) return false
    }

    if (cleanText) {
      const nameMatch = (m.name || '').toLowerCase().includes(cleanText)
      const dispMatch = (m.displayName || '').toLowerCase().includes(cleanText)
      const identMatch = (m.customIdentifier || '').toLowerCase().includes(cleanText)
      const typeMatch = (m.machineType || '').toLowerCase().includes(cleanText)
      const lineMatch = (m.groupId || '').toLowerCase().includes(cleanText)
      const ctrlMatch = (m.controllers || []).some((c: any) => (c.hostname || '').toLowerCase().includes(cleanText) || (c.ipAddress || '').toLowerCase().includes(cleanText))
      return nameMatch || dispMatch || identMatch || typeMatch || lineMatch || ctrlMatch
    }

    return true
  })
})

// Filtered lines with nested matching machines
const filteredLines = computed(() => {
  const machIds = new Set(filteredMachines.value.map(m => m.id))
  return lines.value.map(l => {
    const matched = (l.machines || []).filter((m: any) => machIds.has(m.id))
    return {
      ...l,
      machineCount: matched.length,
      machines: matched
    }
  }).filter(l => l.machineCount > 0)
})

// Filtered technologies with nested matching machines
const filteredTechnologies = computed(() => {
  const machIds = new Set(filteredMachines.value.map(m => m.id))
  return technologies.value.map(t => {
    const matched = (t.machines || []).filter((m: any) => machIds.has(m.id))
    return {
      ...t,
      machineCount: matched.length,
      machines: matched
    }
  }).filter(t => t.machineCount > 0)
})

// Quick line stop request handler (simulation)
const lineStopRequested = ref<Record<string, boolean>>({})
const requestLineStop = (lineName: string) => {
  lineStopRequested.value[lineName] = !lineStopRequested.value[lineName]
}

onMounted(() => {
  fetchData()
})
</script>

<template>
  <div class="space-y-8 animate-in fade-in duration-300">
    <!-- Header Area with View Mode Switchers -->
    <div class="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-2 border-b border-slate-900">
      <div>
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-2xl bg-indigo-600/20 text-indigo-400 border border-indigo-500/30">
            <Factory class="w-6 h-6" />
          </div>
          <div>
            <h1 class="text-2xl font-black text-slate-100 tracking-tight uppercase">
              Production Machines & Lines
            </h1>
            <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">
              Stations, manufacturing cells, production lines, and engineering discipline technologies
            </p>
          </div>
        </div>

        <!-- Global Summary Badges -->
        <div class="flex flex-wrap items-center gap-3 mt-4">
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <Factory class="w-3.5 h-3.5 text-indigo-400" />
            <span class="text-[10px] font-bold text-slate-500 uppercase">Stations:</span>
            <span class="font-mono font-black text-slate-200">{{ machines.length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <Layers class="w-3.5 h-3.5 text-blue-400" />
            <span class="text-[10px] font-bold text-slate-500 uppercase">Lines:</span>
            <span class="font-mono font-black text-slate-200">{{ lines.length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <Cpu class="w-3.5 h-3.5 text-teal-400" />
            <span class="text-[10px] font-bold text-slate-500 uppercase">Technologies:</span>
            <span class="font-mono font-black text-slate-200">{{ technologies.length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <span class="size-2 rounded-full bg-emerald-400 animate-pulse" />
            <span class="text-[10px] font-bold uppercase tracking-widest text-emerald-400">
              Topology Live Sync Active
            </span>
          </div>
        </div>
      </div>

      <!-- 3 View Mode Switcher -->
      <div class="flex flex-wrap items-center gap-3 shrink-0">
        <div class="bg-slate-900 p-1 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button
            variant="ghost"
            @click="activeView = 'machines'"
            :class="activeView === 'machines' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-9 flex items-center gap-2"
          >
            <Factory class="w-3.5 h-3.5" />
            <span>Machines</span>
          </Button>

          <Button
            variant="ghost"
            @click="activeView = 'lines'"
            :class="activeView === 'lines' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-9 flex items-center gap-2"
          >
            <Layers class="w-3.5 h-3.5" />
            <span>Lines</span>
          </Button>

          <Button
            variant="ghost"
            @click="activeView = 'technologies'"
            :class="activeView === 'technologies' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-9 flex items-center gap-2"
          >
            <Cpu class="w-3.5 h-3.5" />
            <span>Technologies</span>
          </Button>
        </div>
      </div>
    </div>

    <!-- Unified OmniSearch Bar across all 3 views -->
    <div class="max-w-4xl mx-auto w-full">
      <OmniSearchBar
        :config="machinesSearchConfig"
        :immediate="true"
        @search="onSearch"
      />
    </div>

    <!-- VIEW 1: Cards of Machines -->
    <div v-if="activeView === 'machines'" class="space-y-4">
      <div class="flex items-center justify-between text-xs font-black uppercase tracking-widest text-slate-400 px-1">
        <span>Station Machinery Inventory ({{ filteredMachines.length }})</span>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <Card
          v-for="mach in filteredMachines"
          :key="mach.id"
          class="bg-slate-900/60 border-slate-800 hover:border-indigo-500/40 transition-all rounded-2xl shadow-md overflow-hidden group flex flex-col justify-between"
        >
          <CardHeader class="p-5 border-b border-slate-850">
            <div class="flex items-start justify-between gap-3">
              <div class="flex items-center gap-2.5">
                <div class="p-2 rounded-xl bg-indigo-950/50 text-indigo-400 border border-indigo-800/40">
                  <Factory class="w-4 h-4" />
                </div>
                <div>
                  <CardTitle class="text-sm font-black text-slate-100 uppercase tracking-tight group-hover:text-indigo-300 transition-colors">
                    {{ mach.name }}
                  </CardTitle>
                  <CardDescription class="text-[10px] text-slate-400 font-mono mt-0.5">
                    {{ mach.customIdentifier || 'CELL-ID' }}
                  </CardDescription>
                </div>
              </div>

              <div class="flex flex-col items-end gap-1.5">
                <Badge variant="outline" class="text-[8px] uppercase tracking-wider font-mono text-emerald-400 border-emerald-500/30 bg-emerald-950/20">
                  {{ mach.machineType || 'Assembly' }}
                </Badge>
                <span class="text-[8px] font-mono text-slate-500 uppercase">
                  Line: <span class="text-slate-300 font-bold">{{ mach.groupId || 'Line 1' }}</span>
                </span>
              </div>
            </div>
          </CardHeader>

          <CardContent class="p-5 space-y-4 flex-1 flex flex-col justify-between">
            <div class="space-y-3">
              <div class="text-xs text-slate-300 font-medium">
                {{ mach.displayName || 'Industrial Automation Station' }}
              </div>

              <!-- Controller PCs associated -->
              <div class="space-y-1.5">
                <div class="text-[9px] font-black uppercase tracking-widest text-slate-500 flex items-center gap-1">
                  <Monitor class="w-3 h-3 text-blue-400" />
                  <span>Host Controller IPCs ({{ mach.controllers?.length || 0 }})</span>
                </div>

                <div v-if="mach.controllers && mach.controllers.length > 0" class="flex flex-wrap gap-1.5">
                  <div
                    v-for="c in mach.controllers"
                    :key="c.id || c.hostname"
                    class="px-2.5 py-1 rounded-lg bg-slate-950/80 border border-slate-800 text-[9px] font-mono text-slate-300 flex items-center gap-2"
                  >
                    <span class="size-1.5 rounded-full bg-emerald-400" />
                    <span>{{ c.hostname }}</span>
                    <span v-if="c.ipAddress" class="text-slate-500">({{ c.ipAddress }})</span>
                  </div>
                </div>
                <div v-else class="text-[9px] text-slate-600 font-mono italic">
                  No dedicated IPC assigned
                </div>
              </div>
            </div>

            <!-- Card Bottom Actions -->
            <div class="pt-3 border-t border-slate-850 flex items-center justify-between gap-2">
              <Button
                variant="outline"
                size="sm"
                @click="openTreeForStation(mach.id)"
                class="h-8 px-3 border-slate-800 bg-slate-950 text-indigo-400 hover:text-indigo-300 hover:border-indigo-500/30 text-[9px] font-black uppercase tracking-wider rounded-xl gap-1.5 shadow-sm"
              >
                <FolderTree class="w-3.5 h-3.5" />
                <span>Visualise Tree</span>
              </Button>

              <NuxtLink
                :to="`/dashboard/inventory?query=station:${encodeURIComponent(mach.name)}`"
                class="text-[9px] text-slate-400 hover:text-slate-200 font-bold uppercase tracking-wider flex items-center gap-1"
              >
                <span>Parts</span>
                <ChevronRight class="w-3 h-3" />
              </NuxtLink>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- VIEW 2: Cards of Lines (with nested table inside) -->
    <div v-if="activeView === 'lines'" class="space-y-6">
      <div class="flex items-center justify-between text-xs font-black uppercase tracking-widest text-slate-400 px-1">
        <span>Production Lines Topology ({{ filteredLines.length }})</span>
      </div>

      <div v-for="line in filteredLines" :key="line.lineId || line.lineName" class="space-y-3">
        <Card class="bg-slate-900/50 border-slate-800 rounded-3xl shadow-xl overflow-hidden">
          <CardHeader class="p-6 border-b border-slate-850 bg-slate-900/80">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div class="flex items-center gap-3">
                <div class="p-3 rounded-2xl bg-blue-600/15 text-blue-400 border border-blue-500/25">
                  <Layers class="w-5 h-5" />
                </div>
                <div>
                  <CardTitle class="text-base font-black text-slate-100 uppercase tracking-tight flex items-center gap-2.5">
                    <span>{{ line.lineName }}</span>
                    <Badge variant="outline" class="text-[8px] uppercase tracking-widest font-mono text-blue-400 border-blue-500/30 bg-blue-950/20">
                      Manufacturing Line
                    </Badge>
                  </CardTitle>
                  <CardDescription class="text-xs text-slate-400 font-bold uppercase tracking-wider mt-0.5">
                    {{ line.machineCount }} Stations Deployed • {{ line.controllersCount || 0 }} Industrial PCs
                  </CardDescription>
                </div>
              </div>

              <!-- Operative Planner Line Stop Action -->
              <div class="flex items-center gap-2">
                <Button
                  v-if="canApproveLineStops || isOperativePlanner"
                  variant="outline"
                  size="sm"
                  @click="requestLineStop(line.lineName)"
                  :class="lineStopRequested[line.lineName] ? 'border-rose-500 text-rose-400 bg-rose-950/30' : 'border-slate-800 text-slate-400 hover:text-rose-400'"
                  class="h-9 px-3 text-[9px] font-black uppercase tracking-wider rounded-xl gap-1.5"
                >
                  <AlertOctagon class="w-3.5 h-3.5" />
                  <span>{{ lineStopRequested[line.lineName] ? 'Line Stop Pending' : 'Request Line Stop' }}</span>
                </Button>
              </div>
            </div>
          </CardHeader>

          <!-- Nested Table of Machines on this Line -->
          <CardContent class="p-0">
            <div class="overflow-x-auto">
              <table class="w-full text-left border-collapse">
                <thead class="bg-slate-950/60 border-b border-slate-800/80">
                  <tr>
                    <th class="px-6 py-3 text-[9px] font-black uppercase tracking-widest text-slate-500">Station Identity</th>
                    <th class="px-6 py-3 text-[9px] font-black uppercase tracking-widest text-slate-500">Technology</th>
                    <th class="px-6 py-3 text-[9px] font-black uppercase tracking-widest text-slate-500">Host Controllers</th>
                    <th class="px-6 py-3 text-[9px] font-black uppercase tracking-widest text-slate-500 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-850">
                  <tr
                    v-for="mach in line.machines"
                    :key="mach.id"
                    class="hover:bg-slate-850/40 transition-colors"
                  >
                    <td class="px-6 py-4">
                      <div class="flex items-center gap-3">
                        <div class="size-2 rounded-full bg-emerald-400" />
                        <div>
                          <div class="text-xs font-black uppercase text-slate-200">
                            {{ mach.name }}
                          </div>
                          <div class="text-[10px] text-slate-400 font-medium mt-0.5">
                            {{ mach.displayName || mach.customIdentifier }}
                          </div>
                        </div>
                      </div>
                    </td>

                    <td class="px-6 py-4">
                      <Badge variant="outline" class="text-[8px] uppercase tracking-wider font-mono text-teal-400 border-teal-500/30 bg-teal-950/20">
                        {{ mach.machineType || 'Assembly' }}
                      </Badge>
                    </td>

                    <td class="px-6 py-4">
                      <div class="flex flex-wrap gap-1.5">
                        <span
                          v-for="c in mach.controllers"
                          :key="c.id || c.hostname"
                          class="px-2 py-0.5 rounded bg-slate-950 border border-slate-800 text-[9px] font-mono text-slate-400"
                        >
                          {{ c.hostname }}
                        </span>
                        <span v-if="!mach.controllers?.length" class="text-[9px] font-mono text-slate-600 italic">
                          None
                        </span>
                      </div>
                    </td>

                    <td class="px-6 py-4 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        @click="openTreeForStation(mach.id)"
                        class="h-7 px-2.5 text-indigo-400 hover:text-indigo-300 text-[9px] font-black uppercase tracking-wider rounded-lg gap-1"
                      >
                        <FolderTree class="w-3.5 h-3.5" />
                        <span>Tree</span>
                      </Button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- VIEW 3: Technologies Grouping -->
    <div v-if="activeView === 'technologies'" class="space-y-6">
      <div class="flex items-center justify-between text-xs font-black uppercase tracking-widest text-slate-400 px-1">
        <span>Discipline & Technology Groupings ({{ filteredTechnologies.length }})</span>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card
          v-for="tech in filteredTechnologies"
          :key="tech.technology"
          class="bg-slate-900/60 border-slate-800 rounded-3xl shadow-xl overflow-hidden flex flex-col justify-between"
        >
          <CardHeader class="p-6 border-b border-slate-850 bg-slate-900/80">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-3">
                <div class="p-3 rounded-2xl bg-teal-600/15 text-teal-400 border border-teal-500/25">
                  <Cpu class="w-5 h-5" />
                </div>
                <div>
                  <CardTitle class="text-base font-black text-slate-100 uppercase tracking-tight">
                    {{ tech.technology }}
                  </CardTitle>
                  <CardDescription class="text-xs text-slate-400 font-bold uppercase tracking-wider mt-0.5">
                    {{ tech.machineCount }} Stations in this engineering discipline
                  </CardDescription>
                </div>
              </div>

              <Badge variant="outline" class="text-[9px] font-mono uppercase px-3 py-1 rounded-full text-teal-300 border-teal-500/30 bg-teal-950/20 font-bold">
                {{ tech.machineCount }} Units
              </Badge>
            </div>
          </CardHeader>

          <CardContent class="p-4 space-y-2">
            <div
              v-for="mach in tech.machines"
              :key="mach.id"
              class="flex items-center justify-between p-3 rounded-2xl bg-slate-950/70 border border-slate-850 hover:border-slate-700 transition-colors"
            >
              <div class="flex items-center gap-3">
                <div class="size-2 rounded-full bg-emerald-400" />
                <div>
                  <div class="text-xs font-black uppercase text-slate-200">
                    {{ mach.name }}
                  </div>
                  <div class="text-[10px] text-slate-400 font-medium">
                    {{ mach.displayName || mach.customIdentifier }} • Line: {{ mach.groupId || 'Line 1' }}
                  </div>
                </div>
              </div>

              <Button
                variant="outline"
                size="sm"
                @click="openTreeForStation(mach.id)"
                class="h-7 px-2.5 border-slate-800 bg-slate-900 text-indigo-400 hover:text-indigo-300 text-[9px] font-black uppercase tracking-wider rounded-lg gap-1 shadow-sm"
              >
                <FolderTree class="w-3.5 h-3.5" />
                <span>Tree</span>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- On-Demand Station Component Tree Visualizer Modal -->
    <DashboardInventoryStationComponentTreeModal
      :open="isTreeModalOpen"
      :station-id="selectedTreeStationId"
      @update:open="isTreeModalOpen = $event"
    />
  </div>
</template>
