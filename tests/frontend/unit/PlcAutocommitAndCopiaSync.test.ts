import { describe, it, expect, beforeEach } from 'vitest'
import {
  extractPlcConfig,
  isTransientArtifact,
  categorizePlcFile,
  normalizeXaePath,
  TWINCAT_GITIGNORE_PATTERNS
} from '../../../src/frontend/web/server/utils/plcConfigExtractor'
import {
  commitControllerRuntime,
  getControllerBranchName,
  getControllerCommits,
  clearControllerCommits,
  type ControllerMetadata
} from '../../../src/frontend/web/server/utils/controllerBranchTracker'
import {
  stagePendingCopiaSync,
  authorizeCopiaPush,
  getAllPendingCopiaSyncs,
  clearPendingCopiaSyncs
} from '../../../src/frontend/web/server/utils/copiaSyncNotifier'
import { setUserCopiaKey, clearUserCopiaKey } from '../../../src/frontend/web/server/utils/copiaUserKeyStore'

describe('Automated PLC Project Extraction & Version Control Sync', () => {

  beforeEach(() => {
    clearControllerCommits()
    clearPendingCopiaSyncs()
    clearUserCopiaKey('usr-eng-001')
  })

  // ============================================================
  // Section 1: TwinCAT Runtime Config Extraction (CurrentConfig.tnzip)
  // ============================================================
  describe('1. TwinCAT Runtime Config Extraction', () => {
    it('filters out transient compiler state and temporary build artifacts', () => {
      const transientFiles = [
        '_Boot/TwinCAT/Plc/TcPlcApp.tszip',
        '_CompileInfo/PlcApp.compileinfo',
        'bin/TcPlcApp.tpy',
        'obj/Debug/temp.tmc',
        'sub/backup.bak',
        'TwinCAT.suo',
        'Project.user',
        '~volatile_lock'
      ]

      for (const file of transientFiles) {
        expect(isTransientArtifact(file)).toBe(true)
      }
    })

    it('preserves valid source code POUs, GVLs, DUTs, and Project manifests', () => {
      const validFiles = [
        'POUs/MAIN.TcPOU',
        'GVLs/GVL_MachineState.TcGVL',
        'DUTs/ST_PartTelemetry.TcDUT',
        'TwinCAT Project.tsproj',
        'VISUs/MainScreen.TcVIS'
      ]

      for (const file of validFiles) {
        expect(isTransientArtifact(file)).toBe(false)
      }
    })

    it('correctly categorizes PLC files by extension', () => {
      expect(categorizePlcFile('POUs/FB_Motion.TcPOU')).toBe('POU')
      expect(categorizePlcFile('DUTs/E_State.TcDUT')).toBe('DUT')
      expect(categorizePlcFile('GVLs/GVL_IO.TcGVL')).toBe('GVL')
      expect(categorizePlcFile('Cell.tsproj')).toBe('TSPROJ')
      expect(categorizePlcFile('Config.xml')).toBe('CONFIG')
    })

    it('normalizes archive relative paths into canonical XAE project structure', () => {
      const normalized = normalizeXaePath('Line1_XAE', 'currentconfig/POUs/MAIN.TcPOU')
      expect(normalized).toBe('Line1_XAE/POUs/MAIN.TcPOU')
    })

    it('extractPlcConfig extracts valid files and computes SHA-256 project checksum', () => {
      const raw = [
        { path: 'POUs/MAIN.TcPOU', content: 'PROGRAM MAIN...' },
        { path: 'GVLs/GVL_IO.TcGVL', content: 'VAR_GLOBAL...' },
        { path: '_Boot/TwinCAT/app.tszip', content: 'BIN' },
        { path: 'compile.compileinfo', content: 'INFO' }
      ]

      const result = extractPlcConfig('IPC-L1-01', raw, { projectName: 'Line1_XAE' })
      expect(result.controllerHostname).toBe('IPC-L1-01')
      expect(result.totalSourceFiles).toBe(2)
      expect(result.totalIgnoredCount).toBe(2)
      expect(result.extractedFiles).toHaveLength(2)
      expect(result.projectChecksum).toMatch(/^[a-f0-9]{64}$/)
      expect(result.extractedFiles[0].normalizedPath).toBe('Line1_XAE/POUs/MAIN.TcPOU')
    })
  })

  // ============================================================
  // Section 2: Branch Rebase & Local Git Tracking
  // ============================================================
  describe('2. Branch Rebase & Local Git Tracking', () => {
    const metadata: ControllerMetadata = {
      hostname: 'IPC-L1-02',
      twincatRuntimeVersion: '3.1.4024.35',
      amsNetId: '192.168.1.111.1.1',
      ipAddress: '192.168.1.111',
      macAddress: '00:01:05:44:55:66'
    }

    it('generates consistent canonical controller branch name', () => {
      const branch = getControllerBranchName('IPC-L1-02')
      expect(branch).toBe('controller/ipc-l1-02-running')
    })

    it('commits extracted changes with internal probe author identity', () => {
      const files = [
        {
          path: 'POUs/MAIN.TcPOU',
          normalizedPath: 'Line1_XAE/POUs/MAIN.TcPOU',
          fileType: 'POU' as const,
          sizeBytes: 120,
          sha256: 'abc123def456'
        }
      ]

      const commit = commitControllerRuntime(metadata, files)
      expect(commit.author).toBe('heimdall-probe <heimdall-probe@internal>')
      expect(commit.branch).toBe('controller/ipc-l1-02-running')
      expect(commit.commitMessage).toContain('IPC-L1-02')
      expect(commit.commitMessage).toContain('3.1.4024.35')
      expect(commit.commitMessage).toContain('192.168.1.111.1.1')
    })

    it('evaluates fast-forward vs 3-way merge on subsequent controller commits', () => {
      const files = [
        {
          path: 'POUs/MAIN.TcPOU',
          normalizedPath: 'Line1_XAE/POUs/MAIN.TcPOU',
          fileType: 'POU' as const,
          sizeBytes: 120,
          sha256: 'abc123def456'
        }
      ]

      const firstCommit = commitControllerRuntime(metadata, files)
      expect(firstCommit.rebaseStatus).toBe('clean_fast_forward')

      const secondCommit = commitControllerRuntime(metadata, files)
      expect(secondCommit.rebaseStatus).toBe('clean_3way_merged')
    })
  })

  // ============================================================
  // Section 3: Cloud Copia Synchronization & Notifications
  // ============================================================
  describe('3. Cloud Copia Synchronization & Anti-User-Pooling', () => {
    it('stages pending sync and creates notification for automation engineer', () => {
      const record = stagePendingCopiaSync({
        deviceId: 'IPC-L1-01',
        hostname: 'IPC-L1-01',
        localCommitHash: 'loc-abc123',
        branch: 'controller/ipc-l1-01-running',
        modifiedFiles: ['POUs/MAIN.TcPOU']
      })

      expect(record.status).toBe('PendingUserAuthorization')
      expect(record.notificationMessage).toContain('EULA Compliance Notice')
      expect(record.notificationMessage).toContain('loc-abc123')
      expect(record.diffPreview.changedBlocks).toHaveLength(1)
    })

    it('rejects generic service account keys in compliance with Copia ToS', () => {
      expect(() => setUserCopiaKey('usr-eng-001', 'service-user')).toThrow(/EULA Compliance Violation/)
      expect(() => setUserCopiaKey('usr-eng-001', 'heimdall-probe')).toThrow(/EULA Compliance Violation/)
      expect(() => setUserCopiaKey('usr-eng-001', 'shared-automation-key')).toThrow(/EULA Compliance Violation/)
    })

    it('authorizes push when engineer provides personal authenticated Copia API key', () => {
      setUserCopiaKey('usr-eng-001', 'copia_pat_valid_personal_token_9918')

      const record = stagePendingCopiaSync({
        deviceId: 'IPC-L1-01',
        hostname: 'IPC-L1-01',
        localCommitHash: 'loc-fedcba',
        branch: 'controller/ipc-l1-01-running',
        modifiedFiles: ['POUs/WeldControl.TcPOU']
      })

      const pushResult = authorizeCopiaPush(record.syncId, {
        id: 'usr-eng-001',
        name: 'István Kovács',
        email: 'istvan.kovacs@factory.corp'
      })

      expect(pushResult.success).toBe(true)
      expect(pushResult.record.status).toBe('ApprovedAndPushed')
      expect(pushResult.record.authorizedByUserName).toBe('István Kovács')
      expect(pushResult.cloudCommitUrl).toContain('loc-fedcba')
    })
  })
})
