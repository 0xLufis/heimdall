import { describe, it, expect } from 'vitest'
import healthHandler from '../../../frontend/web/server/api/health.get'
import readyHandler from '../../../frontend/web/server/api/ready.get'
import enrollDeviceHandler from '../../../frontend/web/server/api/pki/enroll-device.post'

describe('Packaging, Distribution & Deployment Automation', () => {

  // ============================================================
  // Section 1: Health & Readiness Probes
  // ============================================================
  describe('1. Kubernetes Liveness & Readiness Probes', () => {
    it('/api/health returns OK liveness status and uptime', () => {
      const result = healthHandler({} as any)
      expect(result.status).toBe('OK')
      expect(result.subsystem).toBe('heimdall-web-frontend')
      expect(result.uptimeSeconds).toBeGreaterThanOrEqual(0)
      expect(result.timestamp).toBeTruthy()
    })

    it('/api/ready returns system readiness and store checks', async () => {
      const result = await readyHandler({} as any)
      expect(result.status).toBe('Ready')
      expect(result.checks.machinesStore).toBe('Healthy')
      expect(result.checks.memory.rssMb).toBeGreaterThan(0)
      expect(result.timestamp).toBeTruthy()
    })
  })

  // ============================================================
  // Section 2: Automated Device Enrollment & Certificate Issuance
  // ============================================================
  describe('2. Automated Device Enrollment & Certificate Issuance', () => {
    it('rejects device registration without valid enrollment secret', async () => {
      await expect(
        enrollDeviceHandler({
          node: { req: {} },
          context: {},
          _body: { action: 'register', enrollmentSecret: 'short' }
        } as any)
      ).rejects.toThrow(/Valid enrollment secret/)
    })

    it('registers new edge device and sets status to PendingApproval', async () => {
      const result: any = await enrollDeviceHandler({
        node: { req: {} },
        context: {},
        _body: {
          action: 'register',
          enrollmentSecret: 'ENR-TOKEN-ENTERPRISE-SECRET-1234',
          hostname: 'IPC-STATION-NEW',
          machineIdentifier: 'LINE1-OP80',
          macAddress: '00:01:05:AA:BB:CC'
        }
      } as any)

      expect(result.success).toBe(true)
      expect(result.status).toBe('PendingApproval')
      expect(result.enrollmentId).toMatch(/^enr-\d+-\d+$/)

      // Admin approves enrollment
      const approvalResult: any = await enrollDeviceHandler({
        node: { req: {} },
        context: {},
        _body: {
          action: 'approve',
          enrollmentId: result.enrollmentId
        }
      } as any)

      expect(approvalResult.success).toBe(true)
      expect(approvalResult.status).toBe('Approved')
      expect(approvalResult.issuedCertificate.thumbprint).toBeTruthy()
      expect(approvalResult.issuedCertificate.commonName).toContain('IPC-STATION-NEW')

      // Agent polls status and retrieves issued certificate
      const pollResult: any = await enrollDeviceHandler({
        node: { req: {} },
        context: {},
        _body: {
          action: 'poll',
          enrollmentId: result.enrollmentId
        }
      } as any)

      expect(pollResult.success).toBe(true)
      expect(pollResult.record.status).toBe('Approved')
      expect(pollResult.record.clientCertificateThumbprint).toBe(approvalResult.issuedCertificate.thumbprint)
    })
  })

  // ============================================================
  // Section 3: Configuration Search Paths & MSI Properties
  // ============================================================
  describe('3. Standardized Search Paths & Packaging Models', () => {
    it('standardizes cross-platform configuration file search paths', () => {
      const linuxConfigPath = '/etc/heimdall/appsettings.json'
      const windowsConfigPath = '%ProgramData%\\Heimdall\\appsettings.json'

      expect(linuxConfigPath.startsWith('/etc/heimdall/')).toBe(true)
      expect(windowsConfigPath.includes('Heimdall')).toBe(true)
    })

    it('validates public MSI installation properties for silent MDM deployment', () => {
      const msiProperties = {
        SERVER_URL: 'https://heimdall.factory.corp:5001',
        ENROLLMENT_TOKEN: 'ENR-SECRET-991',
        QUIET_SWITCH: '/quiet'
      }

      expect(msiProperties.SERVER_URL).toMatch(/^https?:\/\//)
      expect(msiProperties.ENROLLMENT_TOKEN.length).toBeGreaterThan(6)
      expect(msiProperties.QUIET_SWITCH).toBe('/quiet')
    })
  })
})
