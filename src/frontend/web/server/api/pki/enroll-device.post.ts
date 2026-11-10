import { defineEventHandler, readBody, createError } from 'h3'
import { pkiStore } from '../../utils/pkiStore'

export interface EnrollmentRecord {
  enrollmentId: string
  hostname: string
  machineIdentifier: string
  macAddress: string
  status: 'PendingApproval' | 'Approved' | 'Rejected'
  requestedAt: string
  approvedAt?: string
  clientCertificateThumbprint?: string
  rawClientCertPem?: string
}

const enrollmentStore = new Map<string, EnrollmentRecord>()

// Pre-populate with sample record
const initialEnrollment: EnrollmentRecord = {
  enrollmentId: 'enr-demo-9921',
  hostname: 'IPC-L1-03',
  machineIdentifier: 'LINE1-OP30',
  macAddress: '00:01:05:AA:BB:CC',
  status: 'PendingApproval',
  requestedAt: new Date(Date.now() - 10 * 60 * 1000).toISOString()
}
enrollmentStore.set(initialEnrollment.enrollmentId, initialEnrollment)

export default defineEventHandler(async (event) => {
  let body: any = (event as any)?._body
  if (!body) {
    try {
      body = await readBody(event)
    } catch {
      body = {}
    }
  }
  body = body || {}
  const action = body.action || 'register'

  // Action 1: Edge Agent registers for enrollment
  if (action === 'register') {
    const enrollmentSecret = body.enrollmentSecret || body.enrollmentToken
    if (!enrollmentSecret || enrollmentSecret.trim().length < 6) {
      throw createError({
        statusCode: 401,
        statusMessage: 'Valid enrollment secret/token is required to register new edge device.'
      })
    }

    const hostname = body.hostname || 'NEW-EDGE-NODE'
    const machineIdentifier = body.machineIdentifier || hostname
    const enrollmentId = `enr-${Date.now()}-${Math.floor(1000 + Math.random() * 9000)}`

    const record: EnrollmentRecord = {
      enrollmentId,
      hostname,
      machineIdentifier,
      macAddress: body.macAddress || '00:00:00:00:00:00',
      status: 'PendingApproval',
      requestedAt: new Date().toISOString()
    }

    enrollmentStore.set(enrollmentId, record)

    return {
      success: true,
      enrollmentId,
      status: 'PendingApproval',
      message: 'Device registration submitted. Awaiting administrator approval in PKI Dashboard.'
    }
  }

  // Action 2: Administrator approves enrollment and issues certificate
  if (action === 'approve') {
    const enrollmentId = body.enrollmentId
    const record = enrollmentStore.get(enrollmentId)
    if (!record) {
      throw createError({
        statusCode: 404,
        statusMessage: `Enrollment record '${enrollmentId}' not found.`
      })
    }

    // Generate dedicated client mTLS certificate via pkiStore
    const newCert = pkiStore.addCertificate({
      commonName: `CN=${record.hostname}.factory.corp, O=Enterprise Factory Automation`,
      validityDays: 365,
      keyAlgorithm: 'RSA-2048'
    })

    record.status = 'Approved'
    record.approvedAt = new Date().toISOString()
    record.clientCertificateThumbprint = newCert.thumbprint
    record.rawClientCertPem = newCert.rawPem

    enrollmentStore.set(enrollmentId, record)

    return {
      success: true,
      enrollmentId,
      status: 'Approved',
      issuedCertificate: {
        commonName: newCert.commonName,
        thumbprint: newCert.thumbprint,
        validTo: newCert.validTo
      },
      message: `Device '${record.hostname}' approved. Client certificate issued successfully.`
    }
  }

  // Action 3: Edge Agent checks status & retrieves issued certificate
  if (action === 'poll' || action === 'status') {
    const enrollmentId = body.enrollmentId
    const record = enrollmentStore.get(enrollmentId)
    if (!record) {
      throw createError({
        statusCode: 404,
        statusMessage: `Enrollment record '${enrollmentId}' not found.`
      })
    }

    return {
      success: true,
      record
    }
  }

  // Action 4: List all pending device enrollments
  return {
    success: true,
    enrollments: Array.from(enrollmentStore.values()),
    pendingCount: Array.from(enrollmentStore.values()).filter(e => e.status === 'PendingApproval').length
  }
})
