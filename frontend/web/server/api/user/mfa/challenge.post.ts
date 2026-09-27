import { defineEventHandler, readBody, createError } from 'h3'
import { verifyMfaChallenge } from '../../../utils/userMfaStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event).catch(() => ({}))
  const userId = body?.userId || ((event.context as any).user?.id) || 'usr-default'
  const code = body?.code
  if (!code) {
    throw createError({ statusCode: 400, message: 'Authentication code is required.' })
  }
  try {
    return verifyMfaChallenge(userId, code)
  } catch (err: any) {
    throw createError({ statusCode: 400, message: err.message || 'Challenge failed.' })
  }
})
