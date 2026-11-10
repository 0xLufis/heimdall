import { defineEventHandler, createError } from 'h3'
import { revokeAutomationApiKey, deleteAutomationApiKey } from '../../../utils/automationApiKeysStore'

export default defineEventHandler((event) => {
  const id = event.context.params?.id

  if (!id) {
    throw createError({
      statusCode: 400,
      statusMessage: 'API Key ID is required.'
    })
  }

  const success = deleteAutomationApiKey(id) || revokeAutomationApiKey(id)

  if (!success) {
    throw createError({
      statusCode: 404,
      statusMessage: `API Key with ID '${id}' not found.`
    })
  }

  return {
    success: true,
    message: `API Key '${id}' revoked successfully.`
  }
})
