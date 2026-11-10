import { defineEventHandler, readBody, createError } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.recipeCode || !body?.name) {
    throw createError({ statusCode: 400, statusMessage: 'recipeCode and name are required.' })
  }
  return backendFetch(event, 'component/recipes', { method: 'POST', body })
})
