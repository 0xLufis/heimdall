<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, type Component } from 'vue'
import { useRouter } from 'vue-router'
import {
  useGlobalContextMenu,
  installContextMenuDelegation,
  isInsideContextMenu
} from '~/composables/useGlobalContextMenu'
import {
  buildContextMenuActions,
  contextBadge,
  contextCopyValue,
  contextDisplayName
} from '~/utils/contextMenuActions'
import type { ContextMenuAction, ContextMenuActionKey } from '~/types/contextMenu'
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
  MapPin,
  Eye,
  AlertCircle
} from 'lucide-vue-next'

const router = useRouter()
const {
  isOpen,
  x,
  y,
  contextData,
  anchorEl,
  openSequence,
  closeContextMenu,
  repositionContextMenu,
  unlockNativeMenu
} = useGlobalContextMenu()

type UtilityKey = 'copy' | 'export' | 'native'
type MenuRow =
  | { kind: 'action'; id: string; action: ContextMenuAction }
  | { kind: 'utility'; id: string; utility: UtilityKey }
  | { kind: 'divider'; id: string }

/** Icon and hover accent of each navigation entry (the rules live in utils/contextMenuActions). */
const ACTION_VIEW: Record<ContextMenuActionKey, { icon: Component; accent: string }> = {
  open: { icon: Eye, accent: 'group-hover:text-primary' },
  machine: { icon: Factory, accent: 'group-hover:text-amber-400' },
  controller: { icon: Monitor, accent: 'group-hover:text-sky-400' },
  'new-tab': { icon: ExternalLink, accent: 'group-hover:text-primary' },
  'owner-team': { icon: Users, accent: 'group-hover:text-emerald-400' },
  'owner-person': { icon: UserCheck, accent: 'group-hover:text-teal-400' },
  map: { icon: MapPin, accent: 'group-hover:text-cyan-400' },
  tickets: { icon: Wrench, accent: 'group-hover:text-orange-400' },
  'create-ticket': { icon: PlusCircle, accent: 'group-hover:text-emerald-400' },
  telemetry: { icon: Activity, accent: 'group-hover:text-indigo-400' },
  'component-tree': { icon: FolderTree, accent: 'group-hover:text-purple-400' },
  decommission: { icon: Archive, accent: 'group-hover:text-rose-300' }
}

const ITEM_CLASS = 'w-full px-3 py-2 sm:py-1.5 flex items-center justify-between gap-2 text-left hover:bg-zinc-800/80 hover:text-white transition-colors group cursor-pointer focus:bg-zinc-800/90 focus:outline-hidden data-[active=true]:bg-zinc-800/90 data-[active=true]:text-white'
const DANGER_ITEM_CLASS = 'w-full px-3 py-2 sm:py-1.5 flex items-center justify-between gap-2 text-left text-rose-400 hover:bg-rose-500/10 hover:text-rose-300 transition-colors group cursor-pointer focus:bg-rose-500/15 focus:outline-hidden data-[active=true]:bg-rose-500/15 data-[active=true]:text-rose-300'

const menuContainerRef = ref<HTMLElement | null>(null)
const showNativeUnlockNotice = ref(false)
/** Keyboard-highlighted entry (index into `focusableRows`); -1 until an arrow key is pressed. */
const activeIndex = ref(-1)
const feedback = ref<{ utility: UtilityKey; ok: boolean } | null>(null)
let feedbackTimer: ReturnType<typeof setTimeout> | null = null
let noticeTimer: ReturnType<typeof setTimeout> | null = null
let focusBeforeMenu: HTMLElement | null = null

const currentPath = computed(() => router?.currentRoute?.value?.path || '')
const displayName = computed(() => contextDisplayName(contextData.value))
const entityBadge = computed(() => contextBadge(contextData.value))
const actions = computed(() => buildContextMenuActions(contextData.value, { currentPath: currentPath.value }))

/** Navigation entries, then station work + destructive entries, then the always-present utilities. */
const rows = computed<MenuRow[]>(() => {
  const list: MenuRow[] = []
  const navigate = actions.value.filter(a => a.group === 'navigate')
  const work = actions.value.filter(a => a.group !== 'navigate')
  for (const action of navigate) list.push({ kind: 'action', id: `action:${action.key}`, action })
  if (navigate.length > 0 && work.length > 0) list.push({ kind: 'divider', id: 'divider:work' })
  for (const action of work) list.push({ kind: 'action', id: `action:${action.key}`, action })
  if (actions.value.length > 0) list.push({ kind: 'divider', id: 'divider:utilities' })
  list.push({ kind: 'utility', id: 'utility:copy', utility: 'copy' })
  list.push({ kind: 'utility', id: 'utility:export', utility: 'export' })
  list.push({ kind: 'utility', id: 'utility:native', utility: 'native' })
  return list
})

