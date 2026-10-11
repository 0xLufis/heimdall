<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  Search,
  X,
  Layers,
  Cpu,
  Wrench,
  Boxes,
  ArrowRight,
  Command
} from 'lucide-vue-next'
import type { MachineTwinNode } from '~/composables/useDigitalTwin'

const props = defineProps<{
  machines: MachineTwinNode[]
  tickets: any[]
  controllers: any[]
}>()

const emit = defineEmits<{
  (e: 'select-machine', id: string): void
}>()

const router = useRouter()
const isOpen = ref(false)
const query = ref('')

const openSearch = () => {
  isOpen.value = true
}

const closeSearch = () => {
  isOpen.value = false
  query.value = ''
}

// Global keyboard shortcut: Ctrl+K or Cmd+K or "/"
const handleKeydown = (e: KeyboardEvent) => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
    e.preventDefault()
    isOpen.value = !isOpen.value
  } else if (e.key === 'Escape' && isOpen.value) {
    closeSearch()
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
})

// Filtered categories
const searchResults = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return { machines: [], tickets: [], controllers: [] }

  const matchedMachines = props.machines.filter(m =>
    m.customIdentifier.toLowerCase().includes(q) ||
    m.name.toLowerCase().includes(q) ||
    (m.displayName && m.displayName.toLowerCase().includes(q)) ||
    (m.machineType && m.machineType.toLowerCase().includes(q)) ||
    (m.groupId && m.groupId.toLowerCase().includes(q))
  ).slice(0, 5)

  const matchedTickets = props.tickets.filter(t =>
    (t.ticketNumber && t.ticketNumber.toLowerCase().includes(q)) ||
    (t.title && t.title.toLowerCase().includes(q)) ||
    (t.description && t.description.toLowerCase().includes(q))
  ).slice(0, 4)

  const matchedControllers = props.controllers.filter(c =>
    (c.hostname && c.hostname.toLowerCase().includes(q)) ||
    (c.ipAddress && c.ipAddress.toLowerCase().includes(q)) ||
    (c.name && c.name.toLowerCase().includes(q))
  ).slice(0, 3)

  return {
    machines: matchedMachines,
    tickets: matchedTickets,
    controllers: matchedControllers
  }
})

const handleSelectMachine = (id: string) => {
  emit('select-machine', id)
  closeSearch()
}

const handleSelectTicket = (t: any) => {
  closeSearch()
  router.push('/dashboard/tickets')
}

const handleSelectController = (c: any) => {
  closeSearch()
  router.push('/dashboard/clients')
}
</script>

