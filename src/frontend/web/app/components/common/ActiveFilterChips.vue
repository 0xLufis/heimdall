<script setup lang="ts">
import { computed } from 'vue'
import { X, Search, RotateCcw } from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'

const props = withDefaults(
  defineProps<{
    tagFilters?: string[]
    facetFilters?: Record<string, string[]>
    searchQuery?: string
    totalCount?: number
    matchCount?: number
    showCount?: boolean
  }>(),
  {
    tagFilters: () => [],
    facetFilters: () => ({}),
    searchQuery: '',
    totalCount: 0,
    matchCount: 0,
    showCount: true
  }
)

const emit = defineEmits<{
  (e: 'removeTag', tag: string): void
  (e: 'removeFacetValue', facetKey: string, val: string): void
  (e: 'clearSearch'): void
  (e: 'clearAll'): void
}>()

const activeChips = computed(() => {
  const chips: Array<{
    type: 'tag' | 'facet' | 'search'
    key?: string
    value: string
    displayLabel: string
    isNegative?: boolean
  }> = []

  if (props.searchQuery.trim()) {
    chips.push({
      type: 'search',
      value: props.searchQuery,
      displayLabel: `"${props.searchQuery}"`
    })
  }

  for (const tag of props.tagFilters) {
    const isNegative = tag.startsWith('!')
    chips.push({
      type: 'tag',
      value: tag,
      displayLabel: tag,
      isNegative
    })
  }

  for (const [facetKey, values] of Object.entries(props.facetFilters)) {
    for (const val of values) {
      chips.push({
        type: 'facet',
        key: facetKey,
        value: val,
        displayLabel: `${facetKey}: ${val}`
      })
    }
  }

  return chips
})

const hasActiveFilters = computed(() => activeChips.value.length > 0)
</script>

<template>
  <div v-if="hasActiveFilters || showCount" class="flex flex-wrap items-center justify-between gap-2 pt-1 text-xs font-mono">
    <div class="flex flex-wrap items-center gap-1.5 min-h-[26px]">
      <template v-for="chip in activeChips" :key="chip.type + (chip.key || '') + chip.value">
        <!-- Search query chip -->
        <Badge
          v-if="chip.type === 'search'"
          variant="outline"
          class="bg-teal-500/10 text-teal-300 border-teal-500/30 px-2 py-0.5 rounded-md flex items-center gap-1 text-[11px]"
        >
          <Search class="w-3 h-3 text-teal-400" />
          <span>{{ chip.displayLabel }}</span>
          <button
            type="button"
            class="p-0.5 rounded hover:bg-teal-500/20 text-teal-400 transition-colors ml-0.5"
            @click="emit('clearSearch')"
          >
            <X class="w-3 h-3" />
          </button>
        </Badge>

        <!-- Tag chip (negative in rose, positive in zinc) -->
        <Badge
          v-else-if="chip.type === 'tag'"
          variant="outline"
          :class="[
            'px-2 py-0.5 rounded-md flex items-center gap-1 text-[11px]',
            chip.isNegative
              ? 'bg-rose-500/10 text-rose-300 border-rose-500/30'
              : 'bg-zinc-800/80 text-zinc-300 border-zinc-700'
          ]"
        >
          <span>{{ chip.displayLabel }}</span>
          <button
            type="button"
            class="p-0.5 rounded hover:bg-zinc-700/50 transition-colors ml-0.5"
            @click="emit('removeTag', chip.value)"
          >
            <X class="w-3 h-3" />
          </button>
        </Badge>

        <!-- Facet chip -->
        <Badge
          v-else-if="chip.type === 'facet'"
          variant="outline"
          class="bg-zinc-800/80 text-zinc-300 border-zinc-700 px-2 py-0.5 rounded-md flex items-center gap-1 text-[11px]"
        >
          <span>{{ chip.displayLabel }}</span>
          <button
            type="button"
            class="p-0.5 rounded hover:bg-zinc-700/50 transition-colors ml-0.5"
            @click="emit('removeFacetValue', chip.key!, chip.value)"
          >
            <X class="w-3 h-3" />
          </button>
        </Badge>
      </template>

      <!-- Clear All Button -->
      <button
        v-if="hasActiveFilters"
        type="button"
        class="text-[11px] text-zinc-500 hover:text-zinc-300 flex items-center gap-1 ml-1 px-1.5 py-0.5 rounded hover:bg-zinc-800/50 transition-colors cursor-pointer"
        @click="emit('clearAll')"
      >
        <RotateCcw class="w-2.5 h-2.5" />
        <span>Clear all</span>
      </button>
    </div>

    <!-- Count indicator -->
    <div v-if="showCount" class="text-[11px] text-zinc-500 shrink-0 font-medium">
      Showing <span class="text-zinc-200 font-semibold">{{ matchCount }}</span> of <span class="text-zinc-400">{{ totalCount }}</span>
    </div>
  </div>
</template>