const focusableRows = computed(() => rows.value.filter(row => row.kind !== 'divider'))
const activeRowId = computed(() => focusableRows.value[activeIndex.value]?.id ?? null)

const clearFeedback = () => {
  if (feedbackTimer) {
    clearTimeout(feedbackTimer)
    feedbackTimer = null
  }
  feedback.value = null
}

// ── Navigation entries ──────────────────────────────────────────────────────
const runAction = (action: ContextMenuAction) => {
  closeContextMenu()
  if (action.newTab) {
    if (typeof window !== 'undefined') window.open(action.route, '_blank', 'noopener')
    return
  }
  // A rejected navigation (guard, duplicate) must not surface as an unhandled rejection.
  Promise.resolve(router?.push(action.route)).catch(() => {})
}

// ── Utilities ───────────────────────────────────────────────────────────────
const writeClipboard = async (value: string): Promise<boolean> => {
  try {
    if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(value)
      return true
    }
  } catch {
    // Permission denied or insecure origin: try the legacy path below.
  }
  try {
    const area = document.createElement('textarea')
    area.value = value
    area.setAttribute('readonly', '')
    area.style.position = 'fixed'
    area.style.top = '0'
    area.style.left = '0'
    area.style.opacity = '0'
    document.body.appendChild(area)
    area.select()
    const copied = document.execCommand('copy')
    area.remove()
    return copied
  } catch {
    return false
  }
}

/** Shows the result on the entry itself, then closes the menu (it stays open on failure). */
const reportClipboard = (utility: UtilityKey, ok: boolean) => {
  clearFeedback()
  feedback.value = { utility, ok }
  feedbackTimer = setTimeout(() => {
    feedbackTimer = null
    feedback.value = null
    if (ok) closeContextMenu()
  }, ok ? 1200 : 2000)
}

const handleCopyHandle = async () => {
  const value = contextCopyValue(contextData.value) || displayName.value
  if (!value) return
  reportClipboard('copy', await writeClipboard(value))
}

const exportEntityData = async () => {
  const data = { ...contextData.value, exportedAt: new Date().toISOString() }
  let json: string
  try {
    json = JSON.stringify(data, null, 2)
  } catch {
    // `raw` may hold circular component state: export the identifying fields only.
    const { raw: _raw, ...plain } = data
    json = JSON.stringify(plain, null, 2)
  }
  reportClipboard('export', await writeClipboard(json))
}

const handleUnlockNative = () => {
  unlockNativeMenu()
  showNativeUnlockNotice.value = true
  if (noticeTimer) clearTimeout(noticeTimer)
  noticeTimer = setTimeout(() => {
    noticeTimer = null
    showNativeUnlockNotice.value = false
  }, 4000)
}

const runUtility = (utility: UtilityKey) => {
  if (utility === 'copy') return handleCopyHandle()
  if (utility === 'export') return exportEntityData()
  return handleUnlockNative()
}

const activateRow = (row: MenuRow | undefined) => {
  if (!row) return
  if (row.kind === 'action') runAction(row.action)
  else if (row.kind === 'utility') runUtility(row.utility)
}

const utilityLabel = (utility: UtilityKey) => {
  const state = feedback.value?.utility === utility ? feedback.value : null
  if (state && !state.ok) return 'Clipboard unavailable'
  if (utility === 'copy') return state ? 'Identifier Copied!' : 'Copy Handle / ID'
  if (utility === 'export') return state ? 'JSON Copied!' : 'Export Entity Data (JSON)'
  return 'Open Browser Context Menu'
}

// ── Keyboard ────────────────────────────────────────────────────────────────
const setActive = (index: number) => {
  const count = focusableRows.value.length
  if (count === 0) return
  activeIndex.value = ((index % count) + count) % count
  const id = activeRowId.value
  const el = id ? menuContainerRef.value?.querySelector<HTMLElement>(`[data-context-item="${id}"]`) : null
  if (!el) return
  const focused = typeof document !== 'undefined' ? (document.activeElement as HTMLElement | null) : null
  if (focused && !isInsideContextMenu(focused)) focusBeforeMenu = focused
  el.focus({ preventScroll: true })
  if (typeof el.scrollIntoView === 'function') el.scrollIntoView({ block: 'nearest' })
}

