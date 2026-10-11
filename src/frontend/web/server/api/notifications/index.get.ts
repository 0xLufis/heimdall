import { defineEventHandler, getQuery } from 'h3'
import { getNotifications } from '../../utils/notificationsStore'
import { getAuthenticatedUser } from '../../utils/authHelper'

export default defineEventHandler(async (event) => {
  const user = await getAuthenticatedUser(event, true)
  const query = getQuery(event)
  const unreadOnly = query.unreadOnly === 'true'
  const limit = Math.min(100, Math.max(1, parseInt(query.limit as string || '50', 10)))

  let list = getNotifications(user?.id)
  if (unreadOnly) {
    list = list.filter(n => !n.read)
  }

  const unreadCount = getNotifications(user?.id).filter(n => !n.read).length

  return {
    notifications: list.slice(0, limit),
    total: list.length,
    unreadCount
  }
})
