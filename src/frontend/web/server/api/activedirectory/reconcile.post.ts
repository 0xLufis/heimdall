import { defineEventHandler, readBody, createError } from 'h3'
import { reconcileRolesFromDirectory, getLastReconciliationResult, enforceReadOnlyAdPolicy, resetReconciliationState } from '../../../server/utils/adSyncReconciler'

export default defineEventHandler(async (event) => {
  const body = await readBody(event) || {}

  // Validate read-only policy enforcement
  if (!enforceReadOnlyAdPolicy('read')) {
    throw createError({
      statusCode: 403,
      statusMessage: 'AD write operations are prohibited. Heimdall enforces strict read-only directory synchronization.'
    })
  }

  const forceRefresh = body.forceRefresh === true
  const preserveLocalOverrides = body.preserveLocalOverrides !== false

  // Simulate upstream AD groups (in production, this would be fetched via LDAP/Graph API)
  const upstreamGroups = body.upstreamGroups || [
    {
      groupName: 'Factory Engineers',
      members: [
        { userId: 'usr-eng-001', userName: 'Istvan Kovacs' },
        { userId: 'usr-eng-002', userName: 'Anna Szabo' }
      ]
    },
    {
      groupName: 'Maintenance Team',
      members: [
        { userId: 'usr-tech-001', userName: 'Gabor Varga' },
        { userId: 'usr-tech-002', userName: 'Peter Nagy' }
      ]
    },
    {
      groupName: 'Plant Operators',
      members: [
        { userId: 'usr-op-001', userName: 'Zoltan Kiss' }
      ]
    }
  ]

  const localUsers = body.localUsers || [
    { userId: 'usr-eng-001', userName: 'Istvan Kovacs', currentRole: 'Engineer' },
    { userId: 'usr-eng-002', userName: 'Anna Szabo', currentRole: 'Operator' },
    { userId: 'usr-tech-001', userName: 'Gabor Varga', currentRole: 'Technician' },
    { userId: 'usr-tech-002', userName: 'Peter Nagy', currentRole: 'Engineer' },
    { userId: 'usr-op-001', userName: 'Zoltan Kiss', currentRole: 'Operator' }
  ]

  if (forceRefresh) {
    resetReconciliationState()
  }

  const result = reconcileRolesFromDirectory(upstreamGroups, localUsers, {
    forceRefresh,
    preserveLocalOverrides
  })

  return {
    success: true,
    reconciliation: result,
    policy: {
      mode: 'ReadOnly',
      writeBackProhibited: true,
      ssot: 'ActiveDirectory'
    }
  }
})
