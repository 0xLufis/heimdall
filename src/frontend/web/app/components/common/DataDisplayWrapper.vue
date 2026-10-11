<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  LayoutGrid,
  List,
  Table as TableIcon,
  ArrowUpDown,
  ArrowUp,
  ArrowDown,
  MoreVertical,
  X,
  ExternalLink,
  ChevronRight
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator
} from '~/components/ui/dropdown-menu'
import SearchAndFilterBar from './SearchAndFilterBar.vue'
import { useSearchAndFilter, type FacetDefinition } from '~/composables/useSearchAndFilter'

export type { FacetDefinition } from '~/composables/useSearchAndFilter'

export interface DisplayColumn {
  key: string
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
  class?: string
  getValue?: (item: any) => any
}

export interface DisplayAction {
  id: string
  label: string
  icon?: any
  variant?: 'default' | 'destructive' | 'outline' | 'ghost'
  disabled?: boolean | ((item: any) => boolean)
  hidden?: boolean | ((item: any) => boolean)
  onClick: (item: any, event: MouseEvent) => void
}

const props = withDefaults(
  defineProps<{
    items: any[]
    columns?: DisplayColumn[]
    defaultView?: 'card' | 'list' | 'table'
    availableViews?: ('card' | 'list' | 'table')[]
    showViewToggle?: boolean
    searchQuery?: string
    sortKeyDefault?: string | null
    sortDirectionDefault?: 'asc' | 'desc' | null
    filterPredicate?: (item: any, query: string) => boolean
    getItemId?: (item: any) => string
    defaultActionLabel?: string
    defaultActionIcon?: any
    actions?: DisplayAction[]
    dismissible?: boolean
    decoupleClose?: boolean
    emptyTitle?: string
    emptyDescription?: string
    cardGridClass?: string

    // Split Search & Filtering Engine integration
    enableSearchFilter?: boolean
    searchPlaceholder?: string
    searchFields?: string[]
    facetDefinitions?: FacetDefinition[]
    tagFiltersDefault?: string[]
    facetFiltersDefault?: Record<string, string[]>
    showTags?: boolean
    showFacets?: boolean
    showCount?: boolean
  }>(),
  {
    columns: () => [],
    defaultView: 'card',
    availableViews: () => ['card', 'list', 'table'],
    showViewToggle: true,
    searchQuery: '',
    sortKeyDefault: null,
    sortDirectionDefault: null,
    getItemId: (item: any) => item?.id || item?.key || String(Math.random()),
    defaultActionLabel: 'View',
    actions: () => [],
    dismissible: false,
    decoupleClose: true,
    emptyTitle: 'No records found',
    emptyDescription: 'No items match the current view criteria or filters.',
    cardGridClass: 'grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4',

    enableSearchFilter: false,
    searchPlaceholder: 'Search records, keywords...',
    searchFields: undefined,
    facetDefinitions: () => [],
    tagFiltersDefault: () => [],
    facetFiltersDefault: () => ({}),
    showTags: true,
    showFacets: true,
    showCount: true
  }
)

const emit = defineEmits<{
  (e: 'update:viewMode', mode: 'card' | 'list' | 'table'): void
  (e: 'itemClick', item: any, event: MouseEvent): void
  (e: 'defaultAction', item: any, event: MouseEvent): void
  (e: 'dismiss', item: any, event: MouseEvent): void
  (e: 'sortChange', key: string | null, direction: 'asc' | 'desc' | null): void
  (e: 'searchChange', query: string): void
  (e: 'tagFiltersChange', tags: string[]): void
  (e: 'facetFiltersChange', facets: Record<string, string[]>): void
}>()

// Active View State
const currentView = ref<'card' | 'list' | 'table'>(props.defaultView)

const setViewMode = (mode: 'card' | 'list' | 'table') => {
  currentView.value = mode
  emit('update:viewMode', mode)
}

// 3-State Sorting Engine: none -> asc -> desc -> none
const activeSortKey = ref<string | null>(props.sortKeyDefault)
const activeSortDirection = ref<'asc' | 'desc' | null>(props.sortDirectionDefault)

const toggleSort = (colKey: string) => {
  if (activeSortKey.value !== colKey) {
    activeSortKey.value = colKey
    activeSortDirection.value = 'asc'
  } else {
    if (activeSortDirection.value === 'asc') {
      activeSortDirection.value = 'desc'
    } else if (activeSortDirection.value === 'desc') {
      activeSortKey.value = null
      activeSortDirection.value = null
    } else {
      activeSortDirection.value = 'asc'
    }
  }
  emit('sortChange', activeSortKey.value, activeSortDirection.value)
}

