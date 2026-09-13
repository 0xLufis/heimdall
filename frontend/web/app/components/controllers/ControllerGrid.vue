<script setup lang="ts">
import type { IndustrialController } from '~/types/domain'
import { Monitor, Activity, HardDrive, Cpu, Terminal, ChevronRight, MapPin, Link, Lock, Eye } from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'

const props = defineProps<{
  controllers: IndustrialController[]
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'select', controller: IndustrialController): void
  (e: 'queue-command', controller: IndustrialController): void
  (e: 'link-dxf', controller: IndustrialController): void
  (e: 'locate-dxf', handle: string): void
  (e: 'quick-view', controller: IndustrialController): void
}>()

const { canManageEndpoints, canExecuteRemote } = useRbacPermission()
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
        class="bg-slate-900 border border-slate-800 hover:border-slate-700 rounded-xl p-5 transition-all shadow-sm group flex flex-col justify-between"
      >
        <div>
          <!-- Header -->
          <div class="flex items-start justify-between mb-4">
            <div class="flex items-center gap-3">
              <div class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 group-hover:border-indigo-500/30 transition-colors">
                <Monitor class="w-5 h-5 text-indigo-400" />
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
              <MapPin class="size-3.5 text-indigo-400 shrink-0" />
              <div class="truncate">
                <span class="text-xs text-slate-400 block">CAD Tag</span>
                <span v-if="pc.pinnedObjectHandle" class="text-xs font-mono font-medium text-indigo-300 truncate block">
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
                class="px-2.5 py-1 rounded-md bg-indigo-950/50 hover:bg-indigo-900/60 border border-indigo-500/30 text-xs font-medium text-indigo-300 transition-all"
                title="Locate on CAD Map"
              >
                View Map
              </button>

              <RbacTooltip :disabled="!canManageEndpoints" :tooltip="RBAC_TOOLTIPS.ENDPOINT_MANAGEMENT">
                <button
                  type="button"
                  :disabled="!canManageEndpoints"
                  @click.stop="canManageEndpoints && emit('link-dxf', pc)"
                  class="px-2.5 py-1 rounded-md bg-slate-900 hover:bg-slate-800 border border-slate-800 text-xs font-medium text-slate-300 hover:text-white transition-all disabled:opacity-50 disabled:pointer-events-none flex items-center gap-1"
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
                class="px-2 py-0.5 bg-indigo-950/30 text-indigo-300 border border-indigo-500/20 rounded-md text-xs font-mono font-medium"
              >
                {{ st.customIdentifier || st.name }}
              </span>
            </div>
            <div v-else class="text-xs text-slate-500">Standalone Edge Node</div>
          </div>
        </div>

        <!-- Action Footer -->
        <div class="pt-3 border-t border-slate-800/80 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <RbacTooltip :disabled="!canExecuteRemote" :tooltip="RBAC_TOOLTIPS.REMOTE_EXECUTION">
              <button
                type="button"
                :disabled="!canExecuteRemote"
                @click="canExecuteRemote && emit('queue-command', pc)"
                class="flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-indigo-400 transition-colors disabled:opacity-50 disabled:pointer-events-none"
              >
                <Lock v-if="!canExecuteRemote" class="w-3.5 h-3.5 text-amber-400" />
                <Terminal v-else class="w-3.5 h-3.5" />
                <span>Queue Command</span>
              </button>
            </RbacTooltip>

            <RbacTooltip :disabled="!canExecuteRemote" :tooltip="RBAC_TOOLTIPS.REMOTE_EXECUTION">
              <button
                type="button"
                :disabled="!canExecuteRemote"
                @click="canExecuteRemote && emit('quick-view', pc)"
                class="flex items-center gap-1 text-xs font-medium text-slate-400 hover:text-cyan-400 transition-colors disabled:opacity-50 disabled:pointer-events-none"
                title="Remote Quick View"
              >
                <Eye class="w-3.5 h-3.5 text-slate-400" />
                <span>Remote</span>
              </button>
            </RbacTooltip>
          </div>

          <button
            type="button"
            @click="emit('select', pc)"
            class="flex items-center gap-1 text-xs font-medium text-indigo-400 hover:text-indigo-300 transition-colors"
          >
            <span>Telemetry</span>
            <ChevronRight class="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
