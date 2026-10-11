<script setup lang="ts">
import { ref, computed } from 'vue'
import { Search, X, Tag } from 'lucide-vue-next'
import { Input } from '~/components/ui/input'
import { Button } from '~/components/ui/button'

const props = defineProps<{
  availableTags: string[]
  selectedTags: string[]
}>()

const emit = defineEmits<{
  (e: 'update:selectedTags', value: string[]): void
}>()

const searchQuery = ref('')

const filteredTags = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return props.availableTags
  return props.availableTags.filter(tag => tag.toLowerCase().includes(q))
})

function toggleTag(tag: string) {
  const current = new Set(props.selectedTags)
  if (current.has(tag)) {
    current.delete(tag)
  } else {
    current.add(tag)
  }
  emit('update:selectedTags', Array.from(current))
}

function clearAll() {
  emit('update:selectedTags', [])
}

function isSelected(tag: string): boolean {
  return props.selectedTags.includes(tag)
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <!-- Top Row: search + clear -->
    <div class="flex items-center gap-2">
      <div class="relative flex-1 max-w-[220px]">
        <Search class="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground pointer-events-none" />
        <Input
          v-model="searchQuery"
          placeholder="Filter tags…"
          class="pl-8 h-7 text-xs bg-background border-border text-foreground placeholder:text-muted-foreground focus:border-primary"
        />
      </div>

      <button
        v-if="selectedTags.length > 0"
        class="flex items-center gap-1 px-2 py-1 text-[10px] font-black uppercase tracking-wider text-rose-600 dark:text-rose-400 hover:text-rose-700 dark:hover:text-rose-300 hover:bg-rose-500/10 rounded-lg transition-colors"
        @click="clearAll"
      >
        <X class="w-3 h-3" />
        Clear all
        <span class="font-mono">({{ selectedTags.length }})</span>
      </button>
    </div>

    <!-- Tag Chips Row (horizontal scroll) -->
    <div
      v-if="filteredTags.length > 0"
      class="flex items-center gap-1.5 overflow-x-auto pb-1 scrollbar-thin scrollbar-thumb-muted-foreground/30 scrollbar-track-transparent"
    >
      <button
        v-for="tag in filteredTags"
        :key="tag"
        class="flex items-center gap-1 px-2.5 py-1 rounded-full border text-[10px] font-bold uppercase tracking-wide transition-all whitespace-nowrap shrink-0 select-none"
        :class="isSelected(tag)
          ? 'bg-indigo-500/15 border-indigo-500/60 text-indigo-700 dark:text-indigo-300 shadow-sm shadow-indigo-500/10'
          : 'bg-card border-border text-muted-foreground hover:border-primary/40 hover:text-foreground'"
        @click="toggleTag(tag)"
      >
        <Tag class="w-2.5 h-2.5 shrink-0" />
        {{ tag }}
        <span v-if="isSelected(tag)" class="ml-0.5 text-indigo-600/70 dark:text-indigo-400/70">✓</span>
      </button>
    </div>

    <div v-else class="text-[10px] text-muted-foreground italic py-1">
      No tags match "{{ searchQuery }}"
    </div>
  </div>
</template>
