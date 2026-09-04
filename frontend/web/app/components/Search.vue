<script setup lang="ts">
import { useSearch } from '~/composables/useSearch'
import { watchDebounced } from '@vueuse/core'
import { Popover, PopoverTrigger, PopoverContent } from '~/components/ui/popover'
import { Command, CommandList, CommandEmpty, CommandGroup, CommandItem, CommandSeparator } from '~/components/ui/command'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import { Badge } from '~/components/ui/badge'
import { ScrollArea } from '~/components/ui/scroll-area'

/**
 * @component Search
 * @description The central search interface for the Heimdall ecosystem. 
 * Supports unified tagging, asynchronous results, and advanced filtering.
 * 
 * @example
 * ```vue
 * <Search 
 *   placeholder="Search machines..." 
 *   :immediate="true" 
 *   @search="onSearch" 
 * />
 * ```
 */

/**
 * Props for the Search component.
 */
const props = defineProps<{
  /** Optional placeholder text for the input field. */
  placeholder?: string
  /** Whether to trigger an initial search immediately on mount if a query exists. @default false */
  immediate?: boolean
}>()

/**
 * Emitted events for the Search component.
 */
const emit = defineEmits<{
  /** Fired whenever the search query changes (debounced). */
  search: [query: string]
}>()

const { metaSymbol } = useShortcuts()
const router = useRouter()
const { query, results, searchKeys, isLoading, recommendations, fetchKeys, performSearch } = useSearch()

const openDropdown = ref(false)
const openAdvanced = ref(false)
const inputRef = ref<HTMLInputElement | null>(null)

