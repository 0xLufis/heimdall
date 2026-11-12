import { describe, it, expect } from 'vitest'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describeTerminalLoadError, isDevBypass } from '~/utils/terminalAccess'

const repoRoot = resolve(__dirname, '../../..')

describe('kiosk terminal access messages', () => {
  it('explains a 403 from the certificate path as a station the workstation is not assigned to', () => {
    const failure = describeTerminalLoadError({ statusCode: 403, statusMessage: 'This workstation is not bound to the requested station.' }, 'L01-OP010')
    expect(failure).toEqual({ message: 'This workstation is not assigned to station L01-OP010.', notAssigned: true })
    expect(describeTerminalLoadError({ status: 403 }, '  ').message).toBe('This workstation is not assigned to station this station.')
  })

  it('keeps the token message for 401 and the server text otherwise', () => {
    expect(describeTerminalLoadError({ statusCode: 401 }, 'X')).toEqual({ message: 'Station token invalid or unauthenticated.', notAssigned: false })
    expect(describeTerminalLoadError({ statusCode: 404, statusMessage: 'Machine not found' }, 'X').message).toBe('Machine not found')
    expect(describeTerminalLoadError(new Error('network'), 'X').message).toBe('Unable to load machine terminal data.')
    expect(describeTerminalLoadError(undefined, 'X').notAssigned).toBe(false)
  })

  it('recognises the development bypass only from the BFF\'s authenticatedVia', () => {
    expect(isDevBypass({ workstation: { authenticatedVia: 'DevBypass' } })).toBe(true)
    expect(isDevBypass({ workstation: { authenticatedVia: 'X509Certificate' } })).toBe(false)
    expect(isDevBypass({ workstation: { authenticatedVia: 'WorkstationToken' } })).toBe(false)
    expect(isDevBypass({})).toBe(false)
    expect(isDevBypass(null)).toBe(false)
  })

  it('the terminal page uses both, shows the 403 text and a dev-mode badge in both layouts', () => {
    const page = readFileSync(resolve(repoRoot, 'src/frontend/web/app/pages/terminal/machine/[id].vue'), 'utf8')
    expect(page).toContain("from '~/utils/terminalAccess'")
    expect(page).toContain('describeTerminalLoadError(err,')
    expect(page).toContain('devBypass.value = isDevBypass(res)')
    expect(page.match(/data-testid="terminal-dev-bypass"/g)).toHaveLength(2)
    expect(page.match(/dev mode — no authentication/g)?.length).toBe(2)
    expect(page.match(/data-testid="terminal-auth-error"/g)).toHaveLength(2)
  })
})
