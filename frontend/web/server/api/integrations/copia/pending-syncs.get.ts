import { defineEventHandler } from 'h3'

export default defineEventHandler(async (event) => {
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const records = await $fetch<any[]>(`${backendBase}/api/v1/copia/pending-syncs`, {
      headers: event.headers as any
    })
    return {
      pendingSyncs: records || [],
      total: records?.length || 0
    }
  } catch (err) {
    // Return sample pending notification if backend is offline/mock
    return {
      pendingSyncs: [
        {
          syncId: 'sync-demo-op10',
          deviceId: 'STATION-OP10-01',
          localRepositoryPath: 'repos/station-op10',
          branch: 'main',
          localCommitHash: 'loc-7f9a2b1c',
          serviceUserAuthor: 'heimdall-probe <heimdall-probe@internal>',
          modifiedFiles: ['POUs/MAIN.TcPOU', 'GVLs/GVL_MachineState.TcGVL'],
          createdAt: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
          notificationMessage: "Local PLC logic changes on 'STATION-OP10-01' tracked by 'heimdall-probe' in commit loc-7f9a2b1c. EULA Compliance Notice: To prevent user pooling violations, an engineer-specific Copia API key is required to pull/push this repository to Copia Cloud.",
          status: 'PendingUserAuthorization'
        }
      ],
      total: 1
    }
  }
})
