<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { SearchInstanceConfig, SearchResultItem, AutoTagResult } from '~/types/search'
import { useOmniSearch } from '~/composables/useOmniSearch'
import { Search as SearchIcon, X, Command } from 'lucide-vue-next'
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
  (e: 'select-result', item: SearchResultItem): void
}>()

const router = useRouter()
const inputRef = ref<HTMLInputElement | null>(null)

const {
  rawInput,
  tags,
  freeText,
  autoSuggestions,
  results,
  searchKeyGroups,
  isLoading,
  effectiveQueryString,
  handleInputChange,
  addTag,
  removeTag,
  clearAllTags,
  executeSearch,
  fetchSearchKeys
} = useOmniSearch(props.config)

const isFocused = ref(false)

const showDropdown = computed(() => {
  return (isFocused.value || rawInput.value.length > 0) && (autoSuggestions.value.length > 0 || results.value.length > 0 || searchKeyGroups.value.length > 0)
})

// Emit live search queries to parent components whenever tags or input change
watch(effectiveQueryString, (newVal) => {
  emit('search', newVal)
})

const handleKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Enter') {
    e.preventDefault()
    if (autoSuggestions.value.length > 0) {
      addTag(autoSuggestions.value[0].tag)
      rawInput.value = ''
    } else {
      executeSearch()
      emit('search', effectiveQueryString.value)
    }
  } else if (e.key === 'Backspace' && rawInput.value === '' && tags.value.length > 0) {
    removeTag(tags.value[tags.value.length - 1].id)
    emit('search', effectiveQueryString.value)
  } else if (e.key === 'Escape') {
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
  if (item.link) {
    router.push(item.link)
  } else if (item.itemType === 'ClientPc') {
    router.push('/dashboard/clients')
  } else {
    router.push(`/dashboard/inventory/${item.id}`)
  }
}

const handleKeySelect = (key: string) => {
  rawInput.value = `${key}:`
  inputRef.value?.focus()
}

const handleClear = () => {
  clearAllTags()
  emit('search', '')
}

const handleGlobalKeydown = (e: KeyboardEvent) => {
  if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
    e.preventDefault()
    inputRef.value?.focus()
    isFocused.value = true
  }
}

onMounted(() => {
  fetchSearchKeys()
  if (props.immediate) {
    executeSearch()
  }
  if (typeof window !== 'undefined') {
    window.addEventListener('keydown', handleGlobalKeydown)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('keydown', handleGlobalKeydown)
  }
})
</script>

<template>
  <div class="relative w-full">
    <!-- Main Search Input Container -->
    <div
      class="flex flex-wrap items-center gap-2 p-2 bg-slate-900 border rounded-2xl transition-all shadow-lg"
      :class="isFocused ? 'border-indigo-500 ring-4 ring-indigo-500/10' : 'border-slate-800 hover:border-slate-700'"
      @click="inputRef?.focus()"
    >
      <div class="pl-2 text-slate-500">
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
        class="flex-1 min-w-[160px] bg-transparent border-0 text-sm font-bold text-slate-100 placeholder:text-slate-500 placeholder:font-normal focus:outline-none focus:ring-0 py-1"
        @input="handleInputChange(($event.target as HTMLInputElement).value)"
        @focus="isFocused = true"
        @blur="setTimeout(() => isFocused = false, 250)"
        @keydown="handleKeydown"
      />

      <!-- Clear / Shortcut Badges -->
      <div class="flex items-center gap-1.5 pr-2">
        <button
          v-if="tags.length > 0 || rawInput.length > 0"
          type="button"
          @click.stop="handleClear"
          class="p-1 text-slate-500 hover:text-slate-300 rounded-lg transition-colors"
          title="Clear search"
        >
          <X class="w-4 h-4" />
        </button>

        <div v-if="props.config?.showGlobalShortcut !== false" class="hidden sm:flex items-center gap-1 px-2 py-0.5 bg-slate-800 border border-slate-700 rounded-lg text-[10px] font-mono text-slate-400">
          <Command class="w-3 h-3" />
          <span>K</span>
        </div>
      </div>
    </div>

    <!-- Suggestions and Results Dropdown Popover -->
    <div
      v-if="showDropdown"
      class="absolute left-0 right-0 top-full mt-2 z-50 animate-in fade-in slide-in-from-top-2 duration-150"
    >
      <AutoTagSuggestionDropdown
        :auto-suggestions="autoSuggestions"
        :results="results"
        :search-key-groups="searchKeyGroups"
        :is-loading="isLoading"
        :free-text="freeText"
        @select-tag="handleTagSuggestionSelect"
        @select-result="handleResultSelect"
        @select-key="handleKeySelect"
      />
    </div>
  </div>
</template>
