import { resolveAbsence } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing absence id' })
  const success = resolveAbsence(id)
  if (!success) throw createError({ statusCode: 404, message: 'Absence not found' })
  return { success: true }
})
