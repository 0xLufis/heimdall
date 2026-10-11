<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useGlobalContextMenu } from '~/composables/useGlobalContextMenu'
import {
  Factory,
  Monitor,
  Users,
  UserCheck,
  Wrench,
  Activity,
  FolderTree,
  Copy,
  Check,
  Globe,
  ExternalLink,
  PlusCircle,
  Hash,
  ArrowUpRight,
  Archive,
  Download,
  Share2
} from 'lucide-vue-next'

const router = useRouter()
const { isOpen, x, y, contextData, isNativeMenuBypassed, closeContextMenu, unlockNativeMenu } = useGlobalContextMenu()

const isCopied = ref(false)
const showNativeUnlockNotice = ref(false)
const focusedIndex = ref(0)
const menuContainerRef = ref<HTMLElement | null>(null)

watch(isOpen, (open) => {
  if (open) {
    focusedIndex.value = 0
  }
})

const decommissionMachineFromMenu = () => {
  const mId = contextData.value?.machineId || contextData.value?.entityId
  closeContextMenu()
  if (mId) {
    router.push({ path: '/dashboard/machines', query: { decommission: mId } })
  }
}

const displayName = computed(() => {
  if (!contextData.value) return 'Entity'
  return contextData.value.entityName || contextData.value.machineName || contextData.value.controllerHostname || contextData.value.handle || 'Selected Asset'
})

const entityBadge = computed(() => {
  if (!contextData.value?.entityType) return 'ASSET'
  switch (contextData.value.entityType) {
    case 'map-node': return 'CAD NODE'
    case 'machine': return 'MACHINE'
    case 'controller': return 'CONTROLLER'
    case 'ticket': return 'TICKET'
    default: return 'ASSET'
  }
})

const navigateToMachine = () => {
  const id = contextData.value?.machineId || (contextData.value?.entityType === 'machine' ? contextData.value?.entityId : null)
  const name = contextData.value?.machineName || (contextData.value?.entityType === 'machine' ? contextData.value?.entityName : null)
  closeContextMenu()
  if (id) {
    router.push({ path: '/dashboard/machines', query: { id } })
  } else if (name) {
    router.push({ path: '/dashboard/machines', query: { search: name } })
  } else {
    router.push('/dashboard/machines')
  }
}

const navigateToNode = () => {
  const ctrlId = contextData.value?.controllerId || (contextData.value?.entityType === 'controller' ? contextData.value?.entityId : null)
  const hostname = contextData.value?.controllerHostname
  closeContextMenu()
  if (ctrlId) {
    router.push({ path: '/dashboard/clients', query: { selected: ctrlId, hostname } })
  } else {
    router.push('/dashboard/clients')
  }
}

const navigateToOwnerTeam = () => {
  const teamName = contextData.value?.ownerTeam?.name
  closeContextMenu()
  if (teamName) {
    router.push({ path: '/dashboard/tickets', query: { team: teamName } })
  }
}

const navigateToOwnerPerson = () => {
  const personName = contextData.value?.ownerPerson?.name
  closeContextMenu()
  if (personName) {
    router.push({ path: '/dashboard/tickets', query: { technician: personName } })
  }
}

const navigateToTickets = (create: boolean = false) => {
  const stationId = contextData.value?.machineId || contextData.value?.entityId
  closeContextMenu()
  if (create) {
    router.push({ path: '/dashboard/tickets', query: { create: 'true', stationId: stationId || undefined } })
  } else if (stationId) {
    router.push({ path: '/dashboard/tickets', query: { stationId } })
  } else {
    router.push('/dashboard/tickets')
  }
}

const navigateToTelemetry = () => {
  const ctrlId = contextData.value?.controllerId || (contextData.value?.entityType === 'controller' ? contextData.value?.entityId : null)
  closeContextMenu()
  if (ctrlId) {
    router.push({ path: '/dashboard/telemetry', query: { controllerId: ctrlId } })
  } else {
    router.push('/dashboard/telemetry')
  }
}

const navigateToComponentTree = () => {
  const stationId = contextData.value?.machineId || contextData.value?.entityId
  closeContextMenu()
  if (stationId) {
    router.push({ path: '/dashboard/machines', query: { tree: stationId } })
  }
}

