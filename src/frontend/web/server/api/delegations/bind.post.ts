import { defineEventHandler, readBody, createError } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

// Backend serialises enums as integers (no JsonStringEnumConverter); mirror DelegationDiscipline.
const DISCIPLINE: Record<string, number> = {
  Mechanical: 1, Controls: 2, Electrical: 3, Safety: 4, Quality: 5, Process: 6, Tooling: 7, General: 99
}

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.machineId || !body?.recipeId) {
    throw createError({ statusCode: 400, statusMessage: 'machineId and recipeId are required.' })
  }
  return backendFetch(event, 'delegation/bind', {
    method: 'POST',
    body: { machineId: body.machineId, recipeId: body.recipeId, discipline: typeof body.discipline === 'number' ? body.discipline : (DISCIPLINE[body.discipline] ?? DISCIPLINE.General) }
  })
})
