import { toggleSimulatedOoo, getTeamsOooStatuses } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.userId && !body.id) {
    throw createError({ statusCode: 400, message: 'userId is required' })
  }
  const uid = body.userId || body.id
  toggleSimulatedOoo(uid, Boolean(body.isOutOfOffice))
  return {
    devMode: true,
    statuses: getTeamsOooStatuses()
  }
})
