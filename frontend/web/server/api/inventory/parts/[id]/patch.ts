import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Part ID parameter is required.' })
  }

  const body = await readBody(event)
  if (!body) {
    throw createError({ statusCode: 400, message: 'Request body is required.' })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Technician' }
  let updatedPart

  if (body.condition !== undefined || body.operationalState !== undefined) {
    updatedPart = partsInventoryStore.updatePartCondition(
      id,
      body.condition,
      body.operationalState,
      actor
    )
  }

  if (body.wearDepreciationPercentage !== undefined || body.resellPriceOverrideEur !== undefined) {
    updatedPart = partsInventoryStore.updateResellPrice(
      id,
      {
        wearDepreciationPercentage: body.wearDepreciationPercentage,
        resellPriceOverrideEur: body.resellPriceOverrideEur
      },
      actor
    )
  }

  if (body.minQuantity !== undefined || body.minQuantityScalar !== undefined || body.quantity !== undefined) {
    updatedPart = partsInventoryStore.updatePartQuantities(
      id,
      {
        minQuantity: body.minQuantity,
        minQuantityScalar: body.minQuantityScalar,
        quantity: body.quantity
      },
      actor
    )
  }

  return {
    success: true,
    part: updatedPart
  }
})
