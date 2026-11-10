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
  <div class="bg-card border border-border rounded-2xl overflow-hidden shadow-sm">
    <div class="p-5 border-b border-border flex items-center justify-between">
      <div>
        <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Production Stations</h4>
        <p class="text-[10px] text-muted-foreground/80 font-medium uppercase mt-0.5">Manufacturing cells & assembly lines</p>
      </div>
      <span class="px-2.5 py-0.5 bg-primary/10 text-primary text-[10px] font-semibold rounded-full border border-primary/20 uppercase tracking-wider">
        {{ stations.length }} Nodes
      </span>
    </div>

    <div v-if="loading && stations.length === 0" class="p-16 text-center">
      <div class="w-8 h-8 border-3 border-primary border-t-transparent rounded-full animate-spin mx-auto mb-3"></div>
      <p class="text-xs font-medium uppercase tracking-wider text-muted-foreground">Querying Station Graph...</p>
    </div>

    <div v-else-if="stations.length === 0" class="p-16 text-center text-muted-foreground">
      <Cpu class="w-12 h-12 mx-auto mb-3 opacity-30" />
      <p class="text-xs font-medium uppercase tracking-wider">No production stations registered</p>
    </div>

    <div v-else class="divide-y divide-border/60">
      <div
        v-for="station in stations"
        :key="station.id"
        @click="emit('select', station)"
        class="p-4 hover:bg-muted/40 cursor-pointer transition-all flex items-center justify-between group"
      >
        <div class="flex items-center gap-4">
          <div class="p-2.5 rounded-xl bg-muted/30 border border-border group-hover:border-primary/40 transition-colors">
            <Cpu class="w-5 h-5 text-primary" />
          </div>

          <div>
            <div class="flex items-center gap-2">
              <span class="text-sm font-semibold text-foreground group-hover:text-primary transition-colors">{{ station.name }}</span>
              <span class="text-[10px] font-mono px-2 py-0.5 bg-muted text-muted-foreground rounded border border-border">
                {{ station.customIdentifier }}
              </span>
              <span v-if="station.isOnline" class="w-2 h-2 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.6)]"></span>
            </div>

            <div class="flex items-center gap-4 mt-2">
              <!-- CAD Anchor -->
              <div v-if="station.pinnedObjectHandle" class="flex items-center gap-1 text-[10px] font-mono text-muted-foreground">
                <MapPin class="w-3 h-3 text-primary" />
                <span>Ref: {{ station.pinnedObjectHandle }}</span>
              </div>

              <!-- Controllers -->
              <div class="flex items-center gap-1 text-[10px] text-muted-foreground font-medium uppercase">
                <Monitor class="w-3 h-3 text-muted-foreground" />
                <span>{{ station.controllers?.length || 0 }} Controllers</span>
              </div>

              <!-- Alert Badges -->
              <div v-if="station.alertCount && station.alertCount > 0" class="flex items-center gap-1 text-[10px] text-destructive font-medium uppercase">
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
              class="px-2 py-0.5 bg-muted/60 text-muted-foreground border border-border rounded text-[9px] font-mono"
            >
              {{ c.hostname || c.name || 'IPC' }}
            </span>
          </div>
          <ChevronRight class="w-5 h-5 text-muted-foreground group-hover:text-foreground transition-transform group-hover:translate-x-1" />
        </div>
      </div>
    </div>
  </div>
</template>