// Split query into tags and current typing text
const tags = computed(() => {
  const parts = query.value.match(/(\w+:"[^"]*")|(\w+:[^\s]*)|([^\s]+)/g) || []
  return parts.filter(p => p.includes(':'))
})

const currentText = ref('')

defineShortcuts({
  Meta_K: () => {
    inputRef.value?.focus()
    openDropdown.value = true
  },
})

onMounted(() => {
  fetchKeys()
  if (props.immediate && query.value) {
    performSearch(query.value)
  }
})

watchDebounced(query, (newQuery) => {
  if (newQuery) {
    performSearch(newQuery)
    emit('search', newQuery)
    openDropdown.value = true
  } else {
    results.value = []
    emit('search', '')
  }
}, { debounce: 300 })

function handleSelectLink(link: string) {
  router.push(link)
  openDropdown.value = false
}

function handleAdvancedSearch(advancedQuery: string) {
  query.value = advancedQuery
  openAdvanced.value = false
  performSearch(advancedQuery)
  openDropdown.value = true
}

function insertRecommendation(key: string) {
  const currentQuery = query.value.trim()
  const parts = currentQuery.split(' ')
  const lastPart = parts[parts.length - 1]
  
  if (lastPart && !lastPart.includes(':') && key.toLowerCase().startsWith(lastPart.toLowerCase())) {
    parts[parts.length - 1] = `${key}:`
  } else {
    parts.push(`${key}:`)
  }
  
  query.value = parts.join(' ').trim() + ' '
  inputRef.value?.focus()
}

function removeTag(tag: string) {
  query.value = query.value.replace(tag, '').replace(/\s+/g, ' ').trim()
  inputRef.value?.focus()
}

function clearSearch() {
  query.value = ''
  results.value = []
  openDropdown.value = false
  inputRef.value?.focus()
}
</script>

<template>
  <div class="relative w-full group/search">
    <Popover v-model:open="openDropdown">
      <PopoverTrigger as-child>
        <div 
          class="flex items-center gap-2 w-full bg-slate-900/40 border border-slate-800 rounded-2xl p-1.5 focus-within:ring-2 focus-within:ring-indigo-500/40 focus-within:border-indigo-500/40 transition-all min-h-[56px]"
          @click="inputRef?.focus()"
        >
          <Icon name="i-lucide-search" class="ml-3 size-5 text-slate-500 group-focus-within/search:text-indigo-400 transition-colors shrink-0" />
          
          <!-- Scrollable Tags Area -->
          <div class="flex-1 flex items-center gap-2 overflow-hidden">
            <ScrollArea orientation="horizontal" class="w-full">
              <div class="flex items-center gap-1.5 py-1">
                <Badge 
                  v-for="tag in tags" 
                  :key="tag" 
                  variant="secondary" 
                  class="bg-indigo-500/10 text-indigo-400 border-indigo-500/20 hover:bg-indigo-500/20 pr-1 flex items-center gap-1 whitespace-nowrap"
                >
                  <span class="text-[10px] font-black uppercase tracking-tight">{{ tag.split(':')[0] }}</span>
                  <span class="text-[10px] font-medium text-slate-400">{{ tag.split(':')[1] }}</span>
                  <Button variant="ghost" size="icon" class="h-4 w-4 hover:bg-transparent" @click.stop="removeTag(tag)">
                    <Icon name="i-lucide-x" class="size-3" />
                  </Button>
                </Badge>
                
                <input
                  ref="inputRef"
                  v-model="query"
                  type="text"
                  :placeholder="tags.length === 0 ? (placeholder || 'Search infrastructure...') : ''"
                  class="flex-1 min-w-[120px] bg-transparent border-none py-1 text-sm text-slate-200 placeholder:text-slate-600 focus:outline-none"
                  @focus="openDropdown = query.length > 0"
                  @keydown.enter.prevent="performSearch(query, true); openDropdown = true"
                />
              </div>
            </ScrollArea>
          </div>

          <!-- Actions -->
          <div class="flex items-center gap-1 pr-2 shrink-0">
            <Button v-if="query" variant="ghost" size="icon" class="h-8 w-8 text-slate-500 hover:text-slate-300" @click.stop="clearSearch">
              <Icon name="i-lucide-circle-x" class="size-4" />
            </Button>
            <div class="flex items-center gap-1 px-2 h-7 bg-slate-800/50 border border-slate-700/50 rounded-lg">
               <Kbd class="text-[10px] text-slate-500">{{ metaSymbol }}</Kbd>
               <Kbd class="text-[10px] text-slate-500">K</Kbd>
            </div>
            <Button variant="ghost" size="icon" class="h-8 w-8 text-slate-500 hover:text-indigo-400" @click.stop="openAdvanced = true">
              <Icon name="i-lucide-settings-2" class="size-4" />
            </Button>
          </div>
        </div>
      </PopoverTrigger>
      
      <PopoverContent 
        class="p-0 w-[var(--radix-popover-trigger-width)] bg-slate-950 border-slate-800 shadow-2xl shadow-indigo-500/10 overflow-hidden" 
        align="start" 
        :side-offset="8"
        @open-auto-focus.prevent
      >
        <Command class="bg-transparent" @keydown.esc="openDropdown = false">
          <CommandList class="max-h-[450px]">
            <div v-if="isLoading" class="p-8 flex flex-col items-center justify-center gap-2">
              <Icon name="i-lucide-loader-2" class="size-6 animate-spin text-indigo-500" />
              <span class="text-[10px] font-black uppercase tracking-widest text-slate-500">Scanning Knowledge Base...</span>
            </div>
            
            <template v-else>
              <CommandEmpty v-if="query.length > 0 && results.length === 0" class="p-12 text-center">
                <div class="size-16 rounded-full bg-slate-900 border border-slate-800 flex items-center justify-center mx-auto mb-4">
                  <Icon name="i-lucide-search-x" class="size-8 text-slate-700" />
                </div>
                <h3 class="text-sm font-black text-slate-200 uppercase tracking-widest">No results found</h3>
                <p class="text-xs text-slate-500 mt-2 mb-4">Try adjusting your filters or use advanced search</p>
                <Button variant="outline" size="sm" class="text-indigo-400 border-indigo-500/20 hover:bg-indigo-500/10" @click="openAdvanced = true">
                  Configure Advanced Filters
                </Button>
              </CommandEmpty>

              <template v-if="recommendations.length > 0">
                <CommandGroup 
                  v-for="group in recommendations" 
                  :key="group.group" 
                  :heading="group.group" 
                  class="px-2 pt-2"
                >
                  <div class="grid grid-cols-2 sm:grid-cols-3 gap-1 p-1">
                    <CommandItem
                      v-for="key in group.keys"
                      :key="key"
                      :value="key"
                      class="flex items-center gap-2 rounded-lg cursor-pointer hover:bg-slate-900 border border-transparent hover:border-slate-800 p-2"
                      @select="insertRecommendation(key)"
                    >
                      <Icon name="i-lucide-tag" class="size-3 text-emerald-500" />
                      <span class="text-[10px] font-black uppercase tracking-widest text-slate-300">{{ key }}</span>
                    </CommandItem>
                  </div>
                </CommandGroup>
              </template>

              <CommandSeparator v-if="recommendations.length > 0 && results.length > 0" class="bg-slate-800/50 my-2" />

              <CommandGroup v-if="results.length > 0" heading="Top Matches" class="px-2 pb-2">
                <template v-for="result in results" :key="result?.id">
                  <CommandItem
                    v-if="result"
                    :value="result.name + result.displayName"
                    class="flex items-start gap-4 rounded-xl p-3 cursor-pointer hover:bg-slate-900/50 transition-all border border-transparent hover:border-slate-800 mb-1"
                    @select="handleSelectLink(result.link)"
                  >
                  <div class="size-10 rounded-xl bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center shrink-0">
                    <Icon 
                      :name="result.type === 'component' ? 'i-lucide-package' : 'i-lucide-monitor'" 
                      class="size-5 text-indigo-400" 
                    />
                  </div>
                  <div class="flex flex-col min-w-0 flex-1">
                    <div class="flex items-center justify-between">
                      <span class="text-sm font-black text-slate-100 truncate uppercase tracking-tight">{{ result.name }}</span>
                      <Badge v-if="result.technology" variant="outline" class="text-[8px] font-black uppercase bg-slate-900 border-slate-800 text-slate-500">
                        {{ result.technology }}
                      </Badge>
                    </div>
                    <span v-if="result.displayName" class="text-[10px] text-slate-400 truncate mt-0.5">{{ result.displayName }}</span>
                    <div class="flex items-center gap-3 mt-1.5">
                      <span class="text-[9px] text-slate-600 font-mono tracking-tighter bg-slate-900 px-1.5 py-0.5 rounded border border-slate-800">ID: {{ result.id.slice(0, 8) }}</span>
                    </div>
                  </div>
                </CommandItem>
                </template>
              </CommandGroup>
              
              <div v-if="query.length === 0" class="p-12 text-center">
                 <div class="size-20 rounded-full bg-slate-900 border border-slate-800 flex items-center justify-center mx-auto mb-6 shadow-[inset_0_0_20px_rgba(0,0,0,0.5)]">
                    <Icon name="i-lucide-command" class="size-8 text-indigo-500/50" />
                 </div>
                 <h3 class="text-xs font-black text-slate-200 uppercase tracking-[0.2em] mb-2">Global Search Active</h3>
                 <p class="text-[11px] text-slate-500 max-w-[280px] mx-auto leading-relaxed mb-6 font-medium">
                   Search across components, machines, and networks with unified tagging.
                 </p>
                 <div class="flex items-center justify-center gap-3">
                    <div class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800">
                       <Kbd class="text-[10px] text-slate-400">manufacturer:dell</Kbd>
                    </div>
                    <div class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800">
                       <Kbd class="text-[10px] text-slate-400">type:vision</Kbd>
                    </div>
                 </div>
              </div>
            </template>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>

    <!-- Advanced Search Dialog -->
    <Dialog v-model:open="openAdvanced">
      <DialogContent class="sm:max-w-[600px] bg-slate-950 border-slate-800">
        <DialogHeader>
          <DialogTitle class="text-slate-100 uppercase tracking-widest font-black">Advanced Inventory Search</DialogTitle>
          <DialogDescription class="text-slate-500">
            Use specific fields and custom tags to narrow down your search.
          </DialogDescription>
        </DialogHeader>
        <AdvancedSearchUI 
          :initial-query="query" 
          :available-keys="searchKeys"
          @search="handleAdvancedSearch" 
          @close="openAdvanced = false"
        />
      </DialogContent>
    </Dialog>
  </div>
</template>

<style scoped>
input::placeholder {
  letter-spacing: 0.1em;
  text-transform: uppercase;
  font-weight: 900;
  font-size: 10px;
  opacity: 0.4;
}
</style>

<style scoped>
input::placeholder {
  letter-spacing: 0.05em;
  text-transform: uppercase;
  font-weight: 900;
  font-size: 9px;
  opacity: 0.5;
}
</style>


