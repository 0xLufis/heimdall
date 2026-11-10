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

  const costCenter = body.costCenter || {
    prodLine: body.prodLine,
    project: body.project,
    department: body.department,
    technician: body.technician,
    notes: body.notes
  }

  // Mandatory Cost Center validation: OR relation not AND
  const hasProdLine = Boolean(costCenter.prodLine && String(costCenter.prodLine).trim())
  const hasProject = Boolean(costCenter.project && String(costCenter.project).trim())
  const hasDepartment = Boolean(costCenter.department && String(costCenter.department).trim())

  if (!hasProdLine && !hasProject && !hasDepartment) {
    throw createError({
      statusCode: 400,
      message: 'Mandatory cost center tagging required: specify at least one of Production Line (prodLine), Project (project), or Department (department).'
    })
  }

  const quantity = Number(body.quantity) || 1
  const actor = body.actor || { id: 'usr-current', name: 'Technician' }

  try {
    const result = partsInventoryStore.usePart(id, costCenter, quantity, actor)
    return {
      success: true,
      part: result.part,
      auditRecord: result.auditRecord
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      message: err.message || 'Failed to use part.'
    })
  }
})
