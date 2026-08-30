<script setup lang="ts">
import type { TagPill } from '~/types/search'
import { X, Sparkles } from 'lucide-vue-next'

const props = defineProps<{
  tags: TagPill[]
}>()

const emit = defineEmits<{
  (e: 'remove', tagId: string): void
}>()

const getPillClass = (tag: TagPill) => {
  switch (tag.color || tag.key) {
    case 'status':
    case 'emerald':
      return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30'
    case 'manufacturer':
    case 'blue':
      return 'bg-blue-500/10 text-blue-400 border-blue-500/30'
    case 'category':
    case 'purple':
      return 'bg-purple-500/10 text-purple-400 border-purple-500/30'
    case 'spec':
    case 'amber':
      return 'bg-amber-500/10 text-amber-400 border-amber-500/30'
    case 'cost':
    case 'rose':
      return 'bg-rose-500/10 text-rose-400 border-rose-500/30'
    case 'station':
      return 'bg-cyan-500/10 text-cyan-400 border-cyan-500/30'
    default:
      return 'bg-indigo-500/10 text-indigo-400 border-indigo-500/30'
  }
}
</script>

<template>
  <div v-if="tags.length > 0" class="flex flex-wrap items-center gap-1.5 py-1">
    <div
      v-for="tag in tags"
      :key="tag.id"
      class="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg border text-xs font-bold tracking-tight transition-all group"
      :class="getPillClass(tag)"
    >
      <Sparkles v-if="tag.isAutoDetected" class="w-3 h-3 text-amber-400 shrink-0" />
      <span class="opacity-70 font-semibold">{{ tag.key }}:</span>
      <span class="font-bold">{{ tag.value }}</span>
      <button
        v-if="tag.removable !== false"
        type="button"
        @click.stop="emit('remove', tag.id)"
        class="opacity-60 group-hover:opacity-100 hover:text-white p-0.5 rounded transition-opacity"
        aria-label="Remove tag"
      >
        <X class="w-3 h-3" />
      </button>
    </div>
  </div>
</template>
