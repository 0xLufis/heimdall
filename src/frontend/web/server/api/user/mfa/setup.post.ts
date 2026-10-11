import { defineEventHandler, readBody } from 'h3'
import { initiateMfaSetup } from '../../../utils/userMfaStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event).catch(() => ({}))
  const userId = body?.userId || ((event.context as any).user?.id) || 'usr-default'
  const email = body?.email || ((event.context as any).user?.email) || 'user@heimdall.dev'
  return initiateMfaSetup(userId, email)
})
