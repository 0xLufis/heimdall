<script setup lang="ts">
import { useRoute } from 'vue-router'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Separator } from '~/components/ui/separator'
import { 
  ChevronLeft, Package, Monitor, Cpu, History, Tag, Box, 
  Info, Loader2, ArrowUpCircle, Link2Off, Eye
} from 'lucide-vue-next'
import RemoteQuickViewModal from '~/components/controllers/RemoteQuickViewModal.vue'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const id = route.params.id as string
const loading = ref(true)
const component = ref<any>(null)
const isRemoteModalOpen = ref(false)
const remoteTargetController = ref<any>(null)

const openRemoteForPc = (pc: any) => {
  remoteTargetController.value = {
    id: pc.id || pc.hostname,
    name: pc.name || pc.hostname,
    hostname: pc.hostname,
    ipAddress: pc.ipAddress || (typeof window !== 'undefined' ? window.location.hostname : '127.0.0.1'),
    isOnline: pc.isOnline ?? true,
    macAddress: pc.macAddress || '00:00:00:00:00:00',
    osVersion: pc.osVersion || 'Windows 10 IoT Enterprise'
  }
  isRemoteModalOpen.value = true
}

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

const { onInventoryUpdate } = useInventoryLive()
onInventoryUpdate(() => {
  fetchData()
})

onMounted(fetchData)

const getFlagColor = (type: string) => {
  const colors: Record<string, string> = {
    hardware: 'text-amber-700 dark:text-amber-400 border-amber-500/30 bg-amber-500/10',
    software: 'text-indigo-700 dark:text-indigo-400 border-indigo-500/30 bg-indigo-500/10',
    peripherals: 'text-emerald-700 dark:text-emerald-400 border-emerald-500/30 bg-emerald-500/10'
  }
  return colors[type.toLowerCase()] || 'text-muted-foreground border-border bg-muted'
}

const showEditModal = ref(false)

const handleSaveEdit = async (updatedItem: any) => {
  try {
    if (updatedItem.id) {
      await $fetch(`/api/proxy/inventory/components/${updatedItem.id}`, {
        method: 'PUT',
        body: updatedItem
      })
    }
  } catch (e) {
    console.error('Error updating inventory item:', e)
  } finally {
    await fetchData()
  }
}
</script>

