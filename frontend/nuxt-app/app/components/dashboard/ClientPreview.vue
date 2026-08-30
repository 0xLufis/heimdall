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
  <Card class="bg-card border-border shadow-sm overflow-hidden rounded-xl">
    <CardHeader class="px-6 py-4 border-b border-border flex flex-row justify-between items-center bg-muted/30 space-y-0">
      <CardTitle class="text-sm font-bold text-foreground uppercase tracking-wider flex items-center gap-2">
        <span class="w-2 h-2 bg-primary rounded-full"></span>
        Active Edge Controllers
      </CardTitle>
      <NuxtLink to="/dashboard/clients" class="no-underline">
        <Button 
          variant="outline"
          class="flex items-center gap-1 px-3 py-1 rounded-lg bg-background text-[10px] font-semibold text-muted-foreground hover:text-foreground transition-colors uppercase tracking-wider border border-border h-auto"
        >
          View Fleet
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
          </svg>
        </Button>
      </NuxtLink>
    </CardHeader>
    <CardContent class="p-0">
      <div class="overflow-x-auto">
        <Table>
          <TableHeader class="bg-muted/40">
            <TableRow class="border-border hover:bg-transparent">
              <TableHead class="px-6 py-3 text-[10px] text-muted-foreground uppercase tracking-wider font-semibold h-auto">Endpoint</TableHead>
              <TableHead class="px-6 py-3 text-[10px] text-muted-foreground uppercase tracking-wider font-semibold h-auto">Status</TableHead>
              <TableHead class="px-6 py-3 text-[10px] text-muted-foreground uppercase tracking-wider font-semibold h-auto text-right">Last Telemetry</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody class="divide-y divide-border">
            <TableRow v-for="client in clients" :key="client.id" class="hover:bg-muted/30 transition-colors border-border">
              <TableCell class="px-6 py-4">
                <div class="font-bold text-foreground">{{ client.hostname }}</div>
                <div class="text-[10px] text-muted-foreground font-mono flex items-center gap-1.5 mt-0.5">
                  <span>{{ client.id.substring(0, 8) }}</span>
                  <span class="w-1 h-1 rounded-full bg-border"></span>
                  <span class="font-mono">{{ client.os }}</span>
                </div>
              </TableCell>
              <TableCell class="px-6 py-4">
                <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded text-[10px] font-mono uppercase tracking-wider bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">
                  <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                  ONLINE
                </span>
              </TableCell>
              <TableCell class="px-6 py-4 text-right">
                <div class="text-xs font-mono font-medium text-foreground">{{ client.lastSeen }}</div>
                <div class="text-[10px] text-muted-foreground font-mono uppercase">Heartbeat Active</div>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </CardContent>
  </Card>
</template>
