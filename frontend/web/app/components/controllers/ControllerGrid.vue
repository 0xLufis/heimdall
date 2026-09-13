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
    <div v-if="loading && controllers.length === 0" class="p-16 text-center bg-slate-900 border border-slate-800 rounded-xl">
      <div class="w-8 h-8 border-4 border-indigo-500 border-t-transparent rounded-full animate-spin mx-auto mb-3"></div>
      <p class="text-xs font-medium text-slate-400">Scanning Edge IPC Telemetry...</p>
    </div>

    <div v-else-if="controllers.length === 0" class="p-16 text-center bg-slate-900 border border-slate-800 rounded-xl text-slate-500">
      <Monitor class="w-12 h-12 mx-auto mb-3 opacity-30" />
      <p class="text-xs font-medium text-slate-400">No industrial controllers connected</p>
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
        class="bg-slate-900 border rounded-xl p-5 transition-all duration-200 shadow-sm hover:shadow-lg hover:-translate-y-0.5 active:translate-y-0 active:scale-[0.995] group flex flex-col justify-between cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-zinc-400/50"
        :class="selectedId === pc.id ? 'border-zinc-500 ring-1 ring-zinc-500/60 bg-slate-900/90 shadow-md' : 'border-slate-800 hover:border-zinc-500/60 hover:bg-slate-900/95'"
      >
        <div>
          <!-- Header -->
          <div class="flex items-start justify-between mb-4">
            <div class="flex items-center gap-3">
              <div class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 group-hover:border-zinc-600 group-hover:bg-zinc-900/80 transition-colors">
                <Monitor class="w-5 h-5 text-zinc-400 group-hover:text-zinc-200 transition-colors" />
              </div>
              <div>
                <h4 class="text-sm font-semibold text-slate-100 group-hover:text-white flex items-center gap-2">
                  {{ pc.hostname || pc.name }}
                  <span
                    class="w-2 h-2 rounded-full"
                    :class="pc.telemetry?.isOnline ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.6)]' : 'bg-slate-600'"
                  ></span>
                </h4>
                <p class="text-xs font-mono text-slate-400">{{ pc.macAddress || 'No MAC' }}</p>
              </div>
            </div>

            <Badge
              variant="outline"
              class="text-xs font-medium px-2 py-0.5 rounded-md font-mono"
              :class="pc.telemetry?.isOnline ? 'border-emerald-500/30 text-emerald-400 bg-emerald-950/20' : 'border-slate-800 text-slate-400 bg-slate-950'"
            >
              {{ pc.telemetry?.isOnline ? 'Online' : 'Offline' }}
            </Badge>
          </div>

          <!-- Telemetry Mini Gauges -->
          <div class="grid grid-cols-3 gap-2 p-2.5 bg-slate-950/60 rounded-lg border border-slate-800/80 mb-4">
            <div class="text-center">
              <div class="text-xs font-medium text-slate-400 flex items-center justify-center gap-1">
                <Cpu class="w-3 h-3 text-slate-500" /> CPU
              </div>
              <div class="text-xs font-mono font-semibold text-slate-200 mt-0.5">
                {{ pc.telemetry?.cpuUsagePercent ?? 0 }}%
              </div>
            </div>

            <div class="text-center border-x border-slate-800/80">
              <div class="text-xs font-medium text-slate-400 flex items-center justify-center gap-1">
                <Activity class="w-3 h-3 text-slate-500" /> RAM
              </div>
              <div class="text-xs font-mono font-semibold text-slate-200 mt-0.5">
                {{ pc.telemetry?.ramUsagePercent ?? 0 }}%
              </div>
            </div>

            <div class="text-center">
              <div class="text-xs font-medium text-slate-400 flex items-center justify-center gap-1">
                <HardDrive class="w-3 h-3 text-slate-500" /> Free
              </div>
              <div class="text-xs font-mono font-semibold text-slate-200 mt-0.5">
                {{ pc.freeDiskSpace?.totalFreeGB ? Math.round(pc.freeDiskSpace.totalFreeGB) + 'GB' : 'N/A' }}
              </div>
            </div>
          </div>

          <!-- Spatial CAD / DXF Mapping Tag -->
          <div class="mb-4 p-2.5 bg-slate-950/50 rounded-lg border border-slate-800/80 flex items-center justify-between gap-2">
            <div class="flex items-center gap-2 truncate">
              <MapPin class="size-3.5 text-zinc-400 shrink-0" />
              <div class="truncate">
                <span class="text-xs text-slate-400 block">CAD Tag</span>
                <span v-if="pc.pinnedObjectHandle" class="text-xs font-mono font-medium text-zinc-300 truncate block">
                  {{ pc.pinnedObjectHandle }}
                </span>
                <span v-else class="text-xs font-mono text-slate-500 block">
                  Unpinned
                </span>
              </div>
            </div>

            <div class="flex items-center gap-1.5 shrink-0">
              <button
                v-if="pc.pinnedObjectHandle"
                type="button"
                @click.stop="emit('locate-dxf', pc.pinnedObjectHandle)"
                class="px-2.5 py-1 rounded-md bg-zinc-800/80 hover:bg-zinc-700 border border-zinc-700/80 hover:border-zinc-400 text-xs font-medium text-zinc-300 hover:text-white transition-all shadow-xs hover:shadow-[0_0_10px_rgba(255,255,255,0.08)] active:scale-95 focus:outline-none focus-visible:ring-1 focus-visible:ring-zinc-400"
                title="Locate on CAD Map"
              >
                View Map
              </button>

              <RbacTooltip :disabled="!canManageEndpoints" :tooltip="RBAC_TOOLTIPS.ENDPOINT_MANAGEMENT">
                <button
                  type="button"
                  :disabled="!canManageEndpoints"
                  @click.stop="canManageEndpoints && emit('link-dxf', pc)"
                  class="px-2.5 py-1 rounded-md bg-zinc-800/80 hover:bg-zinc-700 border border-zinc-700/80 hover:border-zinc-400 text-xs font-medium text-zinc-300 hover:text-white transition-all shadow-xs hover:shadow-[0_0_10px_rgba(255,255,255,0.08)] active:scale-95 disabled:opacity-50 disabled:pointer-events-none flex items-center gap-1 focus:outline-none focus-visible:ring-1 focus-visible:ring-zinc-400"
                >
                  <Lock v-if="!canManageEndpoints" class="w-3 h-3 text-amber-400" />
                  <span>{{ pc.pinnedObjectHandle ? 'Edit DXF' : '+ Link DXF' }}</span>
                </button>
              </RbacTooltip>
            </div>
          </div>

          <!-- Controlled Stations Tags -->
          <div class="space-y-1 mb-4">
            <span class="text-xs text-slate-400 block">Controlled Stations</span>
            <div v-if="pc.controlledMachines && pc.controlledMachines.length > 0" class="flex flex-wrap gap-1">
              <span
                v-for="st in pc.controlledMachines"
                :key="st.id"
                class="px-2 py-0.5 bg-zinc-900 text-zinc-300 border border-zinc-700/60 rounded-md text-xs font-mono font-medium"
              >
                {{ st.customIdentifier || st.name }}
              </span>
            </div>
            <div v-else class="text-xs text-slate-500">Standalone Edge Node</div>
          </div>
        </div>

        <!-- Action Footer -->
        <div class="pt-3 border-t border-slate-800/80 flex items-center justify-between gap-2">
          <div class="flex items-center gap-2">
            <RbacTooltip :disabled="!canExecuteRemote" :tooltip="RBAC_TOOLTIPS.REMOTE_EXECUTION">
              <button
                type="button"
                :disabled="!canExecuteRemote"
                @click.stop="canExecuteRemote && emit('queue-command', pc)"
                class="px-2.5 py-1.5 rounded-lg bg-zinc-950/70 hover:bg-zinc-800 border border-zinc-800/80 hover:border-zinc-500 text-xs font-medium text-slate-300 hover:text-white shadow-xs hover:shadow-[0_0_10px_rgba(255,255,255,0.08)] transition-all active:scale-95 disabled:opacity-50 disabled:pointer-events-none flex items-center gap-1.5 focus:outline-none focus-visible:ring-1 focus-visible:ring-zinc-400"
              >
                <Lock v-if="!canExecuteRemote" class="w-3.5 h-3.5 text-amber-400" />
                <Terminal v-else class="w-3.5 h-3.5 text-slate-400 group-hover:text-slate-200" />
                <span>Queue Command</span>
              </button>
            </RbacTooltip>

            <RbacTooltip :disabled="!canExecuteRemote" :tooltip="RBAC_TOOLTIPS.REMOTE_EXECUTION">
              <button
                type="button"
                :disabled="!canExecuteRemote"
                @click.stop="canExecuteRemote && emit('quick-view', pc)"
                class="px-2.5 py-1.5 rounded-lg bg-zinc-950/70 hover:bg-zinc-800 border border-zinc-800/80 hover:border-zinc-500 text-xs font-medium text-slate-300 hover:text-white shadow-xs hover:shadow-[0_0_10px_rgba(255,255,255,0.08)] transition-all active:scale-95 disabled:opacity-50 disabled:pointer-events-none flex items-center gap-1.5 focus:outline-none focus-visible:ring-1 focus-visible:ring-zinc-400"
                title="Remote Quick View"
              >
                <Eye class="w-3.5 h-3.5 text-slate-400 group-hover:text-slate-200" />
                <span>Remote</span>
              </button>
            </RbacTooltip>
          </div>

          <button
            type="button"
            @click.stop="emit('select', pc)"
            class="px-3 py-1.5 rounded-lg bg-zinc-800/90 hover:bg-zinc-700 border border-zinc-700/80 hover:border-zinc-400 text-xs font-semibold text-zinc-200 hover:text-white shadow-xs hover:shadow-[0_0_12px_rgba(255,255,255,0.12)] transition-all active:scale-95 flex items-center gap-1.5 group/btn focus:outline-none focus-visible:ring-1 focus-visible:ring-zinc-400"
          >
            <span>Telemetry</span>
            <ChevronRight class="w-3.5 h-3.5 transition-transform duration-150 group-hover/btn:translate-x-0.5" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
