<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { watchDebounced } from '@vueuse/core'
import { useRouter } from 'vue-router'
import type { SearchInstanceConfig, SearchResultItem, AutoTagResult } from '~/types/search'
import { useHeimdallSearch } from '~/composables/useHeimdallSearch'
import { Search as SearchIcon, X, Command } from 'lucide-vue-next'
import { useGlobalSearchModal } from '~/composables/useGlobalSearchModal'
import TagPillList from './TagPillList.vue'
import AutoTagSuggestionDropdown from './AutoTagSuggestionDropdown.vue'

const props = withDefaults(
  defineProps<{
    config?: Partial<SearchInstanceConfig>
    immediate?: boolean
  }>(),
  {
    immediate: false
  }
)

const emit = defineEmits<{
  (e: 'search', query: string): void
  (e: 'submit', query: string): void
  (e: 'select-result', item: SearchResultItem): void
}>()

const router = useRouter()
const containerRef = ref<HTMLElement | null>(null)
const inputRef = ref<HTMLInputElement | null>(null)
const isInteractingWithDropdown = ref(false)
const { focusTriggerSignal, isOpen: isGlobalModalOpen } = useGlobalSearchModal()

const {
  rawInput,
  tags,
  freeText,
  autoSuggestions,
  results,
  primaryResults,
  crossTableResults,
  matchingKeys,
  valueSuggestions,
  activePendingKey,
  searchKeyGroups,
  isLoading,
  effectiveQueryString,
  handleInputChange,
  addTag,
  selectKeySuggestion,
  selectValueSuggestion,
  removeTag,
  clearAllTags,
  executeSearch,
  fetchSearchKeys,
  dynamicKnownKeyValues
} = useHeimdallSearch(props.config)

const isFocused = ref(false)
const isMenuExplicitlyClosed = ref(false)

const showDropdown = computed(() => {
  if (isMenuExplicitlyClosed.value || !isFocused.value) return false
  return true
})

watch(focusTriggerSignal, () => {
  if (props.config?.instanceId === 'global') {
    isFocused.value = true
    isMenuExplicitlyClosed.value = false
    inputRef.value?.focus()
    inputRef.value?.select()
    handleInputChange(rawInput.value)
  }
})

// Emit debounced live search queries to parent components
// Ensures typing 1 character does not prematurely trigger search operations unless cleared or tags exist
watchDebounced(
  effectiveQueryString,
  (newVal) => {
    const minChars = props.config?.minCharsForSuggestions ?? 2
    if (tags.value.length > 0 || rawInput.value.trim().length >= minChars || rawInput.value.trim().length === 0) {
      emit('search', newVal)
    }
  },
  { debounce: props.config?.debounceMs ?? 250 }
)

const handleKeydown = (e: KeyboardEvent) => {
  // 1. Ctrl+Space / Cmd+Space: IntelliSense autocomplete trigger
  if ((e.ctrlKey || e.metaKey) && (e.code === 'Space' || e.key === ' ' || e.key === 'Spacebar')) {
    e.preventDefault()
    isMenuExplicitlyClosed.value = false
    isFocused.value = true
    handleInputChange(rawInput.value)
    return
  }

  // 2. ArrowDown: Open dropdown if closed
  if (e.key === 'ArrowDown') {
    if (isMenuExplicitlyClosed.value || !showDropdown.value) {
      e.preventDefault()
      isMenuExplicitlyClosed.value = false
      isFocused.value = true
      handleInputChange(rawInput.value)
      return
    }
  }

  // 3. Tab: Complete top suggestion if dropdown is open
  if (e.key === 'Tab' && showDropdown.value) {
    if (activePendingKey.value && valueSuggestions.value.length > 0) {
      e.preventDefault()
      selectValueSuggestion(valueSuggestions.value[0].value)
      emit('search', effectiveQueryString.value)
      return
    } else if (autoSuggestions.value.length > 0) {
      e.preventDefault()
      addTag(autoSuggestions.value[0].tag)
      rawInput.value = ''
      isMenuExplicitlyClosed.value = false
      return
    } else if (matchingKeys.value.length > 0 && !activePendingKey.value) {
      e.preventDefault()
      selectKeySuggestion(matchingKeys.value[0].key)
      inputRef.value?.focus()
      return
    }
  }

  if (e.key === 'Enter') {
    e.preventDefault()
    if (activePendingKey.value && valueSuggestions.value.length > 0) {
      selectValueSuggestion(valueSuggestions.value[0].value)
      emit('search', effectiveQueryString.value)
    } else if (autoSuggestions.value.length > 0) {
      addTag(autoSuggestions.value[0].tag)
      rawInput.value = ''
      isMenuExplicitlyClosed.value = false
    } else {
      executeSearch()
      emit('search', effectiveQueryString.value)
      emit('submit', effectiveQueryString.value)
      isMenuExplicitlyClosed.value = true
      isFocused.value = false
      inputRef.value?.blur()
    }
  } else if (e.key === 'Backspace' && rawInput.value === '' && tags.value.length > 0) {
    removeTag(tags.value[tags.value.length - 1].id)
    emit('search', effectiveQueryString.value)
  } else if (e.key === 'Escape') {
    isMenuExplicitlyClosed.value = true
    isFocused.value = false
    inputRef.value?.blur()
  }
}

