<script setup lang="ts">
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table'

defineProps<{
  items: any[]
  type: 'hardware' | 'software'
  loading?: boolean
}>()
</script>

<template>
  <Card class="border-gray-100 shadow-sm overflow-hidden">
    <CardHeader class="px-8 py-6 border-b border-gray-50 bg-gray-50/30 flex flex-row justify-between items-center space-y-0">
      <CardTitle class="text-lg font-black text-gray-900 uppercase tracking-tight flex items-center gap-2">
        <span :class="type === 'hardware' ? 'bg-indigo-500' : 'bg-purple-500'" class="w-2 h-4 rounded-full"></span>
        {{ type === 'hardware' ? 'Hardware Components' : 'Software Components' }}
      </CardTitle>
      <div v-if="items.length > 0" class="text-[10px] font-black text-gray-400 uppercase tracking-widest">
        Total: {{ items.length }} Records
      </div>
    </CardHeader>
    <CardContent class="p-0">
      <Table>
        <TableHeader class="bg-gray-50/50">
          <TableRow>
            <TableHead class="px-8 py-4 uppercase tracking-widest font-black text-[10px]">Name</TableHead>
            <TableHead class="px-8 py-4 uppercase tracking-widest font-black text-[10px]">Manufacturer</TableHead>
            <TableHead class="px-8 py-4 uppercase tracking-widest font-black text-[10px]">Category</TableHead>
            <TableHead class="px-8 py-4 uppercase tracking-widest font-black text-[10px]">Tech Specs</TableHead>
            <TableHead class="px-8 py-4 text-right uppercase tracking-widest font-black text-[10px]">Cost</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-if="loading">
            <TableRow v-for="i in 3" :key="'loading-'+i">
              <TableCell colspan="5" class="px-8 py-8 text-center">
                <div class="flex flex-col items-center gap-2 animate-pulse">
                    <div class="h-4 w-48 bg-gray-100 rounded"></div>
                    <div class="h-3 w-32 bg-gray-50 rounded"></div>
                </div>
              </TableCell>
            </TableRow>
          </template>
          
          <TableRow v-else-if="items.length === 0">
            <TableCell colspan="5" class="px-8 py-16 text-center">
               <div class="flex flex-col items-center gap-3 opacity-40">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
                  </svg>
                  <p class="text-sm font-black text-gray-400 uppercase tracking-widest">No components found matching criteria.</p>
               </div>
            </TableCell>
          </TableRow>
          
          <template v-else>
            <TableRow v-for="item in items" :key="item.id" class="hover:bg-indigo-50/20 transition-colors group border-b border-gray-50 last:border-0">
              <TableCell class="px-8 py-5">
                <div class="font-bold text-gray-900 group-hover:text-indigo-600 transition-colors leading-tight">{{ item.name }}</div>
                <div class="text-[10px] text-gray-400 truncate max-w-[200px] font-mono tracking-tighter mt-1">{{ item.serialNumber || 'NO-SERIAL-MAPPED' }}</div>
              </TableCell>
              <TableCell class="px-8 py-5">
                <span class="inline-flex px-2 py-1 rounded-lg bg-indigo-50 text-[10px] font-black uppercase tracking-widest text-indigo-600 border border-indigo-100/50">
                  {{ item.manufacturer?.name || 'GENERIC-MFG' }}
                </span>
              </TableCell>
              <TableCell class="px-8 py-5">
                <span v-if="item.technicalSpecs?.category" class="px-2 py-1 rounded-md bg-gray-50 text-[10px] font-black uppercase tracking-widest text-gray-500 border border-gray-100">
                  {{ item.technicalSpecs.category }}
                </span>
              </TableCell>
              <TableCell class="px-8 py-5">
                <div v-if="item.technicalSpecs" class="flex flex-wrap gap-1.5 max-w-sm">
                  <span v-if="item.technicalSpecs.interfaceType" class="text-[9px] font-black uppercase tracking-tighter border border-gray-100 bg-white px-2 py-1 rounded shadow-sm text-gray-600">{{ item.technicalSpecs.interfaceType }}</span>
                  <span v-if="item.technicalSpecs.torqueMax" class="text-[9px] font-black uppercase tracking-tighter border border-gray-100 bg-white px-2 py-1 rounded shadow-sm text-gray-600">{{ item.technicalSpecs.torqueMax }} Nm</span>
                  <span v-if="item.technicalSpecs.resolution" class="text-[9px] font-black uppercase tracking-tighter border border-gray-100 bg-white px-2 py-1 rounded shadow-sm text-gray-600">{{ item.technicalSpecs.resolution }}</span>
                  <span v-if="item.technicalSpecs.payloadCapacityKg" class="text-[9px] font-black uppercase tracking-tighter border border-gray-100 bg-white px-2 py-1 rounded shadow-sm text-gray-600">Payload: {{ item.technicalSpecs.payloadCapacityKg }}kg</span>
                  <span v-if="item.technicalSpecs.reachMm" class="text-[9px] font-black uppercase tracking-tighter border border-gray-100 bg-white px-2 py-1 rounded shadow-sm text-gray-600">Reach: {{ item.technicalSpecs.reachMm }}mm</span>
                </div>
              </TableCell>
              <TableCell class="px-8 py-5 text-right">
                <div class="text-sm font-black text-gray-900 leading-none">{{ item.costInHuf ? item.costInHuf.toLocaleString() : '-' }}</div>
                <div v-if="item.costInHuf" class="text-[9px] text-gray-400 font-black uppercase tracking-widest mt-1">HUF</div>
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </CardContent>
  </Card>
</template>
