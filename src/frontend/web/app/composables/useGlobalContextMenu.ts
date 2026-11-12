import { ref, shallowRef } from 'vue'
import { clampMenuPosition } from '~/utils/contextMenuActions'
import { findContextTarget } from '~/utils/contextMenuDirective'
import type { ContextMenuContextData, MenuPoint } from '~/types/contextMenu'

/**
 * Global right-click menu: singleton state plus the single delegated listener that opens it.
 *
 * Binding an element is `v-ctx="{ entityType, entityId, ... }"` (see utils/contextMenuDirective).
 * `installContextMenuDelegation` (called once by GlobalContextMenu.vue) listens on the document:
 * a right-click on the innermost `[data-ctx]` element opens the menu for it, a right-click
 * anywhere else dismisses an open menu, and a 500 ms touch hold is replayed as a right-click so
 * explicit `@contextmenu` handlers (plant map, machine cards) get long-press for free.
 */

type MenuOpenEvent =
  | MouseEvent
  | TouchEvent
  | {
    clientX: number
    clientY: number
    shiftKey?: boolean
    target?: EventTarget | null
    preventDefault?: () => void
    stopPropagation?: () => void
  }

/** Size assumed until the rendered menu has been measured (GlobalContextMenu re-clamps). */
const ESTIMATED_MENU_SIZE = { width: 256, height: 360 }
const MENU_SELECTOR = '[data-context-menu]'
export const LONG_PRESS_MS = 500
const LONG_PRESS_MOVE_TOLERANCE_PX = 10

// Global Singleton State
const isOpen = ref(false)
const x = ref(0)
const y = ref(0)
const contextData = shallowRef<ContextMenuContextData | null>(null)
/** Pointer position the menu was requested at (before clamping to the viewport). */
const anchorPoint = shallowRef<MenuPoint>({ x: 0, y: 0 })
/** Element the menu was opened for; scrolling a container around it dismisses the menu. */
const anchorEl = shallowRef<Element | null>(null)
/** Bumped on every open so the component re-measures even when the position is unchanged. */
const openSequence = ref(0)
const isNativeMenuBypassed = ref(false)
let bypassTimer: ReturnType<typeof setTimeout> | null = null
/** Right-clicks already handed to the browser (a second handler of the same event must agree). */
const nativeMenuEvents = new WeakSet<object>()

const asElement = (target: unknown): Element | null => {
  const node = target as (Node & { closest?: unknown }) | null | undefined
  if (!node || typeof node !== 'object') return null
  if (typeof node.closest === 'function') return node as unknown as Element
  return node.parentElement ?? null
}

const viewportSize = () => {
  const root = typeof document !== 'undefined' ? document.documentElement : null
  return {
    width: root?.clientWidth || window.innerWidth,
    height: root?.clientHeight || window.innerHeight
  }
}

const clearBypass = () => {
  isNativeMenuBypassed.value = false
  if (bypassTimer) {
    clearTimeout(bypassTimer)
    bypassTimer = null
  }
}

/** True when this right-click belongs to the browser: Shift held, or the one-shot unlock. */
const takesNativeMenu = (event: object): boolean => {
  if (nativeMenuEvents.has(event)) return true
  if ('shiftKey' in event && (event as { shiftKey?: boolean }).shiftKey) return true
  if (isNativeMenuBypassed.value) {
    clearBypass()
    nativeMenuEvents.add(event)
    return true
  }
  return false
}

const pointOf = (event: MenuOpenEvent): MenuPoint | null => {
  const touch = (event as TouchEvent).touches?.[0] ?? (event as TouchEvent).changedTouches?.[0]
  if (touch) return { x: touch.clientX, y: touch.clientY }
  if ('clientX' in event && typeof event.clientX === 'number') return { x: event.clientX, y: event.clientY }
  return null
}

const closeContextMenu = () => {
  isOpen.value = false
  anchorEl.value = null
}

/**
 * Opens the menu for `data` at the event position. Returns false when the right-click was left
 * to the browser (Shift+right-click, or after "Open Browser Context Menu").
 */
