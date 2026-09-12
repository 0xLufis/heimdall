export interface PrioritizedItem {
  priority: number
  [key: string]: any
}

/**
 * Reorders an item within an array from fromIndex to toIndex,
 * and reassigns sequential 1-based priorities to all items.
 *
 * @param items Original array of prioritized items
 * @param fromIndex Source index of the item being moved
 * @param toIndex Target index where the item should be placed
 * @returns A new array with items reordered and priority values updated sequentially
 */
export function reorderAndPrioritize<T extends PrioritizedItem>(
  items: T[],
  fromIndex: number,
  toIndex: number
): T[] {
  if (
    !Array.isArray(items) ||
    items.length === 0 ||
    fromIndex === toIndex ||
    fromIndex < 0 ||
    toIndex < 0 ||
    fromIndex >= items.length ||
    toIndex >= items.length
  ) {
    return Array.isArray(items) ? [...items] : []
  }

  const copy = [...items]
  const [movedItem] = copy.splice(fromIndex, 1)
  copy.splice(toIndex, 0, movedItem)

  return copy.map((item, idx) => ({
    ...item,
    priority: idx + 1
  }))
}

let lastPointerPosition: { x: number; y: number } | null = null

if (typeof window !== 'undefined') {
  const recordPointer = (e: MouseEvent | PointerEvent) => {
    lastPointerPosition = { x: e.clientX, y: e.clientY }
  }
  window.addEventListener('pointerdown', recordPointer, { capture: true, passive: true })
  window.addEventListener('mousedown', recordPointer, { capture: true, passive: true })
}

/**
 * For testing purposes: allows mocking or resetting last pointer position.
 */
export function setLastPointerPositionForTesting(pos: { x: number; y: number } | null): void {
  lastPointerPosition = pos
}

/**
 * Configures the HTML5 DragEvent to anchor the drag preview image at the exact point of click,
 * preventing the browser from snapping to the top-left corner (0, 0).
 *
 * Handles Linux Chromium bug where DragEvent.clientX/Y is 0 during dragstart by falling back
 * to the exact pointerdown coordinates.
 *
 * Clones the draggable element so Blink/WebKit engines respect the custom offset instead of
 * ignoring offsets on the drag source element itself.
 *
 * @param event The native DragEvent from @dragstart
 * @param element Optional explicit element to use as the drag image (defaults to closest [draggable="true"])
 */
export function setDragImageAtClickPoint(
  event: DragEvent,
  element?: HTMLElement | null
): void {
  if (!event || !event.dataTransfer?.setDragImage) return

  const target = element || (event.currentTarget as HTMLElement) || (event.target as HTMLElement)
  if (!target) return

  const draggableEl = (target.closest?.('[draggable="true"]') || target) as HTMLElement
  if (!draggableEl || typeof draggableEl.getBoundingClientRect !== 'function') return

  const rect = draggableEl.getBoundingClientRect()

  // On Linux/Chromium, DragEvent.clientX/Y is often 0 on dragstart.
  // Fall back to the exact pointerdown coordinates captured globally.
  const hasEventCoords = event.clientX !== 0 || event.clientY !== 0
  const clickX = hasEventCoords ? event.clientX : (lastPointerPosition?.x ?? rect.left)
  const clickY = hasEventCoords ? event.clientY : (lastPointerPosition?.y ?? rect.top)

  const offsetX = Math.max(0, Math.min(rect.width, clickX - rect.left))
  const offsetY = Math.max(0, Math.min(rect.height, clickY - rect.top))

  // In Chromium, passing the drag source element itself to setDragImage is ignored or snaps to (0,0).
  // Creating a cloned element forces Chromium to respect the custom offset.
  if (typeof document !== 'undefined') {
    const clone = draggableEl.cloneNode(true) as HTMLElement
    clone.style.width = `${rect.width}px`
    clone.style.height = `${rect.height}px`
    clone.style.boxSizing = 'border-box'
    clone.style.position = 'fixed'
    clone.style.left = '0'
    clone.style.top = '0'
    clone.style.zIndex = '-9999'
    clone.style.pointerEvents = 'none'
    clone.style.margin = '0'
    clone.style.opacity = '0.95'

    document.body.appendChild(clone)
    try {
      event.dataTransfer.setDragImage(clone, offsetX, offsetY)
    } catch {
      event.dataTransfer.setDragImage(draggableEl, offsetX, offsetY)
    }

    setTimeout(() => {
      clone.remove()
    }, 0)
  } else {
    event.dataTransfer.setDragImage(draggableEl, offsetX, offsetY)
  }
}
