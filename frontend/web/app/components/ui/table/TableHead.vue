<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { ref, computed, watch } from 'vue'
import { ArrowUp, ArrowDown, ArrowUpDown } from 'lucide-vue-next'
import { cn } from '~/utils/cn'

const props = withDefaults(
  defineProps<{
    class?: HTMLAttributes['class']
    sortable?: boolean
    sortDirection?: 'asc' | 'desc' | null
  }>(),
  {
    sortable: false,
    sortDirection: undefined
  }
)

const emit = defineEmits<{
  (e: 'update:sortDirection', value: 'asc' | 'desc' | null): void
  (e: 'sort', value: 'asc' | 'desc' | null): void
  (e: 'click', event: MouseEvent): void
}>()

const internalSortDirection = ref<'asc' | 'desc' | null>(props.sortDirection ?? null)

watch(
  () => props.sortDirection,
  (newVal) => {
    if (newVal !== undefined) {
      internalSortDirection.value = newVal
    }
  }
)

const isSortable = computed(() => Boolean(props.sortable || props.sortDirection !== undefined))

const currentDirection = computed(() => {
  return props.sortDirection !== undefined ? props.sortDirection : internalSortDirection.value
})

function handleClick(event: MouseEvent) {
  emit('click', event)
  if (!isSortable.value) return

  // 3-state cycle: null -> 'asc' -> 'desc' -> null (undo sorting on 3rd click)
  let nextDirection: 'asc' | 'desc' | null = 'asc'
  if (currentDirection.value === 'asc') {
    nextDirection = 'desc'
  } else if (currentDirection.value === 'desc') {
    nextDirection = null
  } else {
    nextDirection = 'asc'
  }

  internalSortDirection.value = nextDirection
  emit('update:sortDirection', nextDirection)
  emit('sort', nextDirection)
}
</script>

<template>
  <th
    :class="
      cn(
        'h-12 px-4 text-left align-middle font-medium text-muted-foreground [&:has([role=checkbox])]:pr-0',
        isSortable && 'cursor-pointer select-none hover:text-slate-100 transition-colors group',
        props.class,
      )
    "
    :aria-sort="
      isSortable
        ? currentDirection === 'asc'
          ? 'ascending'
          : currentDirection === 'desc'
            ? 'descending'
            : 'none'
        : undefined
    "
    @click="handleClick"
  >
    <div v-if="isSortable" class="inline-flex items-center gap-1.5">
      <slot />
      <span class="inline-flex items-center shrink-0">
        <ArrowUp v-if="currentDirection === 'asc'" class="size-3.5 text-purple-400 font-bold" />
        <ArrowDown v-else-if="currentDirection === 'desc'" class="size-3.5 text-purple-400 font-bold" />
        <ArrowUpDown v-else class="size-3 text-slate-500/60 opacity-0 group-hover:opacity-100 transition-opacity" />
      </span>
    </div>
    <slot v-else />
  </th>
</template>
