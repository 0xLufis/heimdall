import { defineEventHandler, readBody, createError } from 'h3'
import { regenerateBackupCodes, getBackupCodes } from '../../../utils/userMfaStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event).catch(() => ({}))
  const userId = body?.userId || ((event.context as any).user?.id) || 'usr-default'
  const action = body?.action || 'get'
  try {
    if (action === 'regenerate') {
      const codes = regenerateBackupCodes(userId)
      return { success: true, backupCodes: codes }
    }
    const codes = getBackupCodes(userId)
    return { success: true, backupCodes: codes }
  } catch (err: any) {
    throw createError({ statusCode: 400, message: err.message || 'Failed to manage backup codes.' })
  }
})
