<script setup lang="ts">
import type { IndustrialController } from '~/types/domain'
import { Monitor, Activity, HardDrive, Cpu, Terminal, ChevronRight, MapPin, Link, Lock, Eye } from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'
import { useGlobalContextMenu } from '~/composables/useGlobalContextMenu'

const props = defineProps<{
  controllers: IndustrialController[]
  loading?: boolean
  selectedId?: string
}>()

const emit = defineEmits<{
  (e: 'select', controller: IndustrialController): void
  (e: 'queue-command', controller: IndustrialController): void
  (e: 'link-dxf', controller: IndustrialController): void
  (e: 'locate-dxf', handle: string): void
  (e: 'quick-view', controller: IndustrialController): void
}>()

const { canManageEndpoints, canExecuteRemote } = useRbacPermission()
const { openContextMenu } = useGlobalContextMenu()

const handleCardContextMenu = (pc: IndustrialController, e: MouseEvent) => {
  openContextMenu(e, {
    entityType: 'controller',
    entityId: pc.id,
    entityName: pc.hostname || pc.name,
    handle: pc.pinnedObjectHandle || undefined,
    controllerId: pc.id,
    controllerHostname: pc.hostname,
    machineId: pc.controlledMachines?.[0]?.id,
    machineName: pc.controlledMachines?.[0]?.name,
    ownerTeam: pc.responsibleTeams?.[0] ? { name: pc.responsibleTeams[0].name } : undefined,
    ownerPerson: (pc as any).preferredTechnicianName ? { name: (pc as any).preferredTechnicianName } : undefined
  })
}
</script>

