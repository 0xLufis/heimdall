<script setup lang="ts">
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { ChevronRightIcon } from 'lucide-vue-next'
import { ref } from 'vue'

const props = defineProps<{
  items: any[]
  type: 'hardware' | 'software' | 'hierarchy'
  loading: boolean
  columns: Record<string, boolean>
  isChild?: boolean
}>()

const expanded = ref<Record<string, boolean>>({})

function toggle(id: string) {
  expanded.value[id] = !expanded.value[id]
}

const formatCurrency = (val: any) => {
  if (!val) return '-'
  return new Intl.NumberFormat('hu-HU').format(val)
}
</script>

<template>
  <Card class="border-slate-800 shadow-sm overflow-hidden bg-slate-900/50" :class="{'!bg-transparent !shadow-none !border-none': isChild}">
    <CardHeader v-if="!isChild" class="p-8 border-b border-slate-800">
      <CardTitle class="text-xs font-black text-slate-500 uppercase tracking-[0.3em]">Asset Repository</CardTitle>
    </CardHeader>
    <CardContent class="p-0">
      <Table>
        <TableHeader v-if="!isChild">
          <TableRow class="border-b border-slate-800 hover:bg-transparent">
            <TableHead class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs w-[35%]">Asset Name / ID</TableHead>
            <TableHead v-if="columns.manufacturer" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Manufacturer</TableHead>
            <TableHead class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Team / Owner</TableHead>
            <TableHead v-if="columns.tags" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Attributes</TableHead>
            <TableHead v-if="columns.specs" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Type-Specific</TableHead>
            <TableHead v-if="columns.purchaseDate" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Purchase Date</TableHead>
            <TableHead v-if="columns.cost" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs text-right">Cost (HUF)</TableHead>
          </TableRow>
        </TableHeader>
        
        <TableBody>
          <template v-if="items.length === 0 && !loading && !isChild">
            <TableRow>
              <TableCell colspan="7" class="h-24 text-center text-slate-500 uppercase font-black text-[10px] tracking-widest">
                No assets found matching the criteria.
              </TableCell>
            </TableRow>
          </template>
          <template v-else>
            <template v-for="item in items" :key="item?.id">
              <TableRow v-if="item" class="hover:bg-slate-800/30 transition-colors group border-b border-slate-800 last:border-0" :class="{'bg-slate-950': isChild}">
                <TableCell class="px-8 py-5 font-bold">
                  <div class="flex items-center gap-3">
                     <Button v-if="item.children && item.children.length > 0" @click="toggle(item.id)" variant="ghost" size="icon" class="h-6 w-6 text-slate-500 hover:bg-slate-800">
                      <ChevronRightIcon class="h-4 w-4 transition-transform" :class="{'rotate-90': expanded[item.id]}" />
                    </Button>
                    <div v-else class="w-6 h-6 mr-1.5 flex-shrink-0"></div>
                    <div>
                      <div class="text-slate-100 group-hover:text-white transition-colors leading-tight font-black uppercase tracking-tight">
                        {{ item.name }}
                      </div>
                      <div class="text-[10px] text-slate-400 font-bold uppercase mt-0.5 tracking-wider">
                        {{ item.displayName || item.customIdentifier || item.hostname || '' }}
                      </div>
                      <div class="text-[9px] text-slate-600 truncate max-w-[200px] font-mono tracking-tighter mt-1">
                        {{ item.serialNumber || 'NO-SERIAL' }} 
                        <span v-if="item.itemType" class="ml-2 text-indigo-500/50">[{{ item.itemType }}]</span>
                      </div>
                    </div>
                  </div>
                </TableCell>
                <TableCell v-if="columns.manufacturer" class="px-8 py-5 text-xs font-bold uppercase text-slate-400">
                    {{ item.manufacturer?.name || 'N/A' }}
                </TableCell>
                <TableCell class="px-8 py-5 text-xs font-mono">
                  <div class="flex flex-wrap gap-1">
                    <Badge v-for="team in item.responsibleTeams" :key="team.id" variant="secondary" class="bg-indigo-900/30 text-indigo-400 border-indigo-800/50 text-[9px] uppercase">
                      {{ team.name }}
                    </Badge>
                    <Badge v-if="!item.responsibleTeams?.length" variant="outline" class="text-slate-600 border-slate-800 text-[9px]">
                      UNASSIGNED
                    </Badge>
                  </div>
                </TableCell>
                <TableCell v-if="columns.tags" class="px-8 py-5">
                  <div class="flex flex-wrap gap-1 max-w-xs">
                    <span v-for="(val, key) in item.metadata" :key="key" class="px-1.5 py-0.5 rounded bg-slate-950 text-[9px] font-bold uppercase tracking-tight text-slate-500 border border-slate-800">
                      {{ key }}: {{ val }}
                    </span>
                  </div>
                </TableCell>
                <TableCell v-if="columns.specs" class="px-8 py-5">
                   <div class="flex flex-wrap gap-1.5 max-w-xs">
                    <!-- Class specific fields -->
                    <span v-if="item.modelNumber" class="text-[9px] font-black uppercase border border-slate-700 bg-slate-800 px-2 py-1 rounded text-slate-400">Model: {{ item.modelNumber }}</span>
                    <span v-if="item.version" class="text-[9px] font-black uppercase border border-emerald-900/30 bg-emerald-900/10 px-2 py-1 rounded text-emerald-400">v{{ item.version }}</span>
                    <span v-if="item.capacity" class="text-[9px] font-black uppercase border border-blue-900/30 bg-blue-900/10 px-2 py-1 rounded text-blue-400">{{ item.capacity }}</span>
                  </div>
                </TableCell>
                <TableCell v-if="columns.purchaseDate" class="px-8 py-5 text-xs font-mono text-slate-500">
                    {{ item.purchaseDate ? new Date(item.purchaseDate).toLocaleDateString() : 'N/A' }}
                </TableCell>
                <TableCell v-if="columns.cost" class="px-8 py-5 text-right">
                  <div class="text-sm font-black text-slate-200 leading-none">
                    {{ formatCurrency(item.costInHUF) }}
                    <span v-if="item.costInHUF" class="text-[8px] text-slate-500 ml-1">HUF</span>
                  </div>
                </TableCell>
              </TableRow>
              <TableRow v-if="expanded[item.id] && item.children && item.children.length > 0" class="bg-slate-950/70 border-b border-slate-800 last:border-0">
                <TableCell colspan="7" class="p-0 pl-12">
                   <DashboardInventoryTable :items="item.children" :type="type" :loading="false" :columns="columns" :is-child="true" />
                </TableCell>
              </TableRow>
            </template>
          </template>
        </TableBody>
      </Table>
    </CardContent>
  </Card>
</template>
