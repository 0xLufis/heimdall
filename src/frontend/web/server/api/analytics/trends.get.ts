import { defineEventHandler } from 'h3'
import { proxyToBackend } from '../../utils/backendProxy'

/**
 * `GET /api/analytics/trends` → `GET /api/v1/analytics/trends` (tenant-scoped, computed from stored
 * telemetry snapshots and maintenance tickets; null/empty when no data exists — see
 * docs/api/API_REFERENCE.md §1.9). Previously returned hardcoded dev values.
 */
export default defineEventHandler(event => proxyToBackend(event, 'v1/analytics', 'trends', 'AnalyticsProxy'))
