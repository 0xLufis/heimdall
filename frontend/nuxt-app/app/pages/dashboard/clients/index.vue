<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table'
import { Search, RefreshCw } from 'lucide-vue-next'
import InteractiveMap from '~/components/dashboard/InteractiveMap.vue'

definePageMeta({
  layout: 'shadcn-dashboard'
})

/**
 * Reactive state indicating whether data is currently being loaded.
 * @type {Ref<boolean>}
 */
const loading = ref(false)

/**
 * Reactive state for the search query input by the user.
 * @type {Ref<string>}
 */
const searchQuery = ref('')

/**
 * Reactive state holding the list of client PCs.
 * @type {Ref<any[]>}
 */
const clients = ref<any[]>([])

/**
 * Reactive state holding the list of machines.
 * @type {Ref<any[]>}
 */
const machines = ref<any[]>([])

/**
 * Reactive state for handles of objects currently highlighted on the map.
 * @type {Ref<string[]>}
 */
const highlightedHandles = ref<string[]>([])

/**
 * Computed property that filters the list of clients based on the `searchQuery`.
 * Filters by hostname, MAC address, machine identifier, and custom machine identifiers.
 * @type {ComputedRef<any[]>}
 */
const filteredClients = computed(() => {
    if (!searchQuery.value) return clients.value
    const q = searchQuery.value.toLowerCase()
    return clients.value.filter(c => 
        (c.hostname && c.hostname.toLowerCase().includes(q)) || 
        (c.macAddress && c.macAddress.toLowerCase().includes(q)) || 
        (c.machineIdentifier && c.machineIdentifier.toLowerCase().includes(q)) ||
        (c.machines && c.machines.some((m: any) => m.customIdentifier && m.customIdentifier.toLowerCase().includes(q)))
    )
})

/**
 * Determines if a client is considered online based on its last seen timestamp.
 * A client is online if `lastOnline` is within the last 5 minutes.
 * @param {string | null} lastOnline - The timestamp of the client's last online activity.
 * @returns {boolean} True if the client is online, false otherwise.
 */
const isOnline = (lastOnline: string | null): boolean => {
    if (!lastOnline) return false
    const date = new Date(lastOnline)
    const now = new Date()
    return (now.getTime() - date.getTime()) < (5 * 60 * 1000) // Online if seen in last 5 minutes
}

/**
 * Formats a `lastOnline` timestamp into a locale-specific string.
 * @param {string | null} lastOnline - The timestamp of the client's last online activity.
 * @returns {string} The formatted date string, or 'Never' if `lastOnline` is null.
 */
const formatLastOnline = (lastOnline: string | null): string => {
    if (!lastOnline) return 'Never'
    const date = new Date(lastOnline)
    return date.toLocaleString()
}

/**
 * Fetches client PC and machine data from the API.
 * Sets `loading` state during the fetch operation.
 */
const fetchData = async () => {
    loading.value = true
    try {
        const [clientsData, machinesData] = await Promise.all([
            $fetch('/api/proxy/ClientPc'),
            $fetch('/api/proxy/Machine')
        ])
        
        if (clientsData) {
            clients.value = clientsData as any[]
        }
        if (machinesData) {
            machines.value = machinesData as any[]
        }
    } catch (e) {
        console.error('Error fetching data:', e)
    } finally {
        loading.value = false
    }
}

onMounted(() => {
    fetchData()
})

/**
 * Sets the `highlightedHandles` to a specific client's pinned object handle,
 * or clears it if no handle is found or provided.
 * If a client has multiple machines, it highlights the first machine's handle.
 * @param {any} client - The client object whose pinned object handle should be highlighted.
 */
const setHighlight = (client: any) => {
    let handle = client?.pinnedObjectHandle
    if (!handle && client?.machines && client.machines.length > 0) {
        // Just highlight the first machine's handle for preview if multiple exist
        handle = client.machines[0].pinnedObjectHandle
    }
    
    if (handle) {
        highlightedHandles.value = [handle]
    } else {
        highlightedHandles.value = []
    }
}
</script>

