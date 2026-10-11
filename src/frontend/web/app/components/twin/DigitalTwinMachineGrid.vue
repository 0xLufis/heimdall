<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Search,
  Filter,
  Grid,
  List,
  Cpu,
  Activity,
  Flame,
  ArrowUpRight,
  X,
  Layers,
  ChevronDown
} from 'lucide-vue-next'
import type { MachineTwinNode } from '~/composables/useDigitalTwin'

const props = defineProps<{
  machines: MachineTwinNode[]
  selectedMachineId: string | null
  searchQuery: string
  selectedLine: string
  selectedStatus: string
  selectedTechnology: string
  selectedCriticality: string
  productionLines: string[]
  technologyDisciplines: string[]
}>()

const emit = defineEmits<{
  (e: 'update:searchQuery', val: string): void
  (e: 'update:selectedLine', val: string): void
  (e: 'update:selectedStatus', val: string): void
  (e: 'update:selectedTechnology', val: string): void
  (e: 'update:selectedCriticality', val: string): void
  (e: 'select-machine', id: string): void
  (e: 'reset-filters'): void
}>()

const viewMode = ref<'grid' | 'table'>('grid')

const hasActiveFilters = computed(() => {
  return (
    props.searchQuery.trim().length > 0 ||
    props.selectedLine !== 'all' ||
    props.selectedStatus !== 'all' ||
    props.selectedTechnology !== 'all' ||
    props.selectedCriticality !== 'all'
  )
})
</script>

