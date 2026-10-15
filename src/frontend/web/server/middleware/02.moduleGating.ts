import { defineEventHandler, getRequestURL } from 'h3'
import { assertModuleEnabled } from '~~/server/utils/featureFlags'

export default defineEventHandler((event) => {
  const url = getRequestURL(event)
  const pathname = url.pathname.toLowerCase()

  if (
    pathname.startsWith('/api/tickets') ||
    pathname.startsWith('/api/delegations') ||
    pathname.startsWith('/api/terminal/ticket') ||
    pathname.startsWith('/api/technicians') ||
    pathname.includes('/proxy/v1/maintenanceticket') ||
    pathname.includes('/proxy/v1/tickets') ||
    pathname.includes('/proxy/v1/delegation')
  ) {
    assertModuleEnabled('ticketing')
  } else if (
    pathname.startsWith('/api/inventory') ||
    pathname.startsWith('/api/parts') ||
    pathname.includes('/proxy/v1/inventory') ||
    pathname.includes('/proxy/v1/component')
  ) {
    assertModuleEnabled('inventory')
  } else if (
    pathname.startsWith('/api/telemetry') ||
    pathname.startsWith('/api/kpi') ||
    pathname.includes('/proxy/v1/analytics') ||
    pathname.includes('/proxy/v1/diagnosticsnapshot')
  ) {
    assertModuleEnabled('telemetry')
  }
})
