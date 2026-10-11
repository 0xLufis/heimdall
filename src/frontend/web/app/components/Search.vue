<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { cn } from '~/lib/utils'
import { useGlobalSearchModal } from '~/composables/useGlobalSearchModal'
import { useShortcuts } from '~/composables/useShortcuts'
import { defineShortcuts } from '~/composables/defineShortcuts'
import { useAppI18n } from '~/composables/useAppI18n'

/**
 * Confluence-style Quick Search Launcher.
 * Renders an accessible, sleek quick-search input button in the sidebar header
 * that triggers the full HeimdallSearch modal upon click or keyboard shortcut.
 */
const props = withDefaults(
  defineProps<{
    placeholder?: string
    immediate?: boolean
    class?: HTMLAttributes['class']
  }>(),
  {
    placeholder: undefined
  }
)

const emit = defineEmits<{
  search: [query: string]
}>()

const { t } = useAppI18n()
const { metaSymbol } = useShortcuts()
const { triggerSearch } = useGlobalSearchModal()

const displayPlaceholder = computed(() => props.placeholder || t('common.searchOrJump', 'Search or jump to...'))

defineShortcuts({
  Meta_K: () => triggerSearch(),
  Meta_P: () => triggerSearch()
})
</script>

<template>
  <div :class="cn('w-full px-2 py-1', props.class)">
    <button
      type="button"
      class="w-full flex items-center justify-between px-3 py-2 text-xs text-muted-foreground bg-muted/40 hover:bg-muted/70 hover:text-foreground border border-border/60 rounded-xl transition-all duration-150 cursor-pointer group shadow-sm focus:outline-none focus:ring-2 focus:ring-ring/40"
      :title="t('common.openGlobalSearch', 'Open Global Search') + ' (⌘K)'"
      :aria-label="t('common.openGlobalSearch', 'Open Global Search')"
      @click="triggerSearch"
    >
      <div class="flex items-center gap-2.5 overflow-hidden">
        <Icon
          name="i-lucide-search"
          class="size-3.5 text-muted-foreground group-hover:text-foreground transition-colors shrink-0"
        />
        <span class="truncate text-xs font-normal">
          {{ displayPlaceholder }}
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
