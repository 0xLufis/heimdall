import { ref } from 'vue'

export interface ContextMenuContextData {
  entityType?: 'map-node' | 'machine' | 'controller' | 'ticket' | 'general'
  entityId?: string
  entityName?: string
  handle?: string
  machineId?: string
  machineName?: string
  controllerId?: string
  controllerHostname?: string
  ownerTeam?: { id?: string; name: string }
  ownerPerson?: { id?: string; name: string; email?: string }
  tickets?: Array<{ id: string; title: string; status: string }>
  raw?: any
}

// Global Singleton State
const isOpen = ref(false)
const x = ref(0)
const y = ref(0)
const contextData = ref<ContextMenuContextData | null>(null)
const isNativeMenuBypassed = ref(false)
let bypassTimer: ReturnType<typeof setTimeout> | null = null

export function useGlobalContextMenu() {
  const openContextMenu = (
    event: MouseEvent | TouchEvent | { clientX: number; clientY: number; shiftKey?: boolean; preventDefault?: () => void; stopPropagation?: () => void },
    data: ContextMenuContextData
  ) => {
    // If Shift is pressed or native menu bypass is active, let native browser menu open
    if (('shiftKey' in event && event.shiftKey) || isNativeMenuBypassed.value) {
      if (isNativeMenuBypassed.value) {
        isNativeMenuBypassed.value = false
        if (bypassTimer) {
          clearTimeout(bypassTimer)
          bypassTimer = null
        }
      }
      closeContextMenu()
      return
    }

    if (typeof event.preventDefault === 'function') event.preventDefault()
    if (typeof event.stopPropagation === 'function') event.stopPropagation()

    // Determine coordinates from MouseEvent or TouchEvent
    let clientX = 100
    let clientY = 100

    if ('touches' in event && event.touches && event.touches.length > 0) {
      clientX = event.touches[0].clientX
      clientY = event.touches[0].clientY
    } else if ('changedTouches' in event && (event as any).changedTouches && (event as any).changedTouches.length > 0) {
      clientX = (event as any).changedTouches[0].clientX
      clientY = (event as any).changedTouches[0].clientY
    } else if ('clientX' in event) {
      clientX = event.clientX
      clientY = event.clientY
    }

    // Clamp coordinates to viewport dimensions to avoid overflow
    const menuWidth = 260
    const menuHeight = 360
    const padding = 12

    let targetX = clientX
    let targetY = clientY

    if (typeof window !== 'undefined') {
      if (targetX + menuWidth > window.innerWidth - padding) {
        targetX = Math.max(padding, window.innerWidth - menuWidth - padding)
      }
      if (targetY + menuHeight > window.innerHeight - padding) {
        targetY = Math.max(padding, window.innerHeight - menuHeight - padding)
      }
    }

    x.value = targetX
    y.value = targetY
    contextData.value = data
    isOpen.value = true
  }

  const closeContextMenu = () => {
    isOpen.value = false
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
  const bindLongPress = (data: ContextMenuContextData, delayMs: number = 500) => {
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

  return {
    isOpen,
    x,
    y,
    contextData,
    isNativeMenuBypassed,
    openContextMenu,
    closeContextMenu,
    unlockNativeMenu,
    bindLongPress
  }
}
