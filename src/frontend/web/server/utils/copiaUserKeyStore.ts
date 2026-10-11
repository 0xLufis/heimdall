const userCopiaKeyStore = new Map<string, string>()

export function getUserCopiaKey(userId: string): string | undefined {
  return userCopiaKeyStore.get(userId)
}

export function setUserCopiaKey(userId: string, apiKey: string): void {
  const trimmed = apiKey.trim()
  if (!trimmed) {
    userCopiaKeyStore.delete(userId)
    return
  }
  
  if (
    trimmed.toLowerCase() === 'service-user' ||
    trimmed.toLowerCase() === 'heimdall-probe' ||
    trimmed.toLowerCase().startsWith('shared-')
  ) {
    throw new Error('EULA Compliance Violation: Generic service account keys or shared keys cannot be used for Copia Cloud synchronization.')
  }

  userCopiaKeyStore.set(userId, trimmed)
}

export function clearUserCopiaKey(userId: string): void {
  userCopiaKeyStore.delete(userId)
}

export function maskApiKey(key: string): string {
  if (!key) return ''
  if (key.length <= 8) return '••••••••'
  return `${key.slice(0, 4)}••••••••${key.slice(-4)}`
}
