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
  <div class="w-full bg-slate-950/98 backdrop-blur-xl border border-slate-800/90 rounded-2xl shadow-2xl overflow-hidden divide-y divide-slate-900 z-50 text-slate-100">
    <!-- Value Lookup Dropdown (Triggered when typing `key:`, e.g. tech: or status:) -->
    <div v-if="activePendingKey && valueSuggestions.length > 0" class="p-3 bg-indigo-950/30 border-b border-indigo-900/30">
      <div class="flex items-center justify-between text-[9px] font-black uppercase tracking-widest text-indigo-400 mb-2">
        <div class="flex items-center gap-1.5">
          <CornerDownLeft class="w-3.5 h-3.5 text-indigo-400 shrink-0" />
          <span>Select Value for <span class="font-mono text-white bg-indigo-900/50 px-1.5 py-0.5 rounded">{{ activePendingKey }}:</span></span>
        </div>
        <span class="text-[8px] font-mono text-slate-500">Language Server Autocomplete</span>
      </div>
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-1.5 max-h-48 overflow-y-auto custom-scrollbar">
        <button
          v-for="val in valueSuggestions"
          :key="val.value"
          type="button"
          @click="emit('select-value', val.value)"
          class="flex items-center justify-between p-2 rounded-xl bg-slate-900/90 hover:bg-indigo-600/30 border border-slate-800 hover:border-indigo-500/50 text-left transition-all group"
        >
          <div class="flex items-center gap-2">
            <span class="font-mono text-xs font-bold text-indigo-300 group-hover:text-white">{{ val.value }}</span>
            <span v-if="val.description" class="text-[9px] text-slate-400 truncate max-w-[140px]">{{ val.description }}</span>
          </div>
          <CornerDownLeft class="w-3 h-3 text-slate-600 group-hover:text-indigo-300 opacity-0 group-hover:opacity-100 transition-opacity shrink-0" />
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
      <div class="flex items-center justify-between text-[8px] font-black uppercase tracking-widest text-slate-500 px-3 py-1.5">
        <span class="flex items-center gap-1.5">
          <Database class="w-3 h-3 text-indigo-400" />
          <span>Stage 1: Primary Index Matches ({{ primaryItems.length }})</span>
        </span>
        <span class="font-mono text-slate-600 uppercase">{{ instanceId }} table</span>
      </div>
      <div
        v-for="item in primaryItems"
        :key="item.id"
        @click="emit('select-result', item)"
        class="flex items-center justify-between p-2.5 rounded-xl hover:bg-slate-900 cursor-pointer transition-colors group"
      >
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-slate-900 border border-slate-800 text-slate-400 group-hover:text-indigo-400 group-hover:border-indigo-500/30 transition-colors">
            <Search class="w-4 h-4" />
          </div>
          <div>
            <div class="text-xs font-bold text-slate-200 group-hover:text-white flex items-center gap-2">
              <span>{{ item.name }}</span>
              <span v-if="item.status" class="w-1.5 h-1.5 rounded-full" :class="item.status === 'online' ? 'bg-emerald-500' : 'bg-slate-600'"></span>
            </div>
            <div class="text-[10px] text-slate-500 flex items-center gap-2 mt-0.5">
              <span class="uppercase tracking-wider font-bold text-slate-400">{{ item.typeLabel || item.itemType }}</span>
              <span v-if="item.manufacturerName">• {{ item.manufacturerName }}</span>
              <span v-if="item.subtitle" class="font-mono opacity-80">({{ item.subtitle }})</span>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <span class="text-[8px] font-mono uppercase px-2 py-0.5 rounded bg-slate-800 text-slate-400 border border-slate-700">
            {{ item.sourceTable || instanceId }}
          </span>
          <ArrowRight class="w-4 h-4 text-slate-600 group-hover:text-slate-300 opacity-0 group-hover:opacity-100 transition-all" />
        </div>
      </div>
    </div>

    <!-- Stage 2: Filter Key Suggestions (Index table key names matching text) -->
    <div v-if="!activePendingKey && matchingKeys && matchingKeys.length > 0" class="p-3 bg-slate-950">
      <div class="text-[8px] font-black uppercase tracking-widest text-slate-500 mb-2 flex items-center gap-1.5">
        <Tag class="w-3 h-3 text-slate-400" />
        <span>Stage 2: Filter Key Index (Type or Click to Complete)</span>
      </div>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="k in matchingKeys"
          :key="k.key"
          type="button"
          @click="emit('select-key', k.key)"
          class="flex items-center gap-1.5 px-3 py-1.5 bg-slate-900 hover:bg-indigo-950/60 text-slate-300 hover:text-indigo-300 border border-slate-800 hover:border-indigo-500/40 rounded-full text-[9px] font-mono font-bold uppercase tracking-wider transition-colors shadow-sm group"
        >
          <span>{{ k.key }}:</span>
          <span v-if="k.description" class="text-[8px] text-slate-500 group-hover:text-indigo-400 font-sans normal-case tracking-normal">
            {{ k.description }}
          </span>
        </button>
      </div>
    </div>

    <!-- Stage 3: Cross-Table Matches -->
    <div v-if="crossTableItems.length > 0" class="p-2 max-h-48 overflow-y-auto custom-scrollbar bg-slate-950/60 border-t border-slate-900">
      <div class="flex items-center justify-between text-[8px] font-black uppercase tracking-widest text-amber-500/80 px-3 py-1.5">
        <span class="flex items-center gap-1.5">
          <Network class="w-3 h-3 text-amber-400" />
          <span>Stage 3: Cross-Table Associations ({{ crossTableItems.length }})</span>
        </span>
        <span class="font-mono text-amber-500/60 uppercase">Indexed Links</span>
      </div>
      <div
        v-for="item in crossTableItems"
        :key="item.id"
        @click="emit('select-result', item)"
        class="flex items-center justify-between p-2 rounded-xl hover:bg-slate-900 cursor-pointer transition-colors group"
      >
        <div class="flex items-center gap-2.5">
          <div class="p-1.5 rounded-lg bg-amber-950/30 border border-amber-800/30 text-amber-400">
            <Layers class="w-3.5 h-3.5" />
          </div>
          <div>
            <div class="text-xs font-bold text-slate-300 group-hover:text-amber-200">
              {{ item.name }}
            </div>
            <div class="text-[9px] text-slate-500 flex items-center gap-1.5">
              <span class="uppercase tracking-wider font-semibold">{{ item.typeLabel || item.itemType }}</span>
              <span v-if="item.subtitle" class="font-mono">({{ item.subtitle }})</span>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-1.5">
          <span class="text-[8px] font-mono uppercase px-2 py-0.5 rounded bg-amber-950/50 text-amber-300 border border-amber-800/40">
            Cross: {{ item.sourceTable }}
          </span>
          <ArrowRight class="w-3.5 h-3.5 text-slate-600 group-hover:text-amber-300 opacity-0 group-hover:opacity-100 transition-all" />
        </div>
      </div>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoading" class="p-3 text-center text-xs text-slate-500 font-bold uppercase tracking-widest">
      Scanning asset and topology databases...
    </div>
  </div>
</template>