<template>
  <div class="flex flex-col h-[calc(100vh-theme(spacing.16))] gap-6">
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6 px-8 pt-8">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Client Terminals</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">Real-time edge device monitoring</p>
      </div>
      
      <div class="flex items-center gap-4">
        <div class="relative w-64 group">
          <Search class="absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500 group-focus-within:text-indigo-500 transition-colors z-10" />
          <Input 
            v-model="searchQuery" 
            placeholder="Search by host or MAC..." 
            class="w-full pl-12 pr-4 h-12 bg-slate-900 border-slate-800 rounded-2xl focus:ring-4 focus:ring-indigo-500/10 transition-all font-bold text-xs shadow-sm text-slate-200" 
          />
        </div>
        <Button @click="fetchData" variant="outline" class="bg-slate-900 border-slate-800 text-slate-300 rounded-2xl px-6 h-12 hover:bg-slate-800 transition-all">
          <RefreshCw :class="{'animate-spin': loading}" class="h-4 w-4 mr-2" />
          <span class="text-xs font-black uppercase tracking-widest">Sync</span>
        </Button>
      </div>
    </div>

    <div class="flex-grow grid grid-cols-1 lg:grid-cols-3 gap-6 px-8 pb-8 overflow-hidden">
      <!-- Sidebar List -->
      <Card class="lg:col-span-1 bg-slate-900 border-slate-800 flex flex-col overflow-hidden rounded-3xl">
        <CardHeader class="border-b border-slate-800 p-6 flex flex-row items-center justify-between">
          <CardTitle class="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em]">Connected Endpoints</CardTitle>
          <span class="px-3 py-1 bg-indigo-600/20 text-indigo-400 text-[10px] font-black rounded-full border border-indigo-600/30 uppercase tracking-widest">{{ filteredClients.length }} Active</span>
        </CardHeader>
        <CardContent class="p-0 flex-grow overflow-y-auto">
          <!-- Loading state -->
          <template v-if="loading && clients.length === 0">
            <div class="p-12 flex flex-col items-center justify-center gap-4 text-slate-600">
              <div class="w-8 h-8 border-4 border-indigo-500 border-t-transparent rounded-full animate-spin"></div>
              <p class="text-[10px] font-black uppercase tracking-widest">Acquiring signal...</p>
            </div>
          </template>

          <!-- Empty state -->
          <template v-else-if="filteredClients.length === 0">
            <div class="p-12 text-center">
              <p class="text-xs font-bold text-slate-500 uppercase tracking-widest">No clients found matching query</p>
            </div>
          </template>

          <!-- List state -->
          <template v-else>
            <div class="divide-y divide-slate-800/50">
              <div 
                v-for="client in filteredClients" 
                :key="client.id"
                @mouseenter="setHighlight(client)"
                class="p-6 hover:bg-slate-800/50 transition-all cursor-pointer group relative"
                :class="{'bg-slate-800/80': highlightedHandles.includes(client.pinnedObjectHandle || (client.machines?.[0]?.pinnedObjectHandle))}"
              >
                <div class="flex items-start justify-between mb-2">
                  <div>
                    <h4 class="font-black text-slate-200 group-hover:text-white transition-colors flex items-center gap-2">
                      {{ client.hostname || 'Unknown Host' }}
                      <span v-if="isOnline(client.lastSeen)" class="w-1.5 h-1.5 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]"></span>
                    </h4>
                    <p class="text-[10px] font-mono text-slate-500 mt-0.5">{{ client.macAddress || 'No MAC' }}</p>
                  </div>
                  <div class="text-right">
                    <div class="text-[10px] font-black text-slate-400 uppercase tracking-tighter">{{ formatLastOnline(client.lastSeen) }}</div>
                  </div>
                </div>
                <div class="flex flex-wrap gap-2 mt-3">
                  <span v-for="machine in client.machines" :key="machine.id" class="px-2 py-1 bg-slate-950 text-slate-400 text-[9px] font-black rounded-md border border-slate-800 uppercase tracking-widest">
                    {{ machine.customIdentifier }}
                  </span>
                </div>
              </div>
            </div>
          </template>
        </CardContent>
      </Card>

      <!-- Map View -->
      <Card class="lg:col-span-2 bg-slate-950 border-slate-800 flex flex-col overflow-hidden rounded-3xl relative">
        <div class="absolute top-6 left-6 z-10 flex flex-col gap-2">
           <div class="px-4 py-2 bg-slate-900/90 backdrop-blur-md border border-slate-800 rounded-2xl flex items-center gap-3 shadow-2xl">
              <div class="w-2 h-2 rounded-full bg-indigo-500"></div>
              <span class="text-[10px] font-black text-slate-300 uppercase tracking-[0.2em]">Live Plant Topography</span>
           </div>
        </div>
        
        <div class="w-full h-full">
          <InteractiveMap 
            dxf-url="/sample/assembly_line.dxf" 
            :highlighted-handles="highlightedHandles"
          />
        </div>
      </Card>
    </div>
  </div>
</template>