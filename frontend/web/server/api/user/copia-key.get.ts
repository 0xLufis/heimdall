import { defineEventHandler, getQuery } from 'h3'
import { getUserCopiaKey, maskApiKey } from '../../utils/copiaUserKeyStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const userId = (query.userId as string) || (event.context as any).user?.id || 'usr-default'

  const key = getUserCopiaKey(userId)
  return {
    hasKey: Boolean(key),
    maskedKey: key ? maskApiKey(key) : null
  }
})
