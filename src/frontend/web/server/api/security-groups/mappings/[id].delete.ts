import { defineEventHandler, createError } from 'h3'
import { deleteSecurityGroupMapping } from '../../../utils/securityGroupMappingsStore'

export default defineEventHandler((event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Mapping ID is required.'
    })
  }

  const success = deleteSecurityGroupMapping(id)
  if (!success) {
    throw createError({
      statusCode: 404,
      statusMessage: `Mapping with ID '${id}' not found.`
    })
  }

  return {
    success: true,
    message: `Mapping '${id}' deleted successfully.`
  }
})
