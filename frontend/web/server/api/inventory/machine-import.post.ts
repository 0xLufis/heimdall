import { defineEventHandler, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body) {
    throw createError({ statusCode: 400, message: 'Request payload is required.' })
  }

  if (!body.machineId || !body.machineName) {
    throw createError({
      statusCode: 400,
      message: 'machineId and machineName are required to import machine assets.'
    })
  }

  if (!body.rawContent && (!body.parsedItems || body.parsedItems.length === 0)) {
    throw createError({
      statusCode: 400,
      message: 'Either rawContent (ChatGPT JSON or BOM text) or parsedItems array is required.'
    })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Inventory Manager' }

  try {
    const result = partsInventoryStore.importMachineDocument({
      machineId: body.machineId,
      machineName: body.machineName,
      source: body.source || 'chatgpt',
      rawContent: body.rawContent || '',
      parsedItems: body.parsedItems
    }, actor)

    return result
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      message: err.message || 'Failed to process machine document import.'
    })
  }
})
