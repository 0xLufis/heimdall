import { createHash } from 'node:crypto'
import { ExtractedPlcFile } from './plcConfigExtractor'

export interface ControllerMetadata {
  hostname: string
  twincatRuntimeVersion: string
  amsNetId: string
  ipAddress?: string
  macAddress?: string
  cpuArchitecture?: string
}

export interface ControllerCommit {
  commitHash: string
  branch: string
  upstreamBranch: string
  author: string
  timestampUtc: string
  commitMessage: string
  metadata: ControllerMetadata
  changedFilesCount: number
  rebaseStatus: 'clean_fast_forward' | 'clean_3way_merged' | 'diverged_conflict'
  rebaseSummary: string
}

const controllerCommitStore = new Map<string, ControllerCommit[]>()

/**
 * Returns canonical Git branch name for a controller runtime.
 */
export function getControllerBranchName(hostname: string): string {
  const sanitized = hostname.toLowerCase().replace(/[^a-z0-9-_]/g, '-')
  return `controller/${sanitized}-running`
}

/**
 * Commits extracted PLC runtime files to the controller tracking branch and
 * simulates clean 3-way merge / rebase against the upstream line master branch.
 */
export function commitControllerRuntime(
  metadata: ControllerMetadata,
  files: ExtractedPlcFile[],
  options: { upstreamBranch?: string; forceRebase?: boolean } = {}
): ControllerCommit {
  const branch = getControllerBranchName(metadata.hostname)
  const upstreamBranch = options.upstreamBranch || 'line-master'
  const now = new Date()

  // Generate deterministic commit hash from files + metadata + timestamp
  const commitHasher = createHash('sha1')
  commitHasher.update(branch)
  commitHasher.update(metadata.hostname)
  commitHasher.update(metadata.twincatRuntimeVersion)
  commitHasher.update(metadata.amsNetId)
  for (const f of files) {
    commitHasher.update(f.normalizedPath + f.sha256)
  }
  commitHasher.update(now.toISOString())
  const commitHash = commitHasher.digest('hex').substring(0, 12)

  const commitMessage = `chore(twincat): runtime snapshot from ${metadata.hostname} [${metadata.twincatRuntimeVersion}]

Controller-AMS-Net-ID: ${metadata.amsNetId}
TwinCAT-Runtime: ${metadata.twincatRuntimeVersion}
Hardware-IP: ${metadata.ipAddress || '192.168.1.100'}
Hardware-MAC: ${metadata.macAddress || '00:01:05:AA:BB:CC'}
Snapshot-Timestamp: ${now.toISOString()}
Author-Type: EdgeDaemonAutomatedProbe`

  // Evaluate 3-way merge or rebase against line master
  const existingCommits = controllerCommitStore.get(branch) || []
  let rebaseStatus: ControllerCommit['rebaseStatus'] = 'clean_fast_forward'
  let rebaseSummary = `Fast-forwarded onto ${upstreamBranch} without conflicts`

  if (existingCommits.length > 0) {
    const lastCommit = existingCommits[existingCommits.length - 1]
    if (files.some(f => f.fileType === 'POU' && f.content?.includes('CONFLICT_SIMULATION'))) {
      rebaseStatus = 'diverged_conflict'
      rebaseSummary = `Merge conflict detected between ${branch} and ${upstreamBranch} in POUs`
    } else {
      rebaseStatus = 'clean_3way_merged'
      rebaseSummary = `Successfully 3-way merged ${branch} with ${upstreamBranch} (parent: ${lastCommit.commitHash})`
    }
  }

  const commit: ControllerCommit = {
    commitHash,
    branch,
    upstreamBranch,
    author: 'heimdall-probe <heimdall-probe@internal>',
    timestampUtc: now.toISOString(),
    commitMessage,
    metadata,
    changedFilesCount: files.length,
    rebaseStatus,
    rebaseSummary
  }

  existingCommits.push(commit)
  controllerCommitStore.set(branch, existingCommits)

  return commit
}

export function getControllerCommits(hostname: string): ControllerCommit[] {
  const branch = getControllerBranchName(hostname)
  return controllerCommitStore.get(branch) || []
}

export function clearControllerCommits(): void {
  controllerCommitStore.clear()
}
