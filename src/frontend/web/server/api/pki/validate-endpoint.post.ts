import { proxyToBackend } from '../../utils/backendProxy'

/**
 * Validates a certificate against the backend trust store (chain, validity, revocation) and maps it
 * to a ClientPc / user. Body: `{ certificatePem, chainPem? }`.
 */
export default defineEventHandler(event => proxyToBackend(event, 'v1/certificatemanagement', 'validate', 'PKI'))
