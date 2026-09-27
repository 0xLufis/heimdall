import { ref, computed, onMounted, getCurrentInstance } from 'vue'
import { createSharedComposable, useActiveElement } from '@vueuse/core'

export function _useShortcuts() {
  const macOS = computed(() => Boolean(import.meta.client && typeof navigator !== 'undefined' && navigator.userAgent && navigator.userAgent.match(/Macintosh;/)))

  const metaSymbol = ref('Ctrl')

  const activeElement = useActiveElement()
  const usingInput = computed(() => {
    const tagName = activeElement.value?.tagName
    const contentEditable = activeElement.value?.contentEditable

    const usingInput = (tagName === 'INPUT' || tagName === 'TEXTAREA' || contentEditable === 'true' || contentEditable === 'plaintext-only')

    if (usingInput) {
      return ((activeElement.value as any)?.name as string) || true
    }

    return false
  })

  if (getCurrentInstance()) {
    onMounted(() => {
      metaSymbol.value = macOS.value ? '⌘' : 'Ctrl'
    })
  }

  return {
    macOS,
    metaSymbol,
    activeElement,
    usingInput,
  }
}

export const useShortcuts = createSharedComposable(_useShortcuts)
