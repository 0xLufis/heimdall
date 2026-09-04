import { createAbsence } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.technicianName || !body.reason) {
    throw createError({ statusCode: 400, message: 'technicianName and reason are required' })
  }
  const created = createAbsence(body)
  setResponseStatus(event, 201)
  return created
})
