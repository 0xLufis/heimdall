import { defineEventHandler, readBody, createError } from 'h3'
import { pkiStore } from '../../../server/utils/pkiStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body || !body.thumbprint) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Certificate thumbprint is required for endpoint validation.'
    })
  }

  const thumbprint = body.thumbprint as string
  const hostname = body.hostname || 'unknown-endpoint'
  const now = new Date()

  // Look up certificate in PKI store
  const allCerts = pkiStore.getAllCertificates()
  const rootCa = pkiStore.getRootCa()
  const cert = allCerts.find(c => c.thumbprint === thumbprint)

  const auditEntry = {
    timestamp: now.toISOString(),
    thumbprint,
    hostname,
    action: 'validate' as string,
    result: 'unknown' as string,
    reason: '' as string
  }

  if (!cert) {
    auditEntry.action = 'reject'
    auditEntry.result = 'untrusted'
    auditEntry.reason = 'Certificate thumbprint not found in Heimdall PKI trust store'
    return {
      valid: false,
      status: 'Untrusted',
      reason: auditEntry.reason,
      audit: auditEntry
    }
  }

  // Check revocation status
  if (cert.status === 'Revoked') {
    auditEntry.action = 'reject'
    auditEntry.result = 'revoked'
    auditEntry.reason = `Certificate ${cert.commonName} has been revoked`
    return {
      valid: false,
      status: 'Revoked',
      reason: auditEntry.reason,
      certificate: {
        commonName: cert.commonName,
        thumbprint: cert.thumbprint,
        status: cert.status
      },
      audit: auditEntry
    }
  }

  // Check expiration
  const validTo = new Date(cert.validTo)
  if (validTo < now) {
    auditEntry.action = 'reject'
    auditEntry.result = 'expired'
    auditEntry.reason = `Certificate expired on ${cert.validTo}`
    return {
      valid: false,
      status: 'Expired',
      reason: auditEntry.reason,
      certificate: {
        commonName: cert.commonName,
        thumbprint: cert.thumbprint,
        validTo: cert.validTo,
        status: cert.status
      },
      audit: auditEntry
    }
  }

  // Verify issuer chain against trusted root CA
  const issuerTrusted = rootCa && (
    cert.issuer === rootCa.commonName ||
    cert.isRootCa ||
    cert.issuer?.includes('Heimdall')
  )

  if (!issuerTrusted && !cert.isRootCa) {
    auditEntry.action = 'reject'
    auditEntry.result = 'untrusted_issuer'
    auditEntry.reason = `Certificate issuer "${cert.issuer}" not in trusted chain`
    return {
      valid: false,
      status: 'UntrustedIssuer',
      reason: auditEntry.reason,
      certificate: {
        commonName: cert.commonName,
        thumbprint: cert.thumbprint,
        issuer: cert.issuer
      },
      audit: auditEntry
    }
  }

  // Check approaching expiration (warning)
  const daysUntilExpiry = Math.floor((validTo.getTime() - now.getTime()) / (24 * 3600 * 1000))
  const expirationWarning = daysUntilExpiry <= 30

  auditEntry.action = 'accept'
  auditEntry.result = 'valid'
  auditEntry.reason = 'Certificate validated successfully against Heimdall CA trust chain'

  return {
    valid: true,
    status: 'Valid',
    certificate: {
      commonName: cert.commonName,
      thumbprint: cert.thumbprint,
      validFrom: cert.validFrom,
      validTo: cert.validTo,
      issuer: cert.issuer,
      keyAlgorithm: cert.keyAlgorithm,
      serialNumber: cert.serialNumber,
      daysUntilExpiry
    },
    warnings: expirationWarning ? [`Certificate expires in ${daysUntilExpiry} days`] : [],
    audit: auditEntry
  }
})
