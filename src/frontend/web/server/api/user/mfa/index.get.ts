import { defineEventHandler, getQuery } from 'h3'
import { getUserMfaState } from '../../../utils/userMfaStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const userId = (query.userId as string) || ((event.context as any).user?.id) || 'usr-default'
  return getUserMfaState(userId)
})