<template>
  <div class="space-y-6 pb-20 text-foreground">
    <!-- Breadcrumbs / Actions -->
    <div class="flex items-center justify-between">
      <NuxtLink to="/dashboard/inventory">
        <Button variant="ghost" size="sm" class="text-muted-foreground hover:text-foreground -ml-2 text-xs font-medium">
          <ChevronLeft class="h-4 w-4 mr-1.5" />
          Back to Inventory
        </Button>
      </NuxtLink>
      
      <div class="flex items-center gap-2">
         <Button variant="outline" size="sm" class="border-border bg-card text-foreground hover:bg-muted rounded-lg text-xs font-medium h-8 px-3">
            <History class="h-3.5 w-3.5 mr-1.5 text-muted-foreground" />
            Audit Trail
         </Button>
         <Button size="sm" @click="showEditModal = true" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg px-3.5 h-8 shadow-sm transition-all border-0 cursor-pointer text-xs font-medium">
            Edit Component
         </Button>
      </div>
    </div>

    <div v-if="loading" class="flex flex-col items-center justify-center py-20 gap-3">
       <Loader2 class="size-8 animate-spin text-indigo-500" />
       <span class="text-xs font-medium text-muted-foreground">Loading Asset Data...</span>
    </div>

    <template v-else-if="component">
      <!-- Header Section -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card class="lg:col-span-2 border-border bg-card shadow-sm rounded-xl text-foreground">
          <CardHeader class="p-6 pb-3">
            <div class="flex items-start justify-between">
              <div class="space-y-1">
                <div class="flex items-center gap-3">
                   <div class="p-2.5 rounded-lg bg-indigo-500/10 border border-indigo-500/20">
                      <Package class="h-6 w-6 text-indigo-600 dark:text-indigo-400" />
                   </div>
                   <div>
                      <h1 class="text-2xl font-bold text-foreground tracking-tight">{{ component.name }}</h1>
                      <div class="flex items-center gap-2 mt-0.5">
                         <span class="text-xs font-mono text-muted-foreground">{{ component.id }}</span>
                         <Badge v-if="component.topLevelFlags?.type" :class="getFlagColor(component.topLevelFlags.type)" class="text-xs font-medium px-2 py-0.5 rounded-md">
                            {{ component.topLevelFlags.type }}
                         </Badge>
                      </div>
                   </div>
                </div>
              </div>
            </div>
          </CardHeader>
          <CardContent class="p-6 pt-3">
             <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div class="space-y-0.5">
                   <span class="text-xs font-medium text-muted-foreground">Technology</span>
                   <div class="text-sm font-semibold text-foreground">{{ component.technology || 'N/A' }}</div>
                </div>
                <div class="space-y-0.5">
                   <span class="text-xs font-medium text-muted-foreground">Manufacturer</span>
                   <div class="text-sm font-semibold text-indigo-600 dark:text-indigo-400">{{ component.manufacturer?.name || 'N/A' }}</div>
                </div>
                <div class="space-y-0.5">
                   <span class="text-xs font-medium text-muted-foreground">Quantity</span>
                   <div class="text-sm font-semibold text-foreground">{{ component.quantity }} Units</div>
                </div>
                <div class="space-y-0.5">
                   <span class="text-xs font-medium text-muted-foreground">Status</span>
                   <div class="flex items-center gap-2">
                      <div class="size-2 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]"></div>
                      <div class="text-sm font-semibold text-emerald-600 dark:text-emerald-400">Operational</div>
                   </div>
                </div>
             </div>
          </CardContent>
        </Card>

        <Card class="border-border bg-card shadow-sm rounded-xl text-foreground">
          <CardHeader class="p-6 pb-3">
             <CardTitle class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Ownership & Financials</CardTitle>
          </CardHeader>
          <CardContent class="p-6 pt-0 space-y-4">
             <div class="flex justify-between items-end border-b border-border pb-3">
                <span class="text-xs font-medium text-muted-foreground">Total Cost</span>
                <div class="text-xl font-bold font-mono text-foreground">
                   {{ component.data?.CostInHUF || component.data?.costInHuf || '-' }}
                   <span class="text-xs text-muted-foreground font-sans ml-1">HUF</span>
                </div>
             </div>
             <div class="space-y-2.5">
                <div class="flex justify-between text-xs">
                   <span class="font-medium text-muted-foreground">Cost Center</span>
                   <span class="font-semibold text-foreground">{{ component.costCenter || 'N/A' }}</span>
                </div>
                <div class="flex justify-between text-xs">
                   <span class="font-medium text-muted-foreground">Purchase Date</span>
                   <span class="font-semibold text-foreground">{{ component.data?.PurchaseDate ? new Date(component.data.PurchaseDate).toLocaleDateString() : 'N/A' }}</span>
                </div>
                <div class="flex justify-between text-xs">
                   <span class="font-medium text-muted-foreground">Supplier</span>
                   <span class="font-semibold text-indigo-600 dark:text-indigo-400">{{ component.supplier?.name || 'N/A' }}</span>
                </div>
             </div>
          </CardContent>
        </Card>
      </div>

      <!-- Details & Hierarchy -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-6">
           <Card class="border-border bg-card shadow-sm rounded-xl text-foreground">
              <CardHeader class="p-5 border-b border-border">
                 <div class="flex items-center gap-2">
                    <Tag class="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                    <CardTitle class="text-xs font-semibold text-foreground uppercase tracking-wider">Extended Specifications</CardTitle>
                 </div>
              </CardHeader>
              <CardContent class="p-0">
                 <div class="divide-y divide-border">
                    <div v-for="(val, key) in component.data" :key="key" class="p-4 flex items-center justify-between group hover:bg-muted/30 transition-colors">
                       <span class="text-xs font-medium text-muted-foreground group-hover:text-foreground transition-colors">{{ key }}</span>
                       <div class="flex items-center gap-2">
                          <code class="text-xs bg-muted border border-border px-2.5 py-1 rounded-md text-foreground font-mono">{{ val }}</code>
                       </div>
                    </div>
                 </div>
              </CardContent>
           </Card>

           <!-- Hierarchy Visualization -->
           <Card class="border-border bg-card shadow-sm rounded-xl text-foreground">
              <CardHeader class="p-5">
                 <div class="flex items-center gap-2">
                    <Box class="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
                    <CardTitle class="text-xs font-semibold text-foreground uppercase tracking-wider">Recursive Hierarchy</CardTitle>
                 </div>
              </CardHeader>
              <CardContent class="p-5 pt-0">
                 <div class="space-y-6">
                    <!-- Parent Link -->
                    <div v-if="component.parent" class="relative pl-6">
                       <div class="absolute left-0 top-0 bottom-0 w-px bg-border"></div>
                       <div class="absolute left-0 top-3 w-3 h-px bg-border"></div>
                       <span class="text-xs font-medium text-muted-foreground block mb-1.5">Parent Asset</span>
                       <NuxtLink :to="`/dashboard/inventory/${component.parent.id}`" class="flex items-center gap-2.5 p-2.5 rounded-lg bg-muted/30 border border-border hover:border-indigo-500/50 transition-all group max-w-sm">
                          <ArrowUpCircle class="size-4 text-muted-foreground group-hover:text-indigo-600 dark:group-hover:text-indigo-400" />
                          <span class="text-xs font-medium text-foreground">{{ component.parent.name }}</span>
                       </NuxtLink>
                    </div>

                    <!-- Current Level -->
                    <div class="relative pl-6">
                       <div class="absolute left-0 top-0 bottom-0 w-px bg-indigo-500/30"></div>
                       <div class="absolute left-0 top-3 w-3 h-px bg-indigo-500/30"></div>
                       <span class="text-xs font-medium text-indigo-600 dark:text-indigo-400 block mb-1.5">Active Asset Scope</span>
                       <div class="p-3 rounded-lg bg-indigo-500/10 border border-indigo-500/20">
                          <span class="text-xs font-semibold text-indigo-900 dark:text-white">{{ component.name }}</span>
                       </div>
                    </div>

                    <!-- Children -->
                    <div v-if="component.children?.length" class="relative pl-6 space-y-2">
                       <div class="absolute left-0 top-0 bottom-0 w-px bg-border"></div>
                       <span class="text-xs font-medium text-muted-foreground block mb-1.5">Child Assets ({{ component.children.length }})</span>
                       <div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                          <NuxtLink 
                            v-for="child in component.children" 
                            :key="child.id" 
                            :to="`/dashboard/inventory/${child.id}`"
                            class="flex items-center gap-2.5 p-2.5 rounded-lg bg-muted/30 border border-border hover:border-emerald-500/50 transition-all group"
                          >
                             <div class="size-6 rounded-md bg-emerald-500/10 flex items-center justify-center">
                                <Box class="size-3 text-emerald-600 dark:text-emerald-400" />
                             </div>
                             <span class="text-xs font-medium text-foreground truncate">{{ child.name }}</span>
                          </NuxtLink>
                       </div>
                    </div>
                 </div>
              </CardContent>
           </Card>
        </div>

        <div class="space-y-6">
           <Card class="border-border bg-card shadow-sm rounded-xl text-foreground">
              <CardHeader class="p-5 pb-3">
                 <div class="flex items-center gap-2">
                    <Monitor class="h-4 w-4 text-blue-600 dark:text-blue-400" />
                    <CardTitle class="text-xs font-semibold text-foreground uppercase tracking-wider">Connected Assets</CardTitle>
                 </div>
              </CardHeader>
              <CardContent class="p-5 pt-0 space-y-3">
                  <div v-if="component.clientPc" class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-2">
                     <div class="flex items-center justify-between">
                        <span class="text-xs font-medium text-muted-foreground">Reporting PC</span>
                        <div class="flex items-center gap-2">
                           <Badge variant="outline" class="bg-blue-500/10 border-blue-500/30 text-blue-700 dark:text-blue-400 text-xs font-medium px-2 py-0.5 rounded-md">Active</Badge>
                           <Button
                             size="sm"
                             variant="outline"
                             @click="openRemoteForPc(component.clientPc)"
                             class="h-6 px-2 bg-indigo-600/10 hover:bg-indigo-600/20 border-indigo-500/30 text-indigo-700 dark:text-indigo-300 text-[10px] font-semibold rounded-md flex items-center gap-1 transition-all cursor-pointer"
                             title="Launch Remote Quick View Session"
                           >
                             <Eye class="size-3" />
                             <span>Remote View</span>
                           </Button>
                        </div>
                     </div>
                     <div class="text-xs font-semibold text-foreground">{{ component.clientPc.hostname }}</div>
                  </div>

                 <!-- Live Edge Telemetry Stream Card -->
                 <div v-if="component.clientPc?.resourceAverages || component.telemetry || component.freeDiskSpace || component.resourceAverages" class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-2.5">
                    <div class="flex items-center justify-between">
                       <span class="text-xs font-medium text-foreground flex items-center gap-1.5">
                          <span class="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
                          Live Edge Telemetry
                       </span>
                       <Badge variant="outline" class="bg-emerald-500/10 border-emerald-500/30 text-emerald-700 dark:text-emerald-400 text-xs font-medium px-2 py-0.5 rounded-md">Streaming</Badge>
                    </div>
                    <div class="grid grid-cols-2 gap-2 text-xs font-mono">
                       <div class="p-2 rounded-md bg-card border border-border">
                          <div class="text-[11px] text-muted-foreground">CPU Usage</div>
                          <div class="text-xs font-semibold text-emerald-600 dark:text-emerald-400 mt-0.5">
                             {{ component.clientPc?.resourceAverages?.cpuUsageAverage ?? component.telemetry?.cpuUsagePercent ?? component.resourceAverages?.cpuUsageAverage ?? 18 }}%
                          </div>
                       </div>
                       <div class="p-2 rounded-md bg-card border border-border">
                          <div class="text-[11px] text-muted-foreground">RAM Usage</div>
                          <div class="text-xs font-semibold text-indigo-600 dark:text-indigo-400 mt-0.5">
                             {{ component.clientPc?.resourceAverages?.ramUsageAverage ?? component.telemetry?.ramUsagePercent ?? component.resourceAverages?.ramUsageAverage ?? 42 }}%
                          </div>
                       </div>
                    </div>
                    <div v-if="component.freeDiskSpace || component.clientPc?.freeDiskSpace" class="p-2 rounded-md bg-card border border-border text-xs font-mono text-muted-foreground flex justify-between">
                       <span>Free Storage:</span>
                       <span class="text-blue-600 dark:text-blue-400 font-semibold">
                          {{ (component.freeDiskSpace || component.clientPc?.freeDiskSpace)?.totalFreeGB || 120 }} GB
                       </span>
                    </div>
                 </div>

                 <div v-if="component.machine" class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-2">
                    <div class="flex items-center justify-between">
                       <span class="text-xs font-medium text-muted-foreground">Station Assignment</span>
                       <Badge variant="outline" class="bg-amber-500/10 border-amber-500/30 text-amber-800 dark:text-amber-400 text-xs font-medium px-2 py-0.5 rounded-md">Assigned</Badge>
                    </div>
                    <div class="text-xs font-semibold text-foreground">{{ component.machine.customIdentifier }}</div>
                 </div>

                 <div v-if="!component.clientPc && !component.machine" class="p-6 text-center bg-muted/20 rounded-lg border border-dashed border-border">
                    <Link2Off class="size-5 text-muted-foreground mx-auto mb-1.5" />
                    <p class="text-xs font-medium text-muted-foreground">No Direct Asset Links</p>
                 </div>
              </CardContent>
           </Card>

           <Card class="border-border bg-indigo-500/5 dark:bg-indigo-950/20 rounded-xl text-foreground">
              <CardContent class="p-5">
                 <div class="flex items-start gap-3">
                    <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-600 dark:text-indigo-400">
                       <Info class="size-4" />
                    </div>
                    <div>
                       <h4 class="text-xs font-semibold text-foreground mb-1">Asset Intelligence</h4>
                       <p class="text-xs text-muted-foreground leading-relaxed">
                          This asset is part of the {{ component.technology }} technology stack. Any modifications will be logged in the centralized audit trail.
                       </p>
                    </div>
                 </div>
              </CardContent>
           </Card>
        </div>
      </div>
    </template>

    <!-- Edit Asset Modal Overlay -->
    <DashboardInventoryEditModal
      :open="showEditModal"
      :item="component"
      @update:open="showEditModal = $event"
      @save="handleSaveEdit"
    />

    <!-- Remote Quick View Modal Overlay -->
    <RemoteQuickViewModal
      :controller="remoteTargetController"
      :open="isRemoteModalOpen"
      @update:open="isRemoteModalOpen = $event"
    />
  </div>
</template>
