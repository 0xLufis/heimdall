import { getUserCopiaKey } from './copiaUserKeyStore'

export interface PendingCopiaSyncRecord {
  syncId: string
  deviceId: string
  hostname: string
  localRepositoryPath: string
  branch: string
  localCommitHash: string
  serviceUserAuthor: string
  modifiedFiles: string[]
  diffPreview: {
    addedLines: number
    removedLines: number
    changedBlocks: Array<{ file: string; changeType: 'added' | 'modified' | 'deleted'; diffSnippet: string }>
  }
  notificationMessage: string
  createdAt: string
  status: 'PendingUserAuthorization' | 'ApprovedAndPushed' | 'Rejected'
  authorizedByUserId?: string
  authorizedByUserName?: string
  authorizedAt?: string
  cloudCommitUrl?: string
}

const pendingSyncRecords = new Map<string, PendingCopiaSyncRecord>()

// Pre-populate with standard sample record
const initialRecord: PendingCopiaSyncRecord = {
  syncId: 'sync-demo-op10',
  deviceId: 'STATION-OP10-01',
  hostname: 'IPC-L1-01',
  localRepositoryPath: 'repos/station-op10',
  branch: 'controller/ipc-l1-01-running',
  localCommitHash: 'loc-7f9a2b1c',
  serviceUserAuthor: 'heimdall-probe <heimdall-probe@internal>',
  modifiedFiles: ['POUs/MAIN.TcPOU', 'GVLs/GVL_MachineState.TcGVL'],
  diffPreview: {
    addedLines: 14,
    removedLines: 3,
    changedBlocks: [
      {
        file: 'POUs/MAIN.TcPOU',
        changeType: 'modified',
        diffSnippet: '@@ -42,3 +42,5 @@\n+ // Infeed sensor timeout debounce\n+ tDebounce(IN := bPartInfeedSensor, PT := T#250MS);'
      }
    ]
  },
  createdAt: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
  notificationMessage: "Local PLC logic changes on 'STATION-OP10-01' tracked by 'heimdall-probe' in commit loc-7f9a2b1c. EULA Compliance Notice: To prevent user pooling violations, an engineer-specific Copia API key is required to pull/push this repository to Copia Cloud.",
  status: 'PendingUserAuthorization'
}

pendingSyncRecords.set(initialRecord.syncId, initialRecord)

/**
 * Creates and stages a pending Copia sync record, generating a user notification.
 */
export function stagePendingCopiaSync(payload: {
  deviceId: string
  hostname: string
  localCommitHash: string
  branch: string
  modifiedFiles: string[]
  diffPreview?: PendingCopiaSyncRecord['diffPreview']
}): PendingCopiaSyncRecord {
  const syncId = `sync-${Date.now()}-${Math.floor(1000 + Math.random() * 9000)}`
  const diff = payload.diffPreview || {
    addedLines: payload.modifiedFiles.length * 10,
    removedLines: 2,
    changedBlocks: payload.modifiedFiles.map(f => ({
      file: f,
      changeType: 'modified',
      diffSnippet: `@@ -1,5 +1,8 @@\n+ // Updated logic block for ${f}`
    }))
  }

  const record: PendingCopiaSyncRecord = {
    syncId,
    deviceId: payload.deviceId,
    hostname: payload.hostname,
    localRepositoryPath: `repos/${payload.hostname.toLowerCase()}`,
    branch: payload.branch,
    localCommitHash: payload.localCommitHash,
    serviceUserAuthor: 'heimdall-probe <heimdall-probe@internal>',
    modifiedFiles: payload.modifiedFiles,
    diffPreview: diff,
    notificationMessage: `Local PLC logic changes on '${payload.hostname}' tracked by 'heimdall-probe' in commit ${payload.localCommitHash}. EULA Compliance Notice: To prevent user pooling violations, an engineer-specific Copia API key is required to pull/push this repository to Copia Cloud.`,
    createdAt: new Date().toISOString(),
    status: 'PendingUserAuthorization'
  }

  pendingSyncRecords.set(syncId, record)
  return record
}

/**
 * Authorizes remote push of pending sync record using the engineer's personal Copia credentials.
 */
export function authorizeCopiaPush(
  syncId: string,
  user: { id: string; name: string; email?: string },
  copiaApiKeyOverride?: string
): { success: boolean; record: PendingCopiaSyncRecord; cloudCommitUrl: string } {
  const record = pendingSyncRecords.get(syncId)
  if (!record) {
    throw new Error(`Pending sync record '${syncId}' not found.`)
  }

  const apiKey = copiaApiKeyOverride || getUserCopiaKey(user.id)
  if (!apiKey) {
    throw new Error('No personal Copia API key found. Please configure your Copia PAT in Settings > Integrations.')
  }

  const trimmed = apiKey.trim().toLowerCase()
  if (trimmed === 'service-user' || trimmed === 'heimdall-probe' || trimmed.startsWith('shared-')) {
    throw new Error('EULA Compliance Violation: Generic service account keys or shared keys cannot be used for Copia Cloud synchronization.')
  }

  record.status = 'ApprovedAndPushed'
  record.authorizedByUserId = user.id
  record.authorizedByUserName = user.name
  record.authorizedAt = new Date().toISOString()
  record.cloudCommitUrl = `https://app.copia.io/factory-org/twincat-line1/commit/${record.localCommitHash}`

  pendingSyncRecords.set(syncId, record)

  return {
    success: true,
    record,
    cloudCommitUrl: record.cloudCommitUrl
  }
}

export function getAllPendingCopiaSyncs(): PendingCopiaSyncRecord[] {
  return Array.from(pendingSyncRecords.values())
}

export function getPendingCopiaSyncById(syncId: string): PendingCopiaSyncRecord | undefined {
  return pendingSyncRecords.get(syncId)
}

export function clearPendingCopiaSyncs(): void {
  pendingSyncRecords.clear()
}
