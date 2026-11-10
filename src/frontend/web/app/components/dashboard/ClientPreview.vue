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
  <Card class="bg-card border-border shadow-sm overflow-hidden rounded-xl">
    <CardHeader class="px-6 py-4 border-b border-border flex flex-row justify-between items-center bg-muted/30 space-y-0">
      <CardTitle class="text-base font-semibold text-foreground flex items-center gap-2.5">
        <Monitor class="w-5 h-5 text-primary" />
        <span>Active Edge Controllers</span>
      </CardTitle>
      <NuxtLink to="/dashboard/clients" class="no-underline">
        <Button 
          variant="outline"
          size="sm"
          class="flex items-center gap-1.5 rounded-lg border-border bg-card text-xs font-medium text-foreground hover:bg-accent transition-all h-8"
        >
          <span>View Fleet</span>
          <ArrowUpRight class="h-3.5 w-3.5 text-primary" />
        </Button>
      </NuxtLink>
    </CardHeader>
    <CardContent class="p-0">
      <div class="overflow-x-auto">
        <Table>
          <TableHeader class="bg-muted/50">
            <TableRow class="border-b border-border hover:bg-transparent">
              <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold">Controller / Host</TableHead>
              <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold">Telemetry Status</TableHead>
              <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold text-right">Heartbeat</TableHead>
              <TableHead class="w-10"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody class="divide-y divide-border">
            <template v-if="clients.length === 0">
              <TableRow>
                <TableCell colspan="4" class="h-28 text-center text-muted-foreground text-xs">
                  No edge controllers active at this time.
                </TableCell>
              </TableRow>
            </template>
            <template v-else>
              <TableRow 
                v-for="client in clients" 
                :key="client.id" 
                @click="navigateToClient(client)"
                class="hover:bg-muted/40 transition-colors border-border cursor-pointer group"
              >
                <!-- Host & OS -->
                <TableCell class="px-4 py-3">
                  <div class="flex items-center gap-3">
                    <div class="w-2 h-2 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(52,211,153,0.8)] animate-pulse"></div>
                    <div>
                      <div class="font-medium text-foreground group-hover:text-primary transition-colors text-xs flex items-center gap-2">
                        <span>{{ client.hostname }}</span>
                        <span class="text-[10px] px-1.5 py-0.5 rounded bg-muted border border-border text-muted-foreground">
                          {{ client.os.includes('Win') ? 'Windows' : client.os.includes('Ubuntu') ? 'Linux' : client.os }}
                        </span>
                      </div>
                      <div class="text-[11px] text-muted-foreground font-mono flex items-center gap-1.5 mt-0.5">
                        <span>UUID: {{ client.id.substring(0, 8) }}</span>
                        <span class="w-1 h-1 rounded-full bg-border"></span>
                        <span class="text-muted-foreground">{{ client.ip || '10.0.1.x' }}</span>
                      </div>
                    </div>
                  </div>
                </TableCell>

                <!-- Status Pill -->
                <TableCell class="px-4 py-3">
                  <span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-500/15 text-emerald-600 dark:text-emerald-300 border border-emerald-500/30">
                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                    Online
                  </span>
                </TableCell>

                <!-- Last Seen Telemetry -->
                <TableCell class="px-4 py-3 text-right">
                  <div class="text-xs font-mono text-foreground">{{ client.lastSeen }}</div>
                  <div class="text-[11px] text-muted-foreground font-mono mt-0.5">Streaming active</div>
                </TableCell>

                <!-- Action Chevron -->
                <TableCell class="pr-4 text-right">
                  <ChevronRight class="w-4 h-4 text-muted-foreground group-hover:text-foreground transition-colors" />
                </TableCell>
              </TableRow>
            </template>
          </TableBody>
        </Table>
      </div>
    </CardContent>
  </Card>
</template>
