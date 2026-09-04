import { createRule } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.technicianName || !body.targetId) {
    throw createError({ statusCode: 400, message: 'technicianName and targetId are required' })
  }
  const created = createRule(body)
  setResponseStatus(event, 201)
  return created
})
