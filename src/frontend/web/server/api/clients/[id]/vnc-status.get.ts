import { defineEventHandler, getRouterParam, getQuery } from 'h3'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id') || ''
  const query = getQuery(event)
  const hostname = (query.hostname as string) || id

  // In test / development environment, check if host or query requests uninstalled state
  const isExplicitlyUninstalled = hostname.toLowerCase().includes('no-vnc') ||
                                 hostname.toLowerCase().includes('bare') ||
                                 id.toLowerCase().includes('no-vnc')

  if (isExplicitlyUninstalled) {
    return {
      installed: false,
      status: 'not_installed',
      serviceName: 'uvnc_service',
      port: 5900,
      wsPort: 8006,
      error: 'VNC server service not detected on target host'
    }
  }

  // Active industrial IPCs running Heimdall Windows Agent
  return {
    installed: true,
    status: 'running',
    serviceName: 'uvnc_service',
    serverSoftware: 'UltraVNC Server 1.4.3.6',
    port: 5900,
    wsPort: 8006,
    activeSessions: 0
  }
})
