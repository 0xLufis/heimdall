<script setup lang="ts">
import type { AutoTagResult, SearchResultItem, SearchGroup } from '~/types/search'
import { Sparkles, Search, Tag, ArrowRight } from 'lucide-vue-next'

defineProps<{
  autoSuggestions: AutoTagResult[]
  results: SearchResultItem[]
  searchKeyGroups: SearchGroup[]
  isLoading: boolean
  freeText: string
}>()

const emit = defineEmits<{
  (e: 'select-tag', suggestion: AutoTagResult): void
  (e: 'select-result', item: SearchResultItem): void
  (e: 'select-key', key: string): void
}>()
</script>

<template>
  <div class="w-full bg-slate-950 border border-slate-800 rounded-2xl shadow-2xl overflow-hidden divide-y divide-slate-900">
    <!-- Auto-detected Tag Suggestions -->
    <div v-if="autoSuggestions.length > 0" class="p-3 bg-indigo-950/20">
      <div class="flex items-center gap-1.5 text-[10px] font-black uppercase tracking-widest text-indigo-400 mb-2">
        <Sparkles class="w-3.5 h-3.5 text-indigo-400" />
        <span>Auto-Detected Search Filters</span>
      </div>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="s in autoSuggestions"
          :key="`${s.tag.key}-${s.tag.value}`"
          type="button"
          @click="emit('select-tag', s)"
          class="flex items-center gap-1.5 px-3 py-1.5 bg-indigo-600/20 hover:bg-indigo-600/40 text-indigo-200 border border-indigo-500/30 rounded-xl text-xs font-bold transition-all"
        >
          <span class="opacity-75">{{ s.tag.key }}:</span>
          <span>{{ s.tag.value }}</span>
          <span class="text-[9px] px-1 py-0.2 bg-indigo-500/30 rounded text-indigo-300 font-mono">
            {{ Math.round(s.confidence * 100) }}% match
          </span>
        </button>
      </div>
    </div>

    <!-- Live Search Results -->
    <div v-if="results.length > 0" class="p-2 max-h-64 overflow-y-auto">
      <div class="text-[10px] font-black uppercase tracking-widest text-slate-500 px-3 py-1.5">
        Matching Entities ({{ results.length }})
      </div>
      <div
        v-for="item in results"
        :key="item.id"
        @click="emit('select-result', item)"
        class="flex items-center justify-between p-3 rounded-xl hover:bg-slate-900 cursor-pointer transition-colors group"
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
              <span class="uppercase tracking-wider">{{ item.typeLabel || item.itemType }}</span>
              <span v-if="item.manufacturerName">• {{ item.manufacturerName }}</span>
              <span v-if="item.subtitle" class="font-mono opacity-80">({{ item.subtitle }})</span>
            </div>
          </div>
        </div>
        <ArrowRight class="w-4 h-4 text-slate-600 group-hover:text-slate-300 opacity-0 group-hover:opacity-100 transition-all" />
      </div>
    </div>

    <!-- Available Search Attributes -->
    <div v-if="searchKeyGroups.length > 0 && results.length === 0" class="p-3 bg-slate-950">
      <div class="text-[10px] font-black uppercase tracking-widest text-slate-500 mb-2 flex items-center gap-1">
        <Tag class="w-3 h-3" />
        <span>Search Keys (Type key:value)</span>
      </div>
      <div class="flex flex-wrap gap-1.5">
        <template v-for="group in searchKeyGroups" :key="group.group">
          <button
            v-for="key in group.keys"
            :key="key"
            type="button"
            @click="emit('select-key', key)"
            class="px-2.5 py-1 bg-slate-900 hover:bg-slate-800 text-slate-400 hover:text-slate-200 border border-slate-800 rounded-lg text-xs font-mono transition-colors"
          >
            {{ key }}:
          </button>
        </template>
      </div>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoading" class="p-3 text-center text-xs text-slate-500 font-bold uppercase tracking-widest">
      Scanning asset database...
    </div>
  </div>
</template>
