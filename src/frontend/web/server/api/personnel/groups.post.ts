import { defineEventHandler, readBody, createError } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.name) {
    throw createError({ statusCode: 400, statusMessage: 'Group name is required.' })
  }
  return backendFetch(event, 'delegation/groups', { method: 'POST', body })
})
