import { proxyToBackend } from '../../../utils/backendProxy'

/**
 * BFF proxy for the generic template catalog. Path sanitization (no `..`/encoded separators),
 * session-derived tenant header and retry semantics live in the shared backend proxy.
 */
export default defineEventHandler(event =>
  proxyToBackend(event, 'v1/templates', event.context.params?.path, 'TemplatesProxy')
)
