import { defineEventHandler, readBody, createError } from 'h3'
import { setUserCopiaKey, clearUserCopiaKey, maskApiKey } from '../../utils/copiaUserKeyStore'

export default defineEventHandler(async (event) => {
  const body = (await readBody(event).catch(() => null)) || (event as any)._body || (event.node?.req as any)?.body

  const userId = body?.userId || (event.context as any).user?.id || 'usr-default'
  const apiKey = (body?.apiKey || '').trim()

  if (body?.action === 'clear' || !apiKey) {
    clearUserCopiaKey(userId)
    return {
      success: true,
      hasKey: false,
      message: 'Personal Copia API key removed.'
    }
  }

  try {
    setUserCopiaKey(userId, apiKey)
    return {
      success: true,
      hasKey: true,
      maskedKey: maskApiKey(apiKey),
      message: 'Personal Copia API key saved securely. Ready for EULA-compliant cloud sync.'
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      statusMessage: err.message
    })
  }
})
