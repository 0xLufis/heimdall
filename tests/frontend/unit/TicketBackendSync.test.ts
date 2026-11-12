import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

// The session behind the request: bare token plus the active organisation.
const getSession = vi.fn()
vi.mock('~~/server/utils/auth', () => ({ auth: { api: { getSession: (...args: unknown[]) => getSession(...args) } } }))

import { backendAuthHeaders } from '~~/server/utils/backendProxy'
import patchTicketHandler from '~~/server/api/tickets/[id].patch'
import { escalationHandoverStateNumber } from '~~/server/utils/ticketsStore'

const requestEvent = (headers: Record<string, string>, extra: Record<string, unknown> = {}) => ({
  method: 'PATCH',
  headers: new Headers(headers),
  context: {},
  node: { req: { method: 'PATCH', url: '/api/tickets/x', headers } },
  ...extra
}) as any

describe('BFF -> backend calls run as the caller', () => {
  beforeEach(() => {
    getSession.mockReset()
    getSession.mockResolvedValue({ session: { token: 'tok-123', activeOrganizationId: 'org-line-01' }, user: { id: 'u1', name: 'Tess Tech' } })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('derives the bearer and tenant from the session, never from client headers', async () => {
    const headers = await backendAuthHeaders(requestEvent({ cookie: 'better-auth.session_token=tok-123.signature', 'x-organization-id': 'org-somebody-else' }))
    expect(headers).toEqual({ Authorization: 'Bearer tok-123', 'X-Organization-Id': 'org-line-01' })
  })

  it('maps escalation handover states to the backend enum numbers', () => {
    expect(['None', 'Notification', 'HandOff', 'ParallelWork', 'HandedOffToField'].map(v => escalationHandoverStateNumber(v))).toEqual([0, 1, 2, 3, 4])
    expect(escalationHandoverStateNumber(2)).toBe(2)
    expect(escalationHandoverStateNumber(null, 1)).toBe(1)
    expect(escalationHandoverStateNumber('Bogus', 0)).toBe(0)
    expect(escalationHandoverStateNumber(9, 0)).toBe(0)
  })

  it('sends nothing without a session token, so the backend rejects instead of guessing', async () => {
    expect(await backendAuthHeaders(requestEvent({}))).toEqual({})
    expect(await backendAuthHeaders({ node: { req: {} } } as any)).toEqual({})
    expect(getSession).not.toHaveBeenCalled()
  })

  it('syncs a ticket edit to the backend with the caller\'s session', async () => {
    const calls: Array<{ url: string; method: string; headers: Record<string, string>; body?: any }> = []
    vi.stubGlobal('$fetch', vi.fn(async (url: string, opts: any = {}) => {
      calls.push({ url, method: opts.method || 'GET', headers: opts.headers || {}, body: opts.body })
      if (!opts.method) throw Object.assign(new Error('not found'), { statusCode: 404 }) // ticket lookup
      return {}
    }))
    const id = '0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d'
    const result = await patchTicketHandler(requestEvent(
      { cookie: 'better-auth.session_token=tok-123.signature' },
      { context: { params: { id } }, _requestBody: JSON.stringify({ status: 'InProgress', isEscalated: true, escalationReason: 'Line stop' }) }
    ))
    expect(result.success).toBe(true)

    // The sync runs after the response.
    await vi.waitFor(() => expect(calls.some(c => c.method === 'PUT')).toBe(true))
    const writes = calls.filter(c => c.method !== 'GET').map(c => `${c.method} ${c.url.replace(/^.*\/api\/v1\/tickets\//, '')}`)
    expect(writes).toEqual([`POST ${id}/escalate`, `PATCH ${id}/status`, `PUT ${id}`])
    expect(calls.filter(c => c.method === 'GET').length).toBeGreaterThan(0)
    // The backend's EscalationHandoverState is a numeric enum.
    expect(calls.find(c => c.url.endsWith('/escalate'))!.body.handoverState).toBe(1)
    expect(calls.find(c => c.method === 'PUT')!.body.escalationHandoverState).toBe(0)
    for (const call of calls) {
      expect(call.headers.Authorization, `${call.method} ${call.url}`).toBe('Bearer tok-123')
      expect(call.headers['X-Organization-Id'], `${call.method} ${call.url}`).toBe('org-line-01')
    }
  })
})
