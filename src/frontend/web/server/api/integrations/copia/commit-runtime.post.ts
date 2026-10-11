import { defineEventHandler, readBody, createError } from 'h3'
import { commitControllerRuntime, type ControllerMetadata } from '../../../../server/utils/controllerBranchTracker'
import { stagePendingCopiaSync } from '../../../../server/utils/copiaSyncNotifier'
import { ExtractedPlcFile } from '../../../../server/utils/plcConfigExtractor'

export default defineEventHandler(async (event) => {
  const body = await readBody(event) || {}

  const metadata: ControllerMetadata = {
    hostname: body.hostname || 'IPC-L1-01',
    twincatRuntimeVersion: body.twincatRuntimeVersion || '3.1.4024.35',
    amsNetId: body.amsNetId || '192.168.1.110.1.1',
    ipAddress: body.ipAddress || '192.168.1.110',
    macAddress: body.macAddress || '00:01:05:3A:4B:5C'
  }

  const files: ExtractedPlcFile[] = body.files || [
    {
      path: 'POUs/MAIN.TcPOU',
      normalizedPath: 'Line1_XAE/POUs/MAIN.TcPOU',
      fileType: 'POU',
      sizeBytes: 154,
      sha256: 'a1b2c3d4e5f6',
      content: 'PROGRAM MAIN...'
    },
    {
      path: 'GVLs/GVL_MachineState.TcGVL',
      normalizedPath: 'Line1_XAE/GVLs/GVL_MachineState.TcGVL',
      fileType: 'GVL',
      sizeBytes: 98,
      sha256: 'b2c3d4e5f6a1',
      content: 'VAR_GLOBAL...'
    }
  ]

  const commit = commitControllerRuntime(metadata, files, {
    upstreamBranch: body.upstreamBranch || 'line-master',
    forceRebase: body.forceRebase === true
  })

  // Stage pending sync for automation engineer authorization
  const pendingSync = stagePendingCopiaSync({
    deviceId: metadata.hostname,
    hostname: metadata.hostname,
    localCommitHash: commit.commitHash,
    branch: commit.branch,
    modifiedFiles: files.map(f => f.path)
  })

  return {
    success: true,
    commit,
    pendingSync,
    eulaComplianceNotice: 'Local Git tracking committed under internal probe identity. Remote Copia Cloud push requires engineer authorization.'
  }
})
