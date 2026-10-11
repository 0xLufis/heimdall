<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Activity,
  Cpu,
  Wrench,
  Boxes,
  QrCode,
  X
} from 'lucide-vue-next'
import { QrScanner } from '~/components/ui/qr-scanner'

const route = useRoute()
const router = useRouter()

const isScannerOpen = ref(false)
const activeLineStopsCount = ref(0)

const navItems = [
  { label: 'Overview', to: '/dashboard', icon: Activity },
  { label: 'Machinery', to: '/dashboard/machines', icon: Cpu },
  { label: 'Incidents', to: '/dashboard/tickets', icon: Wrench, hasBadge: true },
  { label: 'Inventory', to: '/dashboard/inventory', icon: Boxes },
]

function isActive(path: string): boolean {
  const currentPath = route?.path || ''
  if (path === '/dashboard') {
    return currentPath === '/dashboard' || currentPath === '/dashboard/' || currentPath === '/'
  }
  return currentPath.startsWith(path)
}

function handleQrScanned(result: string) {
  isScannerOpen.value = false
  if (!result) return
  // If result is a URL, navigate or parse ticket/machine/part ID
  if (result.startsWith('http://') || result.startsWith('https://')) {
    try {
      const url = new URL(result)
      router.push(url.pathname)
      return
    } catch {}
  }
  if (result.startsWith('ticket:') || result.startsWith('TICK-') || result.startsWith('INC-')) {
    const id = result.replace(/^ticket:/, '')
    router.push(`/dashboard/tickets?ticketId=${encodeURIComponent(id)}`)
  } else if (result.startsWith('machine:') || result.startsWith('MC-') || result.startsWith('OP')) {
    const id = result.replace(/^machine:/, '')
    router.push(`/dashboard/machines/${encodeURIComponent(id)}`)
  } else if (result.startsWith('part:') || result.startsWith('P-') || result.startsWith('SKU-')) {
    const id = result.replace(/^part:/, '')
    router.push(`/dashboard/inventory/${encodeURIComponent(id)}`)
  } else {
    router.push(`/dashboard/search?q=${encodeURIComponent(result)}`)
  }
}

onMounted(async () => {
  try {
    const res = await $fetch<any[]>('/api/tickets').catch(() => [])
    if (Array.isArray(res)) {
      activeLineStopsCount.value = res.filter(
        t => t.isLineStop === true && t.status !== 'Closed' && t.status !== 'Resolved'
      ).length
    }
  } catch {}
})
</script>

<template>
  <div class="md:hidden">
    <!-- Bottom Navigation Bar -->
    <nav class="fixed bottom-0 inset-x-0 z-40 bg-[#12151b]/95 backdrop-blur-md border-t border-zinc-800 pb-[env(safe-area-inset-bottom)] px-2">
      <div class="flex items-center justify-around h-14 min-h-[56px]">
        <NuxtLink
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          :class="[
            'flex flex-col items-center justify-center flex-1 h-full py-1 text-[10px] font-medium transition-colors relative min-h-[44px]',
            isActive(item.to)
              ? 'text-teal-400 font-semibold'
              : 'text-zinc-400 hover:text-zinc-200'
          ]"
        >
          <div class="relative">
            <component :is="item.icon" class="w-4 h-4 mb-0.5" />
            <span
              v-if="item.hasBadge && activeLineStopsCount > 0"
              class="absolute -top-1 -right-2 px-1 py-0 bg-rose-500 text-white text-[9px] font-bold rounded-full animate-pulse"
            >
              {{ activeLineStopsCount }}
            </span>
          </div>
          <span>{{ item.label }}</span>
        </NuxtLink>

        <!-- QR Quick Scanner Button -->
        <button
          type="button"
          class="flex flex-col items-center justify-center flex-1 h-full py-1 text-[10px] font-medium text-zinc-400 hover:text-teal-300 transition-colors cursor-pointer min-h-[44px]"
          @click="isScannerOpen = true"
        >
          <div class="p-1 rounded-full bg-teal-500/10 text-teal-400 border border-teal-500/30 mb-0.5">
            <QrCode class="w-3.5 h-3.5" />
          </div>
          <span>Scan</span>
        </button>
      </div>
    </nav>

    <!-- Fullscreen QR Scanner Modal -->
    <div
      v-if="isScannerOpen"
      class="fixed inset-0 z-50 bg-black/90 backdrop-blur-lg flex flex-col items-center justify-center p-4"
    >
      <div class="w-full max-w-sm flex items-center justify-between pb-3 text-zinc-100">
        <div class="flex items-center gap-2">
          <QrCode class="w-4 h-4 text-teal-400" />
          <span class="text-sm font-semibold tracking-wide uppercase font-mono">Camera Barcode Scanner</span>
        </div>
        <button
          type="button"
          class="p-1.5 rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          @click="isScannerOpen = false"
        >
          <X class="w-5 h-5" />
        </button>
      </div>

      <div class="w-full max-w-sm rounded-xl overflow-hidden border border-zinc-800 bg-zinc-950">
        <QrScanner
          @scanned="handleQrScanned"
          @close="isScannerOpen = false"
        />
      </div>
    </div>
  </div>
</template>
