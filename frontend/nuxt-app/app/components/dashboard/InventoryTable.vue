<script setup lang="ts">
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { ChevronRightIcon } from 'lucide-vue-next'
import { ref } from 'vue'

const props = defineProps<{
  items: any[]
  type: 'hardware' | 'software'
  loading: boolean
  columns: Record<string, boolean>
  isChild?: boolean
}>()

const expanded = ref<Record<string, boolean>>({})

function toggle(id: string) {
  expanded.value[id] = !expanded.value[id]
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
            <TableHead class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs w-[35%]">Component</TableHead>
            <TableHead v-if="columns.manufacturer" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Manufacturer</TableHead>
            <TableHead v-if="columns.modelNumber" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Model</TableHead>
            <TableHead v-if="columns.tags" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Tags</TableHead>
            <TableHead v-if="columns.specs" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Specs</TableHead>
            <TableHead v-if="columns.purchaseDate" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs">Purchase Date</TableHead>
            <TableHead v-if="columns.cost" class="px-8 py-4 uppercase tracking-widest font-black text-slate-500 text-xs text-right">Cost (HUF)</TableHead>
          </TableRow>
        </TableHeader>
        
        <TableBody>
          <template v-if="loading && !isChild">
            <!-- Loading Skeleton -->
          </template>
          <template v-else-if="items.length === 0 && !isChild">
            <!-- Empty State -->
          </template>
          <template v-else>
            <template v-for="item in items" :key="item.id">
              <TableRow class="hover:bg-slate-800/30 transition-colors group border-b border-slate-800 last:border-0" :class="{'bg-slate-950': isChild}">
                <TableCell class="px-8 py-5 font-bold">
                  <div class="flex items-center gap-3">
                     <Button v-if="item.children && item.children.length > 0" @click="toggle(item.id)" variant="ghost" size="icon" class="h-6 w-6 text-slate-500 hover:bg-slate-800">
                      <ChevronRightIcon class="h-4 w-4 transition-transform" :class="{'rotate-90': expanded[item.id]}" />
                    </Button>
                    <div v-else class="w-6 h-6 mr-1.5 flex-shrink-0"></div>
                    <div>
                      <div class="text-slate-200 group-hover:text-white transition-colors leading-tight">{{ item.name }}</div>
                      <div class="text-[10px] text-slate-500 truncate max-w-[200px] font-mono tracking-tighter mt-1">{{ item.serialNumber || 'N/A' }}</div>
                    </div>
                  </div>
                </TableCell>
                <TableCell v-if="columns.manufacturer" class="px-8 py-5 text-xs font-bold uppercase text-slate-400">
                    {{ item.manufacturer?.name || 'N/A' }}
                </TableCell>
                <TableCell v-if="columns.modelNumber" class="px-8 py-5 text-xs font-mono text-slate-500">{{ item.modelNumber || 'N/A' }}</TableCell>
                <TableCell v-if="columns.tags" class="px-8 py-5">
                  <div class="flex flex-wrap gap-1.5 max-w-xs">
                    <span v-for="tag in item.technicalSpecs?.categories" :key="tag" class="px-2 py-1 rounded-md bg-slate-950 text-[10px] font-black uppercase tracking-widest text-slate-500 border border-slate-800">
                      {{ tag }}
                    </span>
                  </div>
                </TableCell>
                <TableCell v-if="columns.specs" class="px-8 py-5">
                   <div class="flex flex-wrap gap-1.5 max-w-xs">
                    <span v-if="item.technicalSpecs?.interfaceType" class="text-[9px] font-black uppercase tracking-tighter border border-slate-700 bg-slate-800 px-2 py-1 rounded shadow-sm text-slate-400">{{ item.technicalSpecs.interfaceType }}</span>
                    <span v-if="item.technicalSpecs?.torqueMax" class="text-[9px] font-black uppercase tracking-tighter border border-slate-700 bg-slate-800 px-2 py-1 rounded shadow-sm text-slate-400">{{ item.technicalSpecs.torqueMax }} Nm</span>
                  </div>
                </TableCell>
                <TableCell v-if="columns.purchaseDate" class="px-8 py-5 text-xs font-mono text-slate-500">
                    {{ item.purchaseDate ? new Date(item.purchaseDate).toLocaleDateString() : 'N/A' }}
                </TableCell>
                <TableCell v-if="columns.cost" class="px-8 py-5 text-right">
                  <div class="text-sm font-black text-slate-200 leading-none">{{ item.costInHuf ? item.costInHuf.toLocaleString() : '-' }}</div>
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
