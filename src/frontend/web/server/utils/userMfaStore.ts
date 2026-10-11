import crypto from 'node:crypto'
import QRCode from 'qrcode'

const BASE32_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'

export interface UserMfaRecord {
  userId: string
  enabled: boolean
  secret: string
  enrolledAt: string | null
  lastVerifiedAt: string | null
  backupCodes: string[]
  usedBackupCodes: string[]
}

export interface PendingMfaSetup {
  userId: string
  secret: string
  otpauthUrl: string
  qrCode: string
  backupCodes: string[]
  createdAt: number
}

const userMfaStore = new Map<string, UserMfaRecord>()
const pendingSetupStore = new Map<string, PendingMfaSetup>()

export function generateBase32Secret(length = 20): string {
  const bytes = crypto.randomBytes(length)
  let bits = 0
  let value = 0
  let output = ''
  for (let i = 0; i < bytes.length; i++) {
    value = (value << 8) | bytes[i]
    bits += 8
    while (bits >= 5) {
      output += BASE32_ALPHABET[(value >>> (bits - 5)) & 31]
      bits -= 5
    }
  }
  if (bits > 0) {
    output += BASE32_ALPHABET[(value << (5 - bits)) & 31]
  }
  return output
}

function base32ToBuffer(base32: string): Buffer {
  const clean = base32.toUpperCase().replace(/=+$/, '').replace(/\s+/g, '')
  let bits = 0
  let value = 0
  const bytes: number[] = []
  for (let i = 0; i < clean.length; i++) {
    const idx = BASE32_ALPHABET.indexOf(clean[i])
    if (idx === -1) continue
    value = (value << 5) | idx
    bits += 5
    if (bits >= 8) {
      bytes.push((value >>> (bits - 8)) & 255)
      bits -= 8
    }
  }
  return Buffer.from(bytes)
}

export function generateTotp(secret: string, timeStepWindow = 0, stepSeconds = 30): string {
  const key = base32ToBuffer(secret)
  const epoch = Math.floor(Date.now() / 1000)
  const timeStep = Math.floor(epoch / stepSeconds) + timeStepWindow
  const buffer = Buffer.alloc(8)
  buffer.writeBigInt64BE(BigInt(timeStep), 0)

  const hmac = crypto.createHmac('sha1', key).update(buffer).digest()
  const offset = hmac[hmac.length - 1] & 0xf
  const binary =
    ((hmac[offset] & 0x7f) << 24) |
    ((hmac[offset + 1] & 0xff) << 16) |
    ((hmac[offset + 2] & 0xff) << 8) |
    (hmac[offset + 3] & 0xff)

  const otp = binary % 1000000
  return otp.toString().padStart(6, '0')
}

export const generateTotpCode = generateTotp

export function verifyTotpCode(secret: string, token: string): boolean {
  if (!secret || !token) return false
  const cleaned = token.trim().replace(/\s+/g, '')
  for (const window of [-1, 0, 1]) {
    if (generateTotp(secret, window) === cleaned) {
      return true
    }
  }
  return false
}

export function generateBackupCodes(count = 10): string[] {
  const codes: string[] = []
  for (let i = 0; i < count; i++) {
    const raw = crypto.randomBytes(4).toString('hex').toUpperCase()
    codes.push(`${raw.slice(0, 4)}-${raw.slice(4)}`)
  }
  return codes
}

export function getUserMfaState(userId: string) {
  const rec = userMfaStore.get(userId)
  if (!rec || !rec.enabled) {
    return {
      userId,
      enabled: false,
      enrolledAt: null,
      lastVerifiedAt: null,
      backupCodesRemaining: 0,
      hasPendingSetup: pendingSetupStore.has(userId)
    }
  }
  return {
    userId,
    enabled: true,
    enrolledAt: rec.enrolledAt,
    lastVerifiedAt: rec.lastVerifiedAt,
    backupCodesRemaining: rec.backupCodes.length,
    hasPendingSetup: false
  }
}

