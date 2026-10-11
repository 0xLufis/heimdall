import { defineEventHandler, createError } from 'h3'
import { markNotificationRead } from '../../../utils/notificationsStore'
import { getAuthenticatedUser } from '../../../utils/authHelper'

export default defineEventHandler(async (event) => {
  await getAuthenticatedUser(event, true)
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Notification ID is required' })
  }

  const updated = markNotificationRead(id)
  if (!updated) {
    throw createError({ statusCode: 404, statusMessage: 'Notification not found' })
  }

  return {
    success: true,
    notification: updated
  }
})
