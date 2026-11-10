/**
 * Pure placement math for HTML telemetry overlays (plant-map bubbles and 3D digital-twin HUD).
 *
 * Contract: whenever a placement is `visible`, the box
 * `[left, left + width] x [top, top + height]` lies inside the safe area
 * `[insets.left, containerWidth - insets.right] x [insets.top, containerHeight - insets.bottom]`.
 * The box size shrinks to the safe area when the container is too small, and callers must apply
 * `width` / `maxHeight` to the rendered element so the real DOM box cannot exceed the computed one.
 */

export interface OverlayInsets {
  top: number
  right: number
  bottom: number
  left: number
}

export interface OverlayBoxSize {
  width: number
  height: number
}

export interface OverlayPlacementInput {
  anchorX: number
  anchorY: number
  containerWidth: number
  containerHeight: number
  /** Preferred overlay size; shrunk to the safe area if it does not fit. */
  size: OverlayBoxSize
  insets: OverlayInsets
  /** Gap between the anchor and the overlay when placed above. */
  gapAbove?: number
  /** Gap between the anchor and the overlay when flipped below. */
  gapBelow?: number
}

export interface OverlayPlacement {
  visible: boolean
  left: number
  top: number
  width: number
  height: number
  /** True when the overlay sits below its anchor (pointer arrow on top). */
  isFlippedBottom: boolean
  /** Horizontal anchor position relative to the overlay's left edge (for the pointer arrow). */
  arrowOffset: number
}

const HIDDEN: OverlayPlacement = { visible: false, left: 0, top: 0, width: 0, height: 0, isFlippedBottom: false, arrowOffset: 0 }

/** Default safe area of the plant map: floating HUD toolbar on top, legend / zoom controls at the bottom. */
export const PLANT_MAP_OVERLAY_INSETS: OverlayInsets = { top: 62, right: 14, bottom: 20, left: 14 }
export const PLANT_MAP_PILL_SIZE: OverlayBoxSize = { width: 220, height: 30 }
export const PLANT_MAP_CARD_SIZE: OverlayBoxSize = { width: 320, height: 390 }

export function computeOverlayPlacement(input: OverlayPlacementInput): OverlayPlacement {
  const { anchorX, anchorY, containerWidth: cw, containerHeight: ch, insets } = input
  const gapAbove = input.gapAbove ?? 8
  const gapBelow = input.gapBelow ?? 14

  if (![anchorX, anchorY, cw, ch].every(Number.isFinite)) return HIDDEN
  // Only stations actually on screen get an overlay; an overlay clamped to the edge for an
  // off-screen anchor would point at nothing.
  if (anchorX < 0 || anchorX > cw || anchorY < 0 || anchorY > ch) return HIDDEN

  const minX = insets.left
  const maxX = cw - insets.right
  const minY = insets.top
  const maxY = ch - insets.bottom
  const availW = maxX - minX
  const availH = maxY - minY
  if (availW <= 0 || availH <= 0) return HIDDEN

  const width = Math.min(input.size.width, availW)
  const height = Math.min(input.size.height, availH)

  const left = Math.max(minX, Math.min(anchorX - width / 2, maxX - width))

  const topAbove = anchorY - gapAbove - height
  const topBelow = anchorY + gapBelow
  let top: number
  let isFlippedBottom: boolean
  if (topAbove >= minY) {
    top = topAbove
    isFlippedBottom = false
  } else if (topBelow + height <= maxY) {
    top = topBelow
    isFlippedBottom = true
  } else {
    // Fits on neither side without covering the anchor: use the roomier side, then clamp.
    isFlippedBottom = (maxY - anchorY) > (anchorY - minY)
    top = isFlippedBottom ? topBelow : topAbove
  }
  top = Math.max(minY, Math.min(top, maxY - height))

  const arrowOffset = Math.max(12, Math.min(anchorX - left, width - 12))

  return {
    visible: true,
    left: Math.round(left),
    top: Math.round(top),
    width: Math.floor(width),
    height: Math.floor(height),
    isFlippedBottom,
    arrowOffset: Math.round(arrowOffset)
  }
}

export interface SvgViewBox {
  x: number
  y: number
  width: number
  height: number
}

/**
 * Maps a drawing-space point to CSS pixels inside an <svg> using the default
 * `preserveAspectRatio="xMidYMid meet"` behaviour.
 */
export function projectSvgPointToScreen(
  point: { x: number; y: number },
  viewBox: SvgViewBox,
  containerWidth: number,
  containerHeight: number
): { x: number; y: number } | null {
  if (!viewBox.width || !viewBox.height || containerWidth <= 0 || containerHeight <= 0) return null
  const scale = Math.min(containerWidth / viewBox.width, containerHeight / viewBox.height)
  const offsetX = (containerWidth - viewBox.width * scale) / 2
  const offsetY = (containerHeight - viewBox.height * scale) / 2
  return {
    x: offsetX + (point.x - viewBox.x) * scale,
    y: offsetY + (point.y - viewBox.y) * scale
  }
}
