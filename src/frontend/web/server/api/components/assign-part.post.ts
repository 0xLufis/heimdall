import { defineEventHandler, readBody } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'
import { partsInventoryStore } from '../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.machineId || !body.partId) {
    throw createError({ statusCode: 400, message: 'machineId and partId are required.' })
  }

  // Deduct/update inventory part if applicable
  const { parts } = partsInventoryStore.getAllParts()
  const part = parts.find(p => p.id === body.partId)
  if (part) {
    part.stockQuantity = Math.max(0, (part.stockQuantity || 1) - 1)
    if (part.stockQuantity === 0) part.operationalState = 'in_service'
    part.linkedMachineId = body.machineId
    body.partSerialNumber = part.serialNumber || body.partSerialNumber
    body.partName = part.name || body.partName
  }

  const result = componentsAndDelegationsStore.assignPart(body)
  return result
})
