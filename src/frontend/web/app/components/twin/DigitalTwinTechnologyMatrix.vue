<script setup lang="ts">
import { computed } from 'vue'
import {
  Boxes,
  Cpu,
  Flame,
  CheckCircle2,
  AlertTriangle,
  Layers,
  ArrowRight
} from 'lucide-vue-next'
import type { MachineTwinNode } from '~/composables/useDigitalTwin'

const props = defineProps<{
  machines: MachineTwinNode[]
  selectedTechnology: string
}>()

const emit = defineEmits<{
  (e: 'select-technology', tech: string): void
}>()

// Calculate breakdown per technology domain
const techStats = computed(() => {
  const map: Record<string, { total: number; active: number; warning: number; maint: number }> = {}

  props.machines.forEach(m => {
    const tech = m.machineType || 'General'
    if (!map[tech]) {
      map[tech] = { total: 0, active: 0, warning: 0, maint: 0 }
    }
    map[tech].total++
    if (m.status === 'active' && m.operationalState === 'Running') map[tech].active++
    else if (m.operationalState === 'Warning') map[tech].warning++
    else if (m.status === 'maintenance') map[tech].maint++
  })

  return Object.entries(map).map(([tech, stats]) => ({
    name: tech,
    ...stats,
    healthPercent: Math.round(((stats.active + stats.warning * 0.7) / (stats.total || 1)) * 100)
  }))
})
</script>

<template>
  <div class="bg-[#15181e] rounded-xl p-5 border border-[#232730] shadow-sm flex flex-col justify-between">
    <!-- Header -->
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-2">
        <Boxes class="w-4 h-4 text-teal-400" />
        <span class="text-xs font-bold uppercase tracking-wider text-zinc-200">Engineering Technology Disciplines</span>
      </div>
      <button
        v-if="selectedTechnology !== 'all'"
        @click="emit('select-technology', 'all')"
        class="text-xs text-zinc-400 hover:text-white"
      >
        Clear Filter
      </button>
    </div>

    <!-- Technology Cards Grid -->
    <div class="grid grid-cols-2 sm:grid-cols-3 gap-2.5">
      <div
        v-for="item in techStats"
        :key="item.name"
        @click="emit('select-technology', item.name)"
        class="p-3 bg-[#0c0e12] rounded-xl border cursor-pointer transition-all hover:border-zinc-600 flex flex-col justify-between"
        :class="selectedTechnology === item.name ? 'border-teal-400 bg-teal-950/20 ring-1 ring-teal-400' : 'border-[#232730]'"
      >
        <div>
          <div class="flex items-center justify-between mb-1">
            <span class="text-xs font-bold text-zinc-200 truncate">{{ item.name }}</span>
            <span class="text-[10px] font-mono text-zinc-400">{{ item.total }} cells</span>
          </div>
          <!-- Health bar -->
          <div class="w-full bg-[#1c2027] h-1.5 rounded-full overflow-hidden mt-1.5">
            <div
              class="h-full bg-gradient-to-r from-teal-500 to-emerald-400"
              :style="{ width: `${item.healthPercent}%` }"
            ></div>
          </div>
        </div>

        <div class="flex items-center justify-between text-[10px] font-mono mt-2 pt-1.5 border-t border-[#232730]">
          <span class="text-emerald-400 font-semibold">{{ item.active }} active</span>
          <span v-if="item.warning > 0" class="text-amber-400">{{ item.warning }} warn</span>
          <span v-else class="text-zinc-500">Nominal</span>
        </div>
      </div>
    </div>

    <!-- Footer Note -->
    <div class="mt-4 pt-3 border-t border-[#232730]/60 flex items-center justify-between text-[11px] font-mono text-zinc-500">
      <span>Mapped directly to ISO 13849 &amp; ISA-95</span>
      <span>Click discipline to filter canvas</span>
    </div>
  </div>
</template>
