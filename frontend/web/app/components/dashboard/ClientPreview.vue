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
  <Card class="bg-slate-900 border-slate-800 shadow-sm overflow-hidden rounded-xl">
    <CardHeader class="px-6 py-4 border-b border-slate-800 flex flex-row justify-between items-center bg-slate-900/80 space-y-0">
      <CardTitle class="text-base font-semibold text-slate-100 flex items-center gap-2.5">
        <Monitor class="w-5 h-5 text-indigo-400" />
        <span>Active Edge Controllers</span>
      </CardTitle>
      <NuxtLink to="/dashboard/clients" class="no-underline">
        <Button 
          variant="outline"
          size="sm"
          class="flex items-center gap-1.5 rounded-lg border-slate-800 bg-slate-950 text-xs font-medium text-slate-300 hover:text-white hover:border-slate-700 transition-all h-8"
        >
          <span>View Fleet</span>
          <ArrowUpRight class="h-3.5 w-3.5 text-indigo-400" />
        </Button>
      </NuxtLink>
    </CardHeader>
    <CardContent class="p-0">
      <div class="overflow-x-auto">
        <Table>
          <TableHeader class="bg-slate-950">
            <TableRow class="border-b border-slate-800 hover:bg-transparent">
              <TableHead class="px-4 py-3 text-xs text-slate-400 uppercase tracking-wider font-semibold">Controller / Host</TableHead>
              <TableHead class="px-4 py-3 text-xs text-slate-400 uppercase tracking-wider font-semibold">Telemetry Status</TableHead>
              <TableHead class="px-4 py-3 text-xs text-slate-400 uppercase tracking-wider font-semibold text-right">Heartbeat</TableHead>
              <TableHead class="w-10"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody class="divide-y divide-slate-800">
            <template v-if="clients.length === 0">
              <TableRow>
                <TableCell colspan="4" class="h-28 text-center text-slate-400 text-xs">
                  No edge controllers active at this time.
                </TableCell>
              </TableRow>
            </template>
            <template v-else>
              <TableRow 
                v-for="client in clients" 
                :key="client.id" 
                @click="navigateToClient(client)"
                class="hover:bg-slate-800/30 transition-colors border-slate-800 cursor-pointer group"
              >
                <!-- Host & OS -->
                <TableCell class="px-4 py-3">
                  <div class="flex items-center gap-3">
                    <div class="w-2 h-2 rounded-full bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.8)] animate-pulse"></div>
                    <div>
                      <div class="font-medium text-slate-200 group-hover:text-indigo-300 transition-colors text-xs flex items-center gap-2">
                        <span>{{ client.hostname }}</span>
                        <span class="text-[10px] px-1.5 py-0.5 rounded bg-slate-950 border border-slate-800 text-slate-400">
                          {{ client.os.includes('Win') ? 'Windows' : client.os.includes('Ubuntu') ? 'Linux' : client.os }}
                        </span>
                      </div>
                      <div class="text-[11px] text-slate-500 font-mono flex items-center gap-1.5 mt-0.5">
                        <span>UUID: {{ client.id.substring(0, 8) }}</span>
                        <span class="w-1 h-1 rounded-full bg-slate-700"></span>
                        <span class="text-slate-400">{{ client.ip || '10.0.1.x' }}</span>
                      </div>
                    </div>
                  </div>
                </TableCell>

                <!-- Status Pill -->
                <TableCell class="px-4 py-3">
                  <span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-500/15 text-emerald-300 border border-emerald-500/30">
                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                    Online
                  </span>
                </TableCell>

                <!-- Last Seen Telemetry -->
                <TableCell class="px-4 py-3 text-right">
                  <div class="text-xs font-mono text-slate-200">{{ client.lastSeen }}</div>
                  <div class="text-[11px] text-slate-400 font-mono mt-0.5">Streaming active</div>
                </TableCell>

                <!-- Action Chevron -->
                <TableCell class="pr-4 text-right">
                  <ChevronRight class="w-4 h-4 text-slate-500 group-hover:text-indigo-400 transition-colors" />
                </TableCell>
              </TableRow>
            </template>
          </TableBody>
        </Table>
      </div>
    </CardContent>
  </Card>
</template>