<template>
  <div>
    <!-- Trigger Button in Header -->
    <button
      @click="openSearch"
      class="flex items-center gap-3 px-3 py-1.5 rounded-xl bg-[#0c0e12] border border-[#232730] hover:border-zinc-700 text-zinc-400 hover:text-zinc-200 transition-all text-xs"
    >
      <Search class="w-3.5 h-3.5 text-zinc-500" />
      <span class="hidden sm:inline">Search machines, telemetry, tickets...</span>
      <span class="inline sm:hidden">Search...</span>
      <kbd class="hidden sm:inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded bg-zinc-800 border border-zinc-700 text-[10px] font-mono text-zinc-400">
        <span>Ctrl</span><span>K</span>
      </kbd>
    </button>

    <!-- Search Modal Overlay -->
    <div
      v-if="isOpen"
      class="fixed inset-0 bg-black/80 backdrop-blur-xs flex items-start justify-center pt-20 p-4 z-50 animate-in fade-in duration-150"
    >
      <div
        class="bg-[#15181e] border border-[#232730] rounded-2xl w-full max-w-xl shadow-2xl overflow-hidden flex flex-col max-h-[80vh]"
      >
        <!-- Search Input Bar -->
        <div class="p-4 border-b border-[#232730] flex items-center gap-3 bg-[#11141a]">
          <Search class="w-4 h-4 text-teal-400 flex-shrink-0" />
          <input
            v-model="query"
            type="text"
            autofocus
            placeholder="Search plant floor by machine, ticket ID, IP, or part..."
            class="flex-1 bg-transparent border-0 text-sm text-zinc-100 placeholder:text-zinc-600 focus:outline-hidden"
          />
          <button
            @click="closeSearch"
            class="p-1 rounded-lg text-zinc-500 hover:text-white hover:bg-zinc-800 transition-colors"
          >
            <X class="w-4 h-4" />
          </button>
        </div>

        <!-- Search Results List -->
        <div class="p-4 overflow-y-auto space-y-4 text-xs">
          <div v-if="!query" class="py-6 text-center text-zinc-500">
            Type to search across stations, active tickets, and edge controllers...
          </div>

          <!-- No results -->
          <div
            v-else-if="
              searchResults.machines.length === 0 &&
              searchResults.tickets.length === 0 &&
              searchResults.controllers.length === 0
            "
            class="py-6 text-center text-zinc-500 font-mono"
          >
            No matching entities found for "{{ query }}"
          </div>

          <!-- Machine Results -->
          <div v-if="searchResults.machines.length > 0">
            <div class="text-[10px] font-mono uppercase text-zinc-500 font-bold mb-2 flex items-center gap-1.5">
              <Layers class="w-3.5 h-3.5 text-teal-400" />
              <span>Stations & Machines</span>
            </div>
            <div class="space-y-1.5">
              <div
                v-for="m in searchResults.machines"
                :key="m.id"
                @click="handleSelectMachine(m.id)"
                class="p-2.5 rounded-lg bg-[#0c0e12] border border-[#232730] hover:border-teal-500/50 cursor-pointer flex items-center justify-between transition-colors"
              >
                <div>
                  <div class="font-bold text-zinc-200 font-mono">{{ m.customIdentifier }}</div>
                  <div class="text-[11px] text-zinc-400">{{ m.displayName || m.name }} &bull; {{ m.groupId }}</div>
                </div>
                <div class="flex items-center gap-2">
                  <span class="px-1.5 py-0.5 rounded text-[9px] font-mono uppercase border border-zinc-700 bg-zinc-800 text-zinc-300">
                    {{ m.status }}
                  </span>
                  <ArrowRight class="w-3.5 h-3.5 text-zinc-500" />
                </div>
              </div>
            </div>
          </div>

          <!-- Ticket Results -->
          <div v-if="searchResults.tickets.length > 0">
            <div class="text-[10px] font-mono uppercase text-zinc-500 font-bold mb-2 flex items-center gap-1.5">
              <Wrench class="w-3.5 h-3.5 text-rose-400" />
              <span>Maintenance Tickets</span>
            </div>
            <div class="space-y-1.5">
              <div
                v-for="t in searchResults.tickets"
                :key="t.id"
                @click="handleSelectTicket(t)"
                class="p-2.5 rounded-lg bg-[#0c0e12] border border-[#232730] hover:border-rose-500/50 cursor-pointer flex items-center justify-between transition-colors"
              >
                <div>
                  <div class="font-bold text-zinc-200 font-mono">{{ t.ticketNumber || 'TICK-101' }}</div>
                  <div class="text-[11px] text-zinc-400 truncate max-w-sm">{{ t.title }}</div>
                </div>
                <div class="flex items-center gap-2">
                  <span class="px-1.5 py-0.5 rounded text-[9px] font-mono uppercase border border-rose-500/30 bg-rose-500/10 text-rose-300">
                    {{ t.priority || 'High' }}
                  </span>
                  <ArrowRight class="w-3.5 h-3.5 text-zinc-500" />
                </div>
              </div>
            </div>
          </div>

          <!-- Controller Results -->
          <div v-if="searchResults.controllers.length > 0">
            <div class="text-[10px] font-mono uppercase text-zinc-500 font-bold mb-2 flex items-center gap-1.5">
              <Cpu class="w-3.5 h-3.5 text-sky-400" />
              <span>Edge Controllers & IPCs</span>
            </div>
            <div class="space-y-1.5">
              <div
                v-for="c in searchResults.controllers"
                :key="c.id"
                @click="handleSelectController(c)"
                class="p-2.5 rounded-lg bg-[#0c0e12] border border-[#232730] hover:border-sky-500/50 cursor-pointer flex items-center justify-between transition-colors"
              >
                <div>
                  <div class="font-bold text-zinc-200 font-mono">{{ c.hostname || c.name }}</div>
                  <div class="text-[11px] text-zinc-400 font-mono">{{ c.ipAddress || '192.168.1.100' }}</div>
                </div>
                <ArrowRight class="w-3.5 h-3.5 text-zinc-500" />
              </div>
            </div>
          </div>
        </div>

        <!-- Footer -->
        <div class="px-4 py-2.5 border-t border-[#232730] bg-[#11141a] flex items-center justify-between text-[10px] text-zinc-500 font-mono">
          <span>Press ESC to close</span>
          <span>Tip: Jump directly with Enter</span>
        </div>
      </div>
    </div>
  </div>
</template>
