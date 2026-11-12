import { proxyToBackend } from '../../../utils/backendProxy'

export default defineEventHandler(event => proxyToBackend(event, 'v1/certificatemanagement', 'root-ca/import', 'PKI'))
