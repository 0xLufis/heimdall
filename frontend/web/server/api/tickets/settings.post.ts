import { defineEventHandler, readBody } from 'h3'
import { updateTicketSettings } from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event) || {}
  const updated = updateTicketSettings(body)
  return {
    success: true,
    settings: updated
  }
})
