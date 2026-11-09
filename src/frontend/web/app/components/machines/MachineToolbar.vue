<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Search, RotateCcw, Factory, Layers, Cpu, Activity, Sliders, X, Check } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { useOrganizationMachineTypes } from '~/composables/useOrganizationMachineTypes'

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

const {
  allCategories,
  enabledCategories,
  fetchOrganizationTypes,
  toggleCategory,
  isCategoryEnabled
} = useOrganizationMachineTypes()

const showTypeFilterModal = ref(false)

onMounted(() => {
  fetchOrganizationTypes()
})

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

      <!-- View Switcher & Action Controls -->
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
          @click="showTypeFilterModal = true"
          class="h-9 px-2.5 text-xs text-muted-foreground hover:text-foreground cursor-pointer flex items-center gap-1"
          title="Configure In-Use Machine Types & Technologies"
        >
          <Sliders class="w-3.5 h-3.5" />
          <span class="hidden md:inline">In-Use Types</span>
        </Button>

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

    <!-- Modal: Configure In-Use Machine Types & Technologies -->
    <div
      v-if="showTypeFilterModal"
      class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4"
    >
      <div class="bg-card text-card-foreground border rounded-xl shadow-xl w-full max-w-md p-5 space-y-4">
        <div class="flex items-center justify-between pb-2 border-b">
          <div class="flex items-center gap-2">
            <Sliders class="w-4 h-4 text-primary" />
            <h3 class="text-sm font-bold">Plant Floor Machine Types</h3>
          </div>
          <button @click="showTypeFilterModal = false" class="text-muted-foreground hover:text-foreground cursor-pointer">
            <X class="w-4 h-4" />
          </button>
        </div>

        <p class="text-xs text-muted-foreground">
          Toggle machine types and technologies active on your manufacturing floor. Disabled types will be hidden from technology hierarchies and filter menus.
        </p>

        <div class="grid grid-cols-2 gap-2 pt-1 max-h-60 overflow-y-auto">
          <button
            v-for="cat in allCategories"
            :key="cat"
            type="button"
            @click="toggleCategory(cat)"
            class="flex items-center justify-between p-2 rounded-lg border text-xs transition-colors cursor-pointer"
            :class="isCategoryEnabled(cat)
              ? 'bg-primary/10 border-primary/40 text-foreground font-medium'
              : 'bg-muted/30 border-border text-muted-foreground hover:bg-muted/50'"
          >
            <span>{{ cat }}</span>
            <div
              class="w-4 h-4 rounded flex items-center justify-center border text-[10px]"
              :class="isCategoryEnabled(cat) ? 'bg-primary text-primary-foreground border-primary' : 'border-border bg-background'"
            >
              <Check v-if="isCategoryEnabled(cat)" class="w-3 h-3 stroke-[3]" />
            </div>
          </button>
        </div>

        <div class="flex justify-end pt-2 border-t">
          <Button size="sm" @click="showTypeFilterModal = false">Done</Button>
        </div>
      </div>
    </div>
  </div>
</template>
