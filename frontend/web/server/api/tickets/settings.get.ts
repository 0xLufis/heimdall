import { defineEventHandler } from 'h3'
import { getTicketSettings } from '../../utils/ticketsStore'

export default defineEventHandler(() => {
  return {
    success: true,
    settings: getTicketSettings()
  }
})
