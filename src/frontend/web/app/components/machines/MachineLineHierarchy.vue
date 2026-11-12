<script setup lang="ts">
import { computed } from 'vue'
import { Badge } from '~/components/ui/badge'
import { Layers, Cpu } from 'lucide-vue-next'
import MachineCardGrid from './MachineCardGrid.vue'
import { useOrganizationMachineTypes } from '~/composables/useOrganizationMachineTypes'

const props = defineProps<{
  machines: any[]
  mode: 'lines' | 'technologies'
  lineKpis?: Record<string, any>
}>()

const emit = defineEmits<{
  'open': [machine: any]
  'openTree': [stationId: string]
  'open3d': [machine: any]
  'openQuickView': [controller: any, machine: any]
  'openDecommission': [machine: any]
  'contextMenu': [machine: any, event: MouseEvent]
}>()

const { isMachineTypeVisible } = useOrganizationMachineTypes()

const byName = (a: any, b: any) =>
  String(a.customIdentifier || a.name || '').localeCompare(String(b.customIdentifier || b.name || ''), 'en', { numeric: true, sensitivity: 'base' })

const groups = computed(() => {
  const map = new Map<string, any[]>()
  for (const m of props.machines) {
    if (props.mode === 'technologies' && !isMachineTypeVisible(m.machineType)) {
      continue
    }
    const key = props.mode === 'lines'
      ? (m.lineGroupName || m.groupId || 'Unassigned Line')
      : (m.machineType || 'General Industrial')
    // The key is a machine type, not a category: a type the organization did not disable (checked
    // above) stays visible. Testing it against the category list hid every real machine type.
    if (!map.has(key)) map.set(key, [])
    map.get(key)!.push(m)
  }

  return Array.from(map.entries())
    .sort(([a], [b]) => a.localeCompare(b, 'en', { numeric: true, sensitivity: 'base' }))
    .map(([name, items]) => {
      // Lifecycle status of the machine record ("active"), not a live running state.
      const activeCount = items.filter(i => i.status === 'active').length
      return {
        name,
        items: [...items].sort(byName),
        totalCount: items.length,
        activeCount,
        // Line KPIs (keyed by line name and by line group id) only describe lines.
        kpi: props.mode === 'lines' ? (props.lineKpis?.[name] ?? props.lineKpis?.[items[0]?.lineGroupId]) : undefined
      }
    })
})
</script>

<template>
  <div class="space-y-6">
    <div v-if="groups.length === 0" class="text-center py-12 border border-dashed border-border rounded-xl">
      <p class="text-xs text-muted-foreground font-mono">No machine stations match the current filter criteria.</p>
    </div>
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
              {{ grp.activeCount }} of {{ grp.totalCount }} stations active
            </span>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <Badge v-if="grp.kpi?.oee != null" variant="outline" class="text-[10px] font-mono" title="Mean OEE of the line's stations that report it">
            OEE ⌀ {{ grp.kpi.oee }}%
          </Badge>
          <Badge variant="outline" class="text-[10px] font-mono">
            {{ grp.totalCount }} Machines
          </Badge>
        </div>
      </div>

      <!-- Nested Machine Cards -->
      <MachineCardGrid
        :machines="grp.items"
        @open="emit('open', $event)"
        @open-tree="emit('openTree', $event)"
        @open3d="emit('open3d', $event)"
        @open-quick-view="(controller: any, machine: any) => emit('openQuickView', controller, machine)"
        @open-decommission="emit('openDecommission', $event)"
        @context-menu="(machine: any, event: MouseEvent) => emit('contextMenu', machine, event)"
      />
    </div>
  </div>
</template>