const openInNewTab = () => {
  let url = '/dashboard'
  if (contextData.value?.entityType === 'machine' || contextData.value?.machineId) {
    url = `/dashboard/machines?id=${encodeURIComponent(contextData.value.machineId || contextData.value.entityId || '')}`
  } else if (contextData.value?.entityType === 'controller' || contextData.value?.controllerId) {
    url = `/dashboard/clients?selected=${encodeURIComponent(contextData.value.controllerId || contextData.value.entityId || '')}`
  } else if (contextData.value?.entityType === 'ticket') {
    url = `/dashboard/tickets?ticketId=${encodeURIComponent(contextData.value.entityId || '')}`
  } else if (contextData.value?.entityType === 'inventory' || contextData.value?.partId) {
    url = `/dashboard/inventory/${encodeURIComponent(contextData.value.partId || contextData.value.entityId || '')}`
  } else if (contextData.value?.entityType === 'user' || contextData.value?.userId) {
    url = `/dashboard/users/${encodeURIComponent(contextData.value.userId || contextData.value.entityId || '')}`
  }
  closeContextMenu()
  if (typeof window !== 'undefined') {
    window.open(url, '_blank')
  }
}

const exportEntityData = async () => {
  const data = { ...contextData.value, exportedAt: new Date().toISOString() }
  const jsonStr = JSON.stringify(data, null, 2)
  try {
    await navigator.clipboard.writeText(jsonStr)
    isCopied.value = true
    setTimeout(() => {
      isCopied.value = false
      closeContextMenu()
    }, 1000)
  } catch {
    closeContextMenu()
  }
}

const handleCopyHandle = async () => {
  const text = contextData.value?.handle || contextData.value?.entityId || displayName.value
  if (!text) return
  try {
    await navigator.clipboard.writeText(text)
    isCopied.value = true
    setTimeout(() => {
      isCopied.value = false
      closeContextMenu()
    }, 1200)
  } catch {
    // Ignore clipboard rejection
  }
}

const handleUnlockNative = () => {
  unlockNativeMenu()
  showNativeUnlockNotice.value = true
  setTimeout(() => {
    showNativeUnlockNotice.value = false
  }, 4000)
}

const getVisibleMenuItems = (): HTMLElement[] => {
  if (!menuContainerRef.value) return []
  return Array.from(menuContainerRef.value.querySelectorAll('[data-context-item]')) as HTMLElement[]
}

const handleKeyDown = (e: KeyboardEvent) => {
  if (!isOpen.value) return

  if (e.key === 'Escape') {
    e.preventDefault()
    closeContextMenu()
    return
  }

  const items = getVisibleMenuItems()
  if (items.length === 0) return

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    focusedIndex.value = (focusedIndex.value + 1) % items.length
    items[focusedIndex.value]?.focus()
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    focusedIndex.value = (focusedIndex.value - 1 + items.length) % items.length
    items[focusedIndex.value]?.focus()
  } else if (e.key === 'Enter') {
    e.preventDefault()
    items[focusedIndex.value]?.click()
  }
}

const handleWindowClick = (e: MouseEvent) => {
  if (isOpen.value) {
    closeContextMenu()
  }
}

onMounted(() => {
  if (typeof window !== 'undefined') {
    window.addEventListener('click', handleWindowClick)
    window.addEventListener('keydown', handleKeyDown)
    window.addEventListener('scroll', closeContextMenu, { passive: true })
    window.addEventListener('resize', closeContextMenu)
  }
})

