<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { AutoTagResult, SearchResultItem, SearchGroup, KeyLookupSuggestion } from '~/types/search'
import { 
  Sparkles, 
  Search, 
  Tag, 
  ArrowRight, 
  CornerDownLeft, 
  Database, 
  Layers, 
  Network,
  SlidersHorizontal,
  ChevronRight,
  Check,
  Filter,
  BookOpen
} from 'lucide-vue-next'
import { BASELINE_KNOWN_KEY_VALUES } from '~/utils/search/indexingTables'

const props = withDefaults(
  defineProps<{
    autoSuggestions?: AutoTagResult[]
    results?: SearchResultItem[]
    primaryResults?: SearchResultItem[]
    crossTableResults?: SearchResultItem[]
    matchingKeys?: Array<{ key: string; label?: string; description?: string }>
    valueSuggestions?: KeyLookupSuggestion[]
    activePendingKey?: string
    searchKeyGroups?: SearchGroup[]
    knownKeyValues?: Record<string, { values: string[]; description: string }>
    isLoading?: boolean
    freeText?: string
    instanceId?: string
  }>(),
  {
    autoSuggestions: () => [],
    results: () => [],
    primaryResults: () => [],
    crossTableResults: () => [],
    matchingKeys: () => [],
    valueSuggestions: () => [],
    activePendingKey: '',
    searchKeyGroups: () => [],
    knownKeyValues: () => ({}),
    isLoading: false,
    freeText: '',
    instanceId: 'global'
  }
)

const emit = defineEmits<{
  (e: 'select-tag', suggestion: AutoTagResult): void
  (e: 'select-result', item: SearchResultItem): void
  (e: 'select-key', key: string): void
  (e: 'select-value', value: string): void
  (e: 'select-tag-value', key: string, value: string): void
}>()

// Active View Mode: 'results' (live suggestions/matches) or 'tags' (full tag & stored values browser)
const activeView = ref<'results' | 'tags'>('results')

// Tag Browser State
const selectedBrowseKey = ref<string>('status')
const tagSearchQuery = ref<string>('')
const tagValueSearchQuery = ref<string>('')

// Merge knownKeyValues with baseline known values
const allKnownKeyValues = computed<Record<string, { values: string[]; description: string }>>(() => {
  const merged: Record<string, { values: string[]; description: string }> = {}
  
  // Seed baseline
  for (const [k, v] of Object.entries(BASELINE_KNOWN_KEY_VALUES)) {
    merged[k] = { description: v.description, values: [...v.values] }
  }

  // Merge harvested dynamic values
  if (props.knownKeyValues && Object.keys(props.knownKeyValues).length > 0) {
    for (const [k, v] of Object.entries(props.knownKeyValues)) {
      if (!merged[k]) {
        merged[k] = { description: v.description, values: [...v.values] }
      } else {
        const existingLower = new Set(merged[k].values.map(val => val.toLowerCase()))
        for (const val of v.values) {
          if (!existingLower.has(val.toLowerCase())) {
            existingLower.add(val.toLowerCase())
            merged[k].values.push(val)
          }
        }
      }
    }
  }

  return merged
})

// Total stored values count across all tags
const totalStoredValuesCount = computed(() => {
  return Object.values(allKnownKeyValues.value).reduce((sum, item) => sum + item.values.length, 0)
})

// Filtered Tag Keys in the browser
const availableTagKeys = computed(() => {
  const keys = Object.keys(allKnownKeyValues.value)
  if (!tagSearchQuery.value.trim()) return keys
  const q = tagSearchQuery.value.toLowerCase().trim()
  return keys.filter(k => 
    k.toLowerCase().includes(q) || 
    allKnownKeyValues.value[k]?.description.toLowerCase().includes(q) ||
    allKnownKeyValues.value[k]?.values.some(v => v.toLowerCase().includes(q))
  )
})

// Values for currently selected tag
const activeTagValues = computed(() => {
  const key = selectedBrowseKey.value
  const data = allKnownKeyValues.value[key]
  if (!data) return []
  if (!tagValueSearchQuery.value.trim()) return data.values
  const q = tagValueSearchQuery.value.toLowerCase().trim()
  return data.values.filter(v => v.toLowerCase().includes(q))
})

