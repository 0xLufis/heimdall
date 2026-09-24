<script setup lang="ts">
import { ref, computed } from 'vue'
import { ChevronRight, Cpu, Layers, HardDrive, Edit3, ArrowUpRight, Loader2 } from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Skeleton } from '~/components/ui/skeleton'

const props = defineProps<{
  items: any[]
  type?: 'hardware' | 'software' | 'hierarchy' | 'parts' | 'stock' | string
  classification?: string
  tracking?: string
  loading: boolean
  columns: Record<string, boolean>
  isChild?: boolean
}>()

const emit = defineEmits<{
  (e: 'edit', item: any): void
}>()

const expanded = ref<Record<string, boolean>>({})

function toggle(id: string) {
  expanded.value[id] = !expanded.value[id]
}

const formatCurrency = (val: any) => {
  if (!val && val !== 0) return '-'
  return new Intl.NumberFormat('hu-HU').format(val)
}

const tableTitle = computed(() => {
  const c = (props.classification || props.type || 'all').toLowerCase()
  const t = (props.tracking || 'all').toLowerCase()

  if (c === 'hardware' && t === 'stock') return 'Hardware Consumable Stock & Parts'
  if (c === 'hardware' && t === 'serialized') return 'Serialized Hardware Equipment & Discrete Parts'
  if (c === 'software' && t === 'stock') return 'Software License Pools & Seat Stock'
  if (c === 'software' && t === 'serialized') return 'Serialized Software Licenses & Deployments'
  if (c === 'hardware') return 'Hardware Asset Registry'
  if (c === 'software') return 'Software Licenses & Packages'
  if (t === 'stock') return 'Bulk Consumable Stock Inventory'
  if (t === 'serialized' || t === 'parts') return 'Serialized Parts & High-Value Equipment'
  return 'Unified Asset & Inventory Registry'
})

const tableSubtitle = computed(() => {
  const c = (props.classification || 'all').toLowerCase()
  const t = (props.tracking || 'all').toLowerCase()
  const filterDesc = [
    c !== 'all' ? (c === 'hardware' ? 'Hardware' : 'Software') : null,
    t !== 'all' ? (t === 'serialized' ? 'Serialized Parts' : 'Bulk Stock') : null
  ].filter(Boolean).join(' • ')

  return `${props.items.length} assets listed ${filterDesc ? `(${filterDesc})` : '(All Categories)'} • Click any row to edit`
})
</script>

