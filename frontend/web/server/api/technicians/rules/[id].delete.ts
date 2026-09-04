import { deleteRule } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing rule id' })
  const success = deleteRule(id)
  if (!success) throw createError({ statusCode: 404, message: 'Rule not found' })
  return { success: true }
})
