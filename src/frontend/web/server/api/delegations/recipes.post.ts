import { defineEventHandler, readBody, createError } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

/** Create or update a delegation recipe (backend upserts by id). */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.name || !body?.discipline) {
    throw createError({ statusCode: 400, statusMessage: 'name and discipline are required.' })
  }
  return backendFetch(event, 'delegation/recipes', { method: 'POST', body })
})
