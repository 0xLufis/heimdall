<script setup lang="ts">
import { useGlobalSearchModal } from '~/composables/useGlobalSearchModal'
import { useShortcuts } from '~/composables/useShortcuts'
import { defineShortcuts } from '~/composables/defineShortcuts'

/**
 * Confluence-style Quick Search Launcher.
 * Renders an accessible, sleek quick-search input button in the sidebar header
 * that triggers the full OmniSearch modal upon click or keyboard shortcut.
 */
const props = withDefaults(
  defineProps<{
    placeholder?: string
    immediate?: boolean
  }>(),
  {
    placeholder: 'Search or jump to...'
  }
)

const emit = defineEmits<{
  search: [query: string]
}>()

const { metaSymbol } = useShortcuts()
const { triggerSearch } = useGlobalSearchModal()

defineShortcuts({
  Meta_K: () => triggerSearch(),
  Meta_P: () => triggerSearch()
})
</script>

<template>
  <div class="w-full px-2 py-1">
    <button
      type="button"
      class="w-full flex items-center justify-between px-3 py-2 text-xs text-muted-foreground bg-muted/40 hover:bg-muted/70 hover:text-foreground border border-border/60 rounded-xl transition-all duration-150 cursor-pointer group shadow-sm focus:outline-none focus:ring-2 focus:ring-ring/40"
      title="Open Global Search (⌘K)"
      aria-label="Open Global Search"
      @click="triggerSearch"
    >
      <div class="flex items-center gap-2.5 overflow-hidden">
        <Icon
          name="i-lucide-search"
          class="size-3.5 text-muted-foreground group-hover:text-foreground transition-colors shrink-0"
        />
        <span class="truncate text-xs font-normal">
          {{ placeholder }}
        </span>
      </div>

      <div class="flex items-center gap-0.5 shrink-0 ml-1.5">
        <kbd
          class="pointer-events-none inline-flex h-5 select-none items-center gap-0.5 rounded border border-border/80 bg-background/80 px-1.5 font-mono text-[10px] font-medium text-muted-foreground shadow-xs"
        >
          <ClientOnly>
            <span>{{ metaSymbol }}</span>
            <template #fallback>
              <span>Ctrl</span>
            </template>
          </ClientOnly>K
        </kbd>
      </div>
    </button>
  </div>
</template>
