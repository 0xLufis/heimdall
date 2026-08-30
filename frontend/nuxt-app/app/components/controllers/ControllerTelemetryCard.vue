<script setup lang="ts">
import type { IndustrialController } from '~/types/domain'
import { Monitor, Cpu, Activity, HardDrive, Network, ShieldCheck, ShieldAlert } from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'

const props = defineProps<{
  controller: IndustrialController
}>()
</script>

<template>
  <div class="p-6 bg-slate-950 border border-slate-800 rounded-3xl space-y-6">
    <div class="flex items-start justify-between">
      <div class="flex items-center gap-3">
        <div class="p-3 bg-indigo-500/10 border border-indigo-500/20 rounded-2xl text-indigo-400">
          <Monitor class="w-6 h-6" />
        </div>
        <div>
          <h3 class="text-lg font-black text-white uppercase">{{ controller.hostname || controller.name }}</h3>
          <p class="text-xs font-mono text-slate-500">{{ controller.macAddress }} • {{ controller.ipAddress || '192.168.1.100' }}</p>
        </div>
      </div>

      <Badge
        variant="outline"
        class="text-xs font-mono font-bold uppercase"
        :class="controller.telemetry?.isOnline ? 'border-emerald-500/30 text-emerald-400 bg-emerald-950/20' : 'border-slate-800 text-slate-500'"
      >
        {{ controller.telemetry?.isOnline ? 'Online (5m Heartbeat)' : 'Offline' }}
      </Badge>
    </div>

    <!-- Beckhoff Real-Time NIC Driver Status Section -->
    <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl space-y-3">
      <div class="flex items-center justify-between">
        <span class="text-xs font-black uppercase tracking-widest text-slate-400 flex items-center gap-2">
          <Network class="w-4 h-4 text-indigo-400" />
          Beckhoff TwinCAT RT Ethernet Driver
        </span>

        <Badge
          v-if="controller.telemetry?.beckhoffRT?.isRealtimeDriverBound"
          class="bg-emerald-500/20 border-emerald-500/30 text-emerald-400 text-[10px] font-mono uppercase"
        >
          <ShieldCheck class="w-3 h-3 mr-1" /> RT Driver Bound
        </Badge>
        <Badge
          v-else
          variant="outline"
          class="border-amber-500/30 text-amber-400 bg-amber-500/10 text-[10px] font-mono uppercase"
        >
          <ShieldAlert class="w-3 h-3 mr-1" /> Standard NDIS
        </Badge>
      </div>

      <div class="grid grid-cols-2 gap-3 text-xs">
        <div>
          <span class="text-[10px] font-bold text-slate-500 uppercase">Adapter Service:</span>
          <p class="font-mono font-bold text-slate-300">{{ controller.telemetry?.beckhoffRT?.serviceName || 'TcRTEthernet' }}</p>
        </div>
        <div>
          <span class="text-[10px] font-bold text-slate-500 uppercase">Driver Version:</span>
          <p class="font-mono font-bold text-slate-300">{{ controller.telemetry?.beckhoffRT?.driverVersion || '3.1.4024.12' }}</p>
        </div>
      </div>
    </div>

    <!-- Resource Gauges Grid -->
    <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
      <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl">
        <div class="flex items-center justify-between text-[10px] font-black uppercase tracking-widest text-slate-500">
          <span>CPU Load</span>
          <Cpu class="w-3.5 h-3.5 text-indigo-400" />
        </div>
        <div class="text-2xl font-black font-mono text-white mt-2">{{ controller.telemetry?.cpuUsagePercent ?? 12 }}%</div>
      </div>

      <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl">
        <div class="flex items-center justify-between text-[10px] font-black uppercase tracking-widest text-slate-500">
          <span>RAM Utilization</span>
          <Activity class="w-3.5 h-3.5 text-indigo-400" />
        </div>
        <div class="text-2xl font-black font-mono text-white mt-2">{{ controller.telemetry?.ramUsagePercent ?? 48 }}%</div>
      </div>

      <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl">
        <div class="flex items-center justify-between text-[10px] font-black uppercase tracking-widest text-slate-500">
          <span>Free Disk Space</span>
          <HardDrive class="w-3.5 h-3.5 text-indigo-400" />
        </div>
        <div class="text-2xl font-black font-mono text-white mt-2">
          {{ controller.freeDiskSpace?.totalFreeGB ? Math.round(controller.freeDiskSpace.totalFreeGB) + ' GB' : '128 GB' }}
        </div>
      </div>
    </div>
  </div>
</template>
