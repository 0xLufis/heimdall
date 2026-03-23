<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import InteractiveMap from '~/components/dashboard/InteractiveMap.vue'
import MapPinningDialog from '~/components/dashboard/MapPinningDialog.vue'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { MonitorIcon, MapPinIcon } from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const activePin = ref<string | null>(null)
const activeBlockName = ref<string | null>(null)
const isPinningDialogOpen = ref(false)

// Data
const clients = ref<any[]>([])
const machines = ref<any[]>([])
const loading = ref(false)

const fetchData = async () => {
    loading.value = true
    try {
        const [clientsRes, machinesRes] = await Promise.all([
            $fetch('/api/proxy/ClientPc'),
            $fetch('/api/proxy/Machine')
        ])
        clients.value = (clientsRes || []) as any[]
        machines.value = (machinesRes || []) as any[]
    } catch (e) {
        console.error('Error fetching data:', e)
    } finally {
        loading.value = false
    }
}

onMounted(() => {
    fetchData()
})

const handleObjectClick = (handle: string, blockName: string) => {
    activePin.value = handle
    activeBlockName.value = blockName
    isPinningDialogOpen.value = true
}

const handlePinUpdate = async (type: 'machine' | 'client', targetId: string) => {
    if (!activePin.value) return
    
    try {
        if (type === 'machine') {
            const machine = machines.value.find(m => m.id === targetId)
            if (machine) {
                machine.pinnedObjectHandle = activePin.value
                await $fetch(`/api/proxy/Machine/${machine.id}`, {
                    method: 'PUT',
                    body: machine
                })
            }
        } else {
            const client = clients.value.find(c => c.id === targetId)
            if (client) {
                client.pinnedObjectHandle = activePin.value
                await $fetch(`/api/proxy/ClientPc/${client.id}`, {
                    method: 'PUT',
                    body: client
                })
            }
        }
        await fetchData()
    } catch (e) {
        console.error('Failed to update pin', e)
        alert('Failed to update pin assignment.')
    }
}

const pinnedAssets = computed(() => {
    const list: any[] = []
    clients.value.forEach(c => {
        if (c.pinnedObjectHandle) {
            list.push({ id: c.id, name: c.hostname, handle: c.pinnedObjectHandle, type: 'Client PC' })
        }
    })
    machines.value.forEach(m => {
        if (m.pinnedObjectHandle) {
            list.push({ id: m.id, name: m.customIdentifier, handle: m.pinnedObjectHandle, type: 'Machine' })
        }
    })
    return list
})
</script>

<template>
  <div class="space-y-8 h-full bg-slate-950 text-slate-100">
    <!-- Header -->
    <div class="flex items-end justify-between">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Plant Layout</h3>
        <p class="text-[10px] font-bold text-slate-500 mt-1 uppercase tracking-widest leading-none">Mapping Digital Assets to Physical Space</p>
      </div>
      <div class="flex gap-2">
        <div class="flex items-center gap-2 px-4 py-2 bg-emerald-950/20 border border-emerald-900/30 rounded-xl">
           <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
           <span class="text-[10px] font-black text-emerald-500 uppercase tracking-widest">Live Sync Active</span>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-4 gap-8 h-[calc(100vh-220px)]">
      <!-- Map Area -->
      <div class="lg:col-span-3 h-full rounded-3xl overflow-hidden border border-slate-800 shadow-2xl relative bg-slate-900">
        <InteractiveMap 
          dxfUrl="/sample/assembly_line.dxf" 
          :active-pin="activePin"
          @object-clicked="handleObjectClick"
        />
        
        <!-- Map Controls Legend Overlay -->
        <div class="absolute bottom-6 left-6 p-4 bg-slate-900/90 backdrop-blur-md rounded-2xl border border-slate-800 shadow-xl flex gap-6 z-10 pointer-events-none">
           <div class="flex items-center gap-2">
              <div class="w-3 h-3 rounded-full bg-slate-500 shadow-[0_0_8px_rgba(100,116,139,0.5)]"></div>
              <span class="text-[9px] font-black uppercase tracking-widest text-slate-500">Interactive Object</span>
           </div>
           <div class="flex items-center gap-2 border-l border-slate-800 pl-6">
              <span class="text-[9px] font-black uppercase tracking-widest text-slate-600">Scroll to Zoom</span>
              <span class="text-[9px] font-black uppercase tracking-widest text-slate-600 ml-2">Drag to Pan</span>
           </div>
        </div>
      </div>

      <!-- Inventory Sidebar -->
      <div class="h-full flex flex-col gap-6 overflow-hidden">
        <Card class="border-none shadow-sm flex-1 flex flex-col bg-slate-900/50">
          <CardHeader class="pb-4 border-b border-slate-800 bg-slate-900">
            <CardTitle class="text-[10px] font-black text-slate-500 uppercase tracking-widest flex items-center gap-2">
               <MapPinIcon class="h-3 w-3" />
               Spatial Mapping
            </CardTitle>
          </CardHeader>
          <CardContent class="p-0 overflow-y-auto flex-1 bg-slate-950/20">
            <div v-if="loading" class="p-12 text-center text-[10px] font-black uppercase tracking-widest text-slate-600 animate-pulse">
               Syncing Coordinates...
            </div>
            <div v-else-if="pinnedAssets.length === 0" class="p-12 text-center">
               <div class="w-12 h-12 bg-slate-900 rounded-2xl mx-auto mb-4 flex items-center justify-center">
                  <MapPinIcon class="h-6 w-6 text-slate-700" />
               </div>
               <p class="text-[10px] font-black text-slate-600 uppercase tracking-widest">No pins assigned yet</p>
               <p class="text-[9px] text-slate-700 mt-2">Click an object on the map to start pinning</p>
            </div>
            <div v-else class="divide-y divide-slate-800">
               <div v-for="asset in pinnedAssets" :key="asset.id" 
                    @click="activePin = asset.handle"
                    :class="activePin === asset.handle ? 'bg-slate-800 border-l-4 border-l-slate-400' : 'hover:bg-slate-900/50'"
                    class="p-5 transition-all cursor-pointer group"
               >
                  <div class="flex justify-between items-start mb-1">
                     <p class="text-sm font-black text-slate-200 group-hover:text-white transition-colors">{{ asset.name }}</p>
                     <span class="text-[8px] font-black uppercase tracking-widest px-1.5 py-0.5 bg-slate-800 text-slate-500 rounded">{{ asset.type }}</span>
                  </div>
                  <div class="flex items-center justify-between">
                     <span class="text-[10px] font-mono text-slate-500 group-hover:text-slate-400 transition-colors">Ref: {{ asset.handle }}</span>
                     <MonitorIcon v-if="asset.type === 'Client PC'" class="h-3 w-3 text-slate-600" />
                  </div>
               </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- Mapping Overlay Dialog -->
    <DashboardMapPinningDialog 
      v-model:open="isPinningDialogOpen"
      :handle="activePin || ''"
      :object-name="activeBlockName || ''"
      :machines="machines"
      :clients="clients"
      @pin="handlePinUpdate"
    />
  </div>
</template>
