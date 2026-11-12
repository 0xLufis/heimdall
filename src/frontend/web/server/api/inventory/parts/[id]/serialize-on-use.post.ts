import { defineEventHandler, createError } from 'h3'

/** Splitting a serialized unit off bulk stock has no backend endpoint yet. */
export default defineEventHandler(() => {
  throw createError({ statusCode: 501, statusMessage: 'Serialize-on-use is not available: the inventory backend has no endpoint for it yet.' })
})
