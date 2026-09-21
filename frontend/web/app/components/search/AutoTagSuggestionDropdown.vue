<script setup lang="ts">
import { computed } from 'vue'
import type { AutoTagResult, SearchResultItem, SearchGroup, KeyLookupSuggestion } from '~/types/search'
import { Sparkles, Search, Tag, ArrowRight, CornerDownLeft, Database, Layers, Network } from 'lucide-vue-next'

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
}>()

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
</script>

<template>
  <div data-omni-dropdown="true" class="w-full bg-card/98 backdrop-blur-xl border border-border rounded-xl shadow-2xl overflow-hidden divide-y divide-border/60 z-50 text-card-foreground">
    <!-- Value Lookup Dropdown (Triggered when typing `key:`, e.g. tech: or status:) -->
    <div v-if="activePendingKey && valueSuggestions.length > 0" class="p-3 bg-muted/40 border-b border-border/60">
      <div class="flex items-center justify-between text-[9px] font-black uppercase tracking-widest text-primary mb-2">
        <div class="flex items-center gap-1.5">
          <CornerDownLeft class="w-3.5 h-3.5 text-primary shrink-0" />
          <span>Select Value for <span class="font-mono text-foreground bg-muted px-1.5 py-0.5 rounded">{{ activePendingKey }}:</span></span>
        </div>
        <span class="text-[8px] font-mono text-muted-foreground">Language Server Autocomplete</span>
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
    <div v-if="autoSuggestions.length > 0" class="p-3 bg-emerald-950/20">
      <div class="flex items-center gap-1.5 text-[8px] font-black uppercase tracking-widest text-emerald-400 mb-2">
        <Sparkles class="w-3 h-3 text-emerald-400 shrink-0" />
        <span>Auto-Detected Filter Pills</span>
      </div>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="s in autoSuggestions"
          :key="`${s.tag.key}-${s.tag.value}`"
          type="button"
          @click="emit('select-tag', s)"
          class="flex items-center gap-2 px-3.5 py-1.5 bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-200 border border-emerald-500/30 rounded-full text-[9px] font-black uppercase tracking-wider transition-all shadow-sm"
        >
          <span class="opacity-75 font-semibold">{{ s.tag.key }}:</span>
          <span class="font-bold">{{ s.tag.value }}</span>
          <span class="text-[8px] px-2 py-0.5 bg-emerald-500/30 rounded-full text-emerald-300 font-mono font-bold leading-none">
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
      <div class="text-[8px] font-black uppercase tracking-widest text-muted-foreground mb-2 flex items-center gap-1.5">
        <Tag class="w-3 h-3 text-muted-foreground" />
        <span>Stage 2: Filter Key Index (Type or Click to Complete)</span>
      </div>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="k in matchingKeys"
          :key="k.key"
          type="button"
          @click="emit('select-key', k.key)"
          class="flex items-center gap-1.5 px-3 py-1.5 bg-card hover:bg-accent text-foreground border border-border hover:border-primary/50 rounded-full text-[9px] font-mono font-bold uppercase tracking-wider transition-colors shadow-xs group cursor-pointer"
        >
          <span>{{ k.key }}:</span>
          <span v-if="k.description" class="text-[8px] text-muted-foreground group-hover:text-primary font-sans normal-case tracking-normal">
            {{ k.description }}
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

    <!-- Loading Indicator -->
    <div v-if="isLoading" class="p-3 text-center text-xs text-slate-500 font-bold uppercase tracking-widest">
      Scanning asset and topology databases...
    </div>
  </div>
</template>

