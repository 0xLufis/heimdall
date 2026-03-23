<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle } from '~/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '~/components/ui/table'
import { Button } from '~/components/ui/button'

interface Client {
  id: string
  hostname: string
  os: string
  lastSeen: string
}

defineProps<{
  clients: Client[]
}>()
</script>

<template>
  <Card class="bg-slate-900 border-slate-800 shadow-sm overflow-hidden rounded-3xl">
    <CardHeader class="px-8 py-6 border-b border-slate-800 flex flex-row justify-between items-center bg-slate-900/50 space-y-0">
      <CardTitle class="text-sm font-black text-slate-100 uppercase tracking-tight flex items-center gap-2">
        <span class="w-2 h-4 bg-slate-700 rounded-full"></span>
        Active Clients
      </CardTitle>
      <NuxtLink to="/dashboard/clients" class="no-underline">
        <Button 
          variant="outline"
          class="flex items-center gap-1.5 px-4 py-1.5 rounded-full bg-slate-800 text-[10px] font-black text-slate-300 hover:bg-slate-700 hover:text-white transition-all uppercase tracking-widest border border-slate-700 h-auto"
        >
          View All
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 transition-transform group-hover:translate-x-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M9 5l7 7-7 7" />
          </svg>
        </Button>
      </NuxtLink>
    </CardHeader>
    <CardContent class="p-0">
      <div class="overflow-x-auto">
        <Table>
          <TableHeader class="bg-slate-900/80">
            <TableRow class="border-slate-800 hover:bg-transparent">
              <TableHead class="px-8 py-4 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Endpoint</TableHead>
              <TableHead class="px-8 py-4 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Status</TableHead>
              <TableHead class="px-8 py-4 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto text-right">Uptime</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody class="divide-y divide-slate-800">
            <TableRow v-for="client in clients" :key="client.id" class="hover:bg-slate-800/30 transition-colors group border-slate-800">
              <TableCell class="px-8 py-5">
                <div class="font-bold text-slate-200 group-hover:text-white transition-colors">{{ client.hostname }}</div>
                <div class="text-[10px] text-slate-500 font-mono flex items-center gap-1 mt-0.5">
                  <span class="opacity-50 tracking-tighter">{{ client.id }}</span>
                  <span class="w-1 h-1 rounded-full bg-slate-700"></span>
                  <span class="font-bold text-slate-600">{{ client.os }}</span>
                </div>
              </TableCell>
              <TableCell class="px-8 py-5">
                <span class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-[10px] font-black uppercase tracking-wider bg-slate-800 text-emerald-500 border border-slate-700 shadow-sm">
                  <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                  Operational
                </span>
              </TableCell>
              <TableCell class="px-8 py-5 text-right">
                <div class="text-sm font-black text-slate-200">{{ client.lastSeen }}</div>
                <div class="text-[10px] text-slate-600 font-bold uppercase">Signal Stable</div>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </CardContent>
  </Card>
</template>
