<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Dialog, DialogContent } from '~/components/ui/dialog'
import OmniSearchBar from './OmniSearchBar.vue'
import type { SearchInstanceConfig } from '~/types/search'

import { useRouter } from 'vue-router'

const router = useRouter()
const open = ref(false)

const globalConfig: SearchInstanceConfig = {
  instanceId: 'global',
  placeholder: 'FMFD: Type keyword, manufacturer, IP, or Station ID...',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  enableAutoTagging: true,
  showGlobalShortcut: true
}

const handleSearch = (q: string) => {
  open.value = false
  if (q && q.trim()) {
    router.push(`/dashboard/inventory?query=${encodeURIComponent(q.trim())}`)
  }
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
    open.value = !open.value
  } else if (e.key === '/' && !isInputTarget && !e.ctrlKey && !e.metaKey && !e.altKey && !open.value) {
    e.preventDefault()
    open.value = true
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
  <Dialog :open="open" @update:open="open = $event">
    <DialogContent class="max-w-2xl bg-slate-950/95 backdrop-blur-xl border-slate-800 text-slate-100 p-6 rounded-3xl shadow-2xl">
      <div class="space-y-4">
        <div class="flex items-center justify-between pb-2 border-b border-slate-800/80">
          <div class="flex items-center gap-2">
            <span class="text-xs font-black uppercase tracking-[0.2em] text-indigo-400">Heimdall FMFD</span>
            <span class="text-[11px] text-slate-400 font-medium">— Find My Field Data</span>
          </div>
          <span class="text-[10px] font-mono text-slate-500">Press ESC to exit</span>
        </div>

        <OmniSearchBar
          :config="globalConfig"
          :immediate="true"
          @search="handleSearch"
          @select-result="open = false"
        />
      </div>
    </DialogContent>
  </Dialog>
</template>
