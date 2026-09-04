import { getRootCertificate } from '../../utils/pkiStore'
export default defineEventHandler(() => getRootCertificate())
