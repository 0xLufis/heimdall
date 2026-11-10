import { describe, it, expect, vi } from 'vitest'

// backendProxy imports the Better-Auth instance; the sanitizer under test is pure.
vi.mock('~~/server/utils/auth', () => ({ auth: { api: { getSession: vi.fn() } } }))

import { sanitizeProxyPath } from '~~/server/utils/backendProxy'

describe('sanitizeProxyPath', () => {
  it('passes through ordinary nested paths', () => {
    expect(sanitizeProxyPath('scripts/42/execute')).toBe('scripts/42/execute')
    expect(sanitizeProxyPath('templates/3f2b-1c')).toBe('templates/3f2b-1c')
    expect(sanitizeProxyPath('')).toBe('')
    expect(sanitizeProxyPath(undefined)).toBe('')
  })

  it('collapses duplicate slashes', () => {
    expect(sanitizeProxyPath('a//b/')).toBe('a/b')
  })

  it.each([
    '../admin',
    'scripts/../../users',
    '..%2F..%2Fadmin',
    '%2e%2e/secret',
    'a/%2e/b',
    'a\\..\\b',
    'a%5c..%5cb',
    'a/%00/b',
    '%252e%252e/x',
    'a/<script>',
    '%E0%A4%A',
  ])('rejects traversal / unsafe input %s', (input) => {
    expect(sanitizeProxyPath(input)).toBeNull()
  })
})
