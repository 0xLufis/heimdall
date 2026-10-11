import { defineEventHandler, readBody, createError } from 'h3'
import { addNotification } from '../../utils/notificationsStore'
import { getAuthenticatedUser } from '../../utils/authHelper'

export default defineEventHandler(async (event) => {
  const user = await getAuthenticatedUser(event, true)
  const body = await readBody(event)
  if (!body || !body.title || !body.message) {
    throw createError({ statusCode: 400, statusMessage: 'Title and message are required' })
  }

  const notif = addNotification({
    title: body.title,
    message: body.message,
    type: body.type || 'info',
    source: body.source || 'system',
    link: body.link,
    userId: body.userId || user?.id
  })

  return {
    success: true,
    notification: notif
  }
})
