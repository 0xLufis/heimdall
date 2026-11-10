import { defineEventHandler, readBody, createError } from 'h3'
import { addTemplateToStore } from '../../utils/ticketTemplatesStore'

export default defineEventHandler(async (event) => {
  const body = (await readBody(event).catch(() => null)) || (event as any)._body || (event.node?.req as any)?.body

  if (!body || typeof body !== 'object') {
    throw createError({
      statusCode: 400,
      statusMessage: 'Invalid payload: body is required.'
    })
  }

  if (!body.errorCode || !body.shortDescription || !body.category) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Missing required template fields: errorCode, shortDescription, and category are mandatory.'
    })
  }

  const createdTemplate = addTemplateToStore({
    id: body.id,
    category: body.category,
    errorGroup: body.errorGroup || 'General',
    errorCode: body.errorCode.trim(),
    shortDescription: body.shortDescription.trim(),
    detailedDescription: (body.detailedDescription || '').trim(),
    targetKanbanState: body.targetKanbanState || 'Open',
    externalEscalationTarget: body.externalEscalationTarget,
    defaultTags: Array.isArray(body.defaultTags) ? body.defaultTags : [],
    sampleFbState: body.sampleFbState,
    sampleTelemetryKeys: Array.isArray(body.sampleTelemetryKeys) ? body.sampleTelemetryKeys : [],
    affectedMachineTypes: Array.isArray(body.affectedMachineTypes) ? body.affectedMachineTypes : []
  })

  return {
    success: true,
    template: createdTemplate
  }
})