onBeforeUnmount(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('click', handleWindowClick)
    window.removeEventListener('keydown', handleKeyDown)
    window.removeEventListener('scroll', closeContextMenu)
    window.removeEventListener('resize', closeContextMenu)
  }
})
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 pointer-events-none">
    <!-- Context Menu Panel -->
    <div
      ref="menuContainerRef"
      role="menu"
      aria-label="Entity Context Menu"
      class="pointer-events-auto absolute w-64 bg-zinc-950/95 border border-zinc-800 rounded-xl shadow-2xl backdrop-blur text-xs text-zinc-200 py-1.5 overflow-hidden transition-all duration-100 ease-out select-none"
      :style="{ left: `${x}px`, top: `${y}px` }"
      @click.stop
      @contextmenu.prevent
    >
      <!-- Header Banner -->
      <div class="px-3 py-2 border-b border-zinc-800/80 bg-zinc-900/50 flex items-center justify-between gap-2">
        <div class="flex items-center gap-2 truncate">
          <Hash class="w-3.5 h-3.5 text-zinc-400 shrink-0" />
          <span class="font-semibold text-zinc-100 truncate" :title="displayName">{{ displayName }}</span>
        </div>
        <span class="text-[9px] font-mono uppercase px-1.5 py-0.5 rounded bg-zinc-800/80 text-zinc-300 shrink-0">
          {{ entityBadge }}
        </span>
      </div>

      <!-- Domain Navigation Actions -->
      <div class="py-1 space-y-0.5">
        <!-- 1. Go to Machine -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToMachine"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5">
            <Factory class="w-3.5 h-3.5 text-zinc-400 group-hover:text-amber-400 transition-colors" />
            <span>Go to Machine</span>
          </div>
          <ArrowUpRight class="w-3 h-3 text-zinc-500 opacity-0 group-hover:opacity-100 transition-opacity" />
        </button>

        <!-- 2. Go to Node / Controller -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToNode"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5">
            <Monitor class="w-3.5 h-3.5 text-zinc-400 group-hover:text-sky-400 transition-colors" />
            <span>Go to Node / Controller</span>
          </div>
          <ArrowUpRight class="w-3 h-3 text-zinc-500 opacity-0 group-hover:opacity-100 transition-opacity" />
        </button>

        <!-- 3. Open in New Tab -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="openInNewTab"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5">
            <ExternalLink class="w-3.5 h-3.5 text-zinc-400 group-hover:text-primary transition-colors" />
            <span>Open in New Tab</span>
          </div>
          <span class="text-[9px] font-mono text-zinc-500 group-hover:text-zinc-300">^V</span>
        </button>

        <!-- 4. Go to Owner (Team) -->
        <button
          v-if="contextData?.ownerTeam"
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToOwnerTeam"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5 truncate pr-2">
            <Users class="w-3.5 h-3.5 text-zinc-400 group-hover:text-emerald-400 transition-colors shrink-0" />
            <span class="truncate">Owner Team: {{ contextData.ownerTeam.name }}</span>
          </div>
          <ArrowUpRight class="w-3 h-3 text-zinc-500 opacity-0 group-hover:opacity-100 transition-opacity shrink-0" />
        </button>

        <!-- 5. Go to Owner (Person / Assigned Technician) -->
        <button
          v-if="contextData?.ownerPerson"
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToOwnerPerson"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5 truncate pr-2">
            <UserCheck class="w-3.5 h-3.5 text-zinc-400 group-hover:text-teal-400 transition-colors shrink-0" />
            <span class="truncate">Owner Person: {{ contextData.ownerPerson.name }}</span>
          </div>
          <ArrowUpRight class="w-3 h-3 text-zinc-500 opacity-0 group-hover:opacity-100 transition-opacity shrink-0" />
        </button>

        <!-- Divider -->
        <div class="my-1 border-t border-zinc-800/70"></div>

        <!-- 6. Tickets & Incident Creation -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToTickets(false)"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5">
            <Wrench class="w-3.5 h-3.5 text-zinc-400 group-hover:text-orange-400 transition-colors" />
            <span>Go to Maintenance Tickets</span>
          </div>
          <span
            v-if="contextData?.tickets && contextData.tickets.length > 0"
            class="px-1.5 py-0.2 rounded-full text-[10px] font-mono bg-orange-500/20 text-orange-300"
          >
            {{ contextData.tickets.length }}
          </span>
        </button>

        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToTickets(true)"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center gap-2.5 text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <PlusCircle class="w-3.5 h-3.5 text-zinc-400 group-hover:text-emerald-400 transition-colors" />
          <span>Create Incident Ticket</span>
        </button>

        <!-- 7. Telemetry & Diagnostics -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToTelemetry"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center gap-2.5 text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <Activity class="w-3.5 h-3.5 text-zinc-400 group-hover:text-indigo-400 transition-colors" />
          <span>Live Telemetry & Diagnostics</span>
        </button>

        <!-- 8. Component Tree -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="navigateToComponentTree"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center gap-2.5 text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <FolderTree class="w-3.5 h-3.5 text-zinc-400 group-hover:text-purple-400 transition-colors" />
          <span>Inspect Component Tree</span>
        </button>

        <!-- 8b. Decommission Machine (for machine entities) -->
        <button
          v-if="contextData?.entityType === 'machine' || contextData?.machineId"
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="decommissionMachineFromMenu"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center gap-2.5 text-left text-rose-400 hover:bg-rose-500/10 hover:text-rose-300 transition-colors group cursor-pointer focus:bg-rose-500/15 focus:outline-hidden"
        >
          <Archive class="w-3.5 h-3.5 text-rose-400 group-hover:text-rose-300 transition-colors" />
          <span>Decommission Machine</span>
        </button>

        <!-- Divider -->
        <div class="my-1 border-t border-zinc-800/70"></div>

        <!-- 9. Copy Identifier / Handle -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="handleCopyHandle"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5">
            <Check v-if="isCopied" class="w-3.5 h-3.5 text-emerald-400" />
            <Copy v-else class="w-3.5 h-3.5 text-zinc-400 group-hover:text-zinc-200" />
            <span>{{ isCopied ? 'Identifier Copied!' : 'Copy Handle / ID' }}</span>
          </div>
          <span class="text-[10px] text-zinc-500 font-mono">Clip</span>
        </button>

        <!-- 10. Export Data (JSON) -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="exportEntityData"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5">
            <Download class="w-3.5 h-3.5 text-zinc-400 group-hover:text-sky-300" />
            <span>Export Entity Data (JSON)</span>
          </div>
          <span class="text-[10px] text-zinc-500 font-mono">Export</span>
        </button>

        <!-- 11. Open Browser Context Menu (Native fallback) -->
        <button
          type="button"
          role="menuitem"
          data-context-item="true"
          @click="handleUnlockNative"
          class="w-full px-3 py-2 sm:py-1.5 flex items-center justify-between text-left hover:bg-zinc-800/80 hover:text-white transition-colors group text-zinc-400 cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden"
        >
          <div class="flex items-center gap-2.5">
            <Globe class="w-3.5 h-3.5 text-zinc-400 group-hover:text-amber-300" />
            <span class="group-hover:text-zinc-200">Open Browser Context Menu</span>
          </div>
          <span class="text-[9px] font-mono text-zinc-600 group-hover:text-zinc-400">Shift+RClick</span>
        </button>
      </div>
    </div>
  </div>

  <!-- Toast / Notification when Native Menu is Unlocked -->
  <Transition
    enter-active-class="transition duration-200 ease-out"
    enter-from-class="transform translate-y-2 opacity-0"
    enter-to-class="transform translate-y-0 opacity-100"
    leave-active-class="transition duration-150 ease-in"
    leave-from-class="transform translate-y-0 opacity-100"
    leave-to-class="transform translate-y-2 opacity-0"
  >
    <div
      v-if="showNativeUnlockNotice"
      class="fixed bottom-6 right-6 z-50 max-w-sm p-3.5 rounded-xl bg-zinc-900 border border-zinc-700 shadow-2xl text-xs text-zinc-200 flex items-start gap-3 pointer-events-auto"
    >
      <Globe class="w-4 h-4 text-amber-400 mt-0.5 shrink-0" />
      <div class="space-y-1">
        <p class="font-semibold text-zinc-100">Native Browser Menu Unlocked</p>
        <p class="text-[11px] text-zinc-400">
          Right-click anywhere now to access the default browser context menu. Tip: Hold <kbd class="px-1 py-0.5 rounded bg-zinc-800 font-mono text-[10px] text-zinc-300">Shift</kbd> while right-clicking anytime to bypass the custom menu.
        </p>
      </div>
    </div>
  </Transition>
</template>