<template>
  <div class="bg-[#15181e] rounded-xl p-5 border border-[#232730] shadow-sm flex flex-col gap-4">
    <!-- Header with Search & View Toggle -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <Layers class="w-4 h-4 text-teal-400" />
        <h3 class="text-xs font-bold uppercase tracking-wider text-zinc-200">
          Connected Machine Fleet Matrix
        </h3>
        <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-zinc-800 text-zinc-300 border border-zinc-700">
          {{ machines.length }} Nodes
        </span>
      </div>

      <!-- Search & Controls Bar -->
      <div class="flex items-center gap-2">
        <!-- Search Input -->
        <div class="relative flex-1 md:w-64">
          <Search class="w-3.5 h-3.5 text-zinc-500 absolute left-3 top-1/2 -translate-y-1/2" />
          <input
            :value="searchQuery"
            @input="emit('update:searchQuery', ($event.target as HTMLInputElement).value)"
            type="text"
            placeholder="Search station, CAD, type..."
            class="w-full bg-[#0c0e12] border border-[#232730] rounded-lg pl-9 pr-8 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:outline-hidden focus:border-teal-500 transition-colors"
          />
          <button
            v-if="searchQuery"
            @click="emit('update:searchQuery', '')"
            class="absolute right-2.5 top-1/2 -translate-y-1/2 text-zinc-500 hover:text-zinc-300"
          >
            <X class="w-3.5 h-3.5" />
          </button>
        </div>

        <!-- View Switcher -->
        <div class="flex items-center bg-[#0c0e12] border border-[#232730] rounded-lg p-0.5">
          <button
            @click="viewMode = 'grid'"
            class="p-1 rounded text-xs transition-colors"
            :class="viewMode === 'grid' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'"
            title="Grid View"
          >
            <Grid class="w-3.5 h-3.5" />
          </button>
          <button
            @click="viewMode = 'table'"
            class="p-1 rounded text-xs transition-colors"
            :class="viewMode === 'table' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'"
            title="Table View"
          >
            <List class="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </div>

    <!-- Filter Facets Bar -->
    <div class="flex flex-wrap items-center gap-2 pt-1 border-t border-[#232730]/60 text-xs">
      <span class="text-[10px] font-mono uppercase text-zinc-500 flex items-center gap-1 mr-1">
        <Filter class="w-3 h-3" />
        Filters:
      </span>

      <!-- Production Line Filter -->
      <select
        :value="selectedLine"
        @change="emit('update:selectedLine', ($event.target as HTMLSelectElement).value)"
        class="bg-[#0c0e12] border border-[#232730] rounded-lg px-2.5 py-1 text-xs text-zinc-300 focus:outline-hidden focus:border-teal-500"
      >
        <option value="all">All Lines</option>
        <option v-for="line in productionLines" :key="line" :value="line">{{ line }}</option>
      </select>

      <!-- Operational Status Filter -->
      <select
        :value="selectedStatus"
        @change="emit('update:selectedStatus', ($event.target as HTMLSelectElement).value)"
        class="bg-[#0c0e12] border border-[#232730] rounded-lg px-2.5 py-1 text-xs text-zinc-300 focus:outline-hidden focus:border-teal-500"
      >
        <option value="all">All Statuses</option>
        <option value="active">Running / Active</option>
        <option value="Warning">Warning</option>
        <option value="line-stop">Line Stop Alert</option>
        <option value="maintenance">Maintenance</option>
        <option value="decommissioned">Decommissioned</option>
      </select>

      <!-- Technology Discipline Filter -->
      <select
        :value="selectedTechnology"
        @change="emit('update:selectedTechnology', ($event.target as HTMLSelectElement).value)"
        class="bg-[#0c0e12] border border-[#232730] rounded-lg px-2.5 py-1 text-xs text-zinc-300 focus:outline-hidden focus:border-teal-500"
      >
        <option value="all">All Disciplines</option>
        <option v-for="tech in technologyDisciplines" :key="tech" :value="tech">{{ tech }}</option>
      </select>

      <!-- Criticality Filter -->
      <select
        :value="selectedCriticality"
        @change="emit('update:selectedCriticality', ($event.target as HTMLSelectElement).value)"
        class="bg-[#0c0e12] border border-[#232730] rounded-lg px-2.5 py-1 text-xs text-zinc-300 focus:outline-hidden focus:border-teal-500"
      >
        <option value="all">All Criticalities</option>
        <option value="Tier1_Critical">Tier 1 Critical</option>
        <option value="Tier2_Major">Tier 2 Major</option>
        <option value="Tier3_Standard">Tier 3 Standard</option>
      </select>

      <!-- Reset Button -->
      <button
        v-if="hasActiveFilters"
        @click="emit('reset-filters')"
        class="px-2 py-1 rounded text-[11px] font-mono text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors flex items-center gap-1"
      >
        <X class="w-3 h-3" />
        <span>Reset</span>
      </button>
    </div>

    <!-- Empty State -->
    <div v-if="machines.length === 0" class="py-12 text-center text-xs text-zinc-500 font-mono">
      No machines matched the selected search & filter criteria.
    </div>

    <!-- 1. GRID VIEW -->
    <div v-else-if="viewMode === 'grid'" class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-3">
      <div
        v-for="m in machines"
        :key="m.id"
        @click="emit('select-machine', m.id)"
        class="bg-[#0c0e12] rounded-xl p-3.5 border transition-all cursor-pointer hover:-translate-y-0.5 hover:shadow-lg group flex flex-col justify-between"
        :class="m.id === selectedMachineId ? 'border-teal-400 ring-1 ring-teal-400 shadow-[0_0_12px_rgba(45,212,191,0.2)]' : 'border-[#232730] hover:border-zinc-700'"
      >
        <div>
          <!-- Header -->
          <div class="flex items-center justify-between mb-2">
            <span class="text-xs font-mono font-bold text-zinc-200 group-hover:text-teal-300 transition-colors">
              {{ m.customIdentifier }}
            </span>
            <span
              class="px-1.5 py-0.5 rounded text-[9px] font-mono font-bold uppercase border"
              :class="
                m.status === 'active' ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30' :
                m.status === 'maintenance' ? 'bg-sky-500/10 text-sky-400 border-sky-500/30' :
                m.status === 'decommissioned' ? 'bg-zinc-800 text-zinc-400 border-zinc-700' :
                'bg-amber-500/10 text-amber-400 border-amber-500/30'
              "
            >
              {{ m.status }}
            </span>
          </div>

          <div class="text-xs font-medium text-zinc-300 truncate mb-1">
            {{ m.displayName || m.name }}
          </div>
          <div class="text-[10px] font-mono text-zinc-500 mb-3">
            {{ m.groupId }} • {{ m.machineType }}
          </div>
        </div>

        <!-- Telemetry Gauges Ribbon -->
        <div>
          <div class="grid grid-cols-2 gap-2 p-2 bg-[#15181e] rounded-lg border border-[#232730] text-[10px] font-mono mb-3">
            <div>
              <span class="text-zinc-500 block uppercase">Vibration</span>
              <span class="font-bold text-teal-400">{{ m.telemetry?.vibrationRms }} mm/s</span>
            </div>
            <div>
              <span class="text-zinc-500 block uppercase">Temp</span>
              <span class="font-bold text-amber-400">{{ m.telemetry?.temperature }} °C</span>
            </div>
          </div>

          <!-- Footer with Controller & Inspect action -->
          <div class="pt-2 border-t border-[#232730] flex items-center justify-between text-[10px] text-zinc-500">
            <span class="font-mono flex items-center gap-1 truncate max-w-[120px]">
              <Cpu class="w-3 h-3 text-zinc-400" />
              {{ m.controllers?.[0]?.hostname || 'IPC-EDGE' }}
            </span>
            <span class="text-teal-400 group-hover:translate-x-0.5 transition-transform flex items-center gap-0.5 font-medium">
              <span>Inspect</span>
              <ArrowUpRight class="w-3 h-3" />
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- 2. TABLE VIEW -->
    <div v-else class="overflow-x-auto border border-[#232730] rounded-xl">
      <table class="w-full text-left text-xs font-mono">
        <thead class="bg-[#0c0e12] border-b border-[#232730] text-zinc-400">
          <tr>
            <th class="p-3">Identifier</th>
            <th class="p-3">Station Name</th>
            <th class="p-3">Production Line</th>
            <th class="p-3">Discipline</th>
            <th class="p-3">Status</th>
            <th class="p-3">Vib (RMS)</th>
            <th class="p-3">Temp</th>
            <th class="p-3">Edge IPC</th>
            <th class="p-3 text-right">Action</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-[#232730] bg-[#15181e]">
          <tr
            v-for="m in machines"
            :key="m.id"
            @click="emit('select-machine', m.id)"
            class="hover:bg-[#1a1e26] transition-colors cursor-pointer"
            :class="m.id === selectedMachineId ? 'bg-teal-950/20' : ''"
          >
            <td class="p-3 font-bold text-zinc-200">{{ m.customIdentifier }}</td>
            <td class="p-3 font-sans text-zinc-300 font-medium">{{ m.displayName || m.name }}</td>
            <td class="p-3 text-zinc-400">{{ m.groupId }}</td>
            <td class="p-3 text-zinc-400">{{ m.machineType }}</td>
            <td class="p-3">
              <span
                class="px-1.5 py-0.5 rounded text-[9px] uppercase border"
                :class="
                  m.status === 'active' ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30' :
                  m.status === 'maintenance' ? 'bg-sky-500/10 text-sky-400 border-sky-500/30' :
                  m.status === 'decommissioned' ? 'bg-zinc-800 text-zinc-400 border-zinc-700' :
                  'bg-amber-500/10 text-amber-400 border-amber-500/30'
                "
              >
                {{ m.status }}
              </span>
            </td>
            <td class="p-3 text-teal-400 font-bold">{{ m.telemetry?.vibrationRms }} mm/s</td>
            <td class="p-3 text-amber-400 font-bold">{{ m.telemetry?.temperature }} °C</td>
            <td class="p-3 text-zinc-400">{{ m.controllers?.[0]?.hostname || 'IPC-EDGE' }}</td>
            <td class="p-3 text-right">
              <button
                @click.stop="emit('select-machine', m.id)"
                class="text-teal-400 hover:text-teal-300 font-medium"
              >
                Inspect &rarr;
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
