import { defineEventHandler, readBody, createError } from 'h3'
import { updateSecurityGroupMapping } from '../../../utils/securityGroupMappingsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Mapping ID is required.'
    })
  }

  const body = await readBody(event)
  const updated = updateSecurityGroupMapping(id, body || {})

  if (!updated) {
    throw createError({
      statusCode: 404,
      statusMessage: `Mapping with ID '${id}' not found.`
    })
  }

  return updated
})
