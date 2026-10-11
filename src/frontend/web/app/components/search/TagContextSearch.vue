<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import {
  Search,
  X,
  CornerDownLeft,
  Tag,
  ArrowRight,
  Check,
  AlertCircle,
  SlidersHorizontal,
  ChevronRight
} from 'lucide-vue-next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  useTagContextSearch,
  type TagCompletionItem
} from '~/composables/useTagContextSearch'
import {
  GLOBAL_SEARCH_CONTEXT,
  type SearchContext,
  type ContextSearchResult
} from '~/utils/search/defaultContexts'

const props = withDefaults(defineProps<{
  mode?: 'docked' | 'overlay'
  simpleMode?: boolean
  context?: SearchContext
  placeholder?: string
}>(), {
  mode: 'docked',
  simpleMode: false,
  context: () => GLOBAL_SEARCH_CONTEXT,
  placeholder: 'Search entities or filter with tag:value, !tag:value...'
})

const emit = defineEmits<{
  (e: 'select', result: ContextSearchResult): void
  (e: 'close'): void
}>()

const inputRef = ref<HTMLInputElement | null>(null)
const selectedResultIndex = ref(0)

const {
  currentContext,
  searchQuery,
  cursorPosition,
  parsedQuery,
  activeContext,
  availableTags,
  completionSuggestions,
  isCompletionOpen,
  activeSuggestionIndex,
  results,
  isLoading,
  applyCompletion,
  selectNextSuggestion,
  selectPrevSuggestion,
  applyActiveSuggestion
} = useTagContextSearch(props.context)

function handleInput(e: Event) {
  const target = e.target as HTMLInputElement
  searchQuery.value = target.value
  cursorPosition.value = target.selectionStart ?? target.value.length
  selectedResultIndex.value = 0
}

function handleKeydown(e: KeyboardEvent) {
  if (isCompletionOpen.value) {
    if (e.key === 'ArrowDown' || (e.ctrlKey && e.key === 'j')) {
      e.preventDefault()
      selectNextSuggestion()
      return
    }
    if (e.key === 'ArrowUp' || (e.ctrlKey && e.key === 'k')) {
      e.preventDefault()
      selectPrevSuggestion()
      return
    }
    if (e.key === 'Enter' || e.key === 'Tab') {
      e.preventDefault()
      if (applyActiveSuggestion()) {
        nextTick(() => {
          if (inputRef.value) {
            inputRef.value.focus()
          }
        })
      }
      return
    }
    if (e.key === 'Escape') {
      e.preventDefault()
      cursorPosition.value = undefined
      return
    }
  }

  // Result list keyboard navigation
  if (results.value.length > 0) {
    if (e.key === 'ArrowDown' || (e.ctrlKey && e.key === 'j')) {
      e.preventDefault()
      selectedResultIndex.value = (selectedResultIndex.value + 1) % results.value.length
      return
    }
    if (e.key === 'ArrowUp' || (e.ctrlKey && e.key === 'k')) {
      e.preventDefault()
      selectedResultIndex.value = (selectedResultIndex.value - 1 + results.value.length) % results.value.length
      return
    }
    if (e.key === 'Enter') {
      e.preventDefault()
      const selected = results.value[selectedResultIndex.value]
      if (selected) {
        handleSelectResult(selected)
      }
      return
    }
  }

  if (e.key === 'Escape') {
    emit('close')
  }
}

function handleSelectResult(result: ContextSearchResult) {
  emit('select', result)
  if (result.link && typeof window !== 'undefined') {
    navigateTo(result.link)
  }
}

function removeTag(tagIndex: number) {
  const tag = parsedQuery.value.tags[tagIndex]
  if (!tag) return
  // Remove the tag from query string
  searchQuery.value = searchQuery.value.replace(tag.raw, '').replace(/\s+/g, ' ').trim()
}

function clearQuery() {
  searchQuery.value = ''
  selectedResultIndex.value = 0
}

onMounted(() => {
  if (props.mode === 'overlay' && inputRef.value) {
    inputRef.value.focus()
  }
})
</script>

