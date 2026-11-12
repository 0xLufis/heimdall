import { describe, it, expect, vi, afterEach } from 'vitest'

const state = vi.hoisted(() => ({ sessionUser: undefined as any }))

vi.mock('~~/server/utils/authHelper', () => ({
  resolveSessionUser: vi.fn(async () => state.sessionUser)
}))
import {
  matchTerminalToken,
  requireTerminalAuth,
  assertTerminalStationAccess
} from '~~/server/utils/terminalAuth'
// One exporting module per name: Nuxt auto-imports server/utils and warns on duplicates.
import { isExplicitDevMode, isTerminalAuthBypassEnabled } from '~~/server/utils/devMode'
import {
  isTrustedProxyAddress,
  parseForwardedCertificate,
  clearWorkstationIdentityCache
} from '~~/server/utils/terminalCertAuth'

// Self-signed fixture (validity 2026-2126): SAN DNS:IPC-L03-CONVEYOR-MAIN, URI:urn:heimdall:machine:HW-IPC-L03-CONVEYOR-MAIN.
// Chain validation and mapping happen in the backend PKI (tested in PkiTrustAndMappingTests); here $fetch is mocked.
const WORKSTATION_PEM = `-----BEGIN CERTIFICATE-----
MIIDdjCCAl6gAwIBAgIUCKO+Ske7BESvCVTnBfzXIzrlbRAwDQYJKoZIhvcNAQEL
BQAwIDEeMBwGA1UEAwwVSVBDLUwwMy1DT05WRVlPUi1NQUlOMCAXDTI2MTExMjAy
MjYyMloYDzIxMjYxMDE5MDIyNjIyWjAgMR4wHAYDVQQDDBVJUEMtTDAzLUNPTlZF
WU9SLU1BSU4wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCX95eU6oad
0DLE3I2jT0T8864sBpVjt5raqH4IkjaS5Kk4ehaPkA5y/fZlc2VZc0pCdjYkWqFQ
8Oa53GXCSkrduTQLXwzaXnupJaz1dKM1wRDsOGlMJXNKLpKmqgTHkeWJH5fntevR
jAETqfCwMuBVSL3geAEZVoEjKhzXjHXynyLs+iAoJqaiIGVxMUk0b+ZzMkNI/LHd
fKG/K6T91qAiCHk9Efout5ulbBdrbNKVUH1R3E4m7xpVEkHNY4l6Cz7tQzn7Ykxk
GTrcNDJHHk7FFB0+bCRBoGuUMCKHvmUdj88X9oX5C5c/ZRIE13ThaP8IVoLoSaLC
sBcMaRtOWh3ZAgMBAAGjgaUwgaIwHQYDVR0OBBYEFIXdI4jfcZyw5/xg7YOJEN17
bXz6MB8GA1UdIwQYMBaAFIXdI4jfcZyw5/xg7YOJEN17bXz6MA8GA1UdEwEB/wQF
MAMBAf8wTwYDVR0RBEgwRoIVSVBDLUwwMy1DT05WRVlPUi1NQUlOhi11cm46aGVp
bWRhbGw6bWFjaGluZTpIVy1JUEMtTDAzLUNPTlZFWU9SLU1BSU4wDQYJKoZIhvcN
AQELBQADggEBABmHzw66ce3/NZRKHF7CU3Pe3vwF5BqxncTTPMp4TZ76c7LS+tV5
ofXHae3omphEmWE86T14p8mDbGa/JpezBR2mS4pcnsk0K8LjFmz06+9vIfiSEiny
I4Mj4P6i1K8niVleA0IvIOmqpFsN7HnU/C3UD67LiGxPYtBlxCeNu+oWlOZCX1Jc
LI7J18jNB+DhqficBrACguWaKdXC/u0dfgOChOLlE2x0nA2mslQmfGt7LigTBqNu
mplY6+d2l+xM6wynbR0OhfVs2QHZwzmBzycBzfZhfB1UleDRysoDPnFnzfwlnuPO
/DRl9e1CHE+q8VgVAM+syn2/O4Thc9Asl0E=
-----END CERTIFICATE-----`

