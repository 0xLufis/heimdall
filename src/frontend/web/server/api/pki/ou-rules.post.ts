import { saveOuRule } from '../../utils/pkiStore'
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return saveOuRule(body)
})
