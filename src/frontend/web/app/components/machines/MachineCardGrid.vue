<script setup lang="ts">
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import {
  Monitor,
  FolderTree,
  Archive,
  Wrench,
  Box,
  Activity,
  Layers,
  User,
  Ticket
} from 'lucide-vue-next'
import { ref } from 'vue'
import { useIntervalFn } from '@vueuse/core'
import { useStationTelemetry, type StationTelemetryKpi } from '~/composables/useStationTelemetry'

const props = defineProps<{
  machines: any[]
  loading?: boolean
}>()

const emit = defineEmits<{
  /** The card itself was activated (click / Enter / Space): open the station's detail view. */
  'open': [machine: any]
  'openTree': [stationId: string]
  'open3d': [machine: any]
  'openQuickView': [controller: any, machine: any]
  'openDecommission': [machine: any]
  'contextMenu': [machine: any, event: MouseEvent]
}>()

const { getStationKpi } = useStationTelemetry()

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'active':
      return { label: 'Operational', class: 'border-emerald-500/30 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10' }
    case 'maintenance':
      return { label: 'In Maintenance', class: 'border-amber-500/30 text-amber-600 dark:text-amber-400 bg-amber-500/10' }
    case 'inactive':
      return { label: 'Inactive / Standby', class: 'border-border text-muted-foreground bg-muted/40' }
    case 'decommissioned':
    case 'dismantled':
      return { label: 'Decommissioned', class: 'border-rose-500/30 text-rose-500 bg-rose-500/10' }
    default:
      return { label: status || 'Unknown', class: 'border-border text-muted-foreground' }
  }
}

const LIVE_STATE: Record<string, { label: string; dot: string }> = {
  RUNNING: { label: 'Running', dot: 'bg-emerald-500' },
  IDLE: { label: 'Idle', dot: 'bg-sky-500' },
  WARNING: { label: 'Warning', dot: 'bg-amber-500' },
  FAULTED: { label: 'Faulted', dot: 'bg-rose-500' },
  MAINTENANCE: { label: 'Maintenance', dot: 'bg-violet-500' }
}

/** Live telemetry of a station, looked up by machine id, floor-plan handle, then identifier. */
const kpiFor = (m: any): StationTelemetryKpi | null =>
  (m?.id && getStationKpi(String(m.id))) ||
  (m?.pinnedObjectHandle && getStationKpi(m.pinnedObjectHandle)) ||
  (m?.customIdentifier && getStationKpi(m.customIdentifier)) ||
  null

const liveState = (kpi: StationTelemetryKpi | null) => (kpi ? LIVE_STATE[kpi.operationalStatus] ?? null : null)

// A card re-renders when its own station reports; this 15 s clock keeps "x min ago" moving for a
// station that went silent (which would otherwise keep saying "just now"). Not useNow(): its
// default scheduler is requestAnimationFrame, which re-rendered every card on every frame.
const now = ref(Date.now())
useIntervalFn(() => { now.value = Date.now() }, 15_000)

const lastSample = (kpi: StationTelemetryKpi | null) => {
  if (!kpi?.lastTelemetryAt) return null
  const ms = now.value - Date.parse(kpi.lastTelemetryAt)
  if (!Number.isFinite(ms)) return null
  if (ms < 60_000) return 'just now'
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)} min ago`
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)} h ago`
  return `${Math.floor(ms / 86_400_000)} d ago`
}

const lineLabel = (m: any) => m.lineGroupName || m.groupId || null

/** Keyboard activation of the card; keys pressed on inner controls are theirs. */
const onCardKey = (m: any, e: KeyboardEvent) => {
  if (e.target !== e.currentTarget) return
  e.preventDefault()
  emit('open', m)
}
</script>

