<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Search, Cpu, Factory, X, Check, ChevronsUpDown } from 'lucide-vue-next'

export interface FoundMachine {
  id: string
  name: string
  displayName?: string
  organizationId?: string
  customIdentifier?: string
  controllers?: Array<{
    id: string
    hostname: string
    name?: string
  }>
}

const props = withDefaults(defineProps<{
  modelValue?: string
  stationName?: string
  placeholder?: string
  disabled?: boolean
}>(), {
  modelValue: '',
  stationName: '',
  placeholder: 'Type machine ID or search from found machines...',
  disabled: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', val: string): void
  (e: 'update:stationName', name: string): void
  (e: 'select', machine: FoundMachine): void
}>()

const isOpen = ref(false)
const highlightedIndex = ref(-1)
const rootRef = ref<HTMLElement | null>(null)
const machines = ref<FoundMachine[]>([])
const loading = ref(false)

// Fallback seed machines in case backend or proxy is unreachable
const fallbackMachines: FoundMachine[] = [
  {
    id: 'm-op10',
    name: 'STATION-OP10-01',
    displayName: 'OP10 Machining Cell',
    organizationId: 'Production Floor A',
    customIdentifier: 'STATION-OP10-01',
    controllers: [{ id: 'ctrl-101', hostname: 'CPC-101' }]
  },
  {
    id: 'm-robot-01',
    name: 'ROBOT-CELL-01',
    displayName: 'Robotic Welding Cell 01',
    organizationId: 'Production Floor A',
    customIdentifier: 'ROBOT-CELL-01',
    controllers: [{ id: 'ctrl-001', hostname: 'CPC-001' }]
  },
  {
    id: 'm-l06',
    name: 'L06-OP150',
    displayName: 'Line 06 - Automated Battery Module Line - Station 150',
    organizationId: 'Production Floor B',
    customIdentifier: 'L06-OP150',
    controllers: [{ id: 'ctrl-081', hostname: 'CPC-081' }]
  },
  {
    id: 'm-l09',
    name: 'L09-OP270',
    displayName: 'Line 09 - Optical Quality Inspection - Station 270',
    organizationId: 'Production Floor A',
    customIdentifier: 'L09-OP270',
    controllers: [{ id: 'ctrl-159', hostname: 'CPC-159' }]
  },
  {
    id: 'm-l05',
    name: 'L05-OP80',
    displayName: 'Line 05 - Powertrain Sub-Assembly - Station 80',
    organizationId: 'Production Floor A',
    customIdentifier: 'L05-OP80',
    controllers: [{ id: 'ctrl-470', hostname: 'CPC-470' }]
  },
  {
    id: 'm-l08',
    name: 'L08-OP50',
    displayName: 'Line 08 - Surface Coating & Paint Shop - Station 50',
    organizationId: 'Production Floor D',
    customIdentifier: 'L08-OP50',
    controllers: [{ id: 'ctrl-203', hostname: 'CPC-203' }]
  }
]

async function loadMachines() {
  if (machines.value.length > 0) return
  loading.value = true
  try {
    const fetchFn = typeof $fetch !== 'undefined' ? $fetch : (globalThis as any).$fetch
    if (fetchFn) {
      const data = await fetchFn('/api/proxy/v1/Machine')
      if (data && Array.isArray(data) && data.length > 0) {
        machines.value = data
        return
      }
    }
  } catch (err) {}
  machines.value = fallbackMachines
  loading.value = false
}

onMounted(() => {
  loadMachines()
  if (typeof window !== 'undefined') {
    window.addEventListener('click', handleClickOutside)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('click', handleClickOutside)
  }
})

function handleClickOutside(event: MouseEvent) {
  if (rootRef.value && !rootRef.value.contains(event.target as Node)) {
    isOpen.value = false
  }
}

const inputValue = computed({
  get: () => props.modelValue,
  set: (val: string) => {
    emit('update:modelValue', val)
    emit('update:stationName', val)
    isOpen.value = true
    highlightedIndex.value = 0
  }
})

const filteredMachines = computed(() => {
  const query = (props.modelValue || '').trim().toLowerCase()
  if (!query) {
    return machines.value.slice(0, 8)
  }
  return machines.value
    .filter(m => {
      const matchName = m.name?.toLowerCase().includes(query)
      const matchDisplay = m.displayName?.toLowerCase().includes(query)
      const matchCustom = m.customIdentifier?.toLowerCase().includes(query)
      const matchOrg = m.organizationId?.toLowerCase().includes(query)
      const matchCtrl = m.controllers?.some(c => c.hostname?.toLowerCase().includes(query))
      return matchName || matchDisplay || matchCustom || matchOrg || matchCtrl
    })
    .slice(0, 10)
})

function selectMachine(machine: FoundMachine) {
  const identifier = machine.customIdentifier || machine.name || machine.id
  const name = machine.displayName || machine.name || identifier
  emit('update:modelValue', identifier)
  emit('update:stationName', name)
  emit('select', machine)
  isOpen.value = false
}

function selectCustomText() {
  isOpen.value = false
}

function onKeyDown(e: KeyboardEvent) {
  if (!isOpen.value) {
    if (e.key === 'ArrowDown' || e.key === 'Enter') {
      isOpen.value = true
      e.preventDefault()
    }
    return
  }

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    if (highlightedIndex.value < filteredMachines.value.length - 1) {
      highlightedIndex.value++
    }
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    if (highlightedIndex.value > 0) {
      highlightedIndex.value--
    }
  } else if (e.key === 'Enter') {
    e.preventDefault()
    if (highlightedIndex.value >= 0 && highlightedIndex.value < filteredMachines.value.length) {
      selectMachine(filteredMachines.value[highlightedIndex.value])
    } else {
      isOpen.value = false
    }
  } else if (e.key === 'Escape') {
    isOpen.value = false
  }
}