// Global matching stored values when user types in search bar
const matchingStoredValuesGlobal = computed(() => {
  if (!props.freeText || props.freeText.trim().length < 2) return []
  const q = props.freeText.toLowerCase().trim()
  const matches: Array<{ key: string; value: string; description: string }> = []
  
  for (const [key, data] of Object.entries(allKnownKeyValues.value)) {
    for (const val of data.values) {
      if (val.toLowerCase().includes(q)) {
        matches.push({ key, value: val, description: data.description })
        if (matches.length >= 6) break
      }
    }
    if (matches.length >= 6) break
  }
  return matches
})

// Auto switch default selected browse key if activePendingKey changes
watch(() => props.activePendingKey, (newKey) => {
  if (newKey && allKnownKeyValues.value[newKey.toLowerCase()]) {
    selectedBrowseKey.value = newKey.toLowerCase()
  }
}, { immediate: true })

// Stage 1 Primary Items
const primaryItems = computed(() => {
  if (props.primaryResults && props.primaryResults.length > 0) {
    return props.primaryResults
  }
  return props.results.filter(r => !r.isCrossTable)
})

// Stage 3 Cross-Table Items
const crossTableItems = computed(() => {
  if (props.crossTableResults && props.crossTableResults.length > 0) {
    return props.crossTableResults
  }
  return props.results.filter(r => r.isCrossTable)
})

// Total live matches count
const totalMatchesCount = computed(() => {
  return primaryItems.value.length + crossTableItems.value.length + props.autoSuggestions.length + props.valueSuggestions.length
})

function handleBrowseTagClick(key: string) {
  selectedBrowseKey.value = key
  tagValueSearchQuery.value = ''
}

function handleAddTagValue(key: string, val: string) {
  emit('select-tag-value', key, val)
}
</script>