<template>
  <div class="space-y-4">
    <div v-if="loading && controllers.length === 0" class="p-16 text-center bg-card border border-border rounded-xl">
      <div class="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin mx-auto mb-3"></div>
      <p class="text-xs font-medium text-muted-foreground">Scanning Edge IPC Telemetry...</p>
    </div>

    <div v-else-if="controllers.length === 0" class="p-16 text-center bg-card border border-border rounded-xl text-muted-foreground">
      <Monitor class="w-12 h-12 mx-auto mb-3 opacity-30" />
      <p class="text-xs font-medium text-muted-foreground">No industrial controllers connected</p>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <div
        v-for="pc in controllers"
        :key="pc.id"
        role="button"
        tabindex="0"
        :aria-label="`Select controller ${pc.hostname || pc.name}`"
        @click="emit('select', pc)"
        @keydown.enter="emit('select', pc)"
        @keydown.space.prevent="emit('select', pc)"
        @contextmenu="handleCardContextMenu(pc, $event)"
        class="bg-card border rounded-xl p-5 transition-all duration-200 shadow-xs hover:shadow-lg hover:-translate-y-0.5 active:translate-y-0 active:scale-[0.995] group flex flex-col justify-between cursor-pointer focus:outline-hidden focus-visible:ring-2 focus-visible:ring-primary/50"
        :class="selectedId === pc.id ? 'border-zinc-500 border-primary ring-1 ring-primary/60 bg-card shadow-md' : 'border-border hover:border-primary/50 hover:bg-card/95'"
      >
        <div>
          <!-- Header -->
          <div class="flex items-start justify-between mb-4">
            <div class="flex items-center gap-3">
              <div class="p-2.5 bg-muted rounded-lg border border-border group-hover:bg-accent transition-colors">
                <Monitor class="w-5 h-5 text-foreground group-hover:text-primary transition-colors" />
              </div>
              <div>
                <h4 class="text-sm font-semibold text-foreground group-hover:text-primary flex items-center gap-2 transition-colors">
                  {{ pc.hostname || pc.name }}
                  <span
                    class="w-2 h-2 rounded-full"
                    :class="pc.telemetry?.isOnline ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.6)]' : 'bg-muted-foreground/40'"
                  ></span>
                </h4>
                <p class="text-xs font-mono text-muted-foreground">{{ pc.macAddress || 'No MAC' }}</p>
              </div>
            </div>

            <Badge
              variant="outline"
              class="text-xs font-medium px-2 py-0.5 rounded-md font-mono"
              :class="pc.telemetry?.isOnline ? 'border-emerald-500/30 text-emerald-700 dark:text-emerald-400 bg-emerald-500/10' : 'border-border text-muted-foreground bg-muted/50'"
            >
              {{ pc.telemetry?.isOnline ? 'Online' : 'Offline' }}
            </Badge>
          </div>

          <!-- Telemetry Mini Gauges -->
          <div class="grid grid-cols-3 gap-2 p-2.5 bg-muted/40 rounded-lg border border-border mb-4">
            <div class="text-center">
              <div class="text-xs font-medium text-muted-foreground flex items-center justify-center gap-1">
                <Cpu class="w-3 h-3 text-muted-foreground" /> CPU
              </div>
              <div class="text-xs font-mono font-semibold text-foreground mt-0.5">
                {{ pc.telemetry?.cpuUsagePercent ?? 0 }}%
              </div>
            </div>

            <div class="text-center border-x border-border">
              <div class="text-xs font-medium text-muted-foreground flex items-center justify-center gap-1">
                <Activity class="w-3 h-3 text-muted-foreground" /> RAM
              </div>
              <div class="text-xs font-mono font-semibold text-foreground mt-0.5">
                {{ pc.telemetry?.ramUsagePercent ?? 0 }}%
              </div>
            </div>

            <div class="text-center">
              <div class="text-xs font-medium text-muted-foreground flex items-center justify-center gap-1">
                <HardDrive class="w-3 h-3 text-muted-foreground" /> Free
              </div>
              <div class="text-xs font-mono font-semibold text-foreground mt-0.5">
                {{ pc.freeDiskSpace?.totalFreeGB ? Math.round(pc.freeDiskSpace.totalFreeGB) + 'GB' : 'N/A' }}
              </div>
            </div>
          </div>

          <!-- Spatial CAD / DXF Mapping Tag -->
          <div class="mb-4 p-2.5 bg-muted/30 rounded-lg border border-border flex items-center justify-between gap-2">
            <div class="flex items-center gap-2 truncate">
              <MapPin class="size-3.5 text-muted-foreground shrink-0" />
              <div class="truncate">
                <span class="text-xs text-muted-foreground block">CAD Tag</span>
                <span v-if="pc.pinnedObjectHandle" class="text-xs font-mono font-medium text-foreground truncate block">
                  {{ pc.pinnedObjectHandle }}
                </span>
                <span v-else class="text-xs font-mono text-muted-foreground/70 block">
                  Unpinned
                </span>
              </div>
            </div>

            <div class="flex items-center gap-1.5 shrink-0">
              <button
                v-if="pc.pinnedObjectHandle"
                type="button"
                @click.stop="emit('locate-dxf', pc.pinnedObjectHandle)"
                class="px-2.5 py-1 rounded-md bg-card hover:bg-zinc-700 hover:bg-accent border border-border text-xs font-medium text-foreground transition-all shadow-xs active:scale-95 cursor-pointer"
                title="Locate on CAD Map"
              >
                View Map
              </button>

              <RbacTooltip :disabled="!canManageEndpoints" :tooltip="RBAC_TOOLTIPS.ENDPOINT_MANAGEMENT">
                <button
                  type="button"
                  :disabled="!canManageEndpoints"
                  @click.stop="canManageEndpoints && emit('link-dxf', pc)"
                  class="px-2.5 py-1 rounded-md bg-card hover:bg-accent border border-border text-xs font-medium text-foreground transition-all shadow-xs active:scale-95 disabled:opacity-50 disabled:pointer-events-none flex items-center gap-1 cursor-pointer"
                >
                  <Lock v-if="!canManageEndpoints" class="w-3 h-3 text-amber-500" />
                  <span>{{ pc.pinnedObjectHandle ? 'Edit DXF' : '+ Link DXF' }}</span>
                </button>
              </RbacTooltip>
            </div>
          </div>

          <!-- Controlled Stations Tags -->
          <div class="space-y-1 mb-4">
            <span class="text-xs text-muted-foreground block">Controlled Stations</span>
            <div v-if="pc.controlledMachines && pc.controlledMachines.length > 0" class="flex flex-wrap gap-1">
              <span
                v-for="st in pc.controlledMachines"
                :key="st.id"
                class="px-2 py-0.5 bg-muted text-foreground border border-border rounded-md text-xs font-mono font-medium"
              >
                {{ st.customIdentifier || st.name }}
              </span>
            </div>
            <div v-else class="text-xs text-muted-foreground">Standalone Edge Node</div>
          </div>
        </div>

        <!-- Action Footer -->
        <div class="pt-3 border-t border-border flex items-center justify-between gap-2">
          <div class="flex items-center gap-2">
            <RbacTooltip :disabled="!canExecuteRemote" :tooltip="RBAC_TOOLTIPS.REMOTE_EXECUTION">
              <button
                type="button"
                :disabled="!canExecuteRemote"
                @click.stop="canExecuteRemote && emit('queue-command', pc)"
                class="px-2.5 py-1.5 rounded-lg bg-card hover:bg-accent border border-border text-xs font-medium text-foreground shadow-xs transition-all active:scale-95 disabled:opacity-50 disabled:pointer-events-none flex items-center gap-1.5 cursor-pointer"
              >
                <Lock v-if="!canExecuteRemote" class="w-3.5 h-3.5 text-amber-500" />
                <Terminal v-else class="w-3.5 h-3.5 text-muted-foreground" />
                <span>Queue Command</span>
              </button>
            </RbacTooltip>

            <RbacTooltip :disabled="!canExecuteRemote" :tooltip="RBAC_TOOLTIPS.REMOTE_EXECUTION">
              <button
                type="button"
                :disabled="!canExecuteRemote"
                @click.stop="canExecuteRemote && emit('quick-view', pc)"
                class="px-2.5 py-1.5 rounded-lg bg-card hover:bg-zinc-800 hover:bg-accent border border-border text-xs font-medium text-foreground shadow-xs transition-all active:scale-95 disabled:opacity-50 disabled:pointer-events-none flex items-center gap-1.5 cursor-pointer"
                title="Remote Quick View"
              >
                <Eye class="w-3.5 h-3.5 text-muted-foreground" />
                <span>Remote</span>
              </button>
            </RbacTooltip>
          </div>

          <button
            type="button"
            @click.stop="emit('select', pc)"
            class="px-3 py-1.5 rounded-lg bg-primary hover:bg-primary/90 text-primary-foreground text-xs font-semibold shadow-xs transition-all active:scale-95 flex items-center gap-1.5 group/btn cursor-pointer"
          >
            <span>Telemetry</span>
            <ChevronRight class="w-3.5 h-3.5 transition-transform duration-150 group-hover/btn:translate-x-0.5" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