const openContextMenu = (event: MenuOpenEvent, data: ContextMenuContextData, anchor?: Element | null): boolean => {
  if (takesNativeMenu(event)) {
    closeContextMenu()
    return false
  }

  if (typeof event.preventDefault === 'function') event.preventDefault()
  if (typeof event.stopPropagation === 'function') event.stopPropagation()

  const element = anchor ?? asElement((event as { target?: unknown }).target)
  let point = pointOf(event) ?? { x: 100, y: 100 }
  // Keyboard-invoked menus (Menu key) can arrive without a pointer position: use the element.
  if (point.x === 0 && point.y === 0 && element && typeof element.getBoundingClientRect === 'function') {
    const rect = element.getBoundingClientRect()
    point = { x: rect.left + Math.min(rect.width / 2, 24), y: rect.top + Math.min(rect.height / 2, 24) }
  }

  const position = typeof window !== 'undefined'
    ? clampMenuPosition(point, ESTIMATED_MENU_SIZE, viewportSize())
    : point

  anchorPoint.value = point
  x.value = position.x
  y.value = position.y
  contextData.value = data
  anchorEl.value = element
  openSequence.value++
  isOpen.value = true
  return true
}

/** Re-clamps the open menu once its rendered size is known. */
const repositionContextMenu = (menuSize: { width: number; height: number }) => {
  if (!isOpen.value || typeof window === 'undefined') return
  const position = clampMenuPosition(anchorPoint.value, menuSize, viewportSize())
  x.value = position.x
  y.value = position.y
}

const unlockNativeMenu = () => {
  isNativeMenuBypassed.value = true
  closeContextMenu()

  if (bypassTimer) clearTimeout(bypassTimer)
  bypassTimer = setTimeout(() => {
    isNativeMenuBypassed.value = false
    bypassTimer = null
  }, 10000)
}

/**
 * Helper factory for binding long-press gestures on mobile/tablet screens
 */
const bindLongPress = (data: ContextMenuContextData, delayMs: number = LONG_PRESS_MS) => {
  let timer: ReturnType<typeof setTimeout> | null = null

  const onTouchstart = (e: TouchEvent) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => {
      openContextMenu(e, data)
    }, delayMs)
  }

  const clearTimer = () => {
    if (timer) {
      clearTimeout(timer)
      timer = null
    }
  }

  return {
    onTouchstart,
    onTouchend: clearTimer,
    onTouchmove: clearTimer,
    onTouchcancel: clearTimer
  }
}

export const isInsideContextMenu = (target: unknown): boolean => !!asElement(target)?.closest(MENU_SELECTOR)

const TEXT_INPUT_TYPES = new Set(['', 'text', 'search', 'url', 'tel', 'email', 'password', 'number', 'date', 'datetime-local', 'month', 'time', 'week'])

/** Text fields keep the browser menu (paste, spell-check), also inside a bound row. */
export const isTextEntryTarget = (target: unknown): boolean => {
  const field = asElement(target)?.closest('input, textarea, [contenteditable]')
  if (!field) return false
  if (field.tagName === 'TEXTAREA') return true
  if (field.tagName === 'INPUT') return TEXT_INPUT_TYPES.has((field.getAttribute('type') || '').toLowerCase())
  return field.getAttribute('contenteditable') !== 'false'
}

/**
 * Delegated `contextmenu` handler (capture phase on the document). The innermost `[data-ctx]`
 * element wins; anything else dismisses an open menu and lets the event continue to explicit
 * `@contextmenu` handlers.
 */
export function handleDelegatedContextMenu(event: MouseEvent): void {
  if (isInsideContextMenu(event.target)) return

  const bound = isTextEntryTarget(event.target) ? null : findContextTarget(event.target)
  if (!bound) {
    closeContextMenu()
    // The one-shot native unlock is spent on this right-click, whoever handles it next.
    if (isNativeMenuBypassed.value) takesNativeMenu(event)
    return
  }
  openContextMenu(event, bound.data, bound.el)
}

