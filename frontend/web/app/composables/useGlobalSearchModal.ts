import { ref } from 'vue'

const isGlobalSearchOpen = ref(false)

/**
 * Global search modal controller.
 * Enables opening and closing the OmniSearch modal from any component
 * (such as the sidebar quick search bar or keyboard shortcuts).
 */
export function useGlobalSearchModal() {
  const openModal = () => {
    isGlobalSearchOpen.value = true
  }

  const closeModal = () => {
    isGlobalSearchOpen.value = false
  }

  const toggleModal = () => {
    isGlobalSearchOpen.value = !isGlobalSearchOpen.value
  }

  return {
    isOpen: isGlobalSearchOpen,
    openModal,
    closeModal,
    toggleModal
  }
}
