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
  const openContextMenu = (event: MouseEvent, data: ContextMenuContextData) => {
    // If Shift is pressed or native menu bypass is active, let native browser menu open
    if (event.shiftKey || isNativeMenuBypassed.value) {
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

    event.preventDefault()
    event.stopPropagation()

    // Clamp coordinates to viewport dimensions to avoid overflow
    const menuWidth = 260
    const menuHeight = 340
    const padding = 12

    let targetX = event.clientX
    let targetY = event.clientY

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

  return {
    isOpen,
    x,
    y,
    contextData,
    isNativeMenuBypassed,
    openContextMenu,
    closeContextMenu,
    unlockNativeMenu
  }
}
