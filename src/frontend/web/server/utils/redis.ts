// Decommissioned direct ioredis connection in favor of ASP.NET Core hybrid Redis Cache (CacheService.cs).
// Nitro now operates without maintaining persistent Redis TCP connections.
// In-memory caching is maintained for local Nitro execution and test suites.

const localMemoryCache = new Map<string, { value: any; expiresAt: number }>()

export function getRedisClient(): null {
  return null
}

export async function getCachedJson<T>(key: string): Promise<T | null> {
  const item = localMemoryCache.get(key)
  if (!item) return null
  if (Date.now() > item.expiresAt) {
    localMemoryCache.delete(key)
    return null
  }
  return item.value as T
}

export async function setCachedJson<T>(key: string, value: T, ttlSeconds: number = 60): Promise<void> {
  localMemoryCache.set(key, {
    value,
    expiresAt: Date.now() + ttlSeconds * 1000
  })
}

export async function invalidateCachePattern(pattern: string): Promise<void> {
  const regex = new RegExp('^' + pattern.replace(/\*/g, '.*') + '$')
  for (const key of localMemoryCache.keys()) {
    if (regex.test(key)) {
      localMemoryCache.delete(key)
    }
  }
}
