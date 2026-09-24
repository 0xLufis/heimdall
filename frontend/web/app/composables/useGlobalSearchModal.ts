import { ref, type Ref } from 'vue'

const TRIGGER_TIMEOUT_MS = 300

// Helper to access Nuxt 3 useState when available, falling back to vue ref in vitest/node
function getModalState<T>(key: string, init: () => T): Ref<T> {
  if (typeof useState !== 'undefined') {
    return useState<T>(key, init)
  }
  return ref(init()) as Ref<T>
}

/**
 * Global search modal controller.
 * Enables opening and managing the OmniSearch modal from any component
 * with state change triggers, debounce timeouts, and resistance against
 * page re-paints and rapid key-repeat flickering.
 */
export function useGlobalSearchModal() {
  const isGlobalSearchOpen = getModalState<boolean>('global_search_modal_open', () => false)
  const lastTriggerTimestamp = getModalState<number>('global_search_last_trigger', () => 0)
  const isStateTransitioning = getModalState<boolean>('global_search_is_transitioning', () => false)
  const focusTriggerSignal = getModalState<number>('global_search_focus_signal', () => 0)

  /**
   * Programmatic open
   */
  const openModal = () => {
    isGlobalSearchOpen.value = true
  }

  /**
   * Programmatic close
   */
  const closeModal = () => {
    isGlobalSearchOpen.value = false
  }

  /**
   * Dedicated trigger for CTRL+K / keyboard shortcut execution.
   * Ensures CTRL+K does NOT close, flicker, or require button spamming.
   * If already open, keeps it open and signals the input to refocus & select all.
   * If closed, opens cleanly with state change timeout protection.
   */
  const triggerSearch = () => {
    const now = Date.now()

    // If already open, do not abruptly close or flicker; refocus input instead
    if (isGlobalSearchOpen.value) {
      focusTriggerSignal.value++
      lastTriggerTimestamp.value = now
      return
    }

    // Cooldown check for opening to ignore duplicate OS key repeats
    if (now - lastTriggerTimestamp.value < TRIGGER_TIMEOUT_MS) {
      isGlobalSearchOpen.value = true
      focusTriggerSignal.value++
      return
    }

    lastTriggerTimestamp.value = now
    isStateTransitioning.value = true
    isGlobalSearchOpen.value = true
    focusTriggerSignal.value++

    setTimeout(() => {
      isStateTransitioning.value = false
    }, TRIGGER_TIMEOUT_MS)
  }

  const toggleModal = () => {
    isGlobalSearchOpen.value = !isGlobalSearchOpen.value
  }

  return {
    isOpen: isGlobalSearchOpen,
    isTransitioning: isStateTransitioning,
    focusTriggerSignal,
    openModal,
    closeModal,
    toggleModal,
    triggerSearch
  }
}
