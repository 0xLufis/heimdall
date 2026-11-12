import { defineEventHandler, createError } from 'h3'

/** Condition, operational state and resell price are not stored by the inventory backend yet. */
export default defineEventHandler(() => {
  throw createError({ statusCode: 501, statusMessage: 'Condition and resell price updates are not available: the inventory backend does not store them yet.' })
})
