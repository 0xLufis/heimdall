<script setup lang="ts">
import { computed } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Layers, Cpu, ChevronRight } from 'lucide-vue-next'
import MachineCardGrid from './MachineCardGrid.vue'

const props = defineProps<{
  machines: any[]
  mode: 'lines' | 'technologies'
  lineKpis?: Record<string, any>
}>()

const emit = defineEmits<{
  'openTree': [stationId: string]
  'openQuickView': [controller: any]
  'openDecommission': [machine: any]
  'contextMenu': [machine: any, event: MouseEvent]
}>()

const groups = computed(() => {
  const map = new Map<string, any[]>()
  for (const m of props.machines) {
    const key = props.mode === 'lines'
      ? (m.groupId || 'Unassigned Line')
      : (m.machineType || 'General Industrial')
    if (!map.has(key)) map.set(key, [])
    map.get(key)!.push(m)
  }

  return Array.from(map.entries()).map(([name, items]) => {
    const activeCount = items.filter(i => i.status === 'active').length
    return {
      name,
      items,
      totalCount: items.length,
      activeCount,
      kpi: props.lineKpis?.[name]
    }
  })
})
</script>

<template>
  <div class="space-y-6">
    <div
      v-for="grp in groups"
      :key="grp.name"
      class="border border-border rounded-xl bg-card/60 p-4 space-y-4"
    >
      <!-- Group Header -->
      <div class="flex items-center justify-between border-b border-border/50 pb-3">
        <div class="flex items-center gap-2.5">
          <div class="p-1.5 rounded-lg bg-primary/10 text-primary">
            <component :is="mode === 'lines' ? Layers : Cpu" class="w-4 h-4" />
          </div>
          <div>
            <h3 class="text-sm font-bold text-foreground">{{ grp.name }}</h3>
            <span class="text-[10px] font-mono text-muted-foreground">
              {{ grp.activeCount }} of {{ grp.totalCount }} stations running
            </span>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <Badge v-if="grp.kpi?.oee" variant="outline" class="text-[10px] font-mono border-emerald-500/30 text-emerald-400">
            OEE: {{ grp.kpi.oee }}%
          </Badge>
          <Badge variant="outline" class="text-[10px] font-mono">
            {{ grp.totalCount }} Machines
          </Badge>
        </div>
      </div>

      <!-- Nested Machine Cards -->
      <MachineCardGrid
        :machines="grp.items"
        @open-tree="emit('openTree', $event)"
        @open-quick-view="emit('openQuickView', $event)"
        @open-decommission="emit('openDecommission', $event)"
        @context-menu="emit('contextMenu', $event[0], $event[1])"
      />
    </div>
  </div>
</template>
