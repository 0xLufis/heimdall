import { ref, computed } from 'vue'

export type TicketSwipeDirection = 'left' | 'right' | 'none'
export type TicketLifecycleState = 'Open' | 'InProgress' | 'Pending' | 'Resolved' | 'Closed'

export interface SwipeTransitionResult {
  previousState: TicketLifecycleState
  nextState: TicketLifecycleState
  actionTaken: 'progress' | 'pending' | 'none'
}

/**
 * Composable for mobile field workflows and touch gestures on maintenance tickets.
 */
export function useMobileFieldWorkflows() {
  const isMobileViewport = ref(false)
  const isTouchDevice = ref(false)

  // Touch start and current tracking for swipe gestures
  const touchStartX = ref(0)
  const touchStartY = ref(0)
  const touchEndX = ref(0)
  const touchEndY = ref(0)
  const minSwipeDistance = 60

  if (typeof window !== 'undefined') {
    isMobileViewport.value = window.innerWidth <= 768
    isTouchDevice.value = 'ontouchstart' in window || navigator.maxTouchPoints > 0

    window.addEventListener('resize', () => {
      isMobileViewport.value = window.innerWidth <= 768
    })
  }

  function handleTouchStart(e: TouchEvent | { clientX: number; clientY: number }) {
    const point = 'touches' in e ? e.touches[0] : e
    touchStartX.value = point.clientX
    touchStartY.value = point.clientY
    touchEndX.value = point.clientX
    touchEndY.value = point.clientY
  }

  function handleTouchMove(e: TouchEvent | { clientX: number; clientY: number }) {
    const point = 'touches' in e ? e.touches[0] : e
    touchEndX.value = point.clientX
    touchEndY.value = point.clientY
  }

  function evaluateSwipe(): TicketSwipeDirection {
    const deltaX = touchEndX.value - touchStartX.value
    const deltaY = Math.abs(touchEndY.value - touchStartY.value)

    // Ensure horizontal motion dominates vertical scrolling
    if (Math.abs(deltaX) > minSwipeDistance && Math.abs(deltaX) > deltaY * 1.5) {
      return deltaX > 0 ? 'right' : 'left'
    }
    return 'none'
  }

  /**
   * Advances ticket lifecycle according to standard field swipe gestures:
   * - Swipe Right: Progress ticket (Open -> InProgress -> Resolved)
   * - Swipe Left: Mark Pending / Hold
   */
  function handleSwipeLifecycleTransition(currentState: TicketLifecycleState, direction: TicketSwipeDirection): SwipeTransitionResult {
    if (direction === 'right') {
      if (currentState === 'Open') {
        return { previousState: currentState, nextState: 'InProgress', actionTaken: 'progress' }
      }
      if (currentState === 'InProgress') {
        return { previousState: currentState, nextState: 'Resolved', actionTaken: 'progress' }
      }
    } else if (direction === 'left') {
      if (currentState === 'InProgress') {
        return { previousState: currentState, nextState: 'Pending', actionTaken: 'pending' }
      }
    }

    return { previousState: currentState, nextState: currentState, actionTaken: 'none' }
  }

  return {
    isMobileViewport,
    isTouchDevice,
    touchStartX,
    touchStartY,
    touchEndX,
    touchEndY,
    handleTouchStart,
    handleTouchMove,
    evaluateSwipe,
    handleSwipeLifecycleTransition
  }
}