<template>
  <div>
    <!-- Loading skeleton -->
    <div v-if="loading" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <div v-for="i in 6" :key="i" class="h-52 rounded-xl border border-border bg-card animate-pulse" />
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
        role="button"
        tabindex="0"
        :aria-label="`Open station ${m.displayName || m.name}`"
        class="border border-border bg-card shadow-xs hover:border-primary/50 hover:shadow-md transition-all flex flex-col justify-between group cursor-pointer focus:outline-hidden focus-visible:ring-2 focus-visible:ring-primary/50"
        @click="emit('open', m)"
        @keydown.enter="onCardKey(m, $event)"
        @keydown.space="onCardKey(m, $event)"
        @contextmenu="emit('contextMenu', m, $event)"
      >
        <CardHeader class="pb-2 pt-4 px-4">
          <div class="flex items-start justify-between gap-2">
            <div class="min-w-0">
              <div class="flex items-center gap-1.5 flex-wrap">
                <span class="text-[10px] font-mono text-cyan-600 dark:text-cyan-400 uppercase tracking-wider font-semibold">
                  {{ m.customIdentifier || m.id?.substring(0, 8) }}
                </span>
                <span v-if="lineLabel(m)" class="text-[10px] font-mono text-muted-foreground inline-flex items-center gap-1">
                  <Layers class="w-3 h-3" />{{ lineLabel(m) }}
                </span>
              </div>
              <CardTitle class="text-sm font-bold text-foreground mt-0.5 group-hover:text-primary transition-colors truncate">
                {{ m.displayName || m.name }}
              </CardTitle>
              <div class="text-[10px] text-muted-foreground mt-0.5 truncate">
                {{ m.machineType || 'Type not set' }}
                <span v-if="m.subMachines?.length"> · {{ m.subMachines.length }} sub-machine{{ m.subMachines.length === 1 ? '' : 's' }}</span>
              </div>
            </div>

            <Badge variant="outline" :class="getStatusBadge(m.status).class" class="text-[9px] px-1.5 py-0 font-mono shrink-0">
              {{ getStatusBadge(m.status).label }}
            </Badge>
          </div>
        </CardHeader>

        <CardContent class="pt-0 pb-4 px-4 space-y-3">
          <!-- Live telemetry summary (only what the station's PLC reported) -->
          <div class="grid grid-cols-3 gap-2 p-2.5 rounded-lg bg-muted/30 border border-border/60 text-center">
            <div>
              <div class="text-[9px] uppercase font-semibold text-muted-foreground">State</div>
              <div class="text-[11px] font-semibold text-foreground mt-0.5 flex items-center justify-center gap-1">
                <template v-if="liveState(kpiFor(m))">
                  <span class="size-2 rounded-full" :class="liveState(kpiFor(m))!.dot" />
                  <span>{{ liveState(kpiFor(m))!.label }}</span>
                </template>
                <span v-else class="text-muted-foreground font-normal">No telemetry</span>
              </div>
            </div>
            <div class="border-x border-border/60">
              <div class="text-[9px] uppercase font-semibold text-muted-foreground">OEE</div>
              <div class="text-[11px] font-mono font-semibold text-foreground mt-0.5">
                {{ kpiFor(m)?.oee != null ? `${kpiFor(m)!.oee!.toFixed(0)} %` : '—' }}
              </div>
            </div>
            <div>
              <div class="text-[9px] uppercase font-semibold text-muted-foreground">Last sample</div>
              <div class="text-[11px] font-mono font-semibold text-foreground mt-0.5">{{ lastSample(kpiFor(m)) || '—' }}</div>
            </div>
          </div>

          <!-- Open tickets -->
          <NuxtLink
            v-if="kpiFor(m)?.activeTicketCount"
            :to="{ path: '/dashboard/tickets', query: { stationId: m.id, handle: kpiFor(m)!.handle } }"
            class="flex items-center gap-1.5 text-[11px] font-medium hover:underline"
            :class="kpiFor(m)!.hasSlaBreach || kpiFor(m)!.criticalTicketCount > 0 ? 'text-rose-600 dark:text-rose-400' : 'text-amber-600 dark:text-amber-400'"
            @click.stop
          >
            <Ticket class="w-3.5 h-3.5" />
            <span>
              {{ kpiFor(m)!.activeTicketCount }} open ticket{{ kpiFor(m)!.activeTicketCount === 1 ? '' : 's' }}
              <template v-if="kpiFor(m)!.hasSlaBreach"> · SLA breached</template>
              <template v-else-if="kpiFor(m)!.criticalTicketCount > 0"> · {{ kpiFor(m)!.criticalTicketCount }} critical</template>
            </span>
          </NuxtLink>

          <!-- Controllers bound to this station (each opens its own diagnostics) -->
          <div v-if="m.controllers && m.controllers.length > 0" class="flex flex-wrap gap-1.5">
            <button
              v-for="c in m.controllers"
              :key="c.id || c.hostname"
              v-ctx="{ entityType: 'controller', entityId: c.id, entityName: c.hostname || c.name, machineId: m.id, machineName: m.name }"
              type="button"
              class="inline-flex items-center gap-1.5 px-2 py-1 rounded-md bg-muted/40 border border-border/60 text-[11px] font-mono text-foreground hover:border-primary/50 hover:text-primary transition-colors cursor-pointer max-w-full"
              :title="`Inspect controller ${c.hostname || c.name}`"
              @click.stop="emit('openQuickView', c, m)"
            >
              <Monitor class="w-3.5 h-3.5 text-muted-foreground shrink-0" />
              <span class="truncate">{{ c.hostname || c.name }}</span>
            </button>
          </div>
          <div v-else class="text-[11px] text-muted-foreground/80 italic">
            No controller bound to this station
          </div>

          <div v-if="m.spoc || m.owner || m.preferredTechnicianName" class="text-[10px] text-muted-foreground flex items-center gap-1 truncate">
            <User class="w-3 h-3 shrink-0" />
            <span class="truncate">{{ m.spoc || m.preferredTechnicianName || m.owner }}</span>
          </div>

          <!-- Actions Footer -->
          <div class="flex items-center justify-between pt-1 border-t border-border/40 text-xs">
            <div class="flex items-center gap-0.5 flex-wrap">
              <Button
                variant="ghost"
                size="sm"
                class="h-7 px-2 text-[11px] text-muted-foreground hover:text-foreground gap-1 cursor-pointer"
                title="Explore hardware topology and BOM"
                @click.stop="emit('openTree', m.id)"
              >
                <FolderTree class="w-3 h-3 text-cyan-500" />
                <span>BOM</span>
              </Button>

              <Button
                variant="ghost"
                size="sm"
                class="h-7 px-2 text-[11px] text-muted-foreground hover:text-foreground gap-1 cursor-pointer"
                title="Open the station's 3D digital twin"
                @click.stop="emit('open3d', m)"
              >
                <Box class="w-3 h-3 text-cyan-500" />
                <span>3D</span>
              </Button>

              <NuxtLink
                :to="{ path: '/dashboard/telemetry', query: { station: m.id } }"
                class="inline-flex items-center h-7 px-2 rounded-md text-[11px] text-muted-foreground hover:text-foreground hover:bg-accent gap-1"
                title="Open live telemetry for this station"
                @click.stop
              >
                <Activity class="w-3 h-3 text-emerald-500" />
                <span>Live</span>
              </NuxtLink>

              <NuxtLink
                :to="`/terminal/machine/${m.id}`"
                target="_blank"
                class="inline-flex items-center h-7 px-2 rounded-md text-[11px] text-muted-foreground hover:text-foreground hover:bg-accent gap-1"
                title="Open touch kiosk terminal in a new tab"
                @click.stop
              >
                <Wrench class="w-3 h-3 text-amber-500" />
                <span>Kiosk</span>
              </NuxtLink>
            </div>

            <Button
              v-if="m.status !== 'decommissioned' && m.status !== 'dismantled'"
              variant="ghost"
              size="sm"
              class="h-7 px-2 text-[11px] text-muted-foreground hover:text-rose-500 cursor-pointer"
              title="Decommission machine"
              aria-label="Decommission machine"
              @click.stop="emit('openDecommission', m)"
            >
              <Archive class="w-3 h-3" />
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