const handleTagSuggestionSelect = (suggestion: AutoTagResult) => {
  addTag(suggestion.tag)
  rawInput.value = ''
  emit('search', effectiveQueryString.value)
}

const handleResultSelect = (item: SearchResultItem) => {
  emit('select-result', item)
  isFocused.value = false
  isMenuExplicitlyClosed.value = true
  if (item.link) {
    router.push(item.link)
  } else if (item.itemType === 'ClientPc' || item.itemType === 'endpoint') {
    router.push({ path: '/dashboard/clients', query: { selected: item.id, hostname: item.name } })
  } else if (item.itemType === 'Machine' || item.itemType === 'station') {
    router.push({ path: '/dashboard/machines', query: { id: item.id, search: item.name } })
  } else if (item.itemType === 'Ticket') {
    router.push({ path: '/dashboard/tickets', query: { ticketId: item.id } })
  } else {
    router.push(`/dashboard/inventory/${item.id}`)
  }
}

const handleKeySelect = (key: string) => {
  selectKeySuggestion(key)
  isMenuExplicitlyClosed.value = false
  inputRef.value?.focus()
  isFocused.value = true
}

const handleValueSelect = (val: string) => {
  selectValueSuggestion(val)
  emit('search', effectiveQueryString.value)
  isMenuExplicitlyClosed.value = false
  inputRef.value?.focus()
  isFocused.value = true
}

const handleSelectTagValue = (key: string, value: string) => {
  addTag({
    id: `tag-${key}-${value}`,
    key,
    value,
    removable: true
  })
  rawInput.value = ''
  emit('search', effectiveQueryString.value)
  isMenuExplicitlyClosed.value = false
  inputRef.value?.focus()
  isFocused.value = true
}

const handleClear = () => {
  clearAllTags()
  isMenuExplicitlyClosed.value = true
  isFocused.value = false
  emit('search', '')
}

const handleGlobalKeydown = (e: KeyboardEvent) => {
  const target = e.target as HTMLElement | null
  const isInputTarget = target && (
    target.tagName === 'INPUT' || 
    target.tagName === 'TEXTAREA' || 
    target.isContentEditable
  )

  // 1. Ctrl+K, Cmd+K, Ctrl+P, or Cmd+P: Focus search input
  if ((e.metaKey || e.ctrlKey) && (e.key.toLowerCase() === 'k' || e.key.toLowerCase() === 'p')) {
    if (props.config?.instanceId === 'global') {
      return
    }
    if (isGlobalModalOpen.value) {
      return
    }
    e.preventDefault()
    inputRef.value?.focus()
    inputRef.value?.select()
    isFocused.value = true
    isMenuExplicitlyClosed.value = false
    handleInputChange(rawInput.value)
    return
  }

  // 2. / (Slash) global search trigger when not inside an input
  if (e.key === '/' && !isInputTarget && !e.ctrlKey && !e.metaKey && !e.altKey) {
    if (isGlobalModalOpen.value) {
      return
    }
    e.preventDefault()
    inputRef.value?.focus()
    inputRef.value?.select()
    isFocused.value = true
    isMenuExplicitlyClosed.value = false
    handleInputChange(rawInput.value)
    return
  }
}

function handleClickOutside(e: MouseEvent) {
  if (isInteractingWithDropdown.value) return
  const path = (e.composedPath ? e.composedPath() : []) as Node[]
  const target = e.target as Node | null

  // Guard against DOM re-paints where target is detached from body
  if (target && !document.body.contains(target)) {
    return
  }

  const isInside = (containerRef.value && path.includes(containerRef.value)) ||
                   (containerRef.value && target && containerRef.value.contains(target))
  if (!isInside) {
    isMenuExplicitlyClosed.value = true
    isFocused.value = false
  }
}

