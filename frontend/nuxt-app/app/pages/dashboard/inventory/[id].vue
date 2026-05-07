<script setup lang="ts">
import { useRoute } from 'vue-router'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Separator } from '~/components/ui/separator'
import { ChevronLeft, Package, Monitor, Cpu, History, Tag, Box, Info } from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const id = route.params.id as string
const loading = ref(true)
const component = ref<any>(null)

const fetchData = async () => {
  loading.value = true
  try {
    const data = await $fetch<any>(`/api/proxy/inventory/${id}`)
    component.value = data
  } catch (e) {
    console.error('Error fetching component details:', e)
  } finally {
    loading.value = false
  }
}

onMounted(fetchData)

const getFlagColor = (type: string) => {
  const colors: Record<string, string> = {
    hardware: 'text-amber-500 border-amber-900/30 bg-amber-900/10',
    software: 'text-indigo-400 border-indigo-900/30 bg-indigo-900/10',
    peripherals: 'text-emerald-500 border-emerald-900/30 bg-emerald-900/10'
  }
  return colors[type.toLowerCase()] || 'text-slate-400 border-slate-800 bg-slate-900/50'
}
</script>

<template>
  <div class="space-y-8 pb-20">
    <!-- Breadcrumbs / Actions -->
    <div class="flex items-center justify-between">
      <NuxtLink to="/dashboard/inventory">
        <Button variant="ghost" size="sm" class="text-slate-500 hover:text-slate-200 -ml-2">
          <ChevronLeft class="h-4 w-4 mr-2" />
          Back to Inventory
        </Button>
      </NuxtLink>
      
      <div class="flex items-center gap-2">
         <Button variant="outline" class="border-slate-800 bg-slate-900 text-slate-300 rounded-xl text-[10px] font-black uppercase tracking-widest h-10 px-6">
            <History class="h-4 w-4 mr-2" />
            Audit Trail
         </Button>
         <Button class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl px-8 h-10 shadow-lg transition-all border-0">
            <span class="text-[10px] font-black uppercase tracking-widest">Edit Component</span>
         </Button>
      </div>
    </div>

    <div v-if="loading" class="flex flex-col items-center justify-center py-20 gap-4">
       <Icon name="i-lucide-loader-2" class="size-10 animate-spin text-indigo-500" />
       <span class="text-xs font-black uppercase tracking-[0.3em] text-slate-500">Retrieving Asset DNA...</span>
    </div>

    <template v-else-if="component">
      <!-- Header Section -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <Card class="lg:col-span-2 border-slate-800 bg-slate-950/50 shadow-2xl">
          <CardHeader class="p-8 pb-4">
            <div class="flex items-start justify-between">
              <div class="space-y-1">
                <div class="flex items-center gap-3">
                   <div class="p-3 rounded-2xl bg-indigo-500/10 border border-indigo-500/20">
                      <Package class="h-6 w-6 text-indigo-400" />
                   </div>
                   <div>
                      <h1 class="text-3xl font-black text-white tracking-tighter uppercase">{{ component.name }}</h1>
                      <div class="flex items-center gap-2 mt-1">
                         <span class="text-xs font-mono text-slate-500 tracking-tighter uppercase">{{ component.id }}</span>
                         <Badge v-if="component.topLevelFlags?.type" :class="getFlagColor(component.topLevelFlags.type)" class="text-[9px] font-black uppercase tracking-widest px-2 py-0.5">
                            {{ component.topLevelFlags.type }}
                         </Badge>
                      </div>
                   </div>
                </div>
              </div>
            </div>
          </CardHeader>
          <CardContent class="p-8 pt-4">
             <div class="grid grid-cols-2 md:grid-cols-4 gap-6">
                <div class="space-y-1">
                   <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Technology</span>
                   <div class="text-sm font-bold text-slate-300 uppercase">{{ component.technology || 'N/A' }}</div>
                </div>
                <div class="space-y-1">
                   <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Manufacturer</span>
                   <div class="text-sm font-bold text-indigo-400 uppercase">{{ component.manufacturer?.name || 'N/A' }}</div>
                </div>
                <div class="space-y-1">
                   <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Quantity</span>
                   <div class="text-sm font-bold text-slate-300">{{ component.quantity }} Units</div>
                </div>
                <div class="space-y-1">
                   <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Status</span>
                   <div class="flex items-center gap-2">
                      <div class="size-2 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]"></div>
                      <div class="text-sm font-bold text-emerald-500 uppercase">Operational</div>
                   </div>
                </div>
             </div>
          </CardContent>
        </Card>

        <Card class="border-slate-800 bg-slate-950/50">
          <CardHeader class="p-8 pb-4">
             <CardTitle class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Ownership & Financials</CardTitle>
          </CardHeader>
          <CardContent class="p-8 pt-0 space-y-6">
             <div class="flex justify-between items-end border-b border-slate-900 pb-4">
                <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Total Cost</span>
                <div class="text-2xl font-black text-white">
                   {{ component.data?.CostInHUF || component.data?.costInHuf || '-' }}
                   <span class="text-xs text-slate-500 font-bold ml-1">HUF</span>
                </div>
             </div>
             <div class="space-y-4">
                <div class="flex justify-between text-xs">
                   <span class="font-black text-slate-600 uppercase tracking-widest">Cost Center</span>
                   <span class="font-bold text-slate-400">{{ component.costCenter || 'N/A' }}</span>
                </div>
                <div class="flex justify-between text-xs">
                   <span class="font-black text-slate-600 uppercase tracking-widest">Purchase Date</span>
                   <span class="font-bold text-slate-400">{{ component.data?.PurchaseDate ? new Date(component.data.PurchaseDate).toLocaleDateString() : 'N/A' }}</span>
                </div>
                <div class="flex justify-between text-xs">
                   <span class="font-black text-slate-600 uppercase tracking-widest">Supplier</span>
                   <span class="font-bold text-indigo-400">{{ component.supplier?.name || 'N/A' }}</span>
                </div>
             </div>
          </CardContent>
        </Card>
      </div>

      <!-- Details & Hierarchy -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <div class="lg:col-span-2 space-y-8">
           <Card class="border-slate-800 bg-slate-950/50">
              <CardHeader class="p-8 border-b border-slate-900">
                 <div class="flex items-center gap-2">
                    <Tag class="h-4 w-4 text-emerald-500" />
                    <CardTitle class="text-[10px] font-black text-slate-200 uppercase tracking-widest">Extended Specifications</CardTitle>
                 </div>
              </CardHeader>
              <CardContent class="p-0">
                 <div class="divide-y divide-slate-900">
                    <div v-for="(val, key) in component.data" :key="key" class="p-6 flex items-center justify-between group hover:bg-slate-900/30 transition-colors">
                       <span class="text-xs font-black text-slate-500 uppercase tracking-widest group-hover:text-slate-400 transition-colors">{{ key }}</span>
                       <div class="flex items-center gap-2">
                          <code class="text-xs bg-slate-900 border border-slate-800 px-3 py-1.5 rounded-lg text-slate-200 font-mono shadow-inner">{{ val }}</code>
                       </div>
                    </div>
                 </div>
              </CardContent>
           </Card>

           <!-- Hierarchy Visualization -->
           <Card class="border-slate-800 bg-slate-950/50">
              <CardHeader class="p-8">
                 <div class="flex items-center gap-2">
                    <Box class="h-4 w-4 text-indigo-400" />
                    <CardTitle class="text-[10px] font-black text-slate-200 uppercase tracking-widest">Recursive Hierarchy</CardTitle>
                 </div>
              </CardHeader>
              <CardContent class="p-8 pt-0">
                 <div class="space-y-8">
                    <!-- Parent Link -->
                    <div v-if="component.parent" class="relative pl-8">
                       <div class="absolute left-0 top-0 bottom-0 w-px bg-slate-800"></div>
                       <div class="absolute left-0 top-4 w-4 h-px bg-slate-800"></div>
                       <span class="text-[9px] font-black text-slate-600 uppercase tracking-[0.2em] block mb-2">Parent Asset</span>
                       <NuxtLink :to="`/dashboard/inventory/${component.parent.id}`" class="flex items-center gap-3 p-3 rounded-xl bg-slate-900 border border-slate-800 hover:border-indigo-500/50 transition-all group max-w-sm">
                          <Icon name="i-lucide-arrow-up-circle" class="size-4 text-slate-600 group-hover:text-indigo-400" />
                          <span class="text-xs font-bold text-slate-300">{{ component.parent.name }}</span>
                       </NuxtLink>
                    </div>

                    <!-- Current Level -->
                    <div class="relative pl-8">
                       <div class="absolute left-0 top-0 bottom-0 w-px bg-indigo-500/30"></div>
                       <div class="absolute left-0 top-4 w-4 h-px bg-indigo-500/30"></div>
                       <span class="text-[9px] font-black text-indigo-500 uppercase tracking-[0.2em] block mb-2">Active Asset Scope</span>
                       <div class="p-4 rounded-xl bg-indigo-500/5 border border-indigo-500/20 shadow-[0_0_15px_rgba(99,102,241,0.05)]">
                          <span class="text-xs font-black text-white uppercase">{{ component.name }}</span>
                       </div>
                    </div>

                    <!-- Children -->
                    <div v-if="component.children?.length" class="relative pl-8 space-y-3">
                       <div class="absolute left-0 top-0 bottom-0 w-px bg-slate-800"></div>
                       <span class="text-[9px] font-black text-slate-600 uppercase tracking-[0.2em] block mb-2">Child Assets ({{ component.children.length }})</span>
                       <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                          <NuxtLink 
                            v-for="child in component.children" 
                            :key="child.id" 
                            :to="`/dashboard/inventory/${child.id}`"
                            class="flex items-center gap-3 p-3 rounded-xl bg-slate-900 border border-slate-800 hover:border-emerald-500/50 transition-all group"
                          >
                             <div class="size-6 rounded-lg bg-emerald-500/10 flex items-center justify-center">
                                <Icon name="i-lucide-box" class="size-3 text-emerald-500" />
                             </div>
                             <span class="text-xs font-bold text-slate-300 truncate">{{ child.name }}</span>
                          </NuxtLink>
                       </div>
                    </div>
                 </div>
              </CardContent>
           </Card>
        </div>

        <div class="space-y-8">
           <Card class="border-slate-800 bg-slate-950/50">
              <CardHeader class="p-8 pb-4">
                 <div class="flex items-center gap-2">
                    <Monitor class="h-4 w-4 text-blue-400" />
                    <CardTitle class="text-[10px] font-black text-slate-200 uppercase tracking-widest">Connected Assets</CardTitle>
                 </div>
              </CardHeader>
              <CardContent class="p-8 pt-0 space-y-4">
                 <div v-if="component.clientPc" class="p-4 rounded-xl bg-slate-900 border border-slate-800 space-y-3">
                    <div class="flex items-center justify-between">
                       <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Reporting PC</span>
                       <Badge variant="outline" class="bg-blue-900/10 border-blue-900/30 text-blue-400 text-[9px] font-black uppercase">Active</Badge>
                    </div>
                    <div class="text-sm font-black text-slate-200 uppercase">{{ component.clientPc.hostname }}</div>
                 </div>

                 <div v-if="component.machine" class="p-4 rounded-xl bg-slate-900 border border-slate-800 space-y-3">
                    <div class="flex items-center justify-between">
                       <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Station Assignment</span>
                       <Badge variant="outline" class="bg-amber-900/10 border-amber-900/30 text-amber-500 text-[9px] font-black uppercase">Assigned</Badge>
                    </div>
                    <div class="text-sm font-black text-slate-200 uppercase">{{ component.machine.customIdentifier }}</div>
                 </div>

                 <div v-if="!component.clientPc && !component.machine" class="p-8 text-center bg-slate-900/30 rounded-2xl border border-dashed border-slate-800">
                    <Icon name="i-lucide-link-2-off" class="size-6 text-slate-700 mx-auto mb-2" />
                    <p class="text-[10px] font-black text-slate-600 uppercase tracking-widest">No Direct Asset Links</p>
                 </div>
              </CardContent>
           </Card>

           <Card class="border-slate-800 bg-indigo-950/10">
              <CardContent class="p-8">
                 <div class="flex items-start gap-3">
                    <div class="p-2 rounded-lg bg-indigo-500/20">
                       <Info class="size-4 text-indigo-400" />
                    </div>
                    <div>
                       <h4 class="text-xs font-black text-slate-200 uppercase tracking-widest mb-1">Asset Intelligence</h4>
                       <p class="text-[10px] text-slate-500 leading-relaxed font-medium">
                          This asset is part of the {{ component.technology }} technology stack. Any modifications will be logged in the centralized audit trail.
                       </p>
                    </div>
                 </div>
              </CardContent>
           </Card>
        </div>
      </div>
    </template>
  </div>
</template>