<template>
  <div
    data-omni-dropdown="true"
    class="w-full bg-card/98 backdrop-blur-xl border border-border rounded-xl shadow-2xl overflow-hidden divide-y divide-border/60 z-50 text-card-foreground transition-all"
  >
    <!-- View Switcher Toolbar -->
    <div class="px-3 py-2 bg-muted/30 flex items-center justify-between flex-wrap gap-2 text-xs border-b border-border/60">
      <div class="flex items-center gap-1.5 p-0.5 bg-background/80 rounded-lg border border-border/80">
        <button
          type="button"
          @click="activeView = 'results'"
          class="px-2.5 py-1 rounded-md text-[10px] font-bold uppercase tracking-wider transition-all flex items-center gap-1.5 cursor-pointer"
          :class="activeView === 'results' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
        >
          <Search class="w-3 h-3" />
          <span>Results & Autocomplete</span>
          <span
            v-if="totalMatchesCount > 0"
            class="px-1.5 py-0.2 rounded-full text-[9px] font-mono leading-none"
            :class="activeView === 'results' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'"
          >
            {{ totalMatchesCount }}
          </span>
        </button>

        <button
          type="button"
          @click="activeView = 'tags'"
          class="px-2.5 py-1 rounded-md text-[10px] font-bold uppercase tracking-wider transition-all flex items-center gap-1.5 cursor-pointer"
          :class="activeView === 'tags' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
        >
          <SlidersHorizontal class="w-3 h-3" />
          <span>Browse Tags & Stored Values</span>
          <span
            class="px-1.5 py-0.2 rounded-full text-[9px] font-mono leading-none"
            :class="activeView === 'tags' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'"
          >
            {{ availableTagKeys.length }}
          </span>
        </button>
      </div>

      <div class="flex items-center gap-2 text-[10px] text-muted-foreground font-mono">
        <span class="hidden sm:inline">{{ totalStoredValuesCount }} indexed values</span>
      </div>
    </div>

    <!-- VIEW 1: Full Tag & Stored Values Explorer -->
    <div v-if="activeView === 'tags'" class="grid grid-cols-1 md:grid-cols-12 max-h-[380px] overflow-hidden">
      <!-- Left Column: Tag Categories Selector -->
      <div class="md:col-span-5 border-r border-border/60 bg-muted/20 flex flex-col max-h-[380px]">
        <div class="p-2 border-b border-border/60">
          <div class="relative">
            <Search class="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="tagSearchQuery"
              type="text"
              placeholder="Search tag categories..."
              class="w-full pl-8 pr-2.5 py-1 text-xs bg-background border border-border rounded-lg text-foreground placeholder:text-muted-foreground focus:outline-none focus:border-primary"
            />
          </div>
        </div>

        <div class="overflow-y-auto custom-scrollbar p-1.5 space-y-1 flex-1">
          <button
            v-for="key in availableTagKeys"
            :key="key"
            type="button"
            @click="handleBrowseTagClick(key)"
            class="w-full flex items-center justify-between p-2 rounded-lg text-left transition-all group cursor-pointer"
            :class="selectedBrowseKey === key ? 'bg-primary/10 text-primary border border-primary/30 font-bold' : 'hover:bg-muted/60 text-muted-foreground hover:text-foreground border border-transparent'"
          >
            <div class="flex items-center gap-2 min-w-0">
              <Tag class="w-3.5 h-3.5 shrink-0" :class="selectedBrowseKey === key ? 'text-primary' : 'text-muted-foreground'" />
              <div class="truncate">
                <span class="font-mono text-xs font-bold">{{ key }}:</span>
                <p class="text-[9px] opacity-80 truncate text-muted-foreground">{{ allKnownKeyValues[key]?.description }}</p>
              </div>
            </div>
            <div class="flex items-center gap-1.5 shrink-0 ml-2">
              <span class="text-[9px] px-1.5 py-0.5 rounded-full bg-muted text-muted-foreground font-mono border border-border/60">
                {{ allKnownKeyValues[key]?.values.length || 0 }}
              </span>
              <ChevronRight class="w-3 h-3 opacity-0 group-hover:opacity-100 transition-opacity" :class="selectedBrowseKey === key ? 'opacity-100 text-primary' : ''" />
            </div>
          </button>
        </div>
      </div>

      <!-- Right Column: Stored Values Grid for Selected Tag -->
      <div class="md:col-span-7 flex flex-col max-h-[380px] bg-card">
        <!-- Tag Header & Search -->
        <div class="p-3 border-b border-border/60 bg-muted/10 space-y-2">
          <div class="flex items-center justify-between gap-2">
            <div>
              <div class="flex items-center gap-1.5">
                <span class="font-mono text-xs font-black text-primary bg-primary/10 px-2 py-0.5 rounded-md border border-primary/20">
                  {{ selectedBrowseKey }}:
                </span>
                <span class="text-[10px] text-muted-foreground font-bold uppercase tracking-wider">Stored Values</span>
              </div>
              <p class="text-[10px] text-muted-foreground mt-0.5">
                {{ allKnownKeyValues[selectedBrowseKey]?.description }}
              </p>
            </div>

            <button
              type="button"
              @click="emit('select-key', selectedBrowseKey)"
              class="px-2 py-1 bg-muted hover:bg-muted/80 text-foreground border border-border rounded-lg text-[9px] font-bold uppercase tracking-wider transition-colors shrink-0 cursor-pointer"
              title="Apply key without value"
            >
              Key Only
            </button>
          </div>

          <div class="relative">
            <Search class="w-3 h-3 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="tagValueSearchQuery"
              type="text"
              :placeholder="`Filter ${allKnownKeyValues[selectedBrowseKey]?.values.length || 0} values in ${selectedBrowseKey}...`"
              class="w-full pl-7 pr-2.5 py-1 text-xs bg-background border border-border rounded-lg text-foreground placeholder:text-muted-foreground focus:outline-none focus:border-primary"
            />
          </div>
        </div>

        <!-- Values Grid -->
        <div class="p-3 overflow-y-auto custom-scrollbar flex-1">
          <div v-if="activeTagValues.length === 0" class="text-center py-8 text-xs text-muted-foreground">
            No stored values match "{{ tagValueSearchQuery }}".
          </div>

          <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
            <button
              v-for="val in activeTagValues"
              :key="val"
              type="button"
              @click="handleAddTagValue(selectedBrowseKey, val)"
              class="flex items-center justify-between p-2 rounded-lg bg-muted/40 hover:bg-primary/10 border border-border hover:border-primary/40 text-left transition-all group cursor-pointer"
            >
              <div class="flex items-center gap-2 min-w-0">
                <span class="font-mono text-xs font-bold text-foreground group-hover:text-primary truncate">{{ val }}</span>
              </div>
              <div class="flex items-center gap-1 text-[9px] font-semibold text-muted-foreground group-hover:text-primary transition-colors shrink-0">
                <span class="opacity-0 group-hover:opacity-100 transition-opacity">+ Filter</span>
                <CornerDownLeft class="w-3 h-3" />
              </div>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- VIEW 2: Results & Live Autocomplete -->
    <div v-else class="divide-y divide-border/60">
      <!-- Stored Values Matching Query (Direct Tag Value Discovery) -->
      <div v-if="matchingStoredValuesGlobal.length > 0" class="p-3 bg-primary/5">
        <div class="flex items-center justify-between text-[8px] font-black uppercase tracking-widest text-primary mb-2">
          <div class="flex items-center gap-1.5">
            <SlidersHorizontal class="w-3 h-3 text-primary shrink-0" />
            <span>Matching Stored Tag Values (Click to Filter)</span>
          </div>
          <button
            type="button"
            @click="activeView = 'tags'"
            class="text-[9px] text-primary hover:underline font-bold uppercase tracking-wider cursor-pointer"
          >
            Browse All Tags →
          </button>
        </div>
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="item in matchingStoredValuesGlobal"
            :key="`${item.key}-${item.value}`"
            type="button"
            @click="handleAddTagValue(item.key, item.value)"
            class="flex items-center gap-1.5 px-2.5 py-1 bg-card hover:bg-primary/10 text-foreground hover:text-primary border border-border hover:border-primary/40 rounded-lg text-xs font-mono transition-all group shadow-2xs cursor-pointer"
          >
            <span class="text-primary font-bold">{{ item.key }}:</span>
            <span class="font-semibold">{{ item.value }}</span>
            <CornerDownLeft class="w-2.5 h-2.5 text-muted-foreground group-hover:text-primary opacity-60 group-hover:opacity-100" />
          </button>
        </div>
      </div>

      <!-- Value Lookup Dropdown (Triggered when typing `key:`, e.g. tech: or status:) -->
      <div v-if="activePendingKey && valueSuggestions.length > 0" class="p-3 bg-muted/40">
        <div class="flex items-center justify-between text-[9px] font-black uppercase tracking-widest text-primary mb-2">
          <div class="flex items-center gap-1.5">
            <CornerDownLeft class="w-3.5 h-3.5 text-primary shrink-0" />
            <span>Select Value for <span class="font-mono text-foreground bg-muted px-1.5 py-0.5 rounded">{{ activePendingKey }}:</span></span>
          </div>
          <button
            type="button"
            @click="handleBrowseTagClick(activePendingKey); activeView = 'tags'"
            class="text-[9px] text-primary hover:underline font-bold cursor-pointer"
          >
            Open in Browser →
          </button>
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-1.5 max-h-48 overflow-y-auto custom-scrollbar">
          <button
            v-for="val in valueSuggestions"
            :key="val.value"
            type="button"
            @click="emit('select-value', val.value)"
            class="flex items-center justify-between p-2 rounded-lg bg-card hover:bg-accent border border-border hover:border-primary/50 text-left transition-all group cursor-pointer"
          >
            <div class="flex items-center gap-2">
              <span class="font-mono text-xs font-bold text-foreground group-hover:text-primary">{{ val.value }}</span>
              <span v-if="val.description" class="text-[9px] text-muted-foreground truncate max-w-[140px]">{{ val.description }}</span>
            </div>
            <CornerDownLeft class="w-3 h-3 text-muted-foreground group-hover:text-primary opacity-0 group-hover:opacity-100 transition-opacity shrink-0" />
          </button>
        </div>
      </div>

      <!-- Auto-Detected Tag Suggestions (Regex / Fuzzy) -->
      <div v-if="autoSuggestions.length > 0" class="p-3 bg-emerald-500/10">
        <div class="flex items-center gap-1.5 text-[8px] font-black uppercase tracking-widest text-emerald-600 dark:text-emerald-400 mb-2">
          <Sparkles class="w-3 h-3 text-emerald-600 dark:text-emerald-400 shrink-0" />
          <span>Auto-Detected Filter Pills</span>
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="s in autoSuggestions"
            :key="`${s.tag.key}-${s.tag.value}`"
            type="button"
            @click="emit('select-tag', s)"
            class="flex items-center gap-2 px-3 py-1.5 bg-emerald-500/15 hover:bg-emerald-500/25 text-emerald-700 dark:text-emerald-300 border border-emerald-500/30 rounded-full text-[9px] font-black uppercase tracking-wider transition-all shadow-xs cursor-pointer"
          >
            <span class="opacity-75 font-semibold">{{ s.tag.key }}:</span>
            <span class="font-bold">{{ s.tag.value }}</span>
            <span class="text-[8px] px-1.5 py-0.2 bg-emerald-500/20 rounded-full font-mono font-bold leading-none">
              {{ Math.round(s.confidence * 100) }}% match
            </span>
          </button>
        </div>
      </div>

      <!-- Stage 1: Primary Indexing Table Matches -->
      <div v-if="primaryItems.length > 0" class="p-2 max-h-60 overflow-y-auto custom-scrollbar">
        <div class="flex items-center justify-between text-[8px] font-black uppercase tracking-widest text-muted-foreground px-3 py-1.5">
          <span class="flex items-center gap-1.5">
            <Database class="w-3 h-3 text-primary" />
            <span>Stage 1: Primary Index Matches ({{ primaryItems.length }})</span>
          </span>
          <span class="font-mono text-muted-foreground uppercase">{{ instanceId }} table</span>
        </div>
        <div
          v-for="item in primaryItems"
          :key="item.id"
          @click="emit('select-result', item)"
          class="flex items-center justify-between p-2.5 rounded-lg hover:bg-muted/60 cursor-pointer transition-colors group"
        >
          <div class="flex items-center gap-3">
            <div class="p-2 rounded-lg bg-muted border border-border text-muted-foreground group-hover:text-primary group-hover:border-primary/40 transition-colors">
              <Search class="w-4 h-4" />
            </div>
            <div>
              <div class="text-xs font-bold text-foreground group-hover:text-primary flex items-center gap-2">
                <span>{{ item.name }}</span>
                <span v-if="item.status" class="w-1.5 h-1.5 rounded-full" :class="item.status === 'online' ? 'bg-emerald-600' : 'bg-muted-foreground'"></span>
              </div>
              <div class="text-[10px] text-muted-foreground flex items-center gap-2 mt-0.5">
                <span class="uppercase tracking-wider font-bold text-foreground/80">{{ item.typeLabel || item.itemType }}</span>
                <span v-if="item.manufacturerName">• {{ item.manufacturerName }}</span>
                <span v-if="item.subtitle" class="font-mono opacity-80">({{ item.subtitle }})</span>
              </div>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-[8px] font-mono uppercase px-2 py-0.5 rounded bg-muted text-muted-foreground border border-border">
              {{ item.sourceTable || instanceId }}
            </span>
            <ArrowRight class="w-4 h-4 text-muted-foreground group-hover:text-foreground opacity-0 group-hover:opacity-100 transition-all" />
          </div>
        </div>
      </div>

      <!-- Stage 2: Filter Key Suggestions (Index table key names matching text) -->
      <div v-if="!activePendingKey && matchingKeys && matchingKeys.length > 0" class="p-3 bg-muted/30">
        <div class="flex items-center justify-between text-[8px] font-black uppercase tracking-widest text-muted-foreground mb-2">
          <div class="flex items-center gap-1.5">
            <Tag class="w-3 h-3 text-muted-foreground" />
            <span>Filter Key Index (Click key or browse values)</span>
          </div>
          <button
            type="button"
            @click="activeView = 'tags'"
            class="text-[9px] text-primary hover:underline font-bold uppercase tracking-wider cursor-pointer"
          >
            Browse All Values →
          </button>
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="k in matchingKeys"
            :key="k.key"
            type="button"
            @click="emit('select-key', k.key)"
            class="flex items-center gap-1.5 px-3 py-1.5 bg-card hover:bg-accent text-foreground border border-border hover:border-primary/50 rounded-full text-[9px] font-mono font-bold uppercase tracking-wider transition-colors shadow-2xs group cursor-pointer"
          >
            <span>{{ k.key }}:</span>
            <span v-if="k.description" class="text-[8px] text-muted-foreground group-hover:text-primary font-sans normal-case tracking-normal">
              {{ k.description }}
            </span>
            <span v-if="allKnownKeyValues[k.key]?.values.length" class="text-[8px] font-mono text-muted-foreground bg-muted px-1.5 py-0.2 rounded-full">
              {{ allKnownKeyValues[k.key]?.values.length }}
            </span>
          </button>
        </div>
      </div>

      <!-- Stage 3: Cross-Table Matches -->
      <div v-if="crossTableItems.length > 0" class="p-2 max-h-48 overflow-y-auto custom-scrollbar bg-muted/20 border-t border-border/50">
        <div class="flex items-center justify-between text-[8px] font-black uppercase tracking-widest text-amber-600 dark:text-amber-400 px-3 py-1.5">
          <span class="flex items-center gap-1.5">
            <Network class="w-3 h-3 text-amber-600 dark:text-amber-400" />
            <span>Stage 3: Cross-Table Associations ({{ crossTableItems.length }})</span>
          </span>
          <span class="font-mono text-amber-600/70 dark:text-amber-400/70 uppercase">Indexed Links</span>
        </div>
        <div
          v-for="item in crossTableItems"
          :key="item.id"
          @click="emit('select-result', item)"
          class="flex items-center justify-between p-2 rounded-lg hover:bg-muted/60 cursor-pointer transition-colors group"
        >
          <div class="flex items-center gap-2.5">
            <div class="p-1.5 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-600 dark:text-amber-400">
              <Layers class="w-3.5 h-3.5" />
            </div>
            <div>
              <div class="text-xs font-bold text-foreground group-hover:text-amber-600 dark:group-hover:text-amber-400">
                {{ item.name }}
              </div>
              <div class="text-[9px] text-muted-foreground flex items-center gap-1.5">
                <span class="uppercase tracking-wider font-semibold">{{ item.typeLabel || item.itemType }}</span>
                <span v-if="item.subtitle" class="font-mono">({{ item.subtitle }})</span>
              </div>
            </div>
          </div>
          <div class="flex items-center gap-1.5">
            <span class="text-[8px] font-mono uppercase px-2 py-0.5 rounded bg-amber-500/10 text-amber-700 dark:text-amber-300 border border-amber-500/30">
              Cross: {{ item.sourceTable }}
            </span>
            <ArrowRight class="w-3.5 h-3.5 text-muted-foreground group-hover:text-amber-600 dark:group-hover:text-amber-400 opacity-0 group-hover:opacity-100 transition-all" />
          </div>
        </div>
      </div>

      <!-- Quick Browse All Tags Action Bar (when no results or at bottom) -->
      <div class="p-2.5 bg-muted/10 flex items-center justify-between text-xs text-muted-foreground">
        <div class="flex items-center gap-1.5">
          <BookOpen class="w-3.5 h-3.5 text-muted-foreground" />
          <span class="text-[10px]">Looking for specific attributes?</span>
        </div>
        <button
          type="button"
          @click="activeView = 'tags'"
          class="text-[10px] font-bold text-primary hover:underline uppercase tracking-wider flex items-center gap-1 cursor-pointer"
        >
          <span>Explore all {{ availableTagKeys.length }} tags & stored values</span>
          <ChevronRight class="w-3 h-3" />
        </button>
      </div>

      <!-- Loading Indicator -->
      <div v-if="isLoading" class="p-3 text-center text-xs text-muted-foreground font-bold uppercase tracking-widest">
        Scanning asset and topology databases...
      </div>
    </div>
  </div>
</template>
