import { defineEventHandler, getRequestURL } from 'h3'
import { assertModuleEnabled } from '~~/server/utils/featureFlags'

export default defineEventHandler((event) => {
  const url = getRequestURL(event)
  const pathname = url.pathname

  if (pathname.startsWith('/api/tickets') || pathname.startsWith('/api/delegations')) {
    assertModuleEnabled('ticketing')
  } else if (pathname.startsWith('/api/inventory') || pathname.startsWith('/api/parts')) {
    assertModuleEnabled('inventory')
  } else if (pathname.startsWith('/api/telemetry') || pathname.startsWith('/api/kpi')) {
    assertModuleEnabled('telemetry')
  }
})
