/**
 * Layout of the kiosk station terminal (`pages/terminal/machine/[id].vue`).
 *
 * The terminal runs either full screen / in a large window (full view) or as a compact overlay in a
 * small always-on-top app window next to or over the machine HMI (see
 * `infra/windows/tray/Start-HeimdallTerminal.ps1`).
 *
 * - `?overlay=1` / `true` / bare `?overlay` forces the overlay, `?overlay=0` / `false` the full view.
 * - Without the parameter the window width decides: narrower than {@link OVERLAY_MAX_WIDTH_PX} (a
 *   side strip, a 420x700 window) gets the overlay. The width is unknown during SSR (`null`), so the
 *   server renders the full view unless the query asks for the overlay.
 */
export const OVERLAY_MAX_WIDTH_PX = 640

export type TerminalLayout = 'overlay' | 'full'

/** `true`/`false` when the query value forces a layout, `null` when it is absent or unrecognised. */
export function parseOverlayParam(value: unknown): boolean | null {
  const raw = Array.isArray(value) ? value[0] : value
  // vue-router reports a bare `?overlay` as null; an absent key is undefined.
  if (raw === null) return true
  if (typeof raw !== 'string') return null
  const v = raw.trim().toLowerCase()
  if (v === '' || v === '1' || v === 'true' || v === 'yes' || v === 'on') return true
  if (v === '0' || v === 'false' || v === 'no' || v === 'off') return false
  return null
}

export function resolveTerminalLayout(overlayParam: unknown, viewportWidth: number | null): TerminalLayout {
  const forced = parseOverlayParam(overlayParam)
  if (forced !== null) return forced ? 'overlay' : 'full'
  return viewportWidth !== null && viewportWidth < OVERLAY_MAX_WIDTH_PX ? 'overlay' : 'full'
}