/** Handled in the capture phase so page shortcuts and focused rows do not react as well. */
const handleKeyDown = (e: KeyboardEvent) => {
  if (!isOpen.value) return
  const swallow = () => {
    e.preventDefault()
    e.stopPropagation()
  }

  switch (e.key) {
    case 'Escape':
      swallow()
      closeContextMenu()
      return
    case 'ArrowDown':
      swallow()
      setActive(activeIndex.value + 1)
      return
    case 'ArrowUp':
      swallow()
      setActive(activeIndex.value < 0 ? -1 : activeIndex.value - 1)
      return
    case 'Home':
      swallow()
      setActive(0)
      return
    case 'End':
      swallow()
      setActive(-1)
      return
    case 'Enter':
      swallow()
      activateRow(focusableRows.value[activeIndex.value])
      return
    case ' ':
      if (activeIndex.value < 0) return
      swallow()
      activateRow(focusableRows.value[activeIndex.value])
      return
    case 'Tab':
      closeContextMenu()
  }
}

// ── Dismissal ───────────────────────────────────────────────────────────────
// Capture phase: rows that stop click propagation must still dismiss the menu.
const handlePointerDown = (e: Event) => {
  if (!isOpen.value || isInsideContextMenu(e.target)) return
  closeContextMenu()
}

/**
 * Page scrolls dismiss the menu, and so does scrolling any container that holds the element it
 * was opened for (scroll events do not bubble, hence the capture listener). Scrolling inside the
 * menu or in an unrelated panel leaves it alone.
 */
const handleScroll = (e: Event) => {
  if (!isOpen.value) return
  const target = e.target as Node | null
  if (!target || isInsideContextMenu(target)) return
  const isPageScroll = target === document || target === document.documentElement || target === document.body
  const anchor = anchorEl.value
  if (isPageScroll || (anchor && typeof target.contains === 'function' && target.contains(anchor))) {
    closeContextMenu()
  }
}

const measureAndClamp = () => {
  const el = menuContainerRef.value
  if (!el || !isOpen.value) return
  const rect = el.getBoundingClientRect()
  if (rect.width > 0 && rect.height > 0) repositionContextMenu({ width: rect.width, height: rect.height })
}

// After the menu rendered for a new target: clamp with its real size (before the browser paints).
watch(openSequence, measureAndClamp, { flush: 'post' })

watch(isOpen, (open) => {
  activeIndex.value = -1
  clearFeedback()
  if (open) {
    focusBeforeMenu = null
    return
  }
  // Keyboard navigation moved focus into the menu: hand it back before the menu is removed.
  if (focusBeforeMenu && typeof document !== 'undefined' && isInsideContextMenu(document.activeElement) && focusBeforeMenu.isConnected) {
    focusBeforeMenu.focus({ preventScroll: true })
  }
  focusBeforeMenu = null
})

// Any navigation (menu entry, link, back button) leaves the entity the menu was opened for.
watch(() => router?.currentRoute?.value?.fullPath, () => closeContextMenu())

let uninstallDelegation: (() => void) | null = null

onMounted(() => {
  if (typeof window === 'undefined') return
  uninstallDelegation = installContextMenuDelegation()
  window.addEventListener('pointerdown', handlePointerDown, true)
  window.addEventListener('keydown', handleKeyDown, true)
  window.addEventListener('scroll', handleScroll, { capture: true, passive: true })
  window.addEventListener('resize', closeContextMenu)
  window.addEventListener('blur', closeContextMenu)
})

