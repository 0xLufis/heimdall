import { defineEventHandler, readBody, createError } from 'h3'
import { verifyAndEnableMfa } from '../../../utils/userMfaStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event).catch(() => ({}))
  const userId = body?.userId || ((event.context as any).user?.id) || 'usr-default'
  const code = body?.code
  if (!code) {
    throw createError({ statusCode: 400, message: 'Verification code is required.' })
  }
  try {
    return verifyAndEnableMfa(userId, code)
  } catch (err: any) {
    throw createError({ statusCode: 400, message: err.message || 'Verification failed.' })
  }
})
