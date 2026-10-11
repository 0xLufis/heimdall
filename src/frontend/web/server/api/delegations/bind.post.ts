import { defineEventHandler, readBody } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.machineId || !body.recipeId) {
    throw createError({ statusCode: 400, message: 'machineId and recipeId are required.' })
  }
  return componentsAndDelegationsStore.bindRecipeToMachine(
    body.machineId,
    body.recipeId,
    body.discipline || 'General'
  )
})