<template>
  <div
    class="tag-context-search w-full flex flex-col"
    :class="mode === 'overlay' ? 'fixed top-8 sm:top-12 left-1/2 -translate-x-1/2 z-50 max-w-2xl bg-card border border-border rounded-2xl shadow-2xl overflow-hidden' : 'relative space-y-2'"
  >
    <!-- Search Bar Input Box -->
    <div class="relative flex items-center px-3.5 py-2.5 bg-muted/30 border border-border rounded-xl transition-all focus-within:border-primary focus-within:ring-1 focus-within:ring-primary/40">
      <Search class="size-4.5 text-muted-foreground mr-2.5 shrink-0" />
      
      <input
        ref="inputRef"
        :value="searchQuery"
        type="text"
        :placeholder="placeholder"
        @input="handleInput"
        @keydown="handleKeydown"
        @click="cursorPosition = ($event.target as HTMLInputElement).selectionStart ?? undefined"
        @keyup="cursorPosition = ($event.target as HTMLInputElement).selectionStart ?? undefined"
        class="w-full bg-transparent text-sm text-foreground placeholder:text-muted-foreground focus:outline-hidden"
        data-testid="tag-search-input"
        autocomplete="off"
        spellcheck="false"
      />

      <button
        v-if="searchQuery"
        type="button"
        @click="clearQuery"
        class="p-1 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer transition-colors"
        title="Clear query"
      >
        <X class="size-3.5" />
      </button>

      <kbd
        v-if="!searchQuery && mode === 'overlay'"
        class="text-[10px] font-mono uppercase bg-muted text-muted-foreground px-1.5 py-0.5 rounded border border-border ml-2"
      >
        ESC
      </kbd>
    </div>

    <!-- Active Tag Filter Chips -->
    <div
      v-if="parsedQuery.tags.length > 0 && !simpleMode"
      class="flex flex-wrap items-center gap-1.5 pt-1 px-1"
      data-testid="active-tag-chips"
    >
      <span class="text-[10px] font-semibold uppercase text-muted-foreground mr-1">Active Filters:</span>
      <span
        v-for="(tag, idx) in parsedQuery.tags"
        :key="idx"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-mono border transition-all cursor-default"
        :class="tag.inverted || tag.modifier === 'NOT' ? 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/30' : tag.modifier === 'OR' ? 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30' : tag.modifier === 'XOR' ? 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/30' : 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border-indigo-500/30'"
      >
        <span class="font-bold">{{ tag.rawModifier || (tag.inverted ? '!' : '') }}{{ tag.key }}{{ tag.separator || ':' }}</span>
        <span>{{ tag.value }}</span>
        <button
          type="button"
          @click="removeTag(idx)"
          class="ml-0.5 hover:text-foreground cursor-pointer"
          title="Remove tag"
        >
          <X class="size-3" />
        </button>
      </span>
    </div>

    <!-- Code-Completion Popup for Tag Values (Scoped by Tag Metadata) -->
    <div
      v-if="isCompletionOpen && !simpleMode"
      class="absolute z-50 top-12 left-0 right-0 bg-popover border border-border rounded-xl shadow-xl overflow-hidden py-1 max-h-60 overflow-y-auto animate-in fade-in zoom-in-95 duration-100"
      data-testid="tag-completion-popup"
    >
      <div class="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground border-b border-border/50 flex items-center justify-between">
        <div class="flex items-center gap-1.5">
          <span>Metadata values for</span>
          <span class="px-1.5 py-0.2 bg-primary/10 text-primary font-mono rounded font-bold">
            {{ parsedQuery.activeIncompleteTag?.rawModifier }}{{ parsedQuery.activeIncompleteTag?.key }}{{ parsedQuery.activeIncompleteTag?.separator || '#' }}
          </span>
        </div>
        <span class="text-[9px] text-muted-foreground/70">Tab or Enter to select</span>
      </div>
      <div
        v-for="(sugg, sIdx) in completionSuggestions"
        :key="sugg.value"
        class="px-3 py-1.5 text-xs flex items-center justify-between cursor-pointer transition-colors"
        :class="sIdx === activeSuggestionIndex ? 'bg-primary text-primary-foreground font-medium' : 'text-foreground hover:bg-muted'"
        @mousedown.prevent="applyCompletion(sugg)"
      >
        <div class="flex items-center gap-2 min-w-0">
          <Tag class="size-3 shrink-0" />
          <span class="font-mono truncate">{{ sugg.displayLabel }}</span>
        </div>
        
        <div class="flex items-center gap-1.5 shrink-0 ml-2">
          <!-- Entity Type Indicators for Scoped Tag Metadata -->
          <span
            v-for="ind in (sugg.indicators || [])"
            :key="ind.label"
            class="font-mono text-[9px] px-1 py-0.2 rounded font-bold uppercase border"
            :class="sIdx === activeSuggestionIndex ? 'border-primary-foreground/30 bg-primary-foreground/10 text-primary-foreground' : ind.color"
            :title="`Entity: ${ind.label}`"
          >
            [{{ ind.label }}]
          </span>
          <CornerDownLeft class="size-3 opacity-60 ml-1" />
        </div>
      </div>
    </div>

    <!-- Available Tags Helper Bar (Simple Context Hints) -->
    <div
      v-if="!searchQuery && !simpleMode && availableTags.length > 0"
      class="flex flex-wrap items-center gap-1 text-[11px] text-muted-foreground px-1"
    >
      <span class="font-medium mr-1">Available Tags:</span>
      <button
        v-for="tKey in availableTags.slice(0, 7)"
        :key="tKey"
        type="button"
        @click="searchQuery = `${searchQuery ? searchQuery + ' ' : ''}${tKey}#`; if (inputRef) inputRef.focus()"
        class="px-1.5 py-0.5 bg-muted/40 hover:bg-muted text-foreground/80 hover:text-foreground rounded border border-border/50 font-mono transition-colors cursor-pointer"
      >
        {{ tKey }}#
      </button>
    </div>

    <!-- Results List -->
    <div
      v-if="searchQuery.trim()"
      class="search-results-list rounded-xl overflow-hidden divide-y divide-border border border-border bg-card shadow-sm mt-2 max-h-96 overflow-y-auto"
      data-testid="search-results-container"
    >
      <div
        v-if="results.length === 0"
        class="p-6 text-center text-xs text-muted-foreground"
      >
        No matching entities found in {{ activeContext.title }}. Try relaxing filters or typing a broader query.
      </div>

      <div
        v-for="(item, rIdx) in results"
        :key="item.id"
        class="flex items-center justify-between p-3 cursor-pointer transition-colors group"
        :class="rIdx === selectedResultIndex ? 'bg-primary/10 border-l-4 border-l-primary' : 'hover:bg-muted/40'"
        @click="handleSelectResult(item)"
        data-testid="search-result-row"
      >
        <div class="flex items-center gap-3 min-w-0">
          <!-- Entity Indicator Badge -->
          <Badge
            variant="outline"
            class="font-mono text-[10px] px-1.5 py-0.5 shrink-0 rounded font-bold uppercase"
            :class="item.indicatorColor"
            data-testid="entity-indicator"
          >
            {{ item.indicatorLabel }}
          </Badge>

          <div class="min-w-0">
            <div
              class="text-xs font-semibold text-foreground truncate group-hover:text-primary transition-colors"
              v-html="item.highlightedTitle || item.title"
            />
            <div
              v-if="item.subtitle"
              class="text-[11px] text-muted-foreground truncate mt-0.5"
            >
              {{ item.subtitle }}
            </div>
          </div>
        </div>

        <div class="flex items-center gap-2 shrink-0 ml-3">
          <Badge
            variant="outline"
            class="text-[10px] font-mono text-muted-foreground hidden sm:inline-flex"
          >
            {{ item.entityType }}
          </Badge>
          <ChevronRight class="size-3.5 text-muted-foreground group-hover:text-primary transition-colors" />
        </div>
      </div>
    </div>
  </div>
</template>