// Split Search & Filtering Engine initialization
const sfEngine = useSearchAndFilter(computed(() => props.items), {
  searchFields: props.searchFields,
  facetDefinitions: props.facetDefinitions,
  initialSearchQuery: props.searchQuery,
  initialTags: props.tagFiltersDefault,
  initialFacets: props.facetFiltersDefault
})

// Sync external props.searchQuery changes into engine if changed externally
watch(() => props.searchQuery, (newVal) => {
  if (props.enableSearchFilter && newVal !== undefined && newVal !== sfEngine.searchQuery.value) {
    sfEngine.searchQuery.value = newVal
  }
})

// Filter & Sort Pipeline
const processedItems = computed(() => {
  let result: any[] = []

  if (props.enableSearchFilter) {
    result = [...sfEngine.searchedAndFilteredItems.value]
  } else {
    result = [...(props.items || [])]
    const q = (props.searchQuery || '').trim().toLowerCase()
    if (q) {
      if (props.filterPredicate) {
        result = result.filter(item => props.filterPredicate!(item, q))
      } else {
        result = result.filter(item => {
          const textBlob = JSON.stringify(item).toLowerCase()
          return textBlob.includes(q)
        })
      }
    }
  }

  // 3-State Sorting
  if (activeSortKey.value && activeSortDirection.value) {
    const key = activeSortKey.value
    const dir = activeSortDirection.value
    const col = props.columns.find(c => c.key === key)

    result.sort((a, b) => {
      let valA = col?.getValue ? col.getValue(a) : a[key]
      let valB = col?.getValue ? col.getValue(b) : b[key]

      if (valA === undefined || valA === null) valA = ''
      if (valB === undefined || valB === null) valB = ''

      if (typeof valA === 'number' && typeof valB === 'number') {
        return dir === 'asc' ? valA - valB : valB - valA
      }

      const strA = String(valA).toLowerCase()
      const strB = String(valB).toLowerCase()
      return dir === 'asc' ? strA.localeCompare(strB) : strB.localeCompare(strA)
    })
  }

  return result
})

const handleItemClick = (item: any, event: MouseEvent) => {
  emit('itemClick', item, event)
}

const handleDefaultAction = (item: any, event: MouseEvent) => {
  emit('defaultAction', item, event)
}

const handleDismiss = (item: any, event: MouseEvent) => {
  emit('dismiss', item, event)
}

const isActionVisible = (action: DisplayAction, item: any) => {
  if (typeof action.hidden === 'function') return !action.hidden(item)
  return !action.hidden
}

const isActionDisabled = (action: DisplayAction, item: any) => {
  if (typeof action.disabled === 'function') return action.disabled(item)
  return Boolean(action.disabled)
}

defineExpose({
  currentView,
  setViewMode,
  activeSortKey,
  activeSortDirection,
  searchQuery: sfEngine.searchQuery,
  tagFilters: sfEngine.tagFilters,
  facetFilters: sfEngine.facetFilters,
  clearSearch: sfEngine.clearSearch,
  clearFilters: sfEngine.clearFilters,
  resetAll: sfEngine.resetAll,
  processedItems
})
</script>

