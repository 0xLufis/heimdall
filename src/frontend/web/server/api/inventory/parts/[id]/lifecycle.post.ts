import { defineEventHandler, createError } from 'h3'

/** Lifecycle transitions (in machine / removed / donor / scrap) have no backend endpoint yet. */
export default defineEventHandler(() => {
  throw createError({ statusCode: 501, statusMessage: 'Lifecycle transitions are not available: the inventory backend has no lifecycle endpoint yet.' })
})