const handleBlur = (e: FocusEvent) => {
  if (isInteractingWithDropdown.value) return
  if (e.relatedTarget instanceof Node && containerRef.value?.contains(e.relatedTarget)) {
    return
  }
  setTimeout(() => {
    if (!isInteractingWithDropdown.value && !containerRef.value?.contains(document.activeElement)) {
      isFocused.value = false
    }
  }, 250)
}

onMounted(() => {
  fetchSearchKeys()
  handleInputChange(rawInput.value)
  if (props.immediate) {
    executeSearch()
  }
  if (typeof window !== 'undefined') {
    window.addEventListener('keydown', handleGlobalKeydown)
    window.addEventListener('click', handleClickOutside)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('keydown', handleGlobalKeydown)
    window.removeEventListener('click', handleClickOutside)
  }
})
</script>

<template>
  <div ref="containerRef" class="relative w-full">
    <!-- Main Search Input Container -->
    <div
      class="flex flex-wrap items-center gap-2 p-2 bg-card border rounded-xl transition-all shadow-md"
      :class="isFocused ? 'border-primary ring-2 ring-primary/20' : 'border-border hover:border-border/80'"
      @click="inputRef?.focus(); isFocused = true; isMenuExplicitlyClosed = false"
    >
      <div class="pl-2 text-muted-foreground">
        <SearchIcon class="w-4 h-4" />
      </div>

      <!-- Active Tag Pills -->
      <TagPillList :tags="tags" @remove="removeTag" />

      <!-- Free Text Input -->
      <input
        ref="inputRef"
        v-model="rawInput"
        type="text"
        :placeholder="tags.length === 0 ? (props.config?.placeholder || 'Search everything (e.g. Siemens, OP10, 15kW)...') : 'Type to add more filters...'"
        class="flex-1 min-w-[160px] bg-transparent border-0 text-sm font-semibold text-foreground placeholder:text-muted-foreground placeholder:font-normal focus:outline-none focus:ring-0 py-1"
        @input="isMenuExplicitlyClosed = false; handleInputChange(($event.target as HTMLInputElement).value)"
        @focus="isFocused = true; isMenuExplicitlyClosed = false"
        @blur="handleBlur"
        @keydown="handleKeydown"
      />

      <!-- Clear / Shortcut Badges -->
      <div class="flex items-center gap-1.5 pr-2">
        <button
          v-if="tags.length > 0 || rawInput.length > 0"
          type="button"
          @click.stop="handleClear"
          class="p-1 text-muted-foreground hover:text-foreground rounded-lg transition-colors cursor-pointer"
          title="Clear search"
        >
          <X class="w-4 h-4" />
        </button>

        <div v-if="props.config?.showGlobalShortcut !== false" class="hidden sm:flex items-center gap-1.5 px-2 py-0.5 bg-muted/60 border border-border/80 rounded-lg text-[10px] font-mono text-muted-foreground select-none">
          <div class="flex items-center gap-0.5">
            <Command class="w-3 h-3" />
            <span>K</span>
          </div>
          <span class="text-muted-foreground/60 font-sans">•</span>
          <span class="text-primary font-sans tracking-tight" title="Press Ctrl+Space for autocomplete suggestions">^Space</span>
        </div>
      </div>
    </div>

    <!-- Suggestions and Results Dropdown Popover -->
    <div
      v-if="showDropdown"
      class="absolute left-0 right-0 top-full mt-2 z-50 animate-in fade-in slide-in-from-top-2 duration-150"
      @mousedown="isInteractingWithDropdown = true"
      @mouseup="setTimeout(() => { isInteractingWithDropdown = false }, 200)"
      @touchstart="isInteractingWithDropdown = true"
      @touchend="setTimeout(() => { isInteractingWithDropdown = false }, 200)"
    >
      <AutoTagSuggestionDropdown
        :auto-suggestions="autoSuggestions"
        :results="results"
        :primary-results="primaryResults"
        :cross-table-results="crossTableResults"
        :matching-keys="matchingKeys"
        :value-suggestions="valueSuggestions"
        :active-pending-key="activePendingKey"
        :search-key-groups="searchKeyGroups"
        :known-key-values="dynamicKnownKeyValues"
        :is-loading="isLoading"
        :free-text="freeText"
        :instance-id="props.config?.instanceId || 'global'"
        @select-tag="handleTagSuggestionSelect"
        @select-result="handleResultSelect"
        @select-key="handleKeySelect"
        @select-value="handleValueSelect"
        @select-tag-value="handleSelectTagValue"
      />
    </div>
  </div>
</template>
