import { setOuGovernance } from '../../../utils/activeDirectoryStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body || !body.ouPath) {
    throw createError({
      statusCode: 400,
      statusMessage: 'OU path must be specified.'
    })
  }

  const accessLevel = body.accessLevel || 'read_write'
  const approvedBy = body.approvedBy || 'it_admin'
  const notes = body.notes

  const record = setOuGovernance(body.ouPath, accessLevel, approvedBy, notes)
  return {
    success: true,
    governance: record,
    message: `OU '${body.ouPath}' governance updated to '${accessLevel}'.`
  }
})
