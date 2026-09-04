import { createRule } from '../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.technicianName || (!body.targetId && !body.target)) {
    throw createError({ statusCode: 400, message: 'technicianName and targetId are required' })
  }

  try {
    const created = createRule(body)
    setResponseStatus(event, 201)
    return created
  } catch (err: any) {
    throw createError({
      statusCode: err.statusCode || 400,
      message: err.message || 'Failed to create dedication rule'
    })
  }
})