const ORIGINAL = { ...process.env }
afterEach(() => {
  process.env = { ...ORIGINAL }
  state.sessionUser = undefined
  clearWorkstationIdentityCache()
  delete (globalThis as any).$fetch
})

function prodLike() {
  delete process.env.VITEST
  delete process.env.HEIMDALL_ENABLE_DEV
  delete process.env.HEIMDALL_TERMINAL_TOKENS
  delete process.env.HEIMDALL_TRUST_CLIENT_CERT_HEADER
  delete process.env.HEIMDALL_TRUSTED_PROXIES
  delete process.env.HEIMDALL_CLIENT_CERT_HEADER
  process.env.NODE_ENV = 'development' // non-production NODE_ENV alone must NOT enable dev bypasses
}

function kioskEvent(headers: Record<string, string> = {}, remoteAddress = '10.0.5.20', path = '/api/terminal/machine/LINE-03-OP010') {
  return {
    context: {},
    headers: new Headers(headers),
    node: { req: { headers, method: 'GET', socket: { remoteAddress } }, res: {} },
    path
  } as any
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
})

describe('terminal sessions and the dev / demo bypass', () => {
  it('accepts a signed-in session, including a development SSO simulation session', async () => {
    prodLike()
    // resolveSessionUser is the resolution getAuthenticatedUser uses (Better-Auth + sso_ simulation rows).
    state.sessionUser = { id: 'usr-admin-primary', role: 'system_admin' }
    const result = await requireTerminalAuth(kioskEvent({ cookie: 'better-auth.session_token=sso_abc' }))
    expect(result).toMatchObject({ method: 'session', principal: 'user:usr-admin-primary' })
  })

  it('answers 401 without credentials outside dev mode', async () => {
    prodLike()
    await expect(requireTerminalAuth(kioskEvent())).rejects.toMatchObject({ statusCode: 401 })
  })

  it('lets anonymous kiosks through with HEIMDALL_ENABLE_DEV=true', async () => {
    prodLike()
    process.env.HEIMDALL_ENABLE_DEV = 'true'
    expect(isTerminalAuthBypassEnabled()).toBe(true)
    const result = await requireTerminalAuth(kioskEvent())
    expect(result.method).toBe('dev-bypass')
  })

  it('never bypasses under NODE_ENV=production, whatever else is set', async () => {
    prodLike()
    process.env.HEIMDALL_ENABLE_DEV = 'true'
    process.env.NODE_ENV = 'production'
    expect(isTerminalAuthBypassEnabled()).toBe(false)
    await expect(requireTerminalAuth(kioskEvent())).rejects.toMatchObject({ statusCode: 401 })
  })

  it('is not enabled by Vitest itself', () => {
    prodLike()
    process.env.VITEST = 'true'
    expect(isTerminalAuthBypassEnabled()).toBe(false)
  })
})

