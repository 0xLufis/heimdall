import { importRootCertificate } from '../../../utils/pkiStore'
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return importRootCertificate(body.rawPem, body.profileName)
})