<template>
  <div class="data-display-wrapper space-y-4 w-full">
    <!-- Optional Split Search & Filter Bar Component -->
    <div v-if="enableSearchFilter" class="pb-1">
      <SearchAndFilterBar
        :search-query="sfEngine.searchQuery.value"
        :tag-filters="sfEngine.tagFilters.value"
        :facet-filters="sfEngine.facetFilters.value"
        :available-facets="sfEngine.availableFacets.value"
        :total-count="sfEngine.totalCount.value"
        :match-count="sfEngine.matchCount.value"
        :placeholder="searchPlaceholder"
        :show-tags="showTags"
        :show-facets="showFacets"
        :show-count="showCount"
        @update:search-query="(val) => { sfEngine.searchQuery.value = val; emit('searchChange', val) }"
        @update:tag-filters="(val) => { sfEngine.tagFilters.value = val; emit('tagFiltersChange', val) }"
        @update:facet-filters="(val) => { sfEngine.facetFilters.value = val; emit('facetFiltersChange', val) }"
        @clear-search="sfEngine.clearSearch"
        @clear-filters="sfEngine.clearFilters"
        @reset-all="sfEngine.resetAll"
      />
    </div>
    <!-- Toolbar Controls Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-1">
      <div class="flex items-center gap-2">
        <slot name="toolbar-left">
          <span class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            {{ processedItems.length }} {{ processedItems.length === 1 ? 'Record' : 'Records' }}
          </span>
          <Badge v-if="activeSortKey && activeSortDirection" variant="outline" class="text-[10px] font-mono gap-1">
            <span>Sort: {{ activeSortKey }}</span>
            <span class="font-bold uppercase text-primary">({{ activeSortDirection }})</span>
          </Badge>
        </slot>
      </div>

      <div class="flex items-center gap-2 self-end sm:self-auto">
        <slot name="toolbar-extra" />

        <!-- 3-Way Toggle Control -->
        <div
          v-if="showViewToggle && availableViews.length > 1"
          class="flex items-center p-0.5 rounded-lg border border-border bg-muted/60 text-muted-foreground shadow-2xs"
          role="group"
          aria-label="Display View Toggle"
        >
          <button
            v-if="availableViews.includes('card')"
            type="button"
            @click="setViewMode('card')"
            :class="currentView === 'card' ? 'bg-background text-foreground shadow-2xs font-semibold' : 'hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs flex items-center gap-1.5 transition-all cursor-pointer"
            title="Card Grid View"
          >
            <LayoutGrid class="size-3.5" />
            <span class="hidden md:inline text-[11px]">Card</span>
          </button>

          <button
            v-if="availableViews.includes('list')"
            type="button"
            @click="setViewMode('list')"
            :class="currentView === 'list' ? 'bg-background text-foreground shadow-2xs font-semibold' : 'hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs flex items-center gap-1.5 transition-all cursor-pointer"
            title="Compact List View"
          >
            <List class="size-3.5" />
            <span class="hidden md:inline text-[11px]">List</span>
          </button>

          <button
            v-if="availableViews.includes('table')"
            type="button"
            @click="setViewMode('table')"
            :class="currentView === 'table' ? 'bg-background text-foreground shadow-2xs font-semibold' : 'hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs flex items-center gap-1.5 transition-all cursor-pointer"
            title="Tabular Data View"
          >
            <TableIcon class="size-3.5" />
            <span class="hidden md:inline text-[11px]">Table</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div
      v-if="processedItems.length === 0"
      class="p-12 rounded-xl border border-dashed border-border bg-card text-center space-y-2"
    >
      <slot name="empty">
        <div class="size-10 rounded-full bg-muted flex items-center justify-center mx-auto text-muted-foreground">
          <LayoutGrid class="size-5" />
        </div>
        <h4 class="text-sm font-semibold text-foreground">{{ emptyTitle }}</h4>
        <p class="text-xs text-muted-foreground max-w-sm mx-auto">{{ emptyDescription }}</p>
      </slot>
    </div>

    <!-- 1. CARD VIEW -->
    <div
      v-else-if="currentView === 'card'"
      :class="cardGridClass"
    >
      <div
        v-for="(item, idx) in processedItems"
        :key="getItemId(item)"
        class="data-card-item relative group rounded-xl border border-border bg-card p-4 hover:shadow-md hover:border-primary/40 transition-all flex flex-col justify-between"
        @click="handleItemClick(item, $event)"
      >
        <!-- Decoupled Closing 'X' button (Decoupled from card content click) -->
        <div
          v-if="dismissible"
          class="absolute top-2.5 right-2.5 z-20"
        >
          <button
            type="button"
            @click.stop="handleDismiss(item, $event)"
            class="size-6 rounded-md bg-background/80 hover:bg-destructive/10 hover:text-destructive text-muted-foreground flex items-center justify-center transition-colors border border-border/40 shadow-2xs cursor-pointer"
            title="Dismiss / Close"
          >
            <X class="size-3.5" />
          </button>
        </div>

        <!-- Custom Card Slot or Fallback -->
        <slot
          name="card"
          :item="item"
          :index="idx"
          :defaultAction="() => handleDefaultAction(item, $event)"
          :actions="actions"
        >
          <div class="space-y-2">
            <div class="flex items-start justify-between pr-6">
              <h4 class="text-sm font-bold text-foreground truncate">
                {{ item.displayName || item.name || item.title || item.id }}
              </h4>
            </div>
            <p v-if="item.description || item.subtitle" class="text-xs text-muted-foreground line-clamp-2">
              {{ item.description || item.subtitle }}
            </p>
          </div>
        </slot>

        <!-- Standardized Card Footer Action Bar -->
        <div class="mt-4 pt-3 border-t border-border/60 flex items-center justify-between gap-2">
          <!-- Default Single Action Button -->
          <Button
            size="sm"
            variant="outline"
            class="h-7 text-xs px-2.5 gap-1.5 cursor-pointer hover:bg-primary/10 hover:text-primary hover:border-primary/30"
            @click.stop="handleDefaultAction(item, $event)"
          >
            <component :is="defaultActionIcon || ExternalLink" class="size-3" />
            <span>{{ defaultActionLabel }}</span>
          </Button>

          <!-- Dropdown Menu for Context / Secondary Actions -->
          <DropdownMenu v-if="actions.length > 0">
            <DropdownMenuTrigger as-child>
              <Button
                variant="ghost"
                size="icon"
                class="size-7 rounded-md text-muted-foreground hover:text-foreground cursor-pointer"
                @click.stop
              >
                <MoreVertical class="size-3.5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" class="w-48 bg-card border-border shadow-lg">
              <template v-for="action in actions" :key="action.id">
                <DropdownMenuItem
                  v-if="isActionVisible(action, item)"
                  :disabled="isActionDisabled(action, item)"
                  @click="action.onClick(item, $event)"
                  class="text-xs gap-2 cursor-pointer"
                  :class="action.variant === 'destructive' ? 'text-destructive focus:bg-destructive/10' : ''"
                >
                  <component :is="action.icon" v-if="action.icon" class="size-3.5" />
                  <span>{{ action.label }}</span>
                </DropdownMenuItem>
              </template>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </div>

    <!-- 2. LIST VIEW -->
    <div
      v-else-if="currentView === 'list'"
      class="space-y-2"
    >
      <div
        v-for="(item, idx) in processedItems"
        :key="getItemId(item)"
        class="data-list-item group relative rounded-xl border border-border bg-card p-3 hover:border-primary/40 hover:bg-muted/20 transition-all flex items-center justify-between gap-4 cursor-pointer"
        @click="handleItemClick(item, $event)"
      >
        <div class="flex-1 min-w-0">
          <slot
            name="list"
            :item="item"
            :index="idx"
            :defaultAction="() => handleDefaultAction(item, $event)"
            :actions="actions"
          >
            <div class="flex items-center gap-3">
              <div class="min-w-0">
                <h4 class="text-xs font-bold text-foreground truncate">
                  {{ item.displayName || item.name || item.title || item.id }}
                </h4>
                <p v-if="item.description || item.subtitle" class="text-[11px] text-muted-foreground truncate">
                  {{ item.description || item.subtitle }}
                </p>
              </div>
            </div>
          </slot>
        </div>

        <!-- List Item Actions -->
        <div class="flex items-center gap-1.5 shrink-0">
          <Button
            size="sm"
            variant="outline"
            class="h-7 text-xs px-2.5 gap-1 cursor-pointer"
            @click.stop="handleDefaultAction(item, $event)"
          >
            <component :is="defaultActionIcon || ExternalLink" class="size-3" />
            <span>{{ defaultActionLabel }}</span>
          </Button>

          <DropdownMenu v-if="actions.length > 0">
            <DropdownMenuTrigger as-child>
              <Button
                variant="ghost"
                size="icon"
                class="size-7 text-muted-foreground hover:text-foreground cursor-pointer"
                @click.stop
              >
                <MoreVertical class="size-3.5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" class="w-48 bg-card border-border shadow-lg">
              <template v-for="action in actions" :key="action.id">
                <DropdownMenuItem
                  v-if="isActionVisible(action, item)"
                  :disabled="isActionDisabled(action, item)"
                  @click="action.onClick(item, $event)"
                  class="text-xs gap-2 cursor-pointer"
                  :class="action.variant === 'destructive' ? 'text-destructive focus:bg-destructive/10' : ''"
                >
                  <component :is="action.icon" v-if="action.icon" class="size-3.5" />
                  <span>{{ action.label }}</span>
                </DropdownMenuItem>
              </template>
            </DropdownMenuContent>
          </DropdownMenu>

          <!-- Decoupled close button -->
          <button
            v-if="dismissible"
            type="button"
            @click.stop="handleDismiss(item, $event)"
            class="size-7 rounded-md hover:bg-destructive/10 hover:text-destructive text-muted-foreground flex items-center justify-center transition-colors cursor-pointer"
            title="Dismiss"
          >
            <X class="size-3.5" />
          </button>
        </div>
      </div>
    </div>

    <!-- 3. TABLE VIEW (with On-Click 3-State Sorting) -->
    <div
      v-else-if="currentView === 'table'"
      class="rounded-xl border border-border bg-card overflow-hidden shadow-2xs"
    >
      <div class="overflow-x-auto">
        <table class="w-full text-xs text-left">
          <thead class="bg-muted/50 text-muted-foreground border-b border-border font-medium">
            <tr>
              <th
                v-for="col in columns"
                :key="col.key"
                class="p-3"
                :class="[
                  col.class || '',
                  col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left',
                  col.sortable !== false ? 'cursor-pointer select-none hover:text-foreground hover:bg-muted/70 transition-colors' : ''
                ]"
                @click="col.sortable !== false ? toggleSort(col.key) : undefined"
              >
                <div
                  class="inline-flex items-center gap-1.5"
                  :class="col.align === 'right' ? 'justify-end w-full' : col.align === 'center' ? 'justify-center w-full' : ''"
                >
                  <span>{{ col.label }}</span>
                  <template v-if="col.sortable !== false">
                    <ArrowUp
                      v-if="activeSortKey === col.key && activeSortDirection === 'asc'"
                      class="size-3 text-primary stroke-[2.5]"
                    />
                    <ArrowDown
                      v-else-if="activeSortKey === col.key && activeSortDirection === 'desc'"
                      class="size-3 text-primary stroke-[2.5]"
                    />
                    <ArrowUpDown
                      v-else
                      class="size-3 opacity-30 group-hover:opacity-60"
                    />
                  </template>
                </div>
              </th>

              <!-- Actions Header -->
              <th class="p-3 w-32 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr
              v-for="(item, idx) in processedItems"
              :key="getItemId(item)"
              class="data-table-row hover:bg-muted/30 transition-colors cursor-pointer"
              @click="handleItemClick(item, $event)"
            >
              <td
                v-for="col in columns"
                :key="col.key"
                class="p-3"
                :class="[
                  col.class || '',
                  col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'
                ]"
              >
                <slot
                  :name="`cell-${col.key}`"
                  :item="item"
                  :column="col"
                  :value="col.getValue ? col.getValue(item) : item[col.key]"
                  :index="idx"
                >
                  <span class="text-foreground">
                    {{ col.getValue ? col.getValue(item) : (item[col.key] ?? '-') }}
                  </span>
                </slot>
              </td>

              <!-- Table Action Cell -->
              <td class="p-3 text-right">
                <div class="flex items-center justify-end gap-1.5">
                  <Button
                    size="sm"
                    variant="ghost"
                    class="h-7 px-2 text-xs hover:bg-primary/10 hover:text-primary gap-1 cursor-pointer"
                    @click.stop="handleDefaultAction(item, $event)"
                  >
                    <component :is="defaultActionIcon || ExternalLink" class="size-3" />
                    <span>{{ defaultActionLabel }}</span>
                  </Button>

                  <DropdownMenu v-if="actions.length > 0">
                    <DropdownMenuTrigger as-child>
                      <Button
                        variant="ghost"
                        size="icon"
                        class="size-7 text-muted-foreground hover:text-foreground cursor-pointer"
                        @click.stop
                      >
                        <MoreVertical class="size-3.5" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end" class="w-48 bg-card border-border shadow-lg">
                      <template v-for="action in actions" :key="action.id">
                        <DropdownMenuItem
                          v-if="isActionVisible(action, item)"
                          :disabled="isActionDisabled(action, item)"
                          @click="action.onClick(item, $event)"
                          class="text-xs gap-2 cursor-pointer"
                          :class="action.variant === 'destructive' ? 'text-destructive focus:bg-destructive/10' : ''"
                        >
                          <component :is="action.icon" v-if="action.icon" class="size-3.5" />
                          <span>{{ action.label }}</span>
                        </DropdownMenuItem>
                      </template>
                    </DropdownMenuContent>
                  </DropdownMenu>

                  <button
                    v-if="dismissible"
                    type="button"
                    @click.stop="handleDismiss(item, $event)"
                    class="size-6 rounded-md hover:bg-destructive/10 hover:text-destructive text-muted-foreground flex items-center justify-center transition-colors cursor-pointer"
                    title="Dismiss"
                  >
                    <X class="size-3.5" />
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
