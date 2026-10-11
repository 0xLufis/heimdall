import { defineEventHandler, readBody } from 'h3'
import { disableUserMfa } from '../../../utils/userMfaStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event).catch(() => ({}))
  const userId = body?.userId || ((event.context as any).user?.id) || 'usr-default'
  return disableUserMfa(userId)
})
