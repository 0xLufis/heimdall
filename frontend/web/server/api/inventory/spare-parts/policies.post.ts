import { defineEventHandler, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body || !body.id) {
    throw createError({ statusCode: 400, message: 'Policy ID is required.' })
  }

  try {
    const updated = partsInventoryStore.updateSparePartPolicy(body.id, {
      fractionalRatio: body.fractionalRatio !== undefined ? Number(body.fractionalRatio) : undefined,
      upperBoundQuantity: body.upperBoundQuantity !== undefined ? Number(body.upperBoundQuantity) : undefined,
      upperBoundCostEur: body.upperBoundCostEur !== undefined ? Number(body.upperBoundCostEur) : undefined,
      activeMachinesInProduction: body.activeMachinesInProduction !== undefined ? Number(body.activeMachinesInProduction) : undefined
    })

    return {
      success: true,
      sparePart: updated
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      message: err.message || 'Failed to update spare part policy.'
    })
  }
})
