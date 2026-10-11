<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  SlidersHorizontal,
  Filter,
  Check,
  X,
  RotateCcw,
  Tag as TagIcon,
  Plus
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import {
  Popover,
  PopoverContent,
  PopoverTrigger
} from '~/components/ui/popover'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
  SheetFooter
} from '~/components/ui/sheet'

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
    facetFilters?: Record<string, string[]>
    tagFilters?: string[]
    availableFacets?: AvailableFacet[]
    showTags?: boolean
  }>(),
  {
    facetFilters: () => ({}),
    tagFilters: () => [],
    availableFacets: () => [],
    showTags: true
  }
)

const emit = defineEmits<{
  (e: 'update:facetFilters', val: Record<string, string[]>): void
  (e: 'update:tagFilters', val: string[]): void
  (e: 'clearFilters'): void
}>()

const isDesktopOpen = ref(false)
const isMobileOpen = ref(false)
const newTagInput = ref('')

const activeFilterCount = computed(() => {
  let count = props.tagFilters.length
  for (const list of Object.values(props.facetFilters)) {
    count += list.length
  }
  return count
})

function isSelected(facetKey: string, val: string): boolean {
  const list = props.facetFilters[facetKey] || []
  return list.includes(val)
}

function toggleFacet(facetKey: string, val: string) {
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

function handleClear() {
  emit('clearFilters')
}
</script>

<template>
  <div class="flex items-center">
    <!-- Desktop Popover (Hidden on small screens) -->
    <div class="hidden sm:block">
      <Popover v-model:open="isDesktopOpen">
        <PopoverTrigger as-child>
          <Button
            variant="outline"
            size="sm"
            class="h-10 min-h-[40px] px-3.5 bg-zinc-900/70 border-zinc-800 hover:bg-zinc-800 text-zinc-300 hover:text-zinc-100 flex items-center gap-2 rounded-lg font-mono text-xs"
          >
            <SlidersHorizontal class="w-3.5 h-3.5 text-teal-400" />
            <span>Filters</span>
            <Badge
              v-if="activeFilterCount > 0"
              variant="secondary"
              class="ml-0.5 px-1.5 py-0 h-4 bg-teal-500/20 text-teal-300 border border-teal-500/30 text-[10px] font-mono font-medium rounded-full"
            >
              {{ activeFilterCount }}
            </Badge>
          </Button>
        </PopoverTrigger>

        <PopoverContent
          align="start"
          class="w-80 p-4 bg-[#12151b] border-zinc-800 text-zinc-200 shadow-xl rounded-xl max-h-[480px] overflow-y-auto space-y-4"
        >
          <div class="flex items-center justify-between pb-2 border-b border-zinc-800">
            <div class="flex items-center gap-2">
              <Filter class="w-4 h-4 text-teal-400" />
              <span class="text-xs font-semibold uppercase tracking-wider text-zinc-300">Filter Facets</span>
            </div>
            <button
              v-if="activeFilterCount > 0"
              type="button"
              class="text-[11px] font-mono text-rose-400 hover:text-rose-300 flex items-center gap-1 transition-colors"
              @click="handleClear"
            >
              <RotateCcw class="w-3 h-3" />
              <span>Reset</span>
            </button>
          </div>

          <!-- Facets List -->
          <div v-for="facet in availableFacets" :key="facet.key" class="space-y-1.5">
            <span class="text-[11px] font-mono uppercase tracking-wider text-zinc-400 block font-semibold">
              {{ facet.label }}
            </span>
            <div class="flex flex-wrap gap-1.5">
              <button
                v-for="opt in facet.options"
                :key="opt.value"
                type="button"
                :class="[
                  'px-2 py-1 rounded text-xs font-mono flex items-center gap-1.5 border transition-all cursor-pointer',
                  isSelected(facet.key, opt.value)
                    ? 'bg-teal-500/20 text-teal-200 border-teal-500/40 shadow-sm'
                    : 'bg-zinc-900/60 text-zinc-400 border-zinc-800 hover:border-zinc-700 hover:text-zinc-200'
                ]"
                @click="toggleFacet(facet.key, opt.value)"
              >
                <Check v-if="isSelected(facet.key, opt.value)" class="w-3 h-3 text-teal-400" />
                <span>{{ opt.label || opt.value }}</span>
                <span class="text-[10px] opacity-60">({{ opt.count }})</span>
              </button>
            </div>
          </div>

          <!-- Tag Filters -->
          <div v-if="showTags" class="space-y-2 pt-2 border-t border-zinc-800/80">
            <span class="text-[11px] font-mono uppercase tracking-wider text-zinc-400 block font-semibold">
              Custom Tags
            </span>
            <div class="flex items-center gap-1.5">
              <input
                v-model="newTagInput"
                type="text"
                placeholder="Add tag (e.g. #line1 or !maint)"
                class="flex-1 px-2.5 py-1 text-xs bg-zinc-900 border border-zinc-800 rounded text-zinc-200 placeholder:text-zinc-600 outline-none focus:border-teal-500/50"
                @keydown.enter.prevent="handleAddTag"
              />
              <button
                type="button"
                class="px-2 py-1 text-xs bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded flex items-center gap-1 transition-colors"
                @click="handleAddTag"
              >
                <Plus class="w-3 h-3" />
                <span>Add</span>
              </button>
            </div>
          </div>
        </PopoverContent>
      </Popover>
    </div>

    <!-- Mobile Drawer / Sheet (Visible on small screens) -->
    <div class="sm:hidden">
      <Sheet v-model:open="isMobileOpen">
        <SheetTrigger as-child>
          <Button
            variant="outline"
            size="sm"
            class="h-10 min-h-[40px] px-3 bg-zinc-900/70 border-zinc-800 text-zinc-300 flex items-center gap-1.5 rounded-lg font-mono text-xs"
          >
            <SlidersHorizontal class="w-3.5 h-3.5 text-teal-400" />
            <Badge
              v-if="activeFilterCount > 0"
              variant="secondary"
              class="px-1.5 py-0 h-4 bg-teal-500/20 text-teal-300 border border-teal-500/30 text-[10px] font-mono rounded-full"
            >
              {{ activeFilterCount }}
            </Badge>
          </Button>
        </SheetTrigger>
        <SheetContent side="bottom" class="bg-[#12151b] border-t border-zinc-800 text-zinc-100 max-h-[85vh] overflow-y-auto rounded-t-2xl p-5">
          <SheetHeader class="pb-3 border-b border-zinc-800 flex flex-row items-center justify-between">
            <SheetTitle class="text-sm font-semibold text-zinc-100 uppercase tracking-wider flex items-center gap-2">
              <Filter class="w-4 h-4 text-teal-400" />
              <span>Filters & Facets</span>
            </SheetTitle>
            <button
              v-if="activeFilterCount > 0"
              type="button"
              class="text-xs font-mono text-rose-400 flex items-center gap-1"
              @click="handleClear"
            >
              <RotateCcw class="w-3 h-3" />
              <span>Reset</span>
            </button>
          </SheetHeader>

          <div class="py-4 space-y-4">
            <div v-for="facet in availableFacets" :key="facet.key" class="space-y-2">
              <span class="text-xs font-mono uppercase tracking-wider text-zinc-400 font-semibold block">
                {{ facet.label }}
              </span>
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="opt in facet.options"
                  :key="opt.value"
                  type="button"
                  :class="[
                    'px-3 py-1.5 rounded-lg text-xs font-mono flex items-center gap-1.5 border min-h-[36px] transition-colors',
                    isSelected(facet.key, opt.value)
                      ? 'bg-teal-500/20 text-teal-200 border-teal-500/40'
                      : 'bg-zinc-900 text-zinc-400 border-zinc-800'
                  ]"
                  @click="toggleFacet(facet.key, opt.value)"
                >
                  <Check v-if="isSelected(facet.key, opt.value)" class="w-3.5 h-3.5 text-teal-400" />
                  <span>{{ opt.label || opt.value }}</span>
                  <span class="text-[10px] opacity-60">({{ opt.count }})</span>
                </button>
              </div>
            </div>
          </div>

          <SheetFooter class="pt-3 border-t border-zinc-800 flex gap-2">
            <Button
              class="w-full bg-teal-500 hover:bg-teal-400 text-black font-semibold h-11"
              @click="isMobileOpen = false"
            >
              Apply Filters ({{ activeFilterCount }} active)
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>
    </div>
  </div>
</template>
