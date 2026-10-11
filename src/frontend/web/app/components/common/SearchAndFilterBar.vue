<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Search,
  X,
  Filter,
  SlidersHorizontal,
  Tag as TagIcon,
  RotateCcw,
  Check,
  Plus
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import {
  Popover,
  PopoverContent,
  PopoverTrigger
} from '~/components/ui/popover'

export interface FacetOption {
  value: string
  label?: string
  count: number
}

export interface AvailableFacet {
  key: string
  label: string
  options: FacetOption[]
}

const props = withDefaults(
  defineProps<{
    searchQuery: string
    tagFilters?: string[]
    facetFilters?: Record<string, string[]>
    availableFacets?: AvailableFacet[]
    totalCount?: number
    matchCount?: number
    placeholder?: string
    showTags?: boolean
    showFacets?: boolean
    showCount?: boolean
  }>(),
  {
    tagFilters: () => [],
    facetFilters: () => ({}),
    availableFacets: () => [],
    totalCount: 0,
    matchCount: 0,
    placeholder: 'Search keywords, names, custom identifiers, serial numbers...',
    showTags: true,
    showFacets: true,
    showCount: true
  }
)

const emit = defineEmits<{
  (e: 'update:searchQuery', val: string): void
  (e: 'update:tagFilters', val: string[]): void
  (e: 'update:facetFilters', val: Record<string, string[]>): void
  (e: 'clearSearch'): void
  (e: 'clearFilters'): void
  (e: 'resetAll'): void
}>()

const newTagInput = ref('')
const isFacetPopoverOpen = ref(false)

function onSearchInput(e: Event) {
  const target = e.target as HTMLInputElement
  emit('update:searchQuery', target.value)
}

function handleAddTag() {
  const raw = newTagInput.value.trim()
  if (!raw) return
  if (!props.tagFilters.includes(raw)) {
    emit('update:tagFilters', [...props.tagFilters, raw])
  }
  newTagInput.value = ''
}

function handleRemoveTag(tag: string) {
  emit('update:tagFilters', props.tagFilters.filter(t => t !== tag))
}

function isFacetSelected(facetKey: string, val: string): boolean {
  const selected = props.facetFilters[facetKey] || []
  return selected.includes(val)
}

function toggleFacetSelection(facetKey: string, val: string) {
  const current = props.facetFilters[facetKey] ? [...props.facetFilters[facetKey]] : []
  const idx = current.indexOf(val)
  if (idx >= 0) {
    current.splice(idx, 1)
  } else {
    current.push(val)
  }
  emit('update:facetFilters', {
    ...props.facetFilters,
    [facetKey]: current
  })
}

const activeFilterCount = computed(() => {
  let count = props.tagFilters.length
  for (const arr of Object.values(props.facetFilters)) {
    count += (arr || []).length
  }
  return count
})
</script>

