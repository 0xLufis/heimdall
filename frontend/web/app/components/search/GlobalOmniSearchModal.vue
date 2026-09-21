<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { Dialog, DialogContent } from '~/components/ui/dialog'
import OmniSearchBar from './OmniSearchBar.vue'
import type { SearchInstanceConfig, SearchResultItem } from '~/types/search'
import { useRouter } from 'vue-router'
import { useGlobalSearchModal } from '~/composables/useGlobalSearchModal'

const router = useRouter()
const { isOpen, closeModal, toggleModal, openModal } = useGlobalSearchModal()

const globalConfig: SearchInstanceConfig = {
  instanceId: 'global',
  placeholder: 'FMFD: Type keyword, manufacturer, IP, or Station ID...',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  enableAutoTagging: true,
  showGlobalShortcut: true
}

// Only navigate and close on explicit search submission (e.g. Enter pressed or search button clicked)
const handleSubmit = (q: string) => {
  closeModal()
  if (q && q.trim()) {
    router.push(`/dashboard/inventory?query=${encodeURIComponent(q.trim())}`)
  }
}

// On selecting a concrete item result, close the modal (navigation is handled by OmniSearchBar)
const handleSelectResult = (_item: SearchResultItem) => {
  closeModal()
}

const handleKeydown = (e: KeyboardEvent) => {
  const target = e.target as HTMLElement | null
  const isInputTarget = target && (
    target.tagName === 'INPUT' || 
    target.tagName === 'TEXTAREA' || 
    target.isContentEditable
  )

  if ((e.metaKey || e.ctrlKey) && (e.key.toLowerCase() === 'k' || e.key.toLowerCase() === 'p')) {
    e.preventDefault()
    toggleModal()
  } else if (e.key === '/' && !isInputTarget && !e.ctrlKey && !e.metaKey && !e.altKey && !isOpen.value) {
    e.preventDefault()
    openModal()
  }
}

onMounted(() => {
  if (typeof window !== 'undefined') {
    window.addEventListener('keydown', handleKeydown)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('keydown', handleKeydown)
  }
})
</script>

<template>
  <Dialog :open="isOpen" @update:open="isOpen = $event">
    <DialogContent
      class="max-w-2xl bg-card/95 backdrop-blur-xl border-border text-foreground p-6 rounded-2xl shadow-2xl"
      @pointer-down-outside="(e) => {
        const target = e.target as HTMLElement | null
        if (target && target.closest('[data-omni-dropdown]')) {
          e.preventDefault()
        }
      }"
    >
      <div class="space-y-4">
        <div class="flex items-center justify-between pb-2 border-b border-border/80">
          <div class="flex items-center gap-2">
            <span class="text-xs font-black uppercase tracking-[0.2em] text-primary">Heimdall FMFD</span>
            <span class="text-[11px] text-muted-foreground font-medium">— Find My Field Data</span>
          </div>
          <span class="text-[10px] font-mono text-muted-foreground">Press ESC to exit</span>
        </div>

        <OmniSearchBar
          :config="globalConfig"
          :immediate="false"
          @submit="handleSubmit"
          @select-result="handleSelectResult"
        />
      </div>
    </DialogContent>
  </Dialog>
</template>
