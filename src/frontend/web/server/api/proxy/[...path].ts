import { proxyToBackend } from '../../utils/backendProxy'

/**
 * Generic BFF → backend proxy: `/api/proxy/<path>` → `${BACKEND_API_URL}/api/v1/<path>`.
 * Path validation, session-derived tenant header and retry policy live in `backendProxy.ts`.
 */
export default defineEventHandler((event) => {
  const path = event.context.params?.path || ''
  const subPath = path.startsWith('v1/') ? path.slice(3) : path
  return proxyToBackend(event, 'v1', subPath, 'Proxy')
})