onBeforeUnmount(() => {
  clearFeedback()
  if (noticeTimer) clearTimeout(noticeTimer)
  if (typeof window === 'undefined') return
  uninstallDelegation?.()
  uninstallDelegation = null
  window.removeEventListener('pointerdown', handlePointerDown, true)
  window.removeEventListener('keydown', handleKeyDown, true)
  window.removeEventListener('scroll', handleScroll, { capture: true })
  window.removeEventListener('resize', closeContextMenu)
  window.removeEventListener('blur', closeContextMenu)
})
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-[1000] pointer-events-none">
    <!-- Context Menu Panel -->
    <div
      ref="menuContainerRef"
      data-context-menu
      role="menu"
      aria-label="Entity Context Menu"
      tabindex="-1"
      class="pointer-events-auto absolute w-64 max-w-[calc(100vw-24px)] max-h-[calc(100vh-24px)] overflow-y-auto overscroll-contain bg-zinc-950/95 border border-zinc-800 rounded-xl shadow-2xl backdrop-blur text-xs text-zinc-200 py-1.5 select-none focus:outline-hidden"
      :style="{ left: `${x}px`, top: `${y}px` }"
      @pointerdown.stop
      @click.stop
      @contextmenu.prevent.stop
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

      <div class="py-1 space-y-0.5">
        <template v-for="row in rows" :key="row.id">
          <div v-if="row.kind === 'divider'" role="separator" class="my-1 border-t border-zinc-800/70"></div>

          <!-- Navigation entry (only present when the entity carries the data it needs) -->
          <button
            v-else-if="row.kind === 'action'"
            type="button"
            role="menuitem"
            :data-context-item="row.id"
            :data-context-action="row.action.key"
            :data-active="activeRowId === row.id"
            :class="row.action.group === 'danger' ? DANGER_ITEM_CLASS : ITEM_CLASS"
            @click="runAction(row.action)"
          >
            <div class="flex items-center gap-2.5 min-w-0">
              <component
                :is="ACTION_VIEW[row.action.key].icon"
                class="w-3.5 h-3.5 shrink-0 transition-colors"
                :class="[row.action.group === 'danger' ? 'text-rose-400' : 'text-zinc-400', ACTION_VIEW[row.action.key].accent]"
              />
              <span class="truncate">{{ row.action.label }}</span>
            </div>
            <span
              v-if="row.action.badge"
              class="px-1.5 py-0.2 rounded-full text-[10px] font-mono bg-orange-500/20 text-orange-300 shrink-0"
            >
              {{ row.action.badge }}
            </span>
            <span v-else-if="row.action.newTab" class="text-[9px] font-mono text-zinc-500 group-hover:text-zinc-300 shrink-0">New tab</span>
            <ArrowUpRight
              v-else-if="row.action.group === 'navigate'"
              class="w-3 h-3 text-zinc-500 opacity-0 group-hover:opacity-100 transition-opacity shrink-0"
            />
          </button>

          <!-- Utilities: available for every entity -->
          <button
            v-else
            type="button"
            role="menuitem"
            :data-context-item="row.id"
            :data-active="activeRowId === row.id"
            :class="[ITEM_CLASS, row.utility === 'native' ? 'text-zinc-400' : '']"
            @click="runUtility(row.utility)"
          >
            <div class="flex items-center gap-2.5 min-w-0">
              <template v-if="feedback?.utility === row.utility">
                <Check v-if="feedback.ok" class="w-3.5 h-3.5 text-emerald-400 shrink-0" />
                <AlertCircle v-else class="w-3.5 h-3.5 text-amber-400 shrink-0" />
              </template>
              <Copy v-else-if="row.utility === 'copy'" class="w-3.5 h-3.5 text-zinc-400 group-hover:text-zinc-200 shrink-0" />
              <Download v-else-if="row.utility === 'export'" class="w-3.5 h-3.5 text-zinc-400 group-hover:text-sky-300 shrink-0" />
              <Globe v-else class="w-3.5 h-3.5 text-zinc-400 group-hover:text-amber-300 shrink-0" />
              <span class="truncate" :class="row.utility === 'native' ? 'group-hover:text-zinc-200' : ''">{{ utilityLabel(row.utility) }}</span>
            </div>
            <span v-if="row.utility === 'copy'" class="text-[10px] text-zinc-500 font-mono shrink-0">Clip</span>
            <span v-else-if="row.utility === 'export'" class="text-[10px] text-zinc-500 font-mono shrink-0">Export</span>
            <span v-else class="text-[9px] font-mono text-zinc-600 group-hover:text-zinc-400 shrink-0">Shift+RClick</span>
          </button>
        </template>
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
      class="fixed bottom-6 right-6 z-[1000] max-w-sm p-3.5 rounded-xl bg-zinc-900 border border-zinc-700 shadow-2xl text-xs text-zinc-200 flex items-start gap-3 pointer-events-auto"
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
