<script setup lang="ts">
import type { ProductionStation } from '~/types/domain'
import { Monitor, Cpu, MapPin, AlertTriangle, ShieldCheck, ChevronRight } from 'lucide-vue-next'

const props = defineProps<{
  stations: ProductionStation[]
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'select', station: ProductionStation): void
}>()
</script>

<template>
  <div class="bg-slate-900 border border-slate-800 rounded-3xl overflow-hidden shadow-2xl">
    <div class="p-6 border-b border-slate-800 flex items-center justify-between">
      <div>
        <h4 class="text-xs font-black uppercase tracking-[0.2em] text-slate-400">Production Stations</h4>
        <p class="text-[10px] text-slate-500 font-bold uppercase mt-0.5">Manufacturing cells & assembly lines</p>
      </div>
      <span class="px-3 py-1 bg-indigo-600/20 text-indigo-400 text-[10px] font-black rounded-full border border-indigo-600/30 uppercase tracking-widest">
        {{ stations.length }} Nodes
      </span>
    </div>

    <div v-if="loading && stations.length === 0" class="p-16 text-center">
      <div class="w-8 h-8 border-4 border-indigo-500 border-t-transparent rounded-full animate-spin mx-auto mb-3"></div>
      <p class="text-xs font-black uppercase tracking-widest text-slate-500">Querying Station Graph...</p>
    </div>

    <div v-else-if="stations.length === 0" class="p-16 text-center text-slate-500">
      <Cpu class="w-12 h-12 mx-auto mb-3 opacity-30" />
      <p class="text-xs font-bold uppercase tracking-widest">No production stations registered</p>
    </div>

    <div v-else class="divide-y divide-slate-800/60">
      <div
        v-for="station in stations"
        :key="station.id"
        @click="emit('select', station)"
        class="p-5 hover:bg-slate-800/40 cursor-pointer transition-all flex items-center justify-between group"
      >
        <div class="flex items-center gap-4">
          <div class="p-3 rounded-2xl bg-slate-950 border border-slate-800 group-hover:border-indigo-500/40 transition-colors">
            <Cpu class="w-5 h-5 text-indigo-400" />
          </div>

          <div>
            <div class="flex items-center gap-2">
              <span class="text-sm font-black text-slate-100 group-hover:text-white">{{ station.name }}</span>
              <span class="text-[10px] font-mono font-bold px-2 py-0.5 bg-slate-800 text-indigo-300 rounded border border-slate-700">
                {{ station.customIdentifier }}
              </span>
              <span v-if="station.isOnline" class="w-2 h-2 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.6)]"></span>
            </div>

            <div class="flex items-center gap-4 mt-2">
              <!-- CAD Anchor -->
              <div v-if="station.pinnedObjectHandle" class="flex items-center gap-1 text-[10px] font-mono text-slate-400">
                <MapPin class="w-3 h-3 text-indigo-400" />
                <span>Ref: {{ station.pinnedObjectHandle }}</span>
              </div>

              <!-- Controllers -->
              <div class="flex items-center gap-1 text-[10px] text-slate-500 font-bold uppercase">
                <Monitor class="w-3 h-3 text-slate-400" />
                <span>{{ station.controllers?.length || 0 }} Controllers</span>
              </div>

              <!-- Alert Badges -->
              <div v-if="station.alertCount && station.alertCount > 0" class="flex items-center gap-1 text-[10px] text-rose-400 font-bold uppercase">
                <AlertTriangle class="w-3 h-3" />
                <span>{{ station.alertCount }} Alerts</span>
              </div>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="flex flex-wrap gap-1 max-w-[200px] justify-end">
            <span
              v-for="c in (station.controllers || []).slice(0, 2)"
              :key="c.id"
              class="px-2 py-0.5 bg-slate-950 text-slate-400 border border-slate-800 rounded text-[9px] font-mono"
            >
              {{ c.hostname || c.name || 'IPC' }}
            </span>
          </div>
          <ChevronRight class="w-5 h-5 text-slate-600 group-hover:text-slate-200 transition-transform group-hover:translate-x-1" />
        </div>
      </div>
    </div>
  </div>
</template>
