<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table'
import InteractiveMap from '~/components/dashboard/InteractiveMap.vue'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const loading = ref(false)
const searchQuery = ref('')
const clients = ref<any[]>([])
const machines = ref<any[]>([])
const highlightedHandles = ref<string[]>([])

const filteredClients = computed(() => {
    if (!searchQuery.value) return clients.value
    const q = searchQuery.value.toLowerCase()
    return clients.value.filter(c => 
        c.hostname.toLowerCase().includes(q) || 
        c.macAddress.toLowerCase().includes(q) || 
        (c.machineIdentifier && c.machineIdentifier.toLowerCase().includes(q)) ||
        (c.machines && c.machines.some((m: any) => m.customIdentifier.toLowerCase().includes(q)))
    )
})

const isOnline = (lastOnline: string | null) => {
    if (!lastOnline) return false
    const date = new Date(lastOnline)
    const now = new Date()
    return (now.getTime() - date.getTime()) < (5 * 60 * 1000) // Online if seen in last 5 minutes
}

const formatLastOnline = (lastOnline: string | null) => {
    if (!lastOnline) return 'Never'
    const date = new Date(lastOnline)
    return date.toLocaleString()
}

const fetchData = async () => {
    loading.value = true
    try {
        const [clientsRes, machinesRes] = await Promise.all([
            useFetch('/api/proxy/ClientPc'),
            useFetch('/api/proxy/Machine')
        ])
        
        if (clientsRes.data.value) {
            clients.value = clientsRes.data.value as any[]
        }
        if (machinesRes.data.value) {
            machines.value = machinesRes.data.value as any[]
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
  <div class="space-y-6">
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h3 class="text-2xl font-bold text-gray-900 tracking-tight font-sans">Client PC Management</h3>
        <p class="text-sm text-gray-500 mt-1 font-sans">Monitor and manage connected client devices.</p>
      </div>
      <div class="flex items-center gap-3">
        <Button variant="outline" @click="fetchData" class="gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Refresh
        </Button>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Main Content Column -->
        <div class="lg:col-span-2 space-y-6">
            <!-- Search & Filters -->
            <Card class="border-gray-100 shadow-sm">
               <CardContent class="p-4">
                  <div class="relative flex-grow w-full">
                    <div class="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                      </svg>
                    </div>
                    <input 
                      v-model="searchQuery"
                      type="text" 
                      placeholder="Search by hostname, MAC, or station ID..." 
                      class="block w-full pl-12 pr-4 py-3 border border-gray-100 rounded-xl bg-gray-50/50 focus:bg-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 text-sm transition-all" 
                    />
                  </div>
               </CardContent>
            </Card>

            <!-- Clients Table -->
            <Card class="border-gray-100 shadow-sm overflow-hidden">
                <CardContent class="p-0">
                    <Table>
                        <TableHeader class="bg-gray-50/50 border-b border-gray-100">
                            <TableRow>
                                <TableHead class="px-8 py-5 uppercase tracking-widest font-black text-[10px]">Hostname & Station ID</TableHead>
                                <TableHead class="px-8 py-5 uppercase tracking-widest font-black text-[10px]">Network Info</TableHead>
                                <TableHead class="px-8 py-5 uppercase tracking-widest font-black text-[10px]">Configuration</TableHead>
                                <TableHead class="px-8 py-5 uppercase tracking-widest font-black text-[10px]">Last Online</TableHead>
                                <TableHead class="px-8 py-5 text-right uppercase tracking-widest font-black text-[10px]">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody class="divide-y divide-gray-50">
                            <TableRow v-if="loading" v-for="i in 3" :key="i">
                                <TableCell colspan="5" class="px-8 py-10 text-center text-gray-400 animate-pulse uppercase font-bold text-xs tracking-widest">Loading clients...</TableCell>
                            </TableRow>
                            <TableRow v-else-if="filteredClients.length === 0">
                                <TableCell colspan="5" class="px-8 py-20 text-center">
                                    <div class="flex flex-col items-center gap-2">
                                        <div class="w-12 h-12 bg-gray-50 rounded-2xl flex items-center justify-center text-gray-300">
                                            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                                            </svg>
                                        </div>
                                        <p class="text-sm font-bold text-gray-400 uppercase tracking-widest">No clients found</p>
                                    </div>
                                </TableCell>
                            </TableRow>
                            <TableRow 
                                v-for="client in filteredClients" 
                                :key="client.id" 
                                class="hover:bg-indigo-50/10 transition-colors group cursor-pointer"
                                @mouseenter="setHighlight(client)"
                                @mouseleave="setHighlight(null)"
                            >
                                <TableCell class="px-8 py-5">
                                    <div class="flex flex-col items-start gap-1">
                                        <span class="font-black text-gray-900 tracking-tight">{{ client.hostname }}</span>
                                        <div v-if="client.machines && client.machines.length > 0" class="flex flex-wrap gap-1">
                                            <span v-for="m in client.machines" :key="m.id" class="px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 text-[9px] font-black uppercase tracking-widest">{{ m.customIdentifier }}</span>
                                        </div>
                                        <span v-else class="text-[10px] font-mono text-gray-400">{{ client.machineIdentifier }}</span>
                                    </div>
                                </TableCell>
                                <TableCell class="px-8 py-5">
                                    <div class="flex flex-col gap-1 text-[11px]">
                                        <div class="flex items-center gap-1.5">
                                            <span class="text-[10px] font-bold text-gray-400 uppercase tracking-tighter">MAC:</span>
                                            <span class="font-mono text-gray-600">{{ client.macAddress }}</span>
                                        </div>
                                        <div class="flex items-center gap-1.5">
                                            <span class="text-[10px] font-bold text-gray-400 uppercase tracking-tighter">ID:</span>
                                            <span class="font-mono text-gray-400">{{ client.id.substring(0, 8) }}...</span>
                                        </div>
                                    </div>
                                </TableCell>
                                <TableCell class="px-8 py-5">
                                    <div class="flex flex-col gap-1">
                                        <div class="flex items-center gap-1.5">
                                            <span class="px-1.5 py-0.5 rounded bg-gray-100 text-[9px] font-black text-gray-500 uppercase">{{ client.hardwareConfig?.cpu || 'N/A' }}</span>
                                            <span class="px-1.5 py-0.5 rounded bg-gray-100 text-[9px] font-black text-gray-500 uppercase">{{ client.hardwareConfig?.ram || 'N/A' }}</span>
                                        </div>
                                        <span class="text-[10px] text-gray-400 font-medium">{{ client.softwareConfig?.osVersion || 'Unknown OS' }}</span>
                                    </div>
                                </TableCell>
                                <TableCell class="px-8 py-5">
                                     <div class="flex items-center gap-2">
                                        <div :class="isOnline(client.lastOnline) ? 'bg-emerald-500 animate-pulse' : 'bg-gray-300'" class="w-2 h-2 rounded-full"></div>
                                        <span class="text-xs font-bold text-gray-600">{{ formatLastOnline(client.lastOnline) }}</span>
                                     </div>
                                </TableCell>
                                <TableCell class="px-8 py-5 text-right">
                                     <Button variant="ghost" size="icon" class="text-gray-400 hover:text-indigo-600">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                        </svg>
                                     </Button>
                                </TableCell>
                            </TableRow>
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>
        </div>
        
        <!-- Live Map Preview Column -->
        <div class="lg:col-span-1 flex flex-col h-[500px] lg:h-auto lg:min-h-[600px] sticky top-6">
            <Card class="h-full flex flex-col border-gray-100 shadow-sm overflow-hidden">
                <CardHeader class="pb-4 border-b border-gray-50 bg-white z-10 shrink-0">
                    <CardTitle class="text-sm font-black uppercase tracking-widest text-gray-400">Location Preview</CardTitle>
                </CardHeader>
                <CardContent class="p-0 flex-grow relative bg-slate-900">
                    <!-- Modular map component -->
                    <InteractiveMap 
                        dxfUrl="/sample/assembly_line.dxf" 
                        :highlightedHandles="highlightedHandles"
                        class="absolute inset-0"
                    />
                    
                    <div v-if="highlightedHandles.length === 0" class="absolute bottom-4 left-4 right-4 bg-slate-800/80 backdrop-blur text-xs text-slate-300 p-3 rounded-xl border border-slate-700/50 shadow-lg text-center pointer-events-none">
                        Hover over a client PC to reveal its physical location.
                    </div>
                </CardContent>
            </Card>
        </div>
    </div>
  </div>
</template>
