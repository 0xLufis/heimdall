import { defineEventHandler } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const data = await backendFetch<any[]>(event, 'technician/absences')
  return Array.isArray(data) ? data : []
})
