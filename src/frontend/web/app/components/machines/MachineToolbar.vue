<script setup lang="ts">
import { Search, RotateCcw, Factory, Layers, Cpu, Activity } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'

defineProps<{
  searchQuery: string
  activeView: 'machines' | 'lines' | 'technologies' | 'schematic'
  statusFilter: string
  totalCount: number
}>()

const emit = defineEmits<{
  'update:searchQuery': [value: string]
  'update:activeView': [value: 'machines' | 'lines' | 'technologies' | 'schematic']
  'update:statusFilter': [value: string]
  'reset': []
}>()

const filterOptions = [
  { value: 'active_floor', label: 'Active Floor' },
  { value: 'all', label: 'All Stations' },
  { value: 'maintenance', label: 'In Maintenance' },
  { value: 'decommissioned', label: 'Decommissioned' }
]
</script>

<template>
  <div class="space-y-4">
    <!-- Top Bar: Search, View Switcher, Reset -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <!-- Search Input -->
      <div class="relative flex-1 max-w-md">
        <Search class="absolute left-3 top-2.5 w-4 h-4 text-muted-foreground" />
        <Input
          :model-value="searchQuery"
          @update:model-value="emit('update:searchQuery', $event as string)"
          placeholder="Search station identifier, name, or controller..."
          class="pl-9 text-xs h-9 bg-card border-border shadow-xs"
        />
      </div>

      <!-- View Switcher -->
      <div class="flex items-center gap-2">
        <div class="flex items-center p-0.5 bg-muted/40 border border-border rounded-lg">
          <button
            type="button"
            @click="emit('update:activeView', 'machines')"
            class="px-3 py-1 text-xs font-medium rounded-md transition-colors flex items-center gap-1.5 cursor-pointer"
            :class="activeView === 'machines' ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Factory class="w-3.5 h-3.5" />
            <span>Stations</span>
          </button>
          <button
            type="button"
            @click="emit('update:activeView', 'lines')"
            class="px-3 py-1 text-xs font-medium rounded-md transition-colors flex items-center gap-1.5 cursor-pointer"
            :class="activeView === 'lines' ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Layers class="w-3.5 h-3.5" />
            <span>Lines</span>
          </button>
          <button
            type="button"
            @click="emit('update:activeView', 'technologies')"
            class="px-3 py-1 text-xs font-medium rounded-md transition-colors flex items-center gap-1.5 cursor-pointer"
            :class="activeView === 'technologies' ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Cpu class="w-3.5 h-3.5" />
            <span>Technologies</span>
          </button>
          <button
            type="button"
            @click="emit('update:activeView', 'schematic')"
            class="px-3 py-1 text-xs font-medium rounded-md transition-colors flex items-center gap-1.5 cursor-pointer"
            :class="activeView === 'schematic' ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Activity class="w-3.5 h-3.5" />
            <span>Schematic</span>
          </button>
        </div>

        <Button
          variant="outline"
          size="sm"
          @click="emit('reset')"
          class="h-9 px-2.5 text-xs text-muted-foreground hover:text-foreground"
          title="Reset filters"
        >
          <RotateCcw class="w-3.5 h-3.5" />
        </Button>
      </div>
    </div>

    <!-- Status Filter Pills -->
    <div class="flex items-center justify-between gap-2">
      <div class="flex flex-wrap items-center gap-1.5">
        <button
          v-for="opt in filterOptions"
          :key="opt.value"
          type="button"
          @click="emit('update:statusFilter', opt.value)"
          class="px-2.5 py-1 text-xs font-medium rounded-lg border transition-colors cursor-pointer"
          :class="statusFilter === opt.value
            ? 'bg-primary text-primary-foreground border-primary shadow-xs'
            : 'bg-card text-muted-foreground border-border hover:bg-muted/50 hover:text-foreground'"
        >
          {{ opt.label }}
        </button>
      </div>

      <Badge variant="outline" class="text-[10px] font-mono">
        {{ totalCount }} Results
      </Badge>
    </div>
  </div>
</template>
