import { defineEventHandler, getQuery, createError } from 'h3'
import { isBackendId, loadBackendPart, loadBackendParts, toInventoryPart } from '../../utils/inventoryBackend'

/** Scan lookup (QR / barcode / RFID payload): backend id, identifier or serial number. */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const code = String(query.code || query.q || '').trim()
  if (!code) throw createError({ statusCode: 400, statusMessage: 'Scan code or query string is required.' })
  const currency = (query.currency as string) || 'EUR'

  if (isBackendId(code)) {
    return { success: true, code, part: toInventoryPart(await loadBackendPart(event, code), currency) }
  }
  const wanted = code.toLowerCase()
  const raw = (await loadBackendParts(event)).find(p => [p.name, p.serialNumber].some(v => String(v || '').toLowerCase() === wanted))
  if (!raw) throw createError({ statusCode: 404, statusMessage: `No inventory part found matching code "${code}".` })
  return { success: true, code, part: toInventoryPart(raw, currency) }
})
