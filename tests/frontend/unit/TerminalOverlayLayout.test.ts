import { describe, it, expect } from 'vitest'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { parseOverlayParam, resolveTerminalLayout, OVERLAY_MAX_WIDTH_PX } from '~/utils/terminalLayout'

const repoRoot = resolve(__dirname, '../../..')

describe('kiosk terminal overlay layout', () => {
  it('parses the overlay query parameter', () => {
    expect(parseOverlayParam('1')).toBe(true)
    expect(parseOverlayParam('true')).toBe(true)
    expect(parseOverlayParam(null)).toBe(true) // bare ?overlay
    expect(parseOverlayParam(['1', '0'])).toBe(true)
    expect(parseOverlayParam('0')).toBe(false)
    expect(parseOverlayParam('false')).toBe(false)
    expect(parseOverlayParam(undefined)).toBeNull()
    expect(parseOverlayParam('maybe')).toBeNull()
  })

  it('lets the query win over the window width', () => {
    expect(resolveTerminalLayout('1', 1920)).toBe('overlay')
    expect(resolveTerminalLayout('0', 360)).toBe('full')
  })

  it('picks the overlay for narrow windows only once the width is known', () => {
    expect(resolveTerminalLayout(undefined, 420)).toBe('overlay')
    expect(resolveTerminalLayout(undefined, OVERLAY_MAX_WIDTH_PX - 1)).toBe('overlay')
    expect(resolveTerminalLayout(undefined, OVERLAY_MAX_WIDTH_PX)).toBe('full')
    expect(resolveTerminalLayout(undefined, null)).toBe('full') // SSR
  })

  it('uses normal inputs (no on-screen keypad) and offers both layouts', () => {
    const page = readFileSync(resolve(repoRoot, 'src/frontend/web/app/pages/terminal/machine/[id].vue'), 'utf8')
    expect(page).not.toMatch(/VirtualNumpad/)
    expect(page).toMatch(/data-testid="terminal-overlay"/)
    expect(page).toMatch(/expandToFullView/)
    expect(page).toMatch(/Heimdall Terminal/) // window title the IPC launcher looks for
  })

  it('launcher opens a chromeless app window with the overlay URL and pins it on top', () => {
    const ps1 = readFileSync(resolve(repoRoot, 'infra/windows/tray/Start-HeimdallTerminal.ps1'), 'utf8')
    expect(ps1).toMatch(/--app=/)
    expect(ps1).toMatch(/--kiosk/)
    expect(ps1).toMatch(/--user-data-dir=/)
    expect(ps1).toMatch(/overlay=1/)
    expect(ps1).toMatch(/SetWindowPos/)
    expect(ps1).toMatch(/\$WindowTitlePrefix = 'Heimdall Terminal'/)
    // Plain ASCII: Windows PowerShell 5.1 reads BOM-less UTF-8 scripts as ANSI.
    expect(/^[\x00-\x7F]*$/.test(ps1)).toBe(true)
  })
})
