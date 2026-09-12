import Redis from 'ioredis'

let redisClient: Redis | null = null
let isConnected = false

export function getRedisClient(): Redis | null {
  if (redisClient) return redisClient

  const redisUrl = process.env.REDIS_URL || process.env.REDIS_CONNECTION_STRING
  const redisHost = process.env.REDIS_HOST || (process.env.NODE_ENV === 'production' || process.env.BACKEND_API_URL?.includes('backend') ? 'redis' : 'localhost')
  const redisPort = Number(process.env.REDIS_PORT) || 6379
  const redisPassword = process.env.REDIS_PASSWORD || 'heimdall_redis_dev_secret'

  try {
    if (redisUrl && redisUrl.startsWith('redis')) {
      redisClient = new Redis(redisUrl, {
        lazyConnect: true,
        maxRetriesPerRequest: 1,
        connectTimeout: 2000,
        enableOfflineQueue: false
      })
    } else {
      redisClient = new Redis({
        host: redisHost,
        port: redisPort,
        password: redisPassword,
        lazyConnect: true,
        maxRetriesPerRequest: 1,
        connectTimeout: 2000,
        enableOfflineQueue: false
      })
    }

    redisClient.on('connect', () => {
      isConnected = true
    })

    redisClient.on('error', () => {
      // Gracefully handle offline Redis without crashing the process
      isConnected = false
    })

    redisClient.connect().catch(() => {
      isConnected = false
    })

    return redisClient
  } catch {
    return null
  }
}

export async function getCachedJson<T>(key: string): Promise<T | null> {
  const client = getRedisClient()
  if (!client) return null
  try {
    const raw = await client.get(key)
    if (!raw) return null
    return JSON.parse(raw) as T
  } catch {
    return null
  }
}

export async function setCachedJson<T>(key: string, value: T, ttlSeconds: number = 60): Promise<void> {
  const client = getRedisClient()
  if (!client) return
  try {
    const raw = JSON.stringify(value)
    await client.setex(key, ttlSeconds, raw)
  } catch {
    // Ignore cache write failure
  }
}

export async function invalidateCachePattern(pattern: string): Promise<void> {
  const client = getRedisClient()
  if (!client) return
  try {
    const keys = await client.keys(pattern)
    if (keys.length > 0) {
      await client.del(...keys)
    }
  } catch {
    // Ignore cache invalidation failure
  }
}