<template>
  <div class="space-y-2.5 w-full">
    <!-- Top Row: Search Input + Facet Trigger + Reset -->
    <div class="flex items-center gap-2">
      <!-- Search Input Container -->
      <div class="relative flex-1">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
        <input
          :value="searchQuery"
          @input="onSearchInput"
          :placeholder="placeholder"
          class="w-full pl-9 pr-8 h-9 rounded-xl bg-card border border-border text-foreground text-xs placeholder:text-muted-foreground focus:outline-hidden focus:ring-1 focus:ring-primary shadow-2xs"
        />
        <button
          v-if="searchQuery"
          type="button"
          @click="emit('update:searchQuery', '')"
          class="absolute right-2.5 top-1/2 -translate-y-1/2 p-0.5 rounded text-muted-foreground hover:text-foreground cursor-pointer"
          title="Clear search input"
        >
          <X class="size-3.5" />
        </button>
      </div>

      <!-- Facet Filter Popover Button -->
      <Popover v-if="showFacets && availableFacets.length > 0">
        <PopoverTrigger as-child>
          <Button
            variant="outline"
            size="sm"
            class="h-9 px-3 rounded-xl border border-border bg-card text-foreground text-xs font-bold gap-1.5 cursor-pointer shadow-2xs shrink-0"
            :class="activeFilterCount > 0 ? 'border-primary text-primary' : ''"
          >
            <SlidersHorizontal class="size-3.5" />
            <span>Filters</span>
            <Badge
              v-if="activeFilterCount > 0"
              class="size-4 p-0 flex items-center justify-center text-[9px] font-mono bg-primary text-primary-foreground rounded-full"
            >
              {{ activeFilterCount }}
            </Badge>
          </Button>
        </PopoverTrigger>
        <PopoverContent align="end" class="w-80 p-4 bg-card border-border shadow-xl rounded-2xl space-y-4">
          <div class="flex items-center justify-between border-b border-border pb-2">
            <span class="text-xs font-bold uppercase tracking-wider text-foreground">Filter Facets</span>
            <button
              v-if="activeFilterCount > 0"
              type="button"
              @click="emit('clearFilters')"
              class="text-[10px] text-muted-foreground hover:text-foreground font-medium underline cursor-pointer"
            >
              Reset all
            </button>
          </div>

          <div class="space-y-3 max-h-72 overflow-y-auto pr-1">
            <div v-for="facet in availableFacets" :key="facet.key" class="space-y-1.5">
              <span class="text-[11px] font-bold text-muted-foreground block">{{ facet.label }}</span>
              <div class="flex flex-wrap gap-1.5">
                <button
                  v-for="opt in facet.options"
                  :key="opt.value"
                  type="button"
                  @click="toggleFacetSelection(facet.key, opt.value)"
                  class="px-2 py-1 rounded-lg text-xs font-medium border transition-colors flex items-center gap-1 cursor-pointer"
                  :class="isFacetSelected(facet.key, opt.value)
                    ? 'bg-primary text-primary-foreground border-primary'
                    : 'bg-muted/50 border-border text-foreground hover:bg-muted'"
                >
                  <span>{{ opt.label || opt.value }}</span>
                  <span
                    class="text-[10px] font-mono px-1 rounded"
                    :class="isFacetSelected(facet.key, opt.value) ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-background text-muted-foreground'"
                  >
                    {{ opt.count }}
                  </span>
                </button>
              </div>
            </div>
          </div>
        </PopoverContent>
      </Popover>

      <!-- Clear All Filters & Search -->
      <Button
        v-if="searchQuery || activeFilterCount > 0"
        variant="ghost"
        size="sm"
        @click="emit('resetAll')"
        class="h-9 px-2 text-xs text-muted-foreground hover:text-foreground cursor-pointer rounded-xl shrink-0"
        title="Reset all filters and search query"
      >
        <RotateCcw class="size-3.5 mr-1" />
        <span class="hidden sm:inline">Reset</span>
      </Button>
    </div>

    <!-- Bottom Row: Tag Pills & Tag Add Input + Match Counter -->
    <div v-if="showTags" class="flex flex-wrap items-center justify-between gap-2 pt-0.5">
      <!-- Active Tag Badges & Inline Tag Adder -->
      <div class="flex flex-wrap items-center gap-1.5 flex-1 min-w-0">
        <!-- Tag Pills -->
        <span
          v-for="tag in tagFilters"
          :key="tag"
          class="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-mono border shadow-2xs"
          :class="tag.startsWith('!')
            ? 'border-rose-500/40 bg-rose-500/10 text-rose-600 dark:text-rose-400'
            : 'border-primary/30 bg-primary/10 text-primary'"
        >
          <span class="truncate">{{ tag }}</span>
          <button
            type="button"
            @click="handleRemoveTag(tag)"
            class="hover:opacity-75 cursor-pointer ml-0.5"
            title="Remove tag"
          >
            <X class="size-3" />
          </button>
        </span>

        <!-- Quick Tag Input -->
        <div class="inline-flex items-center gap-1">
          <input
            v-model="newTagInput"
            placeholder="+ Add tag (e.g. status:active, !line:Line 2)..."
            @keyup.enter="handleAddTag"
            class="h-6 px-2 text-[11px] font-mono rounded-lg bg-card border border-dashed border-border text-foreground placeholder:text-muted-foreground focus:outline-hidden focus:border-primary w-48"
          />
          <button
            v-if="newTagInput.trim()"
            type="button"
            @click="handleAddTag"
            class="size-5 rounded bg-primary text-primary-foreground flex items-center justify-center cursor-pointer hover:opacity-90"
            title="Add tag"
          >
            <Plus class="size-3" />
          </button>
        </div>
      </div>

      <!-- Item Counter -->
      <div v-if="showCount && totalCount > 0" class="text-[11px] text-muted-foreground font-mono shrink-0 ml-auto">
        <span>Showing </span>
        <strong class="text-foreground">{{ matchCount }}</strong>
        <span> of </span>
        <span>{{ totalCount }} items</span>
      </div>
    </div>
  </div>
</template>
