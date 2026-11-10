import { defineEventHandler } from 'h3'
import { proxyToBackend } from '../../utils/backendProxy'

/**
 * GET /api/telemetry/station-kpis[?lineId=&stationId=]
 *
 * Station-level KPIs (OEE, yield, parts/hour, SLA flags) for the plant-map HUD.
 * Proxied to the .NET backend (`GET /api/v1/analytics/station-kpis`), which owns the data.
 * The shared proxy forwards the caller's session token and session-derived organization only,
 * so the backend's [Authorize] and tenant scoping apply. When the backend is unreachable the
 * proxy answers 502 and the UI renders its "no telemetry" state; nothing is synthesized here.
 */
export default defineEventHandler((event) => {
  return proxyToBackend(event, 'v1', 'analytics/station-kpis', 'StationKpis')
})
