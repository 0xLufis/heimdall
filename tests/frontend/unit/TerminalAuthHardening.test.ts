import { describe, it, expect, vi, afterEach } from 'vitest'

vi.mock('~~/server/utils/auth', () => ({ auth: { api: { getSession: vi.fn().mockResolvedValue(null) } } }))
vi.mock('~~/server/utils/pkiStore', () => ({
  getAllCertificates: () => [
    { id: 'c1', commonName: 'KIOSK-OP10', thumbprint: 'AABBCC', status: 'Active', validFrom: '2026-01-01', validTo: '2099-01-01' },
    { id: 'c2', commonName: 'KIOSK-OLD', thumbprint: 'DDEEFF', status: 'Revoked', validFrom: '2026-01-01', validTo: '2099-01-01' },
    { id: 'c3', commonName: 'ROOT', thumbprint: '112233', status: 'Active', validFrom: '2026-01-01', validTo: '2099-01-01', isRootCa: true },
  ],
}))

import { matchTerminalToken, matchClientCertificate, isExplicitDevMode } from '~~/server/utils/terminalAuth'

const ORIGINAL = { ...process.env }
afterEach(() => { process.env = { ...ORIGINAL } })

function prodLike() {
  delete process.env.VITEST
  delete process.env.HEIMDALL_ENABLE_DEV
  process.env.NODE_ENV = 'development' // non-production NODE_ENV alone must NOT enable dev bypasses
}

describe('terminal workstation authentication', () => {
  it('no longer accepts arbitrary 4+ character tokens', () => {
    prodLike()
    expect(matchTerminalToken('abcd')).toBeNull()
    expect(matchTerminalToken('anything-long-enough-here')).toBeNull()
  })

  it('rejects the demo kiosk tokens outside explicit dev mode', () => {
    prodLike()
    expect(isExplicitDevMode()).toBe(false)
    expect(matchTerminalToken('kiosk-token-op10')).toBeNull()
    expect(matchTerminalToken('test-token')).toBeNull()
  })

  it('accepts demo kiosk tokens only with HEIMDALL_ENABLE_DEV=true', () => {
    prodLike()
    process.env.HEIMDALL_ENABLE_DEV = 'true'
    expect(matchTerminalToken('kiosk-token-op10')?.method).toBe('dev-token')
  })

  it('accepts configured workstation tokens (min 16 chars)', () => {
    prodLike()
    process.env.HEIMDALL_TERMINAL_TOKENS = 'short, ws-op10-7f3e9c2a1b4d5e6f '
    expect(matchTerminalToken('ws-op10-7f3e9c2a1b4d5e6f')?.method).toBe('workstation-token')
    expect(matchTerminalToken('short')).toBeNull()
    expect(matchTerminalToken('ws-op10-7f3e9c2a1b4d5e6X')).toBeNull()
  })

  it('ignores the client-cert header unless the proxy is explicitly trusted', () => {
    prodLike()
    expect(matchClientCertificate('AABBCC')).toBeNull()
  })

  it('accepts only active, non-root registry thumbprints when trusted', () => {
    prodLike()
    process.env.HEIMDALL_TRUST_CLIENT_CERT_HEADER = 'true'
    expect(matchClientCertificate('aa:bb:cc')?.principal).toBe('cert:KIOSK-OP10')
    expect(matchClientCertificate('DDEEFF')).toBeNull()
    expect(matchClientCertificate('112233')).toBeNull()
    expect(matchClientCertificate('999999')).toBeNull()
  })
})