describe('terminal production path: workstation certificate from the trusted reverse proxy', () => {
  const certHeader = () => ({ 'x-client-cert': encodeURIComponent(WORKSTATION_PEM + '\n') })

  function trustProxy() {
    prodLike()
    process.env.HEIMDALL_TRUST_CLIENT_CERT_HEADER = 'true'
    process.env.HEIMDALL_TRUSTED_PROXIES = '127.0.0.1, 10.0.5.0/24'
  }

  function backendAnswers(answer: any) {
    const fetchMock = vi.fn(async () => answer)
    ;(globalThis as any).$fetch = fetchMock
    return fetchMock
  }

  it('matches trusted proxies by exact address and IPv4 CIDR (IPv4-mapped IPv6 too)', () => {
    trustProxy()
    expect(isTrustedProxyAddress('127.0.0.1')).toBe(true)
    expect(isTrustedProxyAddress('::ffff:10.0.5.77')).toBe(true)
    expect(isTrustedProxyAddress('10.0.6.1')).toBe(false)
    expect(isTrustedProxyAddress(undefined)).toBe(false)
  })

  it('parses URL-encoded PEM, single-line PEM and bare base64 DER', () => {
    const encoded = parseForwardedCertificate(encodeURIComponent(WORKSTATION_PEM))
    expect(encoded?.x509.subject).toContain('IPC-L03-CONVEYOR-MAIN')
    expect(parseForwardedCertificate(WORKSTATION_PEM.replace(/\n/g, ' '))?.x509.fingerprint256).toBe(encoded?.x509.fingerprint256)
    const der = WORKSTATION_PEM.replace(/-----(BEGIN|END) CERTIFICATE-----/g, '').replace(/\s+/g, '')
    expect(parseForwardedCertificate(der)?.x509.fingerprint256).toBe(encoded?.x509.fingerprint256)
    expect(parseForwardedCertificate('not a certificate')).toBeNull()
  })

  it('ignores the header unless HEIMDALL_TRUST_CLIENT_CERT_HEADER=true', async () => {
    prodLike()
    process.env.HEIMDALL_TRUSTED_PROXIES = '10.0.5.0/24'
    const fetchMock = backendAnswers({ valid: true, clientPcId: 'pc-1', stationKeys: ['LINE-03-OP010'] })
    await expect(requireTerminalAuth(kioskEvent(certHeader()))).rejects.toMatchObject({ statusCode: 401 })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('ignores the header from a peer that is not a trusted proxy', async () => {
    trustProxy()
    const fetchMock = backendAnswers({ valid: true, clientPcId: 'pc-1', stationKeys: ['LINE-03-OP010'] })
    await expect(requireTerminalAuth(kioskEvent(certHeader(), '192.168.1.50'))).rejects.toMatchObject({ statusCode: 401 })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('accepts a certificate the backend validates and maps, scoped to the bound stations', async () => {
    trustProxy()
    const fetchMock = backendAnswers({ valid: true, status: 'Valid', clientPcId: 'pc-1', stationKeys: ['LINE-03-OP010', 'GUID-OP010'] })
    const auth = await requireTerminalAuth(kioskEvent(certHeader()))
    expect(auth).toMatchObject({ method: 'client-certificate', clientPcId: 'pc-1' })
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/api/v1/pki/workstation-identity'),
      expect.objectContaining({ method: 'POST', body: { certificatePem: expect.stringContaining('BEGIN CERTIFICATE') } }))

    expect(() => assertTerminalStationAccess(auth, 'line-03-op010')).not.toThrow()
    expect(() => assertTerminalStationAccess(auth, 'unknown-id', 'GUID-OP010')).not.toThrow()
    expect(() => assertTerminalStationAccess(auth, 'LINE-09-OP900', null)).toThrow(expect.objectContaining({ statusCode: 403 }))

    // Cached per certificate fingerprint.
    await requireTerminalAuth(kioskEvent(certHeader()))
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('rejects a certificate the backend does not validate (revoked, untrusted, unmapped)', async () => {
    trustProxy()
    backendAnswers({ valid: false, status: 'Revoked', reason: 'The certificate has been revoked in Heimdall.' })
    await expect(requireTerminalAuth(kioskEvent(certHeader()))).rejects.toMatchObject({ statusCode: 401, statusMessage: expect.stringContaining('Revoked') })
  })

  it('does not station-scope sessions or workstation tokens', () => {
    expect(() => assertTerminalStationAccess({ method: 'session', principal: 'user:u1' }, 'ANY')).not.toThrow()
    expect(() => assertTerminalStationAccess({ method: 'workstation-token', principal: 'workstation:ws-o…' }, 'ANY')).not.toThrow()
  })
})
