import { defineEventHandler } from 'h3'
import { markAllNotificationsRead } from '../../utils/notificationsStore'
import { getAuthenticatedUser } from '../../utils/authHelper'

export default defineEventHandler(async (event) => {
  const user = await getAuthenticatedUser(event, true)
  const count = markAllNotificationsRead(user?.id)

  return {
    success: true,
    markedReadCount: count
  }
})