function clear() {
  emit('update:modelValue', '')
  emit('update:stationName', '')
}
</script>

<template>
  <div ref="rootRef" class="relative w-full">
    <div class="relative flex items-center">
      <Search class="absolute left-3 h-3.5 w-3.5 text-slate-500 pointer-events-none" />
      
      <input
        v-model="inputValue"
        type="text"
        :placeholder="placeholder"
        :disabled="disabled"
        @focus="isOpen = true; loadMachines()"
        @keydown="onKeyDown"
        class="w-full bg-slate-950 border border-slate-800 rounded-xl pl-9 pr-14 py-2 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 disabled:opacity-50 transition-colors"
      />

      <div class="absolute right-2 flex items-center gap-1">
        <button
          v-if="modelValue"
          type="button"
          @click="clear"
          class="p-1 rounded-md text-slate-500 hover:text-slate-300 hover:bg-slate-800/60 transition-colors"
          tabindex="-1"
        >
          <X class="h-3 w-3" />
        </button>

        <button
          type="button"
          @click="isOpen = !isOpen; if (isOpen) loadMachines()"
          class="p-1 rounded-md text-slate-500 hover:text-slate-300 transition-colors"
          tabindex="-1"
        >
          <ChevronsUpDown class="h-3.5 w-3.5" />
        </button>
      </div>
    </div>

    <!-- Floating Dropdown -->
    <div
      v-if="isOpen"
      class="absolute z-50 left-0 right-0 mt-1.5 bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl overflow-hidden max-h-64 overflow-y-auto animate-in fade-in zoom-in-95 duration-150"
    >
      <div class="p-2 border-b border-slate-800/60 flex items-center justify-between text-[10px] font-black uppercase tracking-wider text-slate-400 bg-slate-950/40">
        <span>Found Machines & Stations</span>
        <span v-if="filteredMachines.length > 0" class="text-indigo-400 font-mono">
          {{ filteredMachines.length }} matches
        </span>
      </div>

      <div v-if="filteredMachines.length === 0" class="p-3 text-center text-xs text-slate-400 space-y-1">
        <p>No matching registered machines.</p>
        <p class="text-[10px] text-slate-500">Press Enter or click below to use free-text.</p>
        <button
          type="button"
          @click="selectCustomText"
          class="mt-1 px-3 py-1 bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 rounded-lg text-xs font-semibold"
        >
          Use "{{ modelValue }}"
        </button>
      </div>

      <ul v-else class="p-1 space-y-0.5">
        <li
          v-for="(machine, idx) in filteredMachines"
          :key="machine.id || machine.name"
          @click="selectMachine(machine)"
          @mouseenter="highlightedIndex = idx"
          class="px-3 py-2 rounded-xl text-xs flex items-center justify-between cursor-pointer transition-colors"
          :class="[
            highlightedIndex === idx ? 'bg-indigo-600/20 text-indigo-200' : 'hover:bg-slate-800/60 text-slate-300',
            (modelValue && (machine.customIdentifier === modelValue || machine.name === modelValue)) ? 'border border-indigo-500/30 font-semibold' : ''
          ]"
        >
          <div class="flex items-center gap-2.5 min-w-0 flex-1 pr-2">
            <div class="p-1.5 rounded-lg bg-slate-800 text-indigo-400 shrink-0">
              <Cpu class="h-3.5 w-3.5" />
            </div>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <span class="font-bold text-slate-100 truncate">
                  {{ machine.customIdentifier || machine.name }}
                </span>
                <span v-if="machine.organizationId" class="px-1.5 py-0.2 text-[9px] font-black uppercase rounded bg-slate-800 text-slate-400">
                  {{ machine.organizationId }}
                </span>
              </div>
              <p v-if="machine.displayName && machine.displayName !== machine.name" class="text-[10px] text-slate-400 truncate">
                {{ machine.displayName }}
              </p>
            </div>
          </div>

          <div class="flex items-center gap-1.5 shrink-0 text-[10px] font-mono text-slate-500">
            <span v-if="machine.controllers?.[0]?.hostname" class="text-indigo-400/80 bg-indigo-950/40 px-1.5 py-0.5 rounded">
              {{ machine.controllers[0].hostname }}
            </span>
            <Check v-if="modelValue && (machine.customIdentifier === modelValue || machine.name === modelValue)" class="h-3.5 w-3.5 text-indigo-400" />
          </div>
        </li>
      </ul>
    </div>
  </div>
</template>