const delegationInstalls = new WeakMap<Document, { count: number; dispose: () => void }>()

/**
 * Installs the delegated right-click and long-press listeners on a document. Returns the
 * uninstaller; repeated installs on the same document share one set of listeners.
 */
export function installContextMenuDelegation(doc?: Document): () => void {
  const target = doc ?? (typeof document !== 'undefined' ? document : undefined)
  if (!target) return () => {}

  const existing = delegationInstalls.get(target)
  if (existing) {
    existing.count++
    return () => releaseDelegation(target)
  }

  let timer: ReturnType<typeof setTimeout> | null = null
  let openedByHold = false
  let start: MenuPoint = { x: 0, y: 0 }

  const clearTimer = () => {
    if (timer) {
      clearTimeout(timer)
      timer = null
    }
  }

  const onContextMenu = (event: MouseEvent) => {
    // A native long-press menu event (Android) makes the replayed one redundant.
    if (event.isTrusted) clearTimer()
    handleDelegatedContextMenu(event)
  }

  const onTouchStart = (event: TouchEvent) => {
    clearTimer()
    openedByHold = false
    if (event.touches.length !== 1) return
    const touchTarget = event.target
    if (isInsideContextMenu(touchTarget) || isTextEntryTarget(touchTarget)) return
    const touch = event.touches[0]
    start = { x: touch.clientX, y: touch.clientY }
    timer = setTimeout(() => {
      timer = null
      const view = target.defaultView
      if (!view || !touchTarget || typeof (touchTarget as EventTarget).dispatchEvent !== 'function') return
      // Replay the hold as a right-click so bound rows and explicit handlers behave alike.
      const replay = new view.MouseEvent('contextmenu', {
        bubbles: true,
        cancelable: true,
        clientX: start.x,
        clientY: start.y,
        button: 2
      })
      const unhandled = (touchTarget as EventTarget).dispatchEvent(replay)
      openedByHold = !unhandled && isOpen.value
    }, LONG_PRESS_MS)
  }

  const onTouchMove = (event: TouchEvent) => {
    const touch = event.touches[0]
    if (!touch) return
    if (Math.abs(touch.clientX - start.x) > LONG_PRESS_MOVE_TOLERANCE_PX || Math.abs(touch.clientY - start.y) > LONG_PRESS_MOVE_TOLERANCE_PX) {
      clearTimer()
    }
  }

  const onTouchEnd = (event: TouchEvent) => {
    clearTimer()
    // The hold opened the menu: swallow the tap that would follow and activate the row.
    if (openedByHold && event.cancelable) event.preventDefault()
    openedByHold = false
  }

  const onTouchCancel = () => {
    clearTimer()
    openedByHold = false
  }

  target.addEventListener('contextmenu', onContextMenu, true)
  target.addEventListener('touchstart', onTouchStart, { passive: true })
  target.addEventListener('touchmove', onTouchMove, { passive: true })
  target.addEventListener('touchend', onTouchEnd, { passive: false })
  target.addEventListener('touchcancel', onTouchCancel, { passive: true })

  delegationInstalls.set(target, {
    count: 1,
    dispose: () => {
      clearTimer()
      target.removeEventListener('contextmenu', onContextMenu, true)
      target.removeEventListener('touchstart', onTouchStart)
      target.removeEventListener('touchmove', onTouchMove)
      target.removeEventListener('touchend', onTouchEnd)
      target.removeEventListener('touchcancel', onTouchCancel)
    }
  })
  return () => releaseDelegation(target)
}

const releaseDelegation = (target: Document) => {
  const entry = delegationInstalls.get(target)
  if (!entry) return
  entry.count--
  if (entry.count > 0) return
  entry.dispose()
  delegationInstalls.delete(target)
}

export function useGlobalContextMenu() {
  return {
    isOpen,
    x,
    y,
    contextData,
    anchorEl,
    openSequence,
    isNativeMenuBypassed,
    openContextMenu,
    closeContextMenu,
    repositionContextMenu,
    unlockNativeMenu,
    bindLongPress
  }
}
