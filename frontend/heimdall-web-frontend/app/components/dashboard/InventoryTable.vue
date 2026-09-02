<script setup lang="ts">
import { ref } from 'vue'
import { ChevronRight, Cpu, Layers, HardDrive, Edit3, ArrowUpRight } from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

const props = defineProps<{
  items: any[]
  type: 'hardware' | 'software' | 'hierarchy'
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
</script>

<template>
  <Card class="border-slate-800 shadow-sm overflow-hidden bg-slate-900/50" :class="{'!bg-transparent !shadow-none !border-none': isChild}">
    <CardHeader v-if="!isChild" class="p-6 border-b border-slate-800 flex flex-row items-center justify-between">
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-xl bg-indigo-600/10 text-indigo-400 border border-indigo-500/20">
          <HardDrive class="w-4 h-4" />
        </div>
        <div>
          <CardTitle class="text-xs font-black text-slate-300 uppercase tracking-[0.2em]">
            {{ type === 'software' ? 'Software Licenses & Packages' : 'Hardware Asset Registry' }}
          </CardTitle>
          <p class="text-[10px] text-slate-500 font-bold uppercase tracking-wider mt-0.5">
            {{ items.length }} {{ type }} assets deployed in active infrastructure • Click any row to edit
          </p>
        </div>
      </div>
    </CardHeader>
    
    <CardContent class="p-0">
      <Table>
        <TableHeader v-if="!isChild" class="bg-slate-950/60">
          <TableRow class="border-b border-slate-800 hover:bg-transparent">
            <TableHead class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px] w-[35%]">
              Asset Identity / Class
            </TableHead>
            <TableHead v-if="columns.manufacturer" class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]">
              Manufacturer
            </TableHead>
            <TableHead class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]">
              Responsible Teams
            </TableHead>
            <TableHead v-if="columns.specs" class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]">
              Parameters & Specs
            </TableHead>
            <TableHead v-if="columns.tags" class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]">
              Attributes
            </TableHead>
            <TableHead v-if="columns.purchaseDate" class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]">
              Deployment Date
            </TableHead>
            <TableHead v-if="columns.cost" class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px] text-right">
              Valuation (HUF)
            </TableHead>
            <TableHead class="w-12"></TableHead>
          </TableRow>
        </TableHeader>
        
        <TableBody>
          <template v-if="items.length === 0 && !loading && !isChild">
            <TableRow>
              <TableCell colspan="8" class="h-32 text-center text-slate-500 uppercase font-black text-xs tracking-widest">
                No {{ type }} assets found matching criteria.
              </TableCell>
            </TableRow>
          </template>
          
          <template v-else>
            <template v-for="item in items" :key="item?.id">
              <TableRow 
                v-if="item" 
                @click="emit('edit', item)"
                class="hover:bg-slate-850/70 transition-colors group border-b border-slate-800 last:border-0 cursor-pointer" 
                :class="{'bg-slate-950/80': isChild}"
              >
                <!-- Identity -->
                <TableCell class="px-6 py-4 font-bold">
                  <div class="flex items-center gap-3">
                    <Button 
                      v-if="item.children && item.children.length > 0" 
                      @click.stop="toggle(item.id)" 
                      variant="ghost" 
                      size="icon" 
                      class="h-6 w-6 text-slate-500 hover:bg-slate-800 rounded-lg shrink-0"
                    >
                      <ChevronRight class="h-4 w-4 transition-transform duration-200" :class="{'rotate-90 text-indigo-400': expanded[item.id]}" />
                    </Button>
                    <div v-else class="w-6 h-6 flex items-center justify-center shrink-0">
                      <div class="w-1.5 h-1.5 rounded-full bg-slate-700"></div>
                    </div>
                    <div>
                      <div class="text-slate-100 group-hover:text-indigo-300 transition-colors leading-tight font-black uppercase tracking-tight text-xs flex items-center gap-2">
                        <span>{{ item.name }}</span>
                        <Badge 
                          variant="outline" 
                          class="text-[7.5px] font-black uppercase tracking-widest px-3 py-1 rounded-full border-slate-700 text-indigo-400 bg-indigo-500/10 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm"
                        >
                          {{ item.itemType || type }}
                        </Badge>
                      </div>
                      <div class="text-[10px] text-slate-400 font-bold uppercase mt-0.5 tracking-wider">
                        {{ item.displayName || item.customIdentifier || item.hostname || '' }}
                      </div>
                      <div class="text-[9px] text-slate-500 font-mono tracking-tight mt-0.5">
                        SN: {{ item.serialNumber || 'UNTRACKED' }}
                      </div>
                    </div>
                  </div>
                </TableCell>

                <!-- Manufacturer -->
                <TableCell v-if="columns.manufacturer" class="px-6 py-4 text-xs font-bold uppercase text-slate-300">
                  {{ item.manufacturer?.name || (typeof item.manufacturer === 'string' ? item.manufacturer : 'N/A') }}
                </TableCell>

                <!-- Teams -->
                <TableCell class="px-6 py-4">
                  <div class="flex flex-wrap gap-1.5">
                    <Badge 
                      v-for="team in item.responsibleTeams" 
                      :key="team.id || team.name" 
                      variant="secondary" 
                      class="bg-indigo-950/40 text-indigo-300 border border-indigo-800/40 text-[8px] uppercase font-black tracking-wider px-3 py-1 rounded-full inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm"
                    >
                      {{ team.name || team }}
                    </Badge>
                    <Badge v-if="!item.responsibleTeams?.length" variant="outline" class="text-slate-600 border-slate-800 text-[8px] rounded-full px-3 py-1">
                      UNASSIGNED
                    </Badge>
                  </div>
                </TableCell>

                <!-- Specs -->
                <TableCell v-if="columns.specs" class="px-6 py-4">
                  <div class="flex flex-wrap gap-1.5 max-w-xs">
                    <span v-if="item.metadata?.Power" class="text-[8px] font-black uppercase tracking-wider border border-amber-900/30 bg-amber-900/10 px-3 py-1 rounded-full text-amber-400 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">
                      {{ item.metadata.Power }}
                    </span>
                    <span v-if="item.metadata?.Voltage" class="text-[8px] font-black uppercase tracking-wider border border-blue-900/30 bg-blue-900/10 px-3 py-1 rounded-full text-blue-400 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">
                      {{ item.metadata.Voltage }}
                    </span>
                    <span v-if="item.metadata?.Resolution" class="text-[8px] font-black uppercase tracking-wider border border-purple-900/30 bg-purple-900/10 px-3 py-1 rounded-full text-purple-400 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">
                      {{ item.metadata.Resolution }}
                    </span>
                    <span v-if="item.metadata?.Version || item.version" class="text-[8px] font-black uppercase tracking-wider border border-emerald-900/30 bg-emerald-900/10 px-3 py-1 rounded-full text-emerald-400 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">
                      v{{ item.metadata?.Version || item.version }}
                    </span>
                    <span v-if="item.modelNumber" class="text-[8px] font-black uppercase tracking-wider border border-slate-700 bg-slate-800 px-3 py-1 rounded-full text-slate-400 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">
                      {{ item.modelNumber }}
                    </span>
                  </div>
                </TableCell>

                <!-- Tags / Metadata -->
                <TableCell v-if="columns.tags" class="px-6 py-4">
                  <div class="flex flex-wrap gap-1.5 max-w-xs">
                    <template v-for="(val, key) in item.metadata" :key="key">
                      <span v-if="!['Power', 'Voltage', 'Resolution', 'Version'].includes(key as string)" class="px-3 py-1 rounded-full bg-slate-950 text-[8px] font-bold uppercase tracking-wider text-slate-400 border border-slate-800 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">
                        {{ key }}: {{ val }}
                      </span>
                    </template>
                  </div>
                </TableCell>

                <!-- Purchase Date -->
                <TableCell v-if="columns.purchaseDate" class="px-6 py-4 text-xs font-mono text-slate-400">
                  {{ item.purchaseDate ? new Date(item.purchaseDate).toLocaleDateString() : '-' }}
                </TableCell>

                <!-- Cost -->
                <TableCell v-if="columns.cost" class="px-6 py-4 text-right">
                  <div class="text-xs font-black text-slate-100 font-mono">
                    {{ formatCurrency(item.costInHUF) }}
                    <span v-if="item.costInHUF || item.costInHUF === 0" class="text-[9px] text-slate-500 font-sans ml-1">HUF</span>
                  </div>
                </TableCell>

                <!-- Edit Action -->
                <TableCell class="pr-6 text-right">
                  <div class="p-1.5 rounded-lg text-slate-600 group-hover:text-indigo-400 group-hover:bg-slate-800 transition-all inline-flex items-center justify-center">
                    <Edit3 class="w-3.5 h-3.5" />
                  </div>
                </TableCell>
              </TableRow>

              <!-- Child Rows -->
              <TableRow v-if="expanded[item.id] && item.children && item.children.length > 0" class="bg-slate-950/70 border-b border-slate-800 last:border-0">
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
