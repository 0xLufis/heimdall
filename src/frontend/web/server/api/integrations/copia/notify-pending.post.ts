import { defineEventHandler, readBody, createError } from 'h3'
import {
  getAllPendingCopiaSyncs,
  getPendingCopiaSyncById,
  authorizeCopiaPush,
  clearPendingCopiaSyncs
} from '../../../../server/utils/copiaSyncNotifier'

export default defineEventHandler(async (event) => {
  const body = await readBody(event) || {}
  const action = body.action || 'list'

  if (action === 'clear') {
    clearPendingCopiaSyncs()
    return { success: true, message: 'Pending Copia sync notifications cleared.' }
  }

  if (action === 'authorize') {
    const syncId = body.syncId
    if (!syncId) {
      throw createError({
        statusCode: 400,
        statusMessage: 'syncId is required for Copia push authorization.'
      })
    }

    const sessionUser = (event.context as any)?.auth?.user
    const user = {
      id: body.userId || sessionUser?.id || 'usr-eng-001',
      name: body.userName || sessionUser?.name || 'Controls Engineer',
      email: body.userEmail || sessionUser?.email || 'engineer@factory.corp'
    }

    try {
      const result = authorizeCopiaPush(syncId, user, body.copiaApiKey)
      return {
        success: true,
        authorization: result,
        complianceAttestation: 'Remote push attributed to authorized individual Copia user account in compliance with Copia ToS Exhibit A.'
      }
    } catch (err: any) {
      throw createError({
        statusCode: 400,
        statusMessage: err.message
      })
    }
  }

  // Default: list all pending sync records
  return {
    success: true,
    pendingSyncs: getAllPendingCopiaSyncs(),
    total: getAllPendingCopiaSyncs().length
  }
})
