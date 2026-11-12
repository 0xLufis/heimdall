import { defineEventHandler } from 'h3'
import { proxyToBackend } from '../../utils/backendProxy'

/**
 * `GET /api/analytics/fleet-stations[?windowDays=&includeDecommissioned=]`
 * → `GET /api/v1/analytics/fleet-stations`.
 *
 * One row per machine the caller's organization can see, with the dimensions the fleet analytics
 * page groups and filters by (line, sub-line, project, business unit, machine type, organization),
 * the latest telemetry snapshot, ticket figures for the look-back window and controller
 * connectivity. The backend owns every figure; this route only forwards the session token and the
 * session's organization (see `backendProxy.ts`), so nothing is synthesized here.
 */
export default defineEventHandler(event => proxyToBackend(event, 'v1/analytics', 'fleet-stations', 'AnalyticsProxy'))
