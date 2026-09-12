import { describe, it, expect } from 'vitest'
import studioConfig from '~/../studio.config'
import catchAllHandler from '~/../server/routes/admin/studio/[...all]'
import indexHandler from '~/../server/routes/admin/studio/index'
import { navMenu } from '~/constants/menus'
import { authEvents, hbSchema } from '~/../server/database/drizzle/schema'
import { createDrizzleEventsProvider } from '~/../server/utils/studioEventsProvider'

describe('Better Auth Studio Integration (/admin/studio)', () => {
  it('has correct Studio configuration settings', () => {
    expect(studioConfig.basePath).toBe('/admin/studio')
    expect(studioConfig.metadata?.title).toBe('Heimdall Identity Studio')
    expect(studioConfig.metadata?.theme).toBe('dark')
    expect(studioConfig.access?.roles).toContain('system_admin')
    expect(studioConfig.access?.roles).toContain('heimdall_admin')
    expect(studioConfig.access?.roles).toContain('admin')
    expect(studioConfig.access?.roles).toContain('it_admin')
    expect(studioConfig.events?.enabled).toBe(true)
    expect(studioConfig.events?.tableName).toBe('auth_events')
    expect(studioConfig.events?.schema).toBe('auth')
    expect(studioConfig.events?.clientType).toBe('drizzle')
    expect(studioConfig.events?.liveMarquee?.enabled).toBe(true)
    expect(studioConfig.events?.provider).toBeDefined()
  })

  it('exposes Identity Studio in the Administration sidebar navigation menu with external blank target', () => {
    const adminSection = navMenu.find((group) => group.heading === 'Administration')
    expect(adminSection).toBeDefined()
    const studioItem: any = adminSection?.items.find((item: any) => item.link === '/admin/studio')
    expect(studioItem).toBeDefined()
    expect(studioItem?.title).toBe('Identity Studio')
    expect(studioItem?.icon).toBe('i-lucide-fingerprint')
    expect(studioItem?.external).toBe(true)
    expect(studioItem?.target).toBe('_blank')
  })

  it('defines authEvents table in Drizzle schema under the auth schema', () => {
    expect(authEvents).toBeDefined()
    expect(hbSchema.schemaName).toBe('auth')
  })

  it('serves Studio SPA on GET /admin/studio via index handler with rewritten asset paths', async () => {
    const mockEvent: any = {
      method: 'GET',
      headers: {
        host: 'localhost:3000',
      },
      node: {
        req: {
          url: '/admin/studio',
          socket: { encrypted: false },
        },
      },
    }

    const response = await indexHandler(mockEvent)
    expect(response.status).toBe(200)
    expect(response.headers.get('content-type')).toContain('text/html')

    const html = await response.text()
    expect(html).toContain('/admin/studio/assets/')
    expect(html).toContain('Heimdall Identity Studio')
    expect(html).toContain('"basePath":"/admin/studio"')
  })

  it('serves Studio SPA on catch-all routes like /admin/studio/login', async () => {
    const mockEvent: any = {
      method: 'GET',
      headers: {
        host: 'localhost:3000',
      },
      node: {
        req: {
          url: '/admin/studio/login',
          socket: { encrypted: false },
        },
      },
    }

    const response = await catchAllHandler(mockEvent)
    expect(response.status).toBe(200)
    expect(response.headers.get('content-type')).toContain('text/html')

    const html = await response.text()
    expect(html).toContain('Heimdall Identity Studio')
  })

  it('responds to public health check at /admin/studio/api/health', async () => {
    const mockEvent: any = {
      method: 'GET',
      headers: {
        host: 'localhost:3000',
        accept: 'application/json',
      },
      node: {
        req: {
          url: '/admin/studio/api/health',
          socket: { encrypted: false },
        },
      },
    }

    const response = await catchAllHandler(mockEvent)
    expect(response.status).toBe(200)
    const json = await response.json()
    expect(json.status).toBe('ok')
    expect(json.system).toBeDefined()
  })

  it('rejects unauthenticated requests to protected Studio endpoints with 401', async () => {
    const mockConfigEvent: any = {
      method: 'GET',
      headers: {
        host: 'localhost:3000',
        accept: 'application/json',
      },
      node: {
        req: {
          url: '/admin/studio/api/config',
          socket: { encrypted: false },
        },
      },
    }

    const configResponse = await catchAllHandler(mockConfigEvent)
    expect(configResponse.status).toBe(401)
    const configJson = await configResponse.json()
    expect(configJson.error).toBe('Unauthorized')

    const mockEventsEvent: any = {
      method: 'GET',
      headers: {
        host: 'localhost:3000',
        accept: 'application/json',
      },
      node: {
        req: {
          url: '/admin/studio/api/events/status',
          socket: { encrypted: false },
        },
      },
    }

    const eventsResponse = await catchAllHandler(mockEventsEvent)
    expect(eventsResponse.status).toBe(401)
  })

  it('instantiates Drizzle events provider supporting ingest, query, count and healthCheck', async () => {
    const provider = createDrizzleEventsProvider()
    expect(provider).toBeDefined()
    expect(typeof provider.ingest).toBe('function')
    expect(typeof provider.query).toBe('function')
    expect(typeof provider.count).toBe('function')
    expect(typeof provider.healthCheck).toBe('function')

    const isHealthy = await provider.healthCheck()
    expect(isHealthy).toBe(true)

    const testEvent = {
      id: crypto.randomUUID(),
      type: 'test.event',
      timestamp: new Date(),
      status: 'success' as const,
      userId: 'test-user-id',
      source: 'app' as const,
      display: {
        message: 'Unit test event verification',
        severity: 'info' as const,
      },
    }

    await provider.ingest(testEvent)
    const queryResult = await provider.query({ type: 'test.event', limit: 5 })
    expect(queryResult.events).toBeDefined()
    expect(Array.isArray(queryResult.events)).toBe(true)
  })
})
