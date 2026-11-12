import { proxyToBackend } from '../../utils/backendProxy'

// PKI state lives in the backend only (trusted_ca_certificates / client_certificates).
export default defineEventHandler(event => proxyToBackend(event, 'v1/certificatemanagement', 'root-ca', 'PKI'))