<template>
  <Card class="border-border shadow-xs overflow-hidden bg-card" :class="{'!bg-transparent !shadow-none !border-none': isChild}">
    <CardHeader v-if="!isChild" class="p-4 sm:p-5 border-b border-border bg-muted/30 flex flex-row items-center justify-between">
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg bg-primary/10 text-primary border border-primary/20">
          <HardDrive class="w-4 h-4" />
        </div>
        <div>
          <CardTitle class="text-base font-semibold text-foreground">
            {{ tableTitle }}
          </CardTitle>
          <p class="text-xs text-muted-foreground mt-0.5">
            {{ tableSubtitle }}
          </p>
        </div>
      </div>
    </CardHeader>
    
    <CardContent class="p-0">
      <Table>
        <TableHeader v-if="!isChild" class="bg-muted/50">
          <TableRow class="border-b border-border hover:bg-transparent">
            <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs w-[35%]">
              Asset Identity / Class
            </TableHead>
            <TableHead v-if="columns.manufacturer" class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs">
              Manufacturer
            </TableHead>
            <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs">
              Responsible Teams
            </TableHead>
            <TableHead v-if="columns.specs" class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs">
              Parameters & Specs
            </TableHead>
            <TableHead v-if="columns.tags" class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs">
              Attributes
            </TableHead>
            <TableHead v-if="columns.purchaseDate" class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs">
              Deployment Date
            </TableHead>
            <TableHead v-if="columns.cost" class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs text-right">
              Valuation (HUF)
            </TableHead>
            <TableHead class="w-10"></TableHead>
          </TableRow>
        </TableHeader>
        
        <TableBody>
          <!-- Skeleton Loading State when initial load and no items yet -->
          <template v-if="loading && items.length === 0 && !isChild">
            <TableRow v-for="n in 5" :key="'skel-' + n" class="border-b border-border hover:bg-transparent">
              <TableCell class="px-4 py-3">
                <div class="flex items-center gap-3">
                  <Skeleton class="w-6 h-6 rounded-lg bg-muted" />
                  <div class="space-y-2">
                    <Skeleton class="h-4 w-44 rounded bg-muted" />
                    <div class="flex gap-2">
                      <Skeleton class="h-3 w-14 rounded-md bg-muted/60" />
                      <Skeleton class="h-3 w-20 rounded-md bg-muted/60" />
                    </div>
                  </div>
                </div>
              </TableCell>
              <TableCell v-if="columns.manufacturer" class="px-4 py-3">
                <Skeleton class="h-3.5 w-24 rounded bg-muted/80" />
              </TableCell>
              <TableCell class="px-4 py-3">
                <Skeleton class="h-3.5 w-28 rounded bg-muted/80" />
              </TableCell>
              <TableCell v-if="columns.specs" class="px-4 py-3">
                <div class="flex gap-1.5">
                  <Skeleton class="h-4 w-14 rounded-md bg-muted/60" />
                  <Skeleton class="h-4 w-16 rounded-md bg-muted/60" />
                </div>
              </TableCell>
              <TableCell v-if="columns.tags" class="px-4 py-3">
                <Skeleton class="h-4 w-20 rounded bg-muted/60" />
              </TableCell>
              <TableCell v-if="columns.purchaseDate" class="px-4 py-3">
                <Skeleton class="h-3.5 w-20 rounded bg-muted/80" />
              </TableCell>
              <TableCell v-if="columns.cost" class="px-4 py-3 text-right">
                <Skeleton class="h-4 w-20 rounded ml-auto bg-muted/80" />
              </TableCell>
              <TableCell class="w-10"></TableCell>
            </TableRow>
          </template>

          <template v-else-if="items.length === 0 && !isChild">
            <TableRow>
              <TableCell colspan="8" class="h-32 text-center text-muted-foreground font-medium text-xs">
                No {{ type }} assets found matching criteria.
              </TableCell>
            </TableRow>
          </template>
          
          <template v-else>
            <template v-for="item in items" :key="item?.id">
              <TableRow 
                v-if="item" 
                @click="emit('edit', item)"
                class="hover:bg-muted/40 transition-colors group border-b border-border last:border-0 cursor-pointer" 
                :class="{'bg-muted/20': isChild}"
              >
                <!-- Identity -->
                <TableCell class="px-4 py-3 font-medium">
                  <div class="flex items-center gap-3">
                    <Button 
                      v-if="item.children && item.children.length > 0" 
                      @click.stop="toggle(item.id)" 
                      variant="ghost" 
                      size="icon" 
                      class="h-6 w-6 text-muted-foreground hover:bg-muted rounded-md shrink-0"
                    >
                      <ChevronRight class="h-4 w-4 transition-transform duration-200" :class="{'rotate-90 text-primary': expanded[item.id]}" />
                    </Button>
                    <div v-else class="w-6 h-6 flex items-center justify-center shrink-0">
                      <div class="w-1.5 h-1.5 rounded-full bg-border"></div>
                    </div>
                    <div>
                      <div class="text-foreground group-hover:text-primary transition-colors font-semibold text-xs flex flex-wrap items-center gap-1.5">
                        <span>{{ item.name }}</span>
                        <Badge 
                          variant="outline" 
                          class="text-xs font-medium px-2 py-0.5 rounded-md border-primary/30 text-primary bg-primary/10 inline-flex items-center shadow-xs"
                        >
                          {{ item.itemType || 'Hardware' }}
                        </Badge>
                        <Badge 
                          v-if="item.isStockItem"
                          variant="outline" 
                          class="text-xs font-medium px-2 py-0.5 rounded-md border-purple-500/30 text-purple-700 dark:text-purple-300 bg-purple-500/10 inline-flex items-center gap-1 shadow-xs"
                        >
                          Stock: {{ item.stockQuantity ?? 1 }} units
                        </Badge>
                        <Badge 
                          v-else
                          variant="outline" 
                          class="text-xs font-medium px-2 py-0.5 rounded-md border-teal-500/30 text-teal-700 dark:text-teal-300 bg-teal-500/10 inline-flex items-center gap-1 shadow-xs"
                        >
                          Serialized Part
                        </Badge>
                        <Badge 
                          v-if="item.equipmentStatus"
                          variant="outline" 
                          class="text-xs font-medium px-2 py-0.5 rounded-md inline-flex items-center gap-1 shadow-xs"
                          :class="item.equipmentStatus === 'InMachine' ? 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30' : item.equipmentStatus === 'InStorage' ? 'bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-500/30' : 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30'"
                        >
                          {{ item.equipmentStatus }}
                        </Badge>
                        <Badge 
                          v-if="item.technology"
                          variant="outline" 
                          class="text-xs font-medium px-2 py-0.5 rounded-md border-teal-500/30 text-teal-700 dark:text-teal-300 bg-teal-500/10 inline-flex items-center"
                        >
                          {{ item.technology }}
                        </Badge>
                        <Badge 
                          v-if="item.telemetry?.isOnline !== undefined"
                          variant="outline" 
                          class="text-xs font-medium px-2 py-0.5 rounded-md inline-flex items-center gap-1.5 shadow-xs"
                          :class="item.telemetry.isOnline ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30' : 'bg-muted text-muted-foreground border-border'"
                        >
                          <span class="size-1.5 rounded-full" :class="item.telemetry.isOnline ? 'bg-emerald-500 animate-pulse' : 'bg-muted-foreground'" />
                          {{ item.telemetry.isOnline ? 'Live Agent' : 'Offline' }}
                        </Badge>
                      </div>
                      <div class="text-xs text-muted-foreground mt-0.5 flex items-center gap-2">
                        <span>{{ item.displayName || item.customIdentifier || item.hostname || '' }}</span>
                        <span v-if="item.storageLocation" class="text-xs text-muted-foreground font-mono">
                          [Pos: {{ item.storageLocation }}]
                        </span>
                      </div>
                      <div class="text-xs text-muted-foreground font-mono mt-0.5">
                        {{ item.isStockItem ? (item.serialNumber ? 'ID: ' + item.serialNumber : 'Bulk Stock Batch: ' + (item.metadata?.Batch || 'LOT-STD')) : 'SN: ' + (item.serialNumber || 'UNTRACKED') }}
                      </div>
                    </div>
                  </div>
                </TableCell>

                <!-- Manufacturer -->
                <TableCell v-if="columns.manufacturer" class="px-4 py-3 text-xs font-medium text-foreground">
                  {{ item.manufacturer?.name || (typeof item.manufacturer === 'string' ? item.manufacturer : 'N/A') }}
                </TableCell>

                <!-- Teams -->
                <TableCell class="px-4 py-3">
                  <div class="flex flex-wrap gap-1">
                    <Badge 
                      v-for="team in item.responsibleTeams" 
                      :key="team.id || team.name" 
                      variant="secondary" 
                      class="bg-primary/10 text-primary border border-primary/20 text-xs font-medium px-2 py-0.5 rounded-md inline-flex items-center shadow-xs"
                    >
                      {{ team.name || team }}
                    </Badge>
                    <Badge v-if="!item.responsibleTeams?.length" variant="outline" class="text-muted-foreground border-border text-xs px-2 py-0.5 rounded-md">
                      Unassigned
                    </Badge>
                  </div>
                </TableCell>

                <!-- Specs -->
                <TableCell v-if="columns.specs" class="px-4 py-3">
                  <div class="flex flex-wrap gap-1 max-w-xs">
                    <span v-if="item.telemetry?.cpuUsagePercent !== undefined" class="text-xs font-mono font-medium border border-emerald-500/30 bg-emerald-500/10 px-2 py-0.5 rounded-md text-emerald-600 dark:text-emerald-400 inline-flex items-center shadow-xs">
                      CPU: {{ item.telemetry.cpuUsagePercent }}%
                    </span>
                    <span v-if="item.telemetry?.ramUsagePercent !== undefined" class="text-xs font-mono font-medium border border-primary/30 bg-primary/10 px-2 py-0.5 rounded-md text-primary inline-flex items-center shadow-xs">
                      RAM: {{ item.telemetry.ramUsagePercent }}%
                    </span>
                    <span v-if="item.telemetry?.freeDiskSpace?.totalFreeGB !== undefined" class="text-xs font-mono font-medium border border-blue-500/30 bg-blue-500/10 px-2 py-0.5 rounded-md text-blue-600 dark:text-blue-400 inline-flex items-center shadow-xs">
                      {{ item.telemetry.freeDiskSpace.totalFreeGB }}GB Free
                    </span>
                    <span v-if="item.metadata?.Power" class="text-xs font-medium border border-amber-500/30 bg-amber-500/10 px-2 py-0.5 rounded-md text-amber-700 dark:text-amber-400 inline-flex items-center shadow-xs">
                      {{ item.metadata.Power }}
                    </span>
                    <span v-if="item.metadata?.Voltage" class="text-xs font-medium border border-blue-500/30 bg-blue-500/10 px-2 py-0.5 rounded-md text-blue-700 dark:text-blue-400 inline-flex items-center shadow-xs">
                      {{ item.metadata.Voltage }}
                    </span>
                    <span v-if="item.metadata?.Resolution" class="text-xs font-medium border border-purple-500/30 bg-purple-500/10 px-2 py-0.5 rounded-md text-purple-700 dark:text-purple-400 inline-flex items-center shadow-xs">
                      {{ item.metadata.Resolution }}
                    </span>
                    <span v-if="item.metadata?.Version || item.version" class="text-xs font-medium border border-emerald-500/30 bg-emerald-500/10 px-2 py-0.5 rounded-md text-emerald-700 dark:text-emerald-400 inline-flex items-center shadow-xs">
                      v{{ item.metadata?.Version || item.version }}
                    </span>
                    <span v-if="item.modelNumber" class="text-xs font-medium border border-border bg-muted px-2 py-0.5 rounded-md text-foreground inline-flex items-center shadow-xs">
                      {{ item.modelNumber }}
                    </span>
                  </div>
                </TableCell>

                <!-- Tags / Metadata -->
                <TableCell v-if="columns.tags" class="px-4 py-3">
                  <div class="flex flex-wrap gap-1 max-w-xs">
                    <template v-for="(val, key) in item.metadata" :key="key">
                      <span v-if="!['Power', 'Voltage', 'Resolution', 'Version'].includes(key as string)" class="px-2 py-0.5 rounded-md bg-muted text-xs font-medium text-foreground border border-border inline-flex items-center shadow-xs">
                        {{ key }}: {{ val }}
                      </span>
                    </template>
                  </div>
                </TableCell>

                <!-- Purchase Date -->
                <TableCell v-if="columns.purchaseDate" class="px-4 py-3 text-xs font-mono text-muted-foreground">
                  {{ item.purchaseDate ? new Date(item.purchaseDate).toLocaleDateString() : '-' }}
                </TableCell>

                <!-- Cost -->
                <TableCell v-if="columns.cost" class="px-4 py-3 text-right">
                  <div class="text-xs font-semibold text-foreground font-mono">
                    {{ formatCurrency(item.costInHUF) }}
                    <span v-if="item.costInHUF || item.costInHUF === 0" class="text-xs text-muted-foreground font-sans ml-1">HUF</span>
                  </div>
                </TableCell>

                <!-- Edit Action -->
                <TableCell class="pr-4 text-right">
                  <div class="p-1 rounded-md text-muted-foreground group-hover:text-primary group-hover:bg-muted transition-all inline-flex items-center justify-center">
                    <Edit3 class="w-3.5 h-3.5" />
                  </div>
                </TableCell>
              </TableRow>

              <!-- Child Rows -->
              <TableRow v-if="expanded[item.id] && item.children && item.children.length > 0" class="bg-muted/10 border-b border-border last:border-0">
                <TableCell colspan="8" class="p-0 pl-8">
                  <DashboardInventoryTable 
                    :items="item.children" 
                    :type="type" 
                    :loading="false" 
                    :columns="columns" 
                    :is-child="true"
                    @edit="emit('edit', $event)" 
                  />
                </TableCell>
              </TableRow>
            </template>
          </template>
        </TableBody>
      </Table>
    </CardContent>
  </Card>
</template>
