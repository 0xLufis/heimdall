import { updateMfaPolicy } from '../../utils/mfaPolicyStore'
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return updateMfaPolicy(body)
})
