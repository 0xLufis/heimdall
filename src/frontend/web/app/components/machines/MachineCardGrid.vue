<script setup lang="ts">
import { computed } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import {
  Monitor,
  FolderTree,
  Archive,
  ChevronRight,
  Wifi,
  WifiOff,
  Wrench,
  CheckCircle2,
  AlertOctagon
} from 'lucide-vue-next'

const props = defineProps<{
  machines: any[]
  loading?: boolean
}>()

const emit = defineEmits<{
  'openTree': [stationId: string]
  'openQuickView': [controller: any]
  'openDecommission': [machine: any]
  'contextMenu': [machine: any, event: MouseEvent]
}>()

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'active':
      return { label: 'Operational', class: 'border-emerald-500/30 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10' }
    case 'maintenance':
      return { label: 'In Maintenance', class: 'border-amber-500/30 text-amber-600 dark:text-amber-400 bg-amber-500/10' }
    case 'inactive':
      return { label: 'Inactive / Standby', class: 'border-zinc-500/30 text-zinc-400 bg-zinc-800/40' }
    case 'decommissioned':
      return { label: 'Decommissioned', class: 'border-rose-500/30 text-rose-500 bg-rose-500/10' }
    default:
      return { label: status || 'Unknown', class: 'border-border text-muted-foreground' }
  }
}
</script>

<template>
  <div>
    <!-- Loading skeleton -->
    <div v-if="loading" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <div v-for="i in 6" :key="i" class="h-44 rounded-xl border border-border bg-card animate-pulse" />
    </div>

    <!-- Empty state -->
    <div v-else-if="machines.length === 0" class="text-center py-12 border border-dashed border-border rounded-xl">
      <p class="text-xs text-muted-foreground font-mono">No machine stations match the current filter criteria.</p>
    </div>

    <!-- Card Grid -->
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <Card
        v-for="m in machines"
        :key="m.id"
        @contextmenu.prevent="emit('contextMenu', m, $event)"
        class="border border-border bg-card shadow-xs hover:border-primary/40 transition-all flex flex-col justify-between group"
      >
        <CardHeader class="pb-2 pt-4 px-4">
          <div class="flex items-start justify-between gap-2">
            <div>
              <div class="flex items-center gap-1.5">
                <span class="text-[10px] font-mono text-cyan-500 uppercase tracking-wider font-semibold">
                  {{ m.customIdentifier || m.id?.substring(0, 8) }}
                </span>
                <span v-if="m.groupId" class="text-[10px] font-mono text-muted-foreground">
                  • {{ m.groupId }}
                </span>
              </div>
              <CardTitle class="text-sm font-bold text-foreground mt-0.5 group-hover:text-primary transition-colors">
                {{ m.displayName || m.name }}
              </CardTitle>
            </div>

            <Badge variant="outline" :class="getStatusBadge(m.status).class" class="text-[9px] px-1.5 py-0 font-mono shrink-0">
              {{ getStatusBadge(m.status).label }}
            </Badge>
          </div>
        </CardHeader>

        <CardContent class="pt-0 pb-4 px-4 space-y-3">
          <!-- Controller Info -->
          <div v-if="m.controllers && m.controllers.length > 0" class="space-y-1.5 bg-muted/20 p-2.5 rounded-lg border border-border/50">
            <div class="flex items-center justify-between text-[11px]">
              <span class="text-muted-foreground font-medium flex items-center gap-1.5">
                <Monitor class="w-3.5 h-3.5 text-muted-foreground" />
                <span>{{ m.controllers[0].hostname }}</span>
              </span>
              <button
                type="button"
                @click="emit('openQuickView', m.controllers[0])"
                class="text-[10px] font-mono text-primary hover:underline cursor-pointer"
              >
                Inspect
              </button>
            </div>
            <div v-if="m.controllers[0].ipAddress" class="text-[10px] font-mono text-muted-foreground">
              IP: {{ m.controllers[0].ipAddress }}
            </div>
          </div>

          <div v-else class="text-[11px] text-muted-foreground/70 italic py-1">
            No dedicated controller paired
          </div>

          <!-- Actions Footer -->
          <div class="flex items-center justify-between pt-1 border-t border-border/40 text-xs">
            <div class="flex items-center gap-2">
              <Button
                variant="ghost"
                size="sm"
                @click="emit('openTree', m.id)"
                class="h-7 px-2 text-[11px] text-muted-foreground hover:text-foreground gap-1 cursor-pointer"
                title="Explore subcomponent BOM"
              >
                <FolderTree class="w-3 h-3 text-cyan-400" />
                <span>BOM Tree</span>
              </Button>

              <NuxtLink
                :to="`/terminal/machine/${m.id}`"
                target="_blank"
                class="inline-flex items-center h-7 px-2 text-[11px] text-muted-foreground hover:text-foreground gap-1"
                title="Open touch kiosk terminal"
              >
                <Wrench class="w-3 h-3 text-amber-400" />
                <span>Kiosk</span>
              </NuxtLink>
            </div>

            <Button
              v-if="m.status !== 'decommissioned'"
              variant="ghost"
              size="sm"
              @click="emit('openDecommission', m)"
              class="h-7 px-2 text-[11px] text-muted-foreground hover:text-rose-500 cursor-pointer"
              title="Decommission machine"
            >
              <Archive class="w-3 h-3" />
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