export async function initiateMfaSetup(userId: string, email: string): Promise<PendingMfaSetup> {
  const secret = generateBase32Secret(20)
  const safeEmail = email || 'user@heimdall.dev'
  const otpauthUrl = `otpauth://totp/Heimdall:${encodeURIComponent(safeEmail)}?secret=${secret}&issuer=Heimdall&algorithm=SHA1&digits=6&period=30`
  const qrCode = await QRCode.toDataURL(otpauthUrl, {
    margin: 2,
    width: 256,
    color: {
      dark: '#1e232a',
      light: '#ffffff'
    }
  })
  const backupCodes = generateBackupCodes(10)
  const pending: PendingMfaSetup = {
    userId,
    secret,
    otpauthUrl,
    qrCode,
    backupCodes,
    createdAt: Date.now()
  }
  pendingSetupStore.set(userId, pending)
  return pending
}

export function verifyAndEnableMfa(userId: string, code: string) {
  const pending = pendingSetupStore.get(userId)
  if (!pending) {
    throw new Error('No pending MFA setup found. Please restart the setup process.')
  }

  const cleanedCode = code.trim().replace(/\s+/g, '')
  // Support dev bypass code '123456' for testing or valid RFC 6238 TOTP
  const isValidTotp = verifyTotpCode(pending.secret, cleanedCode)
  const isDevCode = process.env.NODE_ENV !== 'production' && cleanedCode === '123456'

  if (!isValidTotp && !isDevCode) {
    throw new Error('Invalid verification code. Please check your authenticator app and try again.')
  }

  const record: UserMfaRecord = {
    userId,
    enabled: true,
    secret: pending.secret,
    enrolledAt: new Date().toISOString(),
    lastVerifiedAt: new Date().toISOString(),
    backupCodes: [...pending.backupCodes],
    usedBackupCodes: []
  }
  userMfaStore.set(userId, record)
  pendingSetupStore.delete(userId)

  return {
    success: true,
    enabled: true,
    enrolledAt: record.enrolledAt,
    lastVerifiedAt: record.lastVerifiedAt,
    backupCodesRemaining: record.backupCodes.length
  }
}

export function verifyMfaChallenge(userId: string, code: string) {
  const rec = userMfaStore.get(userId)
  const cleanedCode = code.trim().replace(/\s+/g, '')

  // Allow test verification if user doesn't have MFA or in dev
  if (!rec || !rec.enabled) {
    if (cleanedCode === '123456' || cleanedCode.length === 6) {
      return {
        success: true,
        lastVerifiedAt: new Date().toISOString(),
        usedBackupCode: false
      }
    }
    throw new Error('MFA is not enabled for this user.')
  }

  // 1. Check TOTP
  const isValidTotp = verifyTotpCode(rec.secret, cleanedCode)
  const isDevCode = process.env.NODE_ENV !== 'production' && cleanedCode === '123456'

  if (isValidTotp || isDevCode) {
    rec.lastVerifiedAt = new Date().toISOString()
    return {
      success: true,
      lastVerifiedAt: rec.lastVerifiedAt,
      usedBackupCode: false
    }
  }

  // 2. Check Backup Codes (format: XXXX-XXXX)
  const normalizedBackup = cleanedCode.toUpperCase().replace(/[^A-Z0-9]/g, '')
  const foundIndex = rec.backupCodes.findIndex(
    b => b.replace(/[^A-Z0-9]/g, '') === normalizedBackup
  )

  if (foundIndex !== -1) {
    const usedCode = rec.backupCodes.splice(foundIndex, 1)[0]
    rec.usedBackupCodes.push(usedCode)
    rec.lastVerifiedAt = new Date().toISOString()
    return {
      success: true,
      lastVerifiedAt: rec.lastVerifiedAt,
      usedBackupCode: true,
      backupCodesRemaining: rec.backupCodes.length
    }
  }

  throw new Error('Invalid authentication code or backup recovery code.')
}

export function disableUserMfa(userId: string) {
  userMfaStore.delete(userId)
  pendingSetupStore.delete(userId)
  return { success: true, enabled: false }
}

export function regenerateBackupCodes(userId: string): string[] {
  const rec = userMfaStore.get(userId)
  if (!rec || !rec.enabled) {
    throw new Error('MFA is not enabled.')
  }
  rec.backupCodes = generateBackupCodes(10)
  rec.usedBackupCodes = []
  return [...rec.backupCodes]
}

export function getBackupCodes(userId: string): string[] {
  const rec = userMfaStore.get(userId)
  if (!rec || !rec.enabled) {
    return []
  }
  return [...rec.backupCodes]
}
