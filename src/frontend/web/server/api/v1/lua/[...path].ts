import { proxyToBackend } from '../../../utils/backendProxy'

/**
 * BFF → backend proxy for the Lua automation API: `/api/v1/lua/<path>` → `${BACKEND_API_URL}/api/v1/lua/<path>`.
 * Path traversal rejection, session-derived tenant header and retry policy live in `backendProxy.ts`.
 */
export default defineEventHandler((event) => {
  return proxyToBackend(event, 'v1/lua', event.context.params?.path, 'LuaProxy')
})
