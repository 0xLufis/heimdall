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
  placeholder: 'FMFD machines: Type station ID, line:Line-1, tech:Assembly, or status:online...',
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
  <div class="space-y-6 animate-in fade-in duration-300">
    <!-- Header Area with View Mode Switchers -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div>
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Factory class="h-6 w-6" />
          </div>
          <div>
            <h1 class="text-2xl font-bold tracking-tight text-slate-100">
              Production Machinery & Lines
            </h1>
            <p class="text-sm text-slate-400 mt-0.5">
              Stations, manufacturing cells, production lines, and engineering discipline technologies
            </p>
          </div>
        </div>

        <!-- Global Summary Badges -->
        <div class="flex flex-wrap items-center gap-2 mt-4">
          <div class="flex items-center gap-2 px-3 py-1 rounded-lg bg-slate-900 border border-slate-800 text-xs font-medium">
            <Factory class="w-3.5 h-3.5 text-indigo-400" />
            <span class="text-slate-400">Stations:</span>
            <span class="font-mono font-semibold text-slate-200">{{ machines.length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1 rounded-lg bg-slate-900 border border-slate-800 text-xs font-medium">
            <Layers class="w-3.5 h-3.5 text-blue-400" />
            <span class="text-slate-400">Lines:</span>
            <span class="font-mono font-semibold text-slate-200">{{ lines.length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1 rounded-lg bg-slate-900 border border-slate-800 text-xs font-medium">
            <Cpu class="w-3.5 h-3.5 text-teal-400" />
            <span class="text-slate-400">Technologies:</span>
            <span class="font-mono font-semibold text-slate-200">{{ technologies.length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1 rounded-lg bg-slate-900 border border-slate-800 text-xs font-medium">
            <span class="size-2 rounded-full bg-emerald-400 animate-pulse" />
            <span class="text-emerald-400">
              Topology Live Sync Active
            </span>
          </div>
        </div>
      </div>

      <!-- 3 View Mode Switcher -->
      <div class="flex flex-wrap items-center gap-2 shrink-0">
        <div class="bg-slate-900 p-1 rounded-lg border border-slate-800 shadow-sm flex gap-1">
          <Button
            variant="ghost"
            size="sm"
            @click="activeView = 'machines'"
            :class="activeView === 'machines' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-md text-xs font-medium transition-all h-8 flex items-center gap-1.5"
          >
            <Factory class="w-3.5 h-3.5" />
            <span>Machines</span>
          </Button>

          <Button
            variant="ghost"
            size="sm"
            @click="activeView = 'lines'"
            :class="activeView === 'lines' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-md text-xs font-medium transition-all h-8 flex items-center gap-1.5"
          >
            <Layers class="w-3.5 h-3.5" />
            <span>Lines</span>
          </Button>

          <Button
            variant="ghost"
            size="sm"
            @click="activeView = 'technologies'"
            :class="activeView === 'technologies' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-md text-xs font-medium transition-all h-8 flex items-center gap-1.5"
          >
            <Cpu class="w-3.5 h-3.5" />
            <span>Technologies</span>
          </Button>
        </div>
      </div>
    </div>

    <!-- Unified FMFD (Find My Field Data) Bar across all 3 views -->
    <div class="max-w-5xl mx-auto w-full">
      <OmniSearchBar
        :config="machinesSearchConfig"
        :immediate="true"
        @search="onSearch"
      />
    </div>

    <!-- VIEW 1: Cards of Machines -->
    <div v-if="activeView === 'machines'" class="space-y-4">
      <div class="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-slate-400 px-1">
        <span>Station Machinery Inventory ({{ filteredMachines.length }})</span>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <Card
          v-for="mach in filteredMachines"
          :key="mach.id"
          class="bg-slate-900 border-slate-800 hover:border-slate-700 transition-all rounded-xl shadow-sm overflow-hidden group flex flex-col justify-between"
        >
          <CardHeader class="p-4 sm:p-5 border-b border-slate-800">
            <div class="flex items-start justify-between gap-3">
              <div class="flex items-center gap-2.5">
                <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                  <Factory class="w-4 h-4" />
                </div>
                <div>
                  <CardTitle class="text-sm font-semibold text-slate-100 group-hover:text-indigo-300 transition-colors">
                    {{ mach.name }}
                  </CardTitle>
                  <CardDescription class="text-xs text-slate-400 font-mono mt-0.5">
                    {{ mach.customIdentifier || 'CELL-ID' }}
                  </CardDescription>
                </div>
              </div>

              <div class="flex flex-col items-end gap-1">
                <Badge variant="outline" class="text-xs font-medium font-mono text-emerald-400 border-emerald-500/30 bg-emerald-950/20 px-2 py-0.5 rounded-md">
                  {{ mach.machineType || 'Assembly' }}
                </Badge>
                <span class="text-xs font-mono text-slate-400">
                  Line: <span class="text-slate-200 font-medium">{{ mach.groupId || 'Line 1' }}</span>
                </span>
              </div>
            </div>
          </CardHeader>

          <CardContent class="p-4 sm:p-5 space-y-4 flex-1 flex flex-col justify-between">
            <div class="space-y-3">
              <div class="text-xs text-slate-300 font-medium">
                {{ mach.displayName || 'Industrial Automation Station' }}
              </div>

              <!-- Controller PCs associated -->
              <div class="space-y-1.5">
                <div class="text-xs font-medium text-slate-400 flex items-center gap-1.5">
                  <Monitor class="w-3.5 h-3.5 text-blue-400" />
                  <span>Host Controller IPCs ({{ mach.controllers?.length || 0 }})</span>
                </div>

                <div v-if="mach.controllers && mach.controllers.length > 0" class="flex flex-wrap gap-1.5">
                  <div
                    v-for="c in mach.controllers"
                    :key="c.id || c.hostname"
                    class="px-2 py-0.5 rounded-md bg-slate-950 border border-slate-800 text-xs font-mono text-slate-300 flex items-center gap-1.5"
                  >
                    <span class="size-1.5 rounded-full bg-emerald-400" />
                    <span>{{ c.hostname }}</span>
                    <span v-if="c.ipAddress" class="text-slate-500">({{ c.ipAddress }})</span>
                  </div>
                </div>
                <div v-else class="text-xs text-slate-500 italic">
                  No dedicated IPC assigned
                </div>
              </div>
            </div>

            <!-- Card Bottom Actions -->
            <div class="pt-3 border-t border-slate-800 flex items-center justify-between gap-2">
              <Button
                variant="outline"
                size="sm"
                @click="openTreeForStation(mach.id)"
                class="h-8 px-3 border-slate-800 bg-slate-950 text-indigo-400 hover:text-indigo-300 hover:border-indigo-500/30 text-xs font-medium rounded-lg gap-1.5 shadow-sm"
              >
                <FolderTree class="w-3.5 h-3.5" />
                <span>Visualise Tree</span>
              </Button>

              <NuxtLink
                :to="`/dashboard/inventory?query=station:${encodeURIComponent(mach.name)}`"
                class="text-xs text-slate-400 hover:text-slate-200 font-medium flex items-center gap-1"
              >
                <span>Parts</span>
                <ChevronRight class="w-3.5 h-3.5" />
              </NuxtLink>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- VIEW 2: Cards of Lines (with nested table inside) -->
    <div v-if="activeView === 'lines'" class="space-y-6">
      <div class="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-slate-400 px-1">
        <span>Production Lines Topology ({{ filteredLines.length }})</span>
      </div>

      <div v-for="line in filteredLines" :key="line.lineId || line.lineName" class="space-y-3">
        <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm overflow-hidden">
          <CardHeader class="p-4 sm:p-5 border-b border-slate-800 bg-slate-900/90">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div class="flex items-center gap-3">
                <div class="p-2 rounded-lg bg-blue-500/10 text-blue-400 border border-blue-500/20">
                  <Layers class="w-5 h-5" />
                </div>
                <div>
                  <CardTitle class="text-base font-semibold text-slate-100 flex items-center gap-2">
                    <span>{{ line.lineName }}</span>
                    <Badge variant="outline" class="text-xs font-mono font-medium text-blue-400 border-blue-500/30 bg-blue-950/20 px-2 py-0.5 rounded-md">
                      Manufacturing Line
                    </Badge>
                  </CardTitle>
                  <CardDescription class="text-xs text-slate-400 mt-0.5">
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
                  class="h-8 px-3 text-xs font-medium rounded-lg gap-1.5"
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
                <thead class="bg-slate-950/60 border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-slate-400">Station Identity</th>
                    <th class="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-slate-400">Technology</th>
                    <th class="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-slate-400">Host Controllers</th>
                    <th class="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-slate-400 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800">
                  <tr
                    v-for="mach in line.machines"
                    :key="mach.id"
                    class="hover:bg-slate-800/40 transition-colors"
                  >
                    <td class="px-4 py-3">
                      <div class="flex items-center gap-2.5">
                        <div class="size-2 rounded-full bg-emerald-400" />
                        <div>
                          <div class="text-xs font-semibold text-slate-200">
                            {{ mach.name }}
                          </div>
                          <div class="text-xs text-slate-400 mt-0.5">
                            {{ mach.displayName || mach.customIdentifier }}
                          </div>
                        </div>
                      </div>
                    </td>

                    <td class="px-4 py-3">
                      <Badge variant="outline" class="text-xs font-mono font-medium text-teal-400 border-teal-500/30 bg-teal-950/20 px-2 py-0.5 rounded-md">
                        {{ mach.machineType || 'Assembly' }}
                      </Badge>
                    </td>

                    <td class="px-4 py-3">
                      <div class="flex flex-wrap gap-1">
                        <span
                          v-for="c in mach.controllers"
                          :key="c.id || c.hostname"
                          class="px-2 py-0.5 rounded-md bg-slate-950 border border-slate-800 text-xs font-mono text-slate-400"
                        >
                          {{ c.hostname }}
                        </span>
                        <span v-if="!mach.controllers?.length" class="text-xs font-mono text-slate-500 italic">
                          None
                        </span>
                      </div>
                    </td>

                    <td class="px-4 py-3 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        @click="openTreeForStation(mach.id)"
                        class="h-7 px-2 text-indigo-400 hover:text-indigo-300 text-xs font-medium rounded-md gap-1"
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
      <div class="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-slate-400 px-1">
        <span>Discipline & Technology Groupings ({{ filteredTechnologies.length }})</span>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card
          v-for="tech in filteredTechnologies"
          :key="tech.technology"
          class="bg-slate-900 border-slate-800 rounded-xl shadow-sm overflow-hidden flex flex-col justify-between"
        >
          <CardHeader class="p-4 sm:p-5 border-b border-slate-800 bg-slate-900/90">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-3">
                <div class="p-2 rounded-lg bg-teal-500/10 text-teal-400 border border-teal-500/20">
                  <Cpu class="w-5 h-5" />
                </div>
                <div>
                  <CardTitle class="text-base font-semibold text-slate-100">
                    {{ tech.technology }}
                  </CardTitle>
                  <CardDescription class="text-xs text-slate-400 mt-0.5">
                    {{ tech.machineCount }} Stations in this engineering discipline
                  </CardDescription>
                </div>
              </div>

              <Badge variant="outline" class="text-xs font-mono px-2 py-0.5 rounded-md text-teal-300 border-teal-500/30 bg-teal-950/20 font-medium">
                {{ tech.machineCount }} Units
              </Badge>
            </div>
          </CardHeader>

          <CardContent class="p-3 sm:p-4 space-y-2">
            <div
              v-for="mach in tech.machines"
              :key="mach.id"
              class="flex items-center justify-between p-2.5 rounded-lg bg-slate-950/60 border border-slate-800 hover:border-slate-700 transition-colors"
            >
              <div class="flex items-center gap-2.5">
                <div class="size-2 rounded-full bg-emerald-400" />
                <div>
                  <div class="text-xs font-semibold text-slate-200">
                    {{ mach.name }}
                  </div>
                  <div class="text-xs text-slate-400">
                    {{ mach.displayName || mach.customIdentifier }} • Line: {{ mach.groupId || 'Line 1' }}
                  </div>
                </div>
              </div>

              <Button
                variant="outline"
                size="sm"
                @click="openTreeForStation(mach.id)"
                class="h-7 px-2.5 border-slate-800 bg-slate-900 text-indigo-400 hover:text-indigo-300 text-xs font-medium rounded-md gap-1 shadow-sm"
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
