import { defineEventHandler, readBody, createError } from 'h3'
import { updateTemplateInStore, getTemplateById } from '../../utils/ticketTemplatesStore'

export default defineEventHandler(async (event) => {
  const body = (await readBody(event).catch(() => null)) || (event as any)._body || (event.node?.req as any)?.body
  if (!body || typeof body !== 'object') {
    throw createError({ statusCode: 400, statusMessage: 'Request body is required' })
  }

  const id = body.id || body.errorCode
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Template id or errorCode is required' })
  }

  const updated = updateTemplateInStore(id, body)
  if (!updated) {
    throw createError({ statusCode: 404, statusMessage: `Template with ID '${id}' not found` })
  }

  return {
    success: true,
    template: updated
  }
})
