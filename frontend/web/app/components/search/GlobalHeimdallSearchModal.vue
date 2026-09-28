<script setup lang="ts">
import { onMounted, onUnmounted, ref, nextTick, watch } from 'vue'
import { Dialog, DialogContent } from '~/components/ui/dialog'
import HeimdallSearchBar from './HeimdallSearchBar.vue'
import type { SearchInstanceConfig, SearchResultItem } from '~/types/search'
import { useRouter } from 'vue-router'
import { useGlobalSearchModal } from '~/composables/useGlobalSearchModal'
import { X } from 'lucide-vue-next'

const router = useRouter()
const { isOpen, closeModal, triggerSearch, openModal, focusTriggerSignal } = useGlobalSearchModal()

const omniBarRef = ref<any>(null)

const globalConfig: SearchInstanceConfig = {
  instanceId: 'global',
  placeholder: 'Search stations, controllers, inventory, or assets...',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  enableAutoTagging: true,
  showGlobalShortcut: true
}

// Navigate and close on explicit search submission with intelligent intent routing
const handleSubmit = (q: string) => {
  closeModal(true)
  if (!q || !q.trim()) return

  const cleanQ = q.trim()
  const lowerQ = cleanQ.toLowerCase()

  // 1. Station / Machine / Line / Tech intent
  if (
    lowerQ.includes('station:') ||
    lowerQ.includes('line:') ||
    lowerQ.includes('tech:') ||
    lowerQ.includes('machine:') ||
    lowerQ.startsWith('op') ||
    lowerQ.startsWith('cell') ||
    lowerQ.includes('line 1') ||
    lowerQ.includes('line 2') ||
    lowerQ.includes('assembly') ||
    lowerQ.includes('welding') ||
    lowerQ.includes('robot')
  ) {
    router.push(`/dashboard/machines?search=${encodeURIComponent(cleanQ)}`)
    return
  }

  // 2. Client PC / IPC / Controller / Node intent
  if (
    lowerQ.includes('host:') ||
    lowerQ.includes('ip:') ||
    lowerQ.includes('mac:') ||
    lowerQ.includes('client:') ||
    lowerQ.includes('node:') ||
    lowerQ.startsWith('ipc-')
  ) {
    router.push(`/dashboard/clients?search=${encodeURIComponent(cleanQ)}`)
    return
  }

  // 3. Maintenance Ticket / Incident intent
  if (
    lowerQ.includes('ticket:') ||
    lowerQ.includes('code:') ||
    lowerQ.includes('#sfc') ||
    lowerQ.startsWith('#') ||
    lowerQ.startsWith('e-') ||
    lowerQ.startsWith('p-') ||
    lowerQ.startsWith('i-') ||
    lowerQ.startsWith('etc-')
  ) {
    router.push(`/dashboard/tickets?stationId=${encodeURIComponent(cleanQ)}`)
    return
  }

  // 4. Preserve existing page search context if on machines or clients
  if (router.currentRoute.value.path === '/dashboard/machines') {
    router.push(`/dashboard/machines?search=${encodeURIComponent(cleanQ)}`)
    return
  } else if (router.currentRoute.value.path === '/dashboard/clients') {
    router.push(`/dashboard/clients?search=${encodeURIComponent(cleanQ)}`)
    return
  }

  // 5. Default fallback to Inventory
  router.push(`/dashboard/inventory?query=${encodeURIComponent(cleanQ)}`)
}

// On selecting a concrete item result, close the modal (navigation is handled by HeimdallSearchBar)
const handleSelectResult = (_item: SearchResultItem) => {
  closeModal(true)
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
    e.stopPropagation()
    triggerSearch()
    return
  } else if (e.key === 'Escape' && isOpen.value) {
    e.preventDefault()
    e.stopPropagation()
    closeModal(true)
    return
  } else if (e.key === '/' && !isInputTarget && !e.ctrlKey && !e.metaKey && !e.altKey && !isOpen.value) {
    e.preventDefault()
    e.stopPropagation()
    openModal()
    return
  }
}

// Watch modal state and trigger signal to refocus input smoothly
watch([isOpen, focusTriggerSignal], async ([open]) => {
  if (open) {
    await nextTick()
    // Give modal render a brief moment to paint
    setTimeout(() => {
      const el = document.querySelector('[data-slot="dialog-content"] input') as HTMLInputElement | null
      if (el) {
        el.focus()
        el.select()
      }
    }, 50)
  }
})

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
      :show-close="false"
      class="max-w-2xl bg-card/95 backdrop-blur-xl border-border text-foreground p-6 rounded-2xl shadow-2xl"
      @pointer-down-outside="(e) => {
        const rawTarget = (e as any).detail?.originalEvent?.target || (e as any).target
        const target = rawTarget as HTMLElement | null
        // Prevent close if target is detached (during DOM re-paint) or clicking dropdown
        if (!target || !document.body.contains(target) || target.closest('[data-omni-dropdown]')) {
          e.preventDefault()
        }
      }"
      @interact-outside="(e) => {
        const rawTarget = (e as any).detail?.originalEvent?.target || (e as any).target
        const target = rawTarget as HTMLElement | null
        if (!target || !document.body.contains(target) || target.closest('[data-omni-dropdown]')) {
          e.preventDefault()
        }
      }"
      @focus-outside="(e) => {
        // Prevent background telemetry / page re-paints from stealing focus and closing search
        e.preventDefault()
      }"
    >
      <div class="space-y-4">
        <!-- Header -->
        <div class="flex items-center justify-between pb-2 border-b border-border/80">
          <div class="flex items-center gap-2">
            <span class="text-xs font-black uppercase tracking-[0.2em] text-primary">Heimdall Search</span>
            <span class="text-[11px] text-muted-foreground font-medium hidden sm:inline">— Find My Field Data</span>
          </div>

          <div class="flex items-center gap-3">
            <span class="text-[10px] font-mono text-muted-foreground select-none">Press ESC to exit</span>
            <button
              type="button"
              @click="closeModal(true)"
              class="p-1 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/80 transition-colors cursor-pointer"
              title="Close search modal (ESC)"
            >
              <X class="w-4 h-4" />
            </button>
          </div>
        </div>

        <HeimdallSearchBar
          ref="omniBarRef"
          :config="globalConfig"
          :immediate="false"
          @submit="handleSubmit"
          @select-result="handleSelectResult"
        />
      </div>
    </DialogContent>
  </Dialog>
</template>
