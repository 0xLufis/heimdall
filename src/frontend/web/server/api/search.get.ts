import { proxyToBackend } from '../utils/backendProxy'

/**
 * Global search: `GET /api/search?q=&kinds=&limit=` -> backend `GET /api/v1/search`.
 *
 * The backend runs the query through its tenant-scoped database context, so a caller only gets
 * rows of the organization of their session (the bearer token and tenant header are derived
 * from the Better-Auth session in `backendProxy.ts`; nothing client-supplied is forwarded).
 */
export default defineEventHandler((event) => proxyToBackend(event, 'v1', 'search', 'Search'))
