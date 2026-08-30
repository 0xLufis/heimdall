<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '~/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '~/components/ui/table'
import { Button } from '~/components/ui/button'
import { Monitor, ChevronRight, Activity, ArrowUpRight } from 'lucide-vue-next'

interface Client {
  id: string
  hostname: string
  os: string
  lastSeen: string
  ip?: string
  cpuLoad?: number | string
  ramUsage?: number | string
}

const props = defineProps<{
  clients: Client[]
}>()

const router = useRouter()

function navigateToClient(client: Client) {
  router.push({
    path: '/dashboard/clients',
    query: { selected: client.id, hostname: client.hostname }
  })
}
</script>

<template>
  <Card class="bg-slate-900/60 border-slate-800 shadow-xl overflow-hidden rounded-3xl">
    <CardHeader class="px-6 py-4 border-b border-slate-800 flex flex-row justify-between items-center bg-slate-900/80 space-y-0">
      <CardTitle class="text-xs font-black text-slate-200 uppercase tracking-[0.2em] flex items-center gap-2.5">
        <div class="p-1.5 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
          <Monitor class="w-4 h-4" />
        </div>
        <span>Active Edge Controllers</span>
      </CardTitle>
      <NuxtLink to="/dashboard/clients" class="no-underline">
        <Button 
          variant="outline"
          class="flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl bg-slate-950 text-[10px] font-black text-slate-300 hover:text-white hover:border-indigo-500/40 transition-all uppercase tracking-wider border border-slate-800 h-auto group"
        >
          <span>View Fleet</span>
          <ArrowUpRight class="h-3.5 w-3.5 text-indigo-400 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform" />
        </Button>
      </NuxtLink>
    </CardHeader>
    <CardContent class="p-0">
      <div class="overflow-x-auto">
        <Table>
          <TableHeader class="bg-slate-950/60">
            <TableRow class="border-b border-slate-800 hover:bg-transparent">
              <TableHead class="px-6 py-3.5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Controller / Host</TableHead>
              <TableHead class="px-6 py-3.5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Telemetry State</TableHead>
              <TableHead class="px-6 py-3.5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto text-right">Activity</TableHead>
              <TableHead class="w-10"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody class="divide-y divide-slate-800/60">
            <template v-if="clients.length === 0">
              <TableRow>
                <TableCell colspan="4" class="h-28 text-center text-slate-500 uppercase font-black text-xs tracking-widest">
                  No edge controllers active
                </TableCell>
              </TableRow>
            </template>
            <template v-else>
              <TableRow 
                v-for="client in clients" 
                :key="client.id" 
                @click="navigateToClient(client)"
                class="hover:bg-slate-800/40 transition-colors border-slate-800/60 cursor-pointer group"
              >
                <!-- Host & OS -->
                <TableCell class="px-6 py-4">
                  <div class="flex items-center gap-3">
                    <div class="w-2 h-2 rounded-full bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.8)] animate-pulse"></div>
                    <div>
                      <div class="font-black text-slate-100 group-hover:text-indigo-300 transition-colors uppercase tracking-tight text-xs flex items-center gap-2">
                        <span>{{ client.hostname }}</span>
                        <span class="text-[8px] font-black uppercase tracking-wider px-1.5 py-0.5 rounded bg-slate-950 border border-slate-800 text-slate-400">
                          {{ client.os.includes('Win') ? 'Windows' : client.os.includes('Ubuntu') ? 'Linux' : client.os }}
                        </span>
                      </div>
                      <div class="text-[10px] text-slate-500 font-mono flex items-center gap-1.5 mt-0.5">
                        <span>UUID: {{ client.id.substring(0, 8) }}</span>
                        <span class="w-1 h-1 rounded-full bg-slate-700"></span>
                        <span class="text-slate-400 font-semibold">{{ client.ip || '10.0.1.x' }}</span>
                      </div>
                    </div>
                  </div>
                </TableCell>

                <!-- Status Pill -->
                <TableCell class="px-6 py-4">
                  <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[9px] font-mono font-black uppercase tracking-wider bg-emerald-950/40 text-emerald-400 border border-emerald-500/20">
                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                    ONLINE
                  </span>
                </TableCell>

                <!-- Last Seen Telemetry -->
                <TableCell class="px-6 py-4 text-right">
                  <div class="text-xs font-mono font-black text-slate-200">{{ client.lastSeen }}</div>
                  <div class="text-[9px] text-indigo-400 font-mono uppercase tracking-wider mt-0.5">Heartbeat Streaming</div>
                </TableCell>

                <!-- Action Chevron -->
                <TableCell class="pr-6 text-right">
                  <ChevronRight class="w-4 h-4 text-slate-600 group-hover:text-indigo-400 group-hover:translate-x-0.5 transition-all" />
                </TableCell>
              </TableRow>
            </template>
          </TableBody>
        </Table>
      </div>
    </CardContent>
  </Card>
</template>
